import { Grid, Paper, Typography, Box } from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import HourglassEmptyOutlinedIcon from "@mui/icons-material/HourglassEmptyOutlined";
import EventAvailableOutlinedIcon from "@mui/icons-material/EventAvailableOutlined";
import GroupsOutlinedIcon from "@mui/icons-material/GroupsOutlined";
import VerifiedUserOutlinedIcon from "@mui/icons-material/VerifiedUserOutlined";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import { SvgIconComponent } from "@mui/icons-material";
import { DashboardSummary } from "../types/dashboard";
import { brandColors } from "../../../theme";

interface Tile { label: string; value: number; icon: SvgIconComponent; color: string }

// The 6-tile KPI strip from the reference dashboard. Each tile maps to a
// field already returned by GET /dashboard - no per-tile day-over-day
// delta is shown because the backend doesn't compute one for these
// (only KpiDeltas' month-over-month samples/tests do).
export function KpiStrip({ summary }: { summary: DashboardSummary }) {
  const tiles: Tile[] = [
    { label: "Active Tests", value: summary.pendingTests, icon: ScienceOutlinedIcon, color: brandColors.badgeRM },
    { label: "Incubating", value: summary.incubatingCount, icon: HourglassEmptyOutlinedIcon, color: brandColors.badgePM },
    { label: "Ready to Read", value: summary.readyToReadCount, icon: EventAvailableOutlinedIcon, color: brandColors.ok },
    { label: "Awaiting Review", value: summary.reviewerQueue, icon: GroupsOutlinedIcon, color: brandColors.sectionTitle },
    { label: "Under Approval", value: summary.approvalQueue, icon: VerifiedUserOutlinedIcon, color: brandColors.badgeProduct },
    { label: "Overdue / Attention", value: summary.delayedTests, icon: WarningAmberOutlinedIcon, color: brandColors.err }
  ];

  return (
    <Grid container spacing={2} sx={{ mb: 1 }}>
      {tiles.map((t) => (
        <Grid item xs={12} sm={6} md={4} lg={2} key={t.label}>
          <Paper sx={{ p: 2.5, display: "flex", alignItems: "center", gap: 1.5 }}>
            <Box sx={{
              width: 40, height: 40, borderRadius: "50%", display: "flex", alignItems: "center", justifyContent: "center",
              bgcolor: `${t.color}1a`, color: t.color, flexShrink: 0
            }}>
              <t.icon fontSize="small" />
            </Box>
            <Box sx={{ minWidth: 0 }}>
              <Typography sx={{ fontSize: 22, fontWeight: 700, color: brandColors.sectionTitle, lineHeight: 1.1 }}>{t.value}</Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }} noWrap>{t.label}</Typography>
            </Box>
          </Paper>
        </Grid>
      ))}
    </Grid>
  );
}
