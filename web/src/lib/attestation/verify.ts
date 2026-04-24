import * as dagCbor from '@ipld/dag-cbor';
import { CID } from 'multiformats/cid';
import { sha256 } from 'multiformats/hashes/sha2';
import { base58btc } from 'multiformats/bases/base58';
import { p256 } from '@noble/curves/nist.js';

/**
 * Subset of the badge.blue CID-first inline-attestation verifier that the
 * mod emits. Remote (strongRef) attestations aren't produced by the mod;
 * if encountered they're treated as unverifiable.
 *
 * The verifier is stricter than the Rust reference in two ways:
 *   - only P-256 is accepted (the mod only signs P-256)
 *   - the attestation's `key` field must appear in the trusted key set
 *     (the set published at /.well-known/sts2-mod-keys/keys.json)
 */
export type VerifyResult =
	| { status: 'verified'; key: string }
	| { status: 'unsigned' }
	| { status: 'invalid'; reason: string };

export interface VerifyOptions {
	record: Record<string, unknown>;
	repository: string;
	trustedKeys: ReadonlySet<string>;
}

const STRONG_REF = 'com.atproto.repo.strongRef';

export async function verifyRecord(opts: VerifyOptions): Promise<VerifyResult> {
	const { record, repository, trustedKeys } = opts;
	const signatures = Array.isArray(record.signatures) ? record.signatures : [];
	if (signatures.length === 0) return { status: 'unsigned' };

	for (const entry of signatures) {
		if (!entry || typeof entry !== 'object') {
			return { status: 'invalid', reason: 'signature entry is not an object' };
		}
		const $type = (entry as { $type?: unknown }).$type;
		if (typeof $type !== 'string') {
			return { status: 'invalid', reason: 'signature entry missing $type' };
		}
		if ($type === STRONG_REF) {
			return { status: 'invalid', reason: 'remote (strongRef) attestations not supported' };
		}

		const result = await verifyInline(entry as InlineEntry, record, repository, trustedKeys);
		if (result.status !== 'verified') return result;
	}
	// All entries verified. Report the last key as the one in use.
	const last = signatures[signatures.length - 1] as InlineEntry;
	return { status: 'verified', key: last.key };
}

interface InlineEntry {
	$type: string;
	key: string;
	cid: string;
	signature: { $bytes: string };
	[k: string]: unknown;
}

async function verifyInline(
	entry: InlineEntry,
	record: Record<string, unknown>,
	repository: string,
	trustedKeys: ReadonlySet<string>
): Promise<VerifyResult> {
	if (typeof entry.key !== 'string') return { status: 'invalid', reason: 'attestation missing key' };
	if (!trustedKeys.has(entry.key)) {
		return { status: 'invalid', reason: `key ${entry.key} is not in the trusted set` };
	}
	if (typeof entry.cid !== 'string') return { status: 'invalid', reason: 'attestation missing cid' };
	const sigB64 = entry.signature?.$bytes;
	if (typeof sigB64 !== 'string') {
		return { status: 'invalid', reason: 'attestation missing signature.$bytes' };
	}

	// Re-build the signing-time metadata (drop cid + signature) and recompute the CID.
	const { cid: _cid, signature: _sig, ...meta } = entry;
	const computed = await computeContentCid(record, meta, repository);
	if (computed.toString() !== entry.cid) {
		return { status: 'invalid', reason: `CID mismatch (claimed=${entry.cid} computed=${computed})` };
	}

	let publicKey: Uint8Array;
	try {
		publicKey = parseP256DidKey(entry.key);
	} catch (err) {
		return { status: 'invalid', reason: `unusable key: ${(err as Error).message}` };
	}

	const sigBytes = base64Decode(sigB64);
	if (sigBytes.length !== 64) {
		return { status: 'invalid', reason: `signature must be 64 bytes (got ${sigBytes.length})` };
	}
	// Match the Rust reference: don't require low-S on verify (it's enforced at sign time).
	const ok = p256.verify(sigBytes, computed.bytes, publicKey, { lowS: false, prehash: true });
	if (!ok) return { status: 'invalid', reason: 'ECDSA signature verification failed' };
	return { status: 'verified', key: entry.key };
}

export async function computeContentCid(
	record: Record<string, unknown>,
	metadata: Record<string, unknown>,
	repository: string
): Promise<CID> {
	if (!record || typeof record !== 'object') throw new Error('record must be an object');
	if (!metadata || typeof metadata !== 'object') throw new Error('metadata must be an object');
	if (typeof record.$type !== 'string' || !record.$type) throw new Error('record missing $type');
	if (typeof metadata.$type !== 'string' || !metadata.$type) throw new Error('metadata missing $type');

	const { signatures: _s, ...strippedRecord } = record;
	const { cid: _c, signature: _sg, ...sig } = metadata;
	(sig as Record<string, unknown>).repository = repository;

	const merged: Record<string, unknown> = { ...strippedRecord, $sig: sig };
	const bytes = dagCbor.encode(merged);
	const digest = await sha256.digest(bytes);
	return CID.createV1(dagCbor.code, digest);
}

// Parse a P-256 did:key (multibase base58btc, multicodec 0x1200 prefix).
function parseP256DidKey(didKey: string): Uint8Array {
	const scheme = 'did:key:';
	if (!didKey.startsWith(scheme)) throw new Error('not a did:key');
	const mb = didKey.slice(scheme.length);
	if (!mb.startsWith('z')) throw new Error('only base58btc multibase supported');
	const bytes = base58btc.decode(mb);
	if (bytes.length < 2 || bytes[0] !== 0x80 || bytes[1] !== 0x24) {
		throw new Error('only P-256 public keys supported');
	}
	return bytes.subarray(2);
}

function base64Decode(s: string): Uint8Array {
	const bin = atob(s);
	const out = new Uint8Array(bin.length);
	for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
	return out;
}
