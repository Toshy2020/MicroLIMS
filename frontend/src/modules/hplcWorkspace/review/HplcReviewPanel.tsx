import { Trans } from "react-i18next";
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
  useTheme
} from "@mui/material";
import LinkIcon from "@mui/icons-material/Link";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { useTranslation } from "../../../i18n";
import { tableHeadSx } from "../../../theme";
import type { ParameterResultDetail } from "../../testingWorkspace/types/sampleSummaryTypes";
import {
  ResidualSolventCalculationView,
  type ResidualSolventCalculationData
} from "../components/ResidualSolventCalculationView";
import { HplcReportsSection } from "./HplcReportsSection";
import {
  RelatedSubstancesReview,
  isRelatedSubstanceCalculation,
  type RelatedSubstanceCalculationData
} from "./RelatedSubstancesReview";

export interface HplcMethodAssayReplicateData {
  replicateNo: number;
  actualWeightMg: number;
  response: number;
  assayPercent: number;
  assayPercentDisplay?: string;
  internalStandardResponse?: number | null;
  ratio?: number | null;
}

export interface HplcMethodAssayCalculationData {
  hplcRunId?: number;
  hplcRunSampleId?: number;
  analyte: string;
  hplcMethodAnalyteId: number;
  quantity: string;
  basis: string;
  replicateNo?: number | null;
  thWtStdMg: number;
  thWtTestMg: number;
  standardWeightMg: number;
  standardPurityPercent: number;
  standardMoisturePercent: number;
  standardMeanResponse?: number;
  standardMeanRatio?: number | null;
  internalStandardName?: string | null;
  asIsValue?: number | null;
  moisturePercent?: number | null;
  labelClaim?: number | null;
  replicates: HplcMethodAssayReplicateData[];
  reportedValue: number;
  runCode: string;
  sstCode: string;
  hplcMethodId: number;
}

// Every related-substances row of a result stores the same peaks: one panel covers them all.
export function hplcPanelResults(results: ParameterResultDetail[]): ParameterResultDetail[] {
  const firstRs = results.find((p) => isRelatedSubstanceCalculation(p.calculationJson));
  return results.filter((p) => p === firstRs || !isRelatedSubstanceCalculation(p.calculationJson));
}

export interface HplcReviewPanelProps {
  parameter: ParameterResultDetail;
  testOrderId: number;
}

function parseHplcCalc(json: string | null): HplcMethodAssayCalculationData | ResidualSolventCalculationData | RelatedSubstanceCalculationData | null {
  if (!json) return null;
  try {
    return JSON.parse(json);
  } catch {
    return null;
  }
}

function formatReplicateAssay(r: HplcMethodAssayReplicateData): string {
  if (r.assayPercentDisplay) {
    return r.assayPercentDisplay;
  }
  if (typeof r.assayPercent === "number") {
    return `${r.assayPercent.toLocaleString(undefined, { maximumFractionDigits: 2 })} %`;
  }
  return "—";
}

