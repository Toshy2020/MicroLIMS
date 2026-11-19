import { Box, useTheme } from "@mui/material";
import { Theme } from "@mui/material/styles";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import { useTranslation } from "../i18n";
import { statusLabel, statusTone, StatusTone } from "../theme/statusTokens";

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
  const { t } = useTranslation("common");
  return <Pill tone={statusTone(status)}>{label ?? statusLabel(status, t)}</Pill>;
}

// An explicit tone for statuses that have no entry in the status map
// ("Disabled", "Current", "Legacy-only"): same pill, chosen meaning.
export function ToneBadge({ tone, label }: { tone: StatusTone; label: string }) {
  return <Pill tone={tone}>{label}</Pill>;
}

// Type and configuration facts - a role's type, "System", "Enforced" -
// describe what a record is, not where it stands, so they get a neutral
// outline rather than a status colour. Sharing the green of "Active" or
// "Approved" made a system role and an approved test look alike.
// `locked` adds a lock for records the user cannot change.
export function MetaBadge({ label, locked }: { label: string; locked?: boolean }) {
  return (
    <Box
      component="span"
      sx={{
        display: "inline-flex", alignItems: "center", gap: 0.5, px: 1, py: 0.125, borderRadius: 5,
        fontSize: 12, fontWeight: 600, lineHeight: 1.5, whiteSpace: "nowrap", verticalAlign: "middle",
        color: "text.secondary", bgcolor: "transparent", border: "1px solid", borderColor: "divider"
      }}
    >
      {locked && <LockOutlinedIcon sx={{ fontSize: 13 }} aria-hidden="true" />}
      {label}
    </Box>
  );
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
