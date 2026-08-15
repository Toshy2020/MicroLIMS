import { Box } from "@mui/material";
import { brandColors } from "../theme";

const statusColorMap: Record<string, string> = {
  Received: "#9ca3af",
  Waiting: "#9ca3af",
  Running: brandColors.badgeRM,
  InProgress: brandColors.badgeRM,
  Incubating: brandColors.badgePM,
  ResultEntered: brandColors.badgeRM,
  Ready: brandColors.badgeRM,
  Reviewed: brandColors.badgePM,
  Approved: brandColors.badgeProduct,
  Rejected: "#dc2626",
  RetestRequested: brandColors.badgePM,
  Active: brandColors.badgeProduct,
  Inactive: "#9ca3af",
  Frozen: "#9ca3af",
  InStock: brandColors.badgeProduct,
  LowStock: "#f59e0b",
  Depleted: brandColors.badgePM,
  Expired: "#dc2626",
  // Media lot lifecycle - a Conform evaluation only qualifies a lot;
  // "Awaiting Approval" is the gap before a Section Head signs for release.
  "Pending Evaluation": "#9ca3af",
  "Awaiting Approval": brandColors.badgePM,
  Released: brandColors.badgeProduct,
  Quarantined: "#dc2626",
  PendingReview: "#9ca3af",
  Overdue: "#dc2626",
  "Due Soon": "#f59e0b",
  CalibrationDueSoon: "#f59e0b",
  InService: brandColors.badgeProduct,
  OutOfService: brandColors.badgePM,
  Retired: "#9ca3af",
  // EM/After Cleaning batch location results (SampleLocation.Status) -
  // Spec -> Action -> Alert severity, most severe reddest.
  WithinLimits: "#16a34a",
  AlertLimitExceeded: "#f59e0b",
  ActionLimitExceeded: "#ea580c",
  OutOfSpecification: "#dc2626",
  // ResultRecord.ResultLevel (Reports module) - same Spec -> Action ->
  // Alert severity scale as SampleLocation.Status above, just named
  // differently on this enum.
  WithinLimit: "#16a34a",
  AlertLevel: "#f59e0b",
  ActionLevel: "#ea580c",
  NotApplicable: "#9ca3af",
  // EM/After Cleaning batch pathogen results per location.
  Detected: "#dc2626",
  Absent: "#16a34a",
  // My Tasks urgency (TaskUrgency enum, dashboard) - Overdue already
  // mapped above.
  DueSoon: "#ea580c",
  DueToday: brandColors.badgePM,
  DueTomorrow: "#16a34a",
  // Media Expiry evaluation status.
  Passed: brandColors.badgeProduct,
  Failed: "#dc2626",
  Pending: "#9ca3af"
};

// Shared color lookup so non-badge UI (e.g. the Result Level segmented
// buttons in ReportFilterPanel) can match badge colors exactly instead
// of duplicating the hex map.
export function statusColor(status: string): string {
  return statusColorMap[status] ?? "#6b7280";
}

// .type-badge pill from the design, driven off a status string. `label`
// overrides the display text while `status` still drives the color
// lookup - for cases like TaskUrgency where the raw enum value ("DueSoon")
// isn't fit to show a user, but should keep its own color.
export function StatusBadge({ status, label }: { status: string; label?: string }) {
  const bg = statusColor(status);
  return (
    <Box component="span" sx={{ display: "inline-block", px: 1, py: 0.25, borderRadius: 5, fontSize: 11, fontWeight: 700, color: "#fff", bgcolor: bg }}>
      {label ?? status}
    </Box>
  );
}

// .cause-badge pill from the design (light purple background).
export function CauseBadge({ label }: { label: string }) {
  return (
    <Box component="span" sx={{ display: "inline-block", px: 1, py: 0.25, borderRadius: 5, fontSize: 11, fontWeight: 700, bgcolor: brandColors.causeBadgeBg, color: brandColors.causeBadgeText }}>
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
const categoryColorMap: Record<string, string> = {
  RawMaterial: brandColors.badgeRM,
  FinishedProduct: brandColors.badgeProduct,
  PackagingMaterial: brandColors.badgePM,
  // Reports module surfaces every SampleCategory (not just Product/RM/PM),
  // so Water/EM/After Cleaning/GPT need their own colors too.
  Water: "#0891b2",
  EnvironmentalMonitoring: "#7c3aed",
  AfterCleaning: "#be185d",
  GPT: "#64748b"
};

export function categoryLabel(category: string): string {
  return categoryDisplayMap[category] ?? category;
}

export function categoryColor(category: string): string {
  return categoryColorMap[category] ?? "#6b7280";
}

export function CategoryBadge({ category }: { category: string }) {
  const label = categoryDisplayMap[category] ?? category;
  const bg = categoryColorMap[category] ?? "#6b7280";
  return (
    <Box component="span" sx={{ display: "inline-block", px: 1, py: 0.25, borderRadius: 5, fontSize: 11, fontWeight: 700, color: "#fff", bgcolor: bg }}>
      {label}
    </Box>
  );
}
