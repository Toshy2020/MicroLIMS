import {
  Box,
  Typography,
  Paper,
  Alert,
  Stack,
  useTheme
} from "@mui/material";
import LinkIcon from "@mui/icons-material/Link";
import { StatusBadge } from "../../../components/StatusBadge";
import { HplcStatusBadge } from "../../hplcWorkspace/components/HplcStatusBadge";
import type { ParameterResultDetail } from "../types/sampleSummaryTypes";
import {
  formatIcpQuantity,
  icpFormulaText,
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
        <Alert severity="info" sx={{ fontSize: 12, py: 0.5, mb: 1 }}>
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

  const isAssay = calc.mode === "Assay";

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
        </Box>

        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          Run: <strong>{calc.runCode}</strong> · Cal: <strong>{calc.calibrationCode}</strong>
        </Typography>
      </Box>

      {/* Calibration & calculation */}
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
        <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 0.5, textTransform: "uppercase" }}>
          Calibration
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
              Declared ready (signed) at
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
        </Stack>

        <Box sx={{ mt: 1, pt: 1, borderTop: `1px dashed ${theme.palette.divider}` }}>
          <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 0.5, textTransform: "uppercase" }}>
            Calculation
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ fontSize: 12, flexWrap: "wrap", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                Instrument result
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>
                {calc.resultPpm} ppm
              </Typography>
            </Box>
            {isAssay && calc.targetPpm != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  Target
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>
                  {calc.targetPpm} ppm
                </Typography>
              </Box>
            )}
            {isAssay && calc.theoreticalAmount != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  {calc.sampleForm === "Liquid" ? "Th.Vol" : "Th.Wt"}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>
                  {calc.theoreticalAmount} {calc.sampleForm === "Liquid" ? "mL" : "g"}
                </Typography>
              </Box>
            )}
            {isAssay && calc.sampleForm === "Liquid" && calc.specificGravity != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  Sp.gr
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>
                  {calc.specificGravity}
                </Typography>
              </Box>
            )}
            {isAssay && calc.actualWeightG != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  Act.Wt
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>
                  {calc.actualWeightG} g
                </Typography>
              </Box>
            )}
          </Stack>
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block", fontStyle: "italic" }}>
            Formula: {icpFormulaText(calc)}
          </Typography>
        </Box>
      </Paper>

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
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            Raw evidence stored under ICP Run <strong>{calc.runCode}</strong>
          </Typography>
        </Box>
      </Box>
    </Box>
  );
}
