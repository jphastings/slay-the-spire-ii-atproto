<script lang="ts">
	import { onMount } from 'svelte';
	import Card from './Card.svelte';
	import { cardMeta, ensureCardsLoaded, parseDeckId } from '$lib/utils/cardMeta';

	let {
		cards,
		cardUseDistribution
	}: {
		cards: string[];
		// Lexicon-generated type is `unknown` because the distribution
		// object's keys are open-ended (see sts2-run.ts). Coerce here.
		cardUseDistribution?: unknown;
	} = $props();

	const playCounts = $derived<Record<string, number>>(
		(() => {
			if (!cardUseDistribution || typeof cardUseDistribution !== 'object') return {};
			const out: Record<string, number> = {};
			for (const [k, v] of Object.entries(cardUseDistribution as Record<string, unknown>)) {
				if (typeof v === 'number') out[k] = v;
			}
			return out;
		})()
	);
	const hasPlayCounts = $derived(Object.keys(playCounts).length > 0);

	let loaded = $state(false);
	onMount(async () => {
		await ensureCardsLoaded();
		loaded = true;
	});

	// Group duplicates. When cardUseDistribution is present, sort within
	// each type tab by times-played desc so the cards the player leaned
	// on sit at the top. Without use stats, keep first-seen order.
	const grouped = $derived.by(() => {
		const counts = new Map<string, number>();
		for (const c of cards) counts.set(c, (counts.get(c) ?? 0) + 1);
		const entries = [...counts.entries()].map(([id, count], idx) => ({
			id,
			count,
			plays: playCounts[parseDeckId(id).base] ?? 0,
			order: idx
		}));
		if (hasPlayCounts) {
			entries.sort((a, b) => b.plays - a.plays || a.order - b.order);
		}
		return entries;
	});

	// Preferred display order for the type tabs; anything else falls
	// through to the end in alphabetical order.
	const typeOrder = ['attack', 'skill', 'power', 'curse', 'status', 'quest'];
	const typeLabels: Record<string, string> = {
		attack: 'Attacks',
		skill: 'Skills',
		power: 'Powers',
		curse: 'Curses',
		status: 'Statuses',
		quest: 'Quests'
	};
	const typeLabel = (t: string) =>
		typeLabels[t] ?? t.charAt(0).toUpperCase() + t.slice(1) + 's';

	// Bucket counts keyed by each card's meta.type. Pre-load we have
	// nothing to group by, so the counts stay empty and the tab row
	// doesn't appear yet.
	const byType = $derived.by(() => {
		const counts = new Map<string, number>();
		if (!loaded) return counts;
		for (const g of grouped) {
			const m = cardMeta(parseDeckId(g.id).base);
			const t = m?.type ?? 'unknown';
			counts.set(t, (counts.get(t) ?? 0) + g.count);
		}
		return counts;
	});

	// Tabs in the preferred order, skipping empty buckets.
	const tabs = $derived.by(() => {
		const seen = new Set<string>();
		const out: { type: string; count: number }[] = [];
		for (const t of typeOrder) {
			const c = byType.get(t);
			if (c) {
				out.push({ type: t, count: c });
				seen.add(t);
			}
		}
		const extras = [...byType.entries()].filter(([t]) => !seen.has(t)).sort();
		for (const [type, count] of extras) out.push({ type, count });
		return out;
	});

	let active = $state<string | undefined>(undefined);
	// Default to the first tab once we know which tabs are populated.
	$effect(() => {
		if (active === undefined && tabs.length > 0) active = tabs[0].type;
	});

	const visible = $derived.by(() => {
		if (!loaded || !active) return grouped;
		return grouped.filter((g) => {
			const m = cardMeta(parseDeckId(g.id).base);
			return (m?.type ?? 'unknown') === active;
		});
	});
</script>

{#if tabs.length > 1}
	<div class="tabs" role="tablist">
		{#each tabs as tab}
			<button
				type="button"
				role="tab"
				aria-selected={active === tab.type}
				class="tab"
				class:active={active === tab.type}
				onclick={() => (active = tab.type)}
			>
				{typeLabel(tab.type)} <span class="count">{tab.count}</span>
			</button>
		{/each}
	</div>
{/if}

<div class="deck">
	{#each visible as card (card.id)}
		<div class="slot">
			<Card id={card.id} width={160} />
			{#if card.count > 1}
				<span class="count-badge">&times;{card.count}</span>
			{/if}
		</div>
	{/each}
</div>

<style>
	.tabs {
		display: flex;
		flex-wrap: wrap;
		gap: 0.3rem;
		margin-bottom: 0.8rem;
	}

	.tab {
		display: inline-flex;
		align-items: baseline;
		gap: 0.4rem;
		padding: 0.35rem 0.75rem;
		background: var(--bg-secondary);
		border: 1px solid var(--border-subtle);
		border-radius: var(--radius-sm);
		color: var(--text-secondary);
		font-family: var(--font-body);
		font-size: 0.85rem;
		font-weight: 500;
		cursor: pointer;
		transition:
			background 0.12s,
			color 0.12s,
			border-color 0.12s;
	}

	.tab:hover {
		background: var(--bg-card-hover);
		color: var(--text-primary);
	}

	.tab.active {
		background: var(--bg-card);
		border-color: var(--accent-gold);
		color: var(--text-primary);
	}

	.tab .count {
		color: var(--text-muted);
		font-variant-numeric: tabular-nums;
	}

	.tab.active .count {
		color: var(--accent-gold);
	}

	.deck {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
		gap: 0.6rem;
	}

	.slot {
		position: relative;
	}

	.count-badge {
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
