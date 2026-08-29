import { Box, Paper, Typography, Stack, Grid, Chip, useTheme } from "@mui/material";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import HighlightOffIcon from "@mui/icons-material/HighlightOff";
import HourglassEmptyIcon from "@mui/icons-material/HourglassEmpty";
import AssessmentIcon from "@mui/icons-material/Assessment";
import { MediaGptSummary } from "../types/mediaGptTypes";
import { brandColors } from "../../../theme";

interface MediaGptSummaryCardProps {
  summary: MediaGptSummary | null;
  loading?: boolean;
}

export function MediaGptSummaryCard({ summary }: MediaGptSummaryCardProps) {
  const theme = useTheme();

  if (!summary) return null;

  return (
    <Paper sx={{ p: 2.5, mb: 2 }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2, flexWrap: "wrap", gap: 1 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <AssessmentIcon sx={{ color: theme.palette.primary.main, fontSize: 22 }} />
          <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
            Media Qualification & GPT Pass Rate Summary
          </Typography>
        </Box>
        <Chip
          size="small"
          label={`Server-Aggregated • ${summary.totalLots} Prepared Lots`}
          sx={{ bgcolor: theme.custom.status.purple.bg, color: theme.custom.status.purple.text, fontWeight: 700 }}
        />
      </Box>

      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid item xs={12} sm={6} md={3}>
          <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
            <Typography sx={{ fontSize: 11.5, color: "text.secondary", fontWeight: 600 }}>Overall GPT Pass Rate</Typography>
            <Typography sx={{ fontSize: 24, fontWeight: 800, color: summary.overallPassRatePercent >= 90 ? brandColors.ok : brandColors.badgePM }}>
              {summary.overallPassRatePercent}%
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
              {summary.totalConformed} of {summary.totalConformed + summary.totalNonConformed} completed lots
            </Typography>
          </Box>
        </Grid>

        <Grid item xs={12} sm={6} md={3}>
          <Box sx={{ p: 1.5, bgcolor: theme.custom.status.notDetected.bg, borderRadius: 1.5 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
              <CheckCircleOutlineIcon sx={{ color: brandColors.ok, fontSize: 18 }} />
              <Typography sx={{ fontSize: 11.5, color: theme.custom.status.notDetected.text, fontWeight: 600 }}>Conformed Lots</Typography>
            </Box>
            <Typography sx={{ fontSize: 24, fontWeight: 800, color: brandColors.ok }}>
              {summary.totalConformed}
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Qualified for release</Typography>
          </Box>
        </Grid>

        <Grid item xs={12} sm={6} md={3}>
          <Box sx={{ p: 1.5, bgcolor: theme.custom.status.detected.bg, borderRadius: 1.5 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
              <HighlightOffIcon sx={{ color: brandColors.err, fontSize: 18 }} />
              <Typography sx={{ fontSize: 11.5, color: theme.custom.status.detected.text, fontWeight: 600 }}>Non-Conform Lots</Typography>
            </Box>
            <Typography sx={{ fontSize: 24, fontWeight: 800, color: brandColors.err }}>
              {summary.totalNonConformed}
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Quarantined / Rejected</Typography>
          </Box>
        </Grid>

        <Grid item xs={12} sm={6} md={3}>
          <Box sx={{ p: 1.5, bgcolor: theme.custom.status.inconclusive.bg, borderRadius: 1.5 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
              <HourglassEmptyIcon sx={{ color: brandColors.badgePM, fontSize: 18 }} />
              <Typography sx={{ fontSize: 11.5, color: theme.custom.status.inconclusive.text, fontWeight: 600 }}>Pending Evaluation</Typography>
            </Box>
            <Typography sx={{ fontSize: 24, fontWeight: 800, color: brandColors.badgePM }}>
              {summary.totalPending}
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>In incubation or reading</Typography>
          </Box>
        </Grid>
      </Grid>

      {summary.mediaTypes.length > 0 && (
        <Box sx={{ mt: 1 }}>
          <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: "text.secondary", mb: 1 }}>
            Pass Rate by Media Type
          </Typography>
          <Stack spacing={1}>
            {summary.mediaTypes.map((mt) => (
              <Box
                key={mt.mediaType}
                sx={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  p: 1,
                  borderRadius: 1,
                  bgcolor: "background.default",
                  border: "1px solid",
                  borderColor: "divider",
                  fontSize: 12
                }}
              >
                <Typography sx={{ fontWeight: 600, fontSize: 12.5 }}>
                  {mt.mediaType}
                </Typography>
                <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
                  <Typography sx={{ fontSize: 11.5, color: "text.secondary" }}>
                    Total: <strong>{mt.totalLots}</strong> | Conform: <strong style={{ color: brandColors.ok }}>{mt.conformedLots}</strong> | NonConform: <strong style={{ color: brandColors.err }}>{mt.nonConformedLots}</strong>
                    {mt.pendingLots > 0 && ` | Pending: ${mt.pendingLots}`}
                  </Typography>
                  <Chip
                    size="small"
                    label={`${mt.passRatePercent}% Pass`}
                    color={mt.passRatePercent >= 90 ? "success" : mt.passRatePercent > 0 ? "warning" : "default"}
                    sx={{ fontWeight: 700, fontSize: 11, height: 22 }}
                  />
                </Box>
              </Box>
            ))}
          </Stack>
        </Box>
      )}
    </Paper>
  );
}
