import { Box, useTheme } from "@mui/material";
import { Theme } from "@mui/material/styles";
import { statusTone, StatusTone } from "../theme/statusTokens";

const statusLabelMap: Record<string, string> = {
  InProgress: "In Progress",
  ReadyToRead: "Ready to Read",
  EnterResult: "Enter Result",
  PendingReview: "Pending Review",
  ResultEntered: "Result Entered",
  RetestRequested: "Retest Requested",
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
  Returned: "Returned"
};

// Shared color lookup so non-badge UI (e.g. the Result Level segmented
// buttons in ReportFilterPanel) can match badge colors exactly instead
// of duplicating the token map. Needs a theme param (not a hook) so it
// can be called from plain functions - pass `useTheme()`'s result.
export function statusColor(status: string, theme: Theme): string {
  return theme.custom.status[statusTone(status)].bg;
}

// .type-badge pill from the design, driven off a status string. `label`
// overrides the display text while `status` still drives the color
// lookup - for cases like TaskUrgency where the raw enum value ("DueSoon")
// isn't fit to show a user, but should keep its own color. Background,
// text and border all come from theme.custom.status[tone] so light/dark
// values are dedicated tokens, not one color faded for the other mode.
export function StatusBadge({ status, label }: { status: string; label?: string }) {
  const theme = useTheme();
  const tokens = theme.custom.status[statusTone(status)];
  const text = label ?? statusLabelMap[status] ?? status;
  return (
    <Box
      component="span"
      sx={{
        display: "inline-block", px: 1, py: 0.25, borderRadius: 5, fontSize: 11, fontWeight: 700,
        color: tokens.text, bgcolor: tokens.bg, border: `1px solid ${tokens.border}`
      }}
    >
      {text}
    </Box>
  );
}

// .cause-badge pill from the design (purple tone).
export function CauseBadge({ label }: { label: string }) {
  const theme = useTheme();
  const tokens = theme.custom.status.purple;
  return (
    <Box
      component="span"
      sx={{
        display: "inline-block",
        px: 1,
        py: 0.25,
        borderRadius: 5,
        fontSize: 11,
        fontWeight: 700,
        bgcolor: tokens.bg,
        color: tokens.text,
        border: `1px solid ${tokens.border}`
      }}
    >
      {label}
    </Box>
  );
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

function categoryTone(category: string): StatusTone {
  return categoryToneMap[category] ?? "pending";
}

export function categoryLabel(category: string): string {
  return categoryDisplayMap[category] ?? category;
}

export function CategoryBadge({ category }: { category: string }) {
  const theme = useTheme();
  const tone = categoryTone(category);
  const tokens = theme.custom.status[tone];
  const label = categoryDisplayMap[category] ?? category;
  return (
    <Box
      component="span"
      sx={{
        display: "inline-block",
        px: 1,
        py: 0.25,
        borderRadius: 5,
        fontSize: 11,
        fontWeight: 700,
        color: tokens.text,
        bgcolor: tokens.bg,
        border: `1px solid ${tokens.border}`
      }}
    >
      {label}
    </Box>
  );
}
