<script lang="ts">
	import { untrack } from "svelte";
	import { tweened } from "svelte/motion";
	import { cubicOut } from "svelte/easing";
	import { trendAngle, CENTER, RING_RADIUS } from "../geometry";

	let {
		delta,
		color,
		stale,
	}: { delta: number; color: string; stale: boolean } = $props();

	const reducedMotion =
		typeof window !== "undefined" &&
		window.matchMedia?.("(prefers-reduced-motion: reduce)").matches;

	const angle = tweened(untrack(() => trendAngle(delta)), {
		duration: reducedMotion ? 0 : 600,
		easing: cubicOut,
	});

	$effect(() => {
		angle.set(trendAngle(delta));
	});
</script>

{#if !stale}
	<g
		transform="rotate({$angle} {CENTER} {CENTER}) translate({CENTER + RING_RADIUS + 5} {CENTER})"
	>
		<path d="M 0 -6.5 Q 5 -3.2 10 0 Q 5 3.2 0 6.5 Z" fill={color} />
	</g>
{/if}
