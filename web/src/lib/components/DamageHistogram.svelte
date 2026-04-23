<script lang="ts">
	import { onDestroy, onMount } from 'svelte';
	import { Chart } from 'chart.js/auto';
	import type { ChartConfiguration } from 'chart.js';

	let {
		distribution,
		accent,
		direction
	}: {
		distribution: unknown;
		accent: 'gold' | 'red';
		direction: 'dealt' | 'taken';
	} = $props();

	type Bar = { damage: number; count: number };

	const bars = $derived<Bar[]>(
		(() => {
			if (!distribution || typeof distribution !== 'object') return [];
			const raw = distribution as Record<string, unknown>;
			const pairs: [number, number][] = [];
			for (const [k, v] of Object.entries(raw)) {
				const damage = Number.parseInt(k, 10);
				if (!Number.isFinite(damage)) continue;
				if (typeof v !== 'number' || v <= 0) continue;
				pairs.push([damage, v]);
			}
			if (pairs.length === 0) return [];
			pairs.sort((a, b) => a[0] - b[0]);
			const min = Math.min(pairs[0][0], 1);
			const max = pairs[pairs.length - 1][0];
			const lookup = new Map(pairs);
			const out: Bar[] = [];
			for (let d = min; d <= max; d++) {
				out.push({ damage: d, count: lookup.get(d) ?? 0 });
			}
			return out;
		})()
	);

	const total = $derived(bars.reduce((sum, b) => sum + b.count, 0));

	let canvas: HTMLCanvasElement;
	let chart: Chart<'bar'> | null = null;

	function readVar(name: string, fallback: string): string {
		if (typeof document === 'undefined') return fallback;
		const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
		return v || fallback;
	}

	// Blend two hex colors: amount=0 → a, amount=1 → b.
	function blend(a: string, b: string, amount: number): string {
		const parse = (h: string) => {
			h = h.replace('#', '').trim();
			if (h.length === 3)
				h = h
					.split('')
					.map((c) => c + c)
					.join('');
			return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)];
		};
		const [r1, g1, b1] = parse(a);
		const [r2, g2, b2] = parse(b);
		return `rgb(${Math.round(r1 * (1 - amount) + r2 * amount)}, ${Math.round(g1 * (1 - amount) + g2 * amount)}, ${Math.round(b1 * (1 - amount) + b2 * amount)})`;
	}

	function buildConfig(): ChartConfiguration<'bar'> {
		const accentHex = readVar(
			accent === 'gold' ? '--accent-gold' : '--accent-red',
			accent === 'gold' ? '#d4a843' : '#c0392b'
		);
		const dim = blend(accentHex, '#000000', 0.7);
		const bgCard = readVar('--bg-card', '#1a1a1a');
		const borderCard = readVar('--border-card', '#333');
		const borderSubtle = readVar('--border-subtle', '#2a2a2a');
		const textPrimary = readVar('--text-primary', '#e8e0d4');
		const textSecondary = readVar('--text-secondary', '#9a8e7e');
		const textMuted = readVar('--text-muted', '#665e52');

		return {
			type: 'bar',
			data: {
				labels: bars.map((b) => String(b.damage)),
				datasets: [
					{
						data: bars.map((b) => b.count),
						backgroundColor: (ctx) => {
							const bar = bars[ctx.dataIndex];
							if (!bar || bar.count === 0) return blend(borderSubtle, '#000', 0);
							const area = ctx.chart.chartArea;
							if (!area) return accentHex;
							const g = ctx.chart.ctx.createLinearGradient(0, area.bottom, 0, area.top);
							g.addColorStop(0, dim);
							g.addColorStop(1, accentHex);
							return g;
						},
						borderRadius: 2,
						borderSkipped: false,
						categoryPercentage: 1,
						barPercentage: 0.9,
						minBarLength: 2
					}
				]
			},
			options: {
				responsive: true,
				maintainAspectRatio: false,
				animation: {
					duration: 450,
					easing: 'easeOutQuart',
					delay: (ctx) => (ctx.type === 'data' && ctx.mode === 'default' ? ctx.dataIndex * 18 : 0)
				},
				plugins: {
					legend: { display: false },
					tooltip: {
						displayColors: false,
						padding: 6,
						backgroundColor: bgCard,
						borderColor: borderCard,
						borderWidth: 1,
						titleColor: textPrimary,
						bodyColor: textPrimary,
						titleFont: { size: 12 },
						bodyFont: { size: 12 },
						filter: (item) => (item.parsed.y ?? 0) > 0,
						callbacks: {
							title: () => '',
							label: (ctx) => {
								const count = ctx.parsed.y ?? 0;
								if (count <= 0) return '';
								const damage = ctx.label;
								const times = count === 1 ? 'once' : `${count} times`;
								return `${damage}-damage hit ${direction} ${times}`;
							}
						}
					}
				},
				scales: {
					x: {
						ticks: {
							autoSkip: true,
							maxTicksLimit: 12,
							color: textSecondary,
							font: { size: 10 }
						},
						grid: { display: false },
						border: { display: false }
					},
					y: {
						beginAtZero: true,
						ticks: {
							color: textMuted,
							font: { size: 10 },
							maxTicksLimit: 5,
							precision: 0
						},
						grid: { color: borderSubtle, drawTicks: false },
						border: { display: false }
					}
				}
			}
		};
	}

	onMount(() => {
		if (bars.length === 0) return;
		chart = new Chart(canvas, buildConfig());
	});

	onDestroy(() => {
		chart?.destroy();
		chart = null;
	});

	$effect(() => {
		// Retrigger on accent/direction/bars change.
		void bars;
		void accent;
		void direction;
		if (!chart || bars.length === 0) return;
		const fresh = buildConfig();
		chart.data = fresh.data;
		chart.options = fresh.options!;
		chart.update();
	});
</script>

{#if bars.length > 0}
	<figure class="histogram {accent}">
		<div class="plot-wrap">
			<canvas bind:this={canvas}></canvas>
		</div>
		<figcaption>
			<span class="caption-title">Hits {direction}</span>
			<span class="caption-sub">{total} total · biggest {bars[bars.length - 1].damage}</span>
		</figcaption>
	</figure>
{/if}

<style>
	.histogram {
		display: flex;
		flex-direction: column;
		gap: 0.4rem;
		padding: 0.9rem 0.9rem 0.6rem;
		background: var(--bg-card);
		border: 1px solid var(--border-subtle);
		border-radius: var(--radius);
		min-height: 10rem;
		min-width: 0;
		max-width: 100%;
	}

	.plot-wrap {
		position: relative;
		flex: 1;
		min-height: 8rem;
		height: 8rem;
	}

	canvas {
		display: block;
		width: 100% !important;
		height: 100% !important;
	}

	figcaption {
		display: flex;
		justify-content: space-between;
		align-items: baseline;
		gap: 0.75rem;
		margin: 0;
		padding-top: 0.2rem;
		border-top: 1px solid var(--border-subtle);
	}

	.caption-title {
		font-size: 0.68rem;
		text-transform: uppercase;
		letter-spacing: 0.08em;
		color: var(--text-muted);
		font-weight: 600;
	}

	.caption-sub {
		font-size: 0.68rem;
		color: var(--text-muted);
		font-variant-numeric: tabular-nums;
	}
</style>
