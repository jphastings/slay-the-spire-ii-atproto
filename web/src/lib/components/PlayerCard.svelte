<script lang="ts">
	import { resolvePlayer, type Player, type ResolvedPlayer } from '$lib/utils/player';

	let {
		player,
		tid,
		preferLocal = false,
		preferSteam = false,
		compact = false
	}: {
		player: Player;
		tid?: string;
		preferLocal?: boolean;
		/** Force the external Steam profile link even when a local/companion link is available. */
		preferSteam?: boolean;
		compact?: boolean;
	} = $props();

	let resolved = $state<ResolvedPlayer | null>(null);

	$effect(() => {
		let cancelled = false;
		resolvePlayer(player, tid, { preferLocal, preferSteam }).then((r) => {
			if (!cancelled) resolved = r;
		});
		return () => {
			cancelled = true;
		};
	});

	// Pre-resolution placeholder, so the card occupies space immediately.
	const fallbackLabel = $derived(
		player.atproto ? 'Loading…' : player.steam ? 'Steam player' : 'Unknown'
	);
	const fallbackSubtitle = $derived(!player.atproto && player.steam ? player.steam : undefined);
</script>

{#if resolved}
	<a
		class="card"
		class:compact
		href={resolved.href}
		target={resolved.external ? '_blank' : undefined}
		rel={resolved.external ? 'noopener noreferrer' : undefined}
		typeof={player.atproto ? 'schema:Person' : undefined}
		resource={player.atproto || undefined}
	>
		{#if resolved.avatar}
			<img class="avatar" src={resolved.avatar} alt="" loading="lazy" />
		{:else}
			<div class="avatar placeholder" aria-hidden="true"></div>
		{/if}
		<div class="text">
			<div class="label">{resolved.label}</div>
			{#if resolved.subtitle}
				<div class="subtitle">{resolved.subtitle}</div>
			{/if}
		</div>
	</a>
{:else}
	<span
		class="card pending"
		class:compact
		aria-busy="true"
		typeof={player.atproto ? 'schema:Person' : undefined}
		resource={player.atproto || undefined}
	>
		<div class="avatar placeholder" aria-hidden="true"></div>
		<div class="text">
			<div class="label">{fallbackLabel}</div>
			{#if fallbackSubtitle}
				<div class="subtitle">{fallbackSubtitle}</div>
			{/if}
		</div>
	</span>
{/if}

<style>
	.card {
		display: inline-flex;
		align-items: center;
		gap: 0.55rem;
		padding: 0.35rem 0.75rem 0.35rem 0.4rem;
		background: var(--bg-card);
		border: 1px solid var(--border-subtle);
		border-radius: 999px;
		color: var(--text-primary);
		text-decoration: none;
		transition:
			background 0.15s,
			border-color 0.15s;
		max-width: 100%;
	}

	a.card:hover {
		background: var(--bg-card-hover);
		border-color: var(--accent-gold);
	}

	.pending {
		opacity: 0.65;
	}

	.avatar {
		width: 2rem;
		height: 2rem;
		border-radius: 50%;
		object-fit: cover;
		background: var(--bg-secondary);
		flex-shrink: 0;
	}

	.placeholder {
		border: 1px dashed var(--border-subtle);
		background: var(--bg-secondary);
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
	}

	.compact {
		padding: 0.25rem 0.6rem 0.25rem 0.3rem;
	}

	.compact .avatar {
		width: 1.6rem;
		height: 1.6rem;
	}

	.compact .label {
		font-size: 0.85rem;
	}
</style>
