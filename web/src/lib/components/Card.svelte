<script lang="ts">
	import { onMount } from 'svelte';
	import { humanizeId } from '$lib/utils/format';
	import { cardPortrait, characterFrameColor, orbCharacter } from '$lib/utils/assets';
	import { cardMeta, ensureCardsLoaded, normaliseId, parseDeckId } from '$lib/utils/cardMeta';
	import { parseCardText } from '$lib/utils/cardtext';

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
	// upgrade + enchantment state; parseDeckId splits them back apart.
	const parsed = $derived(parseDeckId(id));
	const rawId = $derived(parsed.base);
	const upgraded = $derived(upgradedProp || parsed.upgraded);
	const enchantment = $derived(parsed.enchantment);

	let loaded = $state(false);
	onMount(async () => {
		await ensureCardsLoaded();
		loaded = true;
	});

	const meta = $derived(loaded ? cardMeta(rawId) : undefined);
	const name = $derived(humanizeId(rawId));
	const normId = $derived(normaliseId(rawId));
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
	const descLines = $derived(descSource ? parseCardText(descSource, { upgraded }) : []);
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
	{#if meta?.character}
		<img
			class="layer portrait"
			src={cardPortrait(meta.character, normId)}
			alt=""
			loading="lazy"
			onerror={(e) => ((e.currentTarget as HTMLImageElement).style.visibility = 'hidden')}
		/>
	{/if}
	<img
		class="layer frame"
		src="/cards/parts/frame/{frameType}_{frameColor}.png"
		alt=""
		loading="lazy"
	/>
	<img
		class="layer portrait-border"
		src="/cards/parts/portrait_border/{frameType}_{rarity}.png"
		alt=""
		loading="lazy"
	/>
	<img
		class="layer banner"
		src="/cards/parts/banner/{rarity}.png"
		alt=""
		loading="lazy"
	/>
	<img
		class="layer plaque"
		src="/cards/parts/plaque/{rarity}.png"
		alt=""
		loading="lazy"
	/>
	<span class="type-label">{typeLabel}</span>
	{#if hasCost}
		<img
			class="layer orb"
			src="/cards/parts/orb/{orbChar}.png"
			alt=""
			loading="lazy"
		/>
		<span class="cost">{meta?.cost}</span>
	{/if}
	{#if enchantment}
		<img class="layer enchant-tab" src="/cards/parts/enchant/tab.png" alt="" />
		<img
			class="layer enchant-icon"
			src="/cards/enchantments/{enchantment}.png"
			alt=""
			onerror={(e) => ((e.currentTarget as HTMLImageElement).style.visibility = 'hidden')}
		/>
	{/if}
	<span class="name" class:upgraded>{name}{upgraded ? '+' : ''}</span>
	{#if descLines.length > 0}
		<div class="description">
			<div class="description-inner">
				{#each descLines as line, i}
					{#each line as run}<span class="run {run.style}">{run.text}</span>{/each}{#if i < descLines.length - 1}<br />{/if}
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
	   (-150,-211)..(150,211) → (25,43)..(275,233). */
	.portrait {
		left: 8.333%;
		top: 10.19%;
		width: 83.333%;
		height: 45.02%;
		object-fit: cover;
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
</style>
