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
  Chip,
  Alert,
  useTheme
} from "@mui/material";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import { ActionableGroup, BatchSelectMediaResponse } from "../types/testWorkflowTypes";
import { StatusBadge } from "../../../components/StatusBadge";
import { lookupCache, ReleasedMediaItem, IncubatorEquipmentItem } from "../../../services/lookupCache";
import { TestWorkflowService } from "../services/TestWorkflowService";

interface GroupedActionRowProps {
  group: ActionableGroup;
  onOpenWorkflow?: (sampleId: number, testOrderId: number) => void;
  onActionComplete: (result: BatchSelectMediaResponse) => void;
}

export function GroupedActionRow({
  group,
  onOpenWorkflow,
  onActionComplete
}: GroupedActionRowProps) {
  const theme = useTheme();
  const [expanded, setExpanded] = useState(false);
  const [loadingLookups, setLoadingLookups] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const [releasedMedia, setReleasedMedia] = useState<ReleasedMediaItem[]>([]);
  const [incubators, setIncubators] = useState<IncubatorEquipmentItem[]>([]);

  const [selectedMediaId, setSelectedMediaId] = useState<number | "">("");
  const [selectedIncubatorId, setSelectedIncubatorId] = useState<number | "">("");

  // Load released media lots and incubators on expand
  useEffect(() => {
    if (!expanded) return;
    let mounted = true;
    setLoadingLookups(true);

    Promise.all([
      lookupCache.getReleasedMedia(),
      lookupCache.getIncubators()
    ])
      .then(([mediaList, incList]) => {
        if (!mounted) return;
        setReleasedMedia(mediaList);
        setIncubators(incList);
      })
      .finally(() => {
        if (mounted) setLoadingLookups(false);
      });

    return () => {
      mounted = false;
    };
  }, [expanded]);

  // Filter media lots: must match any of the group's permitted material IDs and not be expired
  const matchingMedia = useMemo(() => {
    const now = Date.now();
    const permittedSet = new Set(group.permittedMaterialIds);
    return releasedMedia.filter((m) => {
      if (!permittedSet.has(m.materialId)) return false;
      if (m.expiryDate && new Date(m.expiryDate).getTime() < now) return false;
      return true;
    });
  }, [releasedMedia, group.permittedMaterialIds]);

  // Filter incubators: must have setPointTemperature within [group.tempMin, group.tempMax]
  const matchingIncubators = useMemo(() => {
    return incubators.filter((inc) => {
      if (inc.setPointTemperature == null) return false;
      if (inc.setPointTemperature < group.tempMin || inc.setPointTemperature > group.tempMax) return false;
      if (inc.calibrationStatus && inc.calibrationStatus.toLowerCase() === "lapsed") return false;
      return true;
    });
  }, [incubators, group.tempMin, group.tempMax]);

  // Auto-select first matching option if available
  useEffect(() => {
    if (matchingMedia.length > 0 && selectedMediaId === "") {
      setSelectedMediaId(matchingMedia[0].id);
    }
  }, [matchingMedia, selectedMediaId]);

  useEffect(() => {
    if (matchingIncubators.length > 0 && selectedIncubatorId === "") {
      setSelectedIncubatorId(matchingIncubators[0].id);
    }
  }, [matchingIncubators, selectedIncubatorId]);

  const handleToggle = (e: React.MouseEvent) => {
    e.stopPropagation();
    setExpanded((prev) => !prev);
  };

  const isIncubatorOnly = group.transitionType === "TRANSFER_INCUBATOR";

  const handleExecuteBatch = async (e: React.MouseEvent) => {
    e.stopPropagation();
    if (!selectedIncubatorId) return;
    if (!isIncubatorOnly && !selectedMediaId) return;

    setSubmitting(true);
    setSubmitError(null);

    try {
      const resp = await TestWorkflowService.batchSelectMedia({
        testOrderIds: group.testOrders.map((t) => t.testOrderId),
        stepName: group.stepName,
        mediaLotId: isIncubatorOnly ? undefined : Number(selectedMediaId),
        incubatorEquipmentId: Number(selectedIncubatorId),
        transitionType: group.transitionType,
        targetStepName: group.targetStepName,
        predecessorStepName: group.predecessorStepName
      });

      setExpanded(false);
      onActionComplete(resp);
    } catch (err: any) {
      setSubmitError(err?.response?.data?.message || err?.message || "Failed to execute batch action.");
    } finally {
      setSubmitting(false);
    }
  };

  const tempLabel = group.tempMin > 0 ? ` · ${group.tempMin}–${group.tempMax}°C` : "";
  const hoursLabel = group.incubationMinHours > 0 ? ` (${group.incubationMinHours}h)` : "";
  const mediaLabel = group.permittedMaterialNames ? ` · ${group.permittedMaterialNames}` : "";

  return (
    <Paper
      elevation={0}
      sx={{
        border: "1px solid",
        borderColor: expanded ? "primary.main" : theme.palette.divider,
        borderRadius: 1.5,
        transition: "all 0.15s ease",
        bgcolor: theme.palette.background.paper,
        overflow: "hidden"
      }}
    >
      {/* Collapsed single-line row */}
      <Box
        onClick={handleToggle}
        sx={{
          minHeight: 46,
          px: 1.5,
          py: 0.75,
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          cursor: "pointer",
          gap: 1.25,
          "&:hover": {
            bgcolor: theme.palette.action.hover
          }
        }}
      >
        {/* Left: Test name, count badge, status badge */}
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexShrink: 0 }}>
          <Typography sx={{ fontWeight: 700, fontSize: "0.82rem", color: theme.palette.text.primary }}>
            {group.transitionLabel || group.stepName}
          </Typography>

          <Chip
            size="small"
            label={`× ${group.sampleCount} ${group.sampleCount === 1 ? "Sample" : "Samples"}`}
            sx={{
              height: 20,
              fontSize: "0.68rem",
              fontWeight: 700,
              bgcolor: theme.palette.mode === "dark" ? "rgba(99, 102, 241, 0.15)" : "#EEF2FF",
              color: theme.palette.mode === "dark" ? "#A5B4FC" : "#4338CA",
              borderRadius: "4px"
            }}
          />

          <StatusBadge
            status={
              group.transitionType === "TRANSFER_INCUBATOR"
                ? "Ready: Transfer"
                : group.transitionType === "TRANSFER_SELECTIVE"
                ? "Ready: Plating"
                : "Ready: Setup"
            }
            label={
              group.transitionType === "TRANSFER_INCUBATOR"
                ? "Ready: Transfer"
                : group.transitionType === "TRANSFER_SELECTIVE"
                ? "Ready: Plating"
                : "Ready: Setup"
            }
          />
        </Box>

        {/* Center: Step description */}
        <Box sx={{ flex: 1, minWidth: 0, textAlign: "center", px: 1 }}>
          <Typography
            noWrap
            sx={{
              fontSize: "0.74rem",
              color: "text.secondary",
              fontWeight: 500
            }}
          >
            {group.transitionType === "TRANSFER_INCUBATOR"
              ? `Transfer to ${group.tempMin}–${group.tempMax}°C Incubator${hoursLabel}`
              : `Next: ${group.stepName}${mediaLabel}${tempLabel}${hoursLabel}`}
          </Typography>
        </Box>

        {/* Right: Expand chevron */}
        <Box sx={{ display: "flex", alignItems: "center", gap: 0.5, flexShrink: 0 }}>
          <IconButton
            size="small"
            onClick={handleToggle}
            sx={{
              width: 26,
              height: 26,
              color: expanded ? "primary.main" : "text.secondary"
            }}
          >
            {expanded ? <KeyboardArrowUpIcon fontSize="small" /> : <KeyboardArrowDownIcon fontSize="small" />}
          </IconButton>
        </Box>
      </Box>

      {/* Expanded temporary inline batch setup drawer */}
      <Collapse in={expanded} timeout="auto" unmountOnExit>
        <Box
          onClick={(e) => e.stopPropagation()}
          sx={{
            p: 1.5,
            pt: 1,
            borderTop: "1px solid",
            borderColor: theme.palette.divider,
            bgcolor: theme.palette.mode === "dark" ? "rgba(255,255,255,0.02)" : "rgba(0,0,0,0.015)"
          }}
        >
          {/* Sample Chips List */}
          <Box sx={{ mb: 1.5 }}>
            <Typography sx={{ fontSize: "0.68rem", fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 0.5 }}>
              Selected Samples ({group.testOrders.length} Tests):
            </Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75 }}>
              {group.testOrders.map((t) => (
                <Chip
                  key={t.testOrderId}
                  size="small"
                  label={`${t.sampleReference} · ${t.displayName}`}
                  onDelete={
                    onOpenWorkflow
                      ? (e) => {
                          e.stopPropagation();
                          onOpenWorkflow(t.sampleId, t.testOrderId);
                        }
                      : undefined
                  }
                  deleteIcon={
                    <Tooltip title="Open individual workflow">
                      <OpenInNewIcon sx={{ fontSize: "13px !important" }} />
                    </Tooltip>
                  }
                  sx={{
                    height: 24,
                    fontSize: "0.72rem",
                    fontWeight: 600,
                    borderRadius: "6px",
                    bgcolor: theme.palette.background.paper,
                    border: "1px solid",
                    borderColor: theme.palette.divider
                  }}
                />
              ))}
            </Box>
          </Box>

          {submitError && (
            <Alert severity="error" sx={{ mb: 1.5, py: 0.25, fontSize: "0.74rem" }}>
              {submitError}
            </Alert>
          )}

          {loadingLookups ? (
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, py: 1 }}>
              <CircularProgress size={16} />
              <Typography sx={{ fontSize: "0.75rem", color: "text.secondary" }}>
                Loading released media lots &amp; incubators...
              </Typography>
            </Box>
          ) : (
            <Box sx={{ display: "flex", flexWrap: "wrap", alignItems: "flex-end", gap: 1.5 }}>
              {/* Media Lot Dropdown - only when not incubator-only transfer */}
              {!isIncubatorOnly && (
                <Box sx={{ flex: "1 1 200px" }}>
                  <Typography sx={{ fontSize: "0.68rem", fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 0.5 }}>
                    Media Lot ({group.permittedMaterialNames || "Approved Media"})
                  </Typography>
                  <Select
                    size="small"
                    fullWidth
                    value={selectedMediaId}
                    onChange={(e) => setSelectedMediaId(e.target.value as number)}
                    displayEmpty
                    sx={{
                      height: 30,
                      fontSize: "0.75rem",
                      bgcolor: theme.palette.background.paper
                    }}
                  >
                    {matchingMedia.length === 0 ? (
                      <MenuItem disabled value="">
                        <em>No released, active lots available</em>
                      </MenuItem>
                    ) : (
                      matchingMedia.map((m) => (
                        <MenuItem key={m.id} value={m.id} sx={{ fontSize: "0.75rem" }}>
                          {m.lotNumber} ({m.materialName}) · Exp: {new Date(m.expiryDate).toLocaleDateString()}
                        </MenuItem>
                      ))
                    )}
                  </Select>
                </Box>
              )}

              {/* Incubator Dropdown */}
              <Box sx={{ flex: "1 1 200px" }}>
                <Typography sx={{ fontSize: "0.68rem", fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 0.5 }}>
                  {isIncubatorOnly ? "Transfer to Incubator" : "Incubator"} ({group.tempMin}–{group.tempMax}°C)
                </Typography>
                <Select
                  size="small"
                  fullWidth
                  value={selectedIncubatorId}
                  onChange={(e) => setSelectedIncubatorId(e.target.value as number)}
                  displayEmpty
                  sx={{
                    height: 30,
                    fontSize: "0.75rem",
                    bgcolor: theme.palette.background.paper
                  }}
                >
                  {matchingIncubators.length === 0 ? (
                    <MenuItem disabled value="">
                      <em>No eligible incubator in range</em>
                    </MenuItem>
                  ) : (
                    matchingIncubators.map((inc) => (
                      <MenuItem key={inc.id} value={inc.id} sx={{ fontSize: "0.75rem" }}>
                        {inc.code} — Set: {inc.setPointTemperature}°C ({inc.name})
                      </MenuItem>
                    ))
                  )}
                </Select>
              </Box>

              {/* Action Buttons */}
              <Box sx={{ display: "flex", alignItems: "center", gap: 1, ml: "auto" }}>
                <Button
                  size="small"
                  variant="outlined"
                  onClick={handleToggle}
                  sx={{
                    height: 30,
                    fontSize: "0.75rem",
                    textTransform: "none",
                    fontWeight: 600,
                    px: 1.5
                  }}
                >
                  Cancel
                </Button>

                <Button
                  size="small"
                  variant="contained"
                  disabled={submitting || !selectedIncubatorId || (!isIncubatorOnly && !selectedMediaId)}
                  onClick={handleExecuteBatch}
                  startIcon={submitting ? <CircularProgress size={14} color="inherit" /> : <ArrowForwardIcon sx={{ fontSize: 14 }} />}
                  sx={{
                    height: 30,
                    fontSize: "0.75rem",
                    textTransform: "none",
                    fontWeight: 700,
                    px: 2,
                    borderRadius: 1
                  }}
                >
                  {submitting
                    ? "Processing..."
                    : group.transitionType === "TRANSFER_INCUBATOR"
                    ? `Transfer to Incubator (${group.testOrders.length} ${group.testOrders.length === 1 ? "Test" : "Tests"})`
                    : group.transitionType === "TRANSFER_SELECTIVE"
                    ? `Transfer & Start Incubation (${group.testOrders.length} ${group.testOrders.length === 1 ? "Test" : "Tests"})`
                    : `Start Incubation (${group.testOrders.length} ${group.testOrders.length === 1 ? "Test" : "Tests"})`}
                </Button>
              </Box>
            </Box>
          )}
        </Box>
      </Collapse>
    </Paper>
  );
}
