import React from "react";
import {
  Paper,
  Box,
  Typography,
  Button,
  IconButton,
  CircularProgress,
  Stack,
  Divider,
  Alert,
  useTheme
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import LayersIcon from "@mui/icons-material/Layers";
import { useActionableGroups } from "../hooks/useActionableGroups";
import { GroupedActionRow } from "./GroupedActionRow";
import { BatchSelectMediaResponse } from "../types/testWorkflowTypes";

interface GroupedActionsPanelProps {
  selectedSampleIds: number[];
  onDeselectAll: () => void;
  onOpenWorkflow?: (sampleId: number, testOrderId: number) => void;
  onActionComplete: (result: BatchSelectMediaResponse) => void;
}

export function GroupedActionsPanel({
  selectedSampleIds,
  onDeselectAll,
  onOpenWorkflow,
  onActionComplete
}: GroupedActionsPanelProps) {
  const theme = useTheme();

  const { groups, loading, error, reload } = useActionableGroups({
    sampleIds: selectedSampleIds,
    scope: "all",
    enabled: selectedSampleIds.length >= 2
  });

  const handleBatchSuccess = (result: BatchSelectMediaResponse) => {
    reload(true);
    onActionComplete(result);
  };

  return (
    <Paper
      elevation={0}
      sx={{
        p: 2,
        height: "100%",
        display: "flex",
        flexDirection: "column",
        gap: 1.5,
        border: "1px solid",
        borderColor: theme.palette.divider,
        borderRadius: 2,
        bgcolor: theme.palette.background.paper,
        overflowY: "auto"
      }}
    >
      {/* Top Header Bar */}
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          flexWrap: "wrap",
          gap: 1,
          pb: 1.25,
          borderBottom: "1px solid",
          borderColor: theme.palette.divider
        }}
      >
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Box
            sx={{
              width: 32,
              height: 32,
              borderRadius: "8px",
              bgcolor: theme.palette.mode === "dark" ? "rgba(99, 102, 241, 0.2)" : "#EEF2FF",
              color: theme.palette.mode === "dark" ? "#A5B4FC" : "#4338CA",
              display: "flex",
              alignItems: "center",
              justifyContent: "center"
            }}
          >
            <LayersIcon fontSize="small" />
          </Box>
          <Box>
            <Typography sx={{ fontWeight: 700, fontSize: "0.95rem", lineHeight: 1.2 }}>
              Grouped Actions
            </Typography>
            <Typography sx={{ fontSize: "0.72rem", color: "text.secondary" }}>
              {selectedSampleIds.length} samples selected across workspace
            </Typography>
          </Box>
        </Box>

        <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
          <Button
            size="small"
            variant="outlined"
            onClick={onDeselectAll}
            sx={{
              height: 26,
              fontSize: "0.72rem",
              textTransform: "none",
              fontWeight: 600,
              px: 1.25,
              borderRadius: 1
            }}
          >
            Deselect All
          </Button>
          <IconButton
            size="small"
            onClick={onDeselectAll}
            sx={{ width: 26, height: 26, color: "text.secondary" }}
          >
            <CloseIcon fontSize="small" />
          </IconButton>
        </Box>
      </Box>

      {error && (
        <Alert severity="error" sx={{ py: 0.5, fontSize: "0.75rem" }}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", py: 6, gap: 1.5 }}>
          <CircularProgress size={28} />
          <Typography sx={{ fontSize: "0.8rem", color: "text.secondary" }}>
            Calculating compatible workflow steps...
          </Typography>
        </Box>
      ) : groups.length === 0 ? (
        <Box
          sx={{
            py: 6,
            px: 3,
            textAlign: "center",
            border: "1px dashed",
            borderColor: theme.palette.divider,
            borderRadius: 2,
            my: "auto"
          }}
        >
          <CheckCircleOutlineIcon sx={{ fontSize: 36, color: "text.disabled", mb: 1 }} />
          <Typography sx={{ fontWeight: 600, fontSize: "0.86rem", mb: 0.5 }}>
            No Shared Setup Actions Available
          </Typography>
          <Typography sx={{ fontSize: "0.75rem", color: "text.secondary", maxWidth: 360, mx: "auto" }}>
            The selected samples do not currently share an active incubation setup or preparation step requiring media and incubator selection.
          </Typography>
        </Box>
      ) : (
        <Box sx={{ display: "flex", flexDirection: "column", gap: 1.25 }}>
          <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
            <Typography sx={{ fontSize: "0.72rem", fontWeight: 700, textTransform: "uppercase", color: "text.secondary", letterSpacing: "0.5px" }}>
              Compatible Actions ({groups.length})
            </Typography>
            <Typography sx={{ fontSize: "0.72rem", color: "text.secondary" }}>
              Single media lot &amp; incubator selection applies to all tests in group
            </Typography>
          </Box>

          <Stack spacing={1}>
            {groups.map((g) => (
              <GroupedActionRow
                key={g.groupKey}
                group={g}
                onOpenWorkflow={onOpenWorkflow}
                onActionComplete={handleBatchSuccess}
              />
            ))}
          </Stack>
        </Box>
      )}
    </Paper>
  );
}
