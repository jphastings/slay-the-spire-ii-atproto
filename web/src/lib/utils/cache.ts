const PREFIX = 'sts2-cache:';
const mem = new Map<string, { value: unknown; expiresAt: number }>();

export async function cached<T>(
	key: string,
	ttlMs: number,
	fetcher: () => Promise<T>
): Promise<T> {
	const fullKey = PREFIX + key;
	const now = Date.now();

	const hit = mem.get(fullKey);
	if (hit && now < hit.expiresAt) return hit.value as T;

	if (typeof localStorage !== 'undefined') {
		const raw = localStorage.getItem(fullKey);
		if (raw) {
			try {
				const parsed = JSON.parse(raw) as { value: T; expiresAt: number };
				if (now < parsed.expiresAt) {
					mem.set(fullKey, parsed);
					return parsed.value;
				}
			} catch {
				/* malformed — fall through to refetch */
			}
		}
	}

	const value = await fetcher();
	const entry = { value, expiresAt: now + ttlMs };
	mem.set(fullKey, entry);
	if (typeof localStorage !== 'undefined') {
		try {
			localStorage.setItem(fullKey, JSON.stringify(entry));
		} catch {
			/* quota exceeded or unserializable */
		}
	}
	return value;
}
