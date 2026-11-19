import { useTranslation } from "../../../../i18n";
import { KpiStrip } from "../../../../components/lab";
import type { SummaryTile } from "../../../../components/configHierarchy/SummaryTiles";
import { CryovialItem } from "../types/cryovialTypes";

interface CryovialKpiCardsProps {
  items: CryovialItem[];
  loading?: boolean;
}

function isMaterialExpiringSoon(expiryDateStr: string | null, daysThreshold: number = 30): boolean {
  if (!expiryDateStr) return false;
  const expiry = new Date(expiryDateStr);
  const now = new Date();
  const diffTime = expiry.getTime() - now.getTime();
  const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
  return diffDays <= daysThreshold;
}

export function CryovialKpiCards({ items, loading }: CryovialKpiCardsProps) {
  const { t } = useTranslation("labConfig");
  const now = new Date();

  // 1. Total Batches: Real count of registered cryovial batches
  const totalBatchesCount = items.length;

  // 2. Vials Available: Sum of usable/remaining vials across approved, non-destroyed, unexpired batches
  const vialsAvailableCount = items
    .filter((c) => c.approvalStatus === "Approved" && !c.isDestroyed && new Date(c.expiryDate) > now)
    .reduce((sum, c) => sum + (c.vialsRemaining || 0), 0);

  // 3. Expiring Soon: Real count of approved, non-destroyed batches expiring within 30 days
  const expiringSoonCount = items.filter(
    (c) =>
      c.approvalStatus === "Approved" &&
      !c.isDestroyed &&
      new Date(c.expiryDate) > now &&
      isMaterialExpiringSoon(c.expiryDate, 30)
  ).length;

  // 4. Low Stock: Real count of approved, non-destroyed batches with <= 2 vials remaining
  const lowStockCount = items.filter(
    (c) =>
      c.approvalStatus === "Approved" &&
      !c.isDestroyed &&
      new Date(c.expiryDate) > now &&
      c.vialsRemaining <= 2
  ).length;

  const tiles: SummaryTile[] = [
    { label: t("cryovials.kpis.totalBatches"), value: totalBatchesCount, tone: "purple" },
    { label: t("cryovials.kpis.vialsAvailable"), value: vialsAvailableCount, tone: "notDetected" },
    { label: t("cryovials.kpis.expiringSoon"), value: expiringSoonCount, tone: "action" },
    { label: t("cryovials.kpis.lowStock"), value: lowStockCount, tone: "detected" }
  ];

  return <KpiStrip tiles={tiles} loading={loading} />;
}
