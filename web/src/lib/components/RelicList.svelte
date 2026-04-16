<script lang="ts">
	import { humanizeId } from '$lib/utils/format';

	let { relics }: { relics: string[] } = $props();
</script>

<div class="relics">
	{#each relics as relic}
		<span class="relic-tag">{humanizeId(relic)}</span>
	{/each}
</div>

<style>
	.relics {
		display: flex;
		flex-wrap: wrap;
		gap: 0.4rem;
	}

	.relic-tag {
		padding: 0.2rem 0.6rem;
		background: color-mix(in srgb, var(--accent-gold) 10%, transparent);
		border: 1px solid color-mix(in srgb, var(--accent-gold) 25%, transparent);
		border-radius: var(--radius-sm);
		font-size: 0.85rem;
		color: var(--accent-gold);
	}
</style>
