<script lang="ts">
	import { humanizeId } from '$lib/utils/format';
	import { characterFrameColor, orbCharacter } from '$lib/utils/assets';
	import { cardMeta, normaliseId, parseDeckId } from '$lib/utils/cardMeta';
	import { parseCardText } from '$lib/utils/cardtext';
	import {
		enchantSheet,
		orbSheet,
		packedSpriteStyle,
		portraitSheet,
		spriteStyle
	} from '$lib/utils/sprites';
	import { frameFilter, rarityFilter } from '$lib/utils/tints';

	let {
		id,
		upgraded: upgradedProp = false,
		width = 200,
		description
	}: {
		id: string;
		upgraded?: boolean;
		width?: number;
		description?: string;
	} = $props();

	// Deck entries are annotated by the mod's CollectDeckIds with
	// upgrade + enchantment + state; parseDeckId splits them back apart.
	const parsed = $derived(parseDeckId(id));
	const rawId = $derived(parsed.base);
	const upgraded = $derived(upgradedProp || parsed.upgraded);
	const enchantment = $derived(parsed.enchantment);

	const meta = $derived(cardMeta(rawId));
	const name = $derived(humanizeId(rawId));
	const normId = $derived(normaliseId(rawId));

	// Per-character portrait sheet — bundled into the JS, looked up
	// synchronously. The webp itself loads lazily on first paint.
	const portraitStyle = $derived.by(() => {
		const sheet = meta?.character ? portraitSheet(meta.character) : undefined;
		return sheet ? spriteStyle(sheet, normId) : null;
	});
	const frameColor = $derived(
		meta ? (characterFrameColor[meta.character ?? ''] ?? 'colorless') : 'colorless'
	);
	// FramePath/PortraitBorderPath in sts2.dll map Status/Curse/None →
	// Skill for frame and border; our extractor only bakes attack/skill/
	// power, so mirror that fallback here.
	const frameType = $derived.by(() => {
		const t = meta?.type ?? 'skill';
		if (t === 'attack' || t === 'skill' || t === 'power') return t;
		return 'skill';
	});
	// Cards with a negative cost are "Unplayable" in the game — the
	// energy orb and number aren't drawn (curses, statuses).
	const hasCost = $derived(meta && !meta.cost.startsWith('-'));
	// Regent star cost: a second badge below the energy orb. Only the
	// Regent's star-costing cards carry a positive value.
	const starCost = $derived(meta?.starCost ?? 0);
	const hasStar = $derived(starCost > 0);
	const character = $derived(meta?.character ?? 'colorless');
	const orbChar = $derived(orbCharacter(character));
	// Normalise rarity to one of our baked banner variants.
	const rarity = $derived.by(() => {
		const r = meta?.rarity ?? 'common';
		const allowed = ['common', 'uncommon', 'rare', 'curse', 'status', 'event', 'quest', 'ancient'];
		if (allowed.includes(r)) return r;
		if (r === 'basic') return 'common'; // Strike / Defend / Bash
		return 'common';
	});
	const typeLabel = $derived(
		frameType === 'attack'
			? 'Attack'
			: frameType === 'skill'
				? 'Skill'
				: frameType === 'power'
					? 'Power'
					: humanizeId(frameType)
	);
	const descSource = $derived(description ?? meta?.description ?? '');
	// Placeholder values: upgrade-appropriate defaults baked by the extractor,
	// overlaid with live state the mod emits for cards with [SavedProperty]
	// (e.g. The Scythe's growing damage).
	const placeholderValues = $derived.by(() => {
		const defaults = (upgraded ? meta?.upgradedVars : meta?.vars) ?? meta?.vars;
		if (!defaults && !parsed.state) return undefined;
		return { ...(defaults ?? {}), ...(parsed.state ?? {}) };
	});
	const descLines = $derived(
		descSource ? parseCardText(descSource, { upgraded, values: placeholderValues }) : []
	);
</script>

<!--
  Layout mirrors scenes/cards/card.tscn. The game's Card scene has a
  300×422 frame; every sub-layer's offset is expressed below as a percentage
  of those dimensions so the card scales cleanly via --w.
