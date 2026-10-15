// Mirrors backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs (camelCase, enums as strings).
// Ratios for unit "%" are fractions (1 = 100%); the page formats them.
export type KpiDirection = "HigherIsBetter" | "LowerIsBetter" | "Neutral";
export type KpiGroup = "Headline" | "Flow" | "Quality" | "Micro" | "Workload";
export type KpiStatus = "Ok" | "NoData" | "Partial";

export interface KpiPrior { value: number | null; n: number; numerator: number | null; denominator: number | null }
export interface KpiSeriesPoint { label: string; group: string | null; at: string | null; value: number | null; n: number; secondary: number | null }
export interface KpiGap { code: string; message: string; count: number }
export interface KpiResult {
  value: number | null; numerator: number | null; denominator: number | null; n: number; prior: KpiPrior | null;
  status: KpiStatus; tooFewRecords: boolean; gaps: KpiGap[]; series: KpiSeriesPoint[];
  extras: Record<string, number | null>; flags: string[];
}
export interface KpiEntry {
  key: string; name: string; unit: string; direction: KpiDirection; group: KpiGroup; order: number;
  result: KpiResult | null; error: string | null;
}
// `to` and `priorTo` are exclusive upper bounds (the selected end date plus one day, 00:00 UTC).
export interface KpiDashboard { from: string; to: string; priorFrom: string; priorTo: string; generatedAt: string; kpis: KpiEntry[] }
export interface KpiDefinition {
  key: string; name: string; unit: string; direction: KpiDirection; group: KpiGroup; order: number;
  definition: string; clockEvents: string; population: string; source: string;
}
export interface ProfileInForce { workflowType: string; testCode: string | null; workUnits: number | null; targetAnalystDays: number | null; effectiveFrom: string; versionId: number }
export interface KpiDefinitions { kpis: KpiDefinition[]; profilesInForce: ProfileInForce[] }
export interface KpiRecord {
  sampleId: number | null; testOrderId: number | null; controlNumber: string | null; testCode: string | null; category: string | null;
  analystName: string | null; eventAt: string | null; value: number | null; unit: string | null;
  inclusion: "Numerator" | "Denominator" | "Excluded"; note: string | null;
}
export interface KpiRecordPage { key: string; page: number; pageSize: number; total: number; items: KpiRecord[] }
