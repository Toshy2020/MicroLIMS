import { useMemo } from "react";
import { Grid, Paper, Typography, Box } from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import PendingActionsOutlinedIcon from "@mui/icons-material/PendingActionsOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import HourglassEmptyOutlinedIcon from "@mui/icons-material/HourglassEmptyOutlined";
import { SvgIconComponent } from "@mui/icons-material";
import { SampleCard } from "./types/workspaceTypes";
import { brandColors } from "../../theme";

interface Tile { label: string; value: number; icon: SvgIconComponent; color: string }

// All 4 counts come from the sample list the page already has in memory
// (WorkspaceService.getActiveSamples()) - no separate backend summary
// call needed, same as how "Active Samples (N)" was already computed
// client-side before this redesign.
export function WorkspaceStatTiles({ samples }: { samples: SampleCard[] }) {
  const counts = useMemo(() => {
    const totalActive = samples.length;
    const underTesting = samples.filter((s) => s.status === "InTesting").length;
    const approved = samples.filter((s) => s.status === "Approved").length;
    const pending = totalActive - underTesting - approved;
    return { totalActive, underTesting, approved, pending };
  }, [samples]);

  const tiles: Tile[] = [
    { label: "Total Active", value: counts.totalActive, icon: ScienceOutlinedIcon, color: brandColors.badgeRM },
    { label: "Under Testing", value: counts.underTesting, icon: HourglassEmptyOutlinedIcon, color: brandColors.badgePM },
    { label: "Approved", value: counts.approved, icon: CheckCircleOutlineIcon, color: brandColors.ok },
    { label: "Pending", value: counts.pending, icon: PendingActionsOutlinedIcon, color: brandColors.sectionTitle }
  ];

  return (
    <Grid container spacing={2} sx={{ mb: 1 }}>
      {tiles.map((t) => (
        <Grid item xs={6} sm={3} key={t.label}>
          <Paper sx={{ p: 2, display: "flex", alignItems: "center", gap: 1.5 }}>
            <Box sx={{
              width: 36, height: 36, borderRadius: "50%", display: "flex", alignItems: "center", justifyContent: "center",
              bgcolor: `${t.color}1a`, color: t.color, flexShrink: 0
            }}>
              <t.icon fontSize="small" />
            </Box>
            <Box sx={{ minWidth: 0 }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }} noWrap>{t.label}</Typography>
              <Typography sx={{ fontSize: 20, fontWeight: 700, color: brandColors.sectionTitle, lineHeight: 1.1 }}>{t.value}</Typography>
            </Box>
          </Paper>
        </Grid>
      ))}
    </Grid>
  );
}
