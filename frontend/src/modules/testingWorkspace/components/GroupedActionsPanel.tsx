import React from "react";
import {
  Paper,
  Box,
  Typography,
  Button,
  IconButton,
  CircularProgress,
  Stack,
  Alert,
  Chip,
  useTheme
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import LayersIcon from "@mui/icons-material/Layers";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import { useActionableGroups } from "../hooks/useActionableGroups";
import { invalidateStepCache } from "../hooks/useTestStepQuickAction";
import { GroupedActionRow } from "./GroupedActionRow";
import { BatchSelectMediaResponse } from "../types/testWorkflowTypes";
import { useGroupedPreparation } from "../../testPreparation/hooks/useGroupedPreparation";
import { GroupedPreparationRow } from "../../testPreparation/components/GroupedPreparationRow";
import type { BatchConfirmPreparationResponse } from "../../testPreparation/services/SamplePreparationService";

interface GroupedActionsPanelProps {
  selectedSampleIds: number[];
  onDeselectAll: () => void;
  onOpenWorkflow?: (sampleId: number, testOrderId: number) => void;
  onActionComplete: (result: BatchSelectMediaResponse) => void;
  onPreparationComplete?: (result: BatchConfirmPreparationResponse) => void;
}

export function GroupedActionsPanel({
  selectedSampleIds,
  onDeselectAll,
  onOpenWorkflow,
  onActionComplete,
  onPreparationComplete
}: GroupedActionsPanelProps) {
  const theme = useTheme();

  const {
    groups,
    loading,
    error,
    reload,
    excludedResultEntryTestOrders,
    excludedResultEntryCount
  } = useActionableGroups({
    sampleIds: selectedSampleIds,
    scope: "all",
    enabled: selectedSampleIds.length >= 2
  });

  // Test Preparation gates incubation, so the same selection is asked for
  // both: whatever still needs preparing is offered first, and the setup
  // actions those samples unlock appear once it is signed.
  const {
    groups: prepGroups,
    excluded: prepExcluded,
    loading: prepLoading,
    error: prepError,
    reload: reloadPrep
  } = useGroupedPreparation({
    sampleIds: selectedSampleIds,
    enabled: selectedSampleIds.length >= 2
  });

  const handleBatchSuccess = (result: BatchSelectMediaResponse) => {
    invalidateStepCache();
    reload(true);
    onActionComplete(result);
  };

  const handlePreparationSuccess = (result: BatchConfirmPreparationResponse) => {
    invalidateStepCache();
    reloadPrep(true);
    reload(true);
    onPreparationComplete?.(result);
  };

  const hasPreparation = prepGroups.length > 0;

  // Preparation blocks these samples from any incubation action, so an
  // empty action list is expected rather than a dead end - say so instead
  // of showing the generic "nothing to do" panel.
  const preparationBlocksActions = hasPreparation && groups.length === 0;

  const preparationSection = hasPreparation ? (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 1.25 }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <Typography sx={{ fontSize: "0.72rem", fontWeight: 700, textTransform: "uppercase", color: "text.secondary", letterSpacing: "0.5px" }}>
          Test Preparation Required ({prepGroups.length})
        </Typography>
        <Typography sx={{ fontSize: "0.72rem", color: "text.secondary" }}>
          Must complete before incubation
        </Typography>
      </Box>

      <Stack spacing={1}>
        {prepGroups.map((g) => (
          <GroupedPreparationRow key={g.groupKey} group={g} onComplete={handlePreparationSuccess} />
        ))}
      </Stack>

      {prepExcluded.length > 0 && (
        <Alert severity="info" icon={<InfoOutlinedIcon fontSize="inherit" />} sx={{ py: 0.75, px: 1.5, borderRadius: 1.5 }}>
          <Typography sx={{ fontSize: "0.74rem", fontWeight: 700, mb: 0.25 }}>
            {prepExcluded.length} {prepExcluded.length === 1 ? "sample" : "samples"} cannot be prepared as a group
          </Typography>
          <Box sx={{ display: "flex", flexDirection: "column", gap: 0.25 }}>
            {prepExcluded.map((s) => (
              <Typography key={s.sampleId} sx={{ fontSize: "0.72rem", color: "text.secondary" }}>
                <strong>{s.sampleReference}</strong> - {s.reason}
              </Typography>
            ))}
          </Box>
        </Alert>
      )}
    </Box>
  ) : null;

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

      {prepError && (
        <Alert severity="error" sx={{ py: 0.5, fontSize: "0.75rem" }}>
          {prepError}
        </Alert>
      )}

      {loading || prepLoading ? (
        <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", py: 6, gap: 1.5 }}>
          <CircularProgress size={28} />
          <Typography sx={{ fontSize: "0.8rem", color: "text.secondary" }}>
            Calculating compatible workflow steps...
          </Typography>
        </Box>
      ) : preparationBlocksActions ? (
        <Box sx={{ display: "flex", flexDirection: "column", gap: 1.25 }}>
          {preparationSection}
          <Box
            sx={{
              py: 3,
              px: 3,
              textAlign: "center",
              border: "1px dashed",
              borderColor: theme.palette.divider,
              borderRadius: 2
            }}
          >
            <Typography sx={{ fontWeight: 600, fontSize: "0.86rem", mb: 0.5 }}>
              Incubation Setup Locked
            </Typography>
            <Typography sx={{ fontSize: "0.75rem", color: "text.secondary", maxWidth: 400, mx: "auto" }}>
              No incubation can start on a sample whose Test Preparation is still outstanding. Confirm the
              preparation above and the compatible setup actions appear here.
            </Typography>
          </Box>
        </Box>
      ) : groups.length === 0 ? (
        excludedResultEntryCount && excludedResultEntryCount > 0 ? (
          <Box
            sx={{
              py: 5,
              px: 3,
              textAlign: "center",
              border: "1px dashed",
              borderColor: theme.palette.mode === "dark" ? "rgba(245, 158, 11, 0.4)" : "#FCD34D",
              borderRadius: 2,
              bgcolor: theme.palette.mode === "dark" ? "rgba(245, 158, 11, 0.05)" : "#FFFBEB",
              my: "auto"
            }}
          >
            <InfoOutlinedIcon sx={{ fontSize: 36, color: "warning.main", mb: 1 }} />
            <Typography sx={{ fontWeight: 700, fontSize: "0.88rem", mb: 0.75, color: "text.primary" }}>
              Individual Result Entry Required
            </Typography>
            <Typography sx={{ fontSize: "0.76rem", color: "text.secondary", maxWidth: 460, mx: "auto", mb: 2 }}>
              No grouped workflow action is available. The selected tests require individual result entry. Result entry must be completed separately for each test.
            </Typography>
            {excludedResultEntryTestOrders && excludedResultEntryTestOrders.length > 0 && (
              <Box sx={{ display: "flex", flexDirection: "column", gap: 0.75, maxWidth: 520, mx: "auto", textAlign: "left" }}>
                <Typography sx={{ fontSize: "0.68rem", fontWeight: 700, textTransform: "uppercase", color: "text.secondary" }}>
                  Tests Awaiting Individual Result Entry ({excludedResultEntryTestOrders.length}):
                </Typography>
                <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75 }}>
                  {excludedResultEntryTestOrders.map((t) => (
                    <Chip
                      key={t.testOrderId}
                      size="small"
                      label={`${t.sampleReference} · ${t.displayName} (${t.stepName})`}
                      onClick={onOpenWorkflow ? () => onOpenWorkflow(t.sampleId, t.testOrderId) : undefined}
                      deleteIcon={onOpenWorkflow ? <OpenInNewIcon sx={{ fontSize: "13px !important" }} /> : undefined}
                      onDelete={onOpenWorkflow ? () => onOpenWorkflow(t.sampleId, t.testOrderId) : undefined}
                      sx={{
                        fontSize: "0.72rem",
                        fontWeight: 600,
                        cursor: onOpenWorkflow ? "pointer" : "default",
                        bgcolor: theme.palette.background.paper,
                        border: "1px solid",
                        borderColor: theme.palette.divider
                      }}
                    />
                  ))}
                </Box>
              </Box>
            )}
          </Box>
        ) : (
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
        )
      ) : (
        <Box sx={{ display: "flex", flexDirection: "column", gap: 1.25 }}>
          {preparationSection}

          {excludedResultEntryCount && excludedResultEntryCount > 0 ? (
            <Alert
              severity="info"
              icon={<InfoOutlinedIcon fontSize="inherit" />}
              sx={{
                py: 0.75,
                px: 1.5,
                fontSize: "0.74rem",
                borderRadius: 1.5
              }}
            >
              <Typography sx={{ fontSize: "0.74rem", fontWeight: 700, mb: 0.25 }}>
                {excludedResultEntryCount} {excludedResultEntryCount === 1 ? "test" : "tests"} excluded from grouped actions
              </Typography>
              <Typography sx={{ fontSize: "0.72rem", color: "text.secondary" }}>
                The selected tests require individual result entry (e.g. colony counts or plate readings) and cannot be executed as a grouped action. Result entry must be completed separately for each test.
              </Typography>
            </Alert>
          ) : null}

          <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
            <Typography sx={{ fontSize: "0.72rem", fontWeight: 700, textTransform: "uppercase", color: "text.secondary", letterSpacing: "0.5px" }}>
              Compatible Actions ({groups.length})
            </Typography>
            <Typography sx={{ fontSize: "0.72rem", color: "text.secondary" }}>
              Shared operational workflow transition
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
