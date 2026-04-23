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

export interface ParsedDeckId {
	base: string;
	upgraded: boolean;
	enchantment?: string;
}

// parseDeckId splits a deck entry into its components. The mod emits:
//   "bash"              → plain
//   "bash+"             → upgraded
//   "bash/sharp"        → base + Sharp enchantment
//   "bash+/perfect_fit" → upgraded + Perfect Fit
export function parseDeckId(id: string): ParsedDeckId {
	const slash = id.indexOf('/');
	const before = slash < 0 ? id : id.slice(0, slash);
	const enchantment = slash < 0 ? undefined : id.slice(slash + 1);
	const isUp = before.endsWith('+');
	return {
		base: isUp ? before.slice(0, -1) : before,
		upgraded: isUp,
		enchantment
	};
}
