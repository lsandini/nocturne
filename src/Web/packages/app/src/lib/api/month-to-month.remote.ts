import { getRequestEvent, query } from "$app/server";
import { z } from "zod";

const MONTH_NAMES = [
  "January",
  "February",
  "March",
  "April",
  "May",
  "June",
  "July",
  "August",
  "September",
  "October",
  "November",
  "December",
];

const punchCardSchema = z.object({
  fromDate: z.string(),
  toDate: z.string(),
});

export const getPunchCardData = query(
  punchCardSchema,
  async ({ fromDate, toDate }) => {
    const { locals } = getRequestEvent();
    const { apiClient } = locals;

    const startDate = new Date(fromDate);
    const endDate = new Date(toDate);

    if (isNaN(startDate.getTime()) || isNaN(endDate.getTime())) {
      return null;
    }

    // Set to full day boundaries
    startDate.setHours(0, 0, 0, 0);
    endDate.setHours(23, 59, 59, 999);

    // Fetch glucose readings, boluses, carb intakes, and daily basal/bolus ratios for the full range
    const [glucoseResponse, bolusResponse, carbResponse, dailyBasalBolus] =
      await Promise.all([
        apiClient.sensorGlucose.getAll(startDate, endDate, 100000),
        apiClient.bolus.getAll(startDate, endDate, 10000),
        apiClient.nutrition.getCarbIntakes(startDate, endDate, 10000),
        apiClient.statistics.getDailyBasalBolusRatios(startDate, endDate),
      ]);

    const allEntries = glucoseResponse.data ?? [];
    const allBoluses = bolusResponse.data ?? [];
    const allCarbs = carbResponse.data ?? [];

    // Build a lookup of per-day basal totals from the daily basal/bolus ratio endpoint
    const dailyBasalMap = new Map<string, number>();
    for (const day of dailyBasalBolus?.dailyData ?? []) {
      if (day.date) {
        dailyBasalMap.set(day.date, day.basal ?? 0);
      }
    }

    // Group by month
    const monthsMap = new Map<
      string,
      {
        year: number;
        month: number;
        monthName: string;
        days: Array<{
          date: string;
          timestamp: number;
          totalReadings: number;
          inRangePercent: number;
          lowPercent: number;
          highPercent: number;
          averageGlucose: number;
          totalCarbs: number;
          totalInsulin: number;
          totalBolus: number;
          totalBasal: number;
          carbToInsulinRatio: number;
          inRangeCount: number;
          lowCount: number;
          highCount: number;
          entries: Array<{ mills: number; mgdl: number }>;
        }>;
        maxCarbs: number;
        maxInsulin: number;
        maxCarbInsulinDiff: number;
        totalReadings: number;
        summary: {
          dayCount: number;
          totalReadings: number;
          inRangePercent: number;
          lowPercent: number;
          highPercent: number;
          avgGlucose: number;
        } | null;
      }
    >();

    // First pass: split inputs by day. No I/O — just filtering already-fetched arrays
    // into per-day buckets. Days are processed in chronological order so the resulting
    // months map preserves the original ordering even though API calls run in parallel.
    type DayInput = {
      date: string;
      timestamp: number;
      monthKey: string;
      dayEntries: typeof allEntries;
      dayBoluses: typeof allBoluses;
      dayCarbs: typeof allCarbs;
    };
    const dayInputs: DayInput[] = [];

    const currentDate = new Date(startDate);
    currentDate.setHours(0, 0, 0, 0);

    while (currentDate <= endDate) {
      const year = currentDate.getFullYear();
      const month = currentDate.getMonth();
      const monthKey = `${year}-${month}`;

      if (!monthsMap.has(monthKey)) {
        monthsMap.set(monthKey, {
          year,
          month,
          monthName: MONTH_NAMES[month],
          days: [],
          maxCarbs: 0,
          maxInsulin: 0,
          maxCarbInsulinDiff: 0,
          totalReadings: 0,
          summary: null,
        });
      }

      const dayStart = new Date(currentDate);
      dayStart.setHours(0, 0, 0, 0);
      const dayEnd = new Date(currentDate);
      dayEnd.setHours(23, 59, 59, 999);
      const dayStartMs = dayStart.getTime();
      const dayEndMs = dayEnd.getTime();

      dayInputs.push({
        date: `${currentDate.getFullYear()}-${String(currentDate.getMonth() + 1).padStart(2, "0")}-${String(currentDate.getDate()).padStart(2, "0")}`,
        timestamp: dayStartMs,
        monthKey,
        dayEntries: allEntries.filter((e) => {
          const t = e.mills ?? 0;
          return t >= dayStartMs && t <= dayEndMs;
        }),
        dayBoluses: allBoluses.filter((b) => {
          const t = b.mills ?? 0;
          return t >= dayStartMs && t <= dayEndMs;
        }),
        dayCarbs: allCarbs.filter((c) => {
          const t = c.mills ?? 0;
          return t >= dayStartMs && t <= dayEndMs;
        }),
      });

      currentDate.setDate(currentDate.getDate() + 1);
    }

    // Second pass: compute per-day metrics in parallel. The `calculateTimeInRange` and
    // `calculateTreatmentSummary` endpoints are pure math (no DB), so this batch is safe
    // to run concurrently. Drops a 31-day month from ~62 sequential round-trips to one
    // parallel batch.
    const dayResults = await Promise.all(
      dayInputs.map(async (input) => {
        const [tirMetrics, treatmentSummary] = await Promise.all([
          input.dayEntries.length > 0
            ? apiClient.statistics.calculateTimeInRange({
                entries: input.dayEntries,
              })
            : null,
          input.dayBoluses.length > 0 || input.dayCarbs.length > 0
            ? apiClient.statistics.calculateTreatmentSummary({
                boluses: input.dayBoluses,
                carbIntakes: input.dayCarbs,
              })
            : null,
        ]);
        return { input, tirMetrics, treatmentSummary };
      })
    );

    // Third pass: assemble per-month data in original chronological order.
    for (const { input, tirMetrics, treatmentSummary } of dayResults) {
      const percentages = tirMetrics?.percentages;
      const inRangePercent = percentages?.target ?? 0;
      const lowPercent = (percentages?.veryLow ?? 0) + (percentages?.low ?? 0);
      const highPercent =
        (percentages?.veryHigh ?? 0) + (percentages?.high ?? 0);

      const durations = tirMetrics?.durations;
      const totalMinutes =
        (durations?.veryLow ?? 0) +
        (durations?.low ?? 0) +
        (durations?.target ?? 0) +
        (durations?.high ?? 0) +
        (durations?.veryHigh ?? 0);
      const totalReadings = Math.round(totalMinutes / 5);

      const inRangeCount = Math.round((inRangePercent / 100) * totalReadings);
      const lowCount = Math.round((lowPercent / 100) * totalReadings);
      const highCount = Math.round((highPercent / 100) * totalReadings);

      const rangeStats = tirMetrics?.rangeStats;
      const averageGlucose =
        rangeStats?.target?.mean ?? rangeStats?.low?.mean ?? 0;

      const totals = treatmentSummary?.totals;
      const totalCarbs = totals?.food?.carbs ?? 0;
      const totalBolus = totals?.insulin?.bolus ?? 0;
      const totalBasal = dailyBasalMap.get(input.date) ?? 0;
      const totalInsulin = totalBolus + totalBasal;

      const carbToInsulinRatio = treatmentSummary?.carbToInsulinRatio ?? 0;

      const entries = input.dayEntries
        .filter((e) => e.mills != null && e.mgdl != null)
        .map((e) => ({ mills: e.mills!, mgdl: e.mgdl! }))
        .sort((a, b) => a.mills - b.mills);

      const dayStats = {
        date: input.date,
        timestamp: input.timestamp,
        totalReadings,
        inRangeCount,
        lowCount,
        highCount,
        inRangePercent,
        lowPercent,
        highPercent,
        averageGlucose,
        totalCarbs,
        totalInsulin,
        totalBolus,
        totalBasal,
        carbToInsulinRatio,
        entries,
      };

      const monthData = monthsMap.get(input.monthKey)!;
      monthData.days.push(dayStats);
      monthData.maxCarbs = Math.max(monthData.maxCarbs, dayStats.totalCarbs);
      monthData.maxInsulin = Math.max(
        monthData.maxInsulin,
        dayStats.totalInsulin
      );
      monthData.maxCarbInsulinDiff = Math.max(
        monthData.maxCarbInsulinDiff,
        Math.abs(dayStats.carbToInsulinRatio)
      );
      monthData.totalReadings += dayStats.totalReadings;
    }

    const months = Array.from(monthsMap.values()).sort((a, b) => {
      if (a.year !== b.year) return a.year - b.year;
      return a.month - b.month;
    });

    // Compute month-level summaries server-side so the frontend doesn't need to aggregate
    for (const month of months) {
      const daysWithData = month.days.filter((d) => d.totalReadings > 0);
      let totalInRange = 0;
      let totalLow = 0;
      let totalHigh = 0;
      let totalReadings = 0;
      let glucoseSum = 0;
      let glucoseDays = 0;

      for (const d of daysWithData) {
        totalInRange += d.inRangeCount;
        totalLow += d.lowCount;
        totalHigh += d.highCount;
        totalReadings += d.totalReadings;
        if (d.averageGlucose > 0) {
          glucoseSum += d.averageGlucose;
          glucoseDays++;
        }
      }

      month.summary = {
        dayCount: daysWithData.length,
        totalReadings,
        inRangePercent:
          totalReadings > 0 ? (totalInRange / totalReadings) * 100 : 0,
        lowPercent: totalReadings > 0 ? (totalLow / totalReadings) * 100 : 0,
        highPercent: totalReadings > 0 ? (totalHigh / totalReadings) * 100 : 0,
        avgGlucose: glucoseDays > 0 ? glucoseSum / glucoseDays : 0,
      };
    }

    let globalMaxCarbs = 0;
    let globalMaxInsulin = 0;
    let globalMaxCarbInsulinDiff = 0;

    for (const month of months) {
      globalMaxCarbs = Math.max(globalMaxCarbs, month.maxCarbs);
      globalMaxInsulin = Math.max(globalMaxInsulin, month.maxInsulin);
      globalMaxCarbInsulinDiff = Math.max(
        globalMaxCarbInsulinDiff,
        month.maxCarbInsulinDiff
      );
    }

    return {
      months,
      dateRange: {
        from: startDate.toISOString(),
        to: endDate.toISOString(),
      },
      globalMaxCarbs,
      globalMaxInsulin,
      globalMaxCarbInsulinDiff,
    };
  }
);
