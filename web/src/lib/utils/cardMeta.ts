// Card metadata emitted by extract/cards/metadata.go from the decompiled
// sts2.dll. Bundled into the JS at build time (see web/src/lib/data) so
// callers can look ids up synchronously — no fetch round trip.

import cardsData from '$lib/data/cards.json';

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
	// Regent "star cost" — a second resource some Regent cards cost on top
	// of (or instead of) energy. Absent/0 means the card has no star cost.
	starCost?: number;
	type: CardType;
	rarity: CardRarity;
	title?: string;
	description?: string;
	// Default values for {Field:diff()} placeholders in the description.
	// Emitted by the extractor from each card's CanonicalVars.
	vars?: Record<string, number>;
	// Values after upgrading. Omitted when the upgrade doesn't change
	// any dynamic var (e.g. upgrades that only add keywords).
	upgradedVars?: Record<string, number>;
}

const byId: Record<string, CardMeta> = {};
for (const c of cardsData as CardMeta[]) byId[c.id] = c;

export function cardMeta(id: string): CardMeta | undefined {
	return byId[normaliseId(id)];
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
	// Per-card DynamicVar overrides emitted by the mod for cards whose
	// class carries [SavedProperty] fields (e.g. The Scythe's growing
	// Damage). Keys are DynamicVar names; values are the card's current
	// base values.
	state?: Record<string, number>;
}

// parseDeckId splits a deck entry into its components. The mod emits:
//   "bash"                             → plain
//   "bash+"                            → upgraded
//   "bash/sharp"                       → base + Sharp enchantment
//   "bash+/perfect_fit"                → upgraded + Perfect Fit
//   "the_scythe?Damage=16,Increase=4"  → stateful card with live values
export function parseDeckId(id: string): ParsedDeckId {
	const q = id.indexOf('?');
	const withoutState = q < 0 ? id : id.slice(0, q);
	const stateStr = q < 0 ? '' : id.slice(q + 1);
	const slash = withoutState.indexOf('/');
	const before = slash < 0 ? withoutState : withoutState.slice(0, slash);
	// Enchantment slug is looked up against the lowercase keys in enchant.json,
	// but the mod emits deck entries uppercase (e.g. CARD.STRIKE_IRONCLAD/TEZCATARAS_EMBER).
	const enchantment = slash < 0 ? undefined : withoutState.slice(slash + 1).toLowerCase();
	const isUp = before.endsWith('+');
	let state: Record<string, number> | undefined;
	if (stateStr.length > 0) {
		state = {};
		for (const pair of stateStr.split(',')) {
			const eq = pair.indexOf('=');
			if (eq < 0) continue;
			const key = pair.slice(0, eq);
			const num = Number(pair.slice(eq + 1));
			if (key.length === 0 || !Number.isFinite(num)) continue;
			state[key] = num;
		}
		if (Object.keys(state).length === 0) state = undefined;
	}
	return {
		base: isUp ? before.slice(0, -1) : before,
		upgraded: isUp,
		enchantment,
		state
	};
}
