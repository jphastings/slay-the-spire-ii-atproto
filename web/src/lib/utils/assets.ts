// Strips any namespace prefix (RELIC., POTION., CARD.) and lowercases.
// Game IDs look like 'RELIC.BURNING_BLOOD'; asset filenames are 'burning_blood.webp'.
function baseName(id: string): string {
	const name = id.includes('.') ? id.slice(id.indexOf('.') + 1) : id;
	return name.toLowerCase();
}

export function relicImage(id: string): string {
	return `/assets/relics/${baseName(id)}.webp`;
}

export function potionImage(id: string): string {
	return `/assets/potions/${baseName(id)}.webp`;
}
