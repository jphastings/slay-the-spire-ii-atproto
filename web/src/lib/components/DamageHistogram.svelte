<script lang="ts">
	import Tooltip from './Tooltip.svelte';

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

	const maxCount = $derived(bars.reduce((m, b) => (b.count > m ? b.count : m), 0));
	const total = $derived(bars.reduce((sum, b) => sum + b.count, 0));

	function tooltipFor(b: Bar): string {
		const times = b.count === 1 ? 'once' : `${b.count} times`;
		return `${b.damage}-damage hit ${direction} ${times}`;
	}

	function tickValues(n: number): number[] {
		if (n <= 0) return [];
		if (n <= 4) return Array.from({ length: n }, (_, i) => i + 1);
		const step = Math.ceil(n / 4);
		const out: number[] = [];
		for (let v = step; v <= n; v += step) out.push(v);
		if (out[out.length - 1] !== n) out.push(n);
		return out;
	}

	// Pick a "nice" step so roughly <=12 axis labels are visible.
	function labelStep(n: number): number {
		if (n <= 12) return 1;
		const nice = [2, 5, 10, 20, 25, 50, 100, 200, 500, 1000];
		for (const s of nice) {
			if (Math.ceil(n / s) <= 12) return s;
		}
		return nice[nice.length - 1];
	}

	const ticks = $derived(tickValues(maxCount));
	const step = $derived(labelStep(bars.length));
</script>

{#if bars.length > 0}
	<figure class="histogram {accent}">
		<div class="plot">
			<div class="grid" aria-hidden="true">
				{#each ticks as t}
					<div class="grid-line" style="bottom: {(t / maxCount) * 100}%">
						<span class="tick">{t}</span>
					</div>
				{/each}
			</div>
			<div class="bars" role="list">
				{#each bars as b, i}
					{#if b.count === 0}
						<span class="bar-col empty-col" role="listitem">
							<span
								class="bar empty"
								style="--h: 0%; --delay: {i * 18}ms"
							></span>
						</span>
					{:else}
						<Tooltip label={tooltipFor(b)}>
							<span class="bar-col" role="listitem">
								<span
									class="bar"
									style="--h: {maxCount > 0 ? (b.count / maxCount) * 100 : 0}%; --delay: {i * 18}ms"
								></span>
							</span>
						</Tooltip>
					{/if}
				{/each}
			</div>
		</div>
		<div class="axis" aria-hidden="true">
			{#each bars as b, i}
				{@const lastIdx = bars.length - 1}
				{@const shown =
					step <= 1 ||
					i === 0 ||
					i === lastIdx ||
					(b.damage % step === 0 && b.damage + step / 2 < bars[lastIdx].damage)}
				<span class="axis-label" class:shown>{b.damage}</span>
			{/each}
		</div>
		<figcaption>
			<span class="caption-title">Hits {direction}</span>
			<span class="caption-sub">{total} total · biggest {bars[bars.length - 1].damage}</span>
		</figcaption>
	</figure>
{/if}

<style>
	.histogram {
		--bar-from: var(--accent-gold);
		--bar-to: color-mix(in srgb, var(--accent-gold) 30%, #000);
		--glow: color-mix(in srgb, var(--accent-gold) 60%, transparent);
		display: flex;
		flex-direction: column;
		gap: 0.4rem;
		padding: 0.9rem 0.9rem 0.6rem;
		background: var(--bg-card);
		border: 1px solid var(--border-subtle);
		border-radius: var(--radius);
		min-height: 10rem;
		/* Hard-clamp width so dense distributions can't push the page wider,
		   but allow tooltips on edge/top bars to escape the box. */
		min-width: 0;
		max-width: 100%;
		overflow: clip;
		overflow-clip-margin: 5rem;
	}

	.histogram.red {
		--bar-from: var(--accent-red);
		--bar-to: color-mix(in srgb, var(--accent-red) 30%, #000);
		--glow: color-mix(in srgb, var(--accent-red) 55%, transparent);
	}

	.plot {
		position: relative;
		flex: 1;
		min-height: 7rem;
	}

	.grid {
		position: absolute;
		inset: 0 1.5rem 0 0;
		pointer-events: none;
	}

	.grid-line {
		position: absolute;
		left: 0;
		right: 0;
		height: 1px;
		background: color-mix(in srgb, var(--border-subtle) 70%, transparent);
	}

	.tick {
		position: absolute;
		right: -1.4rem;
		top: -0.6rem;
		font-size: 0.6rem;
		font-variant-numeric: tabular-nums;
		color: var(--text-muted);
		font-weight: 500;
	}

	.bars {
		position: absolute;
		inset: 0 1.5rem 0 0;
		display: flex;
		align-items: flex-end;
		gap: 0px;
	}

	.bar-col {
		position: relative;
		flex: 1 1 0;
		min-width: 0;
		height: 100%;
		display: flex;
		align-items: flex-end;
		justify-content: stretch;
		cursor: pointer;
	}

	.bar-col.empty-col {
		cursor: default;
	}

	/* Override the inline-flex the Tooltip wrapper applies so bars stretch. */
	.bars :global(.tooltip-host) {
		flex: 1 1 0;
		min-width: 0;
		height: 100%;
		display: flex;
		align-items: flex-end;
	}

	.bar {
		width: 100%;
		height: var(--h);
		min-height: 1px;
		background: linear-gradient(to top, var(--bar-to), var(--bar-from));
		border-radius: 1px 1px 0 0;
		transform-origin: bottom center;
		animation: grow 450ms cubic-bezier(0.22, 1, 0.36, 1) both;
		animation-delay: var(--delay);
		box-shadow: 0 0 0 transparent;
		transition:
			filter 0.15s,
			box-shadow 0.15s,
			transform 0.15s;
	}

	.bar.empty {
		background: color-mix(in srgb, var(--border-subtle) 80%, transparent);
		min-height: 2px;
		border-radius: 1px;
	}

	.bar-col:hover .bar:not(.empty) {
		filter: brightness(1.2);
		box-shadow: 0 0 10px var(--glow);
	}

	.axis {
		display: flex;
		gap: 2px;
		padding-right: 1.5rem;
	}

	.axis-label {
		flex: 1 1 0;
		min-width: 0;
		text-align: center;
		font-size: 0.6rem;
		font-variant-numeric: tabular-nums;
		color: var(--text-secondary);
		/* Decimated out: keep the slot to stay aligned with the bar, hide the label. */
		visibility: hidden;
	}

	.axis-label.shown {
		visibility: visible;
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

	@keyframes grow {
		from {
			transform: scaleY(0);
			opacity: 0;
		}
		to {
			transform: scaleY(1);
			opacity: 1;
		}
	}

	@media (prefers-reduced-motion: reduce) {
		.bar {
			animation: none;
		}
	}
</style>
