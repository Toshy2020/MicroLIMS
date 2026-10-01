import { useRef, useState } from "react";
import type { KeyboardEvent, ReactNode } from "react";
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
import type { HplcMethodWeightDto, HplcReplicateDto } from "../types";

export interface ReplicateEntryTableProps {
  methodWeights: HplcMethodWeightDto[];
  replicates: HplcReplicateDto[];
  onChange: (replicates: HplcReplicateDto[]) => void;
  requiredReplicates?: number | null;
  disabled?: boolean;
  // Rendered under the table (the page puts the sticky Save button here).
  footer?: ReactNode;
}

const isPositive = (n: number | undefined) => typeof n === "number" && Number.isFinite(n) && n > 0;

// True when every weight and every analyte response is a positive number.
// A cleared cell is held as 0 in the model, so it counts as missing.
export function replicatesComplete(methodWeights: HplcMethodWeightDto[], replicates: HplcReplicateDto[]): boolean {
  if (replicates.length === 0) return false;
  return replicates.every(
    (rep) =>
      isPositive(rep.actualWeightMg) &&
      methodWeights.every((mw) =>
        isPositive(rep.responses.find((r) => r.hplcMethodAnalyteId === mw.hplcMethodAnalyteId)?.response)
      )
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
  footer
}: ReplicateEntryTableProps) {
  const theme = useTheme();
  const tableRef = useRef<HTMLDivElement>(null);
  // Per-cell text as typed, so a cleared cell stays empty instead of showing 0.
  const [drafts, setDrafts] = useState<Record<string, string>>({});

  const weightKey = (repNo: number) => `${repNo}:w`;
  const respKey = (repNo: number, analyteId: number) => `${repNo}:a${analyteId}`;

  // A draft is only shown while it still matches the model value; after a
  // server refresh (or row removal) the model value wins.
  const cellText = (key: string, modelValue: number): string => {
    const draft = drafts[key];
    if (draft !== undefined && parseDraft(draft) === modelValue) return draft;
    return modelValue === 0 ? "" : String(modelValue);
  };

  const setDraft = (key: string, value: string) => setDrafts((d) => ({ ...d, [key]: value }));

  const handleAddRow = () => {
    const nextNo = replicates.length + 1;
    const newReplicate: HplcReplicateDto = {
      replicateNo: nextNo,
      actualWeightMg: 0,
      responses: methodWeights.map((mw) => ({ hplcMethodAnalyteId: mw.hplcMethodAnalyteId, response: 0 }))
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

  // Enter moves to the same column in the next replicate.
  const handleEnter = (e: KeyboardEvent, col: string, rowIdx: number) => {
    if (e.key !== "Enter") return;
    e.preventDefault();
    const next = tableRef.current?.querySelector<HTMLInputElement>(`input[data-cell="${col}-${rowIdx + 1}"]`);
    next?.focus();
    next?.select();
  };

  const helper = (value: number): string | undefined => {
    if (disabled) return undefined;
    if (value === 0) return "Required";
    if (!(value > 0)) return "Must be > 0";
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
                label={`Required: ${requiredReplicates}`}
              />
            )}
            <StatusBadge status="Pending" label={`${replicates.length} entered`} />
          </Box>
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
            Record actual test weight (mg) and peak response per analyte for each replicate injection. Press Enter to move to the next replicate.
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
          Add Replicate
        </Button>
      </Box>

      <TableContainer ref={tableRef} sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
        <Table size="small">
          <TableHead sx={tableHeadSx(theme)}>
            <TableRow>
              <TableCell sx={{ width: 80 }}>Rep #</TableCell>
              <TableCell align="right" sx={{ minWidth: 160 }}>Actual Weight *</TableCell>
              {methodWeights.map((mw) => (
                <TableCell key={mw.hplcMethodAnalyteId} align="right" sx={{ minWidth: 180 }}>
                  {mw.analyteName} Response *
                </TableCell>
              ))}
              <TableCell align="right" sx={{ width: 60 }}>Action</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {replicates.length === 0 ? (
              <TableRow>
                <TableCell
                  colSpan={3 + methodWeights.length}
                  sx={{ textAlign: "center", py: 4, color: "text.secondary" }}
                >
                  No replicates added. Click &ldquo;Add Replicate&rdquo; to start entering replicate test data.
                </TableCell>
              </TableRow>
            ) : (
              replicates.map((rep, repIdx) => (
                <TableRow key={rep.replicateNo} hover>
                  <TableCell sx={{ fontWeight: 700 }}>#{rep.replicateNo}</TableCell>
                  <TableCell align="right">
                    <TextField
                      type="number"
                      size="small"
                      value={cellText(weightKey(rep.replicateNo), rep.actualWeightMg)}
                      onChange={(e) => handleWeightChange(repIdx, e.target.value)}
                      onKeyDown={(e) => handleEnter(e, "w", repIdx)}
                      placeholder="0.00"
                      disabled={disabled}
                      error={!disabled && !isPositive(rep.actualWeightMg)}
                      helperText={helper(rep.actualWeightMg)}
                      slotProps={{
                        htmlInput: { min: 0, step: "any", "data-cell": `w-${repIdx}`, "aria-label": `Replicate ${rep.replicateNo} actual weight (mg)` },
                        input: { endAdornment: <InputAdornment position="end">mg</InputAdornment> }
                      }}
                      sx={cellSx}
                    />
                  </TableCell>
                  {methodWeights.map((mw) => {
                    const val =
                      rep.responses.find((r) => r.hplcMethodAnalyteId === mw.hplcMethodAnalyteId)?.response ?? 0;
                    return (
                      <TableCell key={mw.hplcMethodAnalyteId} align="right">
                        <TextField
                          type="number"
                          size="small"
                          value={cellText(respKey(rep.replicateNo, mw.hplcMethodAnalyteId), val)}
                          onChange={(e) => handleResponseChange(repIdx, mw.hplcMethodAnalyteId, e.target.value)}
                          onKeyDown={(e) => handleEnter(e, `a${mw.hplcMethodAnalyteId}`, repIdx)}
                          placeholder="0.00"
                          disabled={disabled}
                          error={!disabled && !isPositive(val)}
                          helperText={helper(val)}
                          slotProps={{
                            htmlInput: {
                              min: 0,
                              step: "any",
                              "data-cell": `a${mw.hplcMethodAnalyteId}-${repIdx}`,
                              "aria-label": `Replicate ${rep.replicateNo} ${mw.analyteName} response`
                            }
                          }}
                          sx={cellSx}
                        />
                      </TableCell>
                    );
                  })}
                  <TableCell align="right">
                    <Tooltip title={replicates.length <= 1 ? "At least one replicate is required" : "Remove replicate"}>
                      <span>
                        <IconButton
                          size="small"
                          color="error"
                          disabled={disabled || replicates.length <= 1}
                          onClick={() => handleRemoveRow(repIdx)}
                        >
                          <DeleteOutlineIcon fontSize="small" />
                        </IconButton>
                      </span>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>
      {footer}
    </Box>
  );
}
