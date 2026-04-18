import type { MiniDoc } from './types';
import { cached } from '$lib/utils/cache';

const ENDPOINT =
	'https://slingshot.microcosm.blue/xrpc/blue.microcosm.identity.resolveMiniDoc';
const CACHE_TTL = 30 * 60 * 1000;

export async function resolveIdentity(identifier: string): Promise<MiniDoc> {
	return cached(`slingshot:${identifier}`, CACHE_TTL, async () => {
		const url = `${ENDPOINT}?identifier=${encodeURIComponent(identifier)}`;
		const res = await fetch(url);
		if (!res.ok) {
			throw new Error(`Could not resolve "${identifier}" — check the handle or DID`);
		}
		return (await res.json()) as MiniDoc;
	});
}
