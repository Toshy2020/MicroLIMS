import { useCallback, useEffect, useState } from "react";
import { Alert, Box, Button, FormControl, InputLabel, MenuItem, Select, Stack, TextField, Typography } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { Link } from "react-router-dom";
import { useTranslation } from "../../i18n";
import { PageHeader } from "../../components/PageHeader";
import { EmptyState } from "../../components/lab/EmptyState";
import { RegisterTable, type RegisterColumn } from "../../components/lab/RegisterTable";
import { getErrorMessage } from "../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import {
  QUALITY_REPEAT_WINDOW_DEFAULT_DAYS,
  QUALITY_TREND_GROUP_BY,
  qualityTrendsService,
  type QualityRepeatCauseDto,
  type QualityTrendFilter,
  type QualityTrendGroupBy,
  type QualityTrendGroupDto,
  type QualityTrendReportDto
} from "../../services/qualityTrendsService";
import { PanelFrame } from "./components/PanelFrame";

const NONE = "—";

function numberCell(value: number) {
  return (
    <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
      {value}
    </Typography>
  );
}

function nullableCell(text: string | null) {
  return (
    <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
      {text ?? NONE}
    </Typography>
  );
}

// The server sends a rate as a fraction (0.8333); showing it as a percentage is formatting, not calculation.
const percent = (rate: number | null) => (rate === null ? null : `${(rate * 100).toFixed(1)}%`);

