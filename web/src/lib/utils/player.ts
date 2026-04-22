import { cached } from './cache';
import { resolveIdentity } from '$lib/api/slingshot';
import { getRun } from '$lib/api/pds';

export interface Player {
	steam?: string;
	atproto?: string;
}

export interface ResolvedPlayer {
	label: string;
	subtitle?: string;
	href: string;
	external: boolean;
	avatar?: string;
}

const COMPANION_TTL = 5 * 60 * 1000;
const KEYTRACE_CLAIM_TTL = 24 * 60 * 60 * 1000;
const BSKY_AVATAR_TTL = 24 * 60 * 60 * 1000;

export function steamProfileUrl(steamId: string): string {
	return `https://steamcommunity.com/profiles/${steamId}`;
}

// --- Keytrace claim on the user's own PDS -------------------------------------

export interface KeytraceClaim {
	displayName?: string;
	avatarUrl?: string;
	/** Only populated when there's a `type: "steam"` claim record. */
	steamId64?: string;
}

export async function fetchKeytraceClaim(
	pds: string,
	did: string
): Promise<KeytraceClaim | null> {
	return cached(`keytrace-claim:${did}`, KEYTRACE_CLAIM_TTL, async () => {
		try {
			const url =
				`${pds}/xrpc/com.atproto.repo.listRecords` +
				`?repo=${encodeURIComponent(did)}` +
				`&collection=dev.keytrace.claim&limit=10`;
			const res = await fetch(url);
			if (!res.ok) return null;
			const body = (await res.json()) as {
				records?: {
					value?: {
						type?: unknown;
						identity?: {
							subject?: unknown;
							displayName?: unknown;
							avatarUrl?: unknown;
						};
					};
				}[];
			};
			const claim: KeytraceClaim = {};
			for (const r of body.records ?? []) {
				const v = r.value ?? {};
				const id = v.identity ?? {};
				if (!claim.displayName && typeof id.displayName === 'string' && id.displayName.length > 0) {
					claim.displayName = id.displayName;
				}
				if (!claim.avatarUrl && typeof id.avatarUrl === 'string' && id.avatarUrl.length > 0) {
					claim.avatarUrl = id.avatarUrl;
				}
				if (
					!claim.steamId64 &&
					v.type === 'steam' &&
					typeof id.subject === 'string' &&
					id.subject.length > 0
				) {
					claim.steamId64 = id.subject;
				}
			}
			return claim.displayName || claim.avatarUrl || claim.steamId64 ? claim : null;
		} catch {
			return null;
		}
	});
}

// --- Bluesky profile avatar (from app.bsky.actor.profile/self on their PDS) ---

async function fetchBlueskyAvatar(pds: string, did: string): Promise<string | null> {
	return cached(`bsky-avatar:${did}`, BSKY_AVATAR_TTL, async () => {
		try {
			const url =
				`${pds}/xrpc/com.atproto.repo.getRecord` +
				`?repo=${encodeURIComponent(did)}` +
				`&collection=app.bsky.actor.profile&rkey=self`;
			const res = await fetch(url);
			if (!res.ok) return null;
			const body = (await res.json()) as {
				value?: { avatar?: { ref?: { $link?: string } } };
			};
			const cid = body.value?.avatar?.ref?.$link;
			if (!cid) return null;
			return `https://cdn.bsky.app/img/avatar_thumbnail/plain/${did}/${cid}@jpeg`;
		} catch {
			return null;
		}
	});
}

async function hasCompanionRun(pds: string, did: string, tid: string): Promise<boolean> {
	return cached(`companion:${did}:${tid}`, COMPANION_TTL, async () => {
		try {
			await getRun(pds, did, tid);
			return true;
		} catch {
			return false;
		}
	});
}

// --- Main resolver ------------------------------------------------------------

export interface ResolveOptions {
	preferLocal?: boolean;
	/** Force the external Steam profile link, overriding companion/local routing. */
	preferSteam?: boolean;
}

export async function resolvePlayer(
	player: Player,
	tid?: string,
	opts: ResolveOptions = {}
): Promise<ResolvedPlayer> {
	const { preferLocal = false, preferSteam = false } = opts;
	let did: string | undefined;
	let handle: string | undefined;
	let pds: string | undefined;
	let companionExists = false;

	if (player.atproto) {
		did = player.atproto;
		try {
			const doc = await resolveIdentity(did);
			handle = doc.handle;
			pds = doc.pds;
			if (tid) companionExists = await hasCompanionRun(pds, did, tid);
		} catch {
			/* DID failed to resolve — treat as Steam-only */
		}
	}

	// Keytrace claim gives us both the display name and the preferred avatar.
	const claim = did && pds ? await fetchKeytraceClaim(pds, did) : null;

	// Avatar priority: keytrace claim → Bluesky profile. (Steam avatars would
	// require a third-party CORS proxy we're not willing to depend on.)
	let avatar: string | undefined = claim?.avatarUrl;
	if (!avatar && did && pds) {
		avatar = (await fetchBlueskyAvatar(pds, did)) ?? undefined;
	}

	// Three display states:
	//   1. keytrace displayName + handle → "{displayName}" / "@{handle}"
	//   2. handle, no displayName        → "@{handle}"
	//   3. Steam only                    → "Steam player" / "{steamId}"
	let label: string;
	let subtitle: string | undefined;
	if (claim?.displayName && handle) {
		label = claim.displayName;
		subtitle = `@${handle}`;
	} else if (handle) {
		label = `@${handle}`;
	} else {
		label = 'Steam player';
		subtitle = player.steam;
	}

	// preferSteam short-circuits the local/companion routing when we have a
	// Steam ID — useful on run pages where the Steam profile is the intended jump.
	if (preferSteam && player.steam) {
		return { label, subtitle, href: steamProfileUrl(player.steam), external: true, avatar };
	}
	if (handle && companionExists && tid) {
		return { label, subtitle, href: `/${handle}/${tid}`, external: false, avatar };
	}
	if (handle && (preferLocal || !player.steam)) {
		return { label, subtitle, href: `/${handle}`, external: false, avatar };
	}
	if (player.steam) {
		return { label, subtitle, href: steamProfileUrl(player.steam), external: true, avatar };
	}
	// Should be unreachable per the "at least one key" contract.
	return { label: 'Unknown', href: '#', external: false, avatar };
}
