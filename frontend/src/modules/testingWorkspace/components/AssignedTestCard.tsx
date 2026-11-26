import React, { useState, useEffect, useMemo } from "react";
import {
  Paper,
  Box,
  Typography,
  Button,
  IconButton,
  Collapse,
  Select,
  MenuItem,
  CircularProgress,
  Tooltip,
  useTheme
} from "@mui/material";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import { Ltr } from "../../../components/Ltr";
import { StatusBadge } from "../../../components/StatusBadge";
import { TestOrderSummary, SampleCard as WorkspaceSampleCard } from "../types/workspaceTypes";
import { useTestStepQuickAction } from "../hooks/useTestStepQuickAction";
import { useTranslation } from "../../../i18n";
import { clickable } from "../../../utils/clickable";
import { useOptionalAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { OverrideDialog } from "../../competency/OverrideDialog";
import { RaiseDeviationButton } from "../../quality/components/RaiseDeviationButton";
import { QualityRecordsPanel } from "../../quality/components/QualityRecordsPanel";

interface AssignedTestCardProps {
  test: TestOrderSummary;
  sample: WorkspaceSampleCard;
  onTestClick: (test: TestOrderSummary, sample: WorkspaceSampleCard) => void;
  onActionComplete: () => void;
}

// Closed test states come from the backend (WorkflowStateResolver). A closed
// test has no next step, so these win over every incubation/step rule below.
const CLOSED_TEST_STATUSES: Record<string, string> = {
  REJECTED: "Rejected",
  VOIDED: "Voided",
  CANCELLED: "Cancelled",
  SUPERSEDED: "Superseded",
  ON_HOLD: "OnHold"
};

export function AssignedTestCard({
  test,
  sample,
  onTestClick,
  onActionComplete
}: AssignedTestCardProps) {
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();
  const auth = useOptionalAuth();
  const canOverride = auth?.permissions.includes(PERMISSIONS.COMPETENCY_OVERRIDE) ?? false;
  const [overrideOpen, setOverrideOpen] = useState(false);
  const [qualityRefreshKey, setQualityRefreshKey] = useState(0);

  const formatDuration = (seconds: number): string => {
    if (seconds <= 0) return t("assignedCard.readyToRead");
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = seconds % 60;
    if (hours > 0) {
      return t("assignedCard.remainingHours", { hours, minutes });
    }
    if (minutes > 0) {
      return t("assignedCard.remainingMinutes", { minutes, seconds: secs });
    }
    return t("assignedCard.remainingSeconds", { seconds: secs });
  };

  // Water, EM and After Cleaning create their test orders inside PrepareAsync,
  // so before preparation those samples have no tests to interact with at all.
  // Product-like samples get their tests at receipt, so the tests sit here
  // fully clickable while the sample is still unprepared - the analyst starts
  // one, the backend correctly refuses it, and the only feedback is an error.
  // Offer the action only once it can actually succeed.
  // FP tests have no preparation stage, so they stay open on a mixed sample.
  const awaitingPreparation = sample.preparationStatus === "NeedsPreparation" && !test.skipsPreparation;

  const [expanded, setExpanded] = useState(false);
  const [optimisticIncubating, setOptimisticIncubating] = useState(false);
  const [optimisticDetails, setOptimisticDetails] = useState<{
    mediaName: string;
    incCode: string;
    endUtc?: string;
  } | null>(null);

  const [nowMs, setNowMs] = useState<number>(Date.now());

  // For closed tests and tests past result entry, dynamicBadge and centerText
  // return before reading any step data, so their current step isn't fetched.
  const stepDataUnused =
    Boolean(test.workflowState && CLOSED_TEST_STATUSES[test.workflowState]) ||
    test.workflowState === "APPROVED" || test.status === "Approved" ||
    test.workflowState === "REVIEWED" || test.status === "Reviewed" ||
    test.workflowState === "RESULTS_RECORDED" || test.status === "UnderReview";
  const canOfferOverride = canOverride && test.assignedAnalystId != null && !stepDataUnused;

  const {
    loading,
    submitting,
    error,
    step,
    requiresMediaSetup,
    matchingMedia,
    permittedMaterialNames,
    matchingIncubators,
    selectedMediaId,
    setSelectedMediaId,
    selectedIncubatorId,
    setSelectedIncubatorId,
    stage1TempMin,
    stage1TempMax,
    stage1IncMinHours,
    stage1IncMaxHours,
    isIncubating,
    activeIncubation,
    remainingSeconds,
    incubationEndUtc,
    openIncubationRow,
    ctaLabel,
    handleStartIncubation
  } = useTestStepQuickAction({
    testOrderId: test.testOrderId,
    testCode: test.testCode,
    expanded,
    enabled: !stepDataUnused,
    refreshKey: `${test.status}|${test.workflowState ?? ""}|${test.workflowStatus ?? ""}`,
    onSuccess: () => {
      setExpanded(false);
      onActionComplete();
    }
  });

  const unit = sample.category === "EnvironmentalMonitoring" ? t("assignedCard.unitRooms") : t("assignedCard.unitParts");
  const locationLabel = test.locationCount > 0 ? ` (${test.locationCount} ${unit})` : "";

  // Effective incubation state (combines server status with optimistic update)
  const isServerIncubating = isIncubating ||
    test.workflowState === "TSB_INCUBATING" ||
    test.workflowState === "COUNT_INCUBATING" ||
    test.workflowState === "INCUBATING" ||
    test.workflowState === "DOWNSTREAM_INCUBATING";

  const effectiveIsIncubating = optimisticIncubating || isServerIncubating;

  // Real-time ticking interval while incubating
  useEffect(() => {
    if (!effectiveIsIncubating) return;
    const interval = setInterval(() => {
      setNowMs(Date.now());
    }, 1000);
    return () => clearInterval(interval);
  }, [effectiveIsIncubating]);

  // Dynamic remaining seconds calculation (ticks down live every second)
  const dynamicRemainingSeconds = useMemo(() => {
    const endUtc = optimisticDetails?.endUtc || incubationEndUtc || activeIncubation?.incubationEndUtc;
    if (endUtc) {
      const endMs = new Date(endUtc).getTime();
      const diffSec = Math.floor((endMs - nowMs) / 1000);
      return Math.max(0, diffSec);
    }
    if (remainingSeconds > 0) {
      return remainingSeconds;
    }
    return 0;
  }, [optimisticDetails, incubationEndUtc, activeIncubation, nowMs, remainingSeconds]);

  // Dynamic Badge resolution: distinguish between media/incubator setup vs actual result entry
  const dynamicBadge = useMemo(() => {
    const closedStatus = test.workflowState ? CLOSED_TEST_STATUSES[test.workflowState] : undefined;
    if (closedStatus && test.workflowState) {
      return { status: closedStatus, label: t(`assignedCard.closed.${test.workflowState}.label`) };
    }
    // Blocked by the server: never shown as ready, whatever the timestamps say.
    if (test.workflowState === "WINDOW_NOT_CONFIGURED") {
      return { status: "Blocked", label: t("assignedCard.badge.windowNotConfigured") };
    }
    if (test.workflowState === "APPROVED" || test.status === "Approved") {
      return { status: "Approved", label: t("assignedCard.badge.approved") };
    }
    if (test.workflowState === "REVIEWED" || test.status === "Reviewed") {
      return { status: "Reviewed", label: t("assignedCard.badge.reviewed") };
    }
    if (test.workflowState === "RESULTS_RECORDED" || test.status === "UnderReview") {
      return { status: "PendingReview", label: t("assignedCard.badge.pendingReview") };
    }
    if (effectiveIsIncubating) {
      const endUtc = optimisticDetails?.endUtc || incubationEndUtc || activeIncubation?.incubationEndUtc;
      if (endUtc && nowMs >= new Date(endUtc).getTime()) {
        if (test.workflowState === "TSB_INCUBATING" || test.usesSharedTsb || step?.stepType === "BrothEnrichment" || step?.stepType === "SelectiveBroth") {
          return { status: "Ready: Plating", label: t("assignedCard.badge.readyPlating") };
        }
        return { status: "EnterResult", label: t("assignedCard.badge.enterResult") };
      }
      return { status: "InProgress", label: t("assignedCard.badge.incubating") };
    }

    // Step-aware resolution when step metadata is loaded
    if (step) {
      // 1. Broth steps (BrothEnrichment, SelectiveBroth) -> Always media/incubator setup
      if (step.stepType === "BrothEnrichment" || step.stepType === "SelectiveBroth") {
        return { status: "Ready: Broth", label: t("assignedCard.badge.readyBroth") };
      }

      // 2. Selective Plating step
      if (step.stepType === "SelectivePlating") {
        const isPlatingIncComplete = openIncubationRow?.status === "Complete" ||
          (openIncubationRow && dynamicRemainingSeconds <= 0) ||
          (test.workflowState === "AWAITING_RESULTS" && !requiresMediaSetup);

        if (isPlatingIncComplete) {
          return { status: "EnterResult", label: t("assignedCard.badge.enterResult") };
        }
        return { status: "Ready: Plating", label: t("assignedCard.badge.readyPlating") };
      }

      // 3. Plate Count step (TAMC, TYMC, etc.)
      if (step.stepType === "PlateCount") {
        const isCountIncComplete = openIncubationRow?.status === "Complete" ||
          (openIncubationRow && dynamicRemainingSeconds <= 0) ||
          (test.workflowState === "AWAITING_RESULTS" && !requiresMediaSetup);

        if (isCountIncComplete) {
          return { status: "EnterResult", label: t("assignedCard.badge.enterResult") };
        }
        return { status: "Ready: Setup", label: t("assignedCard.badge.readySetup") };
      }

      // 4. Confirmatory Plating step
      if (step.stepType === "ConfirmatoryPlating") {
        const isConfComplete = openIncubationRow?.status === "Complete" ||
          (openIncubationRow && dynamicRemainingSeconds <= 0);

        if (isConfComplete) {
          return { status: "EnterResult", label: t("assignedCard.badge.enterResult") };
        }
        return { status: "Ready: Confirmatory", label: t("assignedCard.badge.readyConfirmatory") };
      }

      // 5. Biochemical Test step -> actual result entry
      if (step.stepType === "BiochemicalTest") {
        return { status: "EnterResult", label: t("assignedCard.badge.enterResult") };
      }
    }

    // Fallbacks if step metadata is loading or from workspace state
    if (test.workflowState === "READY_FOR_DOWNSTREAM") {
      return { status: "Ready: Plating", label: t("assignedCard.badge.readyPlating") };
    }
    if (test.workflowStatus === "EnterResult" || test.workflowState === "AWAITING_RESULTS") {
      return { status: "EnterResult", label: t("assignedCard.badge.enterResult") };
    }

    return { status: test.workflowStatus ?? test.status, label: undefined };
  }, [
    test,
    step,
    effectiveIsIncubating,
    optimisticDetails,
    incubationEndUtc,
    activeIncubation,
    nowMs,
    openIncubationRow,
    dynamicRemainingSeconds,
    requiresMediaSetup,
    t
  ]);

  // Center text resolution (e.g. Ready for Selective Plating or countdown timer)
  const centerText = useMemo(() => {
    const closedStatus = test.workflowState ? CLOSED_TEST_STATUSES[test.workflowState] : undefined;
    if (closedStatus && test.workflowState) {
      return t(`assignedCard.closed.${test.workflowState}.text`);
    }
    if (test.workflowState === "WINDOW_NOT_CONFIGURED") {
      return t("assignedCard.center.windowNotConfigured");
    }
    if (test.workflowState === "APPROVED" || test.status === "Approved") {
      return t("assignedCard.center.approved");
    }
    if (test.workflowState === "REVIEWED" || test.status === "Reviewed") {
      return t("assignedCard.center.reviewed");
    }
    if (test.workflowState === "RESULTS_RECORDED" || test.status === "UnderReview") {
      return t("assignedCard.center.pendingReview");
    }

    if (effectiveIsIncubating) {
      const mediaLabel = optimisticDetails?.mediaName || activeIncubation?.mediaName || openIncubationRow?.lotNumber || permittedMaterialNames || t("assignedCard.center.mediaFallback");
      const incLabel = optimisticDetails?.incCode || activeIncubation?.incubatorName || openIncubationRow?.incubatorName || "";
      const incSuffix = incLabel ? ` (${incLabel})` : "";
      const tempRange = stage1TempMin > 0 ? ` · ${stage1TempMin}–${stage1TempMax}°C` : "";
      const summary = `${mediaLabel}${tempRange}`;

      if (dynamicRemainingSeconds > 0) {
        return `⏳ ${summary} · ${formatDuration(dynamicRemainingSeconds)}${incSuffix}`;
      }

      // Check if endUtc has actually passed
      const endUtc = optimisticDetails?.endUtc || incubationEndUtc || activeIncubation?.incubationEndUtc;
      if (endUtc && nowMs >= new Date(endUtc).getTime()) {
        if (test.workflowState === "TSB_INCUBATING" || test.usesSharedTsb || step?.stepType === "BrothEnrichment" || step?.stepType === "SelectiveBroth") {
          return t("assignedCard.center.incubationCompleteDownstream", { summary, incSuffix });
        }
        return t("assignedCard.center.incubationComplete", { summary, incSuffix });
      }

      return t("assignedCard.center.incubationInProgress", { summary, incSuffix });
    }

    // When step is ready for action:
    if (step) {
      // 1. Broth steps (BrothEnrichment, SelectiveBroth) -> media/incubator setup
      if (step.stepType === "BrothEnrichment" || step.stepType === "SelectiveBroth") {
        return t("assignedCard.center.readyForStep", { stepName: step.stepName });
      }

      // 2. Selective Plating
      if (step.stepType === "SelectivePlating") {
        const isPlatingIncComplete = openIncubationRow?.status === "Complete" ||
          (openIncubationRow && dynamicRemainingSeconds <= 0) ||
          (test.workflowState === "AWAITING_RESULTS" && !requiresMediaSetup);

        if (isPlatingIncComplete) {
          return t("assignedCard.center.readyGrowthObservation", { stepName: step.stepName });
        }
        return t("assignedCard.center.readyForStep", { stepName: step.stepName });
      }

      // 3. Plate Count
      if (step.stepType === "PlateCount") {
        const isCountIncComplete = openIncubationRow?.status === "Complete" ||
          (openIncubationRow && dynamicRemainingSeconds <= 0) ||
          (test.workflowState === "AWAITING_RESULTS" && !requiresMediaSetup);

        if (isCountIncComplete) {
          return t("assignedCard.center.readyColonyCount");
        }
        const mediaLabel = permittedMaterialNames ? ` · ${permittedMaterialNames}` : "";
        const tempLabel = stage1TempMin > 0 ? ` · ${stage1TempMin}–${stage1TempMax}°C` : "";
        const hoursLabel = stage1IncMinHours > 0 ? ` (${stage1IncMinHours}h)` : "";
        return t("assignedCard.center.nextWithSummary", { stepName: step.stepName, summary: `${mediaLabel}${tempLabel}${hoursLabel}` });
      }

      // 4. Confirmatory Plating
      if (step.stepType === "ConfirmatoryPlating") {
        const isConfComplete = openIncubationRow?.status === "Complete" ||
          (openIncubationRow && dynamicRemainingSeconds <= 0);

        if (isConfComplete) {
          return t("assignedCard.center.readyConfirmatoryReadings");
        }
        return t("assignedCard.center.readyConfirmatoryPlating", { stepName: step.stepName });
      }

      // 5. Biochemical Test
      if (step.stepType === "BiochemicalTest") {
        return t("assignedCard.center.readyBiochemical");
      }

      return t("assignedCard.center.readyForStep", { stepName: step.stepName });
    }

    // Fallbacks if step metadata is loading or from workspace state
    if (test.workflowState === "READY_FOR_DOWNSTREAM") {
      return t("assignedCard.center.readySelectivePlating");
    }
    if (test.workflowState === "AWAITING_RESULTS" || test.isResultEntryAllowed) {
      return t("assignedCard.center.readyResultEntry");
    }

    const stepLabel = test.currentStep || t("assignedCard.center.incubationFallback");
    return t("assignedCard.center.nextStep", { stepLabel });
  }, [
    effectiveIsIncubating,
    optimisticDetails,
    activeIncubation,
    openIncubationRow,
    permittedMaterialNames,
    stage1TempMin,
    stage1TempMax,
    stage1IncMinHours,
    dynamicRemainingSeconds,
    incubationEndUtc,
    nowMs,
    requiresMediaSetup,
    test,
    step,
    t
  ]);

  const handleCardClick = () => {
    onTestClick(test, sample);
  };

  const handleToggleExpand = (e: React.MouseEvent) => {
    e.stopPropagation();
    setExpanded((prev) => !prev);
  };

  const onExecuteQuickAction = async () => {
    const chosenMedia = matchingMedia.find((m) => m.id === selectedMediaId);
    const chosenInc = matchingIncubators.find((i) => i.id === selectedIncubatorId);

    // Estimated only from the chosen medium's Test Master window; the server's
    // real end time replaces it below. No guessed default when unconfigured.
    const estimatedEndUtc = stage1IncMaxHours > 0
      ? new Date(Date.now() + stage1IncMaxHours * 3600 * 1000).toISOString()
      : undefined;

    // Optimistic UI state flip with live estimated end time
    setOptimisticIncubating(true);
    setOptimisticDetails({
      mediaName: chosenMedia?.lotNumber || permittedMaterialNames || t("assignedCard.center.mediaFallback"),
      incCode: chosenInc?.code || t("assignedCard.center.incubatorFallback"),
      endUtc: estimatedEndUtc
    });
    setExpanded(false);

    try {
      const started = await handleStartIncubation();

      // A refusal must never leave the optimistic state standing: showing a
      // countdown for an incubation that was never created is the one failure
      // this card must not have. Treat "no incubation came back" as a failure
      // as well, so this does not depend solely on the hook throwing.
      if (!started) {
        setOptimisticIncubating(false);
        setOptimisticDetails(null);
        return;
      }

      if (started.expectedReadingAt || started.incubationEndUtc) {
        const actualEndUtc = started.expectedReadingAt || started.incubationEndUtc;
        setOptimisticDetails((prev) => (prev ? { ...prev, endUtc: actualEndUtc } : null));
      }
    } catch {
      setOptimisticIncubating(false);
      setOptimisticDetails(null);
    }
  };

  return (
    <Paper
      elevation={0}
      data-no-row-click="true"
      onClick={handleCardClick}
      sx={{
        px: 1.5,
        py: 0.75,
        minHeight: 46,
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        borderRadius: 1.5,
        border: "1px solid",
        borderColor: expanded ? theme.palette.primary.main : "divider",
        borderInlineStart: "3px solid",
        borderInlineStartColor:
          dynamicBadge.status === "Approved"
            ? theme.custom.status.notDetected.text
            : effectiveIsIncubating
            ? theme.custom.status.info.text
            : awaitingPreparation
            ? theme.palette.divider
            : theme.palette.primary.main,
        bgcolor: "background.paper",
        cursor: "pointer",
        transition: "border-color 150ms cubic-bezier(0.4, 0, 0.2, 1), background-color 150ms cubic-bezier(0.4, 0, 0.2, 1), box-shadow 150ms cubic-bezier(0.4, 0, 0.2, 1)",
        "&:hover": {
          borderColor: theme.palette.primary.main,
          boxShadow: "0 2px 8px rgba(31, 35, 53, 0.08)",
          bgcolor: theme.custom.status.purple.bg
        }
      }}
      {...clickable(handleCardClick, { containsControls: true })}
    >
      {/* 1. Ultra-Compact Single-Line Horizontal Row (~46px) */}
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1.25, width: "100%" }}>
        {/* Left: Test Code (bold), Order #, Status Badge */}
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexShrink: 0 }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary", whiteSpace: "nowrap" }}>
            <Ltr>{test.testCode}</Ltr>{locationLabel}
          </Typography>
          <Typography sx={{ fontSize: 12, color: "text.secondary", fontVariantNumeric: "tabular-nums", whiteSpace: "nowrap" }}>
            <Ltr>#{test.testOrderId}</Ltr>
          </Typography>
          <StatusBadge status={dynamicBadge.status} label={dynamicBadge.label} />
        </Box>

        {/* Center: Step text or timer countdown */}
        <Box sx={{ display: "flex", alignItems: "center", gap: 0.5, minWidth: 0, flex: 1, justifyContent: "center" }}>
          <Typography
            noWrap
            sx={{
              fontSize: 12,
              fontWeight: effectiveIsIncubating ? 600 : 500,
              fontVariantNumeric: "tabular-nums",
              color: effectiveIsIncubating ? theme.custom.status.info.text : "text.secondary"
            }}
          >
            {centerText}
          </Typography>
        </Box>

        {/* Right: "Open Workflow ->" Link + 24x24px Chevron Toggle */}
        <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, flexShrink: 0 }} onClick={(e) => e.stopPropagation()}>
          <Tooltip
            title={
              awaitingPreparation
                ? t("assignedCard.prepTooltip")
                : ""
            }
          >
            {/* span: a disabled MUI Button fires no events, so the tooltip
                needs a wrapper that still does. */}
            <span>
              <Button
                size="small"
                variant="text"
                data-no-row-click="true"
                disabled={awaitingPreparation}
                endIcon={<ArrowForwardIcon sx={{ fontSize: 13 }} />}
                onClick={(e) => {
                  e.stopPropagation();
                  onTestClick(test, sample);
                }}
                sx={{
                  color: theme.palette.primary.main,
                  fontSize: 12,
                  fontWeight: 700,
                  p: 0,
                  minWidth: "auto",
                  textTransform: "none",
                  "&:hover": { bgcolor: "transparent", textDecoration: "underline" }
                }}
              >
                {t("assignedCard.openWorkflow")}
              </Button>
            </span>
          </Tooltip>

          {canOfferOverride && (
            <Button
              size="small"
              variant="outlined"
              data-no-row-click="true"
              aria-label={t("assignedCard.grantOverrideAria", { testCode: test.testCode })}
              onClick={(e) => {
                e.stopPropagation();
                setOverrideOpen(true);
              }}
              sx={{ fontSize: 12, textTransform: "none", py: 0.25, px: 1 }}
            >
              {t("assignedCard.grantOverride")}
            </Button>
          )}
          {/* Chevron button: 24x24px. Shown when step requires media lot + incubator setup */}
          {!effectiveIsIncubating && test.status !== "Approved" && requiresMediaSetup && !awaitingPreparation && (
            <Tooltip title={expanded ? t("assignedCard.closeQuickSetup") : t("assignedCard.openQuickSetup")}>
              <IconButton
                size="small"
                data-no-row-click="true"
                onClick={handleToggleExpand}
                sx={{
                  width: 24,
                  height: 24,
                  p: 0,
                  color: expanded ? theme.palette.primary.main : "text.secondary",
                  bgcolor: expanded ? "action.selected" : "transparent",
                  borderRadius: 0.75,
                  border: "1px solid",
                  borderColor: expanded ? theme.palette.primary.main : "divider",
                  "&:hover": { bgcolor: "action.hover" }
                }}
              >
                {expanded ? (
                  <KeyboardArrowUpIcon sx={{ fontSize: 16 }} />
                ) : (
                  <KeyboardArrowDownIcon sx={{ fontSize: 16 }} />
                )}
              </IconButton>
            </Tooltip>
          )}
        </Box>
      </Box>

      {/* 2. Temporary Quick-Action Drawer */}
      <Collapse in={expanded} timeout="auto" unmountOnExit>
        <Box
          data-no-row-click="true"
          onClick={(e) => e.stopPropagation()}
          onKeyDown={(e) => e.stopPropagation()}
          sx={{
            mt: 1,
            pt: 1,
            borderTop: "1px dashed",
            borderColor: "divider"
          }}
        >
          {loading ? (
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, py: 0.5 }}>
              <CircularProgress size={14} />
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                {t("assignedCard.loadingRequirements")}
              </Typography>
            </Box>
          ) : error ? (
            <Box sx={{ py: 0.5 }}>
              <Typography sx={{ fontSize: 12, color: "error.main" }}>
                {error}
              </Typography>
            </Box>
          ) : requiresMediaSetup ? (
            <Box
              sx={{
                display: "flex",
                alignItems: "center",
                flexWrap: { xs: "wrap", sm: "nowrap" },
                gap: 1
              }}
            >
              {/* Media Lot Dropdown (height: 28px) */}
              <Select<number | "">
                size="small"
                displayEmpty
                data-no-row-click="true"
                value={selectedMediaId}
                onChange={(e) => setSelectedMediaId(e.target.value === "" ? "" : Number(e.target.value))}
                sx={{
                  height: 28,
                  fontSize: 12,
                  flex: 1,
                  minWidth: 160,
                  bgcolor: "background.paper",
                  "& .MuiSelect-select": { py: 0.5, px: 1 }
                }}
                inputProps={{ "aria-label": t("assignedCard.mediaLotAria") }}
              >
                <MenuItem value="" sx={{ fontSize: 12 }}>
                  <em>{t("assignedCard.selectMediaLot", { names: permittedMaterialNames || t("assignedCard.approvedFallback") })}</em>
                </MenuItem>
                {matchingMedia.map((m) => (
                  <MenuItem key={m.id} value={m.id} sx={{ fontSize: 12, py: 0.5 }}>
                    <Ltr>{m.lotNumber}</Ltr> {m.materialName ? `(${m.materialName})` : ""}
                  </MenuItem>
                ))}
                {matchingMedia.length === 0 && (
                  <MenuItem disabled value="" sx={{ fontSize: 12 }}>
                    {t("assignedCard.noReleasedLots")}
                  </MenuItem>
                )}
              </Select>

              {/* Incubator Dropdown (height: 28px) */}
              <Select<number | "">
                size="small"
                displayEmpty
                data-no-row-click="true"
                value={selectedIncubatorId}
                onChange={(e) => setSelectedIncubatorId(e.target.value === "" ? "" : Number(e.target.value))}
                sx={{
                  height: 28,
                  fontSize: 12,
                  flex: 1,
                  minWidth: 150,
                  bgcolor: "background.paper",
                  "& .MuiSelect-select": { py: 0.5, px: 1 }
                }}
                inputProps={{ "aria-label": t("assignedCard.incubatorAria") }}
              >
                <MenuItem value="" sx={{ fontSize: 12 }}>
                  <em>{t("assignedCard.incubatorRange", { min: stage1TempMin, max: stage1TempMax })}</em>
                </MenuItem>
                {matchingIncubators.map((inc) => (
                  <MenuItem key={inc.id} value={inc.id} sx={{ fontSize: 12, py: 0.5 }}>
                    <Ltr>{inc.code}</Ltr> (<Ltr>{inc.setPointTemperature}°C</Ltr>)
                  </MenuItem>
                ))}
                {matchingIncubators.length === 0 && (
                  <MenuItem disabled value="" sx={{ fontSize: 12 }}>
                    {t("assignedCard.noMatchingIncubator")}
                  </MenuItem>
                )}
              </Select>

              {/* Primary Action Button (height: 28px) */}
              <Button
                size="small"
                variant="contained"
                data-no-row-click="true"
                disabled={!selectedMediaId || !selectedIncubatorId || submitting}
                onClick={onExecuteQuickAction}
                sx={{
                  height: 28,
                  fontSize: 12,
                  fontWeight: 700,
                  textTransform: "none",
                  px: 1.5,
                  whiteSpace: "nowrap"
                }}
              >
                {submitting ? t("assignedCard.starting") : ctaLabel}
              </Button>
            </Box>
          ) : (
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", py: 0.25 }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                {t("assignedCard.requiresFullWorkflow")}
              </Typography>
              <Button
                size="small"
                variant="text"
                data-no-row-click="true"
                onClick={() => onTestClick(test, sample)}
                sx={{ fontSize: 12, fontWeight: 700, p: 0, textTransform: "none" }}
              >
                {t("assignedCard.openWorkflowArrow")}
              </Button>
            </Box>
          )}

          <Box
            data-no-row-click="true"
            onClick={(e) => e.stopPropagation()}
            onKeyDown={(e) => e.stopPropagation()}
            sx={{ mt: 1.5, display: "flex", flexDirection: "column", gap: 1 }}
          >
            <Box>
              <RaiseDeviationButton
                targetType="TestOrder"
                targetId={test.testOrderId}
                sectionId={sample.labSectionId ?? null}
                onRaised={() => setQualityRefreshKey((k) => k + 1)}
              />
            </Box>
            <QualityRecordsPanel
              targetType="TestOrder"
              targetId={test.testOrderId}
              refreshKey={qualityRefreshKey}
              compact
            />
          </Box>
        </Box>
      </Collapse>
      {canOfferOverride && overrideOpen && test.assignedAnalystId != null && (
        <Box data-no-row-click="true" onClick={(e) => e.stopPropagation()} onKeyDown={(e) => e.stopPropagation()}>
          <OverrideDialog
            open
            testOrderId={test.testOrderId}
            testLabel={test.testCode}
            candidates={[
              {
                userId: test.assignedAnalystId,
                fullName: test.assignedAnalystName ?? String(test.assignedAnalystId)
              }
            ]}
            defaultAnalystUserId={test.assignedAnalystId}
            onClose={() => setOverrideOpen(false)}
            onGranted={() => {
              setOverrideOpen(false);
              onActionComplete();
            }}
          />
        </Box>
      )}
    </Paper>
  );
}
