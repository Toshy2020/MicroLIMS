import { Box, Stack, Tooltip, Typography } from "@mui/material";
import { CriteriaCard, NumericCell, RegisterTable } from "../../../components/lab";
import type { CriteriaRow, RegisterColumn } from "../../../components/lab";
import { StatusBadge } from "../../../components/StatusBadge";
import type { HplcSstAnalyteDto } from "../types";
import type {
  HplcMethodResponse,
  HplcMethodAnalyteResponse
} from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";

export interface SstValuesTableProps {
  analytes: HplcSstAnalyteDto[];
  method?: HplcMethodResponse | null;
}

interface ReportedRow {
  param: string;
  criterionText: string;
  reportedValue: string | number;
}

function findMethodAnalyte(method: HplcMethodResponse | null | undefined, a: HplcSstAnalyteDto) {
  return method?.analytes.find((ma) => ma.id === a.hplcMethodAnalyteId || ma.name === a.analyteName);
}

// Method limits as configured (display only; verdicts come from the server).
function methodCriteria(ma: HplcMethodAnalyteResponse): { param: string; criterion: string }[] {
  const rows: { param: string; criterion: string }[] = [];
  if (ma.sstMaxRsdPercent != null) rows.push({ param: "%RSD", criterion: `≤ ${ma.sstMaxRsdPercent}%` });
  if (ma.sstMinResolution != null) rows.push({ param: "Resolution", criterion: `≥ ${ma.sstMinResolution}` });
  if (ma.sstMaxTailingFactor != null) rows.push({ param: "Tailing Factor", criterion: `≤ ${ma.sstMaxTailingFactor}` });
  if (ma.sstMinTheoreticalPlates != null) rows.push({ param: "Theoretical Plates", criterion: `≥ ${ma.sstMinTheoreticalPlates}` });
  if (ma.sstMinRetentionFactor != null) rows.push({ param: "Retention Factor (k')", criterion: `≥ ${ma.sstMinRetentionFactor}` });
  if (ma.sstMinSignalToNoise != null) rows.push({ param: "Signal-to-Noise", criterion: `≥ ${ma.sstMinSignalToNoise}` });
  if (ma.sstMinPeakToValley != null) rows.push({ param: "Peak-to-Valley", criterion: `≥ ${ma.sstMinPeakToValley}` });
  return rows;
}

function reportedRows(a: HplcSstAnalyteDto, ma: HplcMethodAnalyteResponse | undefined): ReportedRow[] {
  const rows: ReportedRow[] = [];
  const add = (param: string, criterion: string | null, reported: string | number | null | undefined) => {
    if (criterion === null && reported == null) return;
    rows.push({ param, criterionText: criterion ?? "—", reportedValue: reported ?? "—" });
  };
  add("%RSD", ma?.sstMaxRsdPercent != null ? `≤ ${ma.sstMaxRsdPercent}%` : null, a.reportedRsdPercent != null ? `${a.reportedRsdPercent}%` : null);
  add("Resolution", ma?.sstMinResolution != null ? `≥ ${ma.sstMinResolution}` : null, a.resolution);
  add("Tailing Factor", ma?.sstMaxTailingFactor != null ? `≤ ${ma.sstMaxTailingFactor}` : null, a.tailingFactor);
  add("Theoretical Plates", ma?.sstMinTheoreticalPlates != null ? `≥ ${ma.sstMinTheoreticalPlates}` : null, a.theoreticalPlates);
  add("Retention Factor (k')", ma?.sstMinRetentionFactor != null ? `≥ ${ma.sstMinRetentionFactor}` : null, a.retentionFactor);
  add("Signal-to-Noise", ma?.sstMinSignalToNoise != null ? `≥ ${ma.sstMinSignalToNoise}` : null, a.signalToNoise);
  add("Peak-to-Valley", ma?.sstMinPeakToValley != null ? `≥ ${ma.sstMinPeakToValley}` : null, a.peakToValley);
  return rows;
}

