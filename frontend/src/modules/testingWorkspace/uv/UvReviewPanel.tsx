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
import { Trans } from "react-i18next";
import { HplcStatusBadge } from "../../hplcWorkspace/components/HplcStatusBadge";
import { tableHeadSx } from "../../../theme";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
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
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();
  const calc = parseUvCalc(parameter.calculationJson);

  if (!calc) {
    return null;
  }

  const modeLabel = t(`uvReview.modes.${calc.mode}`, { defaultValue: UV_MODE_LABELS[calc.mode] ?? calc.mode ?? "—" });
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
          {t("uvReview.title", { name: parameter.parameterName })}
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
            {t("uvReview.mode")}
          </Typography>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            {modeLabel}
          </Typography>
        </Box>

        {calc.wavelengthNm != null && (
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("uvReview.wavelength")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              <Ltr>{calc.wavelengthNm} nm</Ltr>
            </Typography>
          </Box>
        )}

        {calc.totalVolumeMl != null && (
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("uvReview.volume")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              <Ltr>{calc.totalVolumeMl} mL</Ltr>
            </Typography>
          </Box>
        )}

        {calc.specificAbsorbance != null && (
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("uvReview.a1Percent")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              <Ltr>{calc.specificAbsorbance}</Ltr>
            </Typography>
          </Box>
        )}

        {!isLimitMode && calc.asIsValue != null && (
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("uvReview.asIsValue")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              <Ltr>{typeof calc.asIsValue === "number" ? calc.asIsValue.toFixed(2) : String(calc.asIsValue)}</Ltr>
            </Typography>
          </Box>
        )}

        {calc.moisturePercent != null && (
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("uvReview.moisture")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              <Ltr>{calc.moisturePercent} %</Ltr>
            </Typography>
          </Box>
        )}

        {isStandardMode && calc.standard && (
          <>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                {t("uvReview.standardLot")}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                <Ltr>{calc.standard.code || "—"}</Ltr>
              </Typography>
            </Box>
            {calc.standard.purityPercent != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  {t("uvReview.stdPurity")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  <Ltr>{calc.standard.purityPercent} %</Ltr>
                </Typography>
              </Box>
            )}
            {calc.standard.moisturePercent != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  {t("uvReview.stdMoisture")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  <Ltr>{calc.standard.moisturePercent} %</Ltr>
                </Typography>
              </Box>
            )}
            {calc.standard.volumeMl != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  {t("uvReview.stdVolume")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  <Ltr>{calc.standard.volumeMl} mL</Ltr>
                </Typography>
              </Box>
            )}
            {calc.standard.weightG != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  {t("uvReview.stdWeight")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  <Ltr>{calc.standard.weightG} g</Ltr>
                </Typography>
              </Box>
            )}
            {calc.standard.absorbance != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  {t("uvReview.stdAbsorbance")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  <Ltr>{calc.standard.absorbance}</Ltr>
                </Typography>
              </Box>
            )}
          </>
        )}

        {isLimitMode && (
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("uvReview.referenceAbsorbance")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              <Ltr>
                {calc.referenceAbsorbance != null
                  ? (typeof calc.referenceAbsorbance === "number"
                      ? calc.referenceAbsorbance.toFixed(6).replace(/(\.\d{3}\d*?)0+$/, "$1")
                      : String(calc.referenceAbsorbance))
                  : "—"}
              </Ltr>
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
                <TableCell sx={{ fontSize: 12 }}>{t("uvReview.colReplicate")}</TableCell>
                {hasWeights && (
                  <TableCell align="right" sx={{ fontSize: 12 }}>
                    {t("uvReview.colSampleWeight")}
                  </TableCell>
                )}
                <TableCell align="right" sx={{ fontSize: 12 }}>
                  {t("uvReview.colAbsorbance")}
                </TableCell>
                {!isLimitMode && (
                  <TableCell align="right" sx={{ fontSize: 12 }}>
                    {calc.moisturePercent != null ? t("uvReview.colResultAsIs") : t("uvReview.colResult")}
                  </TableCell>
                )}
              </TableRow>
            </TableHead>
            <TableBody>
              {calc.replicates.map((r) => (
                <TableRow key={r.replicateNo} hover>
                  <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                    {t("uvReview.repLabel", { index: r.replicateNo })}
                  </TableCell>
                  {hasWeights && (
                    <TableCell align="right" sx={{ fontSize: 12 }}>
                      <Ltr>{r.weightG != null ? r.weightG : "—"}</Ltr>
                    </TableCell>
                  )}
                  <TableCell align="right" sx={{ fontSize: 12 }}>
                    <Ltr>{typeof r.absorbance === "number" ? r.absorbance.toFixed(3) : String(r.absorbance ?? "—")}</Ltr>
                  </TableCell>
                  {!isLimitMode && (
                    <TableCell align="right" sx={{ fontSize: 12, fontWeight: 600 }}>
                      <Ltr>
                        {typeof r.value === "number"
                          ? r.value.toFixed(2)
                          : String(r.value ?? "—")}
                      </Ltr>
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
            {t("uvReview.reportedResult")} <strong><Ltr>{parameter.reportedDisplay || "—"}</Ltr></strong>
          </Typography>
          {parameter.specLimit && (
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              <Trans
                i18nKey="uvReview.specParens"
                ns="testingWorkspace"
                values={{ spec: parameter.specLimit }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
          )}
        </Box>
      </Box>
    </Box>
  );
}
