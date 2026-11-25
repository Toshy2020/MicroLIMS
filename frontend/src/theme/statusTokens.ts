import type { TFunction } from "i18next";
import { i18n } from "../i18n";

export interface StatusToneTokens {
  bg: string;
  text: string;
  border: string;
}

export type StatusTone =
  | "detected" | "notDetected" | "inconclusive" | "pending"
  | "action" | "info" | "purple" | "pale";

// detected/notDetected/inconclusive/pending are lifted verbatim from the
// approved dark-mode mockup (darkmode_mockup.html) - not a naive inversion
// of the light values, each dark tone is its own hand-picked
// desaturated-bg + bright-text + mid-tone-border triad. action/info/purple/
// pale extend the same formula to cover every other status this app colors
// (equipment, media lifecycle, task urgency, etc. - see STATUS_TONE below),
// so the whole app shares one token system instead of a per-component map.
export const statusTonesByMode: Record<"light" | "dark", Record<StatusTone, StatusToneTokens>> = {
  light: {
    detected:     { bg: "#FCEAEA", text: "#A21C1C", border: "#E8B9B9" },
    notDetected:  { bg: "#E8F5EC", text: "#1F7A3D", border: "#B7E0C4" },
    inconclusive: { bg: "#FFF4E0", text: "#92620A", border: "#F0D397" },
    pending:      { bg: "#EEF1F4", text: "#5A6472", border: "#C4CDD5" },
    action:       { bg: "#FCEADD", text: "#9A4A0A", border: "#EFC49A" },
    info:         { bg: "#E6EEFB", text: "#1D4E96", border: "#B9CFEF" },
    purple:       { bg: "#F3E8FB", text: "#6B21A8", border: "#DCC0EF" },
    pale:         { bg: "#FEF9C3", text: "#854D0E", border: "#FDE68A" }
  },
  dark: {
    detected:     { bg: "#3A1F1F", text: "#F0A8A8", border: "#5C2C2C" },
    notDetected:  { bg: "#1B3325", text: "#8FDBA8", border: "#2C4E38" },
    inconclusive: { bg: "#3A2E10", text: "#F0CC7A", border: "#574616" },
    pending:      { bg: "#262C35", text: "#99A3B0", border: "#333B47" },
    action:       { bg: "#3A2712", text: "#F0B27A", border: "#5C3D1E" },
    info:         { bg: "#1B2A40", text: "#8FB8EF", border: "#2C4560" },
    purple:       { bg: "#2E1F3A", text: "#D8A8F0", border: "#4A2C5C" },
    pale:         { bg: "#3A3410", text: "#F0DE7A", border: "#5C531C" }
  }
};

export interface CountdownTokens {
  active: { bg: string; text: string };
  locked: { bg: string; text: string };
}

// Incubation countdown chip - locked stays a flat neutral regardless of
// mode, active gets a tinted-primary treatment, per the mockup.
export const countdownTokensByMode: Record<"light" | "dark", CountdownTokens> = {
  light: {
    active: { bg: "#EAF0FB", text: "#2E5AAC" },
    locked: { bg: "#F1F2F4", text: "#606A78" }
  },
  dark: {
    active: { bg: "#22344F", text: "#8AB0F0" },
    locked: { bg: "#262C35", text: "#949DA9" }
  }
};

