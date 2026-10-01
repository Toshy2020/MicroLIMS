import { KpiStrip } from "../../../../components/lab";
import type { SummaryTile } from "../../../../components/configHierarchy/SummaryTiles";

export type MediaKpiFilterKey =
  | "ALL"
  | "Pending Evaluation"
  | "Awaiting Approval"
  | "Released"
  | "Rejected"
  | "Out of Stock";

interface Props {
  lots: any[];
  awaitingApprovalIds: Set<number>;
  activeKpi: MediaKpiFilterKey | null;
  onSelectKpi: (kpi: MediaKpiFilterKey) => void;
  loading?: boolean;
}

export function lifecycleOf(lot: any, awaitingApprovalIds: Set<number>): string {
  if (lot.status === "OutOfStock") return "Out of Stock";
  if (lot.isReleasedForUse) return "Released";
  if (lot.approvalStatus === "Rejected" || lot.status === "QuarantineFailed") return "Rejected";
  if (awaitingApprovalIds.has(lot.id)) return "Awaiting Approval";
  return "Pending Evaluation";
}

export function MediaLotKpiCards({ lots, awaitingApprovalIds, activeKpi, onSelectKpi, loading }: Props) {
  const totalCount = lots.length;

  let pendingCount = 0;
  let awaitingApprovalCount = 0;
  let releasedCount = 0;
  let rejectedCount = 0;
  let outOfStockCount = 0;

  for (const lot of lots) {
    const lifecycle = lifecycleOf(lot, awaitingApprovalIds);
    if (lifecycle === "Out of Stock") outOfStockCount++;
    else if (lifecycle === "Released") releasedCount++;
    else if (lifecycle === "Rejected") rejectedCount++;
    else if (lifecycle === "Awaiting Approval") awaitingApprovalCount++;
    else pendingCount++;
  }

  // The active tile is marked "(filtered)"; clicking it again clears the shortcut.
  const tile = (key: MediaKpiFilterKey, label: string, value: number, tone: SummaryTile["tone"]): SummaryTile => ({
    label: activeKpi === key && key !== "ALL" ? `${label} (filtered)` : label,
    value,
    tone,
    onClick: () => onSelectKpi(key)
  });

  const tiles: SummaryTile[] = [
    tile("ALL", "Total Lots", totalCount, "purple"),
    tile("Pending Evaluation", "Pending Evaluation", pendingCount, "pending"),
    tile("Awaiting Approval", "Awaiting Approval", awaitingApprovalCount, "action"),
    tile("Released", "Released", releasedCount, "notDetected"),
    tile("Rejected", "Rejected", rejectedCount, "detected"),
    tile("Out of Stock", "Out of Stock", outOfStockCount, "pending")
  ];

  return <KpiStrip tiles={tiles} loading={loading} />;
}
