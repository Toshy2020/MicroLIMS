import { useCallback, useEffect, useMemo, useState } from "react";
import { Alert, Box, Button, Checkbox, FormControlLabel, MenuItem, Stack, Tab, Tabs, TextField, Typography } from "@mui/material";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useTranslation } from "../../i18n";
import { useAuth } from "../../contexts/AuthContext";
import { PageHeader } from "../../components/PageHeader";
import { MetaBadge, StatusBadge, ToneBadge } from "../../components/StatusBadge";
import { PartialOutageBanner } from "../../components/lab/PartialOutageBanner";
import { RegisterTable, type RegisterColumn } from "../../components/lab/RegisterTable";
import { monospaceFontFamily } from "../../theme/palette";
import { QualityCreateActions } from "./QualityCreateActions";
import { statusLabel } from "../../theme/statusTokens";
import { getErrorMessage } from "../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import { getMySections, type LaboratorySection } from "../../services/laboratorySectionService";
import {
  qualityService,
  type QualityClassificationDto,
  type QualityRecordFilter,
  type QualityRecordListItemDto,
  type QualityRecordType
} from "../../services/qualityService";

type RegisterTab = "all" | QualityRecordType;

const TABS: readonly RegisterTab[] = ["all", "Deviation", "Capa", "ChangeControl", "Complaint"];

// The states each record type passes through, as the server names them (the status filter of
// GET api/quality/records takes these names). The path itself is the server's; this only lists them.
const STATUSES_BY_TAB: Record<RegisterTab, readonly string[]> = {
  all: [
    "Raised", "Investigation", "LabConcluded", "Approved", "Closed", "Rejected", "Plan", "PlanApproved", "CompletionVerified",
    "Proposed", "Assessed", "ApprovedForImplementation", "Received", "LabReported", "Concluded", "Cancelled"
  ],
  Deviation: ["Raised", "Investigation", "LabConcluded", "Approved", "Closed", "Rejected", "Cancelled"],
  Capa: ["Plan", "PlanApproved", "CompletionVerified", "Closed", "Cancelled"],
  ChangeControl: ["Proposed", "Assessed", "ApprovedForImplementation", "Closed", "Rejected", "Cancelled"],
  Complaint: ["Received", "Investigation", "LabReported", "Concluded", "Closed", "Cancelled"]
};

// The server clamps list sizes to 100.
const LOOKUP_PAGE_SIZE = 100;

interface RegisterUrlFilters {
  tab: RegisterTab;
  status: string;
  classificationId: number | "";
  sectionId: number | "";
  ownedByMe: boolean;
  overdueOnly: boolean;
  fromDate: string;
  toDate: string;
  page: number; // zero-based; the URL carries it one-based
}

const positiveInt = (raw: string | null): number | "" => {
  if (raw === null || !/^[1-9]\d{0,9}$/.test(raw)) return "";
  return Number(raw);
};

const isoDate = (raw: string | null): string => {
  if (raw === null || !/^\d{4}-\d{2}-\d{2}$/.test(raw)) return "";
  const parsed = new Date(`${raw}T00:00:00.000Z`);
  return Number.isNaN(parsed.getTime()) || parsed.toISOString().slice(0, 10) !== raw ? "" : raw;
};

// Invalid values fall back to the default (never reach the server); the server stays the authority.
function readFilters(params: URLSearchParams): RegisterUrlFilters {
  const rawTab = params.get("type");
  const tab = TABS.find((value) => value !== "all" && value === rawTab) ?? "all";
  const rawStatus = params.get("status");
  const status = rawStatus !== null && STATUSES_BY_TAB[tab].includes(rawStatus) ? rawStatus : "";
  const page = positiveInt(params.get("page"));
  return {
    tab,
    status,
    classificationId: positiveInt(params.get("classificationId")),
    sectionId: positiveInt(params.get("sectionId")),
    ownedByMe: params.get("owner") === "me",
    overdueOnly: params.get("overdue") === "true",
    fromDate: isoDate(params.get("from")),
    toDate: isoDate(params.get("to")),
    page: page === "" ? 0 : page - 1
  };
}

function writeFilters(next: RegisterUrlFilters): URLSearchParams {
  const params = new URLSearchParams();
  if (next.tab !== "all") params.set("type", next.tab);
  if (next.status) params.set("status", next.status);
  if (next.classificationId !== "") params.set("classificationId", String(next.classificationId));
  if (next.sectionId !== "") params.set("sectionId", String(next.sectionId));
  if (next.ownedByMe) params.set("owner", "me");
  if (next.overdueOnly) params.set("overdue", "true");
  if (next.fromDate) params.set("from", next.fromDate);
  if (next.toDate) params.set("to", next.toDate);
  if (next.page > 0) params.set("page", String(next.page + 1));
  return params;
}

type LookupKey = "classifications" | "sections";

