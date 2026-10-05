import {
  Box,
  Typography,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Alert,
  Stack,
  Chip,
  useTheme
} from "@mui/material";
import LinkIcon from "@mui/icons-material/Link";
import { StatusBadge } from "../../../components/StatusBadge";
import { HplcStatusBadge } from "../../hplcWorkspace/components/HplcStatusBadge";
import { tableHeadSx } from "../../../theme";
import type { ParameterResultDetail } from "../types/sampleSummaryTypes";
import {
  formatIcpQuantity,
  type IcpCalculationData
} from "./icpReviewTypes";
import { IcpReportsSection } from "./IcpReportsSection";

export interface IcpReviewPanelProps {
  parameter: ParameterResultDetail;
  testOrderId: number;
}

function parseIcpCalc(json: string | null | undefined): IcpCalculationData | null {
  if (!json) return null;
  try {
    const parsed = JSON.parse(json);
    if (!parsed || typeof parsed !== "object") return null;
    if (typeof parsed.quantity !== "string") return null;
    return parsed as IcpCalculationData;
  } catch {
    return null;
  }
}

function formatNumber(val: number | null | undefined, dp: number): string {
  if (val === null || val === undefined) return "—";
  if (typeof val !== "number") return String(val);
  return val.toLocaleString(undefined, {
    minimumFractionDigits: 0,
    maximumFractionDigits: dp
  });
}

