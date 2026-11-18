import { Button, Chip, Paper, Typography, useTheme } from "@mui/material";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import LayersIcon from "@mui/icons-material/Layers";
import CloseIcon from "@mui/icons-material/Close";
import FilterAltOutlinedIcon from "@mui/icons-material/FilterAltOutlined";
import AssignmentIndIcon from "@mui/icons-material/AssignmentInd";
import FileDownloadOutlinedIcon from "@mui/icons-material/FileDownloadOutlined";

interface Props {
  selectedCount: number;
  // Selected samples the current filters have pushed out of view. Checked ids
  // are deliberately NOT dropped when a filter changes - silently
  // un-selecting a sample someone is about to sign for is worse than telling
  // them it is off screen - so this is what keeps that honest.
  hiddenCount: number;
  showingSelectedOnly: boolean;
  onToggleShowSelectedOnly: () => void;
  onClear: () => void;
  onBulkAssign?: () => void;
  // Present only for a user who may export; the page logs the export with the server before the file is made.
  onExport?: () => void;
}

// Selection used to be invisible until the second checkbox, at which point
// the page swapped to the grouped-actions split view with no warning. This
// gives the count a home from the very first click, and a way to see every
// selected sample before signing for any of them.
export function SelectionSummaryBar({
  selectedCount,
  hiddenCount,
  showingSelectedOnly,
  onToggleShowSelectedOnly,
  onClear,
  onBulkAssign,
  onExport
}: Props) {
  const theme = useTheme();
  const { t } = useTranslation("receiving");
  if (selectedCount === 0) return null;

  const needsMore = selectedCount < 2;

  return (
    <Paper
      elevation={0}
      role="status"
      sx={{
        mb: 1.5,
        px: 1.75,
        py: 1,
        display: "flex",
        alignItems: "center",
        flexWrap: "wrap",
        gap: 1.25,
        border: "1px solid",
        borderColor: theme.custom.status.purple.border,
        bgcolor: theme.custom.status.purple.bg,
        borderRadius: 2
      }}
    >
      <LayersIcon sx={{ fontSize: 18, color: theme.custom.status.purple.text }} />

      <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.custom.status.purple.text }}>
        <Trans
          i18nKey="selection.selectedCount"
          ns="receiving"
          count={selectedCount}
          values={{ count: selectedCount }}
          components={{ ltr: <Ltr>{null}</Ltr> }}
        />
      </Typography>

      {hiddenCount > 0 && !showingSelectedOnly && (
        <Chip
          size="small"
          label={t("selection.hiddenCount", { count: hiddenCount })}
          sx={{
            height: 20,
            fontSize: 12,
            fontWeight: 600,
            bgcolor: "background.paper",
            border: "1px solid",
            borderColor: "divider"
          }}
        />
      )}

      <Typography sx={{ fontSize: 12, color: "text.secondary", flex: 1, minWidth: 180 }}>
        {showingSelectedOnly
          ? t("selection.showingSelectedOnly")
          : needsMore
          ? t("selection.selectMore")
          : t("selection.groupedAvailable")}
      </Typography>

      {(hiddenCount > 0 || showingSelectedOnly) && (
        <Button
          size="small"
          variant="outlined"
          startIcon={<FilterAltOutlinedIcon sx={{ fontSize: 16 }} />}
          onClick={onToggleShowSelectedOnly}
          sx={{ height: 26, fontSize: 12, textTransform: "none", fontWeight: 600, borderColor: "divider" }}
        >
          {showingSelectedOnly ? t("selection.backToFilters") : t("selection.showSelectedOnly")}
        </Button>
      )}

      {onBulkAssign && (
        <Button
          size="small"
          variant="contained"
          startIcon={<AssignmentIndIcon sx={{ fontSize: 16 }} />}
          onClick={onBulkAssign}
          sx={{ height: 26, fontSize: 12, textTransform: "none", fontWeight: 600 }}
        >
          {t("selection.assignAnalyst")}
        </Button>
      )}

      {onExport && (
        <Button
          size="small"
          variant="outlined"
          startIcon={<FileDownloadOutlinedIcon sx={{ fontSize: 16 }} />}
          onClick={onExport}
          sx={{ height: 26, fontSize: 12, textTransform: "none", fontWeight: 600, borderColor: "divider" }}
        >
          {t("common:table.exportSelected")}
        </Button>
      )}

      <Button
        size="small"
        variant="outlined"
        startIcon={<CloseIcon sx={{ fontSize: 16 }} />}
        onClick={onClear}
        sx={{ height: 26, fontSize: 12, textTransform: "none", fontWeight: 600, borderColor: "divider" }}
      >
        {t("selection.clearSelection")}
      </Button>
    </Paper>
  );
}
