<script lang="ts">
	import { humanizeId } from '$lib/utils/format';

	let { cards }: { cards: string[] } = $props();

	// Group duplicate cards: [{name, count}]
	const grouped = $derived.by(() => {
		const counts = new Map<string, number>();
		for (const c of cards) {
			counts.set(c, (counts.get(c) ?? 0) + 1);
		}
		return [...counts.entries()]
			.map(([id, count]) => ({ name: humanizeId(id), count }))
			.sort((a, b) => a.name.localeCompare(b.name));
	});
</script>

<div class="deck">
	{#each grouped as card}
		<span class="card-tag">
			{card.name}{#if card.count > 1} <span class="count"> &times;{card.count}</span>{/if}
		</span>
	{/each}
</div>

<style>
	.deck {
		display: flex;
		flex-wrap: wrap;
		gap: 0.4rem;
	}

	.card-tag {
		padding: 0.2rem 0.6rem;
		background: var(--bg-secondary);
		border: 1px solid var(--border-subtle);
		border-radius: var(--radius-sm);
		font-size: 0.85rem;
		color: var(--text-primary);
	}

	.count {
		color: var(--text-muted);
		margin-left: 0.5em;
	}
</style>
