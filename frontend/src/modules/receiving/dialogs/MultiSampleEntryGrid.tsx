import { useEffect, useMemo, useRef, useState } from "react";
import {
  Box,
  Typography,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Select,
  MenuItem,
  TextField,
  Autocomplete,
  IconButton,
  Button,
  Paper,
  Tooltip,
  Checkbox,
  FormControlLabel,
  FormGroup,
  CircularProgress,
  useTheme
} from "@mui/material";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import AddIcon from "@mui/icons-material/Add";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import {
  BatchReceivedSample,
  ReceiveRowItem,
  ReceivingMasterData,
  SampleCategoryKey,
  ReceiptLabOption
} from "../types/receivingTypes";
import { ReceiveService } from "../services/ReceiveService";
import { tableHeadSx } from "../../../theme";
import { DateField } from "../../../components/DateField";
import { useGridNavigation } from "../../../components/entry/useGridNavigation";

export const MAX_RECEIVE_ROWS = 100;

// The two laboratories always shown in the Laboratories column, even when
// the item has no tests for one of them - so the receiver sees a disabled
// "(0)" entry rather than a lab silently missing. Mirrors env.md's fixed
// section codes (Code is never renamed).
const ALL_LABS: { code: string; label: string }[] = [
  { code: "MICRO", label: "Microbiology Laboratory" },
  { code: "FP", label: "Physicochemical Laboratory" }
];

export type LabMode = { kind: "choose" } | { kind: "fixed"; sectionId: number; code?: string };

export interface FocusErrorTarget {
  row: number;
  field: string;
  nonce: number;
}

interface GridColumnDef {
  key: string;
  isEditable?: (row: ReceiveRowItem) => boolean;
  copyDown?: (above: ReceiveRowItem, rowIndex: number) => void;
}

const cellErrorId = (rowIdx: number, field: string) => `receive-cell-err-${rowIdx}-${field}`;

interface Props {
  category: SampleCategoryKey;
  rows: ReceiveRowItem[];
  masterData: ReceivingMasterData;
  labMode: LabMode;
  onChangeRow: <K extends keyof ReceiveRowItem>(index: number, field: K, value: ReceiveRowItem[K]) => void;
  onAddRow: () => void;
  onDeleteRow: (index: number) => void;
  onSave?: () => void;
  focusErrorTarget?: FocusErrorTarget | null;
  coaErrorByRow?: Record<number, string>;
  retryingCoaRow?: number | null;
  onRetryCoa?: (rowIndex: number) => void;
  receivedByRow?: Record<number, BatchReceivedSample>;
}

