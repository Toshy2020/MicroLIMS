import { Grid, Paper, Typography, Box, useTheme } from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import { SvgIconComponent } from "@mui/icons-material";
import { CryovialItem } from "../types/cryovialTypes";
import { StatusTone } from "../../../../theme/statusTokens";

interface CryovialKpiCardsProps {
  items: CryovialItem[];
}

interface KpiCardDef {
  label: string;
  description: string;
  count: number;
  icon: SvgIconComponent;
  tone: StatusTone;
}

function isMaterialExpiringSoon(expiryDateStr: string | null, daysThreshold: number = 30): boolean {
  if (!expiryDateStr) return false;
  const expiry = new Date(expiryDateStr);
  const now = new Date();
  const diffTime = expiry.getTime() - now.getTime();
  const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
  return diffDays <= daysThreshold;
}

export function CryovialKpiCards({ items }: CryovialKpiCardsProps) {
  const theme = useTheme();
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

  const cards: KpiCardDef[] = [
    { label: "Total Batches", description: "All registered batches", count: totalBatchesCount, icon: ScienceOutlinedIcon, tone: "purple" },
    { label: "Vials Available", description: "Usable vials remaining", count: vialsAvailableCount, icon: CheckCircleOutlineIcon, tone: "notDetected" },
    { label: "Expiring Soon", description: "Within 30 days", count: expiringSoonCount, icon: WarningAmberOutlinedIcon, tone: "action" },
    { label: "Low Stock", description: "≤ 2 vials remaining", count: lowStockCount, icon: ErrorOutlineIcon, tone: "detected" }
  ];

  return (
    <Grid container spacing={1.5} sx={{ mb: 2.5 }}>
      {cards.map((card) => {
        const tokens = theme.custom.status[card.tone];
        return (
          <Grid item xs={12} sm={6} md={3} key={card.label}>
            <Paper
              elevation={0}
              sx={{
                p: 1.75,
                border: "1px solid",
                borderColor: "divider",
                borderRadius: 2,
                bgcolor: "background.paper",
                boxShadow: "0 1px 3px rgba(0,0,0,0.04)"
              }}
            >
              <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 0.75 }}>
                <Box
                  sx={{
                    width: 36,
                    height: 36,
                    borderRadius: 1.5,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    bgcolor: tokens.bg,
                    color: tokens.text
                  }}
                >
                  <card.icon sx={{ fontSize: 20 }} />
                </Box>
                <Typography
                  sx={{
                    fontSize: 24,
                    fontWeight: 800,
                    color: tokens.text,
                    lineHeight: 1
                  }}
                >
                  {card.count}
                </Typography>
              </Box>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary", lineHeight: 1.2 }} noWrap>
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
