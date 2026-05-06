<script lang="ts">
  import { Spline, Points, Axis, ChartClipPath, getChartContext } from "layerchart";
  import { curveMonotoneX } from "d3";
  import { getGlucoseChartContext } from "../chart-context.svelte";
  import { bg } from "$lib/utils/formatting";

  interface Props {
    showAxis?: boolean;
    showPoints?: boolean;
  }

  let { showAxis = true, showPoints }: Props = $props();

  const ctx = getGlucoseChartContext();
  const chartCtx = getChartContext();

  const glucoseData = $derived(ctx.engine.glucoseData);
  const glucoseScale = $derived(ctx.layout.glucose.scale);
  const glucoseAxisScale = $derived(ctx.layout.glucose.axisScale);

  // Only show points when density is reasonable (less than 0.5 points per pixel)
  const pointDensity = $derived(glucoseData.length / chartCtx.width);
  const effectiveShowPoints = $derived(showPoints ?? pointDensity < 0.5);
</script>

<!-- Glucose axis on left -->
{#if showAxis}
  <Axis
    placement="left"
    scale={glucoseAxisScale}
    ticks={5}
    format={(v) => String(bg(v))}
    tickLabelProps={{ class: "text-xs fill-muted-foreground" }}
  />
{/if}

<ChartClipPath>
  <!-- Glucose line -->
  <Spline
    data={glucoseData}
    x={(d) => d.time}
    y={(d) => glucoseScale(d.sgv)}
    class="stroke-glucose-in-range stroke-2 fill-none"
    motion="spring"
    curve={curveMonotoneX}
  />

  <!-- Glucose points -->
  {#if effectiveShowPoints}
    {#each glucoseData as point (point.time)}
      <Points
        data={[point]}
        x={(d) => d.time}
        y={(d) => glucoseScale(d.sgv)}
        r={3}
        fill={point.color}
        class="opacity-90"
      />
    {/each}
  {/if}
</ChartClipPath>
