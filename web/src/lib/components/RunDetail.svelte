<script lang="ts">
	import type { RunRecord } from '$lib/api/types';
	import { COLLECTION } from '$lib/api/pds';
	import { humanizeId, formatAscension, formatDuration } from '$lib/utils/format';
	import OutcomeBadge from './OutcomeBadge.svelte';
	import AttestationBadge from './AttestationBadge.svelte';
	import DeckList from './DeckList.svelte';
	import RelicList from './RelicList.svelte';
	import PotionList from './PotionList.svelte';
	import PlayerCard from './PlayerCard.svelte';
	import StatsPanel from './StatsPanel.svelte';
	import HpBar from './HpBar.svelte';
	import { verifyRecord, type VerifyResult } from '$lib/attestation/verify';
	import { loadTrustedModKeys } from '$lib/attestation/keys';

	let {
		run,
		did,
		tid
	}: { run: RunRecord; did: string; tid: string } = $props();

	let attestation = $state<VerifyResult | 'loading'>('loading');

	$effect(() => {
		// Re-run verification whenever the run or its repo changes.
		let cancelled = false;
		attestation = 'loading';
		(async () => {
			try {
				const trustedKeys = await loadTrustedModKeys();
				const result = await verifyRecord({
					record: run as unknown as Record<string, unknown>,
					repository: did,
					trustedKeys
				});
				if (!cancelled) attestation = result;
			} catch (err) {
				if (!cancelled) {
					attestation = { status: 'invalid', reason: (err as Error).message };
				}
			}
		})();
		return () => {
			cancelled = true;
		};
	});

	const dateFmt = new Intl.DateTimeFormat('en-GB', {
		day: 'numeric',
		month: 'short',
		year: 'numeric'
	});

	function formatDate(iso: string | undefined) {
		if (!iso) return null;
		const d = new Date(iso);
		if (isNaN(d.getTime())) return null;
		return dateFmt.format(d);
	}

	const timestamp = $derived(
		run.endedAt
			? { label: 'Ended', date: formatDate(run.endedAt) }
			: run.updatedAt
				? { label: 'Updated', date: formatDate(run.updatedAt) }
				: run.startedAt
					? { label: 'Started', date: formatDate(run.startedAt) }
					: null
	);
	const pdslsUrl = $derived(`https://pdsls.dev/at://${did}/${COLLECTION}/${tid}`);
	const atUri = $derived(`at://${did}/${COLLECTION}/${tid}`);

	const hasAllies = $derived(!!run.allies && run.allies.length > 0);
	const selfPlayer = $derived({ atproto: did, steam: run.steamID64 });

	const compareHref = $derived.by(() => {
		const allyDids = (run.allies ?? [])
			.map((a) => a.atproto)
			.filter((d): d is string => !!d);
		if (allyDids.length === 0) return null;
		const params = new URLSearchParams();
		params.append('did', did);
		for (const d of allyDids) params.append('did', d);
		return `/multiplayer/${tid}#${params.toString()}`;
	});
</script>

{#snippet titleRow()}
	<h2>{humanizeId(run.character)}</h2>
	<span class="ascension">{formatAscension(run.ascension)}</span>
	<OutcomeBadge outcome={run.outcome} />
	<AttestationBadge result={attestation} />
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
		{#if run.durationSeconds != null || timestamp}
			<div class="field">
				{#if run.durationSeconds != null}
					<dt>Duration</dt>
					<dd>
						{formatDuration(run.durationSeconds)}
						{#if timestamp}
							<div class="time">{timestamp.label} {timestamp.date}</div>
						{/if}
					</dd>
				{:else if timestamp}
					<dt>{timestamp.label}</dt>
					<dd>{timestamp.date}</dd>
				{/if}
			</div>
		{/if}
		{#if run.outcome === 'death' && run.killedBy}
			<div class="field">
				<dt>Killed By</dt>
				<dd class="death">{humanizeId(run.killedBy)}</dd>
			</div>
		{/if}
	</dl>
{/snippet}

<div class="detail" typeof="schema:Thing" resource={atUri}>
	{#if hasAllies}
		<!-- With allies: grid with a dedicated players column (wide) or row (narrow). -->
		<div class="layout">
			<div class="header">
				{@render titleRow()}
			</div>
			<aside class="players">
				<div class="self-group">
					<PlayerCard player={selfPlayer} preferLocal compact />
					<span class="with">with</span>
				</div>
				{#each run.allies ?? [] as ally}
					<PlayerCard player={ally} {tid} compact />
				{/each}
				{#if compareHref}
					<a class="compare-link" href={compareHref}>Compare→</a>
				{/if}
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

	{#if run.outcome !== 'death' && run.maxHp != null && run.maxHp > 0}
		<HpBar currentHp={run.currentHp ?? 0} maxHp={run.maxHp} />
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

	{#if run.deck && run.deck.length > 0}
		<section>
			<h3>Deck ({run.deck.length})</h3>
			<DeckList cards={run.deck} cardUseDistribution={run.stats?.cardUseDistribution} />
		</section>
	{/if}

	{#if run.stats}
		<StatsPanel stats={run.stats} />
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

	.compare-link {
		flex-basis: 100%;
		text-align: center;
		font-variant: small-caps;
		font-size: 0.8rem;
		color: var(--text-muted);
		letter-spacing: 0.05em;
		margin-top: 0.15rem;
	}

	/* Narrow layout: let the self card and "with" sit inline alongside allies. */
	.self-group {
		display: contents;
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

		/* Wide layout: stack self + "with" as a column so "with" centres on
		   the self card's width (not the full players column's width). */
		.self-group {
			display: flex;
			flex-direction: column;
			align-items: center;
			gap: 0.2rem;
		}

		/* Wide layout: sit as the next item below the player-card stack
		   (column flex), centred horizontally instead of right-aligned with
		   the cards. flex-basis returns to auto so it doesn't eat the
		   column's main-axis height. */
		.compare-link {
			flex-basis: auto;
			align-self: center;
		}
	}
</style>
