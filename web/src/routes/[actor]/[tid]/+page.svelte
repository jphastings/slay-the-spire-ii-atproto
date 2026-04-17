<script lang="ts">
	import { page } from '$app/state';
	import { resolveIdentity } from '$lib/api/slingshot';
	import { getRun } from '$lib/api/pds';
	import type { MiniDoc, RunRecord } from '$lib/api/types';
	import RunDetail from '$lib/components/RunDetail.svelte';

	let loading = $state(true);
	let error = $state<string | null>(null);
	let identity = $state<MiniDoc | null>(null);
	let run = $state<RunRecord | null>(null);

	$effect(() => {
		const { actor, tid } = page.params;
		load(actor, tid);
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
