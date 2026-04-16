<script lang="ts">
	import type { RunRecord } from '$lib/api/types';
	import { humanizeId, formatAscension, formatDuration } from '$lib/utils/format';
	import OutcomeBadge from './OutcomeBadge.svelte';
	import DeckList from './DeckList.svelte';
	import RelicList from './RelicList.svelte';

	let { run }: { run: RunRecord } = $props();

	function formatDate(iso: string): string {
		return new Date(iso).toLocaleString();
	}
</script>

<div class="detail">
	<div class="header">
		<h2>{humanizeId(run.character)}</h2>
		<span class="ascension">{formatAscension(run.ascension)}</span>
		<OutcomeBadge outcome={run.outcome} />
	</div>

	<dl class="fields">
		{#if run.floor != null}
			<div class="field">
				<dt>Floor</dt>
				<dd>{run.floor}</dd>
			</div>
		{/if}
		{#if run.act != null}
			<div class="field">
				<dt>Act</dt>
				<dd>{run.act}</dd>
			</div>
		{/if}
		{#if run.score != null}
			<div class="field">
				<dt>Score</dt>
				<dd>{run.score}</dd>
			</div>
		{/if}
		{#if run.durationSeconds != null}
			<div class="field">
				<dt>Duration</dt>
				<dd>{formatDuration(run.durationSeconds)}</dd>
			</div>
		{/if}
		<div class="field">
			<dt>Seed</dt>
			<dd class="mono">{run.seed}</dd>
		</div>
		{#if run.outcome === 'death' && run.killedBy}
			<div class="field">
				<dt>Killed By</dt>
				<dd class="death">{humanizeId(run.killedBy)}</dd>
			</div>
		{/if}
	</dl>

	<dl class="fields">
		<div class="field">
			<dt>Started</dt>
			<dd>{formatDate(run.startedAt)}</dd>
		</div>
		{#if run.endedAt}
			<div class="field">
				<dt>Ended</dt>
				<dd>{formatDate(run.endedAt)}</dd>
			</div>
		{/if}
	</dl>

	{#if run.deck && run.deck.length > 0}
		<section>
			<h3>Deck ({run.deck.length})</h3>
			<DeckList cards={run.deck} />
		</section>
	{/if}

	{#if run.relics && run.relics.length > 0}
		<section>
			<h3>Relics ({run.relics.length})</h3>
			<RelicList relics={run.relics} />
		</section>
	{/if}

	{#if run.modVersion || run.gameVersion}
		<div class="meta">
			{#if run.modVersion}<span>Mod v{run.modVersion}</span>{/if}
			{#if run.gameVersion}<span>Game v{run.gameVersion}</span>{/if}
		</div>
	{/if}
</div>

<style>
	.detail {
		display: flex;
		flex-direction: column;
		gap: 1.5rem;
	}

	.header {
		display: flex;
		align-items: center;
		gap: 0.75rem;
		flex-wrap: wrap;
	}

	h2 {
		font-size: 1.75rem;
		color: var(--accent-gold);
	}

	.ascension {
		color: var(--text-secondary);
		font-size: 1rem;
	}

	h3 {
		font-size: 1rem;
		margin-bottom: 0.6rem;
		color: var(--text-secondary);
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

	.mono {
		font-family: monospace;
	}

	.death {
		color: var(--accent-red);
	}

	.meta {
		display: flex;
		gap: 1rem;
		color: var(--text-muted);
		font-size: 0.8rem;
	}
</style>
