import { Box, Paper, Typography, useTheme } from "@mui/material";
import { brandColors } from "../../../theme";
import { SampleCategory } from "../types/reportingTypes";
import { clickable } from "../../../utils/clickable";
import { useTranslation } from "../../../i18n";

interface Tile {
  key: string;
  category?: SampleCategory;
  type?: "mediaGpt" | "referenceStrains" | "custom";
}

const TILES: Tile[] = [
  { key: "product", category: "FinishedProduct" },
  { key: "rawMaterial", category: "RawMaterial" },
  { key: "water", category: "Water" },
  { key: "environmentalMonitoring", category: "EnvironmentalMonitoring" },
  { key: "afterCleaning", category: "AfterCleaning" },
  { key: "mediaGpt", type: "mediaGpt" },
  { key: "referenceStrains", type: "referenceStrains" },
  { key: "customReport", type: "custom" }
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
  const { t } = useTranslation("reports");
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
      <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1.25 }}>{t("quickReports.title")}</Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 1 }}>
        {TILES.map((tile) => (
          <Box
            key={tile.key}
            onClick={() => handleClick(tile)}
            sx={{
              p: 1.25, borderRadius: 1.5, border: "1px solid", borderColor: "divider", textAlign: "center",
              fontSize: 12, fontWeight: 600, color: brandColors.sectionTitle,
              cursor: "pointer", bgcolor: "transparent",
              "&:hover": { bgcolor: brandColors.causeBadgeBg }
            }}
            {...clickable(() => handleClick(tile))}
          >
            {t(`quickReports.${tile.key}`)}
          </Box>
        ))}
      </Box>
    </Paper>
  );
}
