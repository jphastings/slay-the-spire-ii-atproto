<script lang="ts">
	let {
		currentHp,
		maxHp
	}: {
		currentHp: number;
		maxHp: number;
	} = $props();

	const pct = $derived(maxHp > 0 ? Math.max(0, Math.min(100, (currentHp / maxHp) * 100)) : 0);
</script>

<div class="hp-bar" role="progressbar" aria-valuenow={currentHp} aria-valuemin={0} aria-valuemax={maxHp}>
	<div class="track">
		<div class="fill" style:width="{pct}%"></div>
	</div>
	<span class="readout">{currentHp}/{maxHp}</span>
</div>

<style>
	.hp-bar {
		display: flex;
		align-items: center;
		gap: 0.75rem;
		width: 100%;
	}

	.track {
		flex: 1 1 auto;
		height: 1.4rem;
		background: #0a0a0a;
		border: 1px solid var(--border-card);
		border-radius: 999px;
		overflow: hidden;
		box-shadow: inset 0 1px 3px rgba(0, 0, 0, 0.6);
	}

	.fill {
		height: 100%;
		background: var(--accent-red);
		transition: width 0.3s ease;
	}

	.readout {
		font-family: var(--font-display);
		font-weight: 700;
		font-size: 1rem;
		color: var(--text-primary);
		font-variant-numeric: tabular-nums;
		min-width: 4ch;
		text-align: left;
	}
</style>
