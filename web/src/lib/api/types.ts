import type { sts2Run } from '$lib/lexicons/sts2-run';

export type RunRecord = (typeof sts2Run)['~infer'];
export type Outcome = RunRecord['outcome'];

export interface MiniDoc {
	did: string;
	handle: string;
	pds: string;
}

export interface RecordEntry {
	uri: string;
	cid: string;
	value: RunRecord;
}

export interface ListRecordsResponse {
	cursor?: string;
	records: RecordEntry[];
}

export interface GetRecordResponse {
	uri: string;
	cid: string;
	value: RunRecord;
}
