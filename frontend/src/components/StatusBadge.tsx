import { Box, useTheme } from "@mui/material";
import { Theme } from "@mui/material/styles";
import { statusTone, StatusTone } from "../theme/statusTokens";

const statusLabelMap: Record<string, string> = {
  InProgress: "In Progress",
  ReadyToRead: "Ready to Read",
  EnterResult: "Enter Result",
  "Ready: Plating": "Ready: Plating",
  "Ready: Broth": "Ready: Broth",
  "Ready: Setup": "Ready: Setup",
  "Ready: Confirmatory": "Ready: Confirmatory",
  PendingReview: "Pending Review",
  Reviewed: "Reviewed — Pending Approval",
  ResultEntered: "Result Entered",
  RetestRequested: "Retest Requested",
  OnHold: "On Hold",
  WithinLimits: "Within Limits",
  AlertLimitExceeded: "Alert Limit Exceeded",
  ActionLimitExceeded: "Action Limit Exceeded",
  OutOfSpecification: "Out of Specification",
  WithinLimit: "Within Limit",
  AlertLevel: "Alert Level",
  ActionLevel: "Action Level",
  NotApplicable: "Not Applicable",
  LimitsNotConfigured: "Limits Not Configured",
  DueSoon: "Due Soon",
  DueToday: "Due Today",
  DueTomorrow: "Due Tomorrow",
  Returned: "Returned",
  InTesting: "In Testing",
  UnderReview: "Under Review",
  UnderApproval: "Under Approval",

  // Document Control. The specs mandate the spaced display strings for the
  // three multi-word revision states (ML-DC-FRS-1B-001 §3.3:234,273;
  // CC-DC-R1B-007 §1:37) - without these entries the raw enum leaked to the
  // user as "InReview" / "AwaitingApproval" / "FutureEffective". The
  // single-word states (Draft, Effective, Superseded, Obsolete, Cancelled,
  // Void, Active) already display correctly via the raw-value fallback.
  InReview: "In Review",
  AwaitingApproval: "Awaiting Approval",
  FutureEffective: "Future Effective",
  SupersededIncomplete: "Superseded — Incomplete",
  ReturnedForCorrection: "Returned for Correction",
  AuthorResponded: "Author Responded",
  ReviewerVerified: "Reviewer Verified",
  RemainsValid: "Remains Valid",
  RevisionRequired: "Revision Required",
  ObsolescenceRecommended: "Obsolescence Recommended",

  // Physicochemical outcomes and preparation states.
  BelowSpec: "Below Spec",
  NotSuitable: "Not Suitable",
  DoesNotConform: "Does Not Conform",
  InUse: "In Use"
};

// Shared color lookup so non-badge UI (e.g. the Result Level segmented
// buttons in ReportFilterPanel) can match badge colors exactly instead
// of duplicating the token map. Needs a theme param (not a hook) so it
// can be called from plain functions - pass `useTheme()`'s result.
export function statusColor(status: string, theme: Theme): string {
  return theme.custom.status[statusTone(status)].bg;
}

// One pill shape for every status/category/cause badge in the app, so the
// three badge kinds can never drift apart in size, weight or padding. 12px
// semibold: status is the most important word in a laboratory row, so it is
// not set smaller than the table text around it. Text is always present, so
// status is never communicated by color alone.
function Pill({ tone, children }: { tone: StatusTone; children: string }) {
  const theme = useTheme();
  const tokens = theme.custom.status[tone];
  return (
    <Box
      component="span"
      sx={{
        display: "inline-block", px: 1, py: 0.125, borderRadius: 5,
        fontSize: 12, fontWeight: 600, lineHeight: 1.5, whiteSpace: "nowrap", verticalAlign: "middle",
        color: tokens.text, bgcolor: tokens.bg, border: `1px solid ${tokens.border}`
      }}
    >
      {children}
    </Box>
  );
}

// .type-badge pill from the design, driven off a status string. `label`
// overrides the display text while `status` still drives the color
// lookup - for cases like TaskUrgency where the raw enum value ("DueSoon")
// isn't fit to show a user, but should keep its own color. Background,
// text and border all come from theme.custom.status[tone] so light/dark
// values are dedicated tokens, not one color faded for the other mode.
export function StatusBadge({ status, label }: { status: string; label?: string }) {
  return <Pill tone={statusTone(status)}>{label ?? statusLabelMap[status] ?? status}</Pill>;
}

// .cause-badge pill from the design (purple tone).
export function CauseBadge({ label }: { label: string }) {
  return <Pill tone="purple">{label}</Pill>;
}

// .badge-RM / badge-Product / badge-PM from the design. Backend sends
// the full SampleCategory enum name; map it down to the mockup's
// short codes (falls back to the raw category for Water/EM/etc).
const categoryDisplayMap: Record<string, string> = {
  RawMaterial: "RM",
  FinishedProduct: "Product",
  PackagingMaterial: "PM",
  EnvironmentalMonitoring: "EM",
  AfterCleaning: "AC"
};

const categoryToneMap: Record<string, StatusTone> = {
  RawMaterial: "info",
  FinishedProduct: "notDetected",
  PackagingMaterial: "action",
  EnvironmentalMonitoring: "purple",
  Water: "inconclusive",
  AfterCleaning: "detected",
  GPT: "pending",
  ReferenceStrain: "pale"
};

export function categoryLabel(category: string): string {
  return categoryDisplayMap[category] ?? category;
}

export function CategoryBadge({ category }: { category: string }) {
  return <Pill tone={categoryToneMap[category] ?? "pending"}>{categoryLabel(category)}</Pill>;
}
