<script lang="ts">
	import { page } from '$app/state';
	import { resolveIdentity } from '$lib/api/slingshot';
	import { getRun } from '$lib/api/pds';
	import type { MiniDoc, RunRecord } from '$lib/api/types';
	import RunDetail from '$lib/components/RunDetail.svelte';

	const POLL_INTERVAL_MS = 300_000;

	let loading = $state(true);
	let error = $state<string | null>(null);
	let identity = $state<MiniDoc | null>(null);
	let run = $state<RunRecord | null>(null);

	const isInProgress = $derived(run?.outcome === 'in_progress');

	$effect(() => {
		const { actor, tid } = page.params;
		load(actor, tid);
	});

	$effect(() => {
		const { tid } = page.params;
		if (!identity || !isInProgress || !tid) return;
		const { pds, did } = identity;
		const interval = setInterval(async () => {
			try {
				const result = await getRun(pds, did, tid);
				run = result.value;
			} catch {
				// Transient fetch failures: keep the last good state and try again next tick.
			}
		}, POLL_INTERVAL_MS);
		return () => clearInterval(interval);
	});

	async function load(actor: string, tid: string) {
		loading = true;
		error = null;
		try {
			identity = await resolveIdentity(actor);
			const result = await getRun(identity.pds, identity.did, tid);
			run = result.value;
		} catch (e) {
			error = e instanceof Error ? e.message : 'Unknown error';
		} finally {
			loading = false;
		}
	}
</script>

<svelte:head>
	<title>
		{identity ? `@${identity.handle}` : page.params.actor} run
	</title>
</svelte:head>

<a href={`/${page.params.actor}`} class="back">&larr; All runs</a>

{#if loading}
	<div class="status">Loading...</div>
{:else if error}
	<div class="status error">{error}</div>
{:else if run && identity}
	<RunDetail {run} did={identity.did} tid={page.params.tid} />
{/if}

<style>
	.back {
		display: inline-block;
		margin-bottom: 1.5rem;
		font-size: 0.9rem;
	}

	.status {
		color: var(--text-secondary);
		padding: 2rem 0;
		text-align: center;
	}

	.error {
		color: var(--accent-red);
	}
</style>
