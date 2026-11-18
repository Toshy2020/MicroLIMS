import {
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Chip,
  Typography,
  Tooltip,
  IconButton,
  Button,
  useTheme
} from "@mui/material";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import AddIcon from "@mui/icons-material/Add";
import { tableHeadSx } from "../../../theme";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import type { IcpCalibrationElementDto } from "../types";
import type { CalibrationRowInput } from "./calibrationPayload";

export interface IcpCalibrationElementTableProps {
  rows: CalibrationRowInput[];
  onRowsChange: (rows: CalibrationRowInput[]) => void;
  // The server's verdict for the last saved values, matched to a row by symbol.
  savedElements: IcpCalibrationElementDto[];
  minCorrelation: number;
  disabled?: boolean;
}

const sameSymbol = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase();

// The analyst decides how many metals were calibrated: add a row per element and type the r from the curve.
export function IcpCalibrationElementTable({
  rows,
  onRowsChange,
  savedElements,
  minCorrelation,
  disabled = false
}: IcpCalibrationElementTableProps) {
  const { t } = useTranslation("icpWorkspace");
  const theme = useTheme();

  const update = (index: number, patch: Partial<CalibrationRowInput>) =>
    onRowsChange(rows.map((row, i) => (i === index ? { ...row, ...patch } : row)));

  return (
    <>
      <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1.5, overflowX: "auto" }}>
        <Table size="small">
          <TableHead sx={tableHeadSx}>
            <TableRow>
              <TableCell sx={{ minWidth: 100 }}>{t("calibration.elementTable.columns.element")}</TableCell>
              <TableCell sx={{ minWidth: 160 }}>
                {t("calibration.elementTable.columns.correlation")}
                <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontSize: 12 }}>
                  {t("calibration.elementTable.columns.minCorrelation", { min: minCorrelation })}
                </Typography>
              </TableCell>
              <TableCell sx={{ minWidth: 160 }}>{t("calibration.elementTable.columns.result")}</TableCell>
              {!disabled && <TableCell sx={{ width: 56 }} />}
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.length === 0 && (
              <TableRow>
                <TableCell colSpan={disabled ? 3 : 4}>
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    {t("calibration.elementTable.empty")}
                  </Typography>
                </TableCell>
              </TableRow>
            )}
            {rows.map((row, index) => {
              const saved = savedElements.find((el) => sameSymbol(el.symbol, row.symbol));
              return (
                <TableRow key={index} hover>
                  <TableCell>
                    {disabled ? (
                      <Typography variant="body2" sx={{ fontFamily: monospaceFontFamily, fontWeight: 700 }}>
                        <Ltr>{row.symbol}</Ltr>
                      </Typography>
                    ) : (
                      <TextField
                        size="small"
                        value={row.symbol}
                        onChange={(e) => update(index, { symbol: e.target.value })}
                        placeholder="Zn"
                        slotProps={{ htmlInput: { maxLength: 3, "aria-label": t("calibration.elementTable.elementAria", { num: index + 1 }) } }}
                        sx={{ width: 90 }}
                      />
                    )}
                  </TableCell>
                  <TableCell>
                    {disabled ? (
                      <Typography variant="body2" sx={{ fontFamily: monospaceFontFamily }}>
                        <Ltr>{row.correlationR || "—"}</Ltr>
                      </Typography>
                    ) : (
                      <TextField
                        size="small"
                        type="number"
                        value={row.correlationR}
                        onChange={(e) => update(index, { correlationR: e.target.value })}
                        placeholder="0.9995"
                        slotProps={{ htmlInput: { step: "any", "aria-label": t("calibration.elementTable.correlationAria", { num: index + 1 }) } }}
                        sx={{ width: 140 }}
                      />
                    )}
                  </TableCell>
                  <TableCell>
                    {saved ? (
                      saved.passed ? (
                        <Chip size="small" color="success" variant="outlined" label={t("calibration.elementTable.passed")} sx={{ fontWeight: 600 }} />
                      ) : (
                        <Tooltip title={saved.failureReasons ?? t("calibration.elementTable.failed")} arrow>
                          <Chip size="small" color="error" variant="outlined" label={saved.failureReasons ?? t("calibration.elementTable.failed")} sx={{ fontWeight: 600, maxWidth: 320 }} />
                        </Tooltip>
                      )
                    ) : (
                      <Typography variant="caption" sx={{ color: "text.secondary" }}>
                        {t("calibration.elementTable.saveToCheck")}
                      </Typography>
                    )}
                  </TableCell>
                  {!disabled && (
                    <TableCell>
                      <IconButton
                        size="small"
                        aria-label={t("calibration.elementTable.removeAria", { num: index + 1 })}
                        onClick={() => onRowsChange(rows.filter((_, i) => i !== index))}
                      >
                        <DeleteOutlineIcon fontSize="small" />
                      </IconButton>
                    </TableCell>
                  )}
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>
      {!disabled && (
        <Button
          size="small"
          startIcon={<AddIcon />}
          onClick={() => onRowsChange([...rows, { symbol: "", correlationR: "" }])}
          sx={{ mt: 1, textTransform: "none" }}
        >
          {t("calibration.elementTable.addElement")}
        </Button>
      )}
    </>
  );
}