export function IcpReviewPanel({ parameter, testOrderId }: IcpReviewPanelProps) {
  const theme = useTheme();
  const calc = parseIcpCalc(parameter.calculationJson);

  if (!calc) {
    return (
      <Box
        sx={{
          mt: 1.5,
          p: 2,
          borderRadius: 2,
          border: `1px dashed ${theme.palette.divider}`,
          backgroundColor: theme.palette.background.paper
        }}
      >
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1, mb: 1 }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
            ICP Review · {parameter.parameterName}
          </Typography>
          <HplcStatusBadge status={parameter.comparisonStatus} />
        </Box>
        <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
          Recorded via ICP Workspace. Parameter result: <strong>{parameter.reportedDisplay}</strong>
          {parameter.comparisonStatus ? ` (${parameter.comparisonStatus})` : ""}.
        </Typography>
        <Alert severity="info" sx={{ fontSize: 11, py: 0.5, mb: 1 }}>
          Detailed calculation payload was not found or is in an invalid format.
        </Alert>
        <IcpReportsSection testOrderId={testOrderId} />
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1, mt: 1 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <Typography sx={{ fontSize: 12 }}>
              Reported Result: <strong>{parameter.reportedDisplay}</strong>
            </Typography>
            {parameter.specLimit && (
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                (Spec: {parameter.specLimit})
              </Typography>
            )}
            <HplcStatusBadge status={parameter.comparisonStatus} />
          </Box>
        </Box>
      </Box>
    );
  }

  const amountUnitSymbol = calc.amountUnit === "Milliliter" ? "mL" : "g";
  const contentUnitSymbol = calc.amountUnit === "Milliliter" ? "µg/mL" : "µg/g";

  return (
    <Box
      sx={{
        mt: 1.5,
        p: 2,
        borderRadius: 2,
        border: `1px solid ${theme.palette.divider}`,
        backgroundColor: theme.palette.background.paper
      }}
    >
      {/* Title & Run Header */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1, mb: 1.5 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
            ICP Review · {calc.element || parameter.parameterName}
          </Typography>
          <StatusBadge status="Draft" label={formatIcpQuantity(calc.quantity)} />
          <StatusBadge status="Prepared" label={`Unit: ${amountUnitSymbol}`} />
        </Box>

        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          Run: <strong>{calc.runCode}</strong> · Cal: <strong>{calc.calibrationCode}</strong>
        </Typography>
      </Box>

      {/* Snapshot Reference & Calibration Summary */}
      <Paper
        elevation={0}
        sx={{
          p: 1.5,
          mb: 1.5,
          borderRadius: 1,
          backgroundColor: theme.palette.action.hover,
          border: `1px solid ${theme.palette.divider}`
        }}
      >
        <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", mb: 0.5, textTransform: "uppercase" }}>
          Method Snapshot & Standard Calibration
        </Typography>
        <Stack useFlexGap direction="row" spacing={3} sx={{ fontSize: 12, flexWrap: "wrap", mb: 1 }}>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Calibration Code
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {calc.calibrationCode}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Calibration Confirmed At
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {calc.calibrationConfirmedAt ? new Date(calc.calibrationConfirmedAt).toLocaleString() : "—"}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Correlation (r)
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600, fontFamily: "monospace" }}>
              {typeof calc.correlationR === "number" ? calc.correlationR.toFixed(6) : (calc.correlationR ?? "—")}
            </Typography>
          </Box>
          {calc.unitAmount != null && (
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                Unit Amount
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {calc.unitAmount} {amountUnitSymbol}
              </Typography>
            </Box>
          )}
          {calc.labelClaim != null && (
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                Label Claim
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {calc.labelClaim} {calc.labelClaimUnit ?? ""}
              </Typography>
            </Box>
          )}
          {calc.conversionFactor != null && calc.conversionFactor !== 1 && (
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                Conversion Factor
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {calc.conversionFactor}
              </Typography>
            </Box>
          )}
        </Stack>

        {/* CCV Readings */}
        {calc.ccv && calc.ccv.length > 0 && (
          <Box sx={{ mt: 1, pt: 1, borderTop: `1px dashed ${theme.palette.divider}` }}>
            <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", mb: 0.5 }}>
              Continuing Calibration Verification (CCV)
            </Typography>
            <Stack direction="row" spacing={1.5} sx={{ flexWrap: "wrap", gap: 1 }}>
              {calc.ccv.map((c, idx) => (
                <Box
                  key={idx}
                  sx={{
                    display: "flex",
                    alignItems: "center",
                    gap: 1,
                    px: 1,
                    py: 0.5,
                    borderRadius: 1,
                    border: `1px solid ${theme.palette.divider}`,
                    backgroundColor: theme.palette.background.paper,
                    fontSize: 11
                  }}
                >
                  <span>Measured: <strong>{c.measuredMgPerL} mg/L</strong></span>
                  <span>
                    Recovery: <strong>{typeof c.recoveryPercent === "number" ? `${c.recoveryPercent.toFixed(1)}%` : `${c.recoveryPercent}%`}</strong>
                  </span>
                  <Chip
                    label={c.passed ? "Pass" : "Fail"}
                    size="small"
                    color={c.passed ? "success" : "error"}
                    sx={{ height: 18, fontSize: 10, fontWeight: 700 }}
                  />
                  {c.enteredAt && (
                    <span style={{ color: theme.palette.text.secondary }}>
                      {new Date(c.enteredAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                    </span>
                  )}
                </Box>
              ))}
            </Stack>
          </Box>
        )}

        {/* Formula display */}
        <Box sx={{ mt: 1, pt: 0.5, borderTop: `1px dashed ${theme.palette.divider}` }}>
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block", fontStyle: "italic" }}>
            Formula: content = C × V × DF / W ; mg/unit = content × unit amount / 1000 × factor ; % LC = mg/unit / claim × 100
          </Typography>
        </Box>
      </Paper>

      {/* Replicates Table */}
      {calc.replicates && calc.replicates.length > 0 && (
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1, mb: 1 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell sx={{ fontSize: 11 }}>Replicate</TableCell>
                <TableCell align="right" sx={{ fontSize: 11 }}>Sample Amount ({amountUnitSymbol})</TableCell>
                <TableCell align="right" sx={{ fontSize: 11 }}>Volume (mL)</TableCell>
                <TableCell align="right" sx={{ fontSize: 11 }}>DF</TableCell>
                <TableCell align="right" sx={{ fontSize: 11 }}>Solution (mg/L)</TableCell>
                <TableCell align="right" sx={{ fontSize: 11 }}>Content ({contentUnitSymbol})</TableCell>
                <TableCell align="center" sx={{ fontSize: 11 }}>Flag</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {calc.replicates.map((r) => (
                <TableRow key={r.replicateNo} hover>
                  <TableCell sx={{ fontWeight: 600, fontSize: 12 }}>#{r.replicateNo}</TableCell>
                  <TableCell align="right" sx={{ fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                    {r.sampleAmount}
                  </TableCell>
                  <TableCell align="right" sx={{ fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                    {r.volumeMl}
                  </TableCell>
                  <TableCell align="right" sx={{ fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                    {r.dilutionFactor}
                  </TableCell>
                  <TableCell align="right" sx={{ fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                    {r.solutionMgPerL}
                  </TableCell>
                  <TableCell align="right" sx={{ fontWeight: 700, fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                    {formatNumber(r.contentPerAmount, 4)}
                  </TableCell>
                  <TableCell align="center" sx={{ fontSize: 11 }}>
                    {r.belowLoq ? (
                      <Chip
                        label="<LOQ"
                        size="small"
                        color="warning"
                        variant="outlined"
                        sx={{ fontSize: 10, height: 18, fontWeight: 700 }}
                      />
                    ) : (
                      "—"
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Mean Content Summary */}
      <Box sx={{ display: "flex", justifyContent: "flex-end", alignItems: "center", mb: 1.5, px: 1 }}>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          Mean content:{" "}
          <strong style={{ color: theme.palette.text.primary }}>
            {formatNumber(calc.meanContentPerAmount, 4)} {contentUnitSymbol}
          </strong>
        </Typography>
      </Box>

      {/* Evidence Reports Section */}
      <IcpReportsSection testOrderId={testOrderId} />

      {/* Final Outcome & GxP Traceability */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1, mt: 1 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12 }}>
            Reported Result: <strong>{parameter.reportedDisplay || calc.display || calc.reportedValue}</strong>
          </Typography>
          {parameter.specLimit && (
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              (Spec: {parameter.specLimit})
            </Typography>
          )}
          <HplcStatusBadge status={parameter.comparisonStatus} />
        </Box>

        <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
          <LinkIcon fontSize="small" sx={{ fontSize: 14, color: "text.secondary" }} />
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
            Raw evidence stored under ICP Run <strong>{calc.runCode}</strong>
          </Typography>
        </Box>
      </Box>
    </Box>
  );
}
