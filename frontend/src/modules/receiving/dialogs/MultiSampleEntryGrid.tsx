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
  useTheme
} from "@mui/material";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import AddIcon from "@mui/icons-material/Add";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import { ReceiveRowItem, SampleCategoryKey } from "../types/receivingTypes";
import { SAMPLED_BY_SUGGESTIONS, PRODUCTION_STAGES } from "../../../services/masterDataOptions";
import { brandColors } from "../../../theme";

interface MasterData {
  items: any[];
  waterPoints: any[];
  waterDepartments: any[];
  departments: any[];
  machines: any[];
  causes: any[];
}

interface Props {
  category: SampleCategoryKey;
  rows: ReceiveRowItem[];
  masterData: MasterData;
  onChangeRow: (index: number, field: string, value: any) => void;
  onAddRow: () => void;
  onDeleteRow: (index: number) => void;
}

export function MultiSampleEntryGrid({
  category,
  rows,
  masterData,
  onChangeRow,
  onAddRow,
  onDeleteRow
}: Props) {
  const theme = useTheme();
  const isItemBased = category === "product" || category === "rm" || category === "pm";
  const isWater = category === "water";
  const isEM = category === "em";
  const isAC = category === "ac";

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
          <Table size="small" stickyHeader sx={{ minWidth: isItemBased ? 1200 : 780 }}>
            <TableHead>
              <TableRow sx={{ "& th": { bgcolor: "background.default", fontWeight: 700, fontSize: 12, py: 1.25 } }}>
                <TableCell sx={{ width: 40 }}>#</TableCell>

                {isItemBased && (
                  <TableCell sx={{ minWidth: 200 }}>
                    Item <span style={{ color: theme.custom.status.detected.text }}>*</span>
                  </TableCell>
                )}

                {category === "product" && (
                  <TableCell sx={{ minWidth: 140 }}>Production Stage</TableCell>
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
                  <TableCell sx={{ minWidth: 130 }}>Batch No.</TableCell>
                )}

                <TableCell sx={{ minWidth: 130 }}>Control No.</TableCell>

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
                    </TableCell>

                    {/* Item (for Product, RM, PM) */}
                    {isItemBased && (
                      <TableCell>
                        <Select
                          size="small"
                          fullWidth
                          displayEmpty
                          value={row.itemId ?? ""}
                          error={Boolean(errors.itemId)}
                          onChange={(e) => onChangeRow(idx, "itemId", e.target.value)}
                          sx={{ fontSize: 12 }}
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
                      </TableCell>
                    )}

                    {/* Production Stage (for Product only) */}
                    {category === "product" && (
                      <TableCell>
                        <Select
                          size="small"
                          fullWidth
                          displayEmpty
                          value={row.productionStage ?? ""}
                          onChange={(e) => onChangeRow(idx, "productionStage", e.target.value)}
                          sx={{ fontSize: 12 }}
                        >
                          <MenuItem value="">
                            <em style={{ color: theme.palette.text.secondary }}>Stage</em>
                          </MenuItem>
                          {PRODUCTION_STAGES.map((s) => (
                            <MenuItem key={s} value={s}>
                              {s}
                            </MenuItem>
                          ))}
                        </Select>
                      </TableCell>
                    )}

                    {/* Department (for Water) */}
                    {isWater && (
                      <TableCell>
                        <Select
                          size="small"
                          fullWidth
                          displayEmpty
                          value={row.departmentId ?? ""}
                          error={Boolean(errors.departmentId)}
                          onChange={(e) => onChangeRow(idx, "departmentId", e.target.value)}
                          sx={{ fontSize: 12 }}
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
                      </TableCell>
                    )}

                    {/* Department (for EM) */}
                    {isEM && (
                      <TableCell>
                        <Select
                          size="small"
                          fullWidth
                          displayEmpty
                          value={row.departmentId ?? ""}
                          error={Boolean(errors.departmentId)}
                          onChange={(e) => onChangeRow(idx, "departmentId", e.target.value)}
                          sx={{ fontSize: 12 }}
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
                      </TableCell>
                    )}

                    {/* Machine (for After Cleaning) */}
                    {isAC && (
                      <TableCell>
                        <Select
                          size="small"
                          fullWidth
                          displayEmpty
                          value={row.machineId ?? ""}
                          error={Boolean(errors.machineId)}
                          onChange={(e) => onChangeRow(idx, "machineId", e.target.value)}
                          sx={{ fontSize: 12 }}
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
                      </TableCell>
                    )}

                    {/* Cause of Testing */}
                    <TableCell>
                      <Select
                        size="small"
                        fullWidth
                        displayEmpty
                        value={row.causeOfTestingId ?? ""}
                        error={Boolean(errors.causeOfTestingId)}
                        onChange={(e) => onChangeRow(idx, "causeOfTestingId", e.target.value)}
                        sx={{ fontSize: 12 }}
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
                    </TableCell>

                    {/* Sample Quantity */}
                    {!isEM && !isAC && (
                      <TableCell>
                        <TextField
                          size="small"
                          fullWidth
                          placeholder="e.g. 50g / 100ml"
                          value={row.sampleQuantity ?? ""}
                          onChange={(e) => onChangeRow(idx, "sampleQuantity", e.target.value)}
                          inputProps={{ style: { fontSize: 12 } }}
                        />
                      </TableCell>
                    )}

                    {/* Sampled By */}
                    <TableCell>
                      <Autocomplete
                        freeSolo
                        size="small"
                        options={SAMPLED_BY_SUGGESTIONS}
                        inputValue={row.sampledBy ?? ""}
                        onInputChange={(_, v) => onChangeRow(idx, "sampledBy", v)}
                        renderInput={(params) => (
                          <TextField
                            {...params}
                            placeholder="Sampled By"
                            error={Boolean(errors.sampledBy)}
                            inputProps={{ ...params.inputProps, style: { fontSize: 12 } }}
                          />
                        )}
                      />
                    </TableCell>

                    {/* Batch Number */}
                    {isItemBased && (
                      <TableCell>
                        <TextField
                          size="small"
                          fullWidth
                          placeholder="Batch No."
                          value={row.batchNumber ?? ""}
                          onChange={(e) => onChangeRow(idx, "batchNumber", e.target.value)}
                          inputProps={{ style: { fontSize: 12 } }}
                        />
                      </TableCell>
                    )}

                    {/* Control Number */}
                    <TableCell>
                      <TextField
                        size="small"
                        fullWidth
                        placeholder="Control No."
                        value={row.controlNumber ?? ""}
                        onChange={(e) => onChangeRow(idx, "controlNumber", e.target.value)}
                        inputProps={{ style: { fontSize: 12 } }}
                      />
                    </TableCell>

                    {/* Mfg Date & Exp Date */}
                    {isItemBased && (
                      <>
                        <TableCell>
                          <TextField
                            type="date"
                            size="small"
                            fullWidth
                            value={row.mfgDate ?? ""}
                            onChange={(e) => onChangeRow(idx, "mfgDate", e.target.value)}
                            inputProps={{ style: { fontSize: 12 } }}
                          />
                        </TableCell>
                        <TableCell>
                          <TextField
                            type="date"
                            size="small"
                            fullWidth
                            value={row.expDate ?? ""}
                            error={Boolean(errors.expDate)}
                            onChange={(e) => onChangeRow(idx, "expDate", e.target.value)}
                            inputProps={{ style: { fontSize: 12 } }}
                          />
                        </TableCell>
                      </>
                    )}

                    {/* Action: Delete Row */}
                    <TableCell align="center">
                      <Tooltip title={rows.length === 1 ? "At least one row is required" : "Delete sample row"}>
                        <span>
                          <IconButton
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
      <Box sx={{ display: "flex", justifyContent: "flex-start" }}>
        <Button
          variant="outlined"
          size="small"
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
      </Box>
    </Box>
  );
}
