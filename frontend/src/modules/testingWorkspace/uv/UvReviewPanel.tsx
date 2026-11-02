import {
  Box,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
  useTheme
} from "@mui/material";
import { HplcStatusBadge } from "../../hplcWorkspace/components/HplcStatusBadge";
import { tableHeadSx } from "../../../theme";
import type { ParameterResultDetail } from "../types/sampleSummaryTypes";

export interface UvReplicateData {
  replicateNo: number;
  weightG?: number | null;
  absorbance: number;
  value: number;
}

export interface UvStandardData {
  lotId: number;
  code: string;
  purityPercent?: number | null;
  moisturePercent?: number | null;
  weightG?: number | null;
  absorbance?: number | null;
  volumeMl?: number | null;
}

export interface UvCalculationData {
  engineVersion: string;
  quantity: "Uv";
  mode: "StandardComparison" | "SpecificAbsorbanceAssay" | "SpecificAbsorbanceValue" | "AbsorbanceLimit";
  wavelengthNm: number;
  totalVolumeMl?: number | null;
  specificAbsorbance?: number | null;
  standard?: UvStandardData | null;
  referenceAbsorbance?: number | null;
  resultBasis?: string | null;
  asIsValue: number;
  moisturePercent?: number | null;
  replicates: UvReplicateData[];
  reportedValue: number;
}

export function isUvCalculation(json: string | null | undefined): boolean {
  if (!json) return false;
  try {
    const parsed = JSON.parse(json);
    return typeof parsed?.quantity === "string" && parsed.quantity === "Uv";
  } catch {
    return false;
  }
}

export function parseUvCalc(json: string | null | undefined): UvCalculationData | null {
  if (!json) return null;
  try {
    const parsed = JSON.parse(json);
    if (!parsed || typeof parsed !== "object" || parsed.quantity !== "Uv") return null;
    return parsed as UvCalculationData;
  } catch {
    return null;
  }
}

export interface UvReviewPanelProps {
  parameter: ParameterResultDetail;
  testOrderId: number;
}

export const UV_MODE_LABELS: Record<string, string> = {
  SpecificAbsorbanceAssay: "Specific Absorbance Assay",
  SpecificAbsorbanceValue: "Specific Absorbance Value",
  StandardComparison: "Standard Comparison",
  AbsorbanceLimit: "Absorbance Limit"
};

export function UvReviewPanel({ parameter }: UvReviewPanelProps) {
  const theme = useTheme();
  const calc = parseUvCalc(parameter.calculationJson);

  if (!calc) {
    return null;
  }

  const modeLabel = UV_MODE_LABELS[calc.mode] ?? calc.mode ?? "—";
  const isLimitMode = calc.mode === "AbsorbanceLimit";
  const isStandardMode = calc.mode === "StandardComparison";
  const hasWeights = !isLimitMode && (calc.replicates ?? []).some((r) => r.weightG != null);

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
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1, mb: 1.5 }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
          UV Review · {parameter.parameterName}
        </Typography>
        <HplcStatusBadge status={parameter.comparisonStatus} />
      </Box>

      {/* Summary parameters */}
      <Stack
        direction="row"
        spacing={3}
        useFlexGap
        sx={{ flexWrap: "wrap", mb: 2, p: 1.5, bgcolor: "action.hover", borderRadius: 1 }}
      >
        <Box>
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
            Mode
          </Typography>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            {modeLabel}
          </Typography>
        </Box>

        {calc.wavelengthNm != null && (
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Wavelength
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {calc.wavelengthNm} nm
            </Typography>
          </Box>
        )}

        {calc.totalVolumeMl != null && (
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Volume
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {calc.totalVolumeMl} mL
            </Typography>
          </Box>
        )}

        {calc.specificAbsorbance != null && (
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              A1%
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {calc.specificAbsorbance}
            </Typography>
          </Box>
        )}

        {!isLimitMode && calc.asIsValue != null && (
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              As-is Value
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {typeof calc.asIsValue === "number" ? calc.asIsValue.toFixed(2) : String(calc.asIsValue)}
            </Typography>
          </Box>
        )}

        {calc.moisturePercent != null && (
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Moisture
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {calc.moisturePercent} %
            </Typography>
          </Box>
        )}

        {isStandardMode && calc.standard && (
          <>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                Standard Lot
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {calc.standard.code || "—"}
              </Typography>
            </Box>
            {calc.standard.purityPercent != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  Std Purity
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {calc.standard.purityPercent} %
                </Typography>
              </Box>
            )}
            {calc.standard.moisturePercent != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  Std Moisture
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {calc.standard.moisturePercent} %
                </Typography>
              </Box>
            )}
            {calc.standard.volumeMl != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  Std Volume
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {calc.standard.volumeMl} mL
                </Typography>
              </Box>
            )}
            {calc.standard.weightG != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  Std Weight
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {calc.standard.weightG} g
                </Typography>
              </Box>
            )}
            {calc.standard.absorbance != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  Std Absorbance
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {calc.standard.absorbance}
                </Typography>
              </Box>
            )}
          </>
        )}

        {isLimitMode && (
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Reference Absorbance
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {calc.referenceAbsorbance != null
                ? (typeof calc.referenceAbsorbance === "number"
                    ? calc.referenceAbsorbance.toFixed(6).replace(/(\.\d{3}\d*?)0+$/, "$1")
                    : String(calc.referenceAbsorbance))
                : "—"}
            </Typography>
          </Box>
        )}
      </Stack>

      {/* Replicates table */}
      {(calc.replicates ?? []).length > 0 && (
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1, mb: 1.5 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell sx={{ fontSize: 12 }}>Replicate</TableCell>
                {hasWeights && (
                  <TableCell align="right" sx={{ fontSize: 12 }}>
                    Sample Weight (g)
                  </TableCell>
                )}
                <TableCell align="right" sx={{ fontSize: 12 }}>
                  Absorbance
                </TableCell>
                {!isLimitMode && (
                  <TableCell align="right" sx={{ fontSize: 12 }}>
                    {calc.moisturePercent != null ? "Result (as is)" : "Result"}
                  </TableCell>
                )}
              </TableRow>
            </TableHead>
            <TableBody>
              {calc.replicates.map((r) => (
                <TableRow key={r.replicateNo} hover>
                  <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                    Rep {r.replicateNo}
                  </TableCell>
                  {hasWeights && (
                    <TableCell align="right" sx={{ fontSize: 12 }}>
                      {r.weightG != null ? r.weightG : "—"}
                    </TableCell>
                  )}
                  <TableCell align="right" sx={{ fontSize: 12 }}>
                    {typeof r.absorbance === "number" ? r.absorbance.toFixed(3) : String(r.absorbance ?? "—")}
                  </TableCell>
                  {!isLimitMode && (
                    <TableCell align="right" sx={{ fontSize: 12, fontWeight: 600 }}>
                      {typeof r.value === "number"
                        ? r.value.toFixed(2)
                        : String(r.value ?? "—")}
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Final reported result line */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12 }}>
            Reported Result: <strong>{parameter.reportedDisplay || "—"}</strong>
          </Typography>
          {parameter.specLimit && (
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              (Spec: {parameter.specLimit})
            </Typography>
          )}
        </Box>
      </Box>
    </Box>
  );
}