// Every status string StatusBadge/CategoryBadge/etc. currently colors,
// bucketed into one of the eight tones above. Extend this map (not a
// component-local hex value) when a new status is introduced, so it picks
// up dark-mode tokens automatically instead of needing its own fix later.
const STATUS_TONE: Record<string, StatusTone> = {
  Received: "pending", Waiting: "pending",
  Running: "info", InProgress: "info",
  ReadyToRead: "purple", "Ready to Read": "purple",
  EnterResult: "pale", "Enter Result": "pale",
  "Ready: Plating": "info", "Ready: Broth": "info", "Ready: Setup": "info", "Ready: Confirmatory": "info", "Ready: Subculture": "info",
  Incubating: "action", ResultEntered: "info", Ready: "info", Reviewed: "action",
  PendingReview: "action", "Pending Review": "action",
  Completed: "notDetected", Approved: "notDetected",
  Rejected: "detected", RetestRequested: "action",
  // Closed sample/test states (WorkflowStateResolver): Voided is terminal like
  // Rejected; On Hold waits on a retest outcome.
  Voided: "detected", OnHold: "inconclusive", "On Hold": "inconclusive",
  Active: "notDetected", Inactive: "pending", Frozen: "pending",
  InStock: "notDetected", LowStock: "inconclusive", Depleted: "action", Expired: "detected",
  "Pending Evaluation": "pending", "Awaiting Approval": "action", Released: "notDetected", "Out of Stock": "pending", OutOfStock: "pending",
  Overdue: "detected", "Due Soon": "inconclusive", CalibrationDueSoon: "inconclusive",
  InService: "notDetected", OutOfService: "action", Retired: "pending",
  WithinLimits: "notDetected", AlertLimitExceeded: "inconclusive", ActionLimitExceeded: "action", OutOfSpecification: "detected",
  WithinLimit: "notDetected", AlertLevel: "inconclusive", ActionLevel: "action", NotApplicable: "pending",
  LimitsNotConfigured: "inconclusive", "Limits Not Configured": "inconclusive",
  Detected: "detected", Absent: "notDetected",
  DueSoon: "action", DueToday: "action", DueTomorrow: "notDetected",
  Passed: "notDetected", Failed: "detected", Pending: "pending",
  Returned: "action",
  // Sample-level workflow states (SampleStatus) and the workflow gate that
  // stops a test from progressing. Before these entries they fell through to
  // the grey default, so a sample waiting on the Section Head looked the same
  // as one nobody had touched.
  InTesting: "info", "In Testing": "info",
  UnderReview: "action", "Under Review": "action",
  UnderApproval: "action", "Under Approval": "action",
  Blocked: "inconclusive",

  // Document Control lifecycle. Before this block every DC status fell
  // through statusTone()'s "pending" default, so Draft, Effective, Void and
  // Superseded all rendered as the same grey pill - in a GMP system that is
  // the failure mode that lets an analyst work from an unapproved SOP, so
  // the tones below are grouped by "is this revision safe to execute?":
  //   pending (grey)      - pre-control, not yet a controlled copy
  //   info/action/purple  - inside the approval workflow
  //   notDetected (green) - Effective, the ONLY executable state
  //   inconclusive (amber)- historical, was controlled, superseded
  //   detected (red)      - terminal / do-not-use
  // Canonical 8 revision states per ML-DC-FRS-1B-001 §4:376-389.
  Draft: "pending",
  InReview: "info",
  AwaitingApproval: "action",
  FutureEffective: "purple", "Future Effective": "purple",
  Effective: "notDetected",
  Superseded: "inconclusive",
  Obsolete: "detected",
  Cancelled: "detected",
  // DocumentMaster.RecordStatus (ML-DC-FRS-1A-001 §5:102,117). "Active" is
  // already mapped above for equipment/media and resolves to green there too.
  Void: "detected",
  // Fallback for a master holding no revisions at all - see
  // documentControl/documentStatusDisplay.ts. Grey = not a controlled copy.
  "No Revision": "pending",
  // Reading assignments (ML-DC-FRS-1C-001 §2). Overdue/Cancelled share the
  // entries above; Acknowledged is the terminal compliant state.
  Assigned: "info",
  Reading: "info",
  Acknowledged: "notDetected",
  CompletedPassed: "notDetected",
  SupersededIncomplete: "inconclusive",
  // Training matrix cell states (ML-DC-FRS-1C-001 §2:188-192). The spec assigns
  // these specific colors: Qualified green, Pending yellow, Overdue red,
  // TrainedOnSupersededOnly orange warning, NotAssigned grey dash.
  Qualified: "notDetected",
  TrainedOnSupersededOnly: "inconclusive",
  NotAssigned: "pending",
  // Periodic review outcomes + technical review task/finding states.
  RemainsValid: "notDetected",
  RevisionRequired: "action",
  ObsolescenceRecommended: "detected",
  ReturnedForCorrection: "action",
  Open: "action",
  AuthorResponded: "info",
  ReviewerVerified: "purple",
  Resolved: "notDetected",

  // Physicochemical (FP) results, preparations and suitability outcomes.
  Pass: "notDetected", Fail: "detected",
  BelowSpec: "action", "Below Spec": "action",
  Suitable: "notDetected", NotSuitable: "detected", "Not Suitable": "detected",
  Conforms: "notDetected", DoesNotConform: "detected", "Does Not Conform": "detected",
  Prepared: "info", InUse: "action", "In Use": "action", Discarded: "pending",
  Valid: "notDetected", Invalid: "detected",

  // HPLC workspace (instrument state, run/sample status, review flags).
  Abandoned: "pending", Available: "notDetected", Unavailable: "detected",
  Removed: "pending", RequiresReview: "action",

  // Calibration runs withdrawn after the fact.
  Withdrawn: "inconclusive",

  // Analyst competency matrix cell states (D4).
  ExpiringSoon: "action",
  NotQualified: "pending",

  // Audit trail review & finding states.
  Signed: "notDetected",
  NoFindings: "notDetected",
  FindingsRecorded: "action",
  Closed: "notDetected",

  // OOS / Excursion investigation register states.
  PhaseOne: "info",
  AwaitingRetest: "action",
  PhaseTwo: "purple",
  Concluded: "notDetected",
  Confirmed: "detected",
  Invalidated: "notDetected",
  ExcursionClosed: "notDetected",
  Oos: "detected",
  Excursion: "action",
  Addressed: "notDetected",
  LaboratoryErrorConfirmed: "action",
  NoLaboratoryErrorFound: "info"
};

