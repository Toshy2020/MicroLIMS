import { StatusBadge } from "../../../components/StatusBadge";
import { useTranslation } from "../../../i18n";

export interface HplcStatusBadgeProps {
  status: string;
  label?: string;
  // Kept for call-site compatibility; StatusBadge has a single size.
  size?: "small" | "medium";
  variant?: "filled" | "outlined";
}

// Raw API value (case-insensitive) -> canonical STATUS_TONE key + i18n label key.
const STATUS_MAP: Record<string, { key: string; labelKey: string }> = {
  // A run in progress, not an issue to act on - shares the InProgress (info)
  // tone rather than STATUS_TONE.Open, which is the action tone for findings.
  open: { key: "InProgress", labelKey: "status.open" },
  completed: { key: "Completed", labelKey: "status.completed" },
  abandoned: { key: "Abandoned", labelKey: "status.abandoned" },
  pending: { key: "Pending", labelKey: "status.pending" },
  passed: { key: "Passed", labelKey: "status.passed" },
  failed: { key: "Failed", labelKey: "status.failed" },
  available: { key: "Available", labelKey: "status.available" },
  running: { key: "Running", labelKey: "status.running" },
  unavailable: { key: "Unavailable", labelKey: "status.unavailable" },
  assigned: { key: "Assigned", labelKey: "status.assigned" },
  removed: { key: "Removed", labelKey: "status.removed" },
  withinlimits: { key: "WithinLimits", labelKey: "status.withinLimits" },
  outofspecification: { key: "OutOfSpecification", labelKey: "status.oos" },
  actionlimitexceeded: { key: "ActionLimitExceeded", labelKey: "status.actionLimitExceeded" },
  alertlimitexceeded: { key: "AlertLimitExceeded", labelKey: "status.alertLimitExceeded" },
  requiresreview: { key: "RequiresReview", labelKey: "status.requiresReview" }
};

export function HplcStatusBadge({ status, label }: HplcStatusBadgeProps) {
  const { t } = useTranslation("hplcWorkspace");
  const mapped = STATUS_MAP[status.trim().toLowerCase()];
  if (!mapped) return <StatusBadge status={status} label={label} />;
  return <StatusBadge status={mapped.key} label={label ?? t(mapped.labelKey)} />;
}
