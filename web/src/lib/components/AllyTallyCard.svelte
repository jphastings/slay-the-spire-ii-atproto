<script lang="ts">
	import { resolvePlayer, type ResolvedPlayer } from '$lib/utils/player';
	import type { AllyTally } from '$lib/utils/profile-stats';

	let { tally }: { tally: AllyTally } = $props();

	let resolved = $state<ResolvedPlayer | null>(null);

	$effect(() => {
		let cancelled = false;
		resolvePlayer({ steam: tally.steam, atproto: tally.atproto }, undefined, {
			preferLocal: true
		}).then((r) => {
			if (!cancelled) resolved = r;
		});
		return () => {
			cancelled = true;
		};
	});

	const fallbackLabel = $derived(tally.atproto ? 'Loading…' : 'Steam player');
</script>

{#if resolved}
	<a
		class="ally"
		href={resolved.href}
		target={resolved.external ? '_blank' : undefined}
		rel={resolved.external ? 'noopener noreferrer' : undefined}
	>
		{#if resolved.avatar}
			<img class="avatar" src={resolved.avatar} alt="" loading="lazy" />
		{:else}
			<div class="avatar placeholder" aria-hidden="true"></div>
		{/if}
		<div class="text">
			<span class="games">
				{tally.games} {tally.games === 1 ? 'game' : 'games'}
				<span class="with"> with {resolved.label}</span>
			</span>
			<span class="asc">Max ascension {tally.highestAscension}</span>
		</div>
	</a>
{:else}
	<span class="ally pending" aria-busy="true">
		<div class="avatar placeholder" aria-hidden="true"></div>
		<div class="text">
			<span class="games">
				{tally.games} {tally.games === 1 ? 'game' : 'games'}
				<span class="with"> with {fallbackLabel}</span>
			</span>
			<span class="asc">Max ascension {tally.highestAscension}</span>
		</div>
	</span>
{/if}

<style>
	.ally {
		display: flex;
		align-items: center;
		gap: 0.6rem;
		padding: 0.4rem 0.75rem 0.4rem 0.5rem;
		background: var(--bg-card);
		border: 1px solid var(--border-subtle);
		border-radius: var(--radius);
		text-decoration: none;
		color: inherit;
		transition:
			background 0.15s,
			border-color 0.15s;
	}

	a.ally:hover {
		background: var(--bg-card-hover);
		border-color: var(--accent-gold);
	}

	.pending {
		opacity: 0.65;
	}

	.avatar {
		width: 2.75rem;
		height: 2.75rem;
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
		line-height: 1.15;
	}

	.games {
		font-family: var(--font-display);
		font-weight: 700;
		font-size: 1.05rem;
		color: var(--accent-gold);
		line-height: 1.1;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}

	.with {
		color: var(--text-muted);
		font-weight: 400;
		font-size: 0.85rem;
	}

	.asc {
		color: var(--text-secondary);
		font-size: 0.85rem;
	}
</style>
