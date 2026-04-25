<script lang="ts">
	import { resolvePlayer, type Player, type ResolvedPlayer } from '$lib/utils/player';

	let { player, size = '1.6rem' }: { player: Player; size?: string } = $props();

	let resolved = $state<ResolvedPlayer | null>(null);

	$effect(() => {
		let cancelled = false;
		resolvePlayer(player, undefined, { preferLocal: true }).then((r) => {
			if (!cancelled) resolved = r;
		});
		return () => {
			cancelled = true;
		};
	});
</script>

{#if resolved?.avatar}
	<img
		class="avatar"
		src={resolved.avatar}
		alt=""
		title={resolved.label}
		loading="lazy"
		style:--size={size}
	/>
{:else}
	<div
		class="avatar placeholder"
		title={resolved?.label ?? ''}
		aria-hidden="true"
		style:--size={size}
	></div>
{/if}

<style>
	.avatar {
		width: var(--size);
		height: var(--size);
		border-radius: 50%;
		object-fit: cover;
		background: var(--bg-secondary);
		border: 1px solid var(--border-card);
		flex-shrink: 0;
	}

	.placeholder {
		border: 1px dashed var(--border-subtle);
		background: var(--bg-secondary);
	}
</style>
