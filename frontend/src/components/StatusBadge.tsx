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
  InStock: brandColors.badgeProduct,
  Depleted: brandColors.badgePM,
  Expired: "#dc2626",
  Overdue: "#dc2626",
  InService: brandColors.badgeProduct,
  OutOfService: brandColors.badgePM,
  Retired: "#9ca3af"
};

// .type-badge pill from the design, driven off a status string.
export function StatusBadge({ status }: { status: string }) {
  const bg = statusColorMap[status] ?? "#6b7280";
  return (
    <Box component="span" sx={{ display: "inline-block", px: 1, py: 0.25, borderRadius: 5, fontSize: 11, fontWeight: 700, color: "#fff", bgcolor: bg }}>
      {status}
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
  PackagingMaterial: "PM"
};
const categoryColorMap: Record<string, string> = {
  RawMaterial: brandColors.badgeRM,
  FinishedProduct: brandColors.badgeProduct,
  PackagingMaterial: brandColors.badgePM
};

export function CategoryBadge({ category }: { category: string }) {
  const label = categoryDisplayMap[category] ?? category;
  const bg = categoryColorMap[category] ?? "#6b7280";
  return (
    <Box component="span" sx={{ display: "inline-block", px: 1, py: 0.25, borderRadius: 5, fontSize: 11, fontWeight: 700, color: "#fff", bgcolor: bg }}>
      {label}
    </Box>
  );
}
