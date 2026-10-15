import type { KpiDashboard, KpiEntry, KpiResult, KpiSeriesPoint } from "./types/kpiTypes";
import type { AnalystWorkload, AnalystWorkloadRow } from "./types/workloadTypes";

// Test-only builders. Defaults describe a healthy "Sample TAT" tile.
export function result(over: Partial<KpiResult> = {}): KpiResult {
  return {
    value: 4.2, numerator: null, denominator: null, n: 31, prior: null, status: "Ok",
    tooFewRecords: false, gaps: [], series: [], extras: {}, flags: [], ...over
  };
}

// Pass null as resultOver for an entry that came back without a result.
export function entry(over: Partial<KpiEntry> = {}, resultOver: Partial<KpiResult> | null = {}): KpiEntry {
  return {
    key: "sample-tat", name: "Sample TAT", unit: "days", direction: "LowerIsBetter", group: "Headline", order: 1,
    result: resultOver === null ? null : result(resultOver), error: null, ...over
  };
}

const HEADLINE: [string, string, string][] = [
  ["sample-tat", "Sample TAT", "days"],
  ["on-time-all-stages", "On time, all stages", "%"],
  ["right-first-time", "Right first time", "%"],
  ["retest-rate", "Retest rate", "%"],
  ["oos-rate", "OOS rate", "%"],
  ["wip-backlog", "WIP backlog", "tests"],
  ["incubation-reads-on-time", "Incubation reads on time", "%"],
  ["em-excursion-rate", "EM excursion rate", "%"]
];

export function headlineEntries(): KpiEntry[] {
  return HEADLINE.map(([key, name, unit], i) => entry({ key, name, unit, order: i + 1 }, { value: i + 1, n: 10 + i }));
}

export function dashboard(kpis: KpiEntry[]): KpiDashboard {
  return {
    from: "2026-09-12T00:00:00Z", to: "2026-10-12T00:00:00Z",
    priorFrom: "2026-08-13T00:00:00Z", priorTo: "2026-09-12T00:00:00Z",
    generatedAt: "2026-10-11T09:30:00Z", kpis
  };
}

export function point(label: string, value: number | null, over: Partial<KpiSeriesPoint> = {}): KpiSeriesPoint {
  return { label, group: null, at: null, value, n: 10, secondary: null, ...over };
}

export function workloadRow(over: Partial<AnalystWorkloadRow> = {}): AnalystWorkloadRow {
  return {
    userId: 1, fullName: "Amy Analyst", capacityUnits: 30, deliveredUnits: 24, utilisation: 0.8,
    utilisationCheckProfile: false, openUnits: 10, forwardCapacityUnits: 30, forwardLoad: 0.33,
    forwardLoadOverCapacity: false, weightedOnTime: 0.9, weightedRightFirstTime: 0.95,
    deliveredTests: 12, unweightedTests: 0, unweightedOpenTests: 0, testsOnDefaultTarget: 0, ...over
  };
}

export function workload(rows: AnalystWorkloadRow[], over: Partial<AnalystWorkload> = {}): AnalystWorkload {
  return {
    from: "2026-09-12T00:00:00Z", to: "2026-10-12T00:00:00Z", capacityUsesDefaults: false,
    onlyOwnRow: false, unassignedOpenTests: 0, rows, gaps: [], ...over
  };
}
