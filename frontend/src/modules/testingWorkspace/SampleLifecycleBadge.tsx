import { Box, useTheme } from "@mui/material";
import PrintIcon from "@mui/icons-material/Print";
import { StatusTone } from "../../theme/statusTokens";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";

const LABELS: Record<string, string> = {
  InTesting: "Under Testing",
  UnderReview: "Under Review",
  UnderApproval: "Under Approval",
  Approved: "Approved",
  Rejected: "Rejected"
};

const TONES: Record<string, StatusTone> = {
  InTesting: "info",
  UnderReview: "purple",
  UnderApproval: "action",
  Approved: "notDetected",
  Rejected: "detected"
};

// Which permission lets the user click each status to open the Sample
// Summary dialog and act on it - the same code the review or approval
// endpoint checks - Approved has no entry restriction because its
// print/report view is meant to be viewable by anyone, not just the
// approver. Statuses with no entry here (or not in LABELS at all, e.g.
// "Received") render non-clickable or not at all.
const CLICKABLE_PERMISSION: Partial<Record<string, string>> = {
  UnderReview: PERMISSIONS.SAMPLES_REVIEW,
  UnderApproval: PERMISSIONS.SAMPLES_APPROVE
};
const ALWAYS_CLICKABLE = new Set(["Approved"]);

interface Props {
  status: string;
  onClick?: () => void;
  // false on Sample Receiving's list - that page is a view-only surface,
  // never a place to act on a review/approval decision.
  interactive?: boolean;
}

// Lifecycle badge shown on each Sample's row in the Testing Workspace
// (and read-only in Sample Receiving) - separate from the per-test
// status chips, this reflects where the whole Sample sits in
// Review/Approval. Clicking it (where the user holds the permission to act on it)
// opens SampleSummaryDialog.
export function SampleLifecycleBadge({ status, onClick, interactive = true }: Props) {
  const theme = useTheme();
  const { permissions } = useAuth();
  const label = LABELS[status];
  if (!label) return null;

  const tokens = theme.custom.status[TONES[status] ?? "pending"];
  const permission = CLICKABLE_PERMISSION[status];
  const clickable = interactive && !!onClick && (ALWAYS_CLICKABLE.has(status) || (!!permission && permissions.includes(permission)));

  return (
    <Box
      component="span"
      role={clickable ? "button" : undefined}
      tabIndex={clickable ? 0 : undefined}
      data-no-row-click={clickable ? "true" : undefined}
      onClick={
        clickable
          ? (e) => {
              e.stopPropagation();
              onClick();
            }
          : undefined
      }
      onKeyDown={
        clickable
          ? (e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                e.stopPropagation();
                onClick();
              }
            }
          : undefined
      }
      sx={{
        display: "inline-flex", alignItems: "center", gap: 0.5,
        px: 1, py: 0.25, borderRadius: 5, fontSize: 11, fontWeight: 700,
        color: tokens.text, bgcolor: tokens.bg, border: `1px solid ${tokens.border}`,
        cursor: clickable ? "pointer" : "default",
        "&:hover": clickable ? { opacity: 0.85 } : undefined
      }}
    >
      {label}
      {status === "Approved" && <PrintIcon sx={{ fontSize: 13 }} />}
    </Box>
  );
}
