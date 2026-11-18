import { Box, Typography, Table, TableHead, TableRow, TableCell, TableBody, TableContainer } from "@mui/material";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { AnalysisDetail, ParameterResultDetail, ResultReadingDetail } from "../types/sampleSummaryTypes";
import { HplcReviewPanel, hplcPanelResults } from "../../hplcWorkspace/review/HplcReviewPanel";
import { formatAnalysisConditions } from "../utils/moistureRetentionHelpers";
import { IcpReviewPanel } from "../icp/IcpReviewPanel";
import { isIcpCalculation } from "../icp/icpReviewTypes";
import { UvReviewPanel, isUvCalculation } from "../uv/UvReviewPanel";
import { humanize } from "../SampleReportPage";
import { parseTitrationSnapshot } from "../utils/titrationSnapshot";
import { decimalsOf, withoutUnit } from "../utils/resultDisplay";
import { formatDate } from "./summaryFormatting";
import { SummaryField } from "./SummaryCards";

export const num = (v: number | null | undefined, digits?: number) =>
  v === null || v === undefined ? "—" : digits === undefined ? String(v) : v.toFixed(digits);

export function AnalysisResultBlock({ analysis }: { analysis: AnalysisDetail }) {
  const { t } = useTranslation("testingWorkspace");
  const cellSx = { fontSize: 12, py: 0.75 };
  const headSx = { fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" as const, py: 0.75 };

  let conditionsDisplay: string | null;
  const titration = analysis.analysisType === "Titration" ? parseTitrationSnapshot(analysis.conditionsJson) : null;
  if (titration) {
    conditionsDisplay =
      [
        titration.titrantCode ? t("sampleSummary.analysis.titrant", { code: titration.titrantCode }) : null,
        titration.factor != null
          ? titration.factorState
            ? t("sampleSummary.analysis.factorUsedState", { factor: titration.factor.toFixed(4), state: humanize(titration.factorState) })
            : t("sampleSummary.analysis.factorUsed", { factor: titration.factor.toFixed(4) })
          : null,
        titration.warnings.length > 0 ? t("sampleSummary.analysis.warnings", { warnings: titration.warnings.join("; ") }) : null,
        titration.dueTitrantAcknowledgement?.justification
          ? t("sampleSummary.analysis.dueTitrantAcknowledged", { justification: titration.dueTitrantAcknowledgement.justification })
          : null
      ].filter(Boolean).join(" · ") || null;
  } else {
    conditionsDisplay = formatAnalysisConditions(analysis.conditionsJson);
  }

  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5, bgcolor: "background.default" }}>
      <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1.5 }}>{t("sampleSummary.analysis.title")}</Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(4, 1fr)" }, gap: 1.5, mb: 1.5 }}>
        <SummaryField
          label={t("sampleSummary.analysis.instrument")}
          value={
            analysis.equipmentCode ? (
              <>
                <Ltr>{analysis.equipmentCode}</Ltr>
                {analysis.equipmentName ? ` (${analysis.equipmentName})` : ""}
              </>
            ) : (
              analysis.equipmentName ?? "—"
            )
          }
        />
        <SummaryField label={t("sampleSummary.analysis.analysisTime")} value={<Ltr>{formatDate(analysis.analysedAt)}</Ltr>} />
        {analysis.unitAmount !== null && <SummaryField label={t("sampleSummary.analysis.unitAmount")} value={<Ltr>{num(analysis.unitAmount)}</Ltr>} />}
        {conditionsDisplay && <SummaryField label={t("sampleSummary.analysis.conditions")} value={conditionsDisplay} />}
        <SummaryField
          label={t("sampleSummary.analysis.enteredByAt")}
          value={
            <>
              {analysis.enteredByName ? <Ltr>{analysis.enteredByName}</Ltr> : "—"} · <Ltr>{formatDate(analysis.enteredAt)}</Ltr>
            </>
          }
        />
      </Box>
      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={headSx}>{t("sampleSummary.analysis.parameter")}</TableCell>
              <TableCell sx={headSx}>{t("sampleSummary.analysis.reported")}</TableCell>
              <TableCell sx={headSx}>{t("sampleSummary.analysis.unit")}</TableCell>
              <TableCell sx={headSx}>{t("sampleSummary.analysis.specification")}</TableCell>
              <TableCell sx={headSx}>{t("sampleSummary.analysis.status")}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {analysis.parameterResults.map((p, idx) => {
              const flags = [p.overRange ? ">Range" : null, p.belowLoq ? "<LOQ" : null].filter(Boolean).join(", ");
              return (
                <TableRow key={idx}>
                  <TableCell sx={cellSx}>{p.parameterName}</TableCell>
                  <TableCell sx={{ ...cellSx, fontWeight: 600 }}>
                    <Ltr>
                      {withoutUnit(p.reportedDisplay, p.unit)}
                      {flags ? ` [${flags}]` : ""}
                    </Ltr>
                  </TableCell>
                  <TableCell sx={cellSx}>{p.unit ? <Ltr>{p.unit}</Ltr> : "—"}</TableCell>
                  <TableCell sx={cellSx}>
                    {p.specLimit ? <Ltr>{`${p.specLimit}${p.unit ? ` ${p.unit}` : ""}`}</Ltr> : "—"}
                  </TableCell>
                  <TableCell sx={cellSx}>
                    <StatusBadge status={p.comparisonStatus} />
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>
      {analysis.analysisType === "HplcMethodAssay" &&
        hplcPanelResults(analysis.parameterResults).map((p) => (
          <HplcReviewPanel
            key={`hplc-review-${p.id}`}
            parameter={p}
            testOrderId={analysis.testOrderId}
          />
        ))}
      {(analysis.analysisType === "IcpMethodAssay" || analysis.parameterResults.some((p) => isIcpCalculation(p.calculationJson))) &&
        analysis.parameterResults
          .filter((p) => isIcpCalculation(p.calculationJson) || analysis.analysisType === "IcpMethodAssay")
          .map((p) => (
            <IcpReviewPanel
              key={`icp-review-${p.id}`}
              parameter={p}
              testOrderId={analysis.testOrderId}
            />
          ))}
      {(analysis.analysisType === "UvSpectrophotometry" || analysis.parameterResults.some((p) => isUvCalculation(p.calculationJson))) &&
        analysis.parameterResults
          .filter((p) => isUvCalculation(p.calculationJson) || analysis.analysisType === "UvSpectrophotometry")
          .map((p) => (
            <UvReviewPanel
              key={`uv-review-${p.id}`}
              parameter={p}
              testOrderId={analysis.testOrderId}
            />
          ))}
      {analysis.analysisType !== "HplcMethodAssay" && analysis.analysisType !== "IcpMethodAssay" && analysis.analysisType !== "UvSpectrophotometry" &&
        analysis.parameterResults
          .filter((p) => p.readings.length > 0 && !isIcpCalculation(p.calculationJson) && !isUvCalculation(p.calculationJson))
          .map((p) => (
            <AnalysisReadingsTable key={p.id} parameter={p} analysisType={analysis.analysisType} />
          ))}
    </Box>
  );
}

// Raw readings behind one parameter result; only columns with a value are shown.
export function AnalysisReadingsTable({ parameter, analysisType }: { parameter: ParameterResultDetail; analysisType?: string }) {
  const { t } = useTranslation("testingWorkspace");
  const cellSx = { fontSize: 12, py: 0.5 };
  const headSx = { fontSize: 12, fontWeight: 700, color: "text.secondary", py: 0.5 };
  const r = parameter.readings;
  const isVessel = r.some((x) => x.kind === "Vessel");
  const isDisintegration = analysisType === "Disintegration";
  const isWeightVariation = analysisType === "WeightVariation";
  const isTitration = analysisType === "Titration";
  const isTablet = isWeightVariation && r.every((x) => x.value2 == null);
  type ReadingColumn = { id: string; label: string; get: (x: ResultReadingDetail) => string | null };
  const allCols: ReadingColumn[] = [
    {
      id: "stage",
      label: t("sampleSummary.analysis.col.stage"),
      get: (x) =>
        x.stage != null
          ? isVessel || isDisintegration || isWeightVariation
            ? `S${x.stage}`
            : String(x.stage)
          : null
    },
    {
      id: "time",
      label: t("sampleSummary.analysis.col.timeMin"),
      get: (x) => (!isDisintegration && !isWeightVariation && x.timePointMinutes != null ? num(x.timePointMinutes) : null)
    },
    {
      id: "val1",
      label: isVessel
        ? t("sampleSummary.analysis.col.peakArea")
        : isDisintegration
        ? t("sampleSummary.analysis.col.timeMin")
        : isWeightVariation
        ? (isTablet ? t("sampleSummary.analysis.col.weightMg") : t("sampleSummary.analysis.col.grossMg"))
        : isTitration
        ? t("sampleSummary.analysis.col.sampleWeightMg")
        : t("sampleSummary.analysis.col.value1"),
      get: (x) => (x.value1 != null ? num(x.value1) : isDisintegration && x.text ? x.text : null)
    },
    {
      id: "val2",
      label: isWeightVariation
        ? t("sampleSummary.analysis.col.shellMg")
        : isTitration
        ? t("sampleSummary.analysis.col.titrantVolumeMl")
        : t("sampleSummary.analysis.col.value2"),
      get: (x) => (!isTablet && x.value2 != null ? num(x.value2) : null)
    },
    {
      id: "val3",
      label: isWeightVariation
        ? t("sampleSummary.analysis.col.deviationPct")
        : isTitration
        ? t("sampleSummary.analysis.col.correctedVolumeMl")
        : t("sampleSummary.analysis.col.value3"),
      get: (x) => (x.value3 != null ? (isWeightVariation ? `${num(x.value3)} %` : num(x.value3)) : null)
    },
    {
      id: "text",
      label: t("sampleSummary.analysis.col.text"),
      get: (x) => (isDisintegration || isWeightVariation ? null : (x.text || null))
    },
    {
      id: "computed",
      label: isVessel
        ? t("sampleSummary.analysis.col.dissolvedPct")
        : isWeightVariation
        ? t("sampleSummary.analysis.col.netMg")
        : isTitration
        ? t("sampleSummary.analysis.col.result")
        : t("sampleSummary.analysis.col.computed"),
      get: (x) =>
        isDisintegration || isTablet
          ? null
          : x.computedValue != null
          ? x.kind === "Vessel"
            ? `${num(x.computedValue)} %`
            : num(x.computedValue, isTitration ? decimalsOf(parameter.reportedDisplay) : undefined)
          : null
    },
    {
      id: "pass",
      label: t("sampleSummary.analysis.col.pass"),
      get: (x) => (x.passed == null ? null : x.passed ? t("sampleSummary.analysis.pass") : t("sampleSummary.analysis.fail"))
    }
  ];
  const cols = allCols.filter((c) => r.some((x) => c.get(x) !== null));

  return (
    <Box sx={{ mt: 1.5 }}>
      <Typography sx={{ fontSize: 12, fontWeight: 700, mb: 0.5 }}>
        {t("sampleSummary.analysis.readingsTitle", { parameter: parameter.parameterName })}
      </Typography>
      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={headSx}>#</TableCell>
              {cols.map((c) => <TableCell key={c.id} sx={headSx}>{c.label}</TableCell>)}
            </TableRow>
          </TableHead>
          <TableBody>
            {r.map((x) => (
              <TableRow key={x.id}>
                <TableCell sx={cellSx}><Ltr>{x.index}</Ltr></TableCell>
                {cols.map((c) => {
                  const val = c.get(x);
                  return (
                    <TableCell key={c.id} sx={cellSx}>
                      {val == null ? "—" : c.id === "pass" || c.id === "text" ? val : <Ltr>{val}</Ltr>}
                    </TableCell>
                  );
                })}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
}
