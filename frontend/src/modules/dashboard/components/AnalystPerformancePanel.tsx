import { Paper, Box, Typography, Stack, useTheme } from "@mui/material";
import SpeedIcon from "@mui/icons-material/Speed";
import TrendingUpIcon from "@mui/icons-material/TrendingUp";
import AssignmentTurnedInIcon from "@mui/icons-material/AssignmentTurnedIn";
import { SectionTitle } from "../../../components/SectionTitle";
import { AnalystMetrics } from "../types/dashboard";

interface AnalystPerformancePanelProps {
  metrics: AnalystMetrics | null;
}

export function AnalystPerformancePanel({ metrics }: AnalystPerformancePanelProps) {
  const theme = useTheme();

  const onTimeRate = metrics?.onTimeReadingRate ?? 100;
  const trailing7d = metrics?.trailing7DayVolume ?? 0;
  const activeOrders = metrics?.activeAssignedOrders ?? 0;

  return (
    <Paper sx={{ p: 2.5, height: "100%", display: "flex", flexDirection: "column" }}>
      <SectionTitle>My Operational Performance</SectionTitle>

      <Stack direction="row" spacing={2} sx={{ flex: 1, alignItems: "center" }}>
        <Box
          sx={{
            flex: 1,
            p: 1.75,
            borderRadius: 2,
            bgcolor: theme.palette.mode === "dark" ? "rgba(255,255,255,0.02)" : "rgba(0,0,0,0.02)",
            border: "1px solid",
            borderColor: theme.palette.divider,
            textAlign: "center"
          }}
        >
          <SpeedIcon sx={{ fontSize: 24, color: theme.custom.status.action.text, mb: 0.25 }} />
          <Typography sx={{ fontSize: 22, fontWeight: 700, color: theme.palette.text.primary, lineHeight: 1.1 }}>
            {onTimeRate}%
          </Typography>
          <Typography sx={{ fontSize: 11, fontWeight: 600, color: "text.secondary", mt: 0.5 }}>
            On-Time Reading Rate
          </Typography>
        </Box>

        <Box
          sx={{
            flex: 1,
            p: 1.75,
            borderRadius: 2,
            bgcolor: theme.palette.mode === "dark" ? "rgba(255,255,255,0.02)" : "rgba(0,0,0,0.02)",
            border: "1px solid",
            borderColor: theme.palette.divider,
            textAlign: "center"
          }}
        >
          <TrendingUpIcon sx={{ fontSize: 24, color: theme.custom.status.info.text, mb: 0.25 }} />
          <Typography sx={{ fontSize: 22, fontWeight: 700, color: theme.palette.text.primary, lineHeight: 1.1 }}>
            {trailing7d}
          </Typography>
          <Typography sx={{ fontSize: 11, fontWeight: 600, color: "text.secondary", mt: 0.5 }}>
            7-Day Completed Volume
          </Typography>
        </Box>

        <Box
          sx={{
            flex: 1,
            p: 1.75,
            borderRadius: 2,
            bgcolor: theme.palette.mode === "dark" ? "rgba(255,255,255,0.02)" : "rgba(0,0,0,0.02)",
            border: "1px solid",
            borderColor: theme.palette.divider,
            textAlign: "center"
          }}
        >
          <AssignmentTurnedInIcon sx={{ fontSize: 24, color: theme.custom.status.purple.text, mb: 0.25 }} />
          <Typography sx={{ fontSize: 22, fontWeight: 700, color: theme.palette.text.primary, lineHeight: 1.1 }}>
            {activeOrders}
          </Typography>
          <Typography sx={{ fontSize: 11, fontWeight: 600, color: "text.secondary", mt: 0.5 }}>
            Active Assigned Orders
          </Typography>
        </Box>
      </Stack>
    </Paper>
  );
}
