import { Button, Chip, Paper, Typography, useTheme } from "@mui/material";
import LayersIcon from "@mui/icons-material/Layers";
import CloseIcon from "@mui/icons-material/Close";
import FilterAltOutlinedIcon from "@mui/icons-material/FilterAltOutlined";

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
  onClear
}: Props) {
  const theme = useTheme();
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
        {selectedCount} {selectedCount === 1 ? "sample" : "samples"} selected
      </Typography>

      {hiddenCount > 0 && !showingSelectedOnly && (
        <Chip
          size="small"
          label={`${hiddenCount} hidden by current filters`}
          sx={{
            height: 20,
            fontSize: 11,
            fontWeight: 600,
            bgcolor: "background.paper",
            border: "1px solid",
            borderColor: "divider"
          }}
        />
      )}

      <Typography sx={{ fontSize: 12, color: "text.secondary", flex: 1, minWidth: 180 }}>
        {showingSelectedOnly
          ? "Showing only the selected samples; other filters are paused."
          : needsMore
          ? "Select at least one more to open grouped preparation and incubation setup."
          : "Grouped actions are available for this selection."}
      </Typography>

      {(hiddenCount > 0 || showingSelectedOnly) && (
        <Button
          size="small"
          variant="outlined"
          startIcon={<FilterAltOutlinedIcon sx={{ fontSize: 16 }} />}
          onClick={onToggleShowSelectedOnly}
          sx={{ height: 26, fontSize: 12, textTransform: "none", fontWeight: 600, borderColor: "divider" }}
        >
          {showingSelectedOnly ? "Back to filters" : "Show selected only"}
        </Button>
      )}

      <Button
        size="small"
        variant="outlined"
        startIcon={<CloseIcon sx={{ fontSize: 16 }} />}
        onClick={onClear}
        sx={{ height: 26, fontSize: 12, textTransform: "none", fontWeight: 600, borderColor: "divider" }}
      >
        Clear selection
      </Button>
    </Paper>
  );
}
