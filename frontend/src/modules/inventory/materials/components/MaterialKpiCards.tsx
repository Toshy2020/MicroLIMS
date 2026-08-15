import { Grid, Paper, Typography, Box } from "@mui/material";
import Inventory2OutlinedIcon from "@mui/icons-material/Inventory2Outlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import HourglassBottomOutlinedIcon from "@mui/icons-material/HourglassBottomOutlined";
import { SvgIconComponent } from "@mui/icons-material";
import { MaterialItem, MaterialKpiFilter } from "../types/materialTypes";
import { brandColors } from "../../../../theme";

interface MaterialKpiCardsProps {
  items: MaterialItem[];
  activeFilter: MaterialKpiFilter;
  onFilterSelect: (filter: MaterialKpiFilter) => void;
}

interface KpiCardDef {
  key: MaterialKpiFilter;
  label: string;
  description: string;
  count: number;
  icon: SvgIconComponent;
  color: string;
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

export function MaterialKpiCards({ items, activeFilter, onFilterSelect }: MaterialKpiCardsProps) {
  const totalCount = items.length;
  const inStockCount = items.filter(isMaterialInStock).length;
  const lowStockCount = items.filter(isMaterialLowStock).length;
  const outOfStockCount = items.filter(isMaterialOutOfStock).length;
  const expiringSoonCount = items.filter((m) => isMaterialExpiringSoon(m.expiryDate)).length;

  const cards: KpiCardDef[] = [
    {
      key: "all",
      label: "Total Items",
      description: "All material stock records",
      count: totalCount,
      icon: Inventory2OutlinedIcon,
      color: brandColors.sectionTitle
    },
    {
      key: "in_stock",
      label: "In Stock",
      description: "Sufficient quantity available",
      count: inStockCount,
      icon: CheckCircleOutlineIcon,
      color: brandColors.ok
    },
    {
      key: "low_stock",
      label: "Low Stock",
      description: "Below minimum stock level",
      count: lowStockCount,
      icon: WarningAmberOutlinedIcon,
      color: "#f59e0b"
    },
    {
      key: "out_of_stock",
      label: "Out of Stock",
      description: "No usable quantity remaining",
      count: outOfStockCount,
      icon: ErrorOutlineIcon,
      color: "#dc2626"
    },
    {
      key: "expiring_soon",
      label: "Expiring Soon",
      description: "Within 30-day warning period",
      count: expiringSoonCount,
      icon: HourglassBottomOutlinedIcon,
      color: "#ea580c"
    }
  ];

  return (
    <Grid container spacing={1.5} sx={{ mb: 2 }}>
      {cards.map((card) => {
        const isActive = activeFilter === card.key;
        return (
          <Grid item xs={12} sm={6} md={2.4} key={card.key}>
            <Paper
              onClick={() => onFilterSelect(isActive && card.key !== "all" ? "all" : card.key)}
              sx={{
                p: 1.75,
                cursor: "pointer",
                transition: "all 0.15s ease-in-out",
                border: isActive ? `2px solid ${card.color}` : "1px solid #e5e7eb",
                bgcolor: isActive ? `${card.color}0a` : "#ffffff",
                boxShadow: isActive ? `0 2px 8px ${card.color}26` : "0 1px 3px rgba(0,0,0,0.05)",
                "&:hover": {
                  boxShadow: `0 4px 12px ${card.color}22`,
                  borderColor: card.color,
                  transform: "translateY(-1px)"
                }
              }}
            >
              <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 0.75 }}>
                <Box
                  sx={{
                    width: 32,
                    height: 32,
                    borderRadius: 1.5,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    bgcolor: `${card.color}18`,
                    color: card.color
                  }}
                >
                  <card.icon sx={{ fontSize: 18 }} />
                </Box>
                <Typography
                  sx={{
                    fontSize: 22,
                    fontWeight: 700,
                    color: card.color,
                    lineHeight: 1
                  }}
                >
                  {card.count}
                </Typography>
              </Box>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: "#1f2937", lineHeight: 1.2 }} noWrap>
                {card.label}
              </Typography>
              <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 0.25 }} noWrap>
                {card.description}
              </Typography>
            </Paper>
          </Grid>
        );
      })}
    </Grid>
  );
}