export function SstValuesTable({ analytes, method }: SstValuesTableProps) {
  const criteria: CriteriaRow[] = analytes.flatMap((a) => {
    const ma = findMethodAnalyte(method, a);
    return ma
      ? methodCriteria(ma).map((c) => ({ parameter: `${a.analyteName} - ${c.param}`, criterion: c.criterion, source: "Method" }))
      : [];
  });

  const columns: RegisterColumn<HplcSstAnalyteDto>[] = [
    { key: "analyteName", label: "Analyte", sortable: true, render: (a) => <strong>{a.analyteName}</strong> },
    {
      key: "standardMaterialBatch",
      label: "Standard Lot",
      render: (a) =>
        a.standardMaterialBatch ? (
          <Box>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>{a.standardMaterialBatch}</Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              P: {a.standardPurityPercent ?? "—"}% | MC: {a.standardMoisturePercent ?? "—"}%
            </Typography>
          </Box>
        ) : (
          <Typography variant="caption" sx={{ color: "text.secondary" }}>Not selected</Typography>
        )
    },
    {
      key: "standardWeightMg",
      label: "Std Weight (mg)",
      numeric: true,
      render: (a) => <NumericCell value={a.standardWeightMg} />
    },
    {
      key: "meanResponse",
      label: "Mean Response",
      numeric: true,
      render: (a) => (
        <Box>
          <Box component="span" sx={{ fontVariantNumeric: "tabular-nums" }}>
            {a.meanResponse != null ? a.meanResponse.toLocaleString(undefined, { maximumFractionDigits: 2 }) : "—"}
          </Box>
          {a.meanResponse != null && !a.passed && !a.failureReasons && (
            <Typography variant="caption" sx={{ color: "text.secondary", fontSize: 12, display: "block" }}>
              pending confirmation
            </Typography>
          )}
        </Box>
      )
    },
    {
      key: "computedRsdPercent",
      label: "Computed %RSD",
      numeric: true,
      render: (a) => (
        <Box>
          <NumericCell value={a.computedRsdPercent} decimals={2} unit="%" />
          {a.computedRsdPercent != null && !a.passed && !a.failureReasons && (
            <Typography variant="caption" sx={{ color: "text.secondary", fontSize: 12, display: "block" }}>
              pending confirmation
            </Typography>
          )}
        </Box>
      )
    },
    {
      key: "reported",
      label: "Reported Criteria Values",
      render: (a) => {
        const rows = reportedRows(a, findMethodAnalyte(method, a));
        if (rows.length === 0) {
          return <Typography variant="caption" sx={{ color: "text.secondary" }}>No criteria configured</Typography>;
        }
        return (
          <Box sx={{ display: "flex", flexDirection: "column", gap: 0.5 }}>
            {rows.map((r) => (
              <Typography key={r.param} variant="caption" sx={{ display: "block", fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                <strong>{r.param}:</strong> {r.reportedValue} (Criterion: {r.criterionText})
              </Typography>
            ))}
          </Box>
        );
      }
    },
    {
      key: "passed",
      label: "Verdict",
      align: "center",
      render: (a) =>
        a.passed ? (
          <StatusBadge status="Pass" />
        ) : a.failureReasons ? (
          <Tooltip title={a.failureReasons} arrow placement="left">
            <span><StatusBadge status="Fail" /></span>
          </Tooltip>
        ) : (
          <StatusBadge status="Pending" />
        )
    }
  ];

  return (
    <Stack spacing={1.5}>
      <Box>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          Acceptance Criteria vs Entered / Computed Values
        </Typography>
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          Pass/Fail verdicts are evaluated strictly by the laboratory backend engine.
        </Typography>
      </Box>
      {criteria.length > 0 && <CriteriaCard rows={criteria} />}
      <RegisterTable
        columns={columns}
        rows={analytes}
        getRowId={(a) => a.id}
        dense={false}
        empty={{ title: "No analytes on this record" }}
      />
    </Stack>
  );
}
