import type { TFunction } from "i18next";
import { i18n } from "../../../i18n";
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
export function formatKpiValue(unit: string, value: number | null | undefined, t?: TFunction): string {
  if (value == null) return NONE;
  const translate = t ?? i18n.getFixedT("en", "kpi");
  const v = scaleKpiValue(unit, value);
  switch (unit) {
    case "%": return `${v.toFixed(1)}%`;
    case "days": return translate("format.daysValue", { ns: "kpi", value: v.toFixed(1) });
    case "tests": return translate("format.testsValue", { ns: "kpi", value: Math.round(v) });
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
export function entryView(entry: KpiEntry, t?: TFunction): EntryView {
  if (entry.error != null) return { kind: "error", message: entry.error };
  const translate = t ?? i18n.getFixedT("en", "kpi");
  const r = entry.result;
  if (!r) return { kind: "error", message: translate("format.noResult", { ns: "kpi" }) };
  // The series-only supporting measures (stage-tat, backlog-aging, returns-by-test) may carry
  // value = null with status Ok and a populated series: that is data, not "no data".
  // Only NoData, or no value and no series, is empty.
  if (r.status === "NoData" || (r.value == null && r.series.length === 0)) return { kind: "noData", result: r };
  return { kind: "value", result: r, text: r.value == null ? "" : formatKpiValue(entry.unit, r.value, translate), muted: r.tooFewRecords };
}

export function formatPrior(unit: string, prior: KpiPrior, t?: TFunction): string {
  const translate = t ?? i18n.getFixedT("en", "kpi");
  return translate("format.priorPeriod", {
    ns: "kpi",
    value: formatKpiValue(unit, prior.value, translate),
    n: prior.n
  });
}

const EXTRAS: { key: string; label: string; percent: boolean }[] = [
  { key: "p90", label: "P90", percent: false },
  { key: "alertRate", label: "Alert rate", percent: true },
  { key: "actionRate", label: "Action rate", percent: true }
];

// Only the extras the registry is known to send; an unknown key is ignored
// rather than printed unformatted.
export function formatExtras(entry: KpiEntry, t?: TFunction): { label: string; text: string }[] {
  const translate = t ?? i18n.getFixedT("en", "kpi");
  const extras = entry.result?.extras ?? {};
  return EXTRAS.filter((x) => x.key in extras).map((x) => ({
    label: translate(`format.extras.${x.key}`, { ns: "kpi", defaultValue: x.label }),
    text: formatKpiValue(x.percent ? "%" : entry.unit, extras[x.key], translate)
  }));
}

export const FLAG_LABELS: Record<string, string> = { AdverseTrend: "Adverse trend" };

export function flagLabel(flag: string, t?: TFunction): string {
  if (!(flag in FLAG_LABELS)) return flag;
  const translate = t ?? i18n.getFixedT("en", "kpi");
  return translate(`format.flags.${flag}`, { ns: "kpi", defaultValue: FLAG_LABELS[flag] });
}

export const DIRECTION_LABEL: Record<KpiDirection, string> = {
  HigherIsBetter: "Higher is better",
  LowerIsBetter: "Lower is better",
  Neutral: "No better or worse direction"
};

// Alphabetical, case-insensitive: the default order of every people list.
export function sortByName<T extends { fullName: string }>(rows: readonly T[]): T[] {
  return [...rows].sort((a, b) => a.fullName.localeCompare(b.fullName, undefined, { sensitivity: "base" }));
}
