<script lang="ts">
	import { humanizeId } from '$lib/utils/format';
	import { baseName } from '$lib/utils/assets';
	import { potionsSheet, spriteStyle } from '$lib/utils/sprites';
	import Tooltip from './Tooltip.svelte';

	let { potions }: { potions: string[] } = $props();
</script>

<div class="potions">
	{#each potions as potion}
		{@const name = humanizeId(potion)}
		{@const style = spriteStyle(potionsSheet, baseName(potion))}
		<Tooltip label={name}>
			<span class="potion" class:ready={!!style} style={style} aria-label={name}></span>
		</Tooltip>
	{/each}
</div>

<style>
	.potions {
		display: flex;
		flex-wrap: wrap;
		gap: 0.5rem;
	}

	.potion {
		--size: 3rem;
		display: inline-block;
		width: var(--size);
		height: var(--size);
		transition: transform 0.15s;
	}

	.potion.ready {
		background-image: var(--sprite);
		background-size: calc(var(--size) * var(--cols)) calc(var(--size) * var(--rows));
		background-position: calc(-1 * var(--col) * var(--size)) calc(-1 * var(--row) * var(--size));
		background-repeat: no-repeat;
	}

	.potion:hover {
		transform: scale(1.08);
	}
</style>
