import { defineConfig } from 'vitest/config';

// Standalone config (no SvelteKit plugin) — the units under test are plain
// modules, so we skip the kit/vite pipeline for fast, isolated runs.
export default defineConfig({
	test: {
		include: ['src/**/*.{test,spec}.ts'],
		environment: 'node'
	}
});