export function statusTone(status: string): StatusTone {
  return STATUS_TONE[status] ?? "pending";
}

const STATUS_LABEL_KEYS: Record<string, string> = {
  Received: "statuses.Received",
  Waiting: "statuses.Waiting",
  Running: "statuses.Running",
  InProgress: "statuses.InProgress",
  ReadyToRead: "statuses.ReadyToRead",
  "Ready to Read": "statuses.ReadyToRead",
  EnterResult: "statuses.EnterResult",
  "Enter Result": "statuses.EnterResult",
  "Ready: Plating": "statuses.ReadyPlating",
  "Ready: Broth": "statuses.ReadyBroth",
  "Ready: Setup": "statuses.ReadySetup",
  "Ready: Confirmatory": "statuses.ReadyConfirmatory",
  "Ready: Subculture": "statuses.ReadySubculture",
  Incubating: "statuses.Incubating",
  ResultEntered: "statuses.ResultEntered",
  Ready: "statuses.Ready",
  Reviewed: "statuses.Reviewed",
  PendingReview: "statuses.PendingReview",
  "Pending Review": "statuses.PendingReview",
  Completed: "statuses.Completed",
  Approved: "statuses.Approved",
  Rejected: "statuses.Rejected",
  RetestRequested: "statuses.RetestRequested",
  Voided: "statuses.Voided",
  OnHold: "statuses.OnHold",
  "On Hold": "statuses.OnHold",
  Active: "statuses.Active",
  Inactive: "statuses.Inactive",
  Frozen: "statuses.Frozen",
  InStock: "statuses.InStock",
  LowStock: "statuses.LowStock",
  Depleted: "statuses.Depleted",
  Expired: "statuses.Expired",
  "Pending Evaluation": "statuses.PendingEvaluation",
  "Awaiting Approval": "statuses.AwaitingApproval",
  Released: "statuses.Released",
  "Out of Stock": "statuses.OutOfStockSpace",
  OutOfStock: "statuses.OutOfStock",
  Overdue: "statuses.Overdue",
  "Due Soon": "statuses.DueSoon",
  CalibrationDueSoon: "statuses.CalibrationDueSoon",
  InService: "statuses.InService",
  OutOfService: "statuses.OutOfService",
  Retired: "statuses.Retired",
  WithinLimits: "statuses.WithinLimits",
  AlertLimitExceeded: "statuses.AlertLimitExceeded",
  ActionLimitExceeded: "statuses.ActionLimitExceeded",
  OutOfSpecification: "statuses.OutOfSpecification",
  WithinLimit: "statuses.WithinLimit",
  AlertLevel: "statuses.AlertLevel",
  ActionLevel: "statuses.ActionLevel",
  NotApplicable: "statuses.NotApplicable",
  LimitsNotConfigured: "statuses.LimitsNotConfigured",
  "Limits Not Configured": "statuses.LimitsNotConfigured",
  Detected: "statuses.Detected",
  Absent: "statuses.Absent",
  DueSoon: "statuses.DueSoon",
  DueToday: "statuses.DueToday",
  DueTomorrow: "statuses.DueTomorrow",
  Passed: "statuses.Passed",
  Failed: "statuses.Failed",
  Pending: "statuses.Pending",
  Returned: "statuses.Returned",
  InTesting: "statuses.InTesting",
  "In Testing": "statuses.InTesting",
  UnderReview: "statuses.UnderReview",
  "Under Review": "statuses.UnderReview",
  UnderApproval: "statuses.UnderApproval",
  "Under Approval": "statuses.UnderApproval",
  Blocked: "statuses.Blocked",
  Draft: "statuses.Draft",
  InReview: "statuses.InReview",
  AwaitingApproval: "statuses.AwaitingApproval",
  FutureEffective: "statuses.FutureEffective",
  "Future Effective": "statuses.FutureEffective",
  Effective: "statuses.Effective",
  Superseded: "statuses.Superseded",
  Obsolete: "statuses.Obsolete",
  Cancelled: "statuses.Cancelled",
  Void: "statuses.Void",
  "No Revision": "statuses.NoRevision",
  Assigned: "statuses.Assigned",
  Reading: "statuses.Reading",
  Acknowledged: "statuses.Acknowledged",
  CompletedPassed: "statuses.CompletedPassed",
  SupersededIncomplete: "statuses.SupersededIncomplete",
  Qualified: "statuses.Qualified",
  TrainedOnSupersededOnly: "statuses.TrainedOnSupersededOnly",
  NotAssigned: "statuses.NotAssigned",
  RemainsValid: "statuses.RemainsValid",
  RevisionRequired: "statuses.RevisionRequired",
  ObsolescenceRecommended: "statuses.ObsolescenceRecommended",
  ReturnedForCorrection: "statuses.ReturnedForCorrection",
  Open: "statuses.Open",
  AuthorResponded: "statuses.AuthorResponded",
  ReviewerVerified: "statuses.ReviewerVerified",
  Resolved: "statuses.Resolved",
  Pass: "statuses.Pass",
  Fail: "statuses.Fail",
  BelowSpec: "statuses.BelowSpec",
  "Below Spec": "statuses.BelowSpec",
  Suitable: "statuses.Suitable",
  NotSuitable: "statuses.NotSuitable",
  "Not Suitable": "statuses.NotSuitable",
  Conforms: "statuses.Conforms",
  DoesNotConform: "statuses.DoesNotConform",
  "Does Not Conform": "statuses.DoesNotConform",
  Prepared: "statuses.Prepared",
  InUse: "statuses.InUse",
  "In Use": "statuses.InUse",
  Discarded: "statuses.Discarded",
  Valid: "statuses.Valid",
  Invalid: "statuses.Invalid",
  Abandoned: "statuses.Abandoned",
  Available: "statuses.Available",
  Unavailable: "statuses.Unavailable",
  Removed: "statuses.Removed",
  RequiresReview: "statuses.RequiresReview",
  Withdrawn: "statuses.Withdrawn",
  ExpiringSoon: "statuses.ExpiringSoon",
  NotQualified: "statuses.NotQualified",
  Signed: "statuses.Signed",
  NoFindings: "statuses.NoFindings",
  FindingsRecorded: "statuses.FindingsRecorded",
  Closed: "statuses.Closed",
  PhaseOne: "statuses.PhaseOne",
  AwaitingRetest: "statuses.AwaitingRetest",
  PhaseTwo: "statuses.PhaseTwo",
  Concluded: "statuses.Concluded",
  Confirmed: "statuses.Confirmed",
  Invalidated: "statuses.Invalidated",
  ExcursionClosed: "statuses.ExcursionClosed",
  Oos: "statuses.Oos",
  Excursion: "statuses.Excursion",
  Addressed: "statuses.Addressed",
  LaboratoryErrorConfirmed: "statuses.LaboratoryErrorConfirmed",
  NoLaboratoryErrorFound: "statuses.NoLaboratoryErrorFound"
};

export function statusLabel(status: string, t?: TFunction): string {
  const key = STATUS_LABEL_KEYS[status];
  if (!key) return status;
  const translate = t ?? i18n.getFixedT("en", "common");
  return translate(key, { ns: "common", defaultValue: status });
}
