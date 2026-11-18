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
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import LayersIcon from "@mui/icons-material/Layers";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
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
  const { t } = useTranslation("testingWorkspace");
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
        <Typography sx={{ fontSize: "0.75rem", fontWeight: 700, textTransform: "uppercase", color: "text.secondary", letterSpacing: "0.5px" }}>
          <Trans i18nKey="groupedActions.prepRequiredHeading" ns="testingWorkspace" values={{ count: prepGroups.length }} components={{ ltr: <Ltr>{null}</Ltr> }} />
        </Typography>
        <Typography sx={{ fontSize: "0.75rem", color: "text.secondary" }}>
          {t("groupedActions.prepMustComplete")}
        </Typography>
      </Box>

      <Stack spacing={1}>
        {prepGroups.map((g) => (
          <GroupedPreparationRow key={g.groupKey} group={g} onComplete={handlePreparationSuccess} />
        ))}
      </Stack>

      {prepExcluded.length > 0 && (
        <Alert severity="info" icon={<InfoOutlinedIcon fontSize="inherit" />} sx={{ py: 0.75, px: 1.5, borderRadius: 1.5 }}>
          <Typography sx={{ fontSize: "0.75rem", fontWeight: 700, mb: 0.25 }}>
            {t("groupedActions.cannotPrepareGroup", { count: prepExcluded.length })}
          </Typography>
          <Box sx={{ display: "flex", flexDirection: "column", gap: 0.25 }}>
            {prepExcluded.map((s) => (
              <Typography key={s.sampleId} sx={{ fontSize: "0.75rem", color: "text.secondary" }}>
                <strong><Ltr>{s.sampleReference}</Ltr></strong> - {s.reason}
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
              bgcolor: theme.custom.status.purple.bg,
              color: theme.custom.status.purple.text,
              display: "flex",
              alignItems: "center",
              justifyContent: "center"
            }}
          >
            <LayersIcon fontSize="small" />
          </Box>
          <Box>
            <Typography sx={{ fontWeight: 700, fontSize: "0.95rem", lineHeight: 1.2 }}>
              {t("groupedActions.title")}
            </Typography>
            <Typography sx={{ fontSize: "0.75rem", color: "text.secondary" }}>
              {t("groupedActions.samplesSelectedCount", { count: selectedSampleIds.length })}
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
              fontSize: "0.75rem",
              textTransform: "none",
              fontWeight: 600,
              px: 1.25,
              borderRadius: 1
            }}
          >
            {t("groupedActions.deselectAll")}
          </Button>
          <IconButton aria-label={t("groupedActions.clearSelectionAria")}
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
            {t("groupedActions.calculatingSteps")}
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
              {t("groupedActions.setupLockedTitle")}
            </Typography>
            <Typography sx={{ fontSize: "0.75rem", color: "text.secondary", maxWidth: 400, mx: "auto" }}>
              {t("groupedActions.setupLockedBody")}
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
              borderColor: theme.custom.status.inconclusive.border,
              borderRadius: 2,
              bgcolor: theme.custom.status.inconclusive.bg,
              my: "auto"
            }}
          >
            <InfoOutlinedIcon sx={{ fontSize: 36, color: "warning.main", mb: 1 }} />
            <Typography sx={{ fontWeight: 700, fontSize: "0.88rem", mb: 0.75, color: "text.primary" }}>
              {t("groupedActions.individualRequiredTitle")}
            </Typography>
            <Typography sx={{ fontSize: "0.76rem", color: "text.secondary", maxWidth: 460, mx: "auto", mb: 2 }}>
              {t("groupedActions.individualRequiredBody")}
            </Typography>
            {excludedResultEntryTestOrders && excludedResultEntryTestOrders.length > 0 && (
              <Box sx={{ display: "flex", flexDirection: "column", gap: 0.75, maxWidth: 520, mx: "auto", textAlign: "start" }}>
                <Typography sx={{ fontSize: "0.75rem", fontWeight: 700, textTransform: "uppercase", color: "text.secondary" }}>
                  {t("groupedActions.awaitingIndividualHeading", { count: excludedResultEntryTestOrders.length })}
                </Typography>
                <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75 }}>
                  {excludedResultEntryTestOrders.map((tOrder) => (
                    <Chip
                      key={tOrder.testOrderId}
                      size="small"
                      label={`${tOrder.sampleReference} · ${tOrder.displayName} (${tOrder.stepName})`}
                      onClick={onOpenWorkflow ? () => onOpenWorkflow(tOrder.sampleId, tOrder.testOrderId) : undefined}
                      deleteIcon={onOpenWorkflow ? <OpenInNewIcon sx={{ fontSize: "13px !important" }} /> : undefined}
                      onDelete={onOpenWorkflow ? () => onOpenWorkflow(tOrder.sampleId, tOrder.testOrderId) : undefined}
                      sx={{
                        fontSize: "0.75rem",
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
              {t("groupedActions.noSharedActionsTitle")}
            </Typography>
            <Typography sx={{ fontSize: "0.75rem", color: "text.secondary", maxWidth: 360, mx: "auto" }}>
              {t("groupedActions.noSharedActionsBody")}
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
                fontSize: "0.75rem",
                borderRadius: 1.5
              }}
            >
              <Typography sx={{ fontSize: "0.75rem", fontWeight: 700, mb: 0.25 }}>
                {t("groupedActions.excludedCount", { count: excludedResultEntryCount })}
              </Typography>
              <Typography sx={{ fontSize: "0.75rem", color: "text.secondary" }}>
                {t("groupedActions.excludedBody")}
              </Typography>
            </Alert>
          ) : null}

          <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
            <Typography sx={{ fontSize: "0.75rem", fontWeight: 700, textTransform: "uppercase", color: "text.secondary", letterSpacing: "0.5px" }}>
              <Trans i18nKey="groupedActions.compatibleActions" ns="testingWorkspace" values={{ count: groups.length }} components={{ ltr: <Ltr>{null}</Ltr> }} />
            </Typography>
            <Typography sx={{ fontSize: "0.75rem", color: "text.secondary" }}>
              {t("groupedActions.sharedTransition")}
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
