<script lang="ts">
	import { page } from '$app/state';
	import { resolveIdentity, SlingshotUnavailableError } from '$lib/api/slingshot';
	import { getRun } from '$lib/api/pds';
	import type { RunRecord } from '$lib/api/types';
	import MultiplayerComparison from '$lib/components/MultiplayerComparison.svelte';
	import SlingshotDown from '$lib/components/SlingshotDown.svelte';

	interface LoadedPlayer {
		did: string;
		run: RunRecord;
	}

	let loading = $state(true);
	let players = $state<LoadedPlayer[]>([]);
	let slingshotDown = $state(false);
	// Tracks hash changes so edits like `#did=…` after load re-fire the effect.
	let hash = $state('');

	$effect(() => {
		hash = window.location.hash;
		const onHashChange = () => (hash = window.location.hash);
		window.addEventListener('hashchange', onHashChange);
		return () => window.removeEventListener('hashchange', onHashChange);
	});

	$effect(() => {
		const tid = page.params.tid;
		const raw = hash.startsWith('#') ? hash.slice(1) : hash;
		const dids = raw ? new URLSearchParams(raw).getAll('did') : [];
		load(tid, dids);
	});

	async function load(tid: string, dids: string[]) {
		loading = true;
		slingshotDown = false;
		// Tracked across the parallel resolutions so we can distinguish a real
		// outage (apology UI) from per-DID misses (silently dropped, as before).
		let sawSlingshotDown = false;
		const results = await Promise.all(
			dids.map(async (did) => {
				try {
					const identity = await resolveIdentity(did);
					const record = await getRun(identity.pds, identity.did, tid);
					return { did: identity.did, run: record.value } satisfies LoadedPlayer;
				} catch (e) {
					if (e instanceof SlingshotUnavailableError) sawSlingshotDown = true;
					return null;
				}
			})
		);
		players = results.filter((p): p is LoadedPlayer => p !== null);
		// Only surface the apology when nothing loaded — otherwise we showed
		// the players we got and the partial outage isn't worth interrupting.
		slingshotDown = sawSlingshotDown && players.length === 0;
		loading = false;
	}
</script>

<svelte:head>
	<title>Multiplayer run comparison</title>
</svelte:head>

<h1>Multiplayer comparison</h1>

{#if loading}
	<div class="status">Loading runs...</div>
{:else if slingshotDown}
	<SlingshotDown />
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
