import { useState, useEffect, useRef, type ReactNode } from "react";
import {
  Box,
  Paper,
  Typography,
  Grid,
  TextField,
  Button,
  Stack,
  Divider,
  IconButton,
  Tooltip,
  Alert,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import ScienceIcon from "@mui/icons-material/Science";
import { tableHeadSx } from "../../../theme";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import type {
  HplcMethodImpuritySnapshot,
  HplcMethodWeightDto,
  HplcReplicateDto,
  HplcReplicatePeakInput
} from "../types";

// The run snapshot's view of the method (taken from the sample entry), not the live method.
export interface RelatedSubstancesMethodView {
  analytes?: { id: number; name: string }[];
  impurities?: HplcMethodImpuritySnapshot[];
  referenceSolutionPercent?: number | null;
  disregardLimitPercent?: number | null;
}

export interface RelatedSubstancesPeakTableProps {
  method?: RelatedSubstancesMethodView | null;
  methodWeights?: HplcMethodWeightDto[];
  replicates: HplcReplicateDto[];
  onChange: (replicates: HplcReplicateDto[]) => void;
  requiredReplicates?: number | null;
  disabled?: boolean;
  footer?: ReactNode;
}

export function rsReplicatesComplete(
  replicates: HplcReplicateDto[],
  principalAnalyteId?: number | null
): boolean {
  if (!principalAnalyteId || replicates.length === 0) return false;
  return replicates.every((rep) => {
    const matched = rep.responses.find((r) => r.hplcMethodAnalyteId === principalAnalyteId);
    const refArea = matched?.response;
    if (typeof refArea !== "number" || !Number.isFinite(refArea) || refArea <= 0) {
      return false;
    }
    if (rep.peaks && rep.peaks.some((p) => typeof p.area === "number" && p.area < 0)) {
      return false;
    }
    if (
      rep.peaks &&
      rep.peaks.some(
        (p) =>
          p.hplcMethodImpurityId == null &&
          p.relativeRetention != null &&
          (p.relativeRetention <= 0 || !Number.isFinite(p.relativeRetention))
      )
    ) {
      return false;
    }
    return true;
  });
}

const parseDraft = (draft: string): number => {
  if (draft.trim() === "") return 0;
  const n = parseFloat(draft);
  return Number.isNaN(n) ? 0 : n;
};

let unspSeq = 0;
const nextUnspKey = () => `unsp-${Date.now()}-${++unspSeq}`;

export function RelatedSubstancesPeakTable({
  method,
  replicates,
  onChange,
  requiredReplicates,
  disabled = false,
  footer
}: RelatedSubstancesPeakTableProps) {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();
  const tableRef = useRef<HTMLDivElement>(null);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [rowKeys, setRowKeys] = useState<Record<number, string[]>>({});

  // Keep stable keys in state when rows are loaded or added, without mutating props during render
  useEffect(() => {
    setRowKeys((prev) => {
      let changed = false;
      const next = { ...prev };
      replicates.forEach((rep) => {
        const unspecifiedCount = (rep.peaks ?? []).filter((p) => p.hplcMethodImpurityId == null).length;
        const current = next[rep.replicateNo] || [];
        if (current.length !== unspecifiedCount) {
          changed = true;
          const updated: string[] = [];
          for (let i = 0; i < unspecifiedCount; i++) {
            updated.push(current[i] || nextUnspKey());
          }
          next[rep.replicateNo] = updated;
        }
      });
      return changed ? next : prev;
    });
  }, [replicates]);

  const analytes = method?.analytes ?? [];
  const hasExactlyOneAnalyte = analytes.length === 1;

  if (!hasExactlyOneAnalyte) {
    return (
      <Box ref={tableRef}>
        <Alert severity="error" sx={{ mb: 2 }}>
          {t("entry.rsPeakTable.oneAnalyteError")}
        </Alert>
        {footer}
      </Box>
    );
  }

  const principalAnalyte = analytes[0];
  const principalAnalyteId = principalAnalyte.id;
  const impurities = method?.impurities ?? [];

  const refKey = (repNo: number) => `${repNo}:ref`;
  const namedKey = (repNo: number, impId: number) => `${repNo}:imp${impId}`;
  const unspAreaKey = (repNo: number, clientKey: string) => `${repNo}:unsp_area_${clientKey}`;
  const unspRrtKey = (repNo: number, clientKey: string) => `${repNo}:unsp_rrt_${clientKey}`;

  const setDraft = (key: string, value: string) => setDrafts((d) => ({ ...d, [key]: value }));

  const cellText = (key: string, modelValue: number | null | undefined): string => {
    const draft = drafts[key];
    if (draft !== undefined) {
      if (draft.trim() === "" && (modelValue === 0 || modelValue == null)) return draft;
      if (parseDraft(draft) === modelValue) return draft;
    }
    if (modelValue == null || modelValue === 0) return "";
    return String(modelValue);
  };

  const handleAddReplicate = () => {
    const nextNo = replicates.length + 1;
    const newReplicate: HplcReplicateDto = {
      replicateNo: nextNo,
      actualWeightMg: 0,
      responses: [{ hplcMethodAnalyteId: principalAnalyteId, response: 0 }],
      peaks: impurities.map((imp) => ({
        hplcMethodImpurityId: imp.id,
        relativeRetention: imp.relativeRetention ?? null,
        area: 0
      }))
    };
    onChange([...replicates, newReplicate]);
  };

  const handleRemoveReplicate = (indexToRemove: number) => {
    setDrafts({});
    const updated = replicates
      .filter((_, idx) => idx !== indexToRemove)
      .map((rep, idx) => ({ ...rep, replicateNo: idx + 1 }));
    onChange(updated);
  };

  const handleRefAreaChange = (repIndex: number, rawVal: string) => {
    const rep = replicates[repIndex];
    setDraft(refKey(rep.replicateNo), rawVal);
    const parsed = parseDraft(rawVal);
    const updated = replicates.map((r, idx) => {
      if (idx !== repIndex) return r;
      const existing = r.responses.some((resp) => resp.hplcMethodAnalyteId === principalAnalyteId);
      const newResp = { hplcMethodAnalyteId: principalAnalyteId, response: parsed };
      return {
        ...r,
        responses: existing
          ? r.responses.map((resp) => (resp.hplcMethodAnalyteId === principalAnalyteId ? newResp : resp))
          : [...r.responses, newResp]
      };
    });
    onChange(updated);
  };

  const handleNamedAreaChange = (
    repIndex: number,
    impId: number,
    rrt: number | null | undefined,
    rawVal: string
  ) => {
    const rep = replicates[repIndex];
    setDraft(namedKey(rep.replicateNo, impId), rawVal);
    const parsed = parseDraft(rawVal);
    const updated = replicates.map((r, idx) => {
      if (idx !== repIndex) return r;
      const peaks = r.peaks ?? [];
      const exists = peaks.some((p) => p.hplcMethodImpurityId === impId);
      const newPeak: HplcReplicatePeakInput = {
        hplcMethodImpurityId: impId,
        relativeRetention: rrt ?? null,
        area: parsed
      };
      return {
        ...r,
        peaks: exists
          ? peaks.map((p) => (p.hplcMethodImpurityId === impId ? newPeak : p))
          : [...peaks, newPeak]
      };
    });
    onChange(updated);
  };

  const handleAddUnspecifiedPeak = (repIndex: number) => {
    const assignedKey = nextUnspKey();
    const rep = replicates[repIndex];
    setRowKeys((prev) => ({
      ...prev,
      [rep.replicateNo]: [...(prev[rep.replicateNo] || []), assignedKey]
    }));

    const updated = replicates.map((r, idx) => {
      if (idx !== repIndex) return r;
      const peaks = r.peaks ?? [];
      const newPeak: HplcReplicatePeakInput = {
        clientKey: assignedKey,
        hplcMethodImpurityId: null,
        relativeRetention: null,
        area: 0
      };
      return {
        ...r,
        peaks: [...peaks, newPeak]
      };
    });
    onChange(updated);
  };

  const handleRemoveUnspecifiedPeak = (repIndex: number, clientKey: string, unspIdx: number) => {
    setDrafts({});
    const rep = replicates[repIndex];
    setRowKeys((prev) => {
      const current = prev[rep.replicateNo] || [];
      return {
        ...prev,
        [rep.replicateNo]: current.filter((k, idx) => (k !== clientKey && idx !== unspIdx))
      };
    });

    const updated = replicates.map((r, idx) => {
      if (idx !== repIndex) return r;
      const peaks = r.peaks ?? [];
      let currentUnsp = 0;
      const filtered = peaks.filter((p) => {
        if (p.hplcMethodImpurityId == null) {
          const match = p.clientKey ? p.clientKey === clientKey : currentUnsp === unspIdx;
          currentUnsp++;
          return !match;
        }
        return true;
      });
      return {
        ...r,
        peaks: filtered
      };
    });
    onChange(updated);
  };

  const handleUnspecifiedAreaChange = (repIndex: number, clientKey: string, rawVal: string) => {
    const rep = replicates[repIndex];
    setDraft(unspAreaKey(rep.replicateNo, clientKey), rawVal);
    const parsed = parseDraft(rawVal);
    const updated = replicates.map((r, idx) => {
      if (idx !== repIndex) return r;
      const peaks = r.peaks ?? [];
      let currentUnsp = 0;
      const updatedPeaks = peaks.map((p) => {
        if (p.hplcMethodImpurityId == null) {
          const key = p.clientKey || rowKeys[r.replicateNo]?.[currentUnsp] || `unsp-${r.replicateNo}-${currentUnsp}`;
          currentUnsp++;
          if (key === clientKey) {
            return { ...p, area: parsed };
          }
        }
        return p;
      });
      return { ...r, peaks: updatedPeaks };
    });
    onChange(updated);
  };

  const handleUnspecifiedRrtChange = (repIndex: number, clientKey: string, rawVal: string) => {
    const rep = replicates[repIndex];
    setDraft(unspRrtKey(rep.replicateNo, clientKey), rawVal);
    const parsed = rawVal.trim() === "" ? null : parseFloat(rawVal);
    const updated = replicates.map((r, idx) => {
      if (idx !== repIndex) return r;
      const peaks = r.peaks ?? [];
      let currentUnsp = 0;
      const updatedPeaks = peaks.map((p) => {
        if (p.hplcMethodImpurityId == null) {
          const key = p.clientKey || rowKeys[r.replicateNo]?.[currentUnsp] || `unsp-${r.replicateNo}-${currentUnsp}`;
          currentUnsp++;
          if (key === clientKey) {
            return { ...p, relativeRetention: Number.isNaN(parsed) ? null : parsed };
          }
        }
        return p;
      });
      return { ...r, peaks: updatedPeaks };
    });
    onChange(updated);
  };

  const areaHelper = (val: number, isRef = false): string | undefined => {
    if (disabled) return undefined;
    if (val < 0) return t("entry.rsPeakTable.negAreaError");
    if (isRef && val === 0) return t("entry.rsPeakTable.required");
    return undefined;
  };

  return (
    <Box ref={tableRef}>
      {/* Top action header */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1.5, mb: 2 }}>
        <Box>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            {requiredReplicates != null && (
              <StatusBadge
                status={replicates.length >= requiredReplicates ? "Completed" : "Pending"}
                label={t("entry.rsPeakTable.requiredBadge", { count: requiredReplicates })}
              />
            )}
            <StatusBadge status="Pending" label={t("entry.rsPeakTable.enteredBadge", { count: replicates.length })} />
            {method?.referenceSolutionPercent != null && (
              <StatusBadge status="Assigned" label={t("entry.rsPeakTable.referenceBadge", { percent: method.referenceSolutionPercent })} />
            )}
            {method?.disregardLimitPercent != null && (
              <StatusBadge status="Assigned" label={t("entry.rsPeakTable.disregardBadge", { percent: method.disregardLimitPercent })} />
            )}
          </Box>
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
            {t("entry.rsPeakTable.instructions")}
          </Typography>
        </Box>

        <Button
          variant="outlined"
          color="primary"
          size="small"
          startIcon={<AddIcon />}
          onClick={handleAddReplicate}
          disabled={disabled}
        >
          {t("entry.rsPeakTable.addReplicate")}
        </Button>
      </Box>

      {replicates.length === 0 ? (
        <Paper elevation={0} sx={{ p: 4, textAlign: "center", border: `1px solid ${theme.palette.divider}`, borderRadius: 2 }}>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {t("entry.rsPeakTable.empty")}
          </Typography>
        </Paper>
      ) : (
        <Stack spacing={3}>
          {replicates.map((rep, repIdx) => {
            const refResp =
              rep.responses.find((r) => r.hplcMethodAnalyteId === principalAnalyteId)?.response ?? 0;
            const unspecifiedPeaks = (rep.peaks ?? []).filter((p) => p.hplcMethodImpurityId == null);

            return (
              <Paper
                key={rep.replicateNo}
                elevation={0}
                sx={{
                  p: 2.5,
                  borderRadius: 2,
                  border: `1px solid ${theme.palette.divider}`,
                  backgroundColor: theme.palette.background.paper
                }}
              >
                {/* Replicate title bar */}
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    <ScienceIcon color="primary" />
                    <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 16 }}>
                      {t("entry.rsPeakTable.replicateTitle", { repNo: rep.replicateNo })}
                    </Typography>
                  </Box>
                  <Tooltip title={replicates.length <= 1 ? t("entry.rsPeakTable.minOneTooltip") : t("entry.rsPeakTable.removeTooltip")}>
                    <span>
                      <IconButton
                        size="small"
                        color="error"
                        disabled={disabled || replicates.length <= 1}
                        onClick={() => handleRemoveReplicate(repIdx)}
                        aria-label={t("entry.rsPeakTable.removeAria", { repNo: rep.replicateNo })}
                      >
                        <DeleteOutlineIcon fontSize="small" />
                      </IconButton>
                    </span>
                  </Tooltip>
                </Box>

                <Divider sx={{ mb: 2 }} />

                {/* Section 1: Reference solution area */}
                <Box sx={{ mb: 3 }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                    {t("entry.rsPeakTable.refAreaSection")}
                  </Typography>
                  <Grid container spacing={2}>
                    <Grid size={{ xs: 12, sm: 6, md: 4 }}>
                      <TextField
                        fullWidth
                        size="small"
                        label={t("entry.rsPeakTable.refAreaLabel")}
                        type="number"
                        disabled={disabled}
                        value={cellText(refKey(rep.replicateNo), refResp)}
                        onChange={(e) => handleRefAreaChange(repIdx, e.target.value)}
                        placeholder="0.00"
                        error={!disabled && (refResp < 0 || (refResp === 0 && Boolean(drafts[refKey(rep.replicateNo)])))}
                        helperText={areaHelper(refResp, true)}
                        slotProps={{
                          htmlInput: {
                            min: 0,
                            step: "any",
                            "aria-label": t("entry.rsPeakTable.refAreaAria", { repNo: rep.replicateNo })
                          }
                        }}
                      />
                    </Grid>
                  </Grid>
                </Box>

                {/* Section 2: Named Impurities */}
                {impurities.length > 0 && (
                  <Box sx={{ mb: 3 }}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                      {t("entry.rsPeakTable.namedImpurities")}
                    </Typography>
                    <Grid container spacing={2}>
                      {impurities.map((imp) => {
                        const peak = rep.peaks?.find((p) => p.hplcMethodImpurityId === imp.id);
                        const area = peak?.area ?? 0;
                        const labelText = `${imp.name}${imp.relativeRetention != null ? t("entry.rsPeakTable.rrtSuffix", { rrt: imp.relativeRetention }) : ""}`;

                        return (
                          <Grid size={{ xs: 12, sm: 6, md: 4 }} key={imp.id}>
                            <TextField
                              fullWidth
                              size="small"
                              label={labelText}
                              type="number"
                              disabled={disabled}
                              value={cellText(namedKey(rep.replicateNo, imp.id), area)}
                              onChange={(e) =>
                                handleNamedAreaChange(repIdx, imp.id, imp.relativeRetention, e.target.value)
                              }
                              placeholder="0.00"
                              error={!disabled && area < 0}
                              helperText={areaHelper(area)}
                              slotProps={{
                                htmlInput: {
                                  min: 0,
                                  step: "any",
                                  "aria-label": t("entry.rsPeakTable.areaAria", { label: labelText })
                                }
                              }}
                            />
                          </Grid>
                        );
                      })}
                    </Grid>
                  </Box>
                )}

                {/* Section 3: Unspecified Peaks */}
                <Box>
                  <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                      {t("entry.rsPeakTable.unspecifiedPeaks")}
                    </Typography>
                    <Button
                      size="small"
                      startIcon={<AddIcon />}
                      onClick={() => handleAddUnspecifiedPeak(repIdx)}
                      disabled={disabled}
                      sx={{ textTransform: "none", fontWeight: 600 }}
                    >
                      {t("entry.rsPeakTable.addUnspecifiedPeak")}
                    </Button>
                  </Box>

                  {unspecifiedPeaks.length === 0 ? (
                    <Typography variant="body2" sx={{ color: "text.secondary", py: 1 }}>
                      {t("entry.rsPeakTable.noUnspecifiedPeaks")}
                    </Typography>
                  ) : (
                    <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
                      <Table size="small">
                        <TableHead sx={tableHeadSx(theme)}>
                          <TableRow>
                            <TableCell sx={{ width: 80 }}>{t("entry.rsPeakTable.columns.peak")}</TableCell>
                            <TableCell sx={{ minWidth: 160 }}>{t("entry.rsPeakTable.columns.rrt")}</TableCell>
                            <TableCell sx={{ minWidth: 180 }}>{t("entry.rsPeakTable.columns.area")}</TableCell>
                            <TableCell align="right" sx={{ width: 60 }}>{t("entry.rsPeakTable.columns.action")}</TableCell>
                          </TableRow>
                        </TableHead>
                        <TableBody>
                          {unspecifiedPeaks.map((p, unspIdx) => {
                            const clientKey =
                              p.clientKey ||
                              rowKeys[rep.replicateNo]?.[unspIdx] ||
                              `unsp-${rep.replicateNo}-${unspIdx}`;
                            const hasInvalidRrt = p.relativeRetention != null && p.relativeRetention <= 0;

                            return (
                              <TableRow key={clientKey} hover>
                                <TableCell sx={{ fontWeight: 600 }}><Ltr>#{unspIdx + 1}</Ltr></TableCell>
                                <TableCell>
                                  <TextField
                                    size="small"
                                    type="number"
                                    disabled={disabled}
                                    placeholder={t("entry.rsPeakTable.optionalRrt")}
                                    value={cellText(unspRrtKey(rep.replicateNo, clientKey), p.relativeRetention)}
                                    onChange={(e) => handleUnspecifiedRrtChange(repIdx, clientKey, e.target.value)}
                                    error={!disabled && hasInvalidRrt}
                                    helperText={!disabled && hasInvalidRrt ? t("entry.rsPeakTable.mustBePositive") : undefined}
                                    slotProps={{
                                      htmlInput: {
                                        min: 0,
                                        step: "any",
                                        "aria-label": t("entry.rsPeakTable.unspRrtAria", { num: unspIdx + 1 })
                                      }
                                    }}
                                    sx={{ maxWidth: 180 }}
                                  />
                                </TableCell>
                                <TableCell>
                                  <TextField
                                    size="small"
                                    type="number"
                                    disabled={disabled}
                                    placeholder="0.00"
                                    value={cellText(unspAreaKey(rep.replicateNo, clientKey), p.area)}
                                    onChange={(e) => handleUnspecifiedAreaChange(repIdx, clientKey, e.target.value)}
                                    error={!disabled && p.area < 0}
                                    helperText={areaHelper(p.area)}
                                    slotProps={{
                                      htmlInput: {
                                        min: 0,
                                        step: "any",
                                        "aria-label": t("entry.rsPeakTable.unspAreaAria", { num: unspIdx + 1 })
                                      }
                                    }}
                                    sx={{ maxWidth: 220 }}
                                  />
                                </TableCell>
                                <TableCell align="right">
                                  <IconButton
                                    size="small"
                                    color="error"
                                    disabled={disabled}
                                    onClick={() => handleRemoveUnspecifiedPeak(repIdx, clientKey, unspIdx)}
                                    aria-label={t("entry.rsPeakTable.removeUnspAria", { num: unspIdx + 1 })}
                                  >
                                    <DeleteOutlineIcon fontSize="small" />
                                  </IconButton>
                                </TableCell>
                              </TableRow>
                            );
                          })}
                        </TableBody>
                      </Table>
                    </TableContainer>
                  )}
                </Box>
              </Paper>
            );
          })}
        </Stack>
      )}

      {footer}
    </Box>
  );
}
