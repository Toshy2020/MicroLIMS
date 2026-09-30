import { StatusBadge } from "../../../components/StatusBadge";

export interface HplcStatusBadgeProps {
  status: string;
  label?: string;
  // Kept for call-site compatibility; StatusBadge has a single size.
  size?: "small" | "medium";
  variant?: "filled" | "outlined";
}

// Raw API value (case-insensitive) -> canonical STATUS_TONE key + display label.
const STATUS_MAP: Record<string, { key: string; label: string }> = {
  open: { key: "Open", label: "Open" },
  completed: { key: "Completed", label: "Completed" },
  abandoned: { key: "Abandoned", label: "Abandoned" },
  pending: { key: "Pending", label: "Pending" },
  passed: { key: "Passed", label: "Passed" },
  failed: { key: "Failed", label: "Failed" },
  available: { key: "Available", label: "Available" },
  running: { key: "Running", label: "Running" },
  unavailable: { key: "Unavailable", label: "Unavailable" },
  assigned: { key: "Assigned", label: "Assigned" },
  removed: { key: "Removed", label: "Removed" },
  withinlimits: { key: "WithinLimits", label: "Within Limits" },
  outofspecification: { key: "OutOfSpecification", label: "OOS" },
  actionlimitexceeded: { key: "ActionLimitExceeded", label: "Action Limit Exceeded" },
  alertlimitexceeded: { key: "AlertLimitExceeded", label: "Alert Limit Exceeded" },
  requiresreview: { key: "RequiresReview", label: "Requires Review" }
};

export function HplcStatusBadge({ status, label }: HplcStatusBadgeProps) {
  const mapped = STATUS_MAP[status.trim().toLowerCase()];
  if (!mapped) return <StatusBadge status={status} label={label} />;
  return <StatusBadge status={mapped.key} label={label ?? mapped.label} />;
}
