<script lang="ts">
	import { charactersSheet, spriteStyle } from '$lib/utils/sprites';
	import { baseName } from '$lib/utils/assets';
	import { humanizeId } from '$lib/utils/format';

	let {
		character,
		size = '2rem'
	}: { character: string; size?: string } = $props();

	const style = $derived(spriteStyle(charactersSheet, baseName(character)));
	const label = $derived(humanizeId(character));
</script>

<span
	class="icon"
	class:ready={!!style}
	style={`--size: ${size}; ${style ?? ''}`}
	aria-label={label}
></span>

<style>
	.icon {
		display: inline-block;
		width: var(--size);
		height: var(--size);
		vertical-align: middle;
	}

	.icon.ready {
		background-image: var(--sprite);
		background-size: calc(var(--size) * var(--cols)) calc(var(--size) * var(--rows));
		background-position: calc(-1 * var(--col) * var(--size)) calc(-1 * var(--row) * var(--size));
		background-repeat: no-repeat;
	}
</style>
