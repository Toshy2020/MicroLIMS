import { KpiStrip } from "../../../../components/lab";
import type { SummaryTile } from "../../../../components/configHierarchy/SummaryTiles";
import { MaterialItem, MaterialKpiFilter } from "../types/materialTypes";

interface MaterialKpiCardsProps {
  items: MaterialItem[];
  activeFilter: MaterialKpiFilter;
  onFilterSelect: (filter: MaterialKpiFilter) => void;
  loading?: boolean;
}

export function isMaterialExpiringSoon(expiryDateStr: string | null, daysThreshold: number = 30): boolean {
  if (!expiryDateStr) return false;
  const expiry = new Date(expiryDateStr);
  const now = new Date();
  const diffTime = expiry.getTime() - now.getTime();
  const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
  return diffDays <= daysThreshold;
}

export function isMaterialLowStock(item: MaterialItem): boolean {
  if (item.status === "Expired" || item.status === "Depleted") return false;
  if (item.quantityRemaining <= 0) return false;
  if (item.minimumStockLevel != null && item.minimumStockLevel > 0) {
    return item.quantityRemaining <= item.minimumStockLevel;
  }
  return false;
}

export function isMaterialOutOfStock(item: MaterialItem): boolean {
  return item.quantityRemaining <= 0 || item.status === "Depleted" || item.status === "Expired";
}

export function isMaterialInStock(item: MaterialItem): boolean {
  if (item.status === "Expired" || item.status === "Depleted") return false;
  if (item.quantityRemaining <= 0) return false;
  if (item.minimumStockLevel != null && item.minimumStockLevel > 0) {
    return item.quantityRemaining > item.minimumStockLevel;
  }
  return true;
}

export function MaterialKpiCards({ items, activeFilter, onFilterSelect, loading }: MaterialKpiCardsProps) {
  // Clicking the active tile again clears the shortcut (except "all").
  const pick = (key: MaterialKpiFilter) => () => onFilterSelect(activeFilter === key && key !== "all" ? "all" : key);
  const label = (text: string, key: MaterialKpiFilter) => (activeFilter === key && key !== "all" ? `${text} (filtered)` : text);

  const tiles: SummaryTile[] = [
    { label: label("Total Items", "all"), value: items.length, tone: "purple", onClick: pick("all") },
    { label: label("In Stock", "in_stock"), value: items.filter(isMaterialInStock).length, tone: "notDetected", onClick: pick("in_stock") },
    { label: label("Low Stock", "low_stock"), value: items.filter(isMaterialLowStock).length, tone: "inconclusive", onClick: pick("low_stock") },
    { label: label("Out of Stock", "out_of_stock"), value: items.filter(isMaterialOutOfStock).length, tone: "detected", onClick: pick("out_of_stock") },
    { label: label("Expiring Soon", "expiring_soon"), value: items.filter((m) => isMaterialExpiringSoon(m.expiryDate)).length, tone: "action", onClick: pick("expiring_soon") }
  ];

  return <KpiStrip tiles={tiles} loading={loading} />;
}
