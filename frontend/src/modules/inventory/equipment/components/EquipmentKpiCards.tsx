import { Grid, Paper, Typography, Box } from "@mui/material";
import DevicesOtherOutlinedIcon from "@mui/icons-material/DevicesOtherOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import PauseCircleOutlineIcon from "@mui/icons-material/PauseCircleOutline";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import AlarmOnOutlinedIcon from "@mui/icons-material/AlarmOnOutlined";
import { SvgIconComponent } from "@mui/icons-material";
import { EquipmentItem, EquipmentKpiFilter } from "../types/equipmentTypes";
import { brandColors } from "../../../../theme";

interface EquipmentKpiCardsProps {
  items: EquipmentItem[];
  activeFilter: EquipmentKpiFilter;
  onFilterSelect: (filter: EquipmentKpiFilter) => void;
}

interface KpiCardDef {
  key: EquipmentKpiFilter;
  label: string;
  description: string;
  count: number;
  icon: SvgIconComponent;
  color: string;
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

export function EquipmentKpiCards({ items, activeFilter, onFilterSelect }: EquipmentKpiCardsProps) {
  const totalCount = items.length;
  const inServiceCount = items.filter((e) => e.status === "InService").length;
  const outOfServiceCount = items.filter((e) => e.status === "OutOfService" || e.status === "Retired").length;
  const overdueCount = items.filter((e) => isEquipmentCalibrationOverdue(e)).length;
  const dueSoonCount = items.filter((e) => isEquipmentCalibrationDueSoon(e)).length;

  const cards: KpiCardDef[] = [
    {
      key: "all",
      label: "Total Equipment",
      description: "All registered equipment",
      count: totalCount,
      icon: DevicesOtherOutlinedIcon,
      color: brandColors.sectionTitle
    },
    {
      key: "in_service",
      label: "In Service",
      description: "Operational equipment",
      count: inServiceCount,
      icon: CheckCircleOutlineIcon,
      color: brandColors.ok
    },
    {
      key: "out_of_service",
      label: "Out of Service",
      description: "Unavailable / retired",
      count: outOfServiceCount,
      icon: PauseCircleOutlineIcon,
      color: "#ea580c"
    },
    {
      key: "calibration_overdue",
      label: "Calibration Overdue",
      description: "Calibration date has passed",
      count: overdueCount,
      icon: ErrorOutlineIcon,
      color: "#dc2626"
    },
    {
      key: "calibration_due_soon",
      label: "Calibration Due Soon",
      description: "Due within 30-day window",
      count: dueSoonCount,
      icon: AlarmOnOutlinedIcon,
      color: "#f59e0b"
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
