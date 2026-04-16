<script lang="ts">
	import { goto } from '$app/navigation';

	let query = $state('');

	function lookup(e: SubmitEvent) {
		e.preventDefault();
		const trimmed = query.trim();
		if (trimmed) goto(`/${trimmed}`);
	}
</script>

<svelte:head>
	<title>Slay the Spire 2 Run Tracker</title>
</svelte:head>

<div class="hero">
	<h1>StS2 Runs</h1>
	<p class="tagline">A <em>Slay the Spire 2</em> Run Tracker</p>
	<p class="description">
		An experimental site for browsing info from Slay the Spire 2 players who publish their runs to the
		<a href="https://atproto.com" target="_blank" rel="noopener">Atmosphere</a>
		using <a href="https://github.com/jphastings/slay-the-spire-ii-atproto">this special game mod</a>.
	</p>

	<form class="search" onsubmit={lookup}>
		<input
			name="handle"
			type="text"
			bind:value={query}
			placeholder="eg. byjp.me or you.bsky.social"
			autocapitalize="none"
			autocorrect="off"
			spellcheck="false"
		/>
		<button type="submit">View Runs</button>
	</form>

	<div class="links">
		<a
			href="https://github.com/jphastings/slay-the-spire-ii-atproto"
			target="_blank"
			rel="noopener"
			class="btn btn-secondary"
		>
			Get the Mod
		</a>
		<a
			href="https://store.steampowered.com/app/2868840/Slay_the_Spire_2/"
			target="_blank"
			rel="noopener"
			class="btn btn-secondary"
		>
			Slay the Spire 2
		</a>
	</div>
</div>

<style>
	.hero {
		display: flex;
		flex-direction: column;
		align-items: center;
		text-align: center;
		gap: 1rem;
		padding: 4rem 0 2rem;
	}

	h1 {
		font-size: 3.5rem;
		color: var(--accent-gold);
		letter-spacing: 0.04em;
	}

	.tagline {
		font-family: var(--font-display);
		font-size: 1.25rem;
		color: var(--text-secondary);
	}

	.description {
		max-width: 36rem;
		color: var(--text-secondary);
		line-height: 1.7;
	}

	.search {
		display: flex;
		gap: 0.5rem;
		width: 100%;
		max-width: 28rem;
		margin-top: 1rem;
	}

	input {
		flex: 1;
		padding: 0.65rem 1rem;
		background: var(--bg-card);
		border: 1px solid var(--border-card);
		border-radius: var(--radius);
		color: var(--text-primary);
		font-family: var(--font-body);
		font-size: 0.95rem;
		outline: none;
		transition: border-color 0.15s;
	}

	input:focus {
		border-color: var(--accent-gold);
	}

	input::placeholder {
		color: var(--text-muted);
	}

	button {
		padding: 0.65rem 1.25rem;
		background: var(--accent-red);
		color: var(--text-primary);
		border: none;
		border-radius: var(--radius);
		font-family: var(--font-body);
		font-size: 0.95rem;
		font-weight: 500;
		cursor: pointer;
		transition: background 0.15s;
	}

	button:hover {
		background: var(--accent-red-dim);
	}

	.links {
		display: flex;
		gap: 1rem;
		margin-top: 1.5rem;
		flex-wrap: wrap;
		justify-content: center;
	}

	.btn {
		display: inline-block;
		padding: 0.6rem 1.5rem;
		border-radius: var(--radius);
		font-weight: 500;
		font-size: 0.95rem;
		background: var(--accent-red);
		color: var(--text-primary);
		transition:
			background 0.15s,
			color 0.15s;
	}

	.btn:hover {
		background: var(--accent-red-dim);
		color: var(--text-primary);
	}

	.btn-secondary {
		background: var(--bg-card);
		border: 1px solid var(--border-card);
	}

	.btn-secondary:hover {
		background: var(--bg-card-hover);
	}
</style>
