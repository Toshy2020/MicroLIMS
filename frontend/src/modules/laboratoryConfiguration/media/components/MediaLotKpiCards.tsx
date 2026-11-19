import { KpiStrip } from "../../../../components/lab";
import type { SummaryTile } from "../../../../components/configHierarchy/SummaryTiles";
import { useTranslation } from "../../../../i18n";

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
  const { t } = useTranslation("labConfig");
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
    label: activeKpi === key && key !== "ALL" ? t("media.kpis.filteredSuffix", { label }) : label,
    value,
    tone,
    onClick: () => onSelectKpi(key)
  });

  const tiles: SummaryTile[] = [
    tile("ALL", t("media.kpis.totalLots"), totalCount, "purple"),
    tile("Pending Evaluation", t("media.lotFilterBar.statuses.Pending Evaluation"), pendingCount, "pending"),
    tile("Awaiting Approval", t("media.lotFilterBar.statuses.Awaiting Approval"), awaitingApprovalCount, "action"),
    tile("Released", t("media.lotFilterBar.statuses.Released"), releasedCount, "notDetected"),
    tile("Rejected", t("media.lotFilterBar.statuses.Rejected"), rejectedCount, "detected"),
    tile("Out of Stock", t("media.lotFilterBar.statuses.Out of Stock"), outOfStockCount, "pending")
  ];

  return <KpiStrip tiles={tiles} loading={loading} />;
}
