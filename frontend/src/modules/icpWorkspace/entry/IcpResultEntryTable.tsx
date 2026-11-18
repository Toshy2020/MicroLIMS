import {
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Chip,
  Tooltip,
  Typography,
  useTheme
} from "@mui/material";
import { tableHeadSx } from "../../../theme";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import type { IcpEntryElementDto, IcpTestMode } from "../types";

export interface IcpResultEntryTableProps {
  mode: IcpTestMode;
  elements: IcpEntryElementDto[];
  results: Record<number, string>;
  onResultChange: (icpTestElementId: number, value: string) => void;
  disabled?: boolean;
}

// One row per element the test reports: the target (assay), the instrument-reported ppm typed from the
// device report, and whether the run's calibration allows the element.
export function IcpResultEntryTable({ mode, elements, results, onResultChange, disabled = false }: IcpResultEntryTableProps) {
  const { t } = useTranslation("icpWorkspace");
  const theme = useTheme();

  return (
    <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1.5, overflowX: "auto" }}>
      <Table size="small">
        <TableHead sx={tableHeadSx}>
          <TableRow>
            <TableCell sx={{ minWidth: 90 }}>{t("entry.resultTable.columns.element")}</TableCell>
            {mode === "Assay" && <TableCell sx={{ minWidth: 120 }}>{t("entry.resultTable.columns.target")}</TableCell>}
            <TableCell sx={{ minWidth: 200 }}>{t("entry.resultTable.columns.instrumentResult")}</TableCell>
            <TableCell sx={{ minWidth: 160 }}>{t("entry.resultTable.columns.calibration")}</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {elements.map((el) => (
            <TableRow key={el.icpTestElementId} hover>
              <TableCell>
                <Typography variant="body2" sx={{ fontFamily: monospaceFontFamily, fontWeight: 700 }}>
                  <Ltr>{el.symbol}</Ltr>
                </Typography>
              </TableCell>
              {mode === "Assay" && (
                <TableCell>
                  <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
                    <Ltr>{el.targetPpm ?? "—"}</Ltr>
                  </Typography>
                </TableCell>
              )}
              <TableCell>
                <TextField
                  size="small"
                  type="number"
                  value={results[el.icpTestElementId] ?? ""}
                  onChange={(e) => onResultChange(el.icpTestElementId, e.target.value)}
                  disabled={disabled}
                  placeholder="0.000"
                  slotProps={{
                    htmlInput: {
                      min: 0,
                      step: "any",
                      style: { fontVariantNumeric: "tabular-nums" },
                      "aria-label": t("entry.resultTable.resultAria", { symbol: el.symbol })
                    }
                  }}
                  sx={{ width: 180 }}
                />
              </TableCell>
              <TableCell>
                {el.valid ? (
                  <Chip size="small" color="success" variant="outlined" label={t("entry.resultTable.valid")} sx={{ fontWeight: 600 }} />
                ) : (
                  <Tooltip title={el.reason ?? t("entry.resultTable.notAvailable")} arrow>
                    <Chip
                      size="small"
                      color="error"
                      variant="outlined"
                      label={el.reason ?? t("entry.resultTable.notAvailable")}
                      sx={{ fontWeight: 600, maxWidth: 320 }}
                    />
                  </Tooltip>
                )}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
