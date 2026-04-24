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
import orbManifest from '$lib/data/orb.json';
import enchantManifest from '$lib/data/enchant.json';
import charactersManifest from '$lib/data/characters.json';
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

interface RawPackedManifest {
	image: string;
	width: number;
	height: number;
	items: Record<string, { x: number; y: number; w: number; h: number }>;
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

export interface PackedSheet {
	url: string;
	width: number;
	height: number;
	rectOf(id: string): { x: number; y: number; w: number; h: number } | undefined;
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

function buildPacked(m: RawPackedManifest, webpUrl: string): PackedSheet {
	return {
		url: webpUrl,
		width: m.width,
		height: m.height,
		rectOf: (id) => m.items[id]
	};
}

export const relicsSheet: SpriteSheet = build(relicsManifest, '/assets/relics_sprite.webp');
export const potionsSheet: SpriteSheet = build(potionsManifest, '/assets/potions_sprite.webp');
export const orbSheet: PackedSheet = buildPacked(orbManifest, '/assets/orb_sprite.webp');
export const enchantSheet: PackedSheet = buildPacked(enchantManifest, '/assets/enchant_sprite.webp');
export const charactersSheet: SpriteSheet = build(
	charactersManifest,
	'/assets/characters_sprite.webp'
);

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

/**
 * Packed-sheet equivalent of spriteStyle: emits background-image +
 * background-size + background-position sized so the container's box
 * is filled by exactly the named tile. Works for containers whose
 * aspect matches the source tile; for other aspects the sprite is
 * stretched exactly like a raw <img> would be.
 */
export function packedSpriteStyle(sheet: PackedSheet, id: string): string | null {
	const r = sheet.rectOf(id);
	if (!r) return null;
	// background-size (as %): scale the sheet so the tile's width/height
	// map to 100% of the container.
	const bgW = (sheet.width / r.w) * 100;
	const bgH = (sheet.height / r.h) * 100;
	// background-position (as %): align the tile's top-left with the
	// container origin. When the scaled image is `bgW%` wide, 100%
	// background-position corresponds to (bgW - 100)% of the container
	// — i.e. the image overhangs by that amount. The fraction of that
	// overhang we want is r.x / (sheet.width - r.w).
	const posX = sheet.width === r.w ? 0 : (r.x / (sheet.width - r.w)) * 100;
	const posY = sheet.height === r.h ? 0 : (r.y / (sheet.height - r.h)) * 100;
	return `background-image: url('${sheet.url}'); background-size: ${bgW}% ${bgH}%; background-position: ${posX}% ${posY}%; background-repeat: no-repeat;`;
}
