import { Paper, Box, Typography, Stack, useTheme } from "@mui/material";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import MedicationLiquidIcon from "@mui/icons-material/MedicationLiquid";
import { SectionTitle } from "../../../components/SectionTitle";
import { AnalystMetrics } from "../types/dashboard";

interface CompletedTodayPanelProps {
  metrics: AnalystMetrics | null;
}

export function CompletedTodayPanel({ metrics }: CompletedTodayPanelProps) {
  const theme = useTheme();

  const testsCount = metrics?.testsCompletedToday ?? 0;
  const mediaCount = metrics?.mediaLotsPreparedToday ?? 0;

  return (
    <Paper sx={{ p: 2.5, height: "100%", display: "flex", flexDirection: "column" }}>
      <SectionTitle>Completed Today</SectionTitle>

      <Stack direction="row" spacing={2} sx={{ flex: 1, alignItems: "center" }}>
        <Box
          sx={{
            flex: 1,
            p: 2,
            borderRadius: 2,
            bgcolor: theme.custom.status.notDetected.bg,
            border: "1px solid",
            borderColor: theme.custom.status.notDetected.border,
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            textAlign: "center"
          }}
        >
          <CheckCircleOutlineIcon sx={{ fontSize: 28, color: theme.custom.status.notDetected.text, mb: 0.5 }} />
          <Typography sx={{ fontSize: 28, fontWeight: 700, color: theme.custom.status.notDetected.text, lineHeight: 1.1 }}>
            {testsCount}
          </Typography>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary", mt: 0.5 }}>
            Tests Completed
          </Typography>
        </Box>

        <Box
          sx={{
            flex: 1,
            p: 2,
            borderRadius: 2,
            bgcolor: theme.custom.status.purple.bg,
            border: "1px solid",
            borderColor: theme.custom.status.purple.border,
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            textAlign: "center"
          }}
        >
          <MedicationLiquidIcon sx={{ fontSize: 28, color: theme.custom.status.purple.text, mb: 0.5 }} />
          <Typography sx={{ fontSize: 28, fontWeight: 700, color: theme.custom.status.purple.text, lineHeight: 1.1 }}>
            {mediaCount}
          </Typography>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary", mt: 0.5 }}>
            Media Lots Prepared
          </Typography>
        </Box>
      </Stack>
    </Paper>
  );
}
