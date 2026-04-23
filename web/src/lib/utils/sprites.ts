// Sprite-sheet loaders for the relic and potion icon sheets emitted by
// the Go extractor (see extract/sprite). Each sheet is one WebP whose
// tiles are indexed by the items[] array in the accompanying JSON.
//
// The web sprites are referenced by asset filename stems (snake_case
// relic/potion ids), matching the lexicon suffix. baseName() strips the
// `RELIC.`/`POTION.` namespace prefix before lookup.

export interface SpriteManifest {
	image: string;
	tile: number;
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

async function load(jsonPath: string, webpPath: string): Promise<SpriteSheet> {
	const res = await fetch(jsonPath);
	const m: SpriteManifest = await res.json();
	const index: Record<string, number> = {};
	m.items.forEach((id, i) => (index[id] = i));
	return {
		cols: m.columns,
		rows: Math.ceil(m.items.length / m.columns),
		url: webpPath,
		indexOf: (id) => index[id]
	};
}

let relicsSheet: SpriteSheet | null = null;
let potionsSheet: SpriteSheet | null = null;
let relicsPromise: Promise<SpriteSheet> | null = null;
let potionsPromise: Promise<SpriteSheet> | null = null;

export async function ensureRelicsLoaded(): Promise<SpriteSheet> {
	if (relicsSheet) return relicsSheet;
	if (!relicsPromise)
		relicsPromise = load('/assets/relics_sprite.json', '/assets/relics_sprite.webp').then((s) => {
			relicsSheet = s;
			return s;
		});
	return relicsPromise;
}

export async function ensurePotionsLoaded(): Promise<SpriteSheet> {
	if (potionsSheet) return potionsSheet;
	if (!potionsPromise)
		potionsPromise = load('/assets/potions_sprite.json', '/assets/potions_sprite.webp').then(
			(s) => {
				potionsSheet = s;
				return s;
			}
		);
	return potionsPromise;
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
