<script lang="ts">
	import { onMount } from 'svelte';
	import { humanizeId } from '$lib/utils/format';
	import { baseName } from '$lib/utils/assets';
	import { ensureRelicsLoaded, spriteStyle, type SpriteSheet } from '$lib/utils/sprites';
	import Tooltip from './Tooltip.svelte';

	let { relics }: { relics: string[] } = $props();

	let sheet = $state<SpriteSheet | undefined>(undefined);
	onMount(async () => {
		sheet = await ensureRelicsLoaded();
	});
</script>

<div class="relics">
	{#each relics as relic}
		{@const name = humanizeId(relic)}
		{@const style = sheet ? spriteStyle(sheet, baseName(relic)) : null}
		<Tooltip label={name}>
			<span class="relic" class:ready={!!style} style={style} aria-label={name}></span>
		</Tooltip>
	{/each}
</div>

<style>
	.relics {
		display: flex;
		flex-wrap: wrap;
		gap: 0.5rem;
	}

	.relic {
		--size: 3rem;
		display: inline-block;
		width: var(--size);
		height: var(--size);
		transition: transform 0.15s;
	}

	.relic.ready {
		background-image: var(--sprite);
		background-size: calc(var(--size) * var(--cols)) calc(var(--size) * var(--rows));
		background-position: calc(-1 * var(--col) * var(--size)) calc(-1 * var(--row) * var(--size));
		background-repeat: no-repeat;
	}

	.relic:hover {
		transform: scale(1.08);
	}
</style>
