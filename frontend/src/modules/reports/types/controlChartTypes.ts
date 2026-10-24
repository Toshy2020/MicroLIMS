import { ResultLevel, SampleCategory } from "./reportingTypes";

// Mirrors MicroLIMS.Application.Services.Spc (ControlChartModels.cs). Every
// number here is computed server-side; the charts only draw them.

export type LimitMethod = "MovingRange" | "StandardDeviation" | "Percentile";
export type LimitRounding = "None" | "OneDecimal" | "TwoDecimals" | "UpToWholeNumber";
export type CapabilityBasis = "SpecLimit" | "ActionLimit" | "AlertLimit" | "Custom";

export interface ControlChartSettings {
  method: LimitMethod;
  alertK: number;
  actionK: number;
  alertPercentile: number;
  actionPercentile: number;
  movingRangeLength: number;
  rounding: LimitRounding;
  minimumPoints: number;
  rules: number[];
}

export const DEFAULT_SETTINGS: ControlChartSettings = {
  method: "MovingRange",
  alertK: 2,
  actionK: 3,
  alertPercentile: 95,
  actionPercentile: 99,
  movingRangeLength: 2,
  rounding: "UpToWholeNumber",
  minimumPoints: 30,
  rules: [1, 2]
};

export const RULE_LABELS: Record<number, string> = {
  1: "1 point beyond the action limit",
  2: "9 in a row on one side of the mean",
  3: "6 in a row rising or falling",
  4: "14 in a row alternating",
  5: "2 of 3 beyond 2σ (same side)",
  6: "4 of 5 beyond 1σ (same side)",
  7: "15 in a row within 1σ",
  8: "8 in a row beyond 1σ"
};

export interface ProposedLimits {
  alertUpper: number | null;
  actionUpper: number | null;
  alertLower: number | null;
  actionLower: number | null;
}

export interface CapabilityIndices {
  lsl: number | null;
  usl: number | null;
  cp: number | null;
  cpk: number | null;
  cpu: number | null;
  cpl: number | null;
  pp: number | null;
  ppk: number | null;
  observedPercentOutside: number | null;
  expectedPercentOutside: number | null;
}

export interface HistogramBin {
  from: number;
  to: number;
  count: number;
  expectedCount: number | null;
}

export interface BoxSummary {
  min: number;
  q1: number;
  median: number;
  q3: number;
  max: number;
  lowerWhisker: number;
  upperWhisker: number;
  outliers: number[];
}

export interface ControlChartComputation {
  count: number;
  mean: number | null;
  median: number | null;
  min: number | null;
  max: number | null;
  standardDeviation: number | null;
  sigmaWithin: number | null;
  sigmaUsed: number | null;
  movingRangeMean: number | null;
  movingRangeUcl: number | null;
  movingRangeLcl: number | null;
  belowMinimumPoints: boolean;
  limits: ProposedLimits;
  movingRanges: (number | null)[];
  ruleViolations: number[][];
  capability: CapabilityIndices;
  histogram: HistogramBin[];
  box: BoxSummary | null;
}

export interface ControlChartPoint {
  recordId: number;
  referenceNumber: string;
  date: string;
  value: number;
  reportedValue: string;
  isImputed: boolean;
  movingRange: number | null;
  resultLevel: ResultLevel;
  inCalculation: boolean;
  rules: number[];
  alertLimit: number | null;
  actionLimit: number | null;
  specLimit: number | null;
}

export interface MonthlyBox {
  month: string;
  count: number;
  mean: number;
  box: BoxSummary;
}

export type LimitTargetKind = "RoomTestConfiguration" | "SamplingConfiguration";

export interface LimitTarget {
  kind: LimitTargetKind;
  configurationId: number;
  version: number;
  location: string;
  detail: string | null;
  testCode: string;
  testType: string | null;
  alertLimit: string;
  actionLimit: string;
  specLimit: string;
  unit: string;
}

export interface CurrentLimits {
  alert: number | null;
  action: number | null;
  specLower: number | null;
  specUpper: number | null;
}

export interface ControlChartResult {
  testCode: string;
  testDisplayName: string;
  subjectName: string;
  unit: string | null;
  settings: ControlChartSettings;
  capabilityBasis: CapabilityBasis;
  imputedPointCount: number;
  statistics: ControlChartComputation;
  currentLimits: CurrentLimits;
  points: ControlChartPoint[];
  monthly: MonthlyBox[];
  limitTargets: LimitTarget[];
}

export interface LimitReviewRow {
  subjectName: string;
  count: number;
  belowMinimumPoints: boolean;
  mean: number | null;
  standardDeviation: number | null;
  sigmaUsed: number | null;
  proposedAlert: number | null;
  proposedAction: number | null;
  currentAlert: number | null;
  currentAction: number | null;
  cpk: number | null;
  flaggedPoints: number;
  limitTargets: LimitTarget[];
}

export interface LimitReviewResult {
  testCode: string;
  testDisplayName: string;
  category: SampleCategory;
  unit: string | null;
  settings: ControlChartSettings;
  rows: LimitReviewRow[];
}

export interface ControlChartCriteria {
  category: SampleCategory;
  testCode: string;
  subjectName: string;
  fromDate: string;
  toDate: string;
  calculationFrom: string;
  calculationTo: string;
  approvedOnly: boolean;
  capabilityBasis: CapabilityBasis;
  customLsl: string;
  customUsl: string;
}

export interface ApplyLimitItem {
  kind: LimitTargetKind;
  configurationId: number;
  version: number;
  subjectName: string;
  alertLimit: number;
  actionLimit: number;
}

export interface AppliedLimit {
  kind: LimitTargetKind;
  configurationId: number;
  location: string;
  previousAlert: string;
  previousAction: string;
  newAlert: string;
  newAction: string;
  calculatedAlert: number | null;
  calculatedAction: number | null;
  overridden: boolean;
  signatureId: number;
}

export type ChartKind =
  | "imr"
  | "individuals"
  | "movingRange"
  | "run"
  | "bar"
  | "scatter"
  | "area"
  | "histogram"
  | "boxplot"
  | "monthly";

export const CHART_KINDS: { value: ChartKind; label: string; description: string }[] = [
  { value: "imr", label: "I-MR chart", description: "Individuals and moving range charts together (Minitab style)" },
  { value: "individuals", label: "Individuals (I) chart", description: "Results with mean, alert (±2σ) and action (±3σ) limits" },
  { value: "movingRange", label: "Moving range (MR) chart", description: "Point-to-point variation with its own control limit" },
  { value: "run", label: "Run chart (line)", description: "Results over time with the configured limits" },
  { value: "bar", label: "Bar chart", description: "One bar per result, coloured by result level" },
  { value: "scatter", label: "Scatter plot", description: "Results as points, without connecting lines" },
  { value: "area", label: "Area chart", description: "Results as a filled area" },
  { value: "histogram", label: "Capability histogram", description: "Distribution with a normal curve and the capability limits" },
  { value: "boxplot", label: "Box plot by month", description: "Median, quartiles and outliers per month" },
  { value: "monthly", label: "Monthly summary", description: "Monthly mean and maximum with the configured limits" }
];

// ---- Combined report ------------------------------------------------------

export type ReportFormat = "Pdf" | "Word";

// One chart queued for the combined report: the exact query and settings
// it was drawn from (the server recomputes every figure from these) and
// the picture of it.
export interface ReportItem {
  id: string;
  title: string;
  chartKind: ChartKind;
  chartLabel: string;
  subjectName: string;
  testDisplayName: string;
  criteria: ControlChartCriteria;
  settings: ControlChartSettings;
  image: string; // PNG data URL
}
