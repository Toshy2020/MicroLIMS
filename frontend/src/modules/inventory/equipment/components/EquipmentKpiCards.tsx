import { KpiStrip } from "../../../../components/lab";
import type { SummaryTile } from "../../../../components/configHierarchy/SummaryTiles";
import { useTranslation } from "../../../../i18n";
import { EquipmentItem, EquipmentKpiFilter } from "../types/equipmentTypes";

interface EquipmentKpiCardsProps {
  items: EquipmentItem[];
  activeFilter: EquipmentKpiFilter;
  onFilterSelect: (filter: EquipmentKpiFilter) => void;
  loading?: boolean;
}

export function isEquipmentCalibrationOverdue(item: EquipmentItem): boolean {
  if (item.isCalibrationOverdue) return true;
  if (!item.calibrationDueDate) return false;
  const todayStr = new Date().toISOString().slice(0, 10);
  const dueStr = item.calibrationDueDate.slice(0, 10);
  return dueStr < todayStr;
}

export function isEquipmentCalibrationDueSoon(item: EquipmentItem, daysThreshold: number = 30): boolean {
  if (isEquipmentCalibrationOverdue(item)) return false;
  if (!item.calibrationDueDate) return false;
  const due = new Date(item.calibrationDueDate.slice(0, 10));
  const now = new Date(new Date().toISOString().slice(0, 10));
  const diffTime = due.getTime() - now.getTime();
  const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
  return diffDays >= 0 && diffDays <= daysThreshold;
}

export function EquipmentKpiCards({ items, activeFilter, onFilterSelect, loading }: EquipmentKpiCardsProps) {
  const { t } = useTranslation("inventory");
  // Clicking the active tile again clears the shortcut (except "all").
  const pick = (key: EquipmentKpiFilter) => () => onFilterSelect(activeFilter === key && key !== "all" ? "all" : key);
  const label = (text: string, key: EquipmentKpiFilter) =>
    activeFilter === key && key !== "all" ? t("equipment.kpi.filtered", { label: text }) : text;

  const tiles: SummaryTile[] = [
    { label: label(t("equipment.kpi.total"), "all"), value: items.length, tone: "purple", onClick: pick("all") },
    { label: label(t("equipment.kpi.inService"), "in_service"), value: items.filter((e) => e.status === "InService").length, tone: "notDetected", onClick: pick("in_service") },
    { label: label(t("equipment.kpi.outOfService"), "out_of_service"), value: items.filter((e) => e.status === "OutOfService" || e.status === "Retired").length, tone: "action", onClick: pick("out_of_service") },
    { label: label(t("equipment.kpi.calibrationOverdue"), "calibration_overdue"), value: items.filter((e) => isEquipmentCalibrationOverdue(e)).length, tone: "detected", onClick: pick("calibration_overdue") },
    { label: label(t("equipment.kpi.calibrationDueSoon"), "calibration_due_soon"), value: items.filter((e) => isEquipmentCalibrationDueSoon(e)).length, tone: "inconclusive", onClick: pick("calibration_due_soon") }
  ];

  return <KpiStrip tiles={tiles} loading={loading} />;
}
