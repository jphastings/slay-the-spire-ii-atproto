import type { RunRecord } from '$lib/api/types';

export interface CharacterTally {
	character: string;
	runs: number;
	victories: number;
	highestAscension: number;
}

export interface RecentMonthRatio {
	month: string; // YYYY-MM
	victories: number;
	losses: number; // death + abandoned
}

export interface AllyTally {
	steam: string;
	atproto?: string;
	games: number;
	highestAscension: number;
}

export interface ProfileStats {
	totalRuns: number;
	victories: number;
	hitsDealt: number;
	monstersKilled: number;
	hpHealed: number;
	goldEarned: number;
	deaths: number;
	characters: CharacterTally[];
	allies: AllyTally[];
	recentMonth: RecentMonthRatio | null;
}

function sumDistribution(dist: unknown): number {
	if (!dist || typeof dist !== 'object') return 0;
	let total = 0;
	for (const v of Object.values(dist as Record<string, unknown>)) {
		if (typeof v === 'number' && Number.isFinite(v)) total += v;
	}
	return total;
}

function monthKey(iso: string | undefined): string | null {
	if (!iso) return null;
	const d = new Date(iso);
	if (isNaN(d.getTime())) return null;
	const y = d.getFullYear();
	const m = String(d.getMonth() + 1).padStart(2, '0');
	return `${y}-${m}`;
}

function asNumber(v: unknown): number {
	return typeof v === 'number' && Number.isFinite(v) ? v : 0;
}

export function computeProfileStats(runs: RunRecord[]): ProfileStats {
	let victories = 0;
	let hitsDealt = 0;
	let monstersKilled = 0;
	let hpHealed = 0;
	let goldEarned = 0;
	let deaths = 0;
	const tallies = new Map<string, CharacterTally>();
	const allyTallies = new Map<string, AllyTally>();
	const monthBuckets = new Map<string, { victories: number; losses: number }>();

	for (const run of runs) {
		if (run.outcome === 'victory') victories++;
		hitsDealt += sumDistribution(run.stats?.hitsDealtDistribution);
		monstersKilled += asNumber(run.stats?.killCount);
		hpHealed += asNumber(run.stats?.healingReceived);
		goldEarned += asNumber(run.stats?.goldEarned);
		deaths += asNumber(run.stats?.deaths);

		const char = run.character;
		if (char) {
			const prev = tallies.get(char) ?? {
				character: char,
				runs: 0,
				victories: 0,
				highestAscension: 0
			};
			prev.runs++;
			if (run.outcome === 'victory') prev.victories++;
			if (typeof run.ascension === 'number' && run.ascension > prev.highestAscension) {
				prev.highestAscension = run.ascension;
			}
			tallies.set(char, prev);
		}

		// Steam is required on every ally entry, so it's a stable dedup key
		// even when atproto resolution is intermittent across runs.
		for (const ally of run.allies ?? []) {
			if (!ally.steam) continue;
			const prev = allyTallies.get(ally.steam) ?? {
				steam: ally.steam,
				atproto: ally.atproto,
				games: 0,
				highestAscension: 0
			};
			prev.games++;
			if (!prev.atproto && ally.atproto) prev.atproto = ally.atproto;
			if (typeof run.ascension === 'number' && run.ascension > prev.highestAscension) {
				prev.highestAscension = run.ascension;
			}
			allyTallies.set(ally.steam, prev);
		}

		const key = monthKey(run.updatedAt ?? run.startedAt);
		if (key && (run.outcome === 'victory' || run.outcome === 'death' || run.outcome === 'abandoned')) {
			const bucket = monthBuckets.get(key) ?? { victories: 0, losses: 0 };
			if (run.outcome === 'victory') bucket.victories++;
			else bucket.losses++;
			monthBuckets.set(key, bucket);
		}
	}

	const characters = [...tallies.values()].sort((a, b) => b.runs - a.runs);
	const allies = [...allyTallies.values()].sort(
		(a, b) => b.games - a.games || b.highestAscension - a.highestAscension
	);

	let recentMonth: RecentMonthRatio | null = null;
	const sortedMonths = [...monthBuckets.keys()].sort();
	const latest = sortedMonths.at(-1);
	if (latest) {
		const bucket = monthBuckets.get(latest)!;
		recentMonth = { month: latest, victories: bucket.victories, losses: bucket.losses };
	}

	return {
		totalRuns: runs.length,
		victories,
		hitsDealt,
		monstersKilled,
		hpHealed,
		goldEarned,
		deaths,
		characters,
		allies,
		recentMonth
	};
}

export function formatMonth(ym: string): string {
	const [y, m] = ym.split('-').map(Number);
	const d = new Date(y, m - 1, 1);
	return d.toLocaleDateString('en-GB', { month: 'long', year: 'numeric' });
}
