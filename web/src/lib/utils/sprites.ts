// Sprite-sheet manifests for the relic, potion and card-portrait sheets
// emitted by the Go extractor (see extract/sprite). Each sheet is one
// WebP whose tiles are indexed by the items[] array in the accompanying
// JSON manifest. The manifests are imported statically so vite bundles
// them with the JS — no runtime fetches.
//
// Lookups use asset filename stems (snake_case relic/potion/card ids),
// matching the lexicon suffix. Strip the namespace prefix
// (`RELIC.`/`POTION.`/`CARD.`) before lookup — see baseName / normaliseId.

import relicsManifest from '$lib/data/relics.json';
import potionsManifest from '$lib/data/potions.json';
import colorlessPortraits from '$lib/data/portraits/colorless.json';
import cursePortraits from '$lib/data/portraits/curse.json';
import defectPortraits from '$lib/data/portraits/defect.json';
import eventPortraits from '$lib/data/portraits/event.json';
import ironcladPortraits from '$lib/data/portraits/ironclad.json';
import necrobinderPortraits from '$lib/data/portraits/necrobinder.json';
import questPortraits from '$lib/data/portraits/quest.json';
import regentPortraits from '$lib/data/portraits/regent.json';
import silentPortraits from '$lib/data/portraits/silent.json';
import statusPortraits from '$lib/data/portraits/status.json';
import tokenPortraits from '$lib/data/portraits/token.json';

interface RawManifest {
	image: string;
	tileW?: number;
	tileH?: number;
	tile?: number;
	columns: number;
	items: string[];
}

export interface SpriteSheet {
	/** Grid columns. */
	cols: number;
	/** Grid rows, computed from items.length / cols. */
	rows: number;
	/** URL of the webp sheet. */
	url: string;
	/** id → grid index (column-major position = index % cols, row = index / cols). */
	indexOf(id: string): number | undefined;
}

function build(m: RawManifest, webpUrl: string): SpriteSheet {
	const index: Record<string, number> = {};
	m.items.forEach((id, i) => (index[id] = i));
	return {
		cols: m.columns,
		rows: Math.ceil(m.items.length / m.columns),
		url: webpUrl,
		indexOf: (id) => index[id]
	};
}

export const relicsSheet: SpriteSheet = build(relicsManifest, '/assets/relics_sprite.webp');
export const potionsSheet: SpriteSheet = build(potionsManifest, '/assets/potions_sprite.webp');

const portraitSheets: Record<string, SpriteSheet> = {
	colorless: build(colorlessPortraits, '/assets/card_portraits/colorless.webp'),
	curse: build(cursePortraits, '/assets/card_portraits/curse.webp'),
	defect: build(defectPortraits, '/assets/card_portraits/defect.webp'),
	event: build(eventPortraits, '/assets/card_portraits/event.webp'),
	ironclad: build(ironcladPortraits, '/assets/card_portraits/ironclad.webp'),
	necrobinder: build(necrobinderPortraits, '/assets/card_portraits/necrobinder.webp'),
	quest: build(questPortraits, '/assets/card_portraits/quest.webp'),
	regent: build(regentPortraits, '/assets/card_portraits/regent.webp'),
	silent: build(silentPortraits, '/assets/card_portraits/silent.webp'),
	status: build(statusPortraits, '/assets/card_portraits/status.webp'),
	token: build(tokenPortraits, '/assets/card_portraits/token.webp')
};

export function portraitSheet(character: string): SpriteSheet | undefined {
	return portraitSheets[character];
}

/**
 * CSS custom properties that pin one sprite tile into a square container.
 * The container is expected to declare `--size` (e.g. 3rem) and wire it
 * through `background-size` / `background-position` with
 * `calc(-1 * var(--col) * var(--size))`, etc.
 */
export function spriteStyle(sheet: SpriteSheet, id: string): string | null {
	const idx = sheet.indexOf(id);
	if (idx === undefined) return null;
	const col = idx % sheet.cols;
	const row = Math.floor(idx / sheet.cols);
	return `--col: ${col}; --row: ${row}; --cols: ${sheet.cols}; --rows: ${sheet.rows}; --sprite: url('${sheet.url}');`;
}
