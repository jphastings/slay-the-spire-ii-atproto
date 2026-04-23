// Strips any namespace prefix (RELIC., POTION., CARD.) and lowercases.
// Game IDs look like 'RELIC.BURNING_BLOOD'; asset filenames are 'burning_blood.webp'.
export function baseName(id: string): string {
	const name = id.includes('.') ? id.slice(id.indexOf('.') + 1) : id;
	return name.toLowerCase();
}

// cardPortrait maps a (character, id) to the WebP emitted by
// scripts/sync-from-extract.sh. Character is the game's pool directory
// (ironclad, silent, defect, necrobinder, regent, colorless, curse, …);
// id is the snake_case card id.
export function cardPortrait(character: string, id: string): string {
	return `/assets/card_portraits/${character}/${baseName(id)}.webp`;
}

// Frame color per character. The game maps cards to a frame via the
// `CardFrameMaterialPath` override on each CharacterModel / card pool —
// e.g. Ironclad → card_frame_red_mat.tres. Keep this table in sync with
// extract/cards/hsv.go's frameColors and the cardpool overrides in
// sts2.dll.
export const characterFrameColor: Record<string, string> = {
	ironclad: 'red',
	defect: 'blue',
	silent: 'green',
	regent: 'orange',
	necrobinder: 'pink',
	colorless: 'colorless',
	curse: 'curse',
	quest: 'quest',
	event: 'colorless',
	token: 'colorless',
	status: 'curse',
};

// Characters that have their own cost orb sprite (one energy_<name>.tres
// per character in ui_atlas.sprites/card/). Anything outside this set
// falls back to the colorless orb.
const orbCharacters = new Set([
	'ironclad',
	'silent',
	'defect',
	'necrobinder',
	'regent',
	'colorless',
	'quest'
]);

export function orbCharacter(character: string): string {
	return orbCharacters.has(character) ? character : 'colorless';
}
