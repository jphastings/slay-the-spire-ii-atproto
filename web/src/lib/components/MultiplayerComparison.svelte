<script lang="ts">
	import type { RunRecord } from '$lib/api/types';
	import PlayerCard from './PlayerCard.svelte';
	import { computeComparison, formatScore, type PlayerInput } from '$lib/utils/multiplayer-metrics';

	let {
		players,
		tid
	}: {
		players: { did: string; run: RunRecord }[];
		tid: string;
	} = $props();

	const result = $derived(computeComparison(players as PlayerInput[]));

	const orderedDids = $derived(result.playerOrder);
	const cellsByDid = $derived(
		result.rows.map((row) => {
			const byDid = new Map(row.cells.map((c) => [c.did, c]));
			return { row, byDid };
		})
	);
</script>

<div class="scroll">
	<table class="compare" style="--cols: {orderedDids.length}">
		<thead>
			<tr>
				<th scope="col" class="corner" aria-hidden="true"></th>
				{#each orderedDids as did (did)}
					{@const score = result.scores[did] ?? 0}
					<th scope="col" class="player-col">
						<div class="player-header">
							<PlayerCard player={{ atproto: did }} {tid} preferLocal compact />
							<div
								class="score"
								class:positive={score > 0}
								class:negative={score < 0}
								class:zero={score === 0}
							>
								{formatScore(score)}
							</div>
						</div>
					</th>
				{/each}
			</tr>
		</thead>
		<tbody>
			{#each cellsByDid as { row, byDid } (row.metric.id)}
				{@const multiplier = Math.abs(row.metric.weight)}
				<tr>
					<th scope="row" class="metric-label" title={row.metric.description}>
						{row.metric.label}
						{#if multiplier >= 2}<span class="multiplier">×{multiplier}</span>{/if}
					</th>
					{#each orderedDids as did (did)}
						{@const cell = byDid.get(did)}
						<td
							class="value"
							class:winner-gold={cell?.highlight === 'gold'}
							class:winner-red={cell?.highlight === 'red'}
							class:empty={cell?.value === null}
						>
							{cell?.display ?? '—'}
						</td>
					{/each}
				</tr>
			{/each}
		</tbody>
	</table>
</div>

{#if result.rows.length === 0}
	<p class="empty-message">Not enough comparable data across these runs yet.</p>
{/if}

<style>
	.scroll {
		overflow-x: auto;
		margin: 0 -0.5rem;
		padding: 0 0.5rem;
	}

	.compare {
		border-collapse: collapse;
		width: 100%;
		min-width: fit-content;
	}

	th,
	td {
		padding: 0.6rem 0.9rem;
		text-align: left;
		vertical-align: middle;
		border-bottom: 1px solid var(--border-subtle);
	}

	.corner {
		border-bottom: none;
	}

	.player-col {
		text-align: center;
		padding: 0.75rem 0.75rem 1rem;
		border-bottom: 1px solid var(--border-subtle);
	}

	.player-header {
		display: flex;
		flex-direction: column;
		align-items: center;
		gap: 0.4rem;
	}

	.score {
		font-variant-numeric: tabular-nums;
		font-weight: 600;
		font-size: 1rem;
		line-height: 1;
	}

	.score.positive {
		color: var(--accent-gold);
	}

	.score.negative {
		color: var(--accent-red);
	}

	.score.zero {
		color: var(--text-muted);
	}

	.metric-label {
		font-weight: 500;
		color: var(--text-secondary);
		white-space: nowrap;
		cursor: help;
	}

	.value {
		text-align: center;
		font-variant-numeric: tabular-nums;
		min-width: 6rem;
	}

	.multiplier {
		margin-left: 0.4rem;
		color: var(--text-muted);
		font-weight: 400;
		font-size: 0.85em;
	}

	.winner-gold {
		color: var(--accent-gold);
		font-weight: 600;
	}

	.winner-red {
		color: var(--accent-red);
		font-weight: 600;
	}

	.empty {
		color: var(--text-muted);
	}

	.empty-message {
		color: var(--text-secondary);
		padding: 1rem 0;
		text-align: center;
	}

	tbody tr:last-child th,
	tbody tr:last-child td {
		border-bottom: none;
	}

	/* Narrow viewports: stack each row as "description above cells". */
	@media (max-width: 40rem) {
		.scroll {
			overflow-x: visible;
			margin: 0;
			padding: 0;
		}

		.compare,
		.compare thead,
		.compare tbody,
		.compare tr {
			display: block;
		}

		.compare thead tr,
		.compare tbody tr {
			display: grid;
			grid-template-columns: repeat(var(--cols), minmax(0, 1fr));
			column-gap: 0.5rem;
		}

		.compare thead .corner {
			display: none;
		}

		.compare thead .player-col {
			padding: 0.5rem 0.25rem 0.75rem;
		}

		.compare tbody tr {
			border-bottom: 1px solid var(--border-subtle);
			padding: 0.35rem 0 0.55rem;
		}

		.compare tbody tr:last-child {
			border-bottom: none;
		}

		.compare tbody .metric-label {
			grid-column: 1 / -1;
			white-space: normal;
			text-align: center;
			padding: 0.3rem 0 0.25rem;
			border-bottom: none;
		}

		.compare tbody .value {
			padding: 0.25rem;
			min-width: 0;
			border-bottom: none;
		}
	}
</style>
