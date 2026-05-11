<script lang="ts">
	import { page } from '$app/state';
	import { resolveIdentity, SlingshotUnavailableError } from '$lib/api/slingshot';
	import { listRuns } from '$lib/api/pds';
	import type { MiniDoc, RecordEntry } from '$lib/api/types';
	import RunCard from '$lib/components/RunCard.svelte';
	import PlayerCard from '$lib/components/PlayerCard.svelte';
	import ClaimPromptCard from '$lib/components/ClaimPromptCard.svelte';
	import ProfileStats from '$lib/components/ProfileStats.svelte';
	import SlingshotDown from '$lib/components/SlingshotDown.svelte';
	import { computeProfileStats } from '$lib/utils/profile-stats';
	import { fetchKeytraceClaim, type KeytraceClaim } from '$lib/utils/player';

	let loading = $state(true);
	let error = $state<string | null>(null);
	let slingshotDown = $state(false);
	let identity = $state<MiniDoc | null>(null);
	// undefined = still checking, null = no claim, object = claim present.
	let claim = $state<KeytraceClaim | null | undefined>(undefined);
	let runs = $state<RecordEntry[]>([]);

	const stats = $derived(computeProfileStats(runs.map((r) => r.value)));

	$effect(() => {
		const actor = page.params.actor;
		if (actor) load(actor);
	});

	async function load(actor: string) {
		loading = true;
		error = null;
		slingshotDown = false;
		runs = [];
		claim = undefined;
		try {
			identity = await resolveIdentity(actor);
			// Claim fetch runs in parallel with the paginated run pull.
			const claimPromise = fetchKeytraceClaim(identity.pds, identity.did);
			const collected: RecordEntry[] = [];
			let cursor: string | undefined;
			do {
				const batch = await listRuns(identity.pds, identity.did, cursor);
				collected.push(...batch.records);
				cursor = batch.cursor;
			} while (cursor);
			claim = await claimPromise;
			runs = collected.sort(
				(a, b) => new Date(b.value.updatedAt).getTime() - new Date(a.value.updatedAt).getTime()
			);
		} catch (e) {
			if (e instanceof SlingshotUnavailableError) {
				slingshotDown = true;
			} else {
				error = e instanceof Error ? e.message : 'Unknown error';
			}
		} finally {
			loading = false;
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
{:else if slingshotDown}
	<SlingshotDown />
{:else if error}
	<div class="status error">{error}</div>
{:else if identity}
	<div class="header">
		<div class="title">
			<h1 typeof="schema:Person" resource={identity.did}>@{identity.handle}</h1>
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
		<div class="layout">
			<aside class="stats-col">
				<ProfileStats {stats} />
			</aside>
			<div class="runs">
				{#each runs as entry}
					<RunCard
						run={entry.value}
						href={`/${page.params.actor}/${rkeyFromUri(entry.uri)}`}
						resource={entry.uri}
					/>
				{/each}
			</div>
		</div>
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

	.layout {
		display: flex;
		flex-direction: column;
		gap: 1.5rem;
	}

	.stats-col {
		min-width: 0;
	}

	.runs {
		display: flex;
		flex-direction: column;
		gap: 0.75rem;
		min-width: 0;
	}

	@media (min-width: 60rem) {
		.layout {
			display: grid;
			grid-template-columns: minmax(0, 1fr) minmax(16rem, 22rem);
			gap: 2rem;
			align-items: start;
		}

		.runs {
			grid-column: 1;
			grid-row: 1;
		}

		.stats-col {
			grid-column: 2;
			grid-row: 1;
			position: sticky;
			top: 1rem;
		}
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