-->
<div class="card" style="--w: {width}px" aria-label={name} data-enchantment={enchantment}>
	<!-- Layer order mirrors scenes/cards/card.tscn child order: Portrait
	     is added to PortraitCanvasGroup FIRST, then Frame is drawn on top,
	     then PortraitBorder, then banner/plaque/text. -->
	{#if portraitStyle}
		<div class="layer portrait" style={portraitStyle} aria-hidden="true"></div>
	{/if}
	<!-- Frame, portrait-border, banner and plaque ship as untinted
	     base shapes; the per-color/rarity hue tint is applied via CSS
	     filter, mirroring the game's runtime ShaderMaterial. See
	     utils/tints.ts. -->
	<img
		class="layer frame"
		src="/cards/parts/frame/{frameType}.webp"
		alt=""
		loading="lazy"
		style="filter: {frameFilter(frameColor)}"
	/>
	<img
		class="layer portrait-border"
		src="/cards/parts/portrait_border/{frameType}.webp"
		alt=""
		loading="lazy"
		style="filter: {rarityFilter(rarity)}"
	/>
	<img
		class="layer banner"
		src="/cards/parts/banner.webp"
		alt=""
		loading="lazy"
		style="filter: {rarityFilter(rarity)}"
	/>
	<img
		class="layer plaque"
		src="/cards/parts/plaque.webp"
		alt=""
		loading="lazy"
		style="filter: {rarityFilter(rarity)}"
	/>
	<span class="type-label">{typeLabel}</span>
	{#if hasCost}
		{@const orbStyle = packedSpriteStyle(orbSheet, orbChar)}
		{#if orbStyle}
			<div class="layer orb" style={orbStyle} aria-hidden="true"></div>
		{/if}
		<span class="cost">{meta?.cost}</span>
	{/if}
	{#if hasStar}
		{@const starStyle = packedSpriteStyle(orbSheet, 'star')}
		{#if starStyle}
			<div class="layer star-icon" style={starStyle} aria-hidden="true"></div>
		{/if}
		<span class="star-cost">{starCost}</span>
	{/if}
	{#if enchantment}
		{@const tabStyle = packedSpriteStyle(enchantSheet, 'tab')}
		{@const iconStyle = packedSpriteStyle(enchantSheet, enchantment)}
		{#if tabStyle}
			<div class="layer enchant-tab" style={tabStyle} aria-hidden="true"></div>
		{/if}
		{#if iconStyle}
			<div class="layer enchant-icon" style={iconStyle} aria-hidden="true"></div>
		{/if}
	{/if}
	<span class="name" class:upgraded>{name}{upgraded ? '+' : ''}</span>
	{#if descLines.length > 0}
		<div class="description">
			<div class="description-inner">
				{#each descLines as line, i}
					{#each line as run}{#if run.style === 'icon'}<span
								class="run icon"
								style="--icon: url('/cards/parts/{run.icon}.webp')"
								aria-hidden="true"
							></span>{:else}<span class="run {run.style}">{run.text}</span>{/if}{/each}{#if i < descLines.length - 1}<br />{/if}
				{/each}
			</div>
		</div>
	{/if}
</div>

<style>
	.card {
		position: relative;
		display: block;
		width: var(--w);
		/* Native card aspect ratio from scenes/cards/card.tscn Frame: 300×422. */
		aspect-ratio: 300 / 422;
		font-family: 'Kreon', serif;
		color: oldlace;
		user-select: none;
	}

	.layer {
		position: absolute;
		pointer-events: none;
		user-select: none;
	}

	/* Frame fills the whole card. */
	.frame {
		inset: 0;
		width: 100%;
		height: 100%;
	}

	/* Portrait: card.tscn Portrait (-125,-168)..(125,22) inside frame
	   (-150,-211)..(150,211) → (25,43)..(275,233). Tile sourced from a
	   per-character sprite sheet — see ensurePortraitSheet in
	   utils/sprites.ts. The container's aspect (~1.316) is essentially
	   identical to the source tile's (356/271 ≈ 1.314), so background
	   stretching to fill is visually equivalent to the previous
	   object-fit: cover on an <img>. */
	.portrait {
		left: 8.333%;
		top: 10.19%;
		width: 83.333%;
		height: 45.02%;
		background-image: var(--sprite);
		background-size: calc(100% * var(--cols)) calc(100% * var(--rows));
		background-position:
			calc(var(--col) / (var(--cols) - 1) * 100%)
			calc(var(--row) / (var(--rows) - 1) * 100%);
		background-repeat: no-repeat;
	}

	/* PortraitBorder: (-137.5,-164)..(137.5,46) → (12.5,47)..(287.5,257). */
	.portrait-border {
		left: 4.167%;
		top: 11.14%;
		width: 91.667%;
		height: 49.76%;
	}

	/* TitleBanner: (-163,-207)..(164,-124) → (-13,4)..(314,87). Ribbon ends
	   overhang the card edges by ~4% on each side (the PNG is 327 px
	   wide vs the frame's 300). */
	.banner {
		left: -4.333%;
		top: 3.1%;
		width: 109%;
		height: 19.67%;
	}

	/* TypePlaque: (-30.5,1)..(30.5,38) → (119.5,212)..(180.5,250). */
	.plaque {
		left: 39.833%;
		top: 50.24%;
		width: 20.333%;
		height: 8.77%;
	}

	/* TypeLabel text on the plaque — centered, ~16 px font at frame-300. */
	.type-label {
		position: absolute;
		left: 39.833%;
		top: 50.24%;
		width: 20.333%;
		height: 8.77%;
		display: grid;
		place-items: center;
		font-family: 'Kreon', serif;
		font-weight: 700;
		font-size: calc(var(--w) * 16 / 300);
		color: rgba(0, 0, 0, 0.75);
		line-height: 1;
	}

	/* EnergyIcon: (-166,-227)..(-102,-163) → (-16,-16)..(48,48). Overhangs
	   the card's top-left corner. */
	.orb {
		left: -5.333%;
		top: -3.791%;
		width: 21.333%;
		height: 15.17%;
	}

	/* EnergyLabel: centered inside the orb, font 32. */
	.cost {
		position: absolute;
		left: -5.333%;
		top: -3.791%;
		width: 21.333%;
		height: 15.17%;
		display: grid;
		place-items: center;
		font-weight: 700;
		font-size: calc(var(--w) * 32 / 300);
		color: oldlace;
		line-height: 1;
		-webkit-text-stroke: calc(var(--w) * 8 / 300) #4c4943;
		paint-order: stroke fill;
		text-shadow: calc(var(--w) * 2 / 300) calc(var(--w) * 2 / 300) 0 rgba(0, 0, 0, 0.19);
	}

	/* StarIcon: card.tscn (-186,-189)..(-128,-131) → (-36,22)..(22,80).
	   The Regent star-cost badge, tucked below-left of the energy orb. */
	.star-icon {
		left: -12%;
		top: 5.213%;
		width: 19.333%;
		height: 13.744%;
	}

	/* StarLabel: centered in the star icon, font 22, cream text on a teal
	   outline (outline_size 12 → stroke 6/300, half like the energy label). */
	.star-cost {
		position: absolute;
		left: -12%;
		top: 5.213%;
		width: 19.333%;
		height: 13.744%;
		display: grid;
		place-items: center;
		font-weight: 700;
		font-size: calc(var(--w) * 22 / 300);
		color: #fff6e2;
		line-height: 1;
		-webkit-text-stroke: calc(var(--w) * 6 / 300) #1a5e6b;
		paint-order: stroke fill;
		text-shadow: calc(var(--w) * 2 / 300) calc(var(--w) * 2 / 300) 0 rgba(0, 0, 0, 0.19);
	}

	/* TitleLabel: (-105,-204)..(105,-150) → (45,7)..(255,61), font 26. */
	.name {
		position: absolute;
		left: 15%;
		top: 1.66%;
		width: 70%;
		height: 12.8%;
		display: grid;
		place-items: center;
		text-align: center;
		font-size: calc(var(--w) * 26 / 300);
		line-height: 1;
		color: oldlace;
		-webkit-text-stroke: calc(var(--w) * 6 / 300) #4d4b40;
		paint-order: stroke fill;
		text-shadow: calc(var(--w) * 2 / 300) calc(var(--w) * 2 / 300) 0 rgba(0, 0, 0, 0.19);
		text-wrap: nowrap;
	}

	/* Upgraded cards render the title in green with a darker green outline. */
	.name.upgraded {
		color: #7fff00;
		-webkit-text-stroke-color: #1b6131;
	}

	/* Enchantment tab: card.tscn Enchantment (-166,-116)..(-94,-62) →
	   (-16,95)..(56,149) within the 300×422 frame. Sticks out from the
	   left edge like a bookmark. */
	.enchant-tab {
		left: -5.333%;
		top: 22.51%;
		width: 24%;
		height: 12.8%;
	}

	/* Icon inside the tab (child offset 14,9..49,44 → 35×35 in 72×54). */
	.enchant-icon {
		left: -5.333%;
		top: 22.51%;
		width: calc(24% * 35 / 72);
		height: calc(12.8% * 35 / 54);
		transform: translate(calc(var(--w) * 14 / 300), calc(var(--w) * 9 / 300));
	}

	/* DescriptionLabel: (-122,37)..(121,173) → (28,248)..(271,384), font 21. */
	.description {
		position: absolute;
		left: 9.333%;
		top: 58.77%;
		width: 81%;
		height: 32.23%;
		display: grid;
		place-items: center;
		text-align: center;
		font-size: calc(var(--w) * 21 / 300);
		line-height: 1.1;
		color: oldlace;
		text-shadow: calc(var(--w) * 2 / 300) calc(var(--w) * 2 / 300) 0 rgba(0, 0, 0, 0.25);
	}

	.description .run.highlight,
	.description .run.placeholder {
		color: #f0c850;
	}

	/* Inline {singleStarIcon} sprite — sized to the description text. */
	.description .run.icon {
		display: inline-block;
		width: 1em;
		height: 1em;
		vertical-align: -0.15em;
		background-image: var(--icon);
		background-size: contain;
		background-position: center;
		background-repeat: no-repeat;
	}
</style>
