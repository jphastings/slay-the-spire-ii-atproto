<script lang="ts">
	let {
		currentHp,
		maxHp
	}: {
		currentHp: number;
		maxHp: number;
	} = $props();

	const pct = $derived(maxHp > 0 ? Math.max(0, Math.min(100, (currentHp / maxHp) * 100)) : 0);

	// Width-of-container as a function of maxHp:
	//   70 → 50%, 100+ → 100%, linear in between, floored at 20% so the bar
	//   stays visible for low-HP characters (none ship that low today).
	const widthPct = $derived(
		maxHp >= 100 ? 100 : Math.max(20, (maxHp * 5 - 200) / 3)
	);
</script>

<div
	class="hp-bar"
	style:width="{widthPct}%"
	role="progressbar"
	aria-valuenow={currentHp}
	aria-valuemin={0}
	aria-valuemax={maxHp}
>
	<div class="track">
		<div class="fill" style:width="{pct}%"></div>
	</div>
	<span class="readout">{currentHp}/{maxHp}</span>
</div>

<style>
	.hp-bar {
		position: relative;
		/* Width is set inline as a function of maxHp; left-align so smaller
		   bars sit at the start of the section. */
		margin-inline: 0 auto;
		display: flex;
		align-items: center;
		/* Reserve room for the readout's vertical overhang so it doesn't crowd siblings. */
		padding: 0.4rem 0;
	}

	.track {
		flex: 1 1 auto;
		height: 0.65rem;
		background: #0a0a0a;
		border: 1px solid var(--border-card);
		border-radius: 999px;
		overflow: hidden;
		box-shadow: inset 0 1px 2px rgba(0, 0, 0, 0.6);
	}

	.fill {
		height: 100%;
		background: var(--accent-red);
		transition: width 0.3s ease;
	}

	.readout {
		position: absolute;
		left: 50%;
		top: 50%;
		transform: translateX(-50%);
		/* `cap` is font-aware: half the cap-height above 50% places the
		   cap-height center on the bar's geometric center. Plain
		   translateY(-50%) centers the line-box, which leaves digits
		   visually low because ascent > descent in Cinzel. */
		margin-top: -0.65cap;
		font-family: var(--font-display);
		font-weight: 700;
		font-size: 1.05rem;
		line-height: 1;
		color: #fff;
		font-variant-numeric: tabular-nums;
		-webkit-text-stroke: 3px var(--accent-red-dim);
		paint-order: stroke fill;
		pointer-events: none;
		white-space: nowrap;
	}
</style>
