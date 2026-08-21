import { Paper, Box, Typography, Stack, LinearProgress, useTheme, Button } from "@mui/material";
import HourglassEmptyIcon from "@mui/icons-material/HourglassEmpty";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import { useNavigate } from "react-router-dom";
import { IncubationOverviewRow } from "../types/dashboard";
import { SectionTitle } from "../../../components/SectionTitle";
import { brandColors } from "../../../theme";

interface IncubationObservationPanelProps {
  rows: IncubationOverviewRow[];
  loading?: boolean;
}

export function IncubationObservationPanel({ rows }: IncubationObservationPanelProps) {
  const theme = useTheme();
  const navigate = useNavigate();

  return (
    <Paper sx={{ p: 2.5, height: "100%", display: "flex", flexDirection: "column" }}>
      <SectionTitle
        tabs={[
          {
            label: "Open Workspace",
            onClick: () => navigate("/testing-workspace")
          }
        ]}
      >
        Incubation &amp; Observation
      </SectionTitle>

      <Stack spacing={2} sx={{ flex: 1 }}>
        {rows.map((r) => {
          const total = r.readyToRead + r.incubating;
          const readyPercent = total === 0 ? 0 : (r.readyToRead / total) * 100;

          return (
            <Box
              key={r.testCode}
              sx={{
                p: 1.5,
                borderRadius: 1.5,
                border: "1px solid",
                borderColor: theme.palette.divider,
                bgcolor: theme.palette.mode === "dark" ? "rgba(255,255,255,0.02)" : "rgba(0,0,0,0.01)"
              }}
            >
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
                  {r.testCode}
                </Typography>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary" }}>
                  {total} Active Test{total === 1 ? "" : "s"}
                </Typography>
              </Box>

              <LinearProgress
                variant="determinate"
                value={readyPercent}
                sx={{
                  height: 8,
                  borderRadius: 4,
                  bgcolor: theme.custom.status.purple.bg,
                  "& .MuiLinearProgress-bar": {
                    bgcolor: brandColors.sectionTitle,
                    borderRadius: 4
                  }
                }}
              />

              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 1 }}>
                <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                  <CheckCircleIcon sx={{ fontSize: 14, color: brandColors.ok }} />
                  <Typography sx={{ fontSize: 12, fontWeight: 600, color: brandColors.ok }}>
                    {r.readyToRead} Ready to Read
                  </Typography>
                </Box>

                <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                  <HourglassEmptyIcon sx={{ fontSize: 14, color: "text.secondary" }} />
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                    {r.incubating} Incubating
                  </Typography>
                </Box>
              </Box>
            </Box>
          );
        })}

        {rows.length === 0 && (
          <Box sx={{ py: 3, textAlign: "center" }}>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              No active incubations running.
            </Typography>
          </Box>
        )}
      </Stack>
    </Paper>
  );
}
