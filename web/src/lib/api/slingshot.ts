import type { MiniDoc } from './types';

const ENDPOINT =
	'https://slingshot.microcosm.blue/xrpc/blue.microcosm.identity.resolveMiniDoc';
const CACHE_TTL = 5 * 60 * 1000;

const cache = new Map<string, { doc: MiniDoc; expiresAt: number }>();

export async function resolveIdentity(identifier: string): Promise<MiniDoc> {
	const cached = cache.get(identifier);
	if (cached && Date.now() < cached.expiresAt) return cached.doc;

	const url = `${ENDPOINT}?identifier=${encodeURIComponent(identifier)}`;
	const res = await fetch(url);
	if (!res.ok) {
		throw new Error(`Could not resolve "${identifier}" — check the handle or DID`);
	}

	const doc: MiniDoc = await res.json();
	const expiresAt = Date.now() + CACHE_TTL;
	cache.set(identifier, { doc, expiresAt });
	cache.set(doc.did, { doc, expiresAt });
	cache.set(doc.handle, { doc, expiresAt });

	return doc;
}
