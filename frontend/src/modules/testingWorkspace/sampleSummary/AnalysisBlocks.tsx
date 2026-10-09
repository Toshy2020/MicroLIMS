import { Box, Typography, Table, TableHead, TableRow, TableCell, TableBody, TableContainer } from "@mui/material";
import { StatusBadge } from "../../../components/StatusBadge";
import { AnalysisDetail, ParameterResultDetail, ResultReadingDetail } from "../types/sampleSummaryTypes";
import { HplcReviewPanel } from "../../hplcWorkspace/review/HplcReviewPanel";
import { IcpReviewPanel } from "../icp/IcpReviewPanel";
import { isIcpCalculation } from "../icp/icpReviewTypes";
import { humanize } from "../SampleReportPage";
import { parseTitrationSnapshot } from "../utils/titrationSnapshot";
import { decimalsOf, withoutUnit } from "../utils/resultDisplay";
import { formatDate } from "./summaryFormatting";
import { SummaryField } from "./SummaryCards";

export const num = (v: number | null | undefined, digits?: number) =>
  v === null || v === undefined ? "—" : digits === undefined ? String(v) : v.toFixed(digits);

export function AnalysisResultBlock({ analysis }: { analysis: AnalysisDetail }) {
  const cellSx = { fontSize: 12, py: 0.75 };
  const headSx = { fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" as const, py: 0.75 };

  let conditionsDisplay: string | null = null;
  const titration = analysis.analysisType === "Titration" ? parseTitrationSnapshot(analysis.conditionsJson) : null;
  if (titration) {
    conditionsDisplay =
      [
        titration.titrantCode ? `Titrant: ${titration.titrantCode}` : null,
        titration.factor != null ? `Factor used: ${titration.factor.toFixed(4)}${titration.factorState ? ` (${humanize(titration.factorState)})` : ""}` : null,
        titration.warnings.length > 0 ? `Warnings: ${titration.warnings.join("; ")}` : null,
        titration.dueTitrantAcknowledgement?.justification
          ? `Due titrant acknowledged: ${titration.dueTitrantAcknowledgement.justification}`
          : null
      ].filter(Boolean).join(" · ") || null;
  } else if (analysis.conditionsJson) {
    try {
      const parsed = JSON.parse(analysis.conditionsJson);
      if (typeof parsed === "object" && parsed !== null && !Array.isArray(parsed)) {
        conditionsDisplay = Object.entries(parsed).map(([k, v]) => `${k}: ${String(v)}`).join(" · ");
      } else {
        conditionsDisplay = JSON.stringify(parsed);
      }
    } catch {
      conditionsDisplay = analysis.conditionsJson;
    }
  }

  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5, bgcolor: "background.default" }}>
      <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1.5 }}>Analysis Results</Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(4, 1fr)" }, gap: 1.5, mb: 1.5 }}>
        <SummaryField label="Instrument" value={analysis.equipmentCode ? `${analysis.equipmentCode}${analysis.equipmentName ? ` (${analysis.equipmentName})` : ""}` : (analysis.equipmentName ?? "—")} />
        <SummaryField label="Analysis Time" value={formatDate(analysis.analysedAt)} />
        {analysis.unitAmount !== null && <SummaryField label="Unit Amount" value={num(analysis.unitAmount)} />}
        {conditionsDisplay && <SummaryField label="Conditions" value={conditionsDisplay} />}
        <SummaryField label="Entered By / At" value={`${analysis.enteredByName ?? "—"} · ${formatDate(analysis.enteredAt)}`} />
      </Box>
      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={headSx}>Parameter</TableCell>
              <TableCell sx={headSx}>Reported</TableCell>
              <TableCell sx={headSx}>Unit</TableCell>
              <TableCell sx={headSx}>Specification</TableCell>
              <TableCell sx={headSx}>Status</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {analysis.parameterResults.map((p, idx) => {
              const flags = [p.overRange ? ">Range" : null, p.belowLoq ? "<LOQ" : null].filter(Boolean).join(", ");
              return (
                <TableRow key={idx}>
                  <TableCell sx={cellSx}>{p.parameterName}</TableCell>
                  <TableCell sx={{ ...cellSx, fontWeight: 600 }}>
                    {withoutUnit(p.reportedDisplay, p.unit)}
                    {flags ? ` [${flags}]` : ""}
                  </TableCell>
                  <TableCell sx={cellSx}>{p.unit ?? "—"}</TableCell>
                  <TableCell sx={cellSx}>{p.specLimit ? `${p.specLimit}${p.unit ? ` ${p.unit}` : ""}` : "—"}</TableCell>
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
        analysis.parameterResults.map((p) => (
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
      {analysis.analysisType !== "HplcMethodAssay" && analysis.analysisType !== "IcpMethodAssay" &&
        analysis.parameterResults
          .filter((p) => p.readings.length > 0 && !isIcpCalculation(p.calculationJson))
          .map((p) => (
            <AnalysisReadingsTable key={p.id} parameter={p} analysisType={analysis.analysisType} />
          ))}
    </Box>
  );
}

// Raw readings behind one parameter result; only columns with a value are shown.
export function AnalysisReadingsTable({ parameter, analysisType }: { parameter: ParameterResultDetail; analysisType?: string }) {
  const cellSx = { fontSize: 12, py: 0.5 };
  const headSx = { fontSize: 12, fontWeight: 700, color: "text.secondary", py: 0.5 };
  const r = parameter.readings;
  const isVessel = r.some((x) => x.kind === "Vessel");
  const isDisintegration = analysisType === "Disintegration";
  const isWeightVariation = analysisType === "WeightVariation";
  const isTitration = analysisType === "Titration";
  const isTablet = isWeightVariation && r.every((x) => x.value2 == null);
  type ReadingColumn = { label: string; get: (x: ResultReadingDetail) => string | null };
  const allCols: ReadingColumn[] = [
    {
      label: "Stage",
      get: (x) =>
        x.stage != null
          ? isVessel || isDisintegration || isWeightVariation
            ? `S${x.stage}`
            : String(x.stage)
          : null
    },
    {
      label: "Time (min)",
      get: (x) => (!isDisintegration && !isWeightVariation && x.timePointMinutes != null ? num(x.timePointMinutes) : null)
    },
    {
      label: isVessel
        ? "Peak Area"
        : isDisintegration
        ? "Time (min)"
        : isWeightVariation
        ? (isTablet ? "Weight (mg)" : "Gross (mg)")
        : isTitration
        ? "Sample weight (mg)"
        : "Value 1",
      get: (x) => (x.value1 != null ? num(x.value1) : isDisintegration && x.text ? x.text : null)
    },
    {
      label: isWeightVariation ? "Shell (mg)" : isTitration ? "Titrant volume (mL)" : "Value 2",
      get: (x) => (!isTablet && x.value2 != null ? num(x.value2) : null)
    },
    {
      label: isWeightVariation ? "Deviation %" : isTitration ? "Corrected volume (mL)" : "Value 3",
      get: (x) => (x.value3 != null ? (isWeightVariation ? `${num(x.value3)} %` : num(x.value3)) : null)
    },
    {
      label: "Text",
      get: (x) => (isDisintegration || isWeightVariation ? null : (x.text || null))
    },
    {
      label: isVessel ? "% Dissolved" : isWeightVariation ? "Net (mg)" : isTitration ? "Result" : "Computed",
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
      label: "Pass",
      get: (x) => (x.passed == null ? null : x.passed ? "Pass" : "Fail")
    }
  ];
  const cols = allCols.filter((c) => r.some((x) => c.get(x) !== null));

  return (
    <Box sx={{ mt: 1.5 }}>
      <Typography sx={{ fontSize: 12, fontWeight: 700, mb: 0.5 }}>Readings · {parameter.parameterName}</Typography>
      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={headSx}>#</TableCell>
              {cols.map((c) => <TableCell key={c.label} sx={headSx}>{c.label}</TableCell>)}
            </TableRow>
          </TableHead>
          <TableBody>
            {r.map((x) => (
              <TableRow key={x.id}>
                <TableCell sx={cellSx}>{x.index}</TableCell>
                {cols.map((c) => <TableCell key={c.label} sx={cellSx}>{c.get(x) ?? "—"}</TableCell>)}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
}
