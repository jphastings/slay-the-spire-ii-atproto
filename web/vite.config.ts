import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';

// Single source of truth for the mod version: mod/atproto-tracker.csproj <Version>.
function readModVersion(): string {
	const csproj = readFileSync(resolve(import.meta.dirname, '../mod/atproto-tracker.csproj'), 'utf8');
	const match = csproj.match(/<Version>([^<]+)<\/Version>/);
	if (!match) throw new Error('could not find <Version> in mod/atproto-tracker.csproj');
	return match[1];
}

export default defineConfig({
	plugins: [sveltekit()],
	define: {
		__MOD_VERSION__: JSON.stringify(readModVersion())
	}
});
