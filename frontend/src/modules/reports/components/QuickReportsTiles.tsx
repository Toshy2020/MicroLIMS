import { Box, Paper, Typography, Tooltip } from "@mui/material";
import { brandColors } from "../../../theme";
import { SampleCategory } from "../types/reportingTypes";

interface Tile {
  label: string;
  category?: SampleCategory; // undefined => Custom Report
  disabledReason?: string;
}

// Media/GPT and Reference Strains are not sample results - Media
// Evaluation lives in its own MediaEvaluation entity (see the
// ReplaceGptWithMediaEvaluation migration) and Reference Strains are
// Cryovial records, neither of which ever produces a ResultRecord row.
// Kept visible but disabled (rather than removed) so the tile grid
// still matches the mockup and the gap is explained, not silent.
const TILES: Tile[] = [
  { label: "Product", category: "FinishedProduct" },
  { label: "Raw Material", category: "RawMaterial" },
  { label: "Water", category: "Water" },
  { label: "Environmental Monitoring", category: "EnvironmentalMonitoring" },
  { label: "After Cleaning", category: "AfterCleaning" },
  { label: "Media/GPT", disabledReason: "Media evaluation reporting is coming in a later update." },
  { label: "Reference Strains", disabledReason: "Cryovial reporting is coming in a later update." },
  { label: "Custom Report" }
];

interface QuickReportsTilesProps {
  onPreset: (category: SampleCategory) => void;
  onCustomReport: () => void;
}

export function QuickReportsTiles({ onPreset, onCustomReport }: QuickReportsTilesProps) {
  return (
    <Paper sx={{ p: 2 }}>
      <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.sectionTitle, mb: 1.25 }}>Quick Reports</Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 1 }}>
        {TILES.map((tile) => {
          const disabled = !!tile.disabledReason;
          const button = (
            <Box
              onClick={disabled ? undefined : () => (tile.category ? onPreset(tile.category) : onCustomReport())}
              sx={{
                p: 1.25, borderRadius: 1.5, border: "1px solid #e5e7eb", textAlign: "center",
                fontSize: 12, fontWeight: 600, color: disabled ? "#9ca3af" : brandColors.sectionTitle,
                cursor: disabled ? "not-allowed" : "pointer", bgcolor: disabled ? "#f9fafb" : "transparent",
                "&:hover": disabled ? {} : { bgcolor: brandColors.causeBadgeBg }
              }}
            >
              {tile.label}
            </Box>
          );
          return disabled ? (
            <Tooltip key={tile.label} title={tile.disabledReason!}>
              <span>{button}</span>
            </Tooltip>
          ) : (
            <Box key={tile.label}>{button}</Box>
          );
        })}
      </Box>
    </Paper>
  );
}
