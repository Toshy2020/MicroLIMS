export type MonitoringCategory = "Water" | "EnvironmentalMonitoring" | "AfterCleaning";

export type OccurrenceStatus =
  | "Upcoming" | "Open" | "DueToday" | "PartlyPerformed" | "InTesting" | "AwaitingReview"
  | "AwaitingApproval" | "Performed" | "PerformedExcursion" | "Overdue" | "Missed" | "Skipped" | "Retired";

export type ItemProgress =
  | "None" | "InTesting" | "AwaitingReview" | "AwaitingApproval" | "Performed" | "PerformedExcursion";

export interface OccurrenceSummary {
  id: number;
  version: number;
  category: MonitoringCategory;
  target: string;
  group: string | null;
  periodKey: string;
  targetDate: string | null;
  windowStart: string;
  windowEnd: string;
  originalWindowEnd: string;
  status: OccurrenceStatus;
  late: boolean;
  itemsTotal: number;
  itemsPerformed: number;
  manual: boolean;
}

export interface OccurrenceItem {
  itemId: number;
  label: string;
  progress: ItemProgress;
  sampleReference: string | null;
}

export interface OccurrenceEvent {
  id: number;
  eventType: string;
  oldValue: string | null;
  newValue: string | null;
  reason: string | null;
  performedBy: string;
  performedAt: string;
  signed: boolean;
  secondSigner: string | null;
}

export interface OccurrenceDetail {
  summary: OccurrenceSummary;
  frequencySnapshot: string;
  originalTargetDate: string | null;
  items: OccurrenceItem[];
  events: OccurrenceEvent[];
}

export interface MonthSummary {
  month: number;
  total: number;
  performed: number;
  inProgress: number;
  overdue: number;
  upcoming: number;
  skipped: number;
}

export interface YearSummary {
  year: number;
  months: MonthSummary[];
}

export interface DueResult {
  today: string;
  warningDayOfMonth: number;
  showWarning: boolean;
  items: OccurrenceSummary[];
}

export interface ChecklistItem { itemId: number; label: string }

export interface ChecklistOccurrence {
  occurrenceId: number;
  target: string;
  periodKey: string;
  targetDate: string | null;
  windowStart: string;
  windowEnd: string;
  status: OccurrenceStatus;
  preTicked: boolean;
  remainingItems: ChecklistItem[];
}

export interface ChecklistSection { key: string; title: string; occurrences: ChecklistOccurrence[] }

export interface MonitoringChecklist {
  sampleId: number;
  category: MonitoringCategory;
  sections: ChecklistSection[];
}

export interface SignedChange {
  reason: string;
  password: string;
  secondSignerUsername: string;
  secondSignerPassword: string;
}

export interface CoverageRow {
  category: MonitoringCategory;
  target: string;
  group: string | null;
  planned: number;
  performed: number;
  performedLate: number;
  inProgress: number;
  overdue: number;
  upcoming: number;
  skipped: number;
  coveragePercent: number | null;
}

export interface CoverageReport { year: number; asOf: string; rows: CoverageRow[]; total: CoverageRow }

export interface SpacingRow {
  category: MonitoringCategory;
  target: string;
  group: string | null;
  frequency: string;
  previousOn: string;
  nextOn: string;
  gapDays: number;
  minDays: number;
  maxDays: number;
  flagged: boolean;
}

export interface SpacingReport { year: number; rows: SpacingRow[] }

export interface MonitoringSettings {
  id: number;
  version: number;
  labWeekStartDay: string;
  workingWeekdays: number;
  waterEarlyToleranceDays: number;
  waterLateToleranceDays: number;
  emEarlyToleranceDays: number;
  emLateToleranceDays: number;
  acEarlyToleranceDays: number;
  acLateToleranceDays: number;
  warningDayOfMonth: number;
  graceDays: number;
  monthlyMinGapDays: number;
  monthlyMaxGapDays: number;
  weeklyMinGapDays: number;
  weeklyMaxGapDays: number;
  deviationDueDays: number;
  followUpWindowDays: number;
}

export interface LabCalendarDate { id: number; date: string; description: string }

export interface PlanRevision {
  id: number;
  version: number;
  year: number;
  revisionNumber: number;
  status: "Draft" | "Approved" | "Superseded";
  reason: string | null;
  effectiveFrom: string;
  generatedAt: string;
  approvedAt: string | null;
  occurrenceCount: number;
}

// An occurrence that has not started and that approving the revision moves to new dates.
export interface PlanReschedule {
  occurrenceId: number;
  category: MonitoringCategory;
  target: string;
  periodKey: string;
  oldTargetDate: string | null;
  oldWindowStart: string;
  oldWindowEnd: string;
  newTargetDate: string | null;
  newWindowStart: string;
  newWindowEnd: string;
}

export interface PlanPreview {
  revision: PlanRevision;
  totals: { category: MonitoringCategory; targets: number; occurrences: number }[];
  toRetire: { occurrenceId: number; category: MonitoringCategory; target: string; periodKey: string; windowStart: string; windowEnd: string }[];
  toReschedule: PlanReschedule[];
  exceptions: { kind: string; category: MonitoringCategory; target: string; message: string }[];
}

export type DeviationKind = "MissedOccurrence" | "ActionLimitExceeded";
export type DeviationStatus = "Open" | "Closed";
export type DeviationClosure = "NotApplicableBeforeGoLive" | "Justified" | "Investigated";

// A tracked record of a missed occurrence or a result over its action limit.
export interface Deviation {
  id: number;
  version: number;
  number: string;
  kind: DeviationKind;
  status: DeviationStatus;
  occurrenceId: number;
  category: MonitoringCategory;
  target: string;
  group: string | null;
  periodKey: string;
  targetDate: string | null;
  windowStart: string;
  windowEnd: string;
  description: string;
  openedAt: string;
  dueDate: string;
  pastDue: boolean;
  followUpOccurrenceId: number | null;
  closure: DeviationClosure | null;
  closureReason: string | null;
  closedAt: string | null;
}

export interface DeviationList { openCount: number; pastDueCount: number; items: Deviation[] }

export interface DeviationScan { missedOpened: number; excursionsOpened: number; followUpsCreated: number }
