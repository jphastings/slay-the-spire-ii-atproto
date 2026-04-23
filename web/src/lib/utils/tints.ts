// HSV ShaderMaterial tints translated to SVG `<feColorMatrix>` filters.
// CSS's own `filter: hue-rotate(...) saturate(...) brightness(...)` uses
// BT.709 luminance weights (0.213, 0.715, 0.072) while the game's
// shaders/hsv.gdshader uses BT.601 (0.2989, 0.5870, 0.1140), so the two
// produce visibly different colors. We precompute the equivalent 3×3
// RGB→RGB matrix in BT.601 YIQ space, ship it as one SVG filter per
// (color | rarity), and reference them via `filter: url(#tint-…)`.
//
// The full set of filter <defs> is exposed as tintFiltersSvg() so the
// layout can inject them once into the DOM.

import tints from '$lib/data/tints.json';

interface HSV {
	h: number;
	s: number;
	v: number;
}

interface TintsManifest {
	frameColors: Record<string, HSV>;
	rarities: Record<string, HSV>;
	enchant: HSV;
}

const data = tints as TintsManifest;

// Game's RGB → YIQ matrix (extract/cards/hsv.go yiqFwd, mirrored from
// shaders/hsv.gdshader in the .pck).
const M: number[][] = [
	[0.2989, 0.587, 0.114],
	[0.5959, -0.2774, -0.3216],
	[0.2115, -0.5229, 0.3114]
];

const Minv = invert3(M);

function invert3(m: number[][]): number[][] {
	const det =
		m[0][0] * (m[1][1] * m[2][2] - m[1][2] * m[2][1]) -
		m[0][1] * (m[1][0] * m[2][2] - m[1][2] * m[2][0]) +
		m[0][2] * (m[1][0] * m[2][1] - m[1][1] * m[2][0]);
	return [
		[
			(m[1][1] * m[2][2] - m[1][2] * m[2][1]) / det,
			(m[0][2] * m[2][1] - m[0][1] * m[2][2]) / det,
			(m[0][1] * m[1][2] - m[0][2] * m[1][1]) / det
		],
		[
			(m[1][2] * m[2][0] - m[1][0] * m[2][2]) / det,
			(m[0][0] * m[2][2] - m[0][2] * m[2][0]) / det,
			(m[0][2] * m[1][0] - m[0][0] * m[1][2]) / det
		],
		[
			(m[1][0] * m[2][1] - m[1][1] * m[2][0]) / det,
			(m[0][1] * m[2][0] - m[0][0] * m[2][1]) / det,
			(m[0][0] * m[1][1] - m[0][1] * m[1][0]) / det
		]
	];
}

function mul3(a: number[][], b: number[][]): number[][] {
	const out: number[][] = [
		[0, 0, 0],
		[0, 0, 0],
		[0, 0, 0]
	];
	for (let i = 0; i < 3; i++)
		for (let j = 0; j < 3; j++)
			out[i][j] = a[i][0] * b[0][j] + a[i][1] * b[1][j] + a[i][2] * b[2][j];
	return out;
}

// rgbMatrix mirrors extract/cards/hsv.go tintImage exactly:
//   YIQ = M · RGB
//   rotate I,Q by angle = (1 - h) · 2π  (CCW)
//   I,Q *= s ; Y,I,Q *= v
//   RGB' = M⁻¹ · YIQ
function rgbMatrix(p: HSV): number[][] {
	const angle = (1 - p.h) * 2 * Math.PI;
	const c = Math.cos(angle);
	const s = Math.sin(angle);
	// Combined YIQ-space transform: scale Y by v; rotate IQ by angle,
	// scale by s · v.
	const sv = p.s * p.v;
	const Tyiq: number[][] = [
		[p.v, 0, 0],
		[0, sv * c, -sv * s],
		[0, sv * s, sv * c]
	];
	return mul3(Minv, mul3(Tyiq, M));
}

function feMatrix(p: HSV): string {
	const m = rgbMatrix(p);
	// SVG feColorMatrix values are 4×5 (RGBA + offset). Tint touches
	// only RGB; alpha passes through unchanged.
	return [
		`${m[0][0]} ${m[0][1]} ${m[0][2]} 0 0`,
		`${m[1][0]} ${m[1][1]} ${m[1][2]} 0 0`,
		`${m[2][0]} ${m[2][1]} ${m[2][2]} 0 0`,
		`0 0 0 1 0`
	].join(' ');
}

function filterId(kind: 'frame' | 'rarity' | 'enchant', name: string): string {
	return `tint-${kind}-${name}`;
}

export function frameFilter(color: string): string {
	if (!data.frameColors[color]) return '';
	return `url(#${filterId('frame', color)})`;
}

export function rarityFilter(rarity: string): string {
	if (!data.rarities[rarity]) return '';
	return `url(#${filterId('rarity', rarity)})`;
}

export function enchantFilter(): string {
	return `url(#${filterId('enchant', 'tab')})`;
}

// SVG <defs> containing one <filter> per declared tint. Inject once at
// the page root (see +layout.svelte) so every Card on the page can
// reference them via filter: url(#…).
export function tintFiltersSvg(): string {
	const filters: string[] = [];
	for (const [name, p] of Object.entries(data.frameColors)) {
		filters.push(
			`<filter id="${filterId('frame', name)}" color-interpolation-filters="sRGB"><feColorMatrix type="matrix" values="${feMatrix(p)}"/></filter>`
		);
	}
	for (const [name, p] of Object.entries(data.rarities)) {
		filters.push(
			`<filter id="${filterId('rarity', name)}" color-interpolation-filters="sRGB"><feColorMatrix type="matrix" values="${feMatrix(p)}"/></filter>`
		);
	}
	filters.push(
		`<filter id="${filterId('enchant', 'tab')}" color-interpolation-filters="sRGB"><feColorMatrix type="matrix" values="${feMatrix(data.enchant)}"/></filter>`
	);
	return `<svg xmlns="http://www.w3.org/2000/svg" aria-hidden="true" style="position:absolute;width:0;height:0;overflow:hidden"><defs>${filters.join('')}</defs></svg>`;
}
