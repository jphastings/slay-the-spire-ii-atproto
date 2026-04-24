/**
 * Loads the mod's published signing-key manifest from /.well-known/sts2-mod-keys/keys.json
 * and exposes the trusted did:key set to the verifier. Cached per tab; the set
 * rarely rotates.
 */
export interface ModKeyEntry {
	id: string;
	publicKey: string; // did:key:zDn…
	algorithm: 'ES256';
	status: 'active' | 'retired';
	validFrom?: string;
	validTo?: string;
}

interface ModKeyManifest {
	keys: ModKeyEntry[];
}

const KEYS_URL = '/.well-known/sts2-mod-keys/keys.json';

let cached: Promise<ReadonlySet<string>> | null = null;

export function loadTrustedModKeys(): Promise<ReadonlySet<string>> {
	if (!cached) cached = fetchAndParse();
	return cached;
}

async function fetchAndParse(): Promise<ReadonlySet<string>> {
	const res = await fetch(KEYS_URL);
	if (!res.ok) throw new Error(`failed to fetch mod key manifest: ${res.status}`);
	const body = (await res.json()) as ModKeyManifest;
	if (!Array.isArray(body.keys)) throw new Error('mod key manifest missing keys[]');
	return new Set(body.keys.map((k) => k.publicKey));
}
