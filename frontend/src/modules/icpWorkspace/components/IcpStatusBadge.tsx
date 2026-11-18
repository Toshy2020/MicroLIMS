import { StatusBadge } from "../../../components/StatusBadge";
import { useTranslation } from "../../../i18n";

export interface IcpStatusBadgeProps {
  status: string;
  label?: string;
  size?: "small" | "medium";
  variant?: "filled" | "outlined";
}

const STATUS_MAP: Record<string, { key: string; labelKey: string }> = {
  open: { key: "InProgress", labelKey: "status.open" },
  completed: { key: "Completed", labelKey: "status.completed" },
  abandoned: { key: "Abandoned", labelKey: "status.abandoned" },
  pending: { key: "Pending", labelKey: "status.pending" },
  confirmed: { key: "Completed", labelKey: "status.confirmed" },
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

export function IcpStatusBadge({ status, label }: IcpStatusBadgeProps) {
  const { t } = useTranslation("icpWorkspace");
  const mapped = STATUS_MAP[status.trim().toLowerCase()];
  if (!mapped) return <StatusBadge status={status} label={label} />;
  return <StatusBadge status={mapped.key} label={label ?? t(mapped.labelKey)} />;
}
