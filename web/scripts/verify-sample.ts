// End-to-end check: read the signed JSON produced by
// tools/SigningSmokeTest and verify it with the browser-side verifier.
// Run with: node --experimental-strip-types web/scripts/verify-sample.ts
//
// Exits 0 on success; 1 on any assertion failure.

import { readFileSync } from 'node:fs';
import { verifyRecord } from '../src/lib/attestation/verify.ts';

const signedPath =
	process.argv[2] ??
	'/var/folders/cn/x01prt2d69gf01kxxl5_dw_40000gn/T/sts2-sig-smoke/signed.json';

const TEST_PUBLIC = 'did:key:zDnaezd72k6N6cNJZgYaKNo6zjUuySDZY5aJsr34xZhjf1veB';
const TEST_REPO = 'did:plc:test123';

const record = JSON.parse(readFileSync(signedPath, 'utf8')) as Record<string, unknown>;
const trustedKeys = new Set([TEST_PUBLIC]);

async function expect(label: string, promise: Promise<unknown>, want: 'verified' | 'invalid' | 'unsigned') {
	const result = (await promise) as { status: string; reason?: string };
	const ok = result.status === want;
	console.log(`${ok ? '✓' : '✗'} ${label}: ${result.status}${result.reason ? ` (${result.reason})` : ''}`);
	if (!ok) process.exit(1);
}

// 1. Untouched → verified.
await expect(
	'signed record verifies',
	verifyRecord({ record, repository: TEST_REPO, trustedKeys }),
	'verified'
);

// 2. Wrong repo DID → invalid (replay protection).
await expect(
	'wrong repository rejected',
	verifyRecord({ record, repository: 'did:plc:other456', trustedKeys }),
	'invalid'
);

// 3. Tampered field → invalid.
const tampered = JSON.parse(JSON.stringify(record)) as Record<string, unknown>;
tampered.score = 9999;
await expect(
	'tampered field rejected',
	verifyRecord({ record: tampered, repository: TEST_REPO, trustedKeys }),
	'invalid'
);

// 4. Record with no signatures → unsigned.
const unsigned = { ...record, signatures: [] } as Record<string, unknown>;
await expect(
	'missing signatures reported as unsigned',
	verifyRecord({ record: unsigned, repository: TEST_REPO, trustedKeys }),
	'unsigned'
);

// 5. Attestation whose key isn't in the trusted set → invalid.
await expect(
	'untrusted key rejected',
	verifyRecord({ record, repository: TEST_REPO, trustedKeys: new Set() }),
	'invalid'
);

console.log('all assertions passed.');
