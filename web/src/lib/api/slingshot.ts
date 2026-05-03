import type { MiniDoc } from './types';
import { cached } from '$lib/utils/cache';

const ENDPOINT =
	'https://slingshot.microcosm.blue/xrpc/blue.microcosm.identity.resolveMiniDoc';
const CACHE_TTL = 30 * 60 * 1000;
const TIMEOUT_MS = 15_000;

/**
 * Thrown when Slingshot can't be reached — network failure, abort/timeout,
 * or 5xx response. Distinct from the "identifier not found" 4xx case so
 * pages can render an apology UI specifically for service outages.
 */
export class SlingshotUnavailableError extends Error {
	constructor(reason: string) {
		super(`Slingshot is currently unavailable: ${reason}`);
		this.name = 'SlingshotUnavailableError';
	}
}

export async function resolveIdentity(identifier: string): Promise<MiniDoc> {
	return cached(`slingshot:${identifier}`, CACHE_TTL, async () => {
		const url = `${ENDPOINT}?identifier=${encodeURIComponent(identifier)}`;
		let res: Response;
		try {
			res = await fetch(url, { signal: AbortSignal.timeout(TIMEOUT_MS) });
		} catch (e) {
			const reason =
				e instanceof DOMException && e.name === 'TimeoutError'
					? `request exceeded ${TIMEOUT_MS / 1000}s`
					: e instanceof Error
						? e.message
						: 'network error';
			throw new SlingshotUnavailableError(reason);
		}
		// 5xx → service problem; 4xx → bad identifier (caller's fault).
		if (res.status >= 500) {
			throw new SlingshotUnavailableError(`HTTP ${res.status}`);
		}
		if (!res.ok) {
			throw new Error(`Could not resolve "${identifier}" — check the handle or DID`);
		}
		return (await res.json()) as MiniDoc;
	});
}
