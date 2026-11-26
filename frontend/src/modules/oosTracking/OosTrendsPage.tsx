import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, FormControl, InputLabel, MenuItem, Select, Stack, TextField, Typography } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { Link } from "react-router-dom";
import { useTranslation } from "../../i18n";
import { PageHeader } from "../../components/PageHeader";
import { PartialOutageBanner } from "../../components/lab/PartialOutageBanner";
import { TrendTable, type TrendColumn } from "../../components/lab/TrendTable";
import { serverMessage } from "../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import { laboratorySectionService, type LaboratorySection } from "../../services/laboratorySectionService";
import type { OosInvestigationKind } from "../../services/oosInvestigationService";
import {
  OOS_TREND_GROUP_BY,
  oosTrendsService,
  type OosTrendFilter,
  type OosTrendGroupBy,
  type OosTrendGroupDto,
  type OosTrendReportDto
} from "../../services/oosTrendsService";

function numberCell(value: number) {
  return (
    <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
      {value}
    </Typography>
  );
}

// Trend report of investigations (spec 5.11): counts and median working days to conclude per group. Every
// figure is calculated by the server; this page only filters and lays it out.
export function OosTrendsPage() {
  const { t } = useTranslation("oosTracking");

  const [groupBy, setGroupBy] = useState<OosTrendGroupBy>("cause");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [kind, setKind] = useState<OosInvestigationKind | "">("");
  const [sectionId, setSectionId] = useState<number | "">("");

  const [sections, setSections] = useState<LaboratorySection[]>([]);
  const [sectionsFailed, setSectionsFailed] = useState(false);
  const [sectionsAttempt, setSectionsAttempt] = useState(0);

  const [report, setReport] = useState<OosTrendReportDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setSectionsFailed(false);
    laboratorySectionService
      .getMySections(controller.signal)
      .then(setSections)
      .catch(() => {
        if (controller.signal.aborted) return;
        setSections([]);
        setSectionsFailed(true);
      });
    return () => controller.abort();
  }, [sectionsAttempt]);

  useEffect(() => {
    const controller = new AbortController();
    const filter: OosTrendFilter = { groupBy };
    if (fromDate) filter.from = fromDate;
    if (toDate) filter.to = toDate;
    if (kind) filter.kind = kind;
    if (sectionId !== "") filter.sectionId = sectionId;

    setLoading(true);
    setError(null);
    oosTrendsService
      .getTrends(filter, controller.signal)
      .then((res) => {
        setReport(res);
        setLoading(false);
      })
      .catch((e: unknown) => {
        if (controller.signal.aborted) return;
        setReport(null);
        setError(serverMessage(e) ?? t("trends.loadError"));
        setLoading(false);
      });
    return () => controller.abort();
  }, [groupBy, fromDate, toDate, kind, sectionId, attempt, t]);

  const retry = useCallback(() => setAttempt((n) => n + 1), []);

  const columns: TrendColumn<OosTrendGroupDto>[] = [
    { key: "label", label: t(`trends.groupLabel.${groupBy}`), render: (g) => g.label },
    { key: "totalCount", label: t("trends.columns.total"), numeric: true, render: (g) => numberCell(g.totalCount) },
    { key: "openCount", label: t("trends.columns.open"), numeric: true, render: (g) => numberCell(g.openCount) },
    { key: "concludedCount", label: t("trends.columns.concluded"), numeric: true, render: (g) => numberCell(g.concludedCount) },
    { key: "confirmedCount", label: t("trends.columns.confirmed"), numeric: true, render: (g) => numberCell(g.confirmedCount) },
    { key: "invalidatedCount", label: t("trends.columns.invalidated"), numeric: true, render: (g) => numberCell(g.invalidatedCount) },
    { key: "excursionClosedCount", label: t("trends.columns.excursionClosed"), numeric: true, render: (g) => numberCell(g.excursionClosedCount) },
    {
      key: "medianWorkingDaysToConclude",
      label: t("trends.columns.median"),
      numeric: true,
      render: (g) => (
        <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
          {g.medianWorkingDaysToConclude ?? "—"}
        </Typography>
      )
    }
  ];

  const anyFilter = fromDate !== "" || toDate !== "" || kind !== "" || sectionId !== "";

  return (
    <Box>
      <PageHeader title={t("trends.title")} subtitle={t("trends.subtitle")}>
        <Button component={Link} to="/oos-tracking" startIcon={<ArrowBackIcon />}>
          {t("investigation.backToRegister")}
        </Button>
      </PageHeader>

      <Stack spacing={2}>
        {sectionsFailed && <PartialOutageBanner sources={[t("trends.sectionsSource")]} onRetry={() => setSectionsAttempt((n) => n + 1)} />}

        <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: { sm: "center" }, flexWrap: "wrap" }}>
          <FormControl size="small" sx={{ minWidth: 200 }}>
            <InputLabel id="oos-trend-group-by">{t("trends.filters.groupBy")}</InputLabel>
            <Select
              labelId="oos-trend-group-by"
              label={t("trends.filters.groupBy")}
              value={groupBy}
              onChange={(e) => setGroupBy(e.target.value as OosTrendGroupBy)}
            >
              {OOS_TREND_GROUP_BY.map((g) => (
                <MenuItem key={g} value={g}>
                  {t(`trends.groupBy.${g}`)}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="oos-trend-kind">{t("trends.filters.kind")}</InputLabel>
            <Select
              labelId="oos-trend-kind"
              label={t("trends.filters.kind")}
              value={kind}
              onChange={(e) => setKind(e.target.value as OosInvestigationKind | "")}
            >
              <MenuItem value="">{t("trends.filters.allKinds")}</MenuItem>
              <MenuItem value="Oos">{t("trends.kinds.Oos")}</MenuItem>
              <MenuItem value="Excursion">{t("trends.kinds.Excursion")}</MenuItem>
            </Select>
          </FormControl>

          <FormControl size="small" sx={{ minWidth: 180 }}>
            <InputLabel id="oos-trend-section">{t("register.filters.section")}</InputLabel>
            <Select
              labelId="oos-trend-section"
              label={t("register.filters.section")}
              value={sectionId}
              onChange={(e) => {
                const raw = e.target.value as number | "";
                setSectionId(raw === "" ? "" : Number(raw));
              }}
            >
              <MenuItem value="">{t("register.filters.allSections")}</MenuItem>
              {sections.map((s) => (
                <MenuItem key={s.sectionId} value={s.sectionId}>
                  {s.sectionName}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <TextField
            size="small"
            type="date"
            label={t("register.filters.from")}
            slotProps={{ inputLabel: { shrink: true } }}
            value={fromDate}
            onChange={(e) => setFromDate(e.target.value)}
          />
          <TextField
            size="small"
            type="date"
            label={t("register.filters.to")}
            slotProps={{ inputLabel: { shrink: true } }}
            value={toDate}
            onChange={(e) => setToDate(e.target.value)}
          />

          {anyFilter && (
            <Button
              size="small"
              onClick={() => {
                setFromDate("");
                setToDate("");
                setKind("");
                setSectionId("");
              }}
            >
              {t("register.filters.clear")}
            </Button>
          )}
        </Stack>

        {report && (report.fromUtc || report.toUtc) && (
          <Typography variant="body2" color="text.secondary" sx={{ fontVariantNumeric: "tabular-nums" }}>
            {t("trends.period", {
              from: report.fromUtc ? formatDateTimeWithTimeZone(report.fromUtc) : "—",
              to: report.toUtc ? formatDateTimeWithTimeZone(report.toUtc) : "—"
            })}
          </Typography>
        )}

        {error ? (
          <Alert
            severity="error"
            role="alert"
            action={
              <Button color="inherit" size="small" onClick={retry}>
                {t("trends.retry")}
              </Button>
            }
          >
            {error}
          </Alert>
        ) : (
          <div aria-live="polite">
            <TrendTable
              label={t("trends.title")}
              columns={columns}
              rows={report?.groups ?? []}
              getRowId={(g) => g.key}
              loading={loading}
              emptyTitle={t("trends.empty")}
            />
          </div>
        )}
      </Stack>
    </Box>
  );
}
