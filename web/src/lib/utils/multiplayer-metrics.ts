import type { RunRecord } from '$lib/api/types';

export interface Metric {
	id: string;
	label: string;
	description?: string;
	/**
	 * Sign determines direction and magnitude is row-sort weight:
	 *   > 0 → higher raw value wins (gold highlight)
	 *   < 0 → higher raw value is the WORST performer (red highlight)
	 *   = 0 → neutral, no highlight and no score contribution
	 */
	weight: number;
	compute: (run: RunRecord) => number | null;
	format?: (value: number, run: RunRecord) => string;
}

const integer = (n: number) => n.toLocaleString('en-GB');

const num = (v: unknown): number | null => (typeof v === 'number' ? v : null);

export const METRICS: Metric[] = [
	{
		id: 'damageDealt',
		label: 'Total damage dealt',
		description: 'Total HP damage dealt by the player to enemies (after block).',
		weight: 1,
		compute: (run) => run.stats?.damageDealt ?? null,
		format: integer
	},
	{
		id: 'damageRatio',
		label: 'Damage taken vs dealt',
		description: 'Ratio of damage taken to damage dealt — lower means a better defender.',
		weight: -1,
		compute: (run) => {
			const dealt = run.stats?.damageDealt;
			const taken = run.stats?.damageTaken;
			if (dealt == null || taken == null || dealt === 0) return null;
			return taken / dealt;
		},
		format: (_, run) => {
			// Always normalised so the left side is 1 (units of damage taken),
			// making the right side directly comparable across players: bigger
			// = dealt more per HP taken.
			const taken = Number(run.stats?.damageTaken ?? 0);
			const dealt = Number(run.stats?.damageDealt ?? 0);
			if (taken === 0) return `0 : ${integer(dealt)}`;
			return `1 : ${(dealt / taken).toFixed(2)}`;
		}
	},
	{
		id: 'biggestTurnDamageDealt',
		label: 'Damage dealt in a turn',
		description: 'Most HP damage the player dealt in one player turn (after block).',
		weight: 1,
		compute: (run) => run.stats?.biggestTurnDamageDealt ?? null,
		format: integer
	},
	{
		id: 'biggestTurnDamageTaken',
		label: 'Damage taken in a turn',
		description: 'Most HP damage the player took in one enemy turn (after block).',
		weight: -1,
		compute: (run) => run.stats?.biggestTurnDamageTaken ?? null,
		format: integer
	},
	{
		id: 'biggestDamageDealt',
		label: 'Biggest single hit dealt',
		description: 'Largest HP damage dealt by the player in one attack (after block).',
		weight: 1,
		compute: (run) => run.stats?.biggestDamageDealt ?? null,
		format: integer
	},
	{
		id: 'biggestDamageTaken',
		label: 'Biggest single hit taken',
		description: 'Largest HP damage taken by the player in one attack (after block).',
		weight: -1,
		compute: (run) => run.stats?.biggestDamageTaken ?? null,
		format: integer
	},
	{
		id: 'maxHp',
		label: 'Max HP',
		description: "Character's maximum HP at run end.",
		weight: 1,
		compute: (run) => num(run.maxHp),
		format: integer
	},
	{
		id: 'currentHp',
		label: 'HP remaining',
		description: 'Current HP at run end (0 if dead at the time of emission).',
		weight: 1,
		compute: (run) => num(run.currentHp),
		format: integer
	},
	{
		id: 'killCount',
		label: 'Killing blows',
		description: 'Monsters this player landed the killing blow on.',
		weight: 2,
		compute: (run) => num(run.stats?.killCount),
		format: integer
	},
	{
		id: 'deaths',
		label: 'Deaths',
		description: 'Times this player went to 0 HP. Co-op allies can revive, so this can exceed 1.',
		weight: -1,
		compute: (run) => num(run.stats?.deaths),
		format: integer
	},
	{
		id: 'healingReceived',
		label: 'HP healed',
		description: 'Total HP healed by this player over the run.',
		weight: 1,
		compute: (run) => num(run.stats?.healingReceived),
		format: integer
	},
	{
		id: 'goldEarned',
		label: 'Gold earned',
		description: 'Total gold gained by this player over the run.',
		weight: 1,
		compute: (run) => num(run.stats?.goldEarned),
		format: integer
	},
	{
		id: 'highestBlockInTurn',
		label: 'Highest block in turn',
		description: 'Most block gained by the player in a single player turn.',
		weight: 1,
		compute: (run) => run.stats?.highestBlockInTurn ?? null,
		format: integer
	},
	{
		id: 'noDamageTurns',
		label: 'Turns unscathed',
		description: 'Enemy turns during which the player took zero unblocked damage.',
		weight: 1,
		compute: (run) => num(run.stats?.noDamageTurns),
		format: integer
	},
	{
		id: 'cardsPlayed',
		label: 'Cards played',
		description: 'Total cards played by this player across the run.',
		weight: 1,
		compute: (run) => num(run.stats?.cardsPlayed),
		format: integer
	},
	{
		id: 'cardsExhausted',
		label: 'Cards exhausted',
		description: 'Cards exhausted by this player (strategy-dependent; neutral).',
		weight: 0,
		compute: (run) => num(run.stats?.cardsExhausted),
		format: integer
	},
	{
		id: 'potionsUsed',
		label: 'Potions used',
		description: 'Potions consumed over the run.',
		weight: 0,
		compute: (run) => num(run.stats?.potionsUsed),
		format: integer
	},
	{
		id: 'relicsHeld',
		label: 'Relics held',
		description: 'Relics in the player’s possession at run end.',
		weight: 1,
		compute: (run) => run.relics?.length ?? null,
		format: integer
	},
	{
		id: 'potionsHeld',
		label: 'Potions held',
		description: 'Potions currently held at run end.',
		weight: 1,
		compute: (run) => run.potions?.length ?? null,
		format: integer
	},
	{
		id: 'deckSize',
		label: 'Total card count',
		description: 'Cards in the final deck.',
		weight: 0,
		compute: (run) => run.deck?.length ?? null,
		format: integer
	}
];

