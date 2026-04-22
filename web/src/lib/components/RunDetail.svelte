<script lang="ts">
	import type { RunRecord } from '$lib/api/types';
	import { COLLECTION } from '$lib/api/pds';
	import { humanizeId, formatAscension, formatDuration } from '$lib/utils/format';
	import OutcomeBadge from './OutcomeBadge.svelte';
	import DeckList from './DeckList.svelte';
	import RelicList from './RelicList.svelte';
	import PotionList from './PotionList.svelte';
	import PlayerCard from './PlayerCard.svelte';

	let {
		run,
		did,
		tid
	}: { run: RunRecord; did: string; tid: string } = $props();

	const dateFmt = new Intl.DateTimeFormat('en-GB', {
		day: 'numeric',
		month: 'short',
		year: 'numeric'
	});
	const timeFmt = new Intl.DateTimeFormat('en-GB', {
		hour: '2-digit',
		minute: '2-digit',
		hour12: false
	});

	function splitDate(iso: string | undefined) {
		if (!iso) return null;
		const d = new Date(iso);
		if (isNaN(d.getTime())) return null;
		return { date: dateFmt.format(d), time: timeFmt.format(d) };
	}

	const started = $derived(splitDate(run.startedAt));
	const ended = $derived(splitDate(run.endedAt));
	const updated = $derived(ended ? null : splitDate(run.updatedAt));
	const pdslsUrl = $derived(`https://pdsls.dev/at://${did}/${COLLECTION}/${tid}`);

	const hasAllies = $derived(!!run.allies && run.allies.length > 0);
	const selfPlayer = $derived({ atproto: did, steam: run.steamID64 });
</script>

{#snippet titleRow()}
	<h2>{humanizeId(run.character)}</h2>
	<span class="ascension">{formatAscension(run.ascension)}</span>
	<OutcomeBadge outcome={run.outcome} />
{/snippet}

{#snippet statsBoxes()}
	<dl class="fields">
		{#if run.act != null}
			<div class="field">
				<dt>Act</dt>
				<dd>{run.act}</dd>
			</div>
		{/if}
		{#if run.floor != null}
			<div class="field">
				<dt>Floor</dt>
				<dd>{run.floor}</dd>
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
		{#if run.outcome === 'death' && run.killedBy}
			<div class="field">
				<dt>Killed By</dt>
				<dd class="death">{humanizeId(run.killedBy)}</dd>
			</div>
		{/if}
	</dl>

	{#if started || ended || updated}
		<dl class="fields">
			{#if started}
				{@const s = started}
				<div class="field">
					<dt>Started</dt>
					<dd>
						{s.date}
						<div class="time">{s.time}</div>
					</dd>
				</div>
			{/if}
			{#if ended}
				{@const e = ended}
				<div class="field">
					<dt>Ended</dt>
					<dd>
						{e.date}
						<div class="time">{e.time}</div>
					</dd>
				</div>
			{:else if updated}
				{@const u = updated}
				<div class="field">
					<dt>Updated</dt>
					<dd>
						{u.date}
						<div class="time">{u.time}</div>
					</dd>
				</div>
			{/if}
		</dl>
	{/if}
{/snippet}

<div class="detail">
	{#if hasAllies}
		<!-- With allies: grid with a dedicated players column (wide) or row (narrow). -->
		<div class="layout">
			<div class="header">
				{@render titleRow()}
			</div>
			<aside class="players">
				<PlayerCard player={selfPlayer} preferLocal compact />
				<span class="with">with</span>
				{#each run.allies ?? [] as ally}
					<PlayerCard player={ally} {tid} compact />
				{/each}
			</aside>
			<div class="stats-area">
				{@render statsBoxes()}
			</div>
		</div>
	{:else}
		<!-- No allies: current layout — self pinned to the right of the title. -->
		<div class="header">
			{@render titleRow()}
			<div class="actor">
				<PlayerCard player={selfPlayer} preferLocal compact />
			</div>
		</div>
		{@render statsBoxes()}
	{/if}

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

	{#if run.potions && run.potions.length > 0}
		<section>
			<h3>Potions ({run.potions.length})</h3>
			<PotionList potions={run.potions} />
		</section>
	{/if}

	<div class="meta">
		<span class="mono">Seed: {run.seed}</span>
		{#if run.modVersion}<span>Mod v{run.modVersion}</span>{/if}
		{#if run.gameVersion}<span>Game v{run.gameVersion}</span>{/if}
		<a href={pdslsUrl} target="_blank" rel="noopener noreferrer">View on PDSls</a>
	</div>
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

	.actor {
		margin-left: auto;
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

	.time {
		font-size: 0.8rem;
		color: var(--text-muted);
		font-weight: 400;
		margin-top: 0.15rem;
	}

	.meta {
		display: flex;
		flex-wrap: wrap;
		gap: 1rem;
		color: var(--text-muted);
		font-size: 0.8rem;
		padding-top: 1rem;
		border-top: 1px solid var(--border-subtle);
	}

	/* --- Multiplayer (has-allies) layout ------------------------------------- */

	.layout {
		display: grid;
		gap: 1rem;
		grid-template-columns: 1fr;
		grid-template-areas:
			'title'
			'players'
			'stats';
	}

	.layout > .header {
		grid-area: title;
	}

	.stats-area {
		grid-area: stats;
		display: flex;
		flex-direction: column;
		gap: 0.75rem;
	}

	.players {
		grid-area: players;
		display: flex;
		flex-wrap: wrap;
		align-items: center;
		justify-content: flex-start;
		gap: 0.5rem;
		margin: 0;
	}

	.with {
		font-variant: small-caps;
		color: var(--text-muted);
		font-size: 0.8rem;
		letter-spacing: 0.05em;
	}

	@media (min-width: 45rem) {
		.layout {
			grid-template-columns: 1fr auto;
			grid-template-areas:
				'title   players'
				'stats   players';
			column-gap: 1.5rem;
		}

		.players {
			flex-direction: column;
			align-items: flex-end;
			gap: 0.35rem;
		}
	}
</style>
