import { Box, Stack, Tooltip, Typography } from "@mui/material";
import { CriteriaCard, NumericCell, RegisterTable } from "../../../components/lab";
import type { CriteriaRow, RegisterColumn } from "../../../components/lab";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import type { TFunction } from "i18next";
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
function methodCriteria(ma: HplcMethodAnalyteResponse, t: TFunction<"hplcWorkspace">): { param: string; criterion: string }[] {
  const rows: { param: string; criterion: string }[] = [];
  if (ma.sstMaxRsdPercent != null) rows.push({ param: t("sst.valuesTable.params.rsd"), criterion: `≤ ${ma.sstMaxRsdPercent}%` });
  if (ma.sstMinResolution != null) rows.push({ param: t("sst.valuesTable.params.resolution"), criterion: `≥ ${ma.sstMinResolution}` });
  if (ma.sstMaxTailingFactor != null) rows.push({ param: t("sst.valuesTable.params.tailingFactor"), criterion: `≤ ${ma.sstMaxTailingFactor}` });
  if (ma.sstMinTheoreticalPlates != null) rows.push({ param: t("sst.valuesTable.params.theoreticalPlates"), criterion: `≥ ${ma.sstMinTheoreticalPlates}` });
  if (ma.sstMinRetentionFactor != null) rows.push({ param: t("sst.valuesTable.params.retentionFactor"), criterion: `≥ ${ma.sstMinRetentionFactor}` });
  if (ma.sstMinSignalToNoise != null) rows.push({ param: t("sst.valuesTable.params.signalToNoise"), criterion: `≥ ${ma.sstMinSignalToNoise}` });
  if (ma.sstMinPeakToValley != null) rows.push({ param: t("sst.valuesTable.params.peakToValley"), criterion: `≥ ${ma.sstMinPeakToValley}` });
  return rows;
}

function reportedRows(a: HplcSstAnalyteDto, ma: HplcMethodAnalyteResponse | undefined, t: TFunction<"hplcWorkspace">): ReportedRow[] {
  const rows: ReportedRow[] = [];
  const add = (param: string, criterion: string | null, reported: string | number | null | undefined) => {
    if (criterion === null && reported == null) return;
    rows.push({ param, criterionText: criterion ?? "—", reportedValue: reported ?? "—" });
  };
  add(t("sst.valuesTable.params.rsd"), ma?.sstMaxRsdPercent != null ? `≤ ${ma.sstMaxRsdPercent}%` : null, a.reportedRsdPercent != null ? `${a.reportedRsdPercent}%` : null);
  add(t("sst.valuesTable.params.resolution"), ma?.sstMinResolution != null ? `≥ ${ma.sstMinResolution}` : null, a.resolution);
  add(t("sst.valuesTable.params.tailingFactor"), ma?.sstMaxTailingFactor != null ? `≤ ${ma.sstMaxTailingFactor}` : null, a.tailingFactor);
  add(t("sst.valuesTable.params.theoreticalPlates"), ma?.sstMinTheoreticalPlates != null ? `≥ ${ma.sstMinTheoreticalPlates}` : null, a.theoreticalPlates);
  add(t("sst.valuesTable.params.retentionFactor"), ma?.sstMinRetentionFactor != null ? `≥ ${ma.sstMinRetentionFactor}` : null, a.retentionFactor);
  add(t("sst.valuesTable.params.signalToNoise"), ma?.sstMinSignalToNoise != null ? `≥ ${ma.sstMinSignalToNoise}` : null, a.signalToNoise);
  add(t("sst.valuesTable.params.peakToValley"), ma?.sstMinPeakToValley != null ? `≥ ${ma.sstMinPeakToValley}` : null, a.peakToValley);
  return rows;
}

