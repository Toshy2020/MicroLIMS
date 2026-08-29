import { Box, Paper, Typography, Tooltip, useTheme } from "@mui/material";
import { brandColors } from "../../../theme";
import { SampleCategory } from "../types/reportingTypes";

interface Tile {
  label: string;
  category?: SampleCategory;
  type?: "mediaGpt" | "referenceStrains" | "custom";
}

const TILES: Tile[] = [
  { label: "Product", category: "FinishedProduct" },
  { label: "Raw Material", category: "RawMaterial" },
  { label: "Water", category: "Water" },
  { label: "Environmental Monitoring", category: "EnvironmentalMonitoring" },
  { label: "After Cleaning", category: "AfterCleaning" },
  { label: "Media/GPT", type: "mediaGpt" },
  { label: "Reference Strains", type: "referenceStrains" },
  { label: "Custom Report", type: "custom" }
];

interface QuickReportsTilesProps {
  onPreset: (category: SampleCategory) => void;
  onCustomReport: () => void;
  onMediaGptReport?: () => void;
  onReferenceStrainsReport?: () => void;
}

export function QuickReportsTiles({
  onPreset,
  onCustomReport,
  onMediaGptReport,
  onReferenceStrainsReport
}: QuickReportsTilesProps) {
  const theme = useTheme();

  const handleClick = (tile: Tile) => {
    if (tile.type === "mediaGpt") {
      onMediaGptReport?.();
    } else if (tile.type === "referenceStrains") {
      onReferenceStrainsReport?.();
    } else if (tile.category) {
      onPreset(tile.category);
    } else {
      onCustomReport();
    }
  };

  return (
    <Paper sx={{ p: 2 }}>
      <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1.25 }}>Quick Reports</Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 1 }}>
        {TILES.map((tile) => (
          <Box
            key={tile.label}
            onClick={() => handleClick(tile)}
            sx={{
              p: 1.25, borderRadius: 1.5, border: "1px solid", borderColor: "divider", textAlign: "center",
              fontSize: 12, fontWeight: 600, color: brandColors.sectionTitle,
              cursor: "pointer", bgcolor: "transparent",
              "&:hover": { bgcolor: brandColors.causeBadgeBg }
            }}
          >
            {tile.label}
          </Box>
        ))}
      </Box>
    </Paper>
  );
}
