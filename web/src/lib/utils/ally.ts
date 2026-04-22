import { cached } from './cache';
import { resolveIdentity } from '$lib/api/slingshot';
import { getRun } from '$lib/api/pds';

export interface Ally {
	steam: string;
	atproto?: string;
}

export interface ResolvedAlly {
	steamId: string;
	// Best-effort label for the Steam profile link (steamID → realName → customURL).
	steamName?: string;
	// Handle + DID if the ally has an atproto identity.
	did?: string;
	handle?: string;
	// Populated iff a companion run record exists at the same tid in the ally's repo.
	companionHref?: string;
}

const STEAM_NAME_TTL = 24 * 60 * 60 * 1000; // 24h
const COMPANION_TTL = 5 * 60 * 1000; // 5min — run records can arrive late

export function steamProfileUrl(steamId: string): string {
	return `https://steamcommunity.com/profiles/${steamId}`;
}

// Fetches a Steam profile's public XML and extracts a display name.
// Browser CORS blocks steamcommunity.com directly, so we route through a
// public proxy; if that fails we return null and the caller falls back to the
// raw SteamID.
async function fetchSteamName(steamId: string): Promise<string | null> {
	return cached(`steam-name:${steamId}`, STEAM_NAME_TTL, async () => {
		const target = `https://steamcommunity.com/profiles/${steamId}?xml=1`;
		const proxied = `https://corsproxy.io/?url=${encodeURIComponent(target)}`;
		const res = await fetch(proxied);
		if (!res.ok) return null;
		const xml = await res.text();
		const doc = new DOMParser().parseFromString(xml, 'application/xml');
		if (doc.querySelector('parsererror')) return null;
		// Priority: steamID (display name) → realName → customURL.
		const pick = (tag: string) => doc.querySelector(tag)?.textContent?.trim() || null;
		return pick('steamID') ?? pick('realName') ?? pick('customURL');
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

export async function resolveAlly(ally: Ally, tid: string): Promise<ResolvedAlly> {
	const out: ResolvedAlly = { steamId: ally.steam };

	if (ally.atproto) {
		out.did = ally.atproto;
		try {
			const doc = await resolveIdentity(ally.atproto);
			out.handle = doc.handle;
			if (await hasCompanionRun(doc.pds, doc.did, tid)) {
				out.companionHref = `/${doc.handle}/${tid}`;
			}
		} catch {
			/* DID failed to resolve — treat as Steam-only ally */
		}
	}

	// Only fetch the Steam name when we'll actually render a Steam link.
	if (!out.companionHref) {
		try {
			out.steamName = (await fetchSteamName(ally.steam)) ?? undefined;
		} catch {
			/* leave undefined — render falls back to "Steam user" */
		}
	}

	return out;
}
