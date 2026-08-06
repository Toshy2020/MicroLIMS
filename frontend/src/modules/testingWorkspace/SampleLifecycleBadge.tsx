import { Box } from "@mui/material";
import PrintIcon from "@mui/icons-material/Print";
import { brandColors } from "../../theme";
import { Role } from "../../contexts/AuthContext";

const LABELS: Record<string, string> = {
  InTesting: "Under Testing",
  UnderReview: "Under Review",
  UnderApproval: "Under Approval",
  Approved: "Approved",
  Rejected: "Rejected"
};

const COLORS: Record<string, string> = {
  InTesting: brandColors.badgeRM,
  UnderReview: brandColors.pageTitle,
  UnderApproval: brandColors.badgePM,
  Approved: brandColors.badgeProduct,
  Rejected: brandColors.err
};

// Which roles may click each status to open the Sample Summary dialog
// and act on it - Approved has no entry restriction because its
// print/report view is meant to be viewable by anyone, not just the
// approver. Statuses with no entry here (or not in LABELS at all, e.g.
// "Received") render non-clickable or not at all.
const CLICKABLE_ROLES: Partial<Record<string, Role[]>> = {
  UnderReview: ["Reviewer", "SectionHead", "SystemAdministrator"],
  UnderApproval: ["SectionHead", "SystemAdministrator"]
};
const ALWAYS_CLICKABLE = new Set(["Approved"]);

interface Props {
  status: string;
  role: Role | null;
  onClick?: () => void;
  // false on Sample Receiving's list - that page is a view-only surface,
  // never a place to act on a review/approval decision.
  interactive?: boolean;
}

// Lifecycle badge shown on each Sample's row in the Testing Workspace
// (and read-only in Sample Receiving) - separate from the per-test
// status chips, this reflects where the whole Sample sits in
// Review/Approval. Clicking it (where clickable for the current role)
// opens SampleSummaryDialog.
export function SampleLifecycleBadge({ status, role, onClick, interactive = true }: Props) {
  const label = LABELS[status];
  if (!label) return null;

  const bg = COLORS[status] ?? "#6b7280";
  const allowedRoles = CLICKABLE_ROLES[status];
  const clickable = interactive && !!onClick && (ALWAYS_CLICKABLE.has(status) || (!!allowedRoles && !!role && allowedRoles.includes(role)));

  return (
    <Box
      component="span"
      onClick={clickable ? onClick : undefined}
      sx={{
        display: "inline-flex", alignItems: "center", gap: 0.5,
        px: 1, py: 0.25, borderRadius: 5, fontSize: 11, fontWeight: 700, color: "#fff", bgcolor: bg,
        cursor: clickable ? "pointer" : "default",
        "&:hover": clickable ? { opacity: 0.85 } : undefined
      }}
    >
      {label}
      {status === "Approved" && <PrintIcon sx={{ fontSize: 13 }} />}
    </Box>
  );
}
