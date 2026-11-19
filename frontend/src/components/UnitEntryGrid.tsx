import React from "react";
import {
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField
} from "@mui/material";
import { useGridNavigation } from "./entry/useGridNavigation";

export interface UnitEntryGridColumn {
  key: string;
  label: string;
  unit?: string;
  readOnly?: boolean;
  width?: number;
}

export interface UnitEntryGridProps {
  rowCount: number;
  rowLabel?: (i: number) => string;
  columns: UnitEntryGridColumn[];
  values: Record<string, string>[];
  onChange: (values: Record<string, string>[]) => void;
  readOnlyValues?: Record<string, string>[];
  disabled?: boolean;
  inputMode?: React.HTMLAttributes<HTMLInputElement>["inputMode"];
}

const defaultRowLabel = (i: number) => `Unit ${i + 1}`;

export function UnitEntryGrid({
  rowCount,
  rowLabel = defaultRowLabel,
  columns,
  values,
  onChange,
  readOnlyValues,
  disabled = false,
  inputMode = "decimal"
}: UnitEntryGridProps) {
  const handleCellChange = (rIdx: number, colKey: string, val: string) => {
    const nextValues = Array.from({ length: rowCount }, (_, i) => ({ ...(values[i] || {}) }));
    nextValues[rIdx][colKey] = val;
    onChange(nextValues);
  };

  const { cellProps } = useGridNavigation({
    rows: rowCount,
    cols: columns.length,
    isEditable: (_r, c) => !columns[c]?.readOnly,
    onCopyDown: (r, c) => {
      if (r <= 0) return;
      const col = columns[c];
      if (!col || col.readOnly) return;
      const aboveVal = values[r - 1]?.[col.key] ?? "";
      handleCellChange(r, col.key, aboveVal);
    }
  });

  const handlePaste = (
    e: React.ClipboardEvent,
    startRow: number,
    startCol: number
  ) => {
    const text = e.clipboardData.getData("text/plain") || e.clipboardData.getData("text") || "";
    const hasTabOrNewline = /[\t\r\n]/.test(text);
    if (!hasTabOrNewline) {
      return;
    }

    e.preventDefault();

    const lines = text.split(/\r?\n/);
    if (lines.length > 0 && lines[lines.length - 1] === "") {
      lines.pop();
    }

    const pasteGrid = lines.map((line) => line.split("\t"));
    const nextValues = Array.from({ length: rowCount }, (_, i) => ({ ...(values[i] || {}) }));

    for (let rOffset = 0; rOffset < pasteGrid.length; rOffset++) {
      const targetRow = startRow + rOffset;
      if (targetRow >= rowCount) break;

      const rowCells = pasteGrid[rOffset];
      for (let cOffset = 0; cOffset < rowCells.length; cOffset++) {
        const targetCol = startCol + cOffset;
        if (targetCol >= columns.length) break;

        const col = columns[targetCol];
        if (col.readOnly) continue;

        nextValues[targetRow][col.key] = rowCells[cOffset];
      }
    }

    onChange(nextValues);
  };

  return (
    <TableContainer sx={{ maxHeight: 440, border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
      <Table size="small" stickyHeader aria-label="unit-entry-grid">
        <TableHead>
          <TableRow>
            <TableCell sx={{ fontWeight: 700, width: 100, bgcolor: "background.paper", py: 1 }}>
              Unit
            </TableCell>
            {columns.map((col) => (
              <TableCell
                key={col.key}
                sx={{
                  fontWeight: 700,
                  width: col.width,
                  minWidth: col.width ?? 100,
                  bgcolor: "background.paper",
                  py: 1
                }}
              >
                {col.label}
                {col.unit ? ` (${col.unit})` : ""}
              </TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {Array.from({ length: rowCount }, (_, rIdx) => {
            const currentLabel = rowLabel(rIdx);
            return (
              <TableRow key={rIdx} hover>
                <TableCell sx={{ fontWeight: 600, fontSize: 13, color: "text.secondary", py: 0.5 }}>
                  {currentLabel}
                </TableCell>
                {columns.map((col, cIdx) => {
                  const isReadOnly = Boolean(col.readOnly);
                  const cellVal = isReadOnly
                    ? (readOnlyValues?.[rIdx]?.[col.key] ?? values[rIdx]?.[col.key] ?? "")
                    : (values[rIdx]?.[col.key] ?? "");
                  const ariaLabel = `${currentLabel} ${col.label}`;
                  const nav = cellProps(rIdx, cIdx);

                  return (
                    <TableCell key={col.key} sx={{ py: 0.5 }}>
                      <TextField
                        size="small"
                        variant="outlined"
                        fullWidth
                        disabled={disabled}
                        value={cellVal}
                        onChange={(e) => handleCellChange(rIdx, col.key, e.target.value)}
                        onKeyDown={nav.onKeyDown}
                        onPaste={(e) => handlePaste(e, rIdx, cIdx)}
                        inputRef={nav.ref}
                        slotProps={{
                          htmlInput: {
                            "aria-label": ariaLabel,
                            "aria-description": col.unit ? `Unit: ${col.unit}` : undefined,
                            "data-grid-cell": nav["data-grid-cell"],
                            readOnly: isReadOnly,
                            inputMode: isReadOnly ? undefined : inputMode
                          }
                        }}
                        sx={{
                          "& .MuiInputBase-input": {
                            py: 0.75,
                            px: 1,
                            fontSize: 13,
                            fontVariantNumeric: "tabular-nums"
                          },
                          ...(isReadOnly && {
                            bgcolor: "action.hover",
                            "& .MuiOutlinedInput-notchedOutline": {
                              borderColor: "divider"
                            }
                          })
                        }}
                      />
                    </TableCell>
                  );
                })}
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
