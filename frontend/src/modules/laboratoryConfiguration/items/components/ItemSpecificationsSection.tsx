import React, { useState, useEffect, useCallback } from "react";
import {
  Box,
  Typography,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Button,
  IconButton,
  Chip,
  Alert,
  CircularProgress,
  Stack,
  useTheme
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import AddIcon from "@mui/icons-material/Add";
import { Item } from "../services/ItemService";
import {
  SpecificationService,
  SpecificationDto,
  LimitType
} from "../../specifications/services/SpecificationService";
import { ConfirmationDialog } from "../../../../components/ConfirmationDialog";
import { tableHeadSx } from "../../../../theme";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import {
  SpecificationParameterDialog,
  formatTrimmedDecimal
} from "./SpecificationParameterDialog";

interface ItemSpecificationsSectionProps {
  item: Item;
  onSpecsChanged?: () => void;
}

const LIMIT_CHIP_STYLES: Record<string, { label: string; color: string; bg: string; border: string }> = {
  Range: {
    label: "Range",
    color: "#3b82f6",
    bg: "rgba(59, 130, 246, 0.12)",
    border: "rgba(59, 130, 246, 0.35)"
  },
  NotMoreThan: {
    label: "NMT",
    color: "#f59e0b",
    bg: "rgba(245, 158, 11, 0.12)",
    border: "rgba(245, 158, 11, 0.35)"
  },
  NotLessThan: {
    label: "NLT",
    color: "#f59e0b",
    bg: "rgba(245, 158, 11, 0.12)",
    border: "rgba(245, 158, 11, 0.35)"
  },
  TargetWithTolerance: {
    label: "Target \u00B1 Tol.",
    color: "#06b6d4",
    bg: "rgba(6, 182, 212, 0.12)",
    border: "rgba(6, 182, 212, 0.35)"
  },
  CountTiered: {
    label: "Count-Tiered",
    color: "#10b981",
    bg: "rgba(16, 185, 129, 0.12)",
    border: "rgba(16, 185, 129, 0.35)"
  },
  Qualitative: {
    label: "Qualitative",
    color: "#a855f7",
    bg: "rgba(168, 85, 247, 0.12)",
    border: "rgba(168, 85, 247, 0.35)"
  },
  PresenceAbsence: {
    label: "Presence/Absence",
    color: "#f43f5e",
    bg: "rgba(244, 63, 94, 0.12)",
    border: "rgba(244, 63, 94, 0.35)"
  },
  MultiStage: {
    label: "Multi-Stage",
    color: "#eab308",
    bg: "rgba(234, 179, 8, 0.12)",
    border: "rgba(234, 179, 8, 0.35)"
  },
  StageCriteria: {
    label: "Stage Criteria",
    color: "#94a3b8",
    bg: "rgba(148, 163, 184, 0.12)",
    border: "rgba(148, 163, 184, 0.35)"
  }
};

export const LimitTypeBadge: React.FC<{ type: string; labelOverride?: string }> = ({
  type,
  labelOverride
}) => {
  const style = LIMIT_CHIP_STYLES[type] ?? {
    label: type,
    color: "#94a3b8",
    bg: "rgba(148, 163, 184, 0.12)",
    border: "rgba(148, 163, 184, 0.35)"
  };
  return (
    <Chip
      size="small"
      label={labelOverride ?? style.label}
      sx={{
        height: 22,
        fontSize: 11,
        fontWeight: 600,
        color: style.color,
        bgcolor: style.bg,
        border: "1px solid",
        borderColor: style.border,
        borderRadius: "4px"
      }}
    />
  );
};

export const formatLimitCell = (spec: SpecificationDto): string => {
  const type = (spec.limitType as LimitType) || "CountTiered";
  switch (type) {
    case "Range": {
      const lower = formatTrimmedDecimal(spec.lowerLimit);
      const upper = formatTrimmedDecimal(spec.upperLimit);
      if (lower && upper) return `NLT ${lower} \u2014 NMT ${upper}`;
      if (upper) return `NMT ${upper}`;
      if (lower) return `NLT ${lower}`;
      return spec.specLimit || "\u2014";
    }
    case "NotMoreThan": {
      const upper = formatTrimmedDecimal(spec.upperLimit);
      return upper ? `NMT ${upper}` : spec.specLimit || "\u2014";
    }
    case "NotLessThan": {
      const lower = formatTrimmedDecimal(spec.lowerLimit);
      return lower ? `NLT ${lower}` : spec.specLimit || "\u2014";
    }
    case "TargetWithTolerance": {
      const target = formatTrimmedDecimal(spec.target);
      const tol = formatTrimmedDecimal(spec.tolerance);
      const suffix = spec.toleranceMode === "Percent" ? "%" : "";
      if (target && tol) return `${target} \u00B1 ${tol}${suffix}`;
      return spec.specLimit || "\u2014";
    }
    case "CountTiered": {
      const alert = spec.alertLimit ? `Alert ${spec.alertLimit}` : "";
      const action = spec.actionLimit ? `Action ${spec.actionLimit}` : "";
      const sp = spec.specLimit ? `Spec ${spec.specLimit}` : "";
      const parts = [alert, action, sp].filter(Boolean);
      return parts.length > 0 ? parts.join(" \u00B7 ") : spec.specLimit || "\u2014";
    }
    case "Qualitative": {
      return spec.expectedResultText || spec.specLimit || "\u2014";
    }
    case "PresenceAbsence": {
      const state = spec.expectedState === "Presence" ? "Present" : "Absent";
      const qty = formatTrimmedDecimal(spec.sampleQuantity);
      if (qty) {
        const unitStr = spec.sampleQuantityUnit ? ` ${spec.sampleQuantityUnit}` : "";
        return `${state} in ${qty}${unitStr}`;
      }
      return spec.specLimit || state;
    }
    case "MultiStage": {
      return "\u2014";
    }
    default:
      return spec.specLimit || "\u2014";
  }
};

export const ItemSpecificationsSection: React.FC<ItemSpecificationsSectionProps> = ({
  item,
  onSpecsChanged
}) => {
  const theme = useTheme();

  const [specs, setSpecs] = useState<SpecificationDto[]>(item.specifications ?? []);
  const [loading, setLoading] = useState(false);
  const [workflowTypeByCode, setWorkflowTypeByCode] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);

  // Dialog state
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingSpec, setEditingSpec] = useState<SpecificationDto | null>(null);
  const [preselectedTestCode, setPreselectedTestCode] = useState<string | null>(null);

  // Delete state
  const [pendingDelete, setPendingDelete] = useState<SpecificationDto | null>(null);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    masterDataOptions
      .getTestDefinitions()
      .then((defs: { code: string; workflowType: string }[]) => {
        setWorkflowTypeByCode(Object.fromEntries(defs.map((d) => [d.code, d.workflowType])));
      })
      .catch(() => {
        // Non-fatal fallback
      });
  }, []);

  const loadSpecs = useCallback(async () => {
    if (!item?.id) return;
    setLoading(true);
    try {
      const data = await SpecificationService.getForItem(item.id);
      if (Array.isArray(data)) {
        setSpecs(data);
      }
    } catch {
      setSpecs(item.specifications ?? []);
    } finally {
      setLoading(false);
    }
  }, [item?.id, item.specifications]);

  useEffect(() => {
    setSpecs(item.specifications ?? []);
    loadSpecs();
    setError(null);
  }, [item.id, loadSpecs]);

  const assignedTests = item.assignedTests ?? [];

  const getTestDisplayName = useCallback(
    (testCode: string) => {
      const match = assignedTests.find((t) => t.testCode === testCode);
      return match?.displayName && match.displayName !== testCode
        ? `${match.displayName} (${testCode})`
        : match?.displayName || testCode;
    },
    [assignedTests]
  );

  const handleOpenAdd = () => {
    setEditingSpec(null);
    setPreselectedTestCode(null);
    setDialogOpen(true);
  };

  const handleAddParameterToTest = (testCode: string) => {
    setEditingSpec(null);
    setPreselectedTestCode(testCode);
    setDialogOpen(true);
  };

  const handleOpenEdit = (spec: SpecificationDto) => {
    setEditingSpec(spec);
    setPreselectedTestCode(null);
    setDialogOpen(true);
  };

  const handleDeleteConfirm = async () => {
    if (!pendingDelete?.id) return;
    setDeleting(true);
    setError(null);
    try {
      await SpecificationService.remove(pendingDelete.id);
      setPendingDelete(null);
      await loadSpecs();
      onSpecsChanged?.();
    } catch (err: unknown) {
      const axiosError = err as {
        response?: { data?: { message?: string } };
        message?: string;
      };
      setError(
        axiosError?.response?.data?.message ||
          axiosError?.message ||
          "Failed to delete specification."
      );
    } finally {
      setDeleting(false);
    }
  };

  // Group specs by testCode (preserving order of appearance)
  const groupedSpecs: { testCode: string; specs: SpecificationDto[] }[] = [];
  const groupMap = new Map<string, SpecificationDto[]>();
  for (const spec of specs) {
    const list = groupMap.get(spec.testCode) ?? [];
    list.push(spec);
    groupMap.set(spec.testCode, list);
  }
  for (const [testCode, list] of groupMap.entries()) {
    groupedSpecs.push({ testCode, specs: list });
  }

  return (
    <Box sx={{ p: 0.5 }}>
      {/* Header section */}
      <Stack
        direction="row"
        sx={{
          justifyContent: "space-between",
          alignItems: "flex-start",
          mb: 1
        }}
      >
        <Box>
          <Typography
            variant="subtitle1"
            sx={{
              fontWeight: 800,
              fontSize: 13,
              letterSpacing: "0.5px",
              color: theme.palette.primary.main,
              textTransform: "uppercase"
            }}
          >
            PHARMACOPOEIAL SPECIFICATIONS & LIMITS ({specs.length})
          </Typography>
          <Typography
            variant="caption"
            sx={{ color: "text.secondary", display: "block", mt: 0.25 }}
          >
            One table for every limit type — count-based, numeric, qualitative and multi-stage.
          </Typography>
        </Box>

        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
          {loading && <CircularProgress size={16} />}
          {assignedTests.length > 0 && (
            <Button
              variant="contained"
              size="small"
              startIcon={<AddIcon />}
              onClick={handleOpenAdd}
              sx={{
                fontWeight: 700,
                textTransform: "none",
                fontSize: 12
              }}
            >
              Add Specification Parameter
            </Button>
          )}
        </Stack>
      </Stack>

      {/* Colour Legend */}
      <Stack
        direction="row"
        spacing={1}
        sx={{
          alignItems: "center",
          flexWrap: "wrap",
          gap: 1,
          mb: 2
        }}
      >
        <LimitTypeBadge type="Range" labelOverride="Range" />
        <LimitTypeBadge type="NotMoreThan" labelOverride="NMT/NLT" />
        <LimitTypeBadge type="TargetWithTolerance" labelOverride="Target \u00B1 Tol." />
        <LimitTypeBadge type="CountTiered" labelOverride="Count-Tiered" />
        <LimitTypeBadge type="Qualitative" labelOverride="Qualitative" />
        <LimitTypeBadge type="PresenceAbsence" labelOverride="Presence/Absence" />
        <LimitTypeBadge type="MultiStage" labelOverride="Multi-Stage" />
      </Stack>

      {error && (
        <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {assignedTests.length === 0 ? (
        <Alert severity="warning" sx={{ py: 1, fontSize: 13 }}>
          This item has no assigned tests yet. Assign tests under the{" "}
          <strong>Assigned Tests</strong> tab or edit the item before configuring
          specifications.
        </Alert>
      ) : (
        <Box>
          <Table
            size="small"
            sx={{
              mb: 2,
              border: "1px solid",
              borderColor: "divider",
              borderRadius: 1,
              overflow: "hidden"
            }}
          >
            <TableHead>
              <TableRow sx={tableHeadSx}>
                <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 220 }}>
                  Assigned Test / Parameter
                </TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 140 }}>
                  Limit Type
                </TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 200 }}>
                  Limit
                </TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 100 }}>
                  Unit
                </TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 150 }}>
                  Reference Standard
                </TableCell>
                <TableCell align="right" sx={{ fontWeight: 700, fontSize: 12, width: 90 }}>
                  Actions
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {groupedSpecs.length === 0 ? (
                <TableRow>
                  <TableCell
                    colSpan={6}
                    sx={{ py: 3, textAlign: "center", color: "text.secondary", fontStyle: "italic" }}
                  >
                    No specifications defined yet. Click &ldquo;Add Specification Parameter&rdquo; above to add limits.
                  </TableCell>
                </TableRow>
              ) : (
                groupedSpecs.map((group) => {
                  const testDisplayName = getTestDisplayName(group.testCode);
                  const isMultiParam = group.specs.length > 1;

                  if (!isMultiParam) {
                    // Test with a single parameter
                    const spec = group.specs[0];
                    const isMultiStage = spec.limitType === "MultiStage";
                    const displayName = spec.parameterName || testDisplayName;

                    return (
                      <React.Fragment key={group.testCode}>
                        <TableRow
                          hover
                          sx={{ "&:nth-of-type(even)": { bgcolor: "background.default" } }}
                        >
                          <TableCell sx={{ fontWeight: 600, fontSize: 13 }}>
                            {displayName}
                          </TableCell>
                          <TableCell>
                            <LimitTypeBadge type={spec.limitType ?? "CountTiered"} />
                          </TableCell>
                          <TableCell sx={{ fontSize: 13 }}>
                            {formatLimitCell(spec)}
                            {spec.limitType === "CountTiered" && spec.dilutionFactor != null && (
                              <Box sx={{ mt: 0.5 }}>
                                <Chip
                                  size="small"
                                  label={`Dilution Factor \u00D7${spec.dilutionFactor}`}
                                  sx={{
                                    height: 20,
                                    fontSize: 11,
                                    color: "text.secondary",
                                    bgcolor: "action.hover",
                                    border: "1px solid",
                                    borderColor: "divider"
                                  }}
                                />
                              </Box>
                            )}
                          </TableCell>
                          <TableCell sx={{ fontSize: 13 }}>{spec.unit || "\u2014"}</TableCell>
                          <TableCell sx={{ fontSize: 13 }}>
                            {spec.referenceStandard || "\u2014"}
                          </TableCell>
                          <TableCell align="right">
                            <Stack
                              direction="row"
                              spacing={0.5}
                              sx={{ justifyContent: "flex-end" }}
                            >
                              <IconButton
                                size="small"
                                onClick={() => handleOpenEdit(spec)}
                                title="Edit Specification"
                              >
                                <EditIcon fontSize="small" />
                              </IconButton>
                              <IconButton
                                size="small"
                                color="error"
                                onClick={() => setPendingDelete(spec)}
                                title="Delete Specification"
                              >
                                <DeleteIcon fontSize="small" />
                              </IconButton>
                            </Stack>
                          </TableCell>
                        </TableRow>

                        {/* If MultiStage, always show stages as indented rows */}
                        {isMultiStage &&
                          spec.stages?.map((stage, sIdx) => (
                            <TableRow
                              key={`${spec.id ?? spec.testCode}-stage-${sIdx}`}
                              sx={{ bgcolor: "action.hover" }}
                            >
                              <TableCell sx={{ pl: 3.5 }}>
                                <Stack
                                  direction="row"
                                  spacing={1}
                                  sx={{ alignItems: "center" }}
                                >
                                  <Typography
                                    sx={{ color: "text.secondary", fontSize: 13 }}
                                  >
                                    &#8627;
                                  </Typography>
                                  <Typography sx={{ fontSize: 12 }}>
                                    {stage.stageLabel}
                                  </Typography>
                                </Stack>
                              </TableCell>
                              <TableCell>
                                <LimitTypeBadge type="StageCriteria" />
                              </TableCell>
                              <TableCell sx={{ fontSize: 13, color: "text.primary" }}>
                                {stage.acceptanceCriteriaText}
                              </TableCell>
                              <TableCell sx={{ fontSize: 13 }}>
                                {spec.unit || "\u2014"}
                              </TableCell>
                              <TableCell sx={{ fontSize: 13 }}>{"\u2014"}</TableCell>
                              <TableCell align="right" />
                            </TableRow>
                          ))}
                      </React.Fragment>
                    );
                  }

                  // Test with several parameters
                  return (
                    <React.Fragment key={group.testCode}>
                      {/* Group Header Row */}
                      <TableRow sx={{ bgcolor: "action.selected" }}>
                        <TableCell colSpan={6} sx={{ py: 1 }}>
                          <Stack
                            direction="row"
                            spacing={1.5}
                            sx={{ alignItems: "center" }}
                          >
                            <Typography sx={{ fontWeight: 700, fontSize: 13 }}>
                              {testDisplayName}
                            </Typography>
                            <Chip
                              size="small"
                              label={`${group.specs.length} parameters`}
                              sx={{
                                height: 20,
                                fontSize: 11,
                                bgcolor: "background.paper"
                              }}
                            />
                          </Stack>
                        </TableCell>
                      </TableRow>

                      {/* Indented parameter rows */}
                      {group.specs.map((spec) => {
                        const isMultiStage = spec.limitType === "MultiStage";
                        return (
                          <React.Fragment key={spec.id ?? spec.parameterName}>
                            <TableRow hover>
                              <TableCell sx={{ pl: 3.5 }}>
                                <Stack
                                  direction="row"
                                  spacing={1}
                                  sx={{ alignItems: "center" }}
                                >
                                  <Typography
                                    sx={{ color: "text.secondary", fontSize: 13 }}
                                  >
                                    &#8627;
                                  </Typography>
                                  <Typography sx={{ fontWeight: 600, fontSize: 13 }}>
                                    {spec.parameterName}
                                  </Typography>
                                </Stack>
                              </TableCell>
                              <TableCell>
                                <LimitTypeBadge type={spec.limitType ?? "CountTiered"} />
                              </TableCell>
                              <TableCell sx={{ fontSize: 13 }}>
                                {formatLimitCell(spec)}
                                {spec.limitType === "CountTiered" &&
                                  spec.dilutionFactor != null && (
                                    <Box sx={{ mt: 0.5 }}>
                                      <Chip
                                        size="small"
                                        label={`Dilution Factor \u00D7${spec.dilutionFactor}`}
                                        sx={{
                                          height: 20,
                                          fontSize: 11,
                                          color: "text.secondary",
                                          bgcolor: "action.hover",
                                          border: "1px solid",
                                          borderColor: "divider"
                                        }}
                                      />
                                    </Box>
                                  )}
                              </TableCell>
                              <TableCell sx={{ fontSize: 13 }}>
                                {spec.unit || "\u2014"}
                              </TableCell>
                              <TableCell sx={{ fontSize: 13 }}>
                                {spec.referenceStandard || "\u2014"}
                              </TableCell>
                              <TableCell align="right">
                                <Stack
                                  direction="row"
                                  spacing={0.5}
                                  sx={{ justifyContent: "flex-end" }}
                                >
                                  <IconButton
                                    size="small"
                                    onClick={() => handleOpenEdit(spec)}
                                    title="Edit Specification"
                                  >
                                    <EditIcon fontSize="small" />
                                  </IconButton>
                                  <IconButton
                                    size="small"
                                    color="error"
                                    onClick={() => setPendingDelete(spec)}
                                    title="Delete Specification"
                                  >
                                    <DeleteIcon fontSize="small" />
                                  </IconButton>
                                </Stack>
                              </TableCell>
                            </TableRow>

                            {/* Stages if MultiStage parameter */}
                            {isMultiStage &&
                              spec.stages?.map((stage, sIdx) => (
                                <TableRow
                                  key={`${spec.id ?? spec.testCode}-stage-${sIdx}`}
                                  sx={{ bgcolor: "action.hover" }}
                                >
                                  <TableCell sx={{ pl: 5.5 }}>
                                    <Stack
                                      direction="row"
                                      spacing={1}
                                      sx={{ alignItems: "center" }}
                                    >
                                      <Typography
                                        sx={{ color: "text.secondary", fontSize: 13 }}
                                      >
                                        &#8627;
                                      </Typography>
                                      <Typography sx={{ fontSize: 12 }}>
                                        {stage.stageLabel}
                                      </Typography>
                                    </Stack>
                                  </TableCell>
                                  <TableCell>
                                    <LimitTypeBadge type="StageCriteria" />
                                  </TableCell>
                                  <TableCell
                                    sx={{ fontSize: 13, color: "text.primary" }}
                                  >
                                    {stage.acceptanceCriteriaText}
                                  </TableCell>
                                  <TableCell sx={{ fontSize: 13 }}>
                                    {spec.unit || "\u2014"}
                                  </TableCell>
                                  <TableCell sx={{ fontSize: 13 }}>{"\u2014"}</TableCell>
                                  <TableCell align="right" />
                                </TableRow>
                              ))}
                          </React.Fragment>
                        );
                      })}

                      {/* Add parameter link for this group */}
                      <TableRow sx={{ "&:hover": { bgcolor: "transparent" } }}>
                        <TableCell
                          colSpan={6}
                          sx={{
                            pl: 3.5,
                            py: 0.75,
                            borderBottom: "1px solid",
                            borderColor: "divider"
                          }}
                        >
                          <Button
                            size="small"
                            startIcon={<AddIcon sx={{ fontSize: 16 }} />}
                            onClick={() => handleAddParameterToTest(group.testCode)}
                            sx={{
                              textTransform: "none",
                              fontSize: 12,
                              fontWeight: 600,
                              color: "primary.main",
                              p: 0
                            }}
                          >
                            + Add parameter to {testDisplayName}
                          </Button>
                        </TableCell>
                      </TableRow>
                    </React.Fragment>
                  );
                })
              )}
            </TableBody>
          </Table>
        </Box>
      )}

      {/* Add / Edit Parameter Dialog */}
      <SpecificationParameterDialog
        open={dialogOpen}
        item={item}
        editingSpec={editingSpec}
        preselectedTestCode={preselectedTestCode}
        workflowTypeByCode={workflowTypeByCode}
        existingSpecs={specs}
        onClose={() => setDialogOpen(false)}
        onSuccess={async () => {
          await loadSpecs();
          onSpecsChanged?.();
        }}
      />

      {/* Delete Confirmation Dialog */}
      <ConfirmationDialog
        open={pendingDelete != null}
        message={
          pendingDelete
            ? `Delete specification parameter "${pendingDelete.parameterName || pendingDelete.testCode}" for "${item.name}"? This cannot be undone.`
            : ""
        }
        onCancel={() => setPendingDelete(null)}
        onConfirm={handleDeleteConfirm}
        destructive
      />
    </Box>
  );
};
