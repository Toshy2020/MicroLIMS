import { useState } from "react";
import type { ReactNode } from "react";
import {
  Box,
  Typography,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  InputAdornment,
  IconButton,
  Tooltip,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import { tableHeadSx } from "../../../theme";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { useGridNavigation } from "../../../components/entry/useGridNavigation";
import type { HplcMethodWeightDto, HplcReplicateDto } from "../types";

export interface ReplicateEntryTableProps {
  methodWeights: HplcMethodWeightDto[];
  replicates: HplcReplicateDto[];
  onChange: (replicates: HplcReplicateDto[]) => void;
  requiredReplicates?: number | null;
  disabled?: boolean;
  isResidualSolvents?: boolean;
  hasInternalStandard?: boolean;
  internalStandardName?: string | null;
  onSave?: () => void;
  // Rendered under the table (the page puts the sticky Save button here).
  footer?: ReactNode;
}

const isPositive = (n: number | null | undefined) => typeof n === "number" && Number.isFinite(n) && n > 0;
const isNonNegative = (n: number | null | undefined) => typeof n === "number" && Number.isFinite(n) && n >= 0;

// True when every weight and every analyte response is valid.
// In ResidualSolvents mode, response 0 is allowed (= not detected).
// In internal-standard mode, internalStandardResponse must be > 0.
export function replicatesComplete(
  methodWeights: HplcMethodWeightDto[],
  replicates: HplcReplicateDto[],
  isResidualSolvents = false,
  hasInternalStandard = false
): boolean {
  if (replicates.length === 0) return false;
  return replicates.every(
    (rep) =>
      isPositive(rep.actualWeightMg) &&
      (!hasInternalStandard || isPositive(rep.internalStandardResponse)) &&
      methodWeights.every((mw) => {
        const resp = rep.responses.find((r) => r.hplcMethodAnalyteId === mw.hplcMethodAnalyteId)?.response;
        return isResidualSolvents ? isNonNegative(resp) : isPositive(resp);
      })
  );
}

const parseDraft = (draft: string): number => {
  if (draft.trim() === "") return 0;
  const n = parseFloat(draft);
  return Number.isNaN(n) ? 0 : n;
};

export function ReplicateEntryTable({
  methodWeights,
  replicates,
  onChange,
  requiredReplicates,
  disabled = false,
  isResidualSolvents = false,
  hasInternalStandard = false,
  internalStandardName,
  onSave,
  footer
}: ReplicateEntryTableProps) {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();
  // Per-cell text as typed, so a cleared cell stays empty instead of showing 0.
  const [drafts, setDrafts] = useState<Record<string, string>>({});

  const weightKey = (repNo: number) => `${repNo}:w`;
  const respKey = (repNo: number, analyteId: number) => `${repNo}:a${analyteId}`;
  const isKey = (repNo: number) => `${repNo}:is`;

  // A draft is only shown while it still matches the model value; after a
  // server refresh (or row removal) the model value wins.
  const cellText = (key: string, modelValue: number, isResidualResponse = false): string => {
    const draft = drafts[key];
    if (draft !== undefined && parseDraft(draft) === modelValue) return draft;
    if (isResidualResponse) {
      return String(modelValue);
    }
    return modelValue === 0 ? "" : String(modelValue);
  };

  const setDraft = (key: string, value: string) => setDrafts((d) => ({ ...d, [key]: value }));

  const handleAddRow = () => {
    const nextNo = replicates.length + 1;
    const newReplicate: HplcReplicateDto = {
      replicateNo: nextNo,
      actualWeightMg: 0,
      responses: methodWeights.map((mw) => ({ hplcMethodAnalyteId: mw.hplcMethodAnalyteId, response: 0 })),
      internalStandardResponse: hasInternalStandard ? 0 : undefined
    };
    onChange([...replicates, newReplicate]);
  };

  const handleRemoveRow = (indexToRemove: number) => {
    setDrafts({});
    const updated = replicates
      .filter((_, idx) => idx !== indexToRemove)
      .map((rep, idx) => ({ ...rep, replicateNo: idx + 1 }));
    onChange(updated);
  };

  const handleWeightChange = (index: number, rawVal: string) => {
    setDraft(weightKey(replicates[index].replicateNo), rawVal);
    const parsed = parseDraft(rawVal);
    onChange(replicates.map((rep, idx) => (idx !== index ? rep : { ...rep, actualWeightMg: parsed })));
  };

  const handleResponseChange = (repIndex: number, analyteId: number, rawVal: string) => {
    setDraft(respKey(replicates[repIndex].replicateNo, analyteId), rawVal);
    const parsed = parseDraft(rawVal);
    const updated = replicates.map((rep, idx) => {
      if (idx !== repIndex) return rep;
      const exists = rep.responses.some((r) => r.hplcMethodAnalyteId === analyteId);
      const newResp = { hplcMethodAnalyteId: analyteId, response: parsed };
      return {
        ...rep,
        responses: exists
          ? rep.responses.map((r) => (r.hplcMethodAnalyteId === analyteId ? newResp : r))
          : [...rep.responses, newResp]
      };
    });
    onChange(updated);
  };

  const handleIsResponseChange = (repIndex: number, rawVal: string) => {
    setDraft(isKey(replicates[repIndex].replicateNo), rawVal);
    const parsed = parseDraft(rawVal);
    const updated = replicates.map((rep, idx) =>
      idx !== repIndex ? rep : { ...rep, internalStandardResponse: parsed }
    );
    onChange(updated);
  };

  const { cellProps } = useGridNavigation({
    rows: replicates.length,
    cols: 1 + methodWeights.length + (hasInternalStandard ? 1 : 0),
    isEditable: () => !disabled,
    selectOnFocus: true,
    onSave
  });

  const helper = (value: number, isResidualResponse = false): string | undefined => {
    if (disabled) return undefined;
    if (isResidualResponse) {
      if (value === 0) return t("entry.replicateTable.helperZeroNd");
      if (value < 0) return t("entry.replicateTable.helperNonNeg");
      return undefined;
    }
    if (value === 0) return t("entry.replicateTable.helperRequired");
    if (!(value > 0)) return t("entry.replicateTable.helperPositive");
    return undefined;
  };

  const cellSx = { width: "100%", maxWidth: 200, "& input": { textAlign: "right", fontVariantNumeric: "tabular-nums" } };

  return (
    <Box>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1.5, mb: 1.5 }}>
        <Box>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            {requiredReplicates !== undefined && requiredReplicates !== null && (
              <StatusBadge
                status={replicates.length >= requiredReplicates ? "Completed" : "Pending"}
                label={t("entry.replicateTable.requiredBadge", { count: requiredReplicates })}
              />
            )}
            <StatusBadge status="Pending" label={t("entry.replicateTable.enteredBadge", { count: replicates.length })} />
            {hasInternalStandard && (
              <StatusBadge
                status="Assigned"
                label={internalStandardName ? t("entry.replicateTable.isBadge", { name: internalStandardName }) : t("entry.replicateTable.isBadgeFallback")}
              />
            )}
          </Box>
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
            {hasInternalStandard
              ? t("entry.replicateTable.instructionsIs")
              : isResidualSolvents
              ? t("entry.replicateTable.instructionsResidual")
              : t("entry.replicateTable.instructionsDefault")}
          </Typography>
        </Box>

        <Button
          variant="outlined"
          color="primary"
          size="small"
          startIcon={<AddIcon />}
          onClick={handleAddRow}
          disabled={disabled}
        >
          {t("entry.replicateTable.addReplicate")}
        </Button>
      </Box>

      <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
        <Table size="small">
          <TableHead sx={tableHeadSx(theme)}>
            <TableRow>
              <TableCell sx={{ width: 80 }}>{t("entry.replicateTable.columns.rep")}</TableCell>
              <TableCell align="right" sx={{ minWidth: 160 }}>
                {isResidualSolvents ? t("entry.replicateTable.columns.sampleWeight") : t("entry.replicateTable.columns.actualWeight")}
              </TableCell>
              {methodWeights.map((mw) => (
                <TableCell key={mw.hplcMethodAnalyteId} align="right" sx={{ minWidth: 180 }}>
                  {hasInternalStandard
                    ? t("entry.replicateTable.columns.analyteArea")
                    : isResidualSolvents
                    ? t("entry.replicateTable.columns.solventArea", { analyte: mw.analyteName })
                    : t("entry.replicateTable.columns.analyteResponse", { analyte: mw.analyteName })}
                </TableCell>
              ))}
              {hasInternalStandard && (
                <TableCell align="right" sx={{ minWidth: 180 }}>
                  {internalStandardName
                    ? t("entry.replicateTable.columns.namedIsArea", { name: internalStandardName })
                    : t("entry.replicateTable.columns.isArea")}
                </TableCell>
              )}
              <TableCell align="right" sx={{ width: 60 }}>{t("entry.replicateTable.columns.action")}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {replicates.length === 0 ? (
              <TableRow>
                <TableCell
                  colSpan={3 + methodWeights.length + (hasInternalStandard ? 1 : 0)}
                  sx={{ textAlign: "center", py: 4, color: "text.secondary" }}
                >
                  {t("entry.replicateTable.empty")}
                </TableCell>
              </TableRow>
            ) : (
              replicates.map((rep, repIdx) => {
                const weightNav = cellProps(repIdx, 0);
                const isNav = cellProps(repIdx, 1 + methodWeights.length);
                return (
                <TableRow key={rep.replicateNo} hover>
                  <TableCell sx={{ fontWeight: 700 }}><Ltr>#{rep.replicateNo}</Ltr></TableCell>
                  <TableCell align="right">
                    <TextField
                      type="number"
                      size="small"
                      value={cellText(weightKey(rep.replicateNo), rep.actualWeightMg)}
                      onChange={(e) => handleWeightChange(repIdx, e.target.value)}
                      onKeyDown={weightNav.onKeyDown}
                      inputRef={weightNav.ref}
                      placeholder="0.00"
                      disabled={disabled}
                      error={!disabled && !isPositive(rep.actualWeightMg)}
                      helperText={helper(rep.actualWeightMg)}
                      slotProps={{
                        htmlInput: {
                          min: 0,
                          step: "any",
                          "data-cell": `w-${repIdx}`,
                          "data-grid-cell": weightNav["data-grid-cell"],
                          "aria-label": isResidualSolvents
                            ? t("entry.replicateTable.ariaSampleWeight", { repNo: rep.replicateNo })
                            : t("entry.replicateTable.ariaActualWeight", { repNo: rep.replicateNo })
                        },
                        input: { endAdornment: <InputAdornment position="end">mg</InputAdornment> }
                      }}
                      sx={cellSx}
                    />
                  </TableCell>
                  {methodWeights.map((mw, mwIdx) => {
                    const val =
                      rep.responses.find((r) => r.hplcMethodAnalyteId === mw.hplcMethodAnalyteId)?.response ?? 0;
                    const respNav = cellProps(repIdx, 1 + mwIdx);
                    return (
                      <TableCell key={mw.hplcMethodAnalyteId} align="right">
                        <TextField
                          type="number"
                          size="small"
                          value={cellText(respKey(rep.replicateNo, mw.hplcMethodAnalyteId), val, isResidualSolvents)}
                          onChange={(e) => handleResponseChange(repIdx, mw.hplcMethodAnalyteId, e.target.value)}
                          onKeyDown={respNav.onKeyDown}
                          inputRef={respNav.ref}
                          placeholder="0.00"
                          disabled={disabled}
                          error={!disabled && (isResidualSolvents ? val < 0 : !isPositive(val))}
                          helperText={helper(val, isResidualSolvents)}
                          slotProps={{
                            htmlInput: {
                              min: 0,
                              step: "any",
                              "data-cell": `a${mw.hplcMethodAnalyteId}-${repIdx}`,
                              "data-grid-cell": respNav["data-grid-cell"],
                              "aria-label": hasInternalStandard
                                ? t("entry.replicateTable.ariaAnalyteArea", { repNo: rep.replicateNo })
                                : isResidualSolvents
                                ? t("entry.replicateTable.ariaSolventArea", { repNo: rep.replicateNo, analyte: mw.analyteName })
                                : t("entry.replicateTable.ariaAnalyteResponse", { repNo: rep.replicateNo, analyte: mw.analyteName })
                            }
                          }}
                          sx={cellSx}
                        />
                      </TableCell>
                    );
                  })}
                  {hasInternalStandard && (
                    <TableCell align="right">
                      <TextField
                        type="number"
                        size="small"
                        value={cellText(isKey(rep.replicateNo), rep.internalStandardResponse ?? 0)}
                        onChange={(e) => handleIsResponseChange(repIdx, e.target.value)}
                        onKeyDown={isNav.onKeyDown}
                        inputRef={isNav.ref}
                        placeholder="0.00"
                        disabled={disabled}
                        error={!disabled && !isPositive(rep.internalStandardResponse)}
                        helperText={helper(rep.internalStandardResponse ?? 0)}
                        slotProps={{
                          htmlInput: {
                            min: 0,
                            step: "any",
                            "data-cell": `is-${repIdx}`,
                            "data-grid-cell": isNav["data-grid-cell"],
                            "aria-label": t("entry.replicateTable.ariaIsArea", { repNo: rep.replicateNo })
                          }
                        }}
                        sx={cellSx}
                      />
                    </TableCell>
                  )}
                  <TableCell align="right">
                    <Tooltip title={replicates.length <= 1 ? t("entry.replicateTable.minOneTooltip") : t("entry.replicateTable.removeTooltip")}>
                      <span>
                        <IconButton
                          size="small"
                          color="error"
                          disabled={disabled || replicates.length <= 1}
                          onClick={() => handleRemoveRow(repIdx)}
                          aria-label={t("entry.replicateTable.removeAria", { num: repIdx + 1 })}
                        >
                          <DeleteOutlineIcon fontSize="small" />
                        </IconButton>
                      </span>
                    </Tooltip>
                  </TableCell>
                </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>
      {footer}
    </Box>
  );
}