export interface PlayerInput {
	did: string;
	run: RunRecord;
}

export interface MetricCell {
	did: string;
	value: number | null;
	display: string;
	highlight: 'gold' | 'red' | null;
}

export interface MetricRow {
	metric: Metric;
	cells: MetricCell[];
	/** max - min across non-null values; used for row sort. */
	variation: number;
}

export interface ComparisonResult {
	rows: MetricRow[];
	/** Ordered left-to-right: highest wins score first. */
	playerOrder: string[];
	scores: Record<string, number>;
}

export function computeComparison(
	players: PlayerInput[],
	metrics: Metric[] = METRICS
): ComparisonResult {
	const scores: Record<string, number> = Object.fromEntries(players.map((p) => [p.did, 0]));
	const rows: MetricRow[] = [];

	for (const metric of metrics) {
		const values = players.map((p) => ({
			did: p.did,
			run: p.run,
			value: metric.compute(p.run)
		}));
		const defined = values.filter(
			(v): v is { did: string; run: RunRecord; value: number } => v.value !== null
		);

		// Skip metrics with too little data or no variation.
		if (defined.length < 2) continue;
		const max = Math.max(...defined.map((v) => v.value));
		const min = Math.min(...defined.map((v) => v.value));
		if (max === min) continue;

		const highlightValue = metric.weight === 0 ? null : max;
		const winners = highlightValue === null ? [] : defined.filter((v) => v.value === highlightValue);
		const highlightColor: 'gold' | 'red' | null =
			metric.weight > 0 ? 'gold' : metric.weight < 0 ? 'red' : null;

		if (winners.length > 0 && metric.weight !== 0) {
			const share = metric.weight / winners.length;
			for (const w of winners) scores[w.did] += share;
		}

		const winnerDids = new Set(winners.map((w) => w.did));
		const cells: MetricCell[] = values.map((v) => ({
			did: v.did,
			value: v.value,
			display:
				v.value === null ? '—' : metric.format ? metric.format(v.value, v.run) : String(v.value),
			highlight: v.value !== null && winnerDids.has(v.did) ? highlightColor : null
		}));

		rows.push({ metric, cells, variation: max - min });
	}

	rows.sort((a, b) => b.variation * Math.abs(b.metric.weight) - a.variation * Math.abs(a.metric.weight));

	const playerOrder = [...players]
		.map((p) => p.did)
		.sort((a, b) => scores[b] - scores[a] || a.localeCompare(b));

	return { rows, playerOrder, scores };
}

/** Render a wins score like `+4`, `−2.5`, or `0`. Uses a typographic minus. */
export function formatScore(score: number): string {
	if (score === 0) return '0';
	const rounded = Math.round(score * 10) / 10;
	const abs = Math.abs(rounded);
	const str = Number.isInteger(abs) ? String(abs) : abs.toFixed(1);
	return rounded > 0 ? `+${str}` : `−${str}`;
}
