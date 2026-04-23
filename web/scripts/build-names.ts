/**
 * Extracts display names from StS2 localization files and writes a mapping
 * from game IDs (as they appear in AT Protocol records) to human-readable names.
 *
 * Usage: node --experimental-strip-types scripts/build-names.ts <localization-dir>
 *   eg:  node scripts/build-names.ts ~/Downloads/sts2/localization/eng
 */

import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const locDir = process.argv[2];
if (!locDir) {
	console.error('Usage: node scripts/build-names.ts <localization-eng-dir>');
	process.exit(1);
}

function loadJson(file: string): Record<string, string> {
	return JSON.parse(readFileSync(join(locDir, file), 'utf-8'));
}

// names.json lives in src/lib/data so vite bundles it into the JS chunks
// instead of fetching it at runtime — saves a round trip on every page.
const outPath = join(import.meta.dirname, '..', 'src', 'lib', 'data', 'names.json');

// Start from the existing names.json so deprecated entries are preserved for
// old runs that still reference them.
const names: Record<string, string> = existsSync(outPath)
	? JSON.parse(readFileSync(outPath, 'utf-8'))
	: {};
const before = Object.keys(names).length;

function extractTitles(
	data: Record<string, string>,
	titleKey: string,
	prefix: string
): Record<string, string> {
	const result: Record<string, string> = {};
	const suffix = `.${titleKey}`;
	for (const [key, value] of Object.entries(data)) {
		if (key.endsWith(suffix)) {
			const id = key.slice(0, -suffix.length);
			result[`${prefix}${id}`] = value;
		}
	}
	return result;
}

// Cards: CARD.BASH → "Bash"
Object.assign(names, extractTitles(loadJson('cards.json'), 'title', 'CARD.'));

// Relics: RELIC.BURNING_BLOOD → "Burning Blood"
Object.assign(names, extractTitles(loadJson('relics.json'), 'title', 'RELIC.'));

// Characters: CHARACTER.IRONCLAD → "The Ironclad"
Object.assign(names, extractTitles(loadJson('characters.json'), 'title', 'CHARACTER.'));

// Potions: POTION.BLOOD_POTION → "Blood Potion"
Object.assign(names, extractTitles(loadJson('potions.json'), 'title', 'POTION.'));

// Monsters (for killedBy): use .name key, no prefix (format TBD)
const monsters = loadJson('monsters.json');
for (const [key, value] of Object.entries(monsters)) {
	if (key.endsWith('.name')) {
		const id = key.slice(0, -'.name'.length);
		names[id] = value;
	}
}

// Sort keys for stable output
const sorted = Object.fromEntries(
	Object.entries(names).sort(([a], [b]) => a.localeCompare(b))
);

writeFileSync(outPath, JSON.stringify(sorted, null, '\t') + '\n');

const after = Object.keys(sorted).length;
console.log(
	`Wrote ${after} names to src/lib/data/names.json (was ${before}, +${after - before})`
);
