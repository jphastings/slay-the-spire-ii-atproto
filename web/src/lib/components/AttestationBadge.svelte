<script lang="ts">
	import type { VerifyResult } from '$lib/attestation/verify';

	let { result }: { result: VerifyResult | 'loading' } = $props();

	const tooltips = {
		loading: 'Checking signature…',
		verified: 'Signature verified. This page (almost certainly) shows an authentic Slay the Spire 2 run.',
		unsigned:
			"No signature on this record. We don't know if this run is authentic.",
		invalid: "Signature invalid. The details of this Slay the Spire 2 run appear to have been tampered with."
	};

	const status = $derived(result === 'loading' ? 'loading' : result.status);
</script>

<span class="badge {status}" role="img" aria-label={tooltips[status]}>
	{#if status === 'loading'}
		<!-- lucide: loader-2 -->
		<svg
			class="icon spin"
			viewBox="0 0 24 24"
			width="18"
			height="18"
			fill="none"
			stroke="currentColor"
			stroke-width="2"
			stroke-linecap="round"
			stroke-linejoin="round"
			aria-hidden="true"
		>
			<path d="M21 12a9 9 0 1 1-6.219-8.56" />
		</svg>
	{:else if status === 'verified'}
		<!-- lucide: badge-check -->
		<svg
			class="icon"
			viewBox="0 0 24 24"
			width="18"
			height="18"
			fill="none"
			stroke="currentColor"
			stroke-width="2"
			stroke-linecap="round"
			stroke-linejoin="round"
			aria-hidden="true"
		>
			<path
				d="M3.85 8.62a4 4 0 0 1 4.78-4.77 4 4 0 0 1 6.74 0 4 4 0 0 1 4.78 4.78 4 4 0 0 1 0 6.74 4 4 0 0 1-4.77 4.78 4 4 0 0 1-6.75 0 4 4 0 0 1-4.78-4.77 4 4 0 0 1 0-6.76Z"
			/>
			<path d="m9 12 2 2 4-4" />
		</svg>
	{:else if status === 'unsigned'}
		<!-- lucide: badge-minus -->
		<svg
			class="icon"
			viewBox="0 0 24 24"
			width="18"
			height="18"
			fill="none"
			stroke="currentColor"
			stroke-width="2"
			stroke-linecap="round"
			stroke-linejoin="round"
			aria-hidden="true"
		>
			<path
				d="M3.85 8.62a4 4 0 0 1 4.78-4.77 4 4 0 0 1 6.74 0 4 4 0 0 1 4.78 4.78 4 4 0 0 1 0 6.74 4 4 0 0 1-4.77 4.78 4 4 0 0 1-6.75 0 4 4 0 0 1-4.78-4.77 4 4 0 0 1 0-6.76Z"
			/>
			<line x1="8" x2="16" y1="12" y2="12" />
		</svg>
	{:else if status === 'invalid'}
		<!-- lucide: badge-x -->
		<svg
			class="icon"
			viewBox="0 0 24 24"
			width="18"
			height="18"
			fill="none"
			stroke="currentColor"
			stroke-width="2"
			stroke-linecap="round"
			stroke-linejoin="round"
			aria-hidden="true"
		>
			<path
				d="M3.85 8.62a4 4 0 0 1 4.78-4.77 4 4 0 0 1 6.74 0 4 4 0 0 1 4.78 4.78 4 4 0 0 1 0 6.74 4 4 0 0 1-4.77 4.78 4 4 0 0 1-6.75 0 4 4 0 0 1-4.78-4.77 4 4 0 0 1 0-6.76Z"
			/>
			<line x1="15" x2="9" y1="9" y2="15" />
			<line x1="9" x2="15" y1="9" y2="15" />
		</svg>
	{/if}
	<span class="tooltip" role="tooltip">{tooltips[status]}</span>
</span>

<style>
	.badge {
		position: relative;
		display: inline-flex;
		align-items: center;
		justify-content: center;
		vertical-align: middle;
	}

	.verified {
		color: var(--accent-blue);
	}

	.unsigned {
		color: var(--accent-grey);
	}

	.invalid {
		color: var(--accent-red);
	}

	.loading {
		color: var(--text-muted);
	}

	.icon {
		display: block;
	}

	.spin {
		animation: spin 0.9s linear infinite;
	}

	@keyframes spin {
		from {
			transform: rotate(0deg);
		}
		to {
			transform: rotate(360deg);
		}
	}

	/* Tooltip: show instantly on hover/focus (no browser-native 1s delay). */
	.tooltip {
		position: absolute;
		top: calc(100% + 6px);
		right: 0;
		z-index: 10;
		width: max-content;
		max-width: min(16rem, calc(100vw - 2rem));
		padding: 0.4rem 0.6rem;
		background: var(--bg-card);
		color: var(--text-primary);
		border: 1px solid var(--border-card);
		border-radius: var(--radius-sm);
		box-shadow: var(--shadow-card);
		font-size: 0.75rem;
		line-height: 1.35;
		font-weight: 400;
		letter-spacing: normal;
		text-transform: none;
		white-space: normal;
		opacity: 0;
		visibility: hidden;
		pointer-events: none;
		transition: opacity 0.08s ease-out;
	}

	/* Small caret pointing up toward the badge. */
	.tooltip::before {
		content: '';
		position: absolute;
		bottom: 100%;
		right: 4px;
		border: 5px solid transparent;
		border-bottom-color: var(--border-card);
	}

	.badge:hover .tooltip,
	.badge:focus-visible .tooltip,
	.badge:focus-within .tooltip {
		opacity: 1;
		visibility: visible;
	}
</style>
