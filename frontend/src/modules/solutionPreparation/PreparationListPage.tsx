import { useState, useEffect, useMemo, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { Button, Chip, FormControl, InputLabel, Select, MenuItem, Alert, Stack, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { LabPage, FilterBar, RegisterTable } from "../../components/lab";
import type { RegisterColumn } from "../../components/lab";
import { Ltr } from "../../components/Ltr";
import { StatusBadge } from "../../components/StatusBadge";
import { useTranslation } from "../../i18n";
import { monospaceFontFamily } from "../../theme/palette";
import { getErrorMessage } from "../../utils/errorMessage";
import { SolutionPreparationService } from "./services/SolutionPreparationService";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import type {
  SolutionType,
  SolutionPreparationStatus,
  SolutionPreparationListItem
} from "./types";
import { solutionTypeLabel } from "./types";

export function PreparationListPage() {
  const { t } = useTranslation("solutionPreparation");
  const navigate = useNavigate();

  const [items, setItems] = useState<SolutionPreparationListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState<"ALL" | SolutionPreparationStatus>("ALL");
  const [typeFilter, setTypeFilter] = useState<"ALL" | SolutionType>("ALL");
  const [releaseFilter, setReleaseFilter] = useState<"ALL" | "AWAITING">("ALL");

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await SolutionPreparationService.getAll(
        statusFilter === "ALL" ? undefined : statusFilter,
        typeFilter === "ALL" ? undefined : typeFilter
      );
      setItems(data);
    } catch (err: unknown) {
      setError(getErrorMessage(err, t("list.loadError")));
    } finally {
      setLoading(false);
    }
  }, [statusFilter, typeFilter, t]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Client-side search filtering
  const filteredItems = useMemo(() => {
    const base = releaseFilter === "AWAITING" ? items.filter((i) => i.approvalStatus === "PendingReview") : items;
    if (!searchQuery.trim()) return base;
    const q = searchQuery.toLowerCase().trim();
    return base.filter((item) => {
      const codeMatch = item.code?.toLowerCase().includes(q) ?? false;
      const nameMatch = item.solutionMasterName.toLowerCase().includes(q);
      const methodMatch = item.hplcMethodAbbreviation?.toLowerCase().includes(q) ?? false;
      const preparerMatch = item.preparedByUserName?.toLowerCase().includes(q) ?? false;
      return codeMatch || nameMatch || methodMatch || preparerMatch;
    });
  }, [items, searchQuery, releaseFilter]);

  const openRecord = (item: SolutionPreparationListItem) =>
    navigate(item.effectiveStatus === "InProgress" ? `/preparation/${item.id}/edit` : `/preparation/${item.id}`);

  const columns: RegisterColumn<SolutionPreparationListItem>[] = [
    {
      key: "code",
      label: t("list.columns.code"),
      sortable: true,
      sortValue: (i) => i.code ?? "",
      render: (i) => i.code ? (
        <Typography variant="body2" sx={{ fontFamily: monospaceFontFamily, fontWeight: 700 }}><Ltr>{i.code}</Ltr></Typography>
      ) : (
        <Typography variant="caption" sx={{ color: "text.secondary", fontStyle: "italic" }}>{t("list.inProgressCode")}</Typography>
      )
    },
    { key: "type", label: t("list.columns.type"), sortable: true, render: (i) => solutionTypeLabel(i.type, t) },
    {
      key: "solutionMasterName",
      label: t("list.columns.recipe"),
      sortable: true,
      render: (i) => <Typography variant="body2" sx={{ fontWeight: 600 }}>{i.solutionMasterName}</Typography>
    },
    { key: "hplcMethodAbbreviation", label: t("list.columns.method"), sortable: true, render: (i) => i.hplcMethodAbbreviation ? <Ltr>{i.hplcMethodAbbreviation}</Ltr> : "—" },
    {
      key: "effectiveStatus",
      label: t("list.columns.status"),
      sortable: true,
      render: (i) => (
        <Stack direction="row" spacing={0.75} sx={{ alignItems: "center", flexWrap: "wrap" }} useFlexGap>
          <StatusBadge
            status={i.effectiveStatus}
            label={t(`statuses.${i.effectiveStatus}`, { defaultValue: i.effectiveStatus })}
          />
          {i.approvalStatus === "PendingReview" && (
            <Chip label={t("approval.PendingReview")} size="small" color="warning" variant="outlined" sx={{ fontSize: 11, height: 22 }} />
          )}
          {i.approvalStatus === "Rejected" && (
            <Chip label={t("approval.Rejected")} size="small" color="error" variant="outlined" sx={{ fontSize: 11, height: 22 }} />
          )}
        </Stack>
      )
    },
    {
      key: "preparedAt",
      label: t("list.columns.preparedAt"),
      sortable: true,
      sortValue: (i) => i.preparedAt ?? "",
      render: (i) => (i.preparedAt ? <Ltr>{formatDateTimeWithTimeZone(i.preparedAt)}</Ltr> : "—")
    },
    {
      key: "expiresAt",
      label: t("list.columns.expiresAt"),
      sortable: true,
      sortValue: (i) => i.expiresAt ?? "",
      render: (i) => (i.expiresAt ? <Ltr>{formatDateTimeWithTimeZone(i.expiresAt)}</Ltr> : "—")
    },
    {
      key: "preparedByUserName",
      label: t("list.columns.preparedBy"),
      sortable: true,
      sortValue: (i) => i.preparedByUserName ?? "",
      render: (i) => i.preparedByUserName || "—"
    }
  ];

  return (
    <LabPage
      title={t("list.title")}
      subtitle={t("list.subtitle")}
      actions={
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate("/preparation/new")} sx={{ textTransform: "none", fontWeight: 700 }}>
          {t("list.startPreparation")}
        </Button>
      }
      filters={
        <FilterBar
          search={searchQuery}
          onSearch={setSearchQuery}
          placeholder={t("list.searchPlaceholder")}
          resultCount={filteredItems.length}
          onRefresh={loadData}
          refreshing={loading}
        >
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="status-filter-label">{t("list.statusLabel")}</InputLabel>
            <Select
              labelId="status-filter-label"
              value={statusFilter}
              label={t("list.statusLabel")}
              onChange={(e) => setStatusFilter(e.target.value as "ALL" | SolutionPreparationStatus)}
            >
              <MenuItem value="ALL">{t("list.allStatuses")}</MenuItem>
              <MenuItem value="InProgress">{t("statuses.InProgress")}</MenuItem>
              <MenuItem value="Prepared">{t("statuses.Prepared")}</MenuItem>
              <MenuItem value="Expired">{t("statuses.Expired")}</MenuItem>
              <MenuItem value="Discarded">{t("statuses.Discarded")}</MenuItem>
              <MenuItem value="Cancelled">{t("statuses.Cancelled")}</MenuItem>
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="type-filter-label">{t("list.typeLabel")}</InputLabel>
            <Select
              labelId="type-filter-label"
              value={typeFilter}
              label={t("list.typeLabel")}
              onChange={(e) => setTypeFilter(e.target.value as "ALL" | SolutionType)}
            >
              <MenuItem value="ALL">{t("list.allTypes")}</MenuItem>
              <MenuItem value="MobilePhase">{t("solutionTypes.MobilePhase")}</MenuItem>
              <MenuItem value="Diluent">{t("solutionTypes.Diluent")}</MenuItem>
              <MenuItem value="Reagent">{t("solutionTypes.Reagent")}</MenuItem>
              <MenuItem value="Neutralizer">{t("solutionTypes.Neutralizer")}</MenuItem>
              <MenuItem value="Titrant">{t("solutionTypes.Titrant")}</MenuItem>
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 170 }}>
            <InputLabel id="release-filter-label">{t("list.releaseLabel")}</InputLabel>
            <Select
              labelId="release-filter-label"
              value={releaseFilter}
              label={t("list.releaseLabel")}
              onChange={(e) => setReleaseFilter(e.target.value as "ALL" | "AWAITING")}
            >
              <MenuItem value="ALL">{t("list.allReleases")}</MenuItem>
              <MenuItem value="AWAITING">{t("list.awaitingRelease")}</MenuItem>
            </Select>
          </FormControl>
        </FilterBar>
      }
    >
      {error && <Alert severity="error">{error}</Alert>}
      <RegisterTable
        columns={columns}
        rows={filteredItems}
        getRowId={(i) => i.id}
        loading={loading}
        onRowClick={openRecord}
        rowActions={(i) => [
          i.effectiveStatus === "InProgress"
            ? { label: t("list.resumePreparation"), onClick: () => navigate(`/preparation/${i.id}/edit`) }
            : { label: t("list.viewRecord"), onClick: () => navigate(`/preparation/${i.id}`) }
        ]}
        empty={{
          title: t("list.emptyTitle"),
          description: t("list.emptyDescription"),
          action: <Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate("/preparation/new")}>{t("list.startPreparation")}</Button>
        }}
      />
    </LabPage>
  );
}
