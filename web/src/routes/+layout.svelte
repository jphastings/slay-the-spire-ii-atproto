<script>
	import '../app.css';
	import { tintFiltersSvg } from '$lib/utils/tints';

	let { children } = $props();

	// One SVG <defs> per page with every card-tint <filter> referenced
	// by Card.svelte's frame / portrait-border / banner / plaque layers.
	// Injected verbatim because Svelte sanitizes raw SVG inside <svg>
	// templates differently than HTML inline.
	const tintDefs = tintFiltersSvg();
</script>

{@html tintDefs}

<div class="shell">
	<header>
		<a href="/" class="site-name">StS2 Runs</a>
	</header>

	<main>
		{@render children()}
	</main>

	<footer>
		<p>
			Made <a href="https://www.byjp.me">byJP</a> powered by <a href="https://atproto.com" target="_blank" rel="noopener">AT Protocol</a>
		</p>
	</footer>
</div>

<style>
	.shell {
		display: flex;
		flex-direction: column;
		min-height: 100dvh;
		max-width: var(--max-width);
		margin: 0 auto;
		padding: 0 1.5rem;
	}

	header {
		padding: 1.25rem 0;
		border-bottom: 1px solid var(--border-subtle);
	}

	.site-name {
		font-family: var(--font-display);
		font-size: 1.25rem;
		font-weight: 700;
		color: var(--accent-gold);
	}

	main {
		flex: 1;
		padding: 2rem 0;
	}

	footer {
		padding: 1.5rem 0;
		border-top: 1px solid var(--border-subtle);
		text-align: center;
		color: var(--text-muted);
		font-size: 0.85rem;
	}
</style>
