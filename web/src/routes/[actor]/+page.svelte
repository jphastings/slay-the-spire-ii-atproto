<script lang="ts">
	import { page } from '$app/state';
	import { resolveIdentity } from '$lib/api/slingshot';
	import { listRuns } from '$lib/api/pds';
	import type { MiniDoc, RecordEntry } from '$lib/api/types';
	import RunCard from '$lib/components/RunCard.svelte';
	import PlayerCard from '$lib/components/PlayerCard.svelte';
	import ClaimPromptCard from '$lib/components/ClaimPromptCard.svelte';
	import { fetchKeytraceClaim, type KeytraceClaim } from '$lib/utils/player';

	let loading = $state(true);
	let error = $state<string | null>(null);
	let identity = $state<MiniDoc | null>(null);
	// undefined = still checking, null = no claim, object = claim present.
	let claim = $state<KeytraceClaim | null | undefined>(undefined);
	let runs = $state<RecordEntry[]>([]);
	let cursor = $state<string | undefined>(undefined);
	let loadingMore = $state(false);

	$effect(() => {
		const actor = page.params.actor;
		load(actor);
	});

	async function load(actor: string) {
		loading = true;
		error = null;
		runs = [];
		cursor = undefined;
		claim = undefined;
		try {
			identity = await resolveIdentity(actor);
			// Runs + claim in parallel — the claim gates (and feeds) the right-hand card.
			const [runsResult, claimResult] = await Promise.all([
				listRuns(identity.pds, identity.did),
				fetchKeytraceClaim(identity.pds, identity.did)
			]);
			claim = claimResult;
			runs = runsResult.records.sort(
				(a, b) => new Date(b.value.updatedAt).getTime() - new Date(a.value.updatedAt).getTime()
			);
			cursor = runsResult.cursor;
		} catch (e) {
			error = e instanceof Error ? e.message : 'Unknown error';
		} finally {
			loading = false;
		}
	}

	async function loadMore() {
		if (!identity || !cursor || loadingMore) return;
		loadingMore = true;
		try {
			const result = await listRuns(identity.pds, identity.did, cursor);
			const all = [...runs, ...result.records];
			runs = all.sort(
				(a, b) => new Date(b.value.updatedAt).getTime() - new Date(a.value.updatedAt).getTime()
			);
			cursor = result.cursor;
		} finally {
			loadingMore = false;
		}
	}

	function rkeyFromUri(uri: string): string {
		return uri.split('/').pop() ?? '';
	}
</script>

<svelte:head>
	<title>{identity ? `@${identity.handle}` : page.params.actor}</title>
</svelte:head>

{#if loading}
	<div class="status">Loading...</div>
{:else if error}
	<div class="status error">{error}</div>
{:else if identity}
	<div class="header">
		<div class="title">
			<h1>@{identity.handle}</h1>
			<p class="tagline">Slay the Spire 2 Runs</p>
		</div>
		<div class="actor">
			{#if claim}
				<PlayerCard
					player={{ atproto: identity.did, steam: claim.steamId64 }}
					preferSteam
				/>
			{:else if claim === null}
				<ClaimPromptCard />
			{/if}
		</div>
	</div>

	{#if runs.length === 0}
		<p class="status">No runs found.</p>
	{:else}
		<div class="runs">
			{#each runs as entry}
				<RunCard run={entry.value} href={`/${page.params.actor}/${rkeyFromUri(entry.uri)}`} />
			{/each}
		</div>
		{#if cursor}
			<button class="load-more" onclick={loadMore} disabled={loadingMore}>
				{loadingMore ? 'Loading...' : 'Load more'}
			</button>
		{/if}
	{/if}
{/if}

<style>
	h1 {
		font-size: 2rem;
		color: var(--accent-gold);
	}

	.header {
		display: flex;
		align-items: center;
		gap: 1rem;
		flex-wrap: wrap;
		margin-bottom: 1.5rem;
	}

	.title {
		flex: 1 1 auto;
	}

	.actor {
		margin-left: auto;
	}

	.tagline {
		color: var(--text-secondary);
		margin-top: 0.15rem;
	}

	.runs {
		display: flex;
		flex-direction: column;
		gap: 0.75rem;
	}

	.status {
		color: var(--text-secondary);
		padding: 2rem 0;
		text-align: center;
	}

	.error {
		color: var(--accent-red);
	}

	.load-more {
		display: block;
		margin: 1.5rem auto 0;
		padding: 0.5rem 1.5rem;
		background: var(--bg-card);
		border: 1px solid var(--border-card);
		border-radius: var(--radius);
		color: var(--text-primary);
		font-family: var(--font-body);
		cursor: pointer;
		transition: background 0.15s;
	}

	.load-more:hover:not(:disabled) {
		background: var(--bg-card-hover);
	}

	.load-more:disabled {
		opacity: 0.5;
		cursor: not-allowed;
	}
</style>