export function HplcReviewPanel({ parameter, testOrderId }: HplcReviewPanelProps) {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();
  const calc = parseHplcCalc(parameter.calculationJson);

  if (!calc) {
    return (
      <Box sx={{ mt: 1.5, p: 1.5, border: "1px dashed", borderColor: "divider", borderRadius: 1.5 }}>
        <Typography sx={{ fontSize: 12, fontWeight: 700, mb: 0.5 }}>
          <Trans
            i18nKey="review.assay.fallbackTitle"
            ns="hplcWorkspace"
            values={{ parameter: parameter.parameterName }}
            components={{ ltr: <Ltr>{null}</Ltr> }}
          />
        </Typography>
        <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
          <Trans
            i18nKey="review.assay.fallbackSummary"
            ns="hplcWorkspace"
            values={{ display: parameter.reportedDisplay ?? "", status: parameter.comparisonStatus }}
            components={{ ltr: <Ltr>{null}</Ltr>, strong: <strong /> }}
          />
        </Typography>
        <Alert severity="info" sx={{ fontSize: 12, py: 0.5, mb: 1 }}>
          {t("review.assay.fallbackAlert")}
        </Alert>
        <HplcReportsSection testOrderId={testOrderId} />
      </Box>
    );
  }

  if (calc.quantity === "ResidualSolventPpm") {
    return (
      <ResidualSolventCalculationView
        calc={calc as ResidualSolventCalculationData}
        reportedDisplay={parameter.reportedDisplay}
        specLimit={parameter.specLimit}
        status={parameter.comparisonStatus}
        reportsSection={<HplcReportsSection testOrderId={testOrderId} />}
      />
    );
  }

  if (calc.quantity === "RelatedSubstance") {
    return <RelatedSubstancesReview calc={calc as RelatedSubstanceCalculationData} testOrderId={testOrderId} />;
  }

  const assayCalc = calc as HplcMethodAssayCalculationData;
  const hasInternalStandard = Boolean(assayCalc.internalStandardName);

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
            <Trans
              i18nKey="review.assay.title"
              ns="hplcWorkspace"
              values={{ analyte: assayCalc.analyte }}
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
          <StatusBadge status="Prepared" label={t("review.assay.basis", { basis: assayCalc.basis })} />
          <StatusBadge status="Draft" label={assayCalc.quantity === "AmountPerUnit" ? t("review.assay.amountPerUnit") : t("review.assay.assayPercent")} />
        </Box>

        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          <Trans
            i18nKey="review.assay.runSstMeta"
            ns="hplcWorkspace"
            values={{ runCode: assayCalc.runCode, sstCode: assayCalc.sstCode }}
            components={{ ltr: <Ltr>{null}</Ltr>, strong: <strong /> }}
          />
        </Typography>
      </Box>

      {/* Snapshot Reference and Theoretical Parameters */}
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
          {t("review.assay.methodSnapshotTitle")}
        </Typography>
        <Stack useFlexGap direction="row" spacing={3} sx={{ fontSize: 12, flexWrap: "wrap" }}>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("review.assay.theoreticalWeights")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              <Trans
                i18nKey="review.assay.theoreticalWeightsValue"
                ns="hplcWorkspace"
                values={{ std: assayCalc.thWtStdMg, test: assayCalc.thWtTestMg }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {t("review.assay.standardReference")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              <Trans
                i18nKey="review.assay.standardReferenceValue"
                ns="hplcWorkspace"
                values={{ wt: assayCalc.standardWeightMg, purity: assayCalc.standardPurityPercent, mc: assayCalc.standardMoisturePercent }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              {hasInternalStandard ? t("review.assay.standardMeanRatio") : t("review.assay.standardMeanResponse")}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600, fontFamily: "monospace" }}>
              <Ltr>{hasInternalStandard ? assayCalc.standardMeanRatio : assayCalc.standardMeanResponse}</Ltr>
            </Typography>
          </Box>
          {hasInternalStandard && (
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                {t("review.assay.internalStandard")}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                <Ltr>{assayCalc.internalStandardName}</Ltr>
              </Typography>
            </Box>
          )}
          {assayCalc.moisturePercent != null && (
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                {t("review.assay.contentBasis")}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                <Trans
                  i18nKey="review.assay.contentBasisValue"
                  ns="hplcWorkspace"
                  values={{ asIs: Number(assayCalc.asIsValue).toFixed(2), moisture: assayCalc.moisturePercent }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
            </Box>
          )}
          {assayCalc.labelClaim != null && (
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                {t("review.assay.labelClaim")}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                <Ltr>{assayCalc.labelClaim}</Ltr>
              </Typography>
            </Box>
          )}
        </Stack>
      </Paper>

      {/* Replicate Injections & Calculations Table */}
      {assayCalc.replicates && assayCalc.replicates.length > 0 && (
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1, mb: 1.5 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell sx={{ fontSize: 12 }}>{t("review.assay.columns.replicate")}</TableCell>
                <TableCell align="right" sx={{ fontSize: 12 }}>{t("review.assay.columns.actualWeight")}</TableCell>
                <TableCell align="right" sx={{ fontSize: 12 }}>{t("review.assay.columns.peakResponse")}</TableCell>
                {hasInternalStandard && <TableCell align="right" sx={{ fontSize: 12 }}>{t("review.assay.columns.isResponse")}</TableCell>}
                {hasInternalStandard && <TableCell align="right" sx={{ fontSize: 12 }}>{t("review.assay.columns.ratio")}</TableCell>}
                <TableCell align="right" sx={{ fontSize: 12 }}>{t("review.assay.columns.calculatedAssay")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {assayCalc.replicates.map((r) => (
                <TableRow key={r.replicateNo} hover>
                  <TableCell sx={{ fontWeight: 600, fontSize: 12 }}><Ltr>#{r.replicateNo}</Ltr></TableCell>
                  <TableCell align="right" sx={{ fontSize: 12, fontVariantNumeric: "tabular-nums" }}><Ltr>{r.actualWeightMg}</Ltr></TableCell>
                  <TableCell align="right" sx={{ fontSize: 12, fontVariantNumeric: "tabular-nums" }}><Ltr>{r.response}</Ltr></TableCell>
                  {hasInternalStandard && (
                    <TableCell align="right" sx={{ fontSize: 12, fontVariantNumeric: "tabular-nums" }}><Ltr>{r.internalStandardResponse}</Ltr></TableCell>
                  )}
                  {hasInternalStandard && (
                    <TableCell align="right" sx={{ fontSize: 12, fontVariantNumeric: "tabular-nums" }}><Ltr>{r.ratio}</Ltr></TableCell>
                  )}
                  <TableCell align="right" sx={{ fontWeight: 700, fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                    <Ltr>{formatReplicateAssay(r)}</Ltr>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Reports Section */}
      <HplcReportsSection testOrderId={testOrderId} />

      {/* Final Outcome & GxP Note */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12 }}>
            {t("review.assay.reportedResult")} <strong><Ltr>{parameter.reportedDisplay}</Ltr></strong>
          </Typography>
          {parameter.specLimit && (
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              <Trans
                i18nKey="review.assay.spec"
                ns="hplcWorkspace"
                values={{ specLimit: parameter.specLimit }}
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
              i18nKey="review.assay.evidenceLink"
              ns="hplcWorkspace"
              values={{ runCode: assayCalc.runCode }}
              components={{ ltr: <Ltr>{null}</Ltr>, strong: <strong /> }}
            />
          </Typography>
        </Box>
      </Box>
    </Box>
  );
}