export function MultiSampleEntryGrid({
  category,
  rows,
  masterData,
  labMode,
  onChangeRow,
  onAddRow,
  onDeleteRow,
  onSave,
  focusErrorTarget,
  coaErrorByRow,
  retryingCoaRow,
  onRetryCoa,
  receivedByRow
}: Props) {
  const theme = useTheme();
  const isItemBased = category === "product" || category === "rm" || category === "pm";
  const isWater = category === "water";
  const isEM = category === "em";
  const isAC = category === "ac";
  const isPhyschem = labMode.kind === "fixed" && labMode.code !== "MICRO";
  const showLabColumn = isItemBased && labMode.kind === "choose";

  // Cached receipt-labs by item id, shared across rows that picked the
  // same item - avoids one fetch per row for a repeated item.
  const [labOptionsByItem, setLabOptionsByItem] = useState<Record<string, ReceiptLabOption[]>>({});
  const [loadingItemIds, setLoadingItemIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (!showLabColumn) return;
    const itemIds = Array.from(new Set(rows.map((r) => r.itemId).filter((id): id is number => Boolean(id))));
    itemIds.forEach((id) => {
      const key = String(id);
      if (labOptionsByItem[key] || loadingItemIds.has(key)) return;
      setLoadingItemIds((prev) => new Set(prev).add(key));
      ReceiveService.receiptLabs(id)
        .then((labs) => setLabOptionsByItem((prev) => ({ ...prev, [key]: labs })))
        .finally(() => setLoadingItemIds((prev) => {
          const next = new Set(prev);
          next.delete(key);
          return next;
        }));
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rows, showLabColumn]);

  // Default every row to all labs the item has tests for, once its
  // receipt-labs have loaded - only when the row hasn't been touched yet.
  useEffect(() => {
    if (!showLabColumn) return;
    rows.forEach((row, idx) => {
      if (!row.itemId || row.targetSectionIds !== undefined) return;
      const options = labOptionsByItem[String(row.itemId)];
      if (!options) return;
      onChangeRow(idx, "targetSectionIds", options.filter((o) => o.testCount > 0).map((o) => o.sectionId));
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rows, labOptionsByItem, showLabColumn]);

  // Grades per item (raw materials with graded specifications). Loaded once
  // per item, then copied onto each row so the dialog can require a grade.
  const [gradesByItem, setGradesByItem] = useState<Record<string, string[]>>({});
  const requestedGradeItems = useRef<Set<string>>(new Set());

  useEffect(() => {
    if (!isItemBased) return;
    rows.forEach((row) => {
      if (!row.itemId) return;
      const key = String(row.itemId);
      if (gradesByItem[key] || requestedGradeItems.current.has(key)) return;
      requestedGradeItems.current.add(key);
      ReceiveService.itemGrades(Number(row.itemId))
        .then((grades) => setGradesByItem((prev) => ({ ...prev, [key]: grades })))
        .catch(() => requestedGradeItems.current.delete(key));
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rows, isItemBased]);

  useEffect(() => {
    if (!isItemBased) return;
    rows.forEach((row, idx) => {
      if (!row.itemId || row.gradeOptions !== undefined) return;
      const grades = gradesByItem[String(row.itemId)];
      if (grades) {
        onChangeRow(idx, "gradeOptions", grades);
        // One grade only: preselect it (still shown and changeable).
        if (grades.length === 1 && !row.grade) onChangeRow(idx, "grade", grades[0]);
      }
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rows, gradesByItem, isItemBased]);

  // Raw materials only: the supplier's CoA can be attached while receiving.
  const showCoaColumn = category === "rm";
  const showGradeColumn = isItemBased && rows.some((r) => r.gradeOptions && r.gradeOptions.length > 0);

  const columns = useMemo<GridColumnDef[]>(() => {
    const cols: GridColumnDef[] = [];
    if (isItemBased) {
      cols.push({
        key: "itemId",
        copyDown: (above, rowIndex) => {
          onChangeRow(rowIndex, "itemId", above.itemId ?? "");
          onChangeRow(rowIndex, "targetSectionIds", above.targetSectionIds ? [...above.targetSectionIds] : undefined);
          onChangeRow(rowIndex, "gradeOptions", above.gradeOptions ? [...above.gradeOptions] : undefined);
          onChangeRow(rowIndex, "grade", above.grade ?? "");
        }
      });
    }
    if (showLabColumn) {
      cols.push({
        key: "targetSectionIds",
        isEditable: (row) => Boolean(row.itemId),
        copyDown: (above, rowIndex) => {
          onChangeRow(rowIndex, "targetSectionIds", above.targetSectionIds ? [...above.targetSectionIds] : []);
        }
      });
    }
    if (showGradeColumn) {
      cols.push({
        key: "grade",
        isEditable: (row) => Boolean(row.gradeOptions && row.gradeOptions.length > 0),
        copyDown: (above, rowIndex) => {
          onChangeRow(rowIndex, "grade", above.grade ?? "");
        }
      });
    }
    if (showCoaColumn) {
      cols.push({ key: "coaFile" });
    }
    if (isAC) {
      cols.push({
        key: "previousProduct",
        copyDown: (above, rowIndex) => {
          onChangeRow(rowIndex, "previousProductItemId", above.previousProductItemId ?? null);
          onChangeRow(rowIndex, "previousProductName", above.previousProductName ?? "");
        }
      });
      if (isPhyschem) {
        cols.push({
          key: "techniques",
          copyDown: (above, rowIndex) => {
            onChangeRow(rowIndex, "techniques", above.techniques ? [...above.techniques] : []);
          }
        });
      }
      cols.push({
        key: "previousProductBatchNumber",
        copyDown: (above, rowIndex) => {
          onChangeRow(rowIndex, "previousProductBatchNumber", above.previousProductBatchNumber ?? "");
        }
      });
    }
    if (category === "product") {
      cols.push({
        key: "productionStage",
        copyDown: (above, rowIndex) => {
          onChangeRow(rowIndex, "productionStage", above.productionStage ?? "");
        }
      });
    }
    if (isWater || isEM) {
      cols.push({
        key: "departmentId",
        copyDown: (above, rowIndex) => {
          onChangeRow(rowIndex, "departmentId", above.departmentId ?? "");
        }
      });
    }
    if (isAC) {
      cols.push({
        key: "machineId",
        copyDown: (above, rowIndex) => {
          onChangeRow(rowIndex, "machineId", above.machineId ?? "");
        }
      });
    }
    cols.push({
      key: "causeOfTestingId",
      copyDown: (above, rowIndex) => {
        onChangeRow(rowIndex, "causeOfTestingId", above.causeOfTestingId ?? "");
      }
    });
    if (!isEM && !isAC) {
      cols.push({
        key: "sampleQuantity",
        copyDown: (above, rowIndex) => {
          onChangeRow(rowIndex, "sampleQuantity", above.sampleQuantity ?? "");
        }
      });
    }
    cols.push({
      key: "sampledBy",
      copyDown: (above, rowIndex) => {
        onChangeRow(rowIndex, "sampledBy", above.sampledBy ?? "");
      }
    });
    if (isItemBased) {
      cols.push({
        key: "batchNumber",
        copyDown: (above, rowIndex) => {
          onChangeRow(rowIndex, "batchNumber", above.batchNumber ?? "");
        }
      });
    }
    if (!isItemBased) {
      cols.push({
        key: "sampledAt",
        copyDown: (above, rowIndex) => {
          onChangeRow(rowIndex, "sampledAt", above.sampledAt ?? "");
        }
      });
    }
    cols.push({
      key: "controlNumber",
      copyDown: (above, rowIndex) => {
        onChangeRow(rowIndex, "controlNumber", above.controlNumber ?? "");
      }
    });
    if (isItemBased) {
      cols.push({
        key: "mfgDate",
        copyDown: (above, rowIndex) => {
          onChangeRow(rowIndex, "mfgDate", above.mfgDate ?? "");
        }
      });
      cols.push({
        key: "expDate",
        copyDown: (above, rowIndex) => {
          onChangeRow(rowIndex, "expDate", above.expDate ?? "");
        }
      });
    }
    return cols;
  }, [category, isAC, isEM, isItemBased, isPhyschem, isWater, onChangeRow, showCoaColumn, showGradeColumn, showLabColumn]);

  const colIndexByKey = useMemo(() => {
    const map: Record<string, number> = {};
    columns.forEach((col, i) => {
      map[col.key] = i;
    });
    return map;
  }, [columns]);

  const { cellProps, focusCell } = useGridNavigation({
    rows: rows.length,
    cols: columns.length,
    isEditable: (r, c) => {
      const col = columns[c];
      const row = rows[r];
      if (!col || !row) return false;
      return col.isEditable ? col.isEditable(row) : true;
    },
    onCopyDown: (r, c) => {
      if (r <= 0) return;
      const col = columns[c];
      const above = rows[r - 1];
      if (!col || !above || !col.copyDown) return;
      col.copyDown(above, r);
    },
    onSave
  });

  const lastFocusedNonce = useRef<number>(-1);
  useEffect(() => {
    if (!focusErrorTarget || focusErrorTarget.nonce === lastFocusedNonce.current) return;
    lastFocusedNonce.current = focusErrorTarget.nonce;
    const key =
      focusErrorTarget.field === "previousProductItemId" || focusErrorTarget.field === "previousProductName"
        ? "previousProduct"
        : focusErrorTarget.field === "waterDepartmentId"
        ? "departmentId"
        : focusErrorTarget.field;
    const colIdx = colIndexByKey[key];
    if (colIdx !== undefined) {
      focusCell(focusErrorTarget.row, colIdx);
    }
  }, [focusErrorTarget, colIndexByKey, focusCell]);

  const renderCellError = (rowIdx: number, field: string, message?: string) => {
    if (!message) return null;
    return (
      <Typography
        id={cellErrorId(rowIdx, field)}
        sx={{ fontSize: 11, color: theme.custom.status.detected.text, mt: 0.25 }}
      >
        {message}
      </Typography>
    );
  };

  const atRowLimit = rows.length >= MAX_RECEIVE_ROWS;
  const addRowLimitId = "receive-add-row-limit-reason";

  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
      {(isEM || isAC || isWater) && (
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            gap: 1,
            p: 1.25,
            bgcolor: theme.custom.status.inconclusive.bg,
            borderRadius: 1.5,
            border: "1px solid",
            borderColor: theme.custom.status.inconclusive.border
          }}
        >
          <InfoOutlinedIcon sx={{ fontSize: 18, color: theme.custom.status.inconclusive.text }} />
          <Typography sx={{ fontSize: 12, color: theme.custom.status.inconclusive.text, fontWeight: 500 }}>
            {isEM
              ? "Rooms, test locations, and test types are configured in the Preparation step after receiving."
              : isAC
              ? "Machine parts, test locations, and test types are configured in the Preparation step after receiving."
              : "Sampling points and test types are configured in the Preparation step after receiving."}
          </Typography>
        </Box>
      )}

      <Paper
        elevation={0}
        sx={{
          border: "1px solid",
          borderColor: "divider",
          borderRadius: 2,
          overflow: "hidden",
          bgcolor: "background.paper"
        }}
      >
        <Box sx={{ overflowX: "auto", maxHeight: "55vh" }}>
          <Table size="small" stickyHeader sx={{ minWidth: (showLabColumn ? (showGradeColumn ? 1560 : 1400) : isItemBased || isAC ? (showGradeColumn ? 1360 : 1200) : 780) + (showCoaColumn ? 200 : 0) }}>
            <TableHead>
              <TableRow sx={[tableHeadSx, { "& th": { fontWeight: 700, fontSize: 12, py: 1.25 } }]}>
                <TableCell sx={{ width: 40 }}>#</TableCell>

                {isItemBased && (
                  <TableCell sx={{ minWidth: 200 }}>
                    Item <span style={{ color: theme.custom.status.detected.text }}>*</span>
                  </TableCell>
                )}

                {showLabColumn && (
                  <TableCell sx={{ minWidth: 200 }}>
                    Laboratories <span style={{ color: theme.custom.status.detected.text }}>*</span>
                  </TableCell>
                )}

                {showGradeColumn && (
                  <TableCell sx={{ minWidth: 160 }}>
                    Grade <span style={{ color: theme.custom.status.detected.text }}>*</span>
                  </TableCell>
                )}

                {showCoaColumn && <TableCell sx={{ minWidth: 200 }}>Supplier CoA</TableCell>}

                {isAC && (
                  <>
                    <TableCell sx={{ minWidth: 200 }}>
                      Previous Product <span style={{ color: theme.custom.status.detected.text }}>*</span>
                    </TableCell>
                    {isPhyschem && (
                      <TableCell sx={{ minWidth: 220 }}>
                        Techniques <span style={{ color: theme.custom.status.detected.text }}>*</span>
                      </TableCell>
                    )}
                    <TableCell sx={{ minWidth: 150 }}>
                      Previous Batch No. <span style={{ color: theme.custom.status.detected.text }}>*</span>
                    </TableCell>
                  </>
                )}

                {category === "product" && (
                  <TableCell sx={{ minWidth: 140 }}>
                    Production Stage <span style={{ color: theme.custom.status.detected.text }}>*</span>
                  </TableCell>
                )}

                {isWater && (
                  <TableCell sx={{ minWidth: 180 }}>
                    Department <span style={{ color: theme.custom.status.detected.text }}>*</span>
                  </TableCell>
                )}

                {isEM && (
                  <TableCell sx={{ minWidth: 180 }}>
                    Department <span style={{ color: theme.custom.status.detected.text }}>*</span>
                  </TableCell>
                )}

                {isAC && (
                  <TableCell sx={{ minWidth: 180 }}>
                    Machine <span style={{ color: theme.custom.status.detected.text }}>*</span>
                  </TableCell>
                )}

                <TableCell sx={{ minWidth: 170 }}>
                  Cause of Testing <span style={{ color: theme.custom.status.detected.text }}>*</span>
                </TableCell>

                {!isEM && !isAC && (
                  <TableCell sx={{ minWidth: 120 }}>Quantity</TableCell>
                )}

                <TableCell sx={{ minWidth: 160 }}>
                  Sampled By <span style={{ color: theme.custom.status.detected.text }}>*</span>
                </TableCell>

                {isItemBased && (
                  <TableCell sx={{ minWidth: 130 }}>
                    Batch No. <span style={{ color: theme.custom.status.detected.text }}>*</span>
                  </TableCell>
                )}

                {!isItemBased && <TableCell sx={{ minWidth: 190 }}>Sampled At</TableCell>}

                {/* The server requires both for item-based samples (ReceiveSampleValidator). */}
                <TableCell sx={{ minWidth: 130 }}>
                  Control No.{isItemBased && <> <span style={{ color: theme.custom.status.detected.text }}>*</span></>}
                </TableCell>

                {isItemBased && (
                  <>
                    <TableCell sx={{ minWidth: 135 }}>Mfg Date</TableCell>
                    <TableCell sx={{ minWidth: 135 }}>Exp Date</TableCell>
                  </>
                )}

                <TableCell align="center" sx={{ width: 50 }}>
                  Action
                </TableCell>
              </TableRow>
            </TableHead>

            <TableBody>
              {rows.map((row, idx) => {
                const errors = row.errors || {};

                return (
                  <TableRow
                    key={row.id}
                    sx={{
                      "&:hover": { bgcolor: "action.hover" },
                      "& td": { py: 1 }
                    }}
                  >
                    {/* Row Index */}
                    <TableCell sx={{ fontWeight: 600, fontSize: 12, color: "text.secondary" }}>
                      {idx + 1}
                      {receivedByRow?.[idx] && (
                        <Typography sx={{ fontSize: 11, color: theme.custom.status.notDetected.text, fontWeight: 600 }}>
                          {receivedByRow[idx].referenceNumber}
                        </Typography>
                      )}
                    </TableCell>

                    {/* Item (for Product, RM, PM) */}
                    {isItemBased && (
                      <TableCell {...cellProps(idx, colIndexByKey.itemId)}>
                        <Select
                          size="small"
                          fullWidth
                          displayEmpty
                          value={row.itemId ?? ""}
                          error={Boolean(errors.itemId)}
                          onChange={(e) => {
                            onChangeRow(idx, "itemId", e.target.value as number | "");
                            // A different item can have a different set of
                            // labs - clear the stale choice so it re-defaults.
                            onChangeRow(idx, "targetSectionIds", undefined);
                            onChangeRow(idx, "gradeOptions", undefined);
                            onChangeRow(idx, "grade", "");
                          }}
                          sx={{ fontSize: 12 }}
                          SelectDisplayProps={{
                            "aria-describedby": errors.itemId ? cellErrorId(idx, "itemId") : undefined
                          }}
                          inputProps={{ "aria-label": `Sample ${idx + 1} item` }}
                        >
                          <MenuItem value="">
                            <em style={{ color: theme.palette.text.secondary }}>Select Item</em>
                          </MenuItem>
                          {masterData.items.map((i) => (
                            <MenuItem key={i.id} value={i.id}>
                              {i.name}
                            </MenuItem>
                          ))}
                        </Select>
                        {renderCellError(idx, "itemId", errors.itemId)}
                      </TableCell>
                    )}

                    {/* Laboratories (choose mode only) */}
                    {showLabColumn && (
                      <TableCell {...cellProps(idx, colIndexByKey.targetSectionIds)}>
                        {!row.itemId ? (
                          <Typography sx={{ fontSize: 12, color: "text.disabled" }}>
                            Select an item first
                          </Typography>
                        ) : loadingItemIds.has(String(row.itemId)) ? (
                          <CircularProgress size={16} />
                        ) : (
                          <FormGroup row aria-describedby={errors.targetSectionIds ? cellErrorId(idx, "targetSectionIds") : undefined}>
                            {ALL_LABS.map((lab) => {
                              const options = labOptionsByItem[String(row.itemId)] || [];
                              const option = options.find((o) => o.sectionCode === lab.code);
                              const checked = Boolean(option && row.targetSectionIds?.includes(option.sectionId));
                              return (
                                <FormControlLabel
                                  key={lab.code}
                                  sx={{ mr: 1.5 }}
                                  control={
                                    <Checkbox
                                      size="small"
                                      disabled={!option}
                                      checked={checked}
                                      onChange={(e) => {
                                        if (!option) return;
                                        const current = row.targetSectionIds || [];
                                        const next = e.target.checked
                                          ? [...current, option.sectionId]
                                          : current.filter((id) => id !== option.sectionId);
                                        onChangeRow(idx, "targetSectionIds", next);
                                      }}
                                    />
                                  }
                                  label={
                                    <Typography sx={{ fontSize: 12, color: option ? "text.primary" : "text.disabled" }}>
                                      {(option?.sectionName || lab.label).replace(" Laboratory", "")} ({option?.testCount ?? 0})
                                    </Typography>
                                  }
                                />
                              );
                            })}
                          </FormGroup>
                        )}
                        {renderCellError(idx, "targetSectionIds", errors.targetSectionIds)}
                      </TableCell>
                    )}

                    {/* Grade stated on the supplier's CoA (only for items with graded specifications) */}
                    {showGradeColumn && (
                      <TableCell {...cellProps(idx, colIndexByKey.grade)}>
                        {row.gradeOptions && row.gradeOptions.length > 0 ? (
                          <>
                            <Select
                              size="small"
                              fullWidth
                              displayEmpty
                              value={row.grade ?? ""}
                              error={Boolean(errors.grade)}
                              onChange={(e) => onChangeRow(idx, "grade", e.target.value)}
                              sx={{ fontSize: 12 }}
                              SelectDisplayProps={{
                                "aria-describedby": errors.grade ? cellErrorId(idx, "grade") : undefined
                              }}
                              inputProps={{ "aria-label": `Sample ${idx + 1} grade` }}
                            >
                              <MenuItem value="">
                                <em style={{ color: theme.palette.text.secondary }}>Select grade</em>
                              </MenuItem>
                              {row.gradeOptions.map((g) => (
                                <MenuItem key={g} value={g}>
                                  {g}
                                </MenuItem>
                              ))}
                            </Select>
                            {errors.grade ? (
                              renderCellError(idx, "grade", errors.grade)
                            ) : (
                              <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 0.25 }}>
                                Choose the grade stated on the supplier&apos;s CoA
                              </Typography>
                            )}
                          </>
                        ) : (
                          <Typography sx={{ fontSize: 12, color: "text.disabled" }}>-</Typography>
                        )}
                      </TableCell>
                    )}

                    {/* Supplier CoA (optional; uploaded once the sample exists) */}
                    {showCoaColumn && (
                      <TableCell {...cellProps(idx, colIndexByKey.coaFile)}>
                        <Button component="label" size="small" variant="outlined" sx={{ textTransform: "none", fontSize: 12, maxWidth: 190 }}>
                          <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                            {row.coaFile ? row.coaFile.name : "Attach CoA"}
                          </span>
                          <input
                            type="file"
                            hidden
                            accept=".pdf,.jpg,.jpeg,.png,.webp,.tiff"
                            aria-label={`Sample ${idx + 1} supplier CoA`}
                            onChange={(e) => onChangeRow(idx, "coaFile", e.target.files?.[0] ?? null)}
                          />
                        </Button>
                        {coaErrorByRow?.[idx] && (
                          <Box sx={{ display: "flex", alignItems: "center", gap: 0.5, mt: 0.5 }}>
                            <Typography sx={{ fontSize: 11, color: theme.custom.status.detected.text }}>
                              Certificate not attached — retry
                            </Typography>
                            {onRetryCoa && (
                              <Button
                                size="small"
                                variant="text"
                                disabled={retryingCoaRow === idx}
                                onClick={() => onRetryCoa(idx)}
                                sx={{ fontSize: 11, minWidth: 0, p: 0.25, textTransform: "none" }}
                              >
                                Retry
                              </Button>
                            )}
                          </Box>
                        )}
                      </TableCell>
                    )}

                    {/* Previous Product & Previous Batch No (for After Cleaning) */}
                    {isAC && (
                      <>
                        <TableCell {...cellProps(idx, colIndexByKey.previousProduct)}>
                          {isPhyschem ? (
                            <Autocomplete
                              size="small"
                              fullWidth
                              options={masterData.items}
                              getOptionLabel={(o) => (typeof o === "string" ? o : o.name ?? "")}
                              value={masterData.items.find((i) => i.id === row.previousProductItemId) ?? null}
                              onChange={(_, val) => {
                                onChangeRow(idx, "previousProductItemId", val ? val.id : null);
                                onChangeRow(idx, "previousProductName", val ? val.name : "");
                              }}
                              isOptionEqualToValue={(a, b) => a.id === b.id}
                              renderInput={(params) => (
                                <TextField
                                  {...params}
                                  placeholder="Select Finished Product"
                                  error={Boolean(errors.previousProductItemId || errors.previousProductName)}
                                  slotProps={{
                                    ...params.slotProps,
                                    htmlInput: {
                                      ...params.slotProps.htmlInput,
                                      "aria-label": `Sample ${idx + 1} previous product`,
                                      "aria-describedby": errors.previousProductItemId
                                        ? cellErrorId(idx, "previousProductItemId")
                                        : errors.previousProductName
                                        ? cellErrorId(idx, "previousProductName")
                                        : undefined,
                                      style: { fontSize: 12 }
                                    }
                                  }}
                                />
                              )}
                            />
                          ) : (
                            <Autocomplete
                              freeSolo
                              size="small"
                              fullWidth
                              options={masterData.items}
                              getOptionLabel={(o) => (typeof o === "string" ? o : o.name ?? "")}
                              value={
                                masterData.items.find((i) => i.id === row.previousProductItemId) ??
                                row.previousProductName ??
                                ""
                              }
                              onChange={(_, val) => {
                                if (typeof val === "string") {
                                  onChangeRow(idx, "previousProductItemId", null);
                                  onChangeRow(idx, "previousProductName", val);
                                } else if (val) {
                                  onChangeRow(idx, "previousProductItemId", val.id);
                                  onChangeRow(idx, "previousProductName", val.name);
                                } else {
                                  onChangeRow(idx, "previousProductItemId", null);
                                  onChangeRow(idx, "previousProductName", "");
                                }
                              }}
                              onInputChange={(_, val, reason) => {
                                if (reason === "input") {
                                  onChangeRow(idx, "previousProductItemId", null);
                                  onChangeRow(idx, "previousProductName", val);
                                }
                              }}
                              renderInput={(params) => (
                                <TextField
                                  {...params}
                                  placeholder="Previous Product Name"
                                  error={Boolean(errors.previousProductName)}
                                  slotProps={{
                                    ...params.slotProps,
                                    htmlInput: {
                                      ...params.slotProps.htmlInput,
                                      "aria-label": `Sample ${idx + 1} previous product name`,
                                      "aria-describedby": errors.previousProductName
                                        ? cellErrorId(idx, "previousProductName")
                                        : undefined,
                                      style: { fontSize: 12 }
                                    }
                                  }}
                                />
                              )}
                            />
                          )}
                          {renderCellError(
                            idx,
                            errors.previousProductItemId ? "previousProductItemId" : "previousProductName",
                            errors.previousProductItemId || errors.previousProductName
                          )}
                        </TableCell>
                        {isPhyschem && (
                          <TableCell {...cellProps(idx, colIndexByKey.techniques)}>
                            <FormGroup
                              row
                              sx={{ flexWrap: "nowrap" }}
                              aria-describedby={errors.techniques ? cellErrorId(idx, "techniques") : undefined}
                            >
                              {(["UV", "HPLC", "ICP"] as const).map((tech) => (
                                <FormControlLabel
                                  key={tech}
                                  control={
                                    <Checkbox
                                      size="small"
                                      checked={(row.techniques ?? []).includes(tech)}
                                      onChange={(e) => {
                                        const current = row.techniques ?? [];
                                        const next = e.target.checked
                                          ? [...current, tech]
                                          : current.filter((t) => t !== tech);
                                        onChangeRow(idx, "techniques", next);
                                      }}
                                      slotProps={{
                                        input: {
                                          "aria-label": `Sample ${idx + 1} technique ${tech}`
                                        }
                                      }}
                                    />
                                  }
                                  label={<Typography sx={{ fontSize: 12 }}>{tech}</Typography>}
                                  sx={{ mr: 1 }}
                                />
                              ))}
                            </FormGroup>
                            {renderCellError(idx, "techniques", errors.techniques)}
                          </TableCell>
                        )}
                        <TableCell {...cellProps(idx, colIndexByKey.previousProductBatchNumber)}>
                          <TextField
                            size="small"
                            fullWidth
                            placeholder="Prev. Batch No."
                            value={row.previousProductBatchNumber ?? ""}
                            error={Boolean(errors.previousProductBatchNumber)}
                            onChange={(e) => onChangeRow(idx, "previousProductBatchNumber", e.target.value)}
                            slotProps={{
                              htmlInput: {
                                "aria-label": `Sample ${idx + 1} previous batch number`,
                                "aria-describedby": errors.previousProductBatchNumber
                                  ? cellErrorId(idx, "previousProductBatchNumber")
                                  : undefined,
                                style: { fontSize: 12 }
                              }
                            }}
                          />
                          {renderCellError(idx, "previousProductBatchNumber", errors.previousProductBatchNumber)}
                        </TableCell>
                      </>
                    )}

                    {/* Production Stage (for Product only) */}
                    {category === "product" && (
                      <TableCell {...cellProps(idx, colIndexByKey.productionStage)}>
                        <Select
                          size="small"
                          fullWidth
                          displayEmpty
                          value={row.productionStage ?? ""}
                          error={Boolean(errors.productionStage)}
                          onChange={(e) => onChangeRow(idx, "productionStage", e.target.value)}
                          sx={{ fontSize: 12 }}
                          SelectDisplayProps={{
                            "aria-describedby": errors.productionStage ? cellErrorId(idx, "productionStage") : undefined
                          }}
                          inputProps={{ "aria-label": `Sample ${idx + 1} production stage` }}
                        >
                          <MenuItem value="">
                            <em style={{ color: theme.palette.text.secondary }}>Stage</em>
                          </MenuItem>
                          {masterData.productionStages.map((s) => (
                            <MenuItem key={s.id} value={s.name}>
                              {s.name}
                            </MenuItem>
                          ))}
                        </Select>
                        {renderCellError(idx, "productionStage", errors.productionStage)}
                      </TableCell>
                    )}

                    {/* Department (for Water) */}
                    {isWater && (
                      <TableCell {...cellProps(idx, colIndexByKey.departmentId)}>
                        <Select
                          size="small"
                          fullWidth
                          displayEmpty
                          value={row.departmentId ?? ""}
                          error={Boolean(errors.departmentId)}
                          onChange={(e) => onChangeRow(idx, "departmentId", e.target.value as number | "")}
                          sx={{ fontSize: 12 }}
                          SelectDisplayProps={{
                            "aria-describedby": errors.departmentId ? cellErrorId(idx, "departmentId") : undefined
                          }}
                          inputProps={{ "aria-label": `Sample ${idx + 1} department` }}
                        >
                          <MenuItem value="">
                            <em style={{ color: theme.palette.text.secondary }}>Select Department</em>
                          </MenuItem>
                          {masterData.waterDepartments.map((d) => (
                            <MenuItem key={d.id} value={d.id}>
                              {d.name}
                            </MenuItem>
                          ))}
                        </Select>
                        {renderCellError(idx, "departmentId", errors.departmentId)}
                      </TableCell>
                    )}

                    {/* Department (for EM) */}
                    {isEM && (
                      <TableCell {...cellProps(idx, colIndexByKey.departmentId)}>
                        <Select
                          size="small"
                          fullWidth
                          displayEmpty
                          value={row.departmentId ?? ""}
                          error={Boolean(errors.departmentId)}
                          onChange={(e) => onChangeRow(idx, "departmentId", e.target.value as number | "")}
                          sx={{ fontSize: 12 }}
                          SelectDisplayProps={{
                            "aria-describedby": errors.departmentId ? cellErrorId(idx, "departmentId") : undefined
                          }}
                          inputProps={{ "aria-label": `Sample ${idx + 1} department` }}
                        >
                          <MenuItem value="">
                            <em style={{ color: theme.palette.text.secondary }}>Select Department</em>
                          </MenuItem>
                          {masterData.departments.map((d) => (
                            <MenuItem key={d.id} value={d.id}>
                              {d.name}
                            </MenuItem>
                          ))}
                        </Select>
                        {renderCellError(idx, "departmentId", errors.departmentId)}
                      </TableCell>
                    )}

                    {/* Machine (for After Cleaning) */}
                    {isAC && (
                      <TableCell {...cellProps(idx, colIndexByKey.machineId)}>
                        <Select
                          size="small"
                          fullWidth
                          displayEmpty
                          value={row.machineId ?? ""}
                          error={Boolean(errors.machineId)}
                          onChange={(e) => onChangeRow(idx, "machineId", e.target.value as number | "")}
                          sx={{ fontSize: 12 }}
                          SelectDisplayProps={{
                            "aria-describedby": errors.machineId ? cellErrorId(idx, "machineId") : undefined
                          }}
                          inputProps={{ "aria-label": `Sample ${idx + 1} machine` }}
                        >
                          <MenuItem value="">
                            <em style={{ color: theme.palette.text.secondary }}>Select Machine</em>
                          </MenuItem>
                          {masterData.machines.map((m) => (
                            <MenuItem key={m.id} value={m.id}>
                              {m.name}
                            </MenuItem>
                          ))}
                        </Select>
                        {renderCellError(idx, "machineId", errors.machineId)}
                      </TableCell>
                    )}

                    {/* Cause of Testing */}
                    <TableCell {...cellProps(idx, colIndexByKey.causeOfTestingId)}>
                      <Select
                        size="small"
                        fullWidth
                        displayEmpty
                        value={row.causeOfTestingId ?? ""}
                        error={Boolean(errors.causeOfTestingId)}
                        onChange={(e) => onChangeRow(idx, "causeOfTestingId", e.target.value as number | "")}
                        sx={{ fontSize: 12 }}
                        SelectDisplayProps={{
                          "aria-describedby": errors.causeOfTestingId ? cellErrorId(idx, "causeOfTestingId") : undefined
                        }}
                        inputProps={{ "aria-label": `Sample ${idx + 1} cause of testing` }}
                      >
                        <MenuItem value="">
                          <em style={{ color: theme.palette.text.secondary }}>Cause of Testing</em>
                        </MenuItem>
                        {masterData.causes.map((c) => (
                          <MenuItem key={c.id} value={c.id}>
                            {c.name}
                          </MenuItem>
                        ))}
                      </Select>
                      {renderCellError(idx, "causeOfTestingId", errors.causeOfTestingId)}
                    </TableCell>

                    {/* Sample Quantity */}
                    {!isEM && !isAC && (
                      <TableCell {...cellProps(idx, colIndexByKey.sampleQuantity)}>
                        <TextField
                          size="small"
                          fullWidth
                          placeholder="e.g. 50g / 100ml"
                          value={row.sampleQuantity ?? ""}
                          error={Boolean(errors.sampleQuantity)}
                          onChange={(e) => onChangeRow(idx, "sampleQuantity", e.target.value)}
                          slotProps={{
                            htmlInput: {
                              "aria-label": `Sample ${idx + 1} quantity`,
                              "aria-describedby": errors.sampleQuantity ? cellErrorId(idx, "sampleQuantity") : undefined,
                              style: { fontSize: 12 }
                            }
                          }}
                        />
                        {renderCellError(idx, "sampleQuantity", errors.sampleQuantity)}
                      </TableCell>
                    )}

                    {/* Sampled By */}
                    <TableCell {...cellProps(idx, colIndexByKey.sampledBy)}>
                      <Autocomplete
                        freeSolo
                        size="small"
                        options={masterData.samplers.map((s) => s.name)}
                        inputValue={row.sampledBy ?? ""}
                        onInputChange={(_, v) => onChangeRow(idx, "sampledBy", v)}
                        renderInput={(params) => (
                          <TextField
                            {...params}
                            placeholder="Sampled By"
                            error={Boolean(errors.sampledBy)}
                            slotProps={{
                              ...params.slotProps,
                              htmlInput: {
                                ...params.slotProps.htmlInput,
                                "aria-label": `Sample ${idx + 1} sampled by`,
                                "aria-describedby": errors.sampledBy ? cellErrorId(idx, "sampledBy") : undefined,
                                style: { fontSize: 12 }
                              }
                            }}
                          />
                        )}
                      />
                      {renderCellError(idx, "sampledBy", errors.sampledBy)}
                    </TableCell>

                    {/* Batch Number */}
                    {isItemBased && (
                      <TableCell {...cellProps(idx, colIndexByKey.batchNumber)}>
                        <TextField
                          size="small"
                          fullWidth
                          placeholder="Batch No."
                          value={row.batchNumber ?? ""}
                          error={Boolean(errors.batchNumber)}
                          onChange={(e) => onChangeRow(idx, "batchNumber", e.target.value)}
                          slotProps={{
                            htmlInput: {
                              "aria-label": `Sample ${idx + 1} batch number`,
                              "aria-describedby": errors.batchNumber ? cellErrorId(idx, "batchNumber") : undefined,
                              style: { fontSize: 12 }
                            }
                          }}
                        />
                        {renderCellError(idx, "batchNumber", errors.batchNumber)}
                      </TableCell>
                    )}

                    {!isItemBased && (
                      <TableCell {...cellProps(idx, colIndexByKey.sampledAt)}>
                        <TextField
                          type="datetime-local"
                          size="small"
                          fullWidth
                          value={row.sampledAt ?? ""}
                          error={Boolean(errors.sampledAt)}
                          onChange={(e) => onChangeRow(idx, "sampledAt", e.target.value)}
                          slotProps={{
                            htmlInput: {
                              "aria-label": `Sample ${idx + 1} sampled at`,
                              "aria-describedby": errors.sampledAt ? cellErrorId(idx, "sampledAt") : undefined,
                              style: { fontSize: 12 }
                            }
                          }}
                        />
                        {renderCellError(idx, "sampledAt", errors.sampledAt)}
                      </TableCell>
                    )}

                    {/* Control Number */}
                    <TableCell {...cellProps(idx, colIndexByKey.controlNumber)}>
                      <TextField
                        size="small"
                        fullWidth
                        placeholder="Control No."
                        value={row.controlNumber ?? ""}
                        error={Boolean(errors.controlNumber)}
                        onChange={(e) => onChangeRow(idx, "controlNumber", e.target.value)}
                        slotProps={{
                          htmlInput: {
                            "aria-label": `Sample ${idx + 1} control number`,
                            "aria-describedby": errors.controlNumber ? cellErrorId(idx, "controlNumber") : undefined,
                            style: { fontSize: 12 }
                          }
                        }}
                      />
                      {renderCellError(idx, "controlNumber", errors.controlNumber)}
                    </TableCell>

                    {/* Mfg Date & Exp Date */}
                    {isItemBased && (
                      <>
                        <TableCell {...cellProps(idx, colIndexByKey.mfgDate)}>
                          <DateField
                            fullWidth
                            value={row.mfgDate ?? ""}
                            error={Boolean(errors.mfgDate)}
                            onChange={(v) => onChangeRow(idx, "mfgDate", v)}
                            ariaLabel={`Sample ${idx + 1} manufacturing date`}
                            ariaDescribedBy={errors.mfgDate ? cellErrorId(idx, "mfgDate") : undefined}
                          />
                          {renderCellError(idx, "mfgDate", errors.mfgDate)}
                        </TableCell>
                        <TableCell {...cellProps(idx, colIndexByKey.expDate)}>
                          <DateField
                            fullWidth
                            value={row.expDate ?? ""}
                            error={Boolean(errors.expDate)}
                            onChange={(v) => onChangeRow(idx, "expDate", v)}
                            ariaLabel={`Sample ${idx + 1} expiry date`}
                            ariaDescribedBy={errors.expDate ? cellErrorId(idx, "expDate") : undefined}
                          />
                          {renderCellError(idx, "expDate", errors.expDate)}
                        </TableCell>
                      </>
                    )}

                    {/* Action: Delete Row */}
                    <TableCell align="center">
                      <Tooltip title={rows.length === 1 ? "At least one row is required" : "Delete sample row"}>
                        <span>
                          <IconButton aria-label={rows.length === 1 ? "At least one row is required" : "Delete sample row"}
                            size="small"
                            disabled={rows.length === 1}
                            onClick={() => onDeleteRow(idx)}
                            sx={{
                              color: rows.length === 1 ? "text.disabled" : theme.custom.status.detected.text,
                              "&:hover": rows.length > 1 ? { bgcolor: theme.custom.status.detected.bg } : undefined
                            }}
                          >
                            <DeleteOutlineIcon sx={{ fontSize: 18 }} />
                          </IconButton>
                        </span>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </Box>
      </Paper>

      {/* Add Another Sample Button */}
      <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
        <Button
          variant="outlined"
          size="small"
          disabled={atRowLimit || Boolean(receivedByRow)}
          aria-describedby={atRowLimit ? addRowLimitId : undefined}
          onClick={onAddRow}
          startIcon={<AddIcon sx={{ fontSize: 18 }} />}
          sx={{
            borderColor: theme.palette.primary.main,
            color: theme.palette.primary.main,
            fontSize: 13,
            fontWeight: 600,
            py: 0.75,
            px: 2,
            "&:hover": {
              borderColor: theme.palette.primary.main,
              bgcolor: theme.custom.status.purple.bg
            }
          }}
        >
          Add Another Sample
        </Button>
        {atRowLimit && (
          <Typography id={addRowLimitId} sx={{ fontSize: 12, color: "text.secondary" }}>
            Maximum {MAX_RECEIVE_ROWS} rows per batch.
          </Typography>
        )}
      </Box>
    </Box>
  );
}
