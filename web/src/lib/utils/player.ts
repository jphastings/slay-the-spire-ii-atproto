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

const STEAM_INFO_TTL = 24 * 60 * 60 * 1000;
const COMPANION_TTL = 5 * 60 * 1000;
const KEYTRACE_AVATAR_TTL = 24 * 60 * 60 * 1000;
const BSKY_AVATAR_TTL = 24 * 60 * 60 * 1000;

export function steamProfileUrl(steamId: string): string {
	return `https://steamcommunity.com/profiles/${steamId}`;
}

// --- Steam XML (via corsproxy; Steam doesn't send CORS headers) ---------------

interface SteamInfo {
	name?: string;
	avatar?: string;
}

async function fetchSteamInfo(steamId: string): Promise<SteamInfo | null> {
	return cached(`steam-info:${steamId}`, STEAM_INFO_TTL, async () => {
		const target = `https://steamcommunity.com/profiles/${steamId}?xml=1`;
		const proxied = `https://corsproxy.io/?url=${encodeURIComponent(target)}`;
		const res = await fetch(proxied);
		if (!res.ok) return null;
		const xml = await res.text();
		const doc = new DOMParser().parseFromString(xml, 'application/xml');
		if (doc.querySelector('parsererror')) return null;
		const pick = (tag: string) => doc.querySelector(tag)?.textContent?.trim() || null;
		return {
			// Priority: public display name → the URL slug they picked for their profile.
			name: pick('steamID') ?? pick('customURL') ?? undefined,
			avatar: pick('avatarMedium') ?? undefined
		};
	});
}

// --- Keytrace claim on the user's own PDS -------------------------------------

async function fetchKeytraceAvatar(pds: string, did: string): Promise<string | null> {
	return cached(`keytrace-avatar:${did}`, KEYTRACE_AVATAR_TTL, async () => {
		try {
			const url =
				`${pds}/xrpc/com.atproto.repo.listRecords` +
				`?repo=${encodeURIComponent(did)}` +
				`&collection=dev.keytrace.claim&limit=10`;
			const res = await fetch(url);
			if (!res.ok) return null;
			const body = (await res.json()) as { records?: { value?: { avatarUrl?: string } }[] };
			for (const r of body.records ?? []) {
				if (typeof r.value?.avatarUrl === 'string' && r.value.avatarUrl.length > 0) {
					return r.value.avatarUrl;
				}
			}
			return null;
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

export async function resolvePlayer(
	player: Player,
	tid?: string,
	preferLocal = false
): Promise<ResolvedPlayer> {
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

	const steamInfo = player.steam ? await fetchSteamInfo(player.steam) : null;

	// Avatar priority: keytrace claim → Steam XML → Bluesky profile.
	let avatar: string | undefined;
	if (did && pds) {
		avatar = (await fetchKeytraceAvatar(pds, did)) ?? undefined;
	}
	if (!avatar && steamInfo?.avatar) {
		avatar = steamInfo.avatar;
	}
	if (!avatar && did && pds) {
		avatar = (await fetchBlueskyAvatar(pds, did)) ?? undefined;
	}

	// Link target + label.
	// Label priority when we already know we're linking to a local profile: prefer
	// the Steam display name (or customURL) when present, otherwise fall back to
	// the atproto handle.
	const localLabel = steamInfo?.name ?? `@${handle}`;
	const localSubtitle = handle && localLabel !== `@${handle}` ? `@${handle}` : undefined;

	if (handle && companionExists && tid) {
		return {
			label: localLabel,
			subtitle: localSubtitle,
			href: `/${handle}/${tid}`,
			external: false,
			avatar
		};
	}
	if (handle && (preferLocal || !player.steam)) {
		return {
			label: localLabel,
			subtitle: localSubtitle,
			href: `/${handle}`,
			external: false,
			avatar
		};
	}
	if (player.steam) {
		return {
			label: steamInfo?.name ?? `Steam #${player.steam}`,
			subtitle: handle ? `@${handle}` : undefined,
			href: steamProfileUrl(player.steam),
			external: true,
			avatar
		};
	}
	// Should be unreachable per the "at least one key" contract.
	return { label: 'Unknown', href: '#', external: false, avatar };
}
