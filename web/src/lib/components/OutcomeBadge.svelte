<script lang="ts">
	let { outcome }: { outcome: string } = $props();

	const labels: Record<string, string> = {
		victory: 'Victory…?',
		death: 'Death',
		abandoned: 'Abandoned',
		in_progress: 'In Progress'
	};
</script>

<span class="badge {outcome}">{labels[outcome] ?? outcome}</span>

<style>
	.badge {
		display: inline-block;
		padding: 0.15rem 0.6rem;
		border-radius: var(--radius-sm);
		font-size: 0.8rem;
		font-weight: 600;
		text-transform: uppercase;
		letter-spacing: 0.04em;
	}

	.victory {
		background: color-mix(in srgb, var(--accent-green) 20%, transparent);
		color: var(--accent-green);
	}

	.death {
		background: color-mix(in srgb, var(--accent-red) 20%, transparent);
		color: var(--accent-red);
	}

	.abandoned {
		background: color-mix(in srgb, var(--accent-grey) 20%, transparent);
		color: var(--accent-grey);
	}

	.in_progress {
		background: color-mix(in srgb, var(--accent-amber) 20%, transparent);
		color: var(--accent-amber);
	}
</style>
