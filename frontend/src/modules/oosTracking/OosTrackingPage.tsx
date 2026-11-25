import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography
} from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "../../i18n";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { PageHeader } from "../../components/PageHeader";
import { StatusBadge } from "../../components/StatusBadge";
import {
  RegisterTable,
  type RegisterColumn
} from "../../components/lab/RegisterTable";
import { monospaceFontFamily } from "../../theme/palette";
import { serverMessage } from "../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import {
  laboratorySectionService,
  type LaboratorySection
} from "../../services/laboratorySectionService";
import {
  oosInvestigationService,
  type OosInvestigationFilter,
  type OosInvestigationListItemDto,
  type OosInvestigationStatus
} from "../../services/oosInvestigationService";
import { LegacyGroupsView } from "./components/LegacyGroupsView";

type RegisterTab = "open" | "overdue" | "concluded" | "excursions" | "legacy";

export function OosTrackingPage() {
  const { t } = useTranslation("oosTracking");
  const navigate = useNavigate();
  const { permissions } = useAuth();
  const canManage = permissions.includes(PERMISSIONS.OOS_MANAGE);

  const [tab, setTab] = useState<RegisterTab>("open");
  const [sections, setSections] = useState<LaboratorySection[]>([]);
  const [sectionId, setSectionId] = useState<number | "">("");
  const [statusFilter, setStatusFilter] = useState<OosInvestigationStatus | "">("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(25);

  const [rows, setRows] = useState<OosInvestigationListItemDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    laboratorySectionService
      .getMySections(controller.signal)
      .then(setSections)
      .catch(() => {
        if (!controller.signal.aborted) setSections([]);
      });
    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (tab === "legacy") return;

    const controller = new AbortController();
    setLoading(true);
    setError(null);

    const filter: OosInvestigationFilter = {
      page: page + 1,
      pageSize
    };

    if (tab === "open") {
      filter.kind = "Oos";
      filter.openOnly = true;
      if (statusFilter) filter.status = statusFilter;
    } else if (tab === "overdue") {
      filter.overdue = true;
    } else if (tab === "concluded") {
      filter.status = "Concluded";
    } else if (tab === "excursions") {
      filter.kind = "Excursion";
      if (statusFilter) filter.status = statusFilter;
    }

    if (sectionId !== "") filter.sectionId = sectionId;
    if (fromDate) filter.from = fromDate;
    if (toDate) filter.to = toDate;

    oosInvestigationService
      .list(filter, controller.signal)
      .then((res) => {
        setRows(res.items);
        setTotalCount(res.totalCount);
        setLoading(false);
      })
      .catch((e: unknown) => {
        if (controller.signal.aborted) return;
        setError(serverMessage(e) ?? t("page.loadError"));
        setLoading(false);
      });

    return () => controller.abort();
  }, [tab, sectionId, statusFilter, fromDate, toDate, page, pageSize, t]);

  const handleTabChange = (nextTab: RegisterTab) => {
    setTab(nextTab);
    setPage(0);
    setStatusFilter("");
  };

  const columns: RegisterColumn<OosInvestigationListItemDto>[] = [
    {
      key: "number",
      label: t("register.columns.number"),
      nowrap: true,
      render: (row) => (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
          <Typography
            variant="body2"
            sx={{ fontFamily: monospaceFontFamily, fontWeight: 700 }}
          >
            {row.number}
          </Typography>
          {row.isOverdue && (
            <Chip
              size="small"
              color="error"
              variant="outlined"
              label={t("register.overdueChip")}
            />
          )}
        </Stack>
      )
    },
    {
      key: "kind",
      label: t("register.columns.kind"),
      render: (row) => <StatusBadge status={row.kind} />
    },
    {
      key: "status",
      label: t("register.columns.status"),
      render: (row) => <StatusBadge status={row.status} />
    },
    {
      key: "sampleReferenceNumber",
      label: t("register.columns.sample"),
      render: (row) => (
        <Box>
          <Typography
            variant="body2"
            sx={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}
          >
            {row.sampleReferenceNumber}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {row.sampleDisplayName}
          </Typography>
        </Box>
      )
    },
    {
      key: "sectionName",
      label: t("register.columns.section")
    },
    {
      key: "testCodes",
      label: t("register.columns.tests"),
      render: (row) => (
        <Stack direction="row" spacing={0.5} sx={{ flexWrap: "wrap" }}>
          {row.testCodes.map((code) => (
            <Chip
              key={code}
              size="small"
              label={code}
              sx={{ fontFamily: monospaceFontFamily }}
            />
          ))}
        </Stack>
      )
    },
    {
      key: "openedAtUtc",
      label: t("register.columns.opened"),
      numeric: true,
      render: (row) => (
        <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
          {formatDateTimeWithTimeZone(row.openedAtUtc)}
        </Typography>
      )
    },
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
      key: "conclusion",
      label: t("register.columns.conclusion"),
      render: (row) =>
        row.conclusion ? <StatusBadge status={row.conclusion} /> : "—"
    }
  ];

  return (
    <Box>
      <PageHeader title={t("page.title")} subtitle={t("page.subtitle")} />

      <Tabs
        value={tab}
        onChange={(_, val: RegisterTab) => handleTabChange(val)}
        sx={{ mb: 2, borderBottom: 1, borderColor: "divider" }}
      >
        <Tab value="open" label={t("register.tabs.open")} />
        <Tab value="overdue" label={t("register.tabs.overdue")} />
        <Tab value="concluded" label={t("register.tabs.concluded")} />
        <Tab value="excursions" label={t("register.tabs.excursions")} />
        {canManage && <Tab value="legacy" label={t("register.tabs.legacy")} />}
      </Tabs>

      {tab === "legacy" && canManage ? (
        <LegacyGroupsView />
      ) : (
        <Stack spacing={2}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={1.5}
            sx={{ alignItems: { sm: "center" }, flexWrap: "wrap" }}
          >
            <FormControl size="small" sx={{ minWidth: 180 }}>
              <InputLabel id="oos-filter-section">
                {t("register.filters.section")}
              </InputLabel>
              <Select
                labelId="oos-filter-section"
                label={t("register.filters.section")}
                value={sectionId}
                onChange={(e) => {
                  const raw = e.target.value as number | "";
                  setSectionId(raw === "" ? "" : Number(raw));
                  setPage(0);
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

            {(tab === "open" || tab === "excursions") && (
              <FormControl size="small" sx={{ minWidth: 180 }}>
                <InputLabel id="oos-filter-status">
                  {t("register.filters.status")}
                </InputLabel>
                <Select
                  labelId="oos-filter-status"
                  label={t("register.filters.status")}
                  value={statusFilter}
                  onChange={(e) => {
                    setStatusFilter(
                      e.target.value as OosInvestigationStatus | ""
                    );
                    setPage(0);
                  }}
                >
                  <MenuItem value="">{t("register.filters.allStatuses")}</MenuItem>
                  <MenuItem value="PhaseOne">
                    {t("statuses.PhaseOne", { ns: "common" })}
                  </MenuItem>
                  <MenuItem value="AwaitingRetest">
                    {t("statuses.AwaitingRetest", { ns: "common" })}
                  </MenuItem>
                  <MenuItem value="PhaseTwo">
                    {t("statuses.PhaseTwo", { ns: "common" })}
                  </MenuItem>
                </Select>
              </FormControl>
            )}

            <TextField
              size="small"
              type="date"
              label={t("register.filters.from")}
              slotProps={{ inputLabel: { shrink: true } }}
              value={fromDate}
              onChange={(e) => {
                setFromDate(e.target.value);
                setPage(0);
              }}
            />
            <TextField
              size="small"
              type="date"
              label={t("register.filters.to")}
              slotProps={{ inputLabel: { shrink: true } }}
              value={toDate}
              onChange={(e) => {
                setToDate(e.target.value);
                setPage(0);
              }}
            />

            {(sectionId !== "" || statusFilter !== "" || fromDate || toDate) && (
              <Button
                size="small"
                onClick={() => {
                  setSectionId("");
                  setStatusFilter("");
                  setFromDate("");
                  setToDate("");
                  setPage(0);
                }}
              >
                {t("register.filters.clear")}
              </Button>
            )}
          </Stack>

          {error && <Alert severity="error">{error}</Alert>}

          <RegisterTable
            tableKey="oosTracking.register"
            columns={columns}
            rows={rows}
            getRowId={(r) => r.id}
            loading={loading}
            onRowClick={(row) => navigate(`/oos-tracking/${row.id}`)}
            empty={{
              title: t("register.emptyTitle"),
              description: t("register.emptyDescription")
            }}
            serverPaged={{
              totalCount,
              page,
              pageSize,
              onPageChange: setPage,
              onPageSizeChange: (next) => {
                setPageSize(next);
                setPage(0);
              }
            }}
            trailingCell={{
              label: t("register.columns.actions"),
              render: (row) => (
                <Button
                  size="small"
                  variant="outlined"
                  onClick={(e) => {
                    e.stopPropagation();
                    navigate(`/oos-tracking/${row.id}`);
                  }}
                >
                  {t("register.openDetail")}
                </Button>
              )
            }}
          />
        </Stack>
      )}
    </Box>
  );
}
