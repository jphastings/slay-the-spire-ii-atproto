<script lang="ts">
	import type { RunRecord } from '$lib/api/types';
	import { humanizeId, formatAscension, formatDuration, formatRelativeTime } from '$lib/utils/format';
	import OutcomeBadge from './OutcomeBadge.svelte';
	import AllyAvatar from './AllyAvatar.svelte';

	let { run, href, resource }: { run: RunRecord; href: string; resource: string } = $props();

	const allies = $derived(run.allies ?? []);
</script>

<a {href} class="card" typeof="schema:Thing" {resource}>
	{#if allies.length > 0}
		<div class="allies" aria-label="Played with">
			{#each allies as ally}
				<AllyAvatar player={ally} />
			{/each}
		</div>
	{/if}
	<div class="header">
		<span class="character">{humanizeId(run.character)}</span>
		<span class="ascension">{formatAscension(run.ascension)}</span>
		<OutcomeBadge outcome={run.outcome} />
	</div>

	<div class="details">
		{#if run.act != null}
			<span>Act {run.act}</span>
		{/if}
		{#if run.floor != null}
			<span>Floor {run.floor}</span>
		{/if}
		{#if run.score != null}
			<span>{run.score} pts</span>
		{/if}
		{#if run.durationSeconds != null}
			<span>{formatDuration(run.durationSeconds)}</span>
		{/if}
	</div>

	{#if run.outcome === 'death' && run.killedBy}
		<p class="killed-by">Killed by {humanizeId(run.killedBy)}</p>
	{/if}

	<p class="time">{formatRelativeTime(run.updatedAt)}</p>
</a>

<style>
	.card {
		position: relative;
		display: block;
		padding: 1rem 1.25rem;
		background: var(--bg-card);
		border: 1px solid var(--border-card);
		border-radius: var(--radius);
		box-shadow: var(--shadow-card);
		color: var(--text-primary);
		transition:
			background 0.15s,
			border-color 0.15s;
	}

	.allies {
		position: absolute;
		top: 0.6rem;
		right: 0.6rem;
		display: flex;
		gap: 0.25rem;
	}

	.card:hover {
		background: var(--bg-card-hover);
		border-color: var(--accent-gold);
		color: var(--text-primary);
	}

	.header {
		display: flex;
		align-items: center;
		gap: 0.6rem;
		flex-wrap: wrap;
	}

	.character {
		font-family: var(--font-display);
		font-size: 1.1rem;
		font-weight: 700;
	}

	.ascension {
		color: var(--text-secondary);
		font-size: 0.9rem;
	}

	.details {
		display: flex;
		gap: 1rem;
		margin-top: 0.5rem;
		color: var(--text-secondary);
		font-size: 0.9rem;
	}

	.killed-by {
		margin-top: 0.4rem;
		color: var(--accent-red);
		font-size: 0.85rem;
	}

	.time {
		margin-top: 0.4rem;
		color: var(--text-muted);
		font-size: 0.8rem;
	}
</style>
