import type { KpiDirection, KpiEntry, KpiPrior, KpiResult } from "../types/kpiTypes";

// Wire scales, confirmed against the backend (KpiScope.Rate returns Math.Round(n/d, 4); workload
// ratios are fractions too). KPI values whose unit is "%" are scaled by KPI_PERCENT_SCALE to reach a
// 0-100 number (0.875 = 87.5%); workload ratios (utilisation, forward load, weighted rates) by
// WORKLOAD_RATIO_SCALE. If a backend slice changes either, change it here and nowhere else.
export const KPI_PERCENT_SCALE = 100;
export const WORKLOAD_RATIO_SCALE = 100;

const NONE = "—";

export function scaleKpiValue(unit: string, value: number): number {
  return unit === "%" ? value * KPI_PERCENT_SCALE : value;
}

// null/undefined is the only "no value"; 0 is a real value and must print.
export function formatKpiValue(unit: string, value: number | null | undefined): string {
  if (value == null) return NONE;
  const v = scaleKpiValue(unit, value);
  switch (unit) {
    case "%": return `${v.toFixed(1)}%`;
    case "days": return `${v.toFixed(1)} days`;
    case "tests": return `${Math.round(v)} tests`;
    default: return `${v.toFixed(1)} ${unit}`;
  }
}

export function formatRatio(value: number | null | undefined): string {
  return value == null ? NONE : `${(value * WORKLOAD_RATIO_SCALE).toFixed(0)}%`;
}

export function formatUnits(value: number | null | undefined): string {
  return value == null ? NONE : value.toFixed(1);
}

export type EntryView =
  | { kind: "error"; message: string }
  | { kind: "noData"; result: KpiResult }
  | { kind: "value"; result: KpiResult; text: string; muted: boolean };

// The one decision about what an entry looks like. Tiles, panels and the PDF
// all read it, so screen and print cannot disagree.
export function entryView(entry: KpiEntry): EntryView {
  if (entry.error != null) return { kind: "error", message: entry.error };
  const r = entry.result;
  if (!r) return { kind: "error", message: "No result was returned for this measure." };
  // The series-only supporting measures (stage-tat, backlog-aging, returns-by-test) may carry
  // value = null with status Ok and a populated series: that is data, not "no data".
  // Only NoData, or no value and no series, is empty.
  if (r.status === "NoData" || (r.value == null && r.series.length === 0)) return { kind: "noData", result: r };
  return { kind: "value", result: r, text: r.value == null ? "" : formatKpiValue(entry.unit, r.value), muted: r.tooFewRecords };
}

export function formatPrior(unit: string, prior: KpiPrior): string {
  return `Prior period: ${formatKpiValue(unit, prior.value)} (n=${prior.n})`;
}

const EXTRAS: { key: string; label: string; percent: boolean }[] = [
  { key: "p90", label: "P90", percent: false },
  { key: "alertRate", label: "Alert rate", percent: true },
  { key: "actionRate", label: "Action rate", percent: true }
];

// Only the extras the registry is known to send; an unknown key is ignored
// rather than printed unformatted.
export function formatExtras(entry: KpiEntry): { label: string; text: string }[] {
  const extras = entry.result?.extras ?? {};
  return EXTRAS.filter((x) => x.key in extras).map((x) => ({
    label: x.label,
    text: formatKpiValue(x.percent ? "%" : entry.unit, extras[x.key])
  }));
}

export const FLAG_LABELS: Record<string, string> = { AdverseTrend: "Adverse trend" };

export const DIRECTION_LABEL: Record<KpiDirection, string> = {
  HigherIsBetter: "Higher is better",
  LowerIsBetter: "Lower is better",
  Neutral: "No better or worse direction"
};

// Alphabetical, case-insensitive: the default order of every people list.
export function sortByName<T extends { fullName: string }>(rows: readonly T[]): T[] {
  return [...rows].sort((a, b) => a.fullName.localeCompare(b.fullName, undefined, { sensitivity: "base" }));
}
