import { Paper, Box, Typography, Stack, LinearProgress } from "@mui/material";
import { IncubationOverviewRow } from "../types/dashboard";
import { SectionTitle } from "../../../components/SectionTitle";
import { brandColors } from "../../../theme";

export function IncubationOverview({ rows }: { rows: IncubationOverviewRow[] }) {
  return (
    <Paper sx={{ p: 2.5 }}>
      <SectionTitle>Incubation Overview</SectionTitle>
      <Stack spacing={2}>
        {rows.map((r) => {
          const total = r.readyToRead + r.incubating;
          const readyPercent = total === 0 ? 0 : (r.readyToRead / total) * 100;
          return (
            <Box key={r.testCode}>
              <Box sx={{ display: "flex", justifyContent: "space-between", mb: 0.5 }}>
                <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{r.testCode}</Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{total} tests</Typography>
              </Box>
              <LinearProgress
                variant="determinate" value={readyPercent}
                sx={{ height: 8, borderRadius: 4, bgcolor: "#f0e6f2", "& .MuiLinearProgress-bar": { bgcolor: brandColors.sectionTitle, borderRadius: 4 } }}
              />
              <Box sx={{ display: "flex", justifyContent: "space-between", mt: 0.5 }}>
                <Typography sx={{ fontSize: 11, color: brandColors.ok }}>{r.readyToRead} ready</Typography>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>{r.incubating} incubating</Typography>
              </Box>
            </Box>
          );
        })}
        {rows.length === 0 && <Typography sx={{ fontSize: 13, color: "text.secondary" }}>No active incubations.</Typography>}
      </Stack>
    </Paper>
  );
}