// Trend report of quality records (spec 5.11): counts, time to close, on-time rate, CAPA effectiveness rate
// and repeat causes. Every figure comes from GET api/quality/trends; the page only filters and lays it out.
export function QualityTrendsPage() {
  const { t } = useTranslation("quality");

  const [groupBy, setGroupBy] = useState<QualityTrendGroupBy>("type");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [windowDays, setWindowDays] = useState(String(QUALITY_REPEAT_WINDOW_DEFAULT_DAYS));

  const [report, setReport] = useState<QualityTrendReportDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  // The server clamps the window to 1..3650 itself; an empty or non-numeric box falls back to its default.
  const parsedWindow = Number.parseInt(windowDays, 10);
  const repeatWindowDays = Number.isFinite(parsedWindow) && parsedWindow > 0 ? parsedWindow : QUALITY_REPEAT_WINDOW_DEFAULT_DAYS;

  useEffect(() => {
    const controller = new AbortController();
    const filter: QualityTrendFilter = { groupBy, repeatWindowDays };
    if (fromDate) filter.from = fromDate;
    if (toDate) filter.to = toDate;

    setLoading(true);
    setError(null);
    qualityTrendsService
      .getTrends(filter, controller.signal)
      .then((res) => {
        setReport(res);
        setLoading(false);
      })
      .catch((e: unknown) => {
        if (controller.signal.aborted) return;
        setReport(null);
        setError(getErrorMessage(e, t("trends.loadError")));
        setLoading(false);
      });
    return () => controller.abort();
  }, [groupBy, fromDate, toDate, repeatWindowDays, attempt, t]);

  const retry = useCallback(() => setAttempt((n) => n + 1), []);

  const columns: RegisterColumn<QualityTrendGroupDto>[] = [
    { key: "label", label: t(`trends.groupBy.${groupBy}`), sortable: true, sortValue: (g) => g.label },
    { key: "totalCount", label: t("trends.columns.total"), numeric: true, sortable: true, render: (g) => numberCell(g.totalCount) },
    { key: "openCount", label: t("trends.columns.open"), numeric: true, sortable: true, render: (g) => numberCell(g.openCount) },
    { key: "closedCount", label: t("trends.columns.closed"), numeric: true, sortable: true, render: (g) => numberCell(g.closedCount) },
    { key: "cancelledCount", label: t("trends.columns.cancelled"), numeric: true, sortable: true, render: (g) => numberCell(g.cancelledCount) },
    { key: "rejectedCount", label: t("trends.columns.rejected"), numeric: true, sortable: true, render: (g) => numberCell(g.rejectedCount) },
    {
      key: "averageTimeToCloseDays",
      label: t("trends.columns.averageDaysToClose"),
      numeric: true,
      sortable: true,
      sortValue: (g) => g.averageTimeToCloseDays,
      render: (g) => nullableCell(g.averageTimeToCloseDays === null ? null : String(g.averageTimeToCloseDays))
    },
    {
      key: "onTimeRate",
      label: t("trends.columns.onTimeRate"),
      numeric: true,
      sortable: true,
      sortValue: (g) => g.onTimeRate,
      render: (g) => nullableCell(percent(g.onTimeRate))
    },
    {
      key: "capaEffectivenessRate",
      label: t("trends.columns.capaEffectiveness"),
      numeric: true,
      sortable: true,
      sortValue: (g) => g.capaEffectivenessRate,
      render: (g) => nullableCell(percent(g.capaEffectivenessRate))
    }
  ];

  const repeatColumns: RegisterColumn<QualityRepeatCauseDto>[] = [
    { key: "causeCategoryName", label: t("trends.repeat.cause"), sortable: true },
    {
      key: "targetLabel",
      label: t("trends.repeat.target"),
      sortable: true,
      render: (r) => (
        <Stack>
          <Typography variant="body2">{r.targetLabel}</Typography>
          <Typography variant="caption" color="text.secondary">
            {t(`links.targetTypes.${r.targetType}`)}
          </Typography>
        </Stack>
      )
    },
    { key: "occurrenceCount", label: t("trends.repeat.occurrences"), numeric: true, sortable: true, render: (r) => numberCell(r.occurrenceCount) },
    {
      key: "firstRaisedAtUtc",
      label: t("trends.repeat.first"),
      numeric: true,
      sortable: true,
      render: (r) => nullableCell(formatDateTimeWithTimeZone(r.firstRaisedAtUtc))
    },
    {
      key: "lastRaisedAtUtc",
      label: t("trends.repeat.last"),
      numeric: true,
      sortable: true,
      render: (r) => nullableCell(formatDateTimeWithTimeZone(r.lastRaisedAtUtc))
    },
    {
      key: "recordNumbers",
      label: t("trends.repeat.records"),
      sortable: false,
      render: (r) => (
        <Typography variant="body2" sx={{ wordBreak: "break-word" }}>
          {r.recordNumbers.join(", ")}
        </Typography>
      )
    }
  ];

  const anyFilter = fromDate !== "" || toDate !== "";

  return (
    <Box>
      <PageHeader title={t("trends.title")} subtitle={t("trends.subtitle")}>
        <Button component={Link} to="/quality" startIcon={<ArrowBackIcon />}>
          {t("trends.back")}
        </Button>
      </PageHeader>

      <Stack spacing={2}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: { sm: "center" }, flexWrap: "wrap" }}>
          <FormControl size="small" sx={{ minWidth: 200 }}>
            <InputLabel id="quality-trend-group-by">{t("trends.filters.groupBy")}</InputLabel>
            <Select
              labelId="quality-trend-group-by"
              label={t("trends.filters.groupBy")}
              value={groupBy}
              onChange={(e) => setGroupBy(e.target.value as QualityTrendGroupBy)}
            >
              {QUALITY_TREND_GROUP_BY.map((g) => (
                <MenuItem key={g} value={g}>
                  {t(`trends.groupBy.${g}`)}
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
          <TextField
            size="small"
            type="number"
            label={t("trends.filters.repeatWindow")}
            value={windowDays}
            onChange={(e) => setWindowDays(e.target.value)}
            slotProps={{ htmlInput: { min: 1, max: 3650 } }}
            sx={{ width: 190 }}
          />
          {anyFilter && (
            <Button
              size="small"
              onClick={() => {
                setFromDate("");
                setToDate("");
              }}
            >
              {t("register.filters.clear")}
            </Button>
          )}
        </Stack>

        {report && (report.fromUtc || report.toUtc) && (
          <Typography variant="body2" color="text.secondary" sx={{ fontVariantNumeric: "tabular-nums" }}>
            {t("trends.period", {
              from: report.fromUtc ? formatDateTimeWithTimeZone(report.fromUtc) : NONE,
              to: report.toUtc ? formatDateTimeWithTimeZone(report.toUtc) : NONE
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
            <Stack spacing={2.5}>
              <RegisterTable
                columns={columns}
                rows={report?.groups ?? []}
                getRowId={(g) => g.key}
                loading={loading}
                defaultSort={{ key: "totalCount", direction: "desc" }}
                empty={{ title: t("trends.empty") }}
              />
              {report && (
                <PanelFrame title={t("trends.repeat.title")}>
                  {report.repeatCauses.length === 0 ? (
                    <EmptyState title={t("trends.repeat.empty")} />
                  ) : (
                    <>
                      <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                        {t("trends.repeat.intro", { days: report.repeatWindowDays })}
                      </Typography>
                      <RegisterTable
                        columns={repeatColumns}
                        rows={report.repeatCauses}
                        getRowId={(r) => `${r.causeCategoryId}|${r.targetType}|${r.targetId}`}
                        empty={{ title: t("trends.repeat.empty") }}
                      />
                    </>
                  )}
                </PanelFrame>
              )}
            </Stack>
          </div>
        )}
      </Stack>
    </Box>
  );
}
