<script lang="ts">
	import type { Snippet } from 'svelte';
	let { label, children }: { label: string; children: Snippet } = $props();
</script>

<!--
	Wraps arbitrary content with a CSS tooltip. `tabindex="0"` makes the host
	focusable so mobile taps also fire `:focus` and show the label.
-->
<span class="tooltip-host" data-tooltip={label} aria-label={label} tabindex="0">
	{@render children()}
</span>

<style>
	.tooltip-host {
		position: relative;
		display: inline-flex;
	}

	.tooltip-host:focus {
		outline: none;
	}

	.tooltip-host:focus-visible {
		outline: 2px solid var(--accent-gold);
		outline-offset: 2px;
		border-radius: var(--radius-sm);
	}

	.tooltip-host::after {
		content: attr(data-tooltip);
		position: absolute;
		bottom: calc(100% + 0.4rem);
		left: 50%;
		transform: translateX(-50%);
		padding: 0.3rem 0.55rem;
		background: var(--bg-card);
		color: var(--text-primary);
		font-size: 0.75rem;
		line-height: 1.2;
		white-space: nowrap;
		border: 1px solid var(--border-card);
		border-radius: var(--radius-sm);
		opacity: 0;
		pointer-events: none;
		transition: opacity 80ms;
		z-index: 10;
	}

	.tooltip-host:hover::after,
	.tooltip-host:focus::after {
		opacity: 1;
	}
</style>
