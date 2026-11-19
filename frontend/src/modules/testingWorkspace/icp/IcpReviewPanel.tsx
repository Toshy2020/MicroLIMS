import {
  Box,
  Typography,
  Paper,
  Alert,
  Stack,
  useTheme
} from "@mui/material";
import LinkIcon from "@mui/icons-material/Link";
import { Trans } from "react-i18next";
import { StatusBadge } from "../../../components/StatusBadge";
import { HplcStatusBadge } from "../../hplcWorkspace/components/HplcStatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import type { ParameterResultDetail } from "../types/sampleSummaryTypes";
import {
  formatIcpQuantity,
  icpFormulaText,
  type IcpCalculationData
} from "./icpReviewTypes";
import { IcpReportsSection } from "./IcpReportsSection";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

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
  const { t } = useTranslation("testingWorkspace");
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
            {t("icpReview.title", { name: parameter.parameterName })}
          </Typography>
          <HplcStatusBadge status={parameter.comparisonStatus} />
        </Box>
        <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
          <Trans
            i18nKey="icpReview.recordedFallback"
            ns="testingWorkspace"
            values={{
              display: parameter.reportedDisplay,
              statusPart: parameter.comparisonStatus ? ` (${parameter.comparisonStatus})` : ""
            }}
            components={{ strong: <strong />, ltr: <Ltr>{null}</Ltr> }}
          />
        </Typography>
        <Alert severity="info" sx={{ fontSize: 12, py: 0.5, mb: 1 }}>
          {t("icpReview.payloadMissingAlert")}
        </Alert>
        <IcpReportsSection testOrderId={testOrderId} />
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1, mt: 1 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <Typography sx={{ fontSize: 12 }}>
              {t("icpReview.reportedResult")} <strong><Ltr>{parameter.reportedDisplay}</Ltr></strong>
            </Typography>
            {parameter.specLimit && (
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                <Trans
                  i18nKey="icpReview.specParens"
                  ns="testingWorkspace"
                  values={{ spec: parameter.specLimit }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
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
            {t("icpReview.title", { name: calc.element || parameter.parameterName })}
          </Typography>
          <StatusBadge status="Draft" label={formatIcpQuantity(calc.quantity)} />
        </Box>

        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          <Trans
            i18nKey="icpReview.runCalHeader"
            ns="testingWorkspace"
            values={{ runCode: calc.runCode, calCode: calc.calibrationCode }}
            components={{ strong: <strong />, ltr: <Ltr>{null}</Ltr> }}
          />
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
          {t("icpReview.calibrationSection")}
        </Typography>
        <Stack useFlexGap direction="row" spacing={3} sx={{ fontSize: 12, flexWrap: "wrap", mb: 1 }}>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("icpReview.calibrationCode")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              <Ltr>{calc.calibrationCode}</Ltr>
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("icpReview.declaredReadyAt")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {calc.calibrationConfirmedAt ? <Ltr>{formatDateTimeWithTimeZone(calc.calibrationConfirmedAt)}</Ltr> : "—"}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("icpReview.correlationR")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600, fontFamily: "monospace" }}>
              <Ltr>{typeof calc.correlationR === "number" ? calc.correlationR.toFixed(6) : (calc.correlationR ?? "—")}</Ltr>
            </Typography>
          </Box>
        </Stack>

        <Box sx={{ mt: 1, pt: 1, borderTop: `1px dashed ${theme.palette.divider}` }}>
          <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 0.5, textTransform: "uppercase" }}>
            {t("icpReview.calculationSection")}
          </Typography>
          <Stack useFlexGap direction="row" spacing={3} sx={{ fontSize: 12, flexWrap: "wrap", mb: 1 }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                {t("icpReview.instrumentResult")}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>
                <Ltr>{calc.resultPpm} ppm</Ltr>
              </Typography>
            </Box>
            {isAssay && calc.targetPpm != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  {t("icpReview.target")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>
                  <Ltr>{calc.targetPpm} ppm</Ltr>
                </Typography>
              </Box>
            )}
            {isAssay && calc.theoreticalAmount != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  {calc.sampleForm === "Liquid" ? t("icpReview.thVol") : t("icpReview.thWt")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>
                  <Ltr>{calc.theoreticalAmount} {calc.sampleForm === "Liquid" ? "mL" : "g"}</Ltr>
                </Typography>
              </Box>
            )}
            {isAssay && calc.sampleForm === "Liquid" && calc.specificGravity != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  {t("icpReview.spGr")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>
                  <Ltr>{calc.specificGravity}</Ltr>
                </Typography>
              </Box>
            )}
            {isAssay && calc.actualWeightG != null && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  {t("icpReview.actWt")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>
                  <Ltr>{calc.actualWeightG} g</Ltr>
                </Typography>
              </Box>
            )}
          </Stack>
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block", fontStyle: "italic" }}>
            <Trans
              i18nKey="icpReview.formula"
              ns="testingWorkspace"
              values={{ formula: icpFormulaText(calc) }}
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
        </Box>
      </Paper>

      {/* Evidence Reports Section */}
      <IcpReportsSection testOrderId={testOrderId} />

      {/* Final Outcome & GxP Traceability */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1, mt: 1 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12 }}>
            {t("icpReview.reportedResult")} <strong><Ltr>{parameter.reportedDisplay || calc.display || calc.reportedValue}</Ltr></strong>
          </Typography>
          {parameter.specLimit && (
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              <Trans
                i18nKey="icpReview.specParens"
                ns="testingWorkspace"
                values={{ spec: parameter.specLimit }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
          )}
          <HplcStatusBadge status={parameter.comparisonStatus} />
        </Box>

        <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
          <LinkIcon fontSize="small" sx={{ fontSize: 14, color: "text.secondary" }} />
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            <Trans
              i18nKey="icpReview.rawEvidenceRun"
              ns="testingWorkspace"
              values={{ runCode: calc.runCode }}
              components={{ strong: <strong />, ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
        </Box>
      </Box>
    </Box>
  );
}
