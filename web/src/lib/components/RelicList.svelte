<script lang="ts">
	import { humanizeId } from '$lib/utils/format';
	import { relicImage } from '$lib/utils/assets';
	import Tooltip from './Tooltip.svelte';

	let { relics }: { relics: string[] } = $props();
</script>

<div class="relics">
	{#each relics as relic}
		{@const name = humanizeId(relic)}
		<Tooltip label={name}>
			<img class="relic" src={relicImage(relic)} alt={name} loading="lazy" />
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
		width: 3rem;
		height: 3rem;
		object-fit: contain;
		padding: 0.2rem;
		transition: transform 0.15s;
	}

	.relic:hover {
		transform: scale(1.08);
	}
</style>
