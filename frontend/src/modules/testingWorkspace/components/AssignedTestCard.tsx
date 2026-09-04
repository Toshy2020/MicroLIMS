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
import { StatusBadge } from "../../../components/StatusBadge";
import { TestOrderSummary, SampleCard as WorkspaceSampleCard } from "../types/workspaceTypes";
import { useTestStepQuickAction } from "../hooks/useTestStepQuickAction";

interface AssignedTestCardProps {
  test: TestOrderSummary;
  sample: WorkspaceSampleCard;
  stepInfo: { label: string; icon: React.ReactNode; color: string };
  onTestClick: (test: TestOrderSummary, sample: WorkspaceSampleCard) => void;
  onActionComplete: () => void;
}

function formatDuration(seconds: number): string {
  if (seconds <= 0) return "Ready to read";
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const secs = seconds % 60;
  if (hours > 0) {
    return `${hours}h ${minutes}m remaining`;
  }
  if (minutes > 0) {
    return `${minutes}m ${secs}s remaining`;
  }
  return `${secs}s remaining`;
}

export function AssignedTestCard({
  test,
  sample,
  stepInfo,
  onTestClick,
  onActionComplete
}: AssignedTestCardProps) {
  const theme = useTheme();
  const [expanded, setExpanded] = useState(false);
  const [optimisticIncubating, setOptimisticIncubating] = useState(false);
  const [optimisticDetails, setOptimisticDetails] = useState<{
    mediaName: string;
    incCode: string;
    endUtc?: string;
  } | null>(null);

  const [nowMs, setNowMs] = useState<number>(Date.now());

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
    onSuccess: () => {
      setExpanded(false);
      onActionComplete();
    }
  });

  const unit = sample.category === "EnvironmentalMonitoring" ? "rooms" : "parts";
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
    if (optimisticDetails) {
      return (stage1IncMinHours || 24) * 3600;
    }
    return 0;
  }, [optimisticDetails, incubationEndUtc, activeIncubation, nowMs, remainingSeconds, stage1IncMinHours]);

  // Dynamic Badge resolution: distinguish between media/incubator setup vs actual result entry
  const dynamicBadge = useMemo(() => {
    if (test.workflowState === "APPROVED" || test.status === "Approved") {
      return { status: "Approved", label: "Approved" };
    }
    if (test.workflowState === "RESULTS_RECORDED" || test.status === "UnderReview") {
      return { status: "PendingReview", label: "Pending Review" };
    }
    if (effectiveIsIncubating) {
      const endUtc = optimisticDetails?.endUtc || incubationEndUtc || activeIncubation?.incubationEndUtc;
      if (endUtc && nowMs >= new Date(endUtc).getTime()) {
        return { status: "EnterResult", label: "Ready to Read" };
      }
      return { status: "InProgress", label: "Incubating" };
    }

    // Step-aware resolution when step metadata is loaded
    if (step) {
      // 1. Broth steps (BrothEnrichment, SelectiveBroth) -> Always media/incubator setup
      if (step.stepType === "BrothEnrichment" || step.stepType === "SelectiveBroth") {
        return { status: "Ready: Broth", label: "Ready: Broth" };
      }

      // 2. Selective Plating step
      if (step.stepType === "SelectivePlating") {
        const isPlatingIncComplete = openIncubationRow?.status === "Complete" ||
          (openIncubationRow && dynamicRemainingSeconds <= 0) ||
          (test.workflowState === "AWAITING_RESULTS" && !requiresMediaSetup);

        if (isPlatingIncComplete) {
          return { status: "EnterResult", label: "Enter Result" };
        }
        return { status: "Ready: Plating", label: "Ready: Plating" };
      }

      // 3. Plate Count step (TAMC, TYMC, etc.)
      if (step.stepType === "PlateCount") {
        const isCountIncComplete = openIncubationRow?.status === "Complete" ||
          (openIncubationRow && dynamicRemainingSeconds <= 0) ||
          (test.workflowState === "AWAITING_RESULTS" && !requiresMediaSetup);

        if (isCountIncComplete) {
          return { status: "EnterResult", label: "Enter Result" };
        }
        return { status: "Ready: Setup", label: "Ready: Setup" };
      }

      // 4. Confirmatory Plating step
      if (step.stepType === "ConfirmatoryPlating") {
        const isConfComplete = openIncubationRow?.status === "Complete" ||
          (openIncubationRow && dynamicRemainingSeconds <= 0);

        if (isConfComplete) {
          return { status: "EnterResult", label: "Enter Result" };
        }
        return { status: "Ready: Confirmatory", label: "Ready: Confirmatory" };
      }

      // 5. Biochemical Test step -> actual result entry
      if (step.stepType === "BiochemicalTest") {
        return { status: "EnterResult", label: "Enter Result" };
      }
    }

    // Fallbacks if step metadata is loading or from workspace state
    if (test.workflowState === "READY_FOR_DOWNSTREAM") {
      return { status: "Ready: Plating", label: "Ready: Plating" };
    }
    if (test.workflowStatus === "EnterResult" || test.workflowState === "AWAITING_RESULTS") {
      return { status: "EnterResult", label: "Enter Result" };
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
    requiresMediaSetup
  ]);

  // Center text resolution (e.g. Ready for Selective Plating or countdown timer)
  const centerText = useMemo(() => {
    if (test.workflowState === "APPROVED" || test.status === "Approved") {
      return "✓ Approved & Complete";
    }
    if (test.workflowState === "RESULTS_RECORDED" || test.status === "UnderReview") {
      return "Result Recorded — Pending Review";
    }

    if (effectiveIsIncubating) {
      const mediaLabel = optimisticDetails?.mediaName || activeIncubation?.mediaName || openIncubationRow?.lotNumber || permittedMaterialNames || "Media";
      const incLabel = optimisticDetails?.incCode || activeIncubation?.incubatorName || openIncubationRow?.incubatorName || "";
      const incSuffix = incLabel ? ` (${incLabel})` : "";
      const tempRange = stage1TempMin > 0 ? ` · ${stage1TempMin}–${stage1TempMax}°C` : "";

      if (dynamicRemainingSeconds > 0) {
        return `⏳ ${mediaLabel}${tempRange} · ${formatDuration(dynamicRemainingSeconds)}${incSuffix}`;
      }

      // Check if endUtc has actually passed
      const endUtc = optimisticDetails?.endUtc || incubationEndUtc || activeIncubation?.incubationEndUtc;
      if (endUtc && nowMs >= new Date(endUtc).getTime()) {
        return `⏳ ${mediaLabel}${tempRange} · Incubation Complete${incSuffix}`;
      }

      return `⏳ ${mediaLabel}${tempRange} · Incubation In Progress${incSuffix}`;
    }

    // When step is ready for action:
    if (step) {
      // 1. Broth steps (BrothEnrichment, SelectiveBroth) -> media/incubator setup
      if (step.stepType === "BrothEnrichment" || step.stepType === "SelectiveBroth") {
        return `Ready for ${step.stepName}`;
      }

      // 2. Selective Plating
      if (step.stepType === "SelectivePlating") {
        const isPlatingIncComplete = openIncubationRow?.status === "Complete" ||
          (openIncubationRow && dynamicRemainingSeconds <= 0) ||
          (test.workflowState === "AWAITING_RESULTS" && !requiresMediaSetup);

        if (isPlatingIncComplete) {
          return `Ready for Growth Observation (${step.stepName})`;
        }
        return `Ready for ${step.stepName}`;
      }

      // 3. Plate Count
      if (step.stepType === "PlateCount") {
        const isCountIncComplete = openIncubationRow?.status === "Complete" ||
          (openIncubationRow && dynamicRemainingSeconds <= 0) ||
          (test.workflowState === "AWAITING_RESULTS" && !requiresMediaSetup);

        if (isCountIncComplete) {
          return "Ready for Colony Count Readings (CFU)";
        }
        const mediaLabel = permittedMaterialNames ? ` · ${permittedMaterialNames}` : "";
        const tempLabel = stage1TempMin > 0 ? ` · ${stage1TempMin}–${stage1TempMax}°C` : "";
        const hoursLabel = stage1IncMinHours > 0 ? ` (${stage1IncMinHours}h)` : "";
        return `Next: ${step.stepName}${mediaLabel}${tempLabel}${hoursLabel}`;
      }

      // 4. Confirmatory Plating
      if (step.stepType === "ConfirmatoryPlating") {
        const isConfComplete = openIncubationRow?.status === "Complete" ||
          (openIncubationRow && dynamicRemainingSeconds <= 0);

        if (isConfComplete) {
          return "Ready for Confirmatory Readings";
        }
        return `Ready for Confirmatory Plating (${step.stepName})`;
      }

      // 5. Biochemical Test
      if (step.stepType === "BiochemicalTest") {
        return `Ready for Biochemical Test Result`;
      }

      return `Ready for ${step.stepName}`;
    }

    // Fallbacks if step metadata is loading or from workspace state
    if (test.workflowState === "READY_FOR_DOWNSTREAM") {
      return "Ready for Selective Plating";
    }
    if (test.workflowState === "AWAITING_RESULTS" || test.isResultEntryAllowed) {
      return "Ready for Result Entry";
    }

    const stepLabel = test.currentStep || "Incubation";
    return `Next: ${stepLabel}`;
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
    step
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

    const incHours = stage1IncMinHours || 24;
    const estimatedEndUtc = new Date(Date.now() + incHours * 3600 * 1000).toISOString();

    // Optimistic UI state flip with live estimated end time
    setOptimisticIncubating(true);
    setOptimisticDetails({
      mediaName: chosenMedia?.lotNumber || permittedMaterialNames || "Media",
      incCode: chosenInc?.code || "Incubator",
      endUtc: estimatedEndUtc
    });
    setExpanded(false);

    try {
      const started = await handleStartIncubation();
      if (started?.expectedReadingAt || started?.incubationEndUtc) {
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
        bgcolor: "background.paper",
        cursor: "pointer",
        transition: "all 0.12s ease-in-out",
        "&:hover": {
          borderColor: theme.palette.primary.main,
          boxShadow: "0 1px 6px rgba(123, 45, 142, 0.08)",
          bgcolor: theme.custom.status.purple.bg
        }
      }}
    >
      {/* 1. Ultra-Compact Single-Line Horizontal Row (~46px) */}
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1.25, width: "100%" }}>
        {/* Left: Test Code (bold), Order #, Status Badge */}
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexShrink: 0 }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary", whiteSpace: "nowrap" }}>
            {test.testCode}{locationLabel}
          </Typography>
          <Typography sx={{ fontSize: 11, color: "text.secondary", whiteSpace: "nowrap" }}>
            #{test.testOrderId}
          </Typography>
          <StatusBadge status={dynamicBadge.status} label={dynamicBadge.label} />
        </Box>

        {/* Center: Step text or timer countdown */}
        <Box sx={{ display: "flex", alignItems: "center", gap: 0.5, minWidth: 0, flex: 1, justifyContent: "center" }}>
          <Typography
            noWrap
            sx={{
              fontSize: 11.5,
              fontWeight: effectiveIsIncubating ? 600 : 500,
              color: effectiveIsIncubating ? theme.custom.status.info.text : "text.secondary"
            }}
          >
            {centerText}
          </Typography>
        </Box>

        {/* Right: "Open Workflow ->" Link + 24x24px Chevron Toggle */}
        <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, flexShrink: 0 }} onClick={(e) => e.stopPropagation()}>
          <Button
            size="small"
            variant="text"
            data-no-row-click="true"
            endIcon={<ArrowForwardIcon sx={{ fontSize: 13 }} />}
            onClick={(e) => {
              e.stopPropagation();
              onTestClick(test, sample);
            }}
            sx={{
              color: theme.palette.primary.main,
              fontSize: 11.5,
              fontWeight: 700,
              p: 0,
              minWidth: "auto",
              textTransform: "none",
              "&:hover": { bgcolor: "transparent", textDecoration: "underline" }
            }}
          >
            Open Workflow
          </Button>

          {/* Chevron button: 24x24px. Shown when step requires media lot + incubator setup */}
          {!effectiveIsIncubating && test.status !== "Approved" && requiresMediaSetup && (
            <Tooltip title={expanded ? "Close quick setup" : "Quick setup: Select Media & Incubator"}>
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
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                Loading step requirements...
              </Typography>
            </Box>
          ) : error ? (
            <Box sx={{ py: 0.5 }}>
              <Typography sx={{ fontSize: 11, color: "error.main" }}>
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
              <Select
                size="small"
                displayEmpty
                data-no-row-click="true"
                value={selectedMediaId}
                onChange={(e) => setSelectedMediaId(e.target.value === "" ? "" : Number(e.target.value))}
                sx={{
                  height: 28,
                  fontSize: 11.5,
                  flex: 1,
                  minWidth: 160,
                  bgcolor: "background.paper",
                  "& .MuiSelect-select": { py: 0.5, px: 1 }
                }}
              >
                <MenuItem value="" sx={{ fontSize: 11.5 }}>
                  <em>Select Media Lot ({permittedMaterialNames || "Approved"})</em>
                </MenuItem>
                {matchingMedia.map((m) => (
                  <MenuItem key={m.id} value={m.id} sx={{ fontSize: 11.5, py: 0.5 }}>
                    {m.lotNumber} {m.materialName ? `(${m.materialName})` : ""}
                  </MenuItem>
                ))}
                {matchingMedia.length === 0 && (
                  <MenuItem disabled value="" sx={{ fontSize: 11.5 }}>
                    No released lots available
                  </MenuItem>
                )}
              </Select>

              {/* Incubator Dropdown (height: 28px) */}
              <Select
                size="small"
                displayEmpty
                data-no-row-click="true"
                value={selectedIncubatorId}
                onChange={(e) => setSelectedIncubatorId(e.target.value === "" ? "" : Number(e.target.value))}
                sx={{
                  height: 28,
                  fontSize: 11.5,
                  flex: 1,
                  minWidth: 150,
                  bgcolor: "background.paper",
                  "& .MuiSelect-select": { py: 0.5, px: 1 }
                }}
              >
                <MenuItem value="" sx={{ fontSize: 11.5 }}>
                  <em>Incubator ({stage1TempMin}–{stage1TempMax}°C)</em>
                </MenuItem>
                {matchingIncubators.map((inc) => (
                  <MenuItem key={inc.id} value={inc.id} sx={{ fontSize: 11.5, py: 0.5 }}>
                    {inc.code} ({inc.setPointTemperature}°C)
                  </MenuItem>
                ))}
                {matchingIncubators.length === 0 && (
                  <MenuItem disabled value="" sx={{ fontSize: 11.5 }}>
                    No matching incubator
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
                  fontSize: 11.5,
                  fontWeight: 700,
                  textTransform: "none",
                  px: 1.5,
                  whiteSpace: "nowrap"
                }}
              >
                {submitting ? "Starting..." : ctaLabel}
              </Button>
            </Box>
          ) : (
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", py: 0.25 }}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                Step requires full workflow interaction.
              </Typography>
              <Button
                size="small"
                variant="text"
                data-no-row-click="true"
                onClick={() => onTestClick(test, sample)}
                sx={{ fontSize: 11, fontWeight: 700, p: 0, textTransform: "none" }}
              >
                Open Workflow →
              </Button>
            </Box>
          )}
        </Box>
      </Collapse>
    </Paper>
  );
}
