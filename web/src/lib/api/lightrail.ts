import { cached } from '$lib/utils/cache';

const ENDPOINT =
	'https://lightrail.microcosm.blue/xrpc/com.atproto.sync.listReposByCollection';
const CACHE_TTL = 30 * 60 * 1000;

interface ListReposResponse {
	cursor?: string;
	repos?: { did: string }[];
}

export async function listReposByCollection(collection: string): Promise<string[]> {
	return cached(`lightrail:${collection}`, CACHE_TTL, async () => {
		const url = `${ENDPOINT}?collection=${encodeURIComponent(collection)}`;
		const res = await fetch(url);
		if (!res.ok) throw new Error(`listReposByCollection failed: HTTP ${res.status}`);
		const body = (await res.json()) as ListReposResponse;
		return body.repos?.map((r) => r.did).filter(Boolean) ?? [];
	});
}
