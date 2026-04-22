<script lang="ts">
	import type { RunRecord } from '$lib/api/types';
	import DamageHistogram from './DamageHistogram.svelte';

	let { stats }: { stats: NonNullable<RunRecord['stats']> } = $props();

	type Tile = { label: string; value: string | number; accent?: 'gold' | 'red' | 'green' };

	const nf = new Intl.NumberFormat('en-US');

	function num(n: unknown): string | null {
		return typeof n === 'number' ? nf.format(n) : null;
	}

	function tile(label: string, n: unknown, accent?: Tile['accent']): Tile | null {
		const v = num(n);
		return v === null ? null : { label, value: v, accent };
	}

	function nonNull<T>(items: (T | null)[]): T[] {
		return items.filter((t): t is T => t !== null);
	}

	const combatTiles = $derived(
		nonNull([
			tile('Combats Won', stats.combatsWon),
			tile('Elites Won', stats.elitesWon),
			tile('Turns Taken', stats.turns),
			tile('Longest Combat', stats.longestCombat)
		])
	);

	const dealtTiles = $derived(
		nonNull([
			tile('Total', stats.damageDealt, 'gold'),
			tile('Biggest Blow', stats.biggestDamageDealt, 'gold'),
			tile('Best Attack', stats.biggestTurnDamageDealt, 'gold')
		])
	);

	const takenTiles = $derived(
		nonNull([
			tile('Total', stats.damageTaken, 'red'),
			tile('Biggest Blow', stats.biggestDamageTaken, 'red'),
			tile('Worst Turn', stats.biggestTurnDamageTaken, 'red'),
			tile('Best Block', stats.highestBlockInTurn)
		])
	);

	const itemsTiles = $derived(
		nonNull([
			tile('Cards Played', stats.cardsPlayed),
			tile('Cards Drawn', stats.cardsDrawn),
			tile('Exhausted', stats.cardsExhausted),
			tile('Potions Used', stats.potionsUsed),
			tile('Untouched Turns', stats.noDamageTurns)
		])
	);

	const hasDealt = $derived(dealtTiles.length > 0 || !!stats.hitsDealtDistribution);
	const hasTaken = $derived(takenTiles.length > 0 || !!stats.hitsTakenDistribution);
	const hasAny = $derived(
		combatTiles.length > 0 || hasDealt || hasTaken || itemsTiles.length > 0
	);
</script>

{#snippet tileGrid(tiles: Tile[])}
	<div class="tiles">
		{#each tiles as t}
			<div
				class="tile"
				class:gold={t.accent === 'gold'}
				class:red={t.accent === 'red'}
				class:green={t.accent === 'green'}
			>
				<span class="value">{t.value}</span>
				<span class="label">{t.label}</span>
			</div>
		{/each}
	</div>
{/snippet}

{#if hasAny}
	<section class="stats-panel">
		<h3>Run Stats</h3>

		{#if combatTiles.length > 0}
			<div class="group">
				<h4>Combat</h4>
				{@render tileGrid(combatTiles)}
			</div>
		{/if}

		{#if hasDealt}
			<div class="group">
				<h4 class="dealt">Damage Dealt</h4>
				<div class="damage-row">
					{#if dealtTiles.length > 0}
						{@render tileGrid(dealtTiles)}
					{/if}
					{#if stats.hitsDealtDistribution}
						<DamageHistogram
							distribution={stats.hitsDealtDistribution}
							accent="gold"
							direction="dealt"
						/>
					{/if}
				</div>
			</div>
		{/if}

		{#if hasTaken}
			<div class="group">
				<h4 class="taken">Damage Taken</h4>
				<div class="damage-row">
					{#if takenTiles.length > 0}
						{@render tileGrid(takenTiles)}
					{/if}
					{#if stats.hitsTakenDistribution}
						<DamageHistogram
							distribution={stats.hitsTakenDistribution}
							accent="red"
							direction="taken"
						/>
					{/if}
				</div>
			</div>
		{/if}

		{#if itemsTiles.length > 0}
			<div class="group">
				<h4>Cards &amp; Items</h4>
				{@render tileGrid(itemsTiles)}
			</div>
		{/if}
	</section>
{/if}

<style>
	.stats-panel {
		display: flex;
		flex-direction: column;
		gap: 1rem;
	}

	h3 {
		font-size: 1rem;
		color: var(--text-secondary);
		margin-bottom: 0.3rem;
	}

	.group {
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
	}

	h4 {
		font-family: var(--font-body);
		font-size: 0.7rem;
		text-transform: uppercase;
		letter-spacing: 0.12em;
		color: var(--text-muted);
		font-weight: 600;
		padding-bottom: 0.3rem;
		border-bottom: 1px solid var(--border-subtle);
	}

	h4.dealt {
		color: color-mix(in srgb, var(--accent-gold) 75%, var(--text-muted));
		border-bottom-color: color-mix(in srgb, var(--accent-gold) 25%, var(--border-subtle));
	}

	h4.taken {
		color: color-mix(in srgb, var(--accent-red) 75%, var(--text-muted));
		border-bottom-color: color-mix(in srgb, var(--accent-red) 25%, var(--border-subtle));
	}

	.tiles {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(7rem, 1fr));
		gap: 0.5rem;
	}

	/* Two-pane damage row: stats left, histogram right on wide screens. */
	.damage-row {
		display: grid;
		grid-template-columns: 1fr;
		gap: 0.6rem;
		align-items: stretch;
	}

	@media (min-width: 40rem) {
		.damage-row {
			grid-template-columns: minmax(0, 1fr) minmax(0, 1.6fr);
		}
	}

	.tile {
		position: relative;
		display: flex;
		flex-direction: column;
		gap: 0.15rem;
		padding: 0.7rem 0.9rem;
		background: var(--bg-card);
		border: 1px solid var(--border-subtle);
		border-radius: var(--radius);
		overflow: hidden;
		transition:
			border-color 0.15s,
			transform 0.15s;
	}

	.tile::before {
		content: '';
		position: absolute;
		inset: 0;
		pointer-events: none;
		background: radial-gradient(
			circle at top left,
			var(--accent, transparent) 0%,
			transparent 55%
		);
		opacity: 0.08;
	}

	.tile.gold {
		--accent: var(--accent-gold);
		border-color: color-mix(in srgb, var(--accent-gold) 35%, var(--border-subtle));
	}

	.tile.red {
		--accent: var(--accent-red);
		border-color: color-mix(in srgb, var(--accent-red) 30%, var(--border-subtle));
	}

	.tile.green {
		--accent: var(--accent-green);
		border-color: color-mix(in srgb, var(--accent-green) 30%, var(--border-subtle));
	}

	.value {
		font-family: var(--font-display);
		font-size: 1.5rem;
		font-weight: 700;
		color: var(--text-primary);
		line-height: 1;
		letter-spacing: 0.02em;
		font-variant-numeric: tabular-nums;
	}

	.tile.gold .value {
		color: var(--accent-gold);
	}

	.tile.red .value {
		color: var(--accent-red);
	}

	.tile.green .value {
		color: var(--accent-green);
	}

	.label {
		font-size: 0.68rem;
		text-transform: uppercase;
		letter-spacing: 0.08em;
		color: var(--text-muted);
		font-weight: 500;
	}
</style>