export function SstValuesTable({ analytes, method }: SstValuesTableProps) {
  const { t } = useTranslation("hplcWorkspace");
  const criteria: CriteriaRow[] = analytes.flatMap((a) => {
    const ma = findMethodAnalyte(method, a);
    return ma
      ? methodCriteria(ma, t).map((c) => ({
          parameter: t("sst.valuesTable.paramWithAnalyte", { analyte: a.analyteName, param: c.param }),
          criterion: c.criterion,
          source: t("sst.valuesTable.sourceMethod")
        }))
      : [];
  });

  const columns: RegisterColumn<HplcSstAnalyteDto>[] = [
    { key: "analyteName", label: t("sst.valuesTable.columns.analyte"), sortable: true, render: (a) => <strong><Ltr>{a.analyteName}</Ltr></strong> },
    {
      key: "standardMaterialBatch",
      label: t("sst.valuesTable.columns.standardLot"),
      render: (a) =>
        a.standardMaterialBatch ? (
          <Box>
            <Typography variant="body2" sx={{ fontWeight: 600 }}><Ltr>{a.standardMaterialBatch}</Ltr></Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {t("sst.valuesTable.purityMc", {
                purity: a.standardPurityPercent ?? "—",
                mc: a.standardMoisturePercent ?? "—"
              })}
            </Typography>
          </Box>
        ) : (
          <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("sst.valuesTable.notSelected")}</Typography>
        )
    },
    {
      key: "standardWeightMg",
      label: t("sst.valuesTable.columns.stdWeight"),
      numeric: true,
      render: (a) => <NumericCell value={a.standardWeightMg} />
    },
    {
      key: "meanResponse",
      label: t("sst.valuesTable.columns.meanResponse"),
      numeric: true,
      render: (a) => (
        <Box>
          <Box component="span" sx={{ fontVariantNumeric: "tabular-nums" }}>
            <Ltr>{a.meanResponse != null ? a.meanResponse.toLocaleString(undefined, { maximumFractionDigits: 2 }) : "—"}</Ltr>
          </Box>
          {a.meanResponse != null && !a.passed && !a.failureReasons && (
            <Typography variant="caption" sx={{ color: "text.secondary", fontSize: 12, display: "block" }}>
              {t("sst.valuesTable.pendingConfirmation")}
            </Typography>
          )}
        </Box>
      )
    },
    {
      key: "computedRsdPercent",
      label: t("sst.valuesTable.columns.computedRsd"),
      numeric: true,
      render: (a) => (
        <Box>
          <NumericCell value={a.computedRsdPercent} decimals={2} unit="%" />
          {a.computedRsdPercent != null && !a.passed && !a.failureReasons && (
            <Typography variant="caption" sx={{ color: "text.secondary", fontSize: 12, display: "block" }}>
              {t("sst.valuesTable.pendingConfirmation")}
            </Typography>
          )}
        </Box>
      )
    },
    {
      key: "reported",
      label: t("sst.valuesTable.columns.reportedCriteria"),
      render: (a) => {
        const rows = reportedRows(a, findMethodAnalyte(method, a), t);
        if (rows.length === 0) {
          return <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("sst.valuesTable.noCriteriaConfigured")}</Typography>;
        }
        return (
          <Box sx={{ display: "flex", flexDirection: "column", gap: 0.5 }}>
            {rows.map((r) => (
              <Typography key={r.param} variant="caption" sx={{ display: "block", fontSize: 12, fontVariantNumeric: "tabular-nums" }}>
                <strong>{r.param}:</strong> <Ltr>{r.reportedValue}</Ltr> {t("sst.valuesTable.criterionEntry", { criterion: r.criterionText })}
              </Typography>
            ))}
          </Box>
        );
      }
    },
    {
      key: "passed",
      label: t("sst.valuesTable.columns.verdict"),
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
          {t("sst.valuesTable.title")}
        </Typography>
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          {t("sst.valuesTable.subtitle")}
        </Typography>
      </Box>
      {criteria.length > 0 && <CriteriaCard rows={criteria} />}
      <RegisterTable
        columns={columns}
        rows={analytes}
        getRowId={(a) => a.id}
        dense={false}
        empty={{ title: t("sst.valuesTable.emptyTitle") }}
      />
    </Stack>
  );
}
