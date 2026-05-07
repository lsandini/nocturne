<script lang="ts">
  import {
    Spline,
    Area,
    Points,
    Axis,
    LinearGradient,
    ChartClipPath,
    getChartContext,
  } from "layerchart";
  import { curveMonotoneX } from "d3";
  import { getGlucoseChartContext } from "../chart-context.svelte";
  import { bg } from "$lib/utils/formatting";
  import {
    getGlucoseColor,
    getGlucoseColorContinuous,
  } from "$lib/utils/chart-colors";
  import {
    thresholdLineStops,
    continuousLineStops,
    fillStopsFromLineStops,
    singleColorFillStops,
    areaY0Accessor,
  } from "../utils/glucose-gradient-stops";

  type LineColorMode = "single" | "threshold" | "continuous";
  type AreaMode = "off" | "baseline" | "deviation";

  interface Props {
    showAxis?: boolean;
    showPoints?: boolean;
    areaMode?: AreaMode;
    lineColorMode?: LineColorMode;
    lineColor?: string;
    pointColorMode?: LineColorMode;
    pointColor?: string;
    areaOpacity?: number;
  }

  let {
    showAxis = true,
    showPoints,
    areaMode = "off",
    lineColorMode = "threshold",
    lineColor = "var(--glucose-in-range)",
    pointColorMode,
    pointColor,
    areaOpacity = 0.5,
  }: Props = $props();

  const ctx = getGlucoseChartContext();
  const chartCtx = getChartContext();

  const glucoseData = $derived(ctx.engine.glucoseData);
  const glucoseScale = $derived(ctx.layout.glucose.scale);
  const glucoseAxisScale = $derived(ctx.layout.glucose.axisScale);
  const thresholds = $derived(ctx.engine.thresholds);

  const pointDensity = $derived(glucoseData.length / chartCtx.width);
  const effectiveShowPoints = $derived(showPoints ?? pointDensity < 0.5);

  const effectivePointMode = $derived(pointColorMode ?? lineColorMode);
  const effectivePointColor = $derived(pointColor ?? lineColor);

  const y0 = $derived(
    areaMode === "off" ? undefined : areaY0Accessor(areaMode, thresholds),
  );

  const lineStops = $derived.by(() => {
    if (lineColorMode === "single") return null;
    if (lineColorMode === "threshold")
      return thresholdLineStops(thresholds, glucoseAxisScale, chartCtx.height);
    return continuousLineStops(glucoseAxisScale, chartCtx.height);
  });

  const fillStops = $derived.by(() => {
    if (areaMode === "off") return null;
    if (lineColorMode === "single")
      return singleColorFillStops(lineColor, areaOpacity);
    return lineStops ? fillStopsFromLineStops(lineStops, areaOpacity) : null;
  });

  function pointFill(sgv: number): string {
    if (effectivePointMode === "single") return effectivePointColor;
    if (effectivePointMode === "continuous")
      return getGlucoseColorContinuous(sgv);
    return getGlucoseColor(sgv, thresholds);
  }
</script>

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
  {#if lineColorMode === "single"}
    {#if areaMode === "off"}
      <Spline
        data={glucoseData}
        x={(d) => d.time}
        y={(d) => glucoseScale(d.sgv)}
        stroke={lineColor}
        class="stroke-2 fill-none"
        motion="spring"
        curve={curveMonotoneX}
      />
    {:else}
      <LinearGradient stops={fillStops ?? undefined} units="userSpaceOnUse" vertical>
        {#snippet children({ gradient: fillGrad })}
          <Area
            data={glucoseData}
            x={(d) => d.time}
            y={(d) => glucoseScale(d.sgv)}
            {y0}
            line={{ stroke: lineColor, class: "stroke-2" }}
            fill={fillGrad}
            curve={curveMonotoneX}
          />
        {/snippet}
      </LinearGradient>
    {/if}
  {:else}
    <LinearGradient stops={lineStops ?? undefined} units="userSpaceOnUse" vertical>
      {#snippet children({ gradient: strokeGrad })}
        {#if areaMode === "off"}
          <Spline
            data={glucoseData}
            x={(d) => d.time}
            y={(d) => glucoseScale(d.sgv)}
            stroke={strokeGrad}
            class="stroke-2 fill-none"
            motion="spring"
            curve={curveMonotoneX}
          />
        {:else}
          <LinearGradient stops={fillStops ?? undefined} units="userSpaceOnUse" vertical>
            {#snippet children({ gradient: fillGrad })}
              <Area
                data={glucoseData}
                x={(d) => d.time}
                y={(d) => glucoseScale(d.sgv)}
                {y0}
                line={{ stroke: strokeGrad, class: "stroke-2" }}
                fill={fillGrad}
                curve={curveMonotoneX}
              />
            {/snippet}
          </LinearGradient>
        {/if}
      {/snippet}
    </LinearGradient>
  {/if}

  {#if effectiveShowPoints}
    {#each glucoseData as point (point.time)}
      <Points
        data={[point]}
        x={(d) => d.time}
        y={(d) => glucoseScale(d.sgv)}
        r={3}
        fill={pointFill(point.sgv)}
        class="opacity-90"
      />
    {/each}
  {/if}
</ChartClipPath>
