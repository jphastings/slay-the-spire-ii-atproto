import type { ListRecordsResponse, GetRecordResponse } from './types';

export const COLLECTION = 'me.byjp.pesos.sts2.run';

export async function listRuns(
	pds: string,
	did: string,
	cursor?: string,
	limit = 100
): Promise<ListRecordsResponse> {
	const params = new URLSearchParams({
		repo: did,
		collection: COLLECTION,
		limit: String(limit),
		reverse: 'true'
	});
	if (cursor) params.set('cursor', cursor);

	const res = await fetch(`${pds}/xrpc/com.atproto.repo.listRecords?${params}`);
	if (!res.ok) throw new Error(`Failed to fetch runs: ${res.status}`);
	return res.json();
}

export async function getRun(
	pds: string,
	did: string,
	tid: string
): Promise<GetRecordResponse> {
	const params = new URLSearchParams({
		repo: did,
		collection: COLLECTION,
		rkey: tid
	});

	const res = await fetch(`${pds}/xrpc/com.atproto.repo.getRecord?${params}`);
	if (!res.ok) throw new Error(`Run not found`);
	return res.json();
}
