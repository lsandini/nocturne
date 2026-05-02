<!-- Fallback to <path> due to no polar Chart context yet — revisit when HaloDial is wired in Task 4.8. -->
<script lang="ts">
	import { RING_RADIUS, polar, historyVertices } from "../geometry";
	import { bgColor } from "../colors";
	import type { HaloDialColorMode } from "../config";

	interface Props {
		historyValues: number[];
		historyMinutes: number;
		predictionMinutes: number;
		colorMode: HaloDialColorMode;
		gradientId?: string;
	}

	const {
		historyValues,
		historyMinutes,
		predictionMinutes,
		colorMode,
		gradientId = "history-gradient",
	}: Props = $props();

	const STROKE_WIDTH = 5;
	const OPACITY_OLDEST = 0.45;
	const OPACITY_NEWEST = 0.85;

	const vertices = $derived(
		historyVertices({
			values: historyValues,
			historyMinutes,
			predictionMinutes,
		}),
	);

	const points = $derived(
		vertices.map((v) => {
			const { x, y } = polar(v.angleDeg, v.radius);
			return { x, y, value: v.value, radius: v.radius };
		}),
	);

	const spiralActive = $derived(points.some((p) => p.radius > RING_RADIUS));

	const pathD = $derived.by(() => {
		if (points.length < 2) return "";
		// points[0] is oldest (most CCW); render in order from oldest → newest.
		const head = points[0];
		let d = `M ${head.x.toFixed(3)} ${head.y.toFixed(3)}`;
		for (let i = 1; i < points.length; i++) {
			const p = points[i];
			if (spiralActive) {
				// Radius changes between vertices — straight segments approximate
				// the Archimedean spiral well enough at typical resolutions.
				d += ` L ${p.x.toFixed(3)} ${p.y.toFixed(3)}`;
			} else {
				// Pure circular arc on the ring; sweep CCW (sweep-flag = 0).
				d += ` A ${RING_RADIUS} ${RING_RADIUS} 0 0 0 ${p.x.toFixed(3)} ${p.y.toFixed(3)}`;
			}
		}
		return d;
	});

	const gradientStops = $derived(
		points.map((p, i) => {
			const t = points.length <= 1 ? 0 : i / (points.length - 1);
			// i=0 is oldest (lowest opacity), last index is newest.
			const opacity =
				OPACITY_OLDEST + (OPACITY_NEWEST - OPACITY_OLDEST) * t;
			return {
				offset: t,
				color: bgColor(p.value, colorMode),
				opacity,
			};
		}),
	);

	const gradientStart = $derived(points[0] ?? { x: 0, y: 0 });
	const gradientEnd = $derived(
		points[points.length - 1] ?? { x: 0, y: 0 },
	);
</script>

{#if points.length >= 2}
	<defs>
		<linearGradient
			id={gradientId}
			gradientUnits="userSpaceOnUse"
			x1={gradientStart.x}
			y1={gradientStart.y}
			x2={gradientEnd.x}
			y2={gradientEnd.y}
		>
			{#each gradientStops as stop (stop.offset)}
				<stop
					offset={stop.offset}
					stop-color={stop.color}
					stop-opacity={stop.opacity}
				/>
			{/each}
		</linearGradient>
	</defs>
	<path
		d={pathD}
		fill="none"
		stroke={`url(#${gradientId})`}
		stroke-width={STROKE_WIDTH}
		stroke-linecap="round"
		data-testid="history-ring"
		data-spiral-active={spiralActive ? "true" : "false"}
	/>
{/if}
