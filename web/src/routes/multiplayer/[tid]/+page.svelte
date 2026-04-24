<script lang="ts">
	import { page } from '$app/state';
	import { resolveIdentity } from '$lib/api/slingshot';
	import { getRun } from '$lib/api/pds';
	import type { RunRecord } from '$lib/api/types';
	import MultiplayerComparison from '$lib/components/MultiplayerComparison.svelte';

	interface LoadedPlayer {
		did: string;
		run: RunRecord;
	}

	let loading = $state(true);
	let players = $state<LoadedPlayer[]>([]);

	$effect(() => {
		const tid = page.params.tid;
		const dids = page.url.searchParams.getAll('did');
		load(tid, dids);
	});

	async function load(tid: string, dids: string[]) {
		loading = true;
		const results = await Promise.all(
			dids.map(async (did) => {
				try {
					const identity = await resolveIdentity(did);
					const record = await getRun(identity.pds, identity.did, tid);
					return { did: identity.did, run: record.value } satisfies LoadedPlayer;
				} catch {
					return null;
				}
			})
		);
		players = results.filter((p): p is LoadedPlayer => p !== null);
		loading = false;
	}
</script>

<svelte:head>
	<title>Multiplayer run comparison</title>
</svelte:head>

<h1>Multiplayer comparison</h1>

{#if loading}
	<div class="status">Loading runs...</div>
{:else if players.length < 2}
	<div class="status">
		Not enough runs to compare. Need at least two players with visible runs for this game.
	</div>
{:else}
	<MultiplayerComparison {players} tid={page.params.tid} />
{/if}

<style>
	h1 {
		font-size: 1.75rem;
		color: var(--accent-gold);
		margin-bottom: 1.25rem;
	}

	.status {
		color: var(--text-secondary);
		padding: 2rem 0;
		text-align: center;
	}
</style>
