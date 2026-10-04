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
  Select,
  MenuItem,
  IconButton,
  Tooltip,
  Typography,
  Paper,
  Stack,
  Alert
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import type { IcpAnalyteView } from "../services/IcpMethodService";
import {
  IcpElementRowState,
  ANALYTE_VIEW_OPTIONS,
  normaliseSymbol
} from "./icpMethodForm";
import type { IcpMethodErrors } from "./icpMethodValidation";

export interface IcpElementsTableProps {
  elements: IcpElementRowState[];
  errors: IcpMethodErrors;
  onAddElement: () => void;
  onRemoveElement: (idx: number) => void;
  onMoveElement: (idx: number, direction: "up" | "down") => void;
  onUpdateElement: <K extends keyof IcpElementRowState>(
    idx: number,
    field: K,
    val: IcpElementRowState[K]
  ) => void;
}

export function IcpElementsTable({
  elements,
  errors,
  onAddElement,
  onRemoveElement,
  onMoveElement,
  onUpdateElement
}: IcpElementsTableProps) {
  const getFieldError = (idx: number, field: string) => {
    return errors[`element_${idx}_${field}`];
  };

  return (
    <Box>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
            Method Elements & Emission Lines
          </Typography>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Configure monitored element wavelengths, optical viewing configuration, and conversion factors
          </Typography>
        </Box>
        <Button
          variant="outlined"
          size="small"
          startIcon={<AddIcon />}
          onClick={onAddElement}
          sx={{ textTransform: "none", fontWeight: 600 }}
        >
          Add Element
        </Button>
      </Stack>

      {errors.icp_form_elements && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {errors.icp_form_elements}
        </Alert>
      )}

      {elements.length === 0 ? (
        <Paper
          variant="outlined"
          sx={{
            py: 4,
            px: 2,
            textAlign: "center",
            bgcolor: "action.hover",
            borderRadius: 1.5
          }}
        >
          <ScienceOutlinedIcon sx={{ fontSize: 36, color: "text.disabled", mb: 1 }} />
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 1.5 }}>
            No elements configured for this ICP method.
          </Typography>
          <Button
            variant="contained"
            size="small"
            startIcon={<AddIcon />}
            onClick={onAddElement}
            sx={{ textTransform: "none" }}
          >
            Add First Element
          </Button>
        </Paper>
      ) : (
        <TableContainer
          component={Paper}
          variant="outlined"
          sx={{ borderRadius: 1.5, overflowX: "auto" }}
        >
          <Table size="small">
            <TableHead>
              <TableRow sx={{ bgcolor: "action.hover" }}>
                <TableCell sx={{ width: 44, fontWeight: 700 }}>#</TableCell>
                <TableCell sx={{ minWidth: 120, fontWeight: 700 }}>Symbol *</TableCell>
                <TableCell sx={{ minWidth: 150, fontWeight: 700 }}>Wavelength (nm) *</TableCell>
                <TableCell sx={{ minWidth: 130, fontWeight: 700 }}>Optical View *</TableCell>
                <TableCell sx={{ minWidth: 140, fontWeight: 700 }}>Conversion Factor *</TableCell>
                <TableCell sx={{ width: 140, textAlign: "center", fontWeight: 700 }}>Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {elements.map((e, idx) => {
                const symbolError = getFieldError(idx, "symbol");
                const wlError = getFieldError(idx, "wavelengthNm");
                const cfError = getFieldError(idx, "conversionFactor");

                return (
                  <TableRow key={idx} hover>
                    {/* Index & ID */}
                    <TableCell sx={{ verticalAlign: "top", pt: 2 }}>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {idx + 1}
                      </Typography>
                      {e.id != null && (
                        <Typography variant="caption" sx={{ color: "text.disabled", display: "block" }}>
                          ID:{e.id}
                        </Typography>
                      )}
                    </TableCell>

                    {/* Symbol */}
                    <TableCell sx={{ verticalAlign: "top", pt: 1.5 }}>
                      <TextField
                        size="small"
                        placeholder="e.g. Zn"
                        value={e.symbol}
                        onChange={(ev) => onUpdateElement(idx, "symbol", ev.target.value)}
                        onBlur={() => onUpdateElement(idx, "symbol", normaliseSymbol(e.symbol))}
                        error={Boolean(symbolError)}
                        helperText={symbolError}
                        slotProps={{ htmlInput: { maxLength: 3 } }}
                        fullWidth
                      />
                    </TableCell>

                    {/* Wavelength */}
                    <TableCell sx={{ verticalAlign: "top", pt: 1.5 }}>
                      <TextField
                        size="small"
                        placeholder="e.g. 213.857"
                        type="number"
                        slotProps={{ htmlInput: { step: "any", min: "0" } }}
                        value={e.wavelengthNm}
                        onChange={(ev) => onUpdateElement(idx, "wavelengthNm", ev.target.value)}
                        error={Boolean(wlError)}
                        helperText={wlError}
                        fullWidth
                      />
                    </TableCell>

                    {/* View */}
                    <TableCell sx={{ verticalAlign: "top", pt: 1.5 }}>
                      <Select
                        size="small"
                        value={e.view}
                        onChange={(ev) => onUpdateElement(idx, "view", ev.target.value as IcpAnalyteView)}
                        fullWidth
                      >
                        {ANALYTE_VIEW_OPTIONS.map((opt) => (
                          <MenuItem key={opt.value} value={opt.value}>
                            {opt.label}
                          </MenuItem>
                        ))}
                      </Select>
                    </TableCell>

                    {/* Conversion Factor */}
                    <TableCell sx={{ verticalAlign: "top", pt: 1.5 }}>
                      <TextField
                        size="small"
                        placeholder="1.0"
                        type="number"
                        slotProps={{ htmlInput: { step: "any", min: "0.0001" } }}
                        value={e.conversionFactor}
                        onChange={(ev) => onUpdateElement(idx, "conversionFactor", ev.target.value)}
                        error={Boolean(cfError)}
                        helperText={cfError}
                        fullWidth
                      />
                    </TableCell>

                    {/* Actions: Reorder & Delete */}
                    <TableCell sx={{ verticalAlign: "top", pt: 1.5, textAlign: "center" }}>
                      <Stack direction="row" spacing={0.5} sx={{ justifyContent: "center" }}>
                        <Tooltip title="Move up">
                          <span>
                            <IconButton
                              aria-label={`Move element ${e.symbol || idx + 1} up`}
                              size="small"
                              onClick={() => onMoveElement(idx, "up")}
                              disabled={idx === 0}
                            >
                              <ArrowUpwardIcon fontSize="small" />
                            </IconButton>
                          </span>
                        </Tooltip>
                        <Tooltip title="Move down">
                          <span>
                            <IconButton
                              aria-label={`Move element ${e.symbol || idx + 1} down`}
                              size="small"
                              onClick={() => onMoveElement(idx, "down")}
                              disabled={idx === elements.length - 1}
                            >
                              <ArrowDownwardIcon fontSize="small" />
                            </IconButton>
                          </span>
                        </Tooltip>
                        <Tooltip title="Delete element">
                          <span>
                            <IconButton
                              aria-label={`Delete element ${e.symbol || idx + 1}`}
                              size="small"
                              color="error"
                              onClick={() => onRemoveElement(idx)}
                              disabled={elements.length <= 1}
                            >
                              <DeleteOutlineIcon fontSize="small" />
                            </IconButton>
                          </span>
                        </Tooltip>
                      </Stack>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
