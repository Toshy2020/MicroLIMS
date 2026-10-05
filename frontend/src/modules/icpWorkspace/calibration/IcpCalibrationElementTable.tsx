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
  useTheme
} from "@mui/material";
import { tableHeadSx } from "../../../theme";
import { monospaceFontFamily } from "../../../theme/palette";
import type {
  IcpCalibrationElementDto,
  IcpMethodResponse,
  IcpCalibrationStatus
} from "../types";

export interface ElementInputState {
  correlationR: string;
  blankMgPerL: string;
  icvMeasuredMgPerL: string;
}

export interface IcpCalibrationElementTableProps {
  status: IcpCalibrationStatus;
  elements: IcpCalibrationElementDto[];
  method: IcpMethodResponse;
  elementInputs: Record<number, ElementInputState>;
  onInputChange: (calElementId: number, field: keyof ElementInputState, value: string) => void;
  disabled?: boolean;
}

export function IcpCalibrationElementTable({
  status,
  elements,
  method,
  elementInputs,
  onInputChange,
  disabled = false
}: IcpCalibrationElementTableProps) {
  const theme = useTheme();
  const isConfirmed = status === "Confirmed";

  return (
    <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1.5, overflowX: "auto" }}>
      <Table size="small">
        <TableHead sx={tableHeadSx}>
          <TableRow>
            <TableCell sx={{ minWidth: 80 }}>Symbol</TableCell>
            <TableCell sx={{ minWidth: 120 }}>Wavelength (nm)</TableCell>
            <TableCell sx={{ minWidth: 160 }}>
              Correlation (r)
              <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontSize: 12 }}>
                min {method.minCorrelation.toFixed(4)}
              </Typography>
            </TableCell>
            {method.requireBlank && (
              <TableCell sx={{ minWidth: 140 }}>
                Blank (mg/L)
                {method.blankMaxMgPerL != null && (
                  <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontSize: 12 }}>
                    max {method.blankMaxMgPerL}
                  </Typography>
                )}
              </TableCell>
            )}
            {method.requireIcv && (
              <TableCell sx={{ minWidth: 140 }}>
                ICV Measured (mg/L)
                {method.icvNominalMgPerL != null && (
                  <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontSize: 12 }}>
                    nominal {method.icvNominalMgPerL}
                  </Typography>
                )}
              </TableCell>
            )}
            {method.requireIcv && (
              <TableCell sx={{ minWidth: 130 }}>
                ICV Recovery
                <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontSize: 12 }}>
                  {method.icvRecoveryLowPercent}%–{method.icvRecoveryHighPercent}%
                </Typography>
              </TableCell>
            )}
            <TableCell sx={{ minWidth: 100 }}>Status</TableCell>
            <TableCell sx={{ minWidth: 200 }}>Failure Reasons</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {elements.map((el) => {
            const methodEl = method.elements?.find((m) => m.id === el.icpMethodElementId || m.symbol === el.symbol);
            const wavelength = methodEl?.wavelengthNm;
            const inputState = elementInputs[el.id] || {
              correlationR: el.correlationR != null ? String(el.correlationR) : "",
              blankMgPerL: el.blankMgPerL != null ? String(el.blankMgPerL) : "",
              icvMeasuredMgPerL: el.icvMeasuredMgPerL != null ? String(el.icvMeasuredMgPerL) : ""
            };

            const hasAnyInput = el.correlationR != null || el.blankMgPerL != null || el.icvMeasuredMgPerL != null;

            return (
              <TableRow key={el.id}>
                {/* Symbol */}
                <TableCell sx={{ fontWeight: 700, fontSize: 14 }}>
                  {el.symbol}
                </TableCell>

                {/* Wavelength */}
                <TableCell sx={{ fontFamily: monospaceFontFamily }}>
                  {typeof wavelength === "number" ? wavelength.toFixed(3) : wavelength ?? "—"}
                </TableCell>

                {/* Correlation r */}
                <TableCell>
                  {isConfirmed ? (
                    <span style={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>
                      {el.correlationR != null ? Number(el.correlationR).toFixed(6) : "—"}
                    </span>
                  ) : (
                    <TextField
                      size="small"
                      type="number"
                      placeholder="e.g. 0.999500"
                      value={inputState.correlationR}
                      onChange={(e) => onInputChange(el.id, "correlationR", e.target.value)}
                      disabled={disabled}
                      slotProps={{
                        htmlInput: {
                          step: "0.000001",
                          min: 0,
                          max: 1,
                          style: { fontFamily: monospaceFontFamily, fontSize: 13, padding: "6px 8px" }
                        }
                      }}
                      sx={{ width: 140 }}
                    />
                  )}
                </TableCell>

                {/* Blank mg/L */}
                {method.requireBlank && (
                  <TableCell>
                    {isConfirmed ? (
                      <span style={{ fontFamily: monospaceFontFamily }}>
                        {el.blankMgPerL != null ? Number(el.blankMgPerL).toFixed(4) : "—"}
                      </span>
                    ) : (
                      <TextField
                        size="small"
                        type="number"
                        placeholder="0.0000"
                        value={inputState.blankMgPerL}
                        onChange={(e) => onInputChange(el.id, "blankMgPerL", e.target.value)}
                        disabled={disabled}
                        slotProps={{
                          htmlInput: {
                            step: "any",
                            style: { fontFamily: monospaceFontFamily, fontSize: 13, padding: "6px 8px" }
                          }
                        }}
                        sx={{ width: 120 }}
                      />
                    )}
                  </TableCell>
                )}

                {/* ICV Measured mg/L */}
                {method.requireIcv && (
                  <TableCell>
                    {isConfirmed ? (
                      <span style={{ fontFamily: monospaceFontFamily }}>
                        {el.icvMeasuredMgPerL != null ? Number(el.icvMeasuredMgPerL).toFixed(4) : "—"}
                      </span>
                    ) : (
                      <TextField
                        size="small"
                        type="number"
                        placeholder="0.0000"
                        value={inputState.icvMeasuredMgPerL}
                        onChange={(e) => onInputChange(el.id, "icvMeasuredMgPerL", e.target.value)}
                        disabled={disabled}
                        slotProps={{
                          htmlInput: {
                            step: "any",
                            style: { fontFamily: monospaceFontFamily, fontSize: 13, padding: "6px 8px" }
                          }
                        }}
                        sx={{ width: 120 }}
                      />
                    )}
                  </TableCell>
                )}

                {/* ICV Recovery % (calculated by server) */}
                {method.requireIcv && (
                  <TableCell sx={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>
                    {el.icvRecoveryPercent != null ? `${Number(el.icvRecoveryPercent).toFixed(1)}%` : "—"}
                  </TableCell>
                )}

                {/* Status chip */}
                <TableCell>
                  {el.passed ? (
                    <Chip label="Pass" color="success" size="small" sx={{ fontWeight: 600, minWidth: 60 }} />
                  ) : hasAnyInput ? (
                    <Chip label="Fail" color="error" size="small" sx={{ fontWeight: 600, minWidth: 60 }} />
                  ) : (
                    <Chip label="Pending" size="small" sx={{ fontWeight: 600, minWidth: 60 }} />
                  )}
                </TableCell>

                {/* Failure Reasons */}
                <TableCell>
                  {el.failureReasons ? (
                    <Tooltip title={el.failureReasons}>
                      <Typography
                        variant="caption"
                        sx={{
                          color: "error.main",
                          fontWeight: 500,
                          display: "-webkit-box",
                          WebkitLineClamp: 2,
                          WebkitBoxOrient: "vertical",
                          overflow: "hidden"
                        }}
                      >
                        {el.failureReasons}
                      </Typography>
                    </Tooltip>
                  ) : (
                    <Typography variant="caption" sx={{ color: "text.disabled" }}>
                      —
                    </Typography>
                  )}
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
