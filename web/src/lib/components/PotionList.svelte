<script lang="ts">
	import { humanizeId } from '$lib/utils/format';
	import { potionImage } from '$lib/utils/assets';
	import Tooltip from './Tooltip.svelte';

	let { potions }: { potions: string[] } = $props();
</script>

<div class="potions">
	{#each potions as potion}
		{@const name = humanizeId(potion)}
		<Tooltip label={name}>
			<img class="potion" src={potionImage(potion)} alt={name} loading="lazy" />
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
		width: 3rem;
		height: 3rem;
		object-fit: contain;
		padding: 0.2rem;
		transition: transform 0.15s;
	}

	.potion:hover {
		transform: scale(1.08);
	}
</style>
