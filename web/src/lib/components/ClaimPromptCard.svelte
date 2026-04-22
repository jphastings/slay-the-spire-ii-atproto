<script lang="ts">
	let {
		href = 'https://keytrace.dev/add/steam',
		label = 'Is this you?',
		subtitle = 'Claim your Steam account',
		compact = false
	}: {
		href?: string;
		label?: string;
		subtitle?: string;
		compact?: boolean;
	} = $props();
</script>

<a class="card" class:compact {href} target="_blank" rel="noopener noreferrer">
	<div class="avatar placeholder" aria-hidden="true">?</div>
	<div class="text">
		<div class="label">{label}</div>
		<div class="subtitle">{subtitle}</div>
	</div>
</a>

<style>
	/* Mirrors PlayerCard visuals so the two read as the same component. */
	.card {
		display: inline-flex;
		align-items: center;
		gap: 0.55rem;
		padding: 0.35rem 0.75rem 0.35rem 0.4rem;
		background: var(--bg-card);
		border: 1px dashed var(--border-subtle);
		border-radius: 999px;
		color: var(--text-primary);
		text-decoration: none;
		transition:
			background 0.15s,
			border-color 0.15s;
		max-width: 100%;
	}

	.card:hover {
		background: var(--bg-card-hover);
		border-color: var(--accent-gold);

		.subtitle {
			color: var(--accent-gold);
		}
	}

	.avatar {
		width: 2rem;
		height: 2rem;
		border-radius: 50%;
		background: var(--bg-secondary);
		flex-shrink: 0;
		display: flex;
		align-items: center;
		justify-content: center;
		line-height: 1;
		font-size: 1rem;
		font-weight: 500;
		color: var(--text-muted);
	}

	.placeholder {
		border: 1px dashed var(--border-subtle);
	}

	.text {
		display: flex;
		flex-direction: column;
		min-width: 0;
		line-height: 1.1;
	}

	.label {
		font-size: 0.95rem;
		font-weight: 500;
		color: var(--accent-gold);
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
		max-width: 14rem;
	}

	.subtitle {
		font-size: 0.72rem;
		color: var(--text-muted);
		margin-top: 0.1rem;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
		max-width: 14rem;
		transition:
			color 0.15s;
	}

	.compact {
		padding: 0.25rem 0.6rem 0.25rem 0.3rem;
	}

	.compact .avatar {
		width: 1.6rem;
		height: 1.6rem;
		font-size: 0.85rem;
	}

	.compact .label {
		font-size: 0.85rem;
	}
</style>
