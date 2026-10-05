import { useRef } from "react";
import type { KeyboardEvent, ReactNode } from "react";
import {
  Box,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  IconButton,
  Tooltip,
  Typography,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import { tableHeadSx } from "../../../theme";
import type {
  IcpAmountUnit,
  IcpElementStateDto,
  IcpReplicateDto
} from "../types";

export interface IcpReplicateEntryTableProps {
  elements: IcpElementStateDto[];
  replicates: IcpReplicateDto[];
  onChange: (replicates: IcpReplicateDto[]) => void;
  amountUnit: IcpAmountUnit;
  defaultVolumeMl: number;
  defaultDilutionFactor: number;
  drafts: Record<string, string>;
  onDraftChange: (key: string, value: string) => void;
  disabled?: boolean;
  footer?: ReactNode;
}

export function IcpReplicateEntryTable({
  elements,
  replicates,
  onChange,
  amountUnit,
  defaultVolumeMl,
  defaultDilutionFactor,
  drafts,
  onDraftChange,
  disabled = false,
  footer
}: IcpReplicateEntryTableProps) {
  const theme = useTheme();
  const tableRef = useRef<HTMLDivElement>(null);

  const amountKey = (idx: number) => `${idx}:amount`;
  const volumeKey = (idx: number) => `${idx}:volume`;
  const dilutionKey = (idx: number) => `${idx}:dilution`;
  const concKey = (idx: number, elId: number) => `${idx}:conc_${elId}`;

  const getCellDisplay = (key: string, modelVal: number, fallback = ""): string => {
    if (drafts[key] !== undefined) return drafts[key];
    if (modelVal === 0) return fallback;
    return String(modelVal);
  };

  const handleAddRow = () => {
    const nextIdx = replicates.length;
    const newReplicate: IcpReplicateDto = {
      replicateNo: nextIdx + 1,
      sampleAmount: 0,
      volumeMl: defaultVolumeMl,
      dilutionFactor: defaultDilutionFactor,
      concentrations: elements.map((e) => ({
        icpMethodElementId: e.icpMethodElementId,
        solutionMgPerL: 0
      }))
    };
    onDraftChange(volumeKey(nextIdx), String(defaultVolumeMl));
    onDraftChange(dilutionKey(nextIdx), String(defaultDilutionFactor));
    onChange([...replicates, newReplicate]);
  };

  const handleRemoveRow = (indexToRemove: number) => {
    const updated = replicates
      .filter((_, idx) => idx !== indexToRemove)
      .map((rep, idx) => ({ ...rep, replicateNo: idx + 1 }));
    onChange(updated);
  };

  const handleEnterKey = (e: KeyboardEvent, colKey: string, rowIdx: number) => {
    if (e.key !== "Enter") return;
    e.preventDefault();
    const nextInput = tableRef.current?.querySelector<HTMLInputElement>(
      `input[data-cell="${colKey}-${rowIdx + 1}"]`
    );
    nextInput?.focus();
    nextInput?.select();
  };

  const amountUnitLabel = amountUnit === "Milliliter" ? "mL" : "g";

  return (
    <Box>
      <TableContainer
        ref={tableRef}
        sx={{
          border: `1px solid ${theme.palette.divider}`,
          borderRadius: 1.5,
          overflowX: "auto",
          maxHeight: 520
        }}
      >
        <Table size="small" stickyHeader>
          <TableHead sx={tableHeadSx(theme)}>
            <TableRow>
              <TableCell sx={{ minWidth: 60, width: 70, textAlign: "center" }}>Rep #</TableCell>
              <TableCell sx={{ minWidth: 150 }}>Sample Amount ({amountUnitLabel}) *</TableCell>
              <TableCell sx={{ minWidth: 140 }}>Volume (mL) *</TableCell>
              <TableCell sx={{ minWidth: 140 }}>Dilution Factor *</TableCell>
              {elements.map((el) => (
                <TableCell key={el.icpMethodElementId} sx={{ minWidth: 190 }}>
                  {el.symbol} — Conc. in Calib. Units, mg/L *
                </TableCell>
              ))}
              {!disabled && (
                <TableCell align="center" sx={{ width: 60, minWidth: 60 }}>
                  Actions
                </TableCell>
              )}
            </TableRow>
          </TableHead>
          <TableBody>
            {replicates.length === 0 ? (
              <TableRow>
                <TableCell
                  colSpan={4 + elements.length + (disabled ? 0 : 1)}
                  sx={{ py: 4, textAlign: "center", color: "text.secondary" }}
                >
                  No replicate rows added yet. Click “+ Add Replicate Row” below to begin entering measurements.
                </TableCell>
              </TableRow>
            ) : (
              replicates.map((rep, idx) => {
                const aKey = amountKey(idx);
                const vKey = volumeKey(idx);
                const dKey = dilutionKey(idx);

                return (
                  <TableRow key={rep.replicateNo} hover>
                    <TableCell sx={{ textAlign: "center", fontWeight: 700 }}>
                      {rep.replicateNo}
                    </TableCell>

                    {/* Sample Amount */}
                    <TableCell>
                      <TextField
                        size="small"
                        fullWidth
                        disabled={disabled}
                        value={getCellDisplay(aKey, rep.sampleAmount, "")}
                        onChange={(e) => onDraftChange(aKey, e.target.value)}
                        onKeyDown={(e) => handleEnterKey(e, "amt", idx)}
                        placeholder={`e.g. ${amountUnit === "Milliliter" ? "5.0" : "1.0000"}`}
                        slotProps={{
                          htmlInput: {
                            "data-cell": `amt-${idx}`,
                            "aria-label": `Replicate ${rep.replicateNo} Sample Amount in ${amountUnitLabel}`,
                            style: { textAlign: "right", fontVariantNumeric: "tabular-nums" }
                          }
                        }}
                      />
                    </TableCell>

                    {/* Volume (mL) */}
                    <TableCell>
                      <TextField
                        size="small"
                        fullWidth
                        disabled={disabled}
                        value={getCellDisplay(vKey, rep.volumeMl, String(defaultVolumeMl))}
                        onChange={(e) => onDraftChange(vKey, e.target.value)}
                        onKeyDown={(e) => handleEnterKey(e, "vol", idx)}
                        placeholder={String(defaultVolumeMl)}
                        slotProps={{
                          htmlInput: {
                            "data-cell": `vol-${idx}`,
                            "aria-label": `Replicate ${rep.replicateNo} Volume in mL`,
                            style: { textAlign: "right", fontVariantNumeric: "tabular-nums" }
                          }
                        }}
                      />
                    </TableCell>

                    {/* Dilution Factor */}
                    <TableCell>
                      <TextField
                        size="small"
                        fullWidth
                        disabled={disabled}
                        value={getCellDisplay(dKey, rep.dilutionFactor, String(defaultDilutionFactor))}
                        onChange={(e) => onDraftChange(dKey, e.target.value)}
                        onKeyDown={(e) => handleEnterKey(e, "dil", idx)}
                        placeholder={String(defaultDilutionFactor)}
                        slotProps={{
                          htmlInput: {
                            "data-cell": `dil-${idx}`,
                            "aria-label": `Replicate ${rep.replicateNo} Dilution Factor`,
                            style: { textAlign: "right", fontVariantNumeric: "tabular-nums" }
                          }
                        }}
                      />
                    </TableCell>

                    {/* Element concentrations in calib units (mg/L) */}
                    {elements.map((el) => {
                      const cKey = concKey(idx, el.icpMethodElementId);
                      const modelConc =
                        rep.concentrations.find((c) => c.icpMethodElementId === el.icpMethodElementId)
                          ?.solutionMgPerL ?? 0;

                      return (
                        <TableCell key={el.icpMethodElementId}>
                          <TextField
                            size="small"
                            fullWidth
                            disabled={disabled}
                            value={getCellDisplay(cKey, modelConc, "")}
                            onChange={(e) => onDraftChange(cKey, e.target.value)}
                            onKeyDown={(e) => handleEnterKey(e, `c_${el.icpMethodElementId}`, idx)}
                            placeholder="mg/L"
                            slotProps={{
                              htmlInput: {
                                "data-cell": `c_${el.icpMethodElementId}-${idx}`,
                                "aria-label": `Replicate ${rep.replicateNo} ${el.symbol} concentration in mg/L`,
                                style: { textAlign: "right", fontVariantNumeric: "tabular-nums" }
                              }
                            }}
                          />
                        </TableCell>
                      );
                    })}

                    {/* Delete row action */}
                    {!disabled && (
                      <TableCell align="center">
                        <Tooltip title="Remove Replicate">
                          <span>
                            <IconButton
                              size="small"
                              color="error"
                              onClick={() => handleRemoveRow(idx)}
                              aria-label={`Remove replicate ${rep.replicateNo}`}
                            >
                              <DeleteOutlineIcon fontSize="small" />
                            </IconButton>
                          </span>
                        </Tooltip>
                      </TableCell>
                    )}
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {!disabled && (
        <Box sx={{ mt: 1.5, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <Button
            size="small"
            variant="outlined"
            startIcon={<AddIcon />}
            onClick={handleAddRow}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            Add Replicate Row
          </Button>

          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Press Enter in any input cell to move to the next replicate row.
          </Typography>
        </Box>
      )}

      {footer}
    </Box>
  );
}
