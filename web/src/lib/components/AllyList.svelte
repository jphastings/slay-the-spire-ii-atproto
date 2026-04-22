<script lang="ts">
	import PlayerCard from './PlayerCard.svelte';
	import type { Player } from '$lib/utils/player';

	let { allies, tid }: { allies: Player[]; tid: string } = $props();
</script>

<div class="allies">
	{#each allies as ally}
		<PlayerCard player={ally} {tid} />
	{/each}
</div>

<style>
	.allies {
		display: flex;
		flex-wrap: wrap;
		gap: 0.5rem;
	}
</style>
