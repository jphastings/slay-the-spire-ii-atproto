import { ensureLoaded } from '$lib/utils/names';

export const ssr = false;
export const prerender = false;

export async function load() {
	await ensureLoaded();
}
