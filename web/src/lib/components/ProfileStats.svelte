<script lang="ts">
	import type { ProfileStats } from '$lib/utils/profile-stats';
	import { formatMonth } from '$lib/utils/profile-stats';
	import { humanizeId } from '$lib/utils/format';
	import CharacterIcon from './CharacterIcon.svelte';
	import Tooltip from './Tooltip.svelte';
	import AllyTallyCard from './AllyTallyCard.svelte';

	const ALLY_LIMIT = 5;

	let { stats }: { stats: ProfileStats } = $props();

	const recentTotal = $derived(
		stats.recentMonth ? stats.recentMonth.victories + stats.recentMonth.losses : 0
	);
	const recentPct = $derived(
		stats.recentMonth && recentTotal > 0
			? Math.round((stats.recentMonth.victories / recentTotal) * 100)
			: null
	);
</script>

<section class="wrap">
	<dl class="fields">
		<div class="field">
			<dt>Runs</dt>
			<dd>{stats.totalRuns}</dd>
		</div>
		<div class="field">
			<dt>Victories</dt>
			<dd>{stats.victories}</dd>
		</div>
		{#if stats.hitsDealt > 0}
			<div class="field">
				<dt>Hits on Monsters</dt>
				<dd>{stats.hitsDealt.toLocaleString()}</dd>
			</div>
		{/if}
		{#if stats.recentMonth && recentTotal > 0}
			<div class="field">
				<dt>{formatMonth(stats.recentMonth.month)}</dt>
				<dd>
					{stats.recentMonth.victories}W / {stats.recentMonth.losses}L
					{#if recentPct !== null}
						<span class="muted">· {recentPct}%</span>
					{/if}
				</dd>
			</div>
		{/if}
	</dl>

	{#if stats.characters.length > 0}
		<ul class="chars">
			{#each stats.characters as c}
				{@const name = humanizeId(c.character)}
				<li class="char">
					<Tooltip label={name}>
						<CharacterIcon character={c.character} size="2.75rem" />
					</Tooltip>
					<div class="char-text">
						<span class="char-wins">
							{c.victories} {c.victories === 1 ? 'win' : 'wins'} <span class="char-runs"> of {c.runs} runs</span>
						</span>
						<span class="char-asc">Max ascension {c.highestAscension}</span>
					</div>
				</li>
			{/each}
		</ul>
	{/if}

	{#if stats.allies.length > 0}
		<h3 class="section-heading">Played with</h3>
		<ul class="allies">
			{#each stats.allies.slice(0, ALLY_LIMIT) as ally (ally.steam)}
				<li>
					<AllyTallyCard tally={ally} />
				</li>
			{/each}
		</ul>
	{/if}
</section>

<style>
	.wrap {
		display: flex;
		flex-direction: column;
		gap: 1rem;
	}

	.fields {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(10rem, 1fr));
		gap: 0.75rem;
	}

	.field {
		padding: 0.75rem 1rem;
		background: var(--bg-card);
		border: 1px solid var(--border-subtle);
		border-radius: var(--radius);
	}

	dt {
		font-size: 0.75rem;
		text-transform: uppercase;
		letter-spacing: 0.06em;
		color: var(--text-muted);
		margin-bottom: 0.15rem;
	}

	dd {
		font-size: 1rem;
		font-weight: 500;
	}

	.muted {
		color: var(--text-muted);
		font-weight: 400;
		font-size: 0.9em;
	}

	.chars {
		list-style: none;
		display: flex;
		flex-wrap: wrap;
		gap: 0.5rem;
		padding: 0;
		margin: 0;
	}

	.char {
		display: flex;
		align-items: center;
		gap: 0.6rem;
		padding: 0.4rem 0.75rem 0.4rem 0.5rem;
		background: var(--bg-card);
		border: 1px solid var(--border-subtle);
		border-radius: var(--radius);
	}

	/* Matches the sidebar breakpoint in [actor]/+page.svelte — at that size
	   the stats live in a narrow right-hand column, so each char box should
	   span the full width of that column on its own line. */
	@media (min-width: 60rem) {
		.chars {
			flex-direction: column;
			gap: 0.4rem;
		}

		.char {
			width: 100%;
		}
	}

	.char-text {
		display: flex;
		flex-direction: column;
		line-height: 1.15;
	}

	.char-wins {
		font-family: var(--font-display);
		font-weight: 700;
		font-size: 1.05rem;
		color: var(--accent-gold);
		line-height: 1.1;
	}

	.char-runs {
		color: var(--text-muted);
		font-weight: 400;
		font-size: 0.85rem;
	}

	.char-asc {
		color: var(--text-secondary);
		font-size: 0.85rem;
	}

	.section-heading {
		font-family: var(--font-display);
		font-size: 0.95rem;
		color: var(--text-secondary);
		margin-top: 0.25rem;
	}

	.allies {
		list-style: none;
		display: flex;
		flex-wrap: wrap;
		gap: 0.5rem;
		padding: 0;
		margin: 0;
	}

	.allies li {
		display: contents;
	}

	@media (min-width: 60rem) {
		.allies {
			flex-direction: column;
			gap: 0.4rem;
		}

		.allies :global(.ally) {
			width: 100%;
		}
	}
</style>