export function QualityRegisterPage() {
  const { t } = useTranslation("quality");
  const { t: tCommon } = useTranslation("common");
  const navigate = useNavigate();
  const { userId } = useAuth();

  const [searchParams, setSearchParams] = useSearchParams();
  const filters = useMemo(() => readFilters(searchParams), [searchParams]);
  const { tab, status, classificationId, sectionId, ownedByMe, overdueOnly, fromDate, toDate, page } = filters;
  // replace, not push: Back leaves the register instead of stepping through every filter change.
  const update = useCallback(
    (patch: Partial<RegisterUrlFilters>) => setSearchParams(writeFilters({ ...filters, page: 0, ...patch }), { replace: true }),
    [filters, setSearchParams]
  );
  const [pageSize, setPageSize] = useState(25);

  const [classifications, setClassifications] = useState<QualityClassificationDto[]>([]);
  const [sections, setSections] = useState<LaboratorySection[]>([]);
  const [hasActiveQa, setHasActiveQa] = useState(true);
  const [failedLookups, setFailedLookups] = useState<LookupKey[]>([]);
  const [lookupAttempt, setLookupAttempt] = useState(0);

  const [rows, setRows] = useState<QualityRecordListItemDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    const failed: LookupKey[] = [];
    const markFailed = (key: LookupKey) => {
      if (!controller.signal.aborted) failed.push(key);
    };
    void Promise.all([
      qualityService
        .listClassifications(1, LOOKUP_PAGE_SIZE, controller.signal)
        .then((result) => {
          if (!controller.signal.aborted) setClassifications(result);
        })
        .catch(() => markFailed("classifications")),
      getMySections(controller.signal)
        .then((result) => {
          if (!controller.signal.aborted) setSections(result);
        })
        .catch(() => markFailed("sections")),
      qualityService
        .getQaAvailability(controller.signal)
        .then((result) => {
          if (!controller.signal.aborted) setHasActiveQa(result.hasActiveQa);
        })
        .catch(() => {
          // Keep default true if availability check fails so we don't show a false warning.
        })
    ]).then(() => {
      if (!controller.signal.aborted) setFailedLookups(failed);
    });
    return () => controller.abort();
  }, [lookupAttempt]);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);

    const filter: QualityRecordFilter = { page: page + 1, pageSize };
    if (tab !== "all") filter.type = tab;
    if (status) filter.status = status;
    if (classificationId !== "") filter.classificationId = classificationId;
    if (sectionId !== "") filter.sectionId = sectionId;
    if (ownedByMe && userId !== null) filter.ownerUserId = userId;
    if (overdueOnly) filter.overdue = true;
    // A whole day, in UTC: the register filters on the instant a record was raised.
    if (fromDate) filter.from = `${fromDate}T00:00:00.000Z`;
    if (toDate) filter.to = `${toDate}T23:59:59.999Z`;

    qualityService
      .listRecords(filter, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setRows(result.items);
        setTotalCount(result.totalCount);
        setLoading(false);
      })
      .catch((e: unknown) => {
        if (controller.signal.aborted) return;
        setError(getErrorMessage(e, t("register.loadError")));
        setLoading(false);
      });

    return () => controller.abort();
  }, [tab, status, classificationId, sectionId, ownedByMe, overdueOnly, fromDate, toDate, page, pageSize, userId, attempt, t]);

  const filtersActive =
    status !== "" || classificationId !== "" || sectionId !== "" || ownedByMe || overdueOnly || fromDate !== "" || toDate !== "";

  const changeTab = (next: RegisterTab) => update({ tab: next, status: "" });

  const clearFilters = () =>
    update({ status: "", classificationId: "", sectionId: "", ownedByMe: false, overdueOnly: false, fromDate: "", toDate: "" });

  const columns: RegisterColumn<QualityRecordListItemDto>[] = [
    {
      key: "number",
      label: t("register.columns.number"),
      nowrap: true,
      sortValue: (row) => row.number,
      render: (row) => (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
          <Typography variant="body2" sx={{ fontFamily: monospaceFontFamily, fontWeight: 700 }}>
            {row.number}
          </Typography>
          {row.isOverdue && <ToneBadge tone="detected" label={t("register.overdueBadge")} />}
        </Stack>
      )
    },
    { key: "type", label: t("register.columns.type"), render: (row) => <MetaBadge label={t(`types.${row.type}`)} /> },
    { key: "title", label: t("register.columns.title") },
    {
      key: "status",
      label: t("register.columns.status"),
      render: (row) => <StatusBadge status={row.isCancelled ? "Cancelled" : row.status} />
    },
    { key: "classification", label: t("register.columns.classification"), render: (row) => row.classificationName ?? "—" },
    { key: "section", label: t("register.columns.section"), render: (row) => row.sectionName ?? "—" },
    { key: "owner", label: t("register.columns.owner"), render: (row) => row.ownerName ?? "—" },
    {
      key: "dueDate",
      label: t("register.columns.dueDate"),
      numeric: true,
      render: (row) => (
        <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
          {row.dueDate ?? "—"}
        </Typography>
      )
    },
    {
      key: "raised",
      label: t("register.columns.raised"),
      numeric: true,
      render: (row) => (
        <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
          {formatDateTimeWithTimeZone(row.raisedAtUtc)}
        </Typography>
      )
    }
  ];

  const statusOptions = STATUSES_BY_TAB[tab];

  return (
    <Box>
      <PageHeader title={t("register.title")} subtitle={t("register.subtitle")}>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
          <Button component={Link} to="/quality/trends" variant="outlined">
            {t("trends.openTrends")}
          </Button>
          <QualityCreateActions />
        </Stack>
      </PageHeader>

      <Tabs
        value={tab}
        onChange={(_, value: RegisterTab) => changeTab(value)}
        aria-label={t("register.tabsLabel")}
        variant="scrollable"
        scrollButtons="auto"
        sx={{ mb: 2, borderBottom: 1, borderColor: "divider" }}
      >
        {TABS.map((value) => (
          <Tab key={value} value={value} label={t(`typeTabs.${value}`)} />
        ))}
      </Tabs>

      <Stack spacing={2}>
        {!hasActiveQa && (
          <Alert severity="warning" role="status">
            {t("register.noActiveQa")}
          </Alert>
        )}
        {failedLookups.length > 0 && (
          <PartialOutageBanner
            sources={failedLookups.map((key) => t(`register.outageSources.${key}`))}
            onRetry={() => setLookupAttempt((n) => n + 1)}
          />
        )}

        <Box component="section" aria-label={t("register.filtersLabel")}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: { sm: "center" }, flexWrap: "wrap", rowGap: 1.5 }}>
            <TextField
              select
              size="small"
              label={t("register.filters.status")}
              value={status}
              onChange={(e) => {
                update({ status: e.target.value });
              }}
              sx={{ minWidth: 180 }}
            >
              <MenuItem value="">{t("register.filters.allStatuses")}</MenuItem>
              {statusOptions.map((value) => (
                <MenuItem key={value} value={value}>
                  {statusLabel(value, tCommon)}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              size="small"
              label={t("register.filters.classification")}
              value={classificationId}
              onChange={(e) => {
                update({ classificationId: e.target.value === "" ? "" : Number(e.target.value) });
              }}
              sx={{ minWidth: 180 }}
            >
              <MenuItem value="">{t("register.filters.allClassifications")}</MenuItem>
              {classifications.map((c) => (
                <MenuItem key={c.id} value={c.id}>
                  {c.name}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              size="small"
              label={t("register.filters.section")}
              value={sectionId}
              onChange={(e) => {
                update({ sectionId: e.target.value === "" ? "" : Number(e.target.value) });
              }}
              sx={{ minWidth: 180 }}
            >
              <MenuItem value="">{t("register.filters.allSections")}</MenuItem>
              {sections.map((s) => (
                <MenuItem key={s.sectionId} value={s.sectionId}>
                  {s.sectionName}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              size="small"
              type="date"
              label={t("register.filters.from")}
              slotProps={{ inputLabel: { shrink: true } }}
              value={fromDate}
              onChange={(e) => {
                update({ fromDate: e.target.value });
              }}
            />
            <TextField
              size="small"
              type="date"
              label={t("register.filters.to")}
              slotProps={{ inputLabel: { shrink: true } }}
              value={toDate}
              onChange={(e) => {
                update({ toDate: e.target.value });
              }}
            />

            {userId !== null && (
              <FormControlLabel
                control={
                  <Checkbox
                    checked={ownedByMe}
                    onChange={(e) => {
                      update({ ownedByMe: e.target.checked });
                    }}
                  />
                }
                label={t("register.filters.ownedByMe")}
              />
            )}
            <FormControlLabel
              control={
                <Checkbox
                  checked={overdueOnly}
                  onChange={(e) => {
                    update({ overdueOnly: e.target.checked });
                  }}
                />
              }
              label={t("register.filters.overdueOnly")}
            />

            {filtersActive && (
              <Button size="small" onClick={clearFilters}>
                {t("register.filters.clear")}
              </Button>
            )}
          </Stack>
        </Box>

        {error && (
          <Alert
            severity="error"
            action={
              <Button color="inherit" size="small" onClick={() => setAttempt((n) => n + 1)}>
                {tCommon("actions.retry")}
              </Button>
            }
          >
            {error}
          </Alert>
        )}

        <Typography role="status" variant="caption" color="text.secondary">
          {!loading && !error ? t("register.shown", { shown: rows.length, total: totalCount }) : ""}
        </Typography>

        <RegisterTable
          tableKey="quality.register"
          columns={columns}
          rows={rows}
          getRowId={(row) => row.id}
          loading={loading}
          onRowClick={(row) => navigate(`/quality/${row.id}`)}
          empty={{ title: t("register.emptyTitle"), description: t("register.emptyDescription") }}
          serverPaged={{
            totalCount,
            page,
            pageSize,
            onPageChange: (next) => update({ page: next }),
            onPageSizeChange: (next) => {
              setPageSize(next);
              update({ page: 0 });
            }
          }}
          trailingCell={{
            label: t("register.columns.actions"),
            render: (row) => (
              <Button
                size="small"
                variant="outlined"
                aria-label={t("register.openDetailAria", { number: row.number })}
                onClick={(e) => {
                  e.stopPropagation();
                  navigate(`/quality/${row.id}`);
                }}
              >
                {t("register.openDetail")}
              </Button>
            )
          }}
        />
      </Stack>
    </Box>
  );
}
