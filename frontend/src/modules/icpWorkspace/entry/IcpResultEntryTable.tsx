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
  const theme = useTheme();

  return (
    <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1.5, overflowX: "auto" }}>
      <Table size="small">
        <TableHead sx={tableHeadSx}>
          <TableRow>
            <TableCell sx={{ minWidth: 90 }}>Element</TableCell>
            {mode === "Assay" && <TableCell sx={{ minWidth: 120 }}>Target (ppm)</TableCell>}
            <TableCell sx={{ minWidth: 200 }}>Result from instrument report (ppm)</TableCell>
            <TableCell sx={{ minWidth: 160 }}>Calibration</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {elements.map((el) => (
            <TableRow key={el.icpTestElementId} hover>
              <TableCell>
                <Typography variant="body2" sx={{ fontFamily: monospaceFontFamily, fontWeight: 700 }}>
                  {el.symbol}
                </Typography>
              </TableCell>
              {mode === "Assay" && (
                <TableCell>
                  <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
                    {el.targetPpm ?? "—"}
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
                      "aria-label": `${el.symbol} result (ppm)`
                    }
                  }}
                  sx={{ width: 180 }}
                />
              </TableCell>
              <TableCell>
                {el.valid ? (
                  <Chip size="small" color="success" variant="outlined" label="Valid" sx={{ fontWeight: 600 }} />
                ) : (
                  <Tooltip title={el.reason ?? "Not available"} arrow>
                    <Chip
                      size="small"
                      color="error"
                      variant="outlined"
                      label={el.reason ?? "Not available"}
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
