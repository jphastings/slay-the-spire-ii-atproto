<script lang="ts">
	import Card from './Card.svelte';

	let { cards }: { cards: string[] } = $props();

	// Group duplicates while preserving the first-seen order — keeps
	// long, sorted-at-ingest decks readable.
	const grouped = $derived.by(() => {
		const counts = new Map<string, number>();
		for (const c of cards) counts.set(c, (counts.get(c) ?? 0) + 1);
		return [...counts.entries()].map(([id, count]) => ({ id, count }));
	});
</script>

<div class="deck">
	{#each grouped as card (card.id)}
		<div class="slot">
			<Card id={card.id} width={160} />
			{#if card.count > 1}
				<span class="count">&times;{card.count}</span>
			{/if}
		</div>
	{/each}
</div>

<style>
	.deck {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
		gap: 0.6rem;
	}

	.slot {
		position: relative;
	}

	.count {
		position: absolute;
		top: 0.25rem;
		right: 0.25rem;
		padding: 0.1rem 0.4rem;
		background: rgba(0, 0, 0, 0.7);
		border: 1px solid var(--border-card);
		border-radius: var(--radius-sm);
		color: var(--text-primary);
		font-family: var(--font-body);
		font-size: 0.8rem;
		font-weight: 600;
	}
</style>
