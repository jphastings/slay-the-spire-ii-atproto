// Lazy-loads /cards/cards.json (emitted by extract/cards/metadata.go from
// the decompiled sts2.dll) and exposes a per-id lookup for type, rarity,
// cost, and character. Unknown ids return undefined so callers can fall
// back to a minimal rendering.

export type CardType = 'attack' | 'skill' | 'power' | string;
export type CardRarity =
	| 'basic'
	| 'common'
	| 'uncommon'
	| 'rare'
	| 'curse'
	| 'status'
	| 'event'
	| 'quest'
	| 'ancient'
	| string;

export interface CardMeta {
	id: string;
	class?: string;
	character?: string;
	cost: string;
	type: CardType;
	rarity: CardRarity;
	title?: string;
	description?: string;
}

let byId: Record<string, CardMeta> | null = null;
let loading: Promise<void> | null = null;

async function load(): Promise<void> {
	const res = await fetch('/cards/cards.json');
	const arr: CardMeta[] = await res.json();
	const map: Record<string, CardMeta> = {};
	for (const c of arr) map[c.id] = c;
	byId = map;
}

export async function ensureCardsLoaded(): Promise<void> {
	if (byId) return;
	if (!loading) loading = load();
	return loading;
}

export function cardMeta(id: string): CardMeta | undefined {
	return byId?.[normaliseId(id)];
}

// Strips the lexicon namespace (CARD.) so "CARD.BASH" → "bash".
export function normaliseId(id: string): string {
	const name = id.includes('.') ? id.slice(id.indexOf('.') + 1) : id;
	return name.toLowerCase();
}
