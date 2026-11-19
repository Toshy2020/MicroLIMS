import type { ReactNode } from "react";
import { Chip, Typography, Stack } from "@mui/material";
import { RegisterTable, RegisterColumn } from "../../../../components/lab";
import { StatusBadge } from "../../../../components/StatusBadge";
import { HplcMethodListItem } from "../services/HplcMethodService";
import { formatDateTimeWithTimeZone } from "../../../../utils/formatDate";
import { useTranslation } from "../../../../i18n";

export interface HplcMethodTableProps {
  methods: HplcMethodListItem[];
  loading: boolean;
  searchQuery: string;
  techniqueFilter?: string;
  statusFilter: string;
  sectionFilter: string;
  resolveSectionDisplay: (sectionName: string) => string;
  onViewHistory: (method: HplcMethodListItem) => void;
  onEdit: (method: HplcMethodListItem) => void;
  onToggleActive: (method: HplcMethodListItem) => void;
  // Primary action shown on the empty register (e.g. Add Method).
  emptyAction?: ReactNode;
}

export function HplcMethodTable({
  methods,
  loading,
  searchQuery,
  techniqueFilter = "ALL",
  statusFilter,
  sectionFilter,
  resolveSectionDisplay,
  onViewHistory,
  onEdit,
  onToggleActive,
  emptyAction
}: HplcMethodTableProps) {
  const { t } = useTranslation("masterData");
  const columns: RegisterColumn<HplcMethodListItem>[] = [
    {
      key: "name",
      label: t("hplcMethod.table.columns.name"),
      sortable: true,
      render: (m) => <Typography component="span" sx={{ fontWeight: 600, fontSize: "0.875rem" }}>{m.name}</Typography>
    },
    {
      key: "abbreviation",
      label: t("hplcMethod.table.columns.abbreviation"),
      sortable: true,
      render: (m) => (
        <Chip label={m.abbreviation} size="small" color="primary" variant="outlined" sx={{ fontSize: 12, fontWeight: 700 }} />
      )
    },
    {
      key: "technique",
      label: t("hplcMethod.table.columns.technique"),
      sortable: true,
      sortValue: (m) => m.technique ?? "Hplc",
      render: (m) => {
        const isGc = m.technique === "Gc";
        const isResidual = isGc && m.resultMode === "ResidualSolvents";
        const isRelatedSubstances = !isGc && m.resultMode === "RelatedSubstances";
        const isCleaningResidue = !isGc && m.resultMode === "CleaningResidue";
        return (
          <Stack direction="row" spacing={0.5} sx={{ alignItems: "center" }}>
            <Chip
              label={isGc ? "GC" : "HPLC"}
              size="small"
              color={isGc ? "secondary" : "default"}
              variant="outlined"
              sx={{ fontSize: 12, fontWeight: 700 }}
            />
            {isResidual && (
              <Chip
                label={t("hplcMethod.table.chips.residualSolvents")}
                size="small"
                color="warning"
                variant="outlined"
                sx={{ fontSize: 12, fontWeight: 600 }}
              />
            )}
            {isRelatedSubstances && (
              <Chip
                label={t("hplcMethod.table.chips.relatedSubstances")}
                size="small"
                color="info"
                variant="outlined"
                sx={{ fontSize: 12, fontWeight: 600 }}
              />
            )}
            {isCleaningResidue && (
              <Chip
                label={t("hplcMethod.table.chips.cleaningResidue")}
                size="small"
                color="info"
                variant="outlined"
                sx={{ fontSize: 12, fontWeight: 600 }}
              />
            )}
          </Stack>
        );
      }
    },
    {
      key: "analyteCount",
      label: t("hplcMethod.table.columns.analytes"),
      sortable: true,
      render: (m) => (
        <Chip
          label={t("hplcMethod.table.chips.analyteCount", { count: m.analyteCount })}
          size="small"
          variant="outlined"
          sx={{ fontSize: 12 }}
        />
      )
    },
    {
      key: "sectionName",
      label: t("hplcMethod.table.columns.section"),
      sortable: true,
      render: (m) => <Chip label={resolveSectionDisplay(m.sectionName)} size="small" variant="outlined" sx={{ fontSize: 12 }} />
    },
    {
      key: "isActive",
      label: t("hplcMethod.table.columns.status"),
      sortable: true,
      sortValue: (m) => (m.isActive ? 0 : 1),
      render: (m) => <StatusBadge status={m.isActive ? "Active" : "Inactive"} />
    },
    {
      key: "lastModifiedAt",
      label: t("hplcMethod.table.columns.lastModified"),
      sortable: true,
      sortValue: (m) => new Date(m.lastModifiedAt).getTime(),
      render: (m) => (
        <Typography component="span" sx={{ fontSize: 12, color: "text.secondary" }}>
          {formatDateTimeWithTimeZone(m.lastModifiedAt)}
        </Typography>
      )
    }
  ];

  const filtersActive =
    Boolean(searchQuery.trim()) ||
    techniqueFilter !== "ALL" ||
    statusFilter !== "ALL" ||
    sectionFilter !== "ALL";

  return (
    <RegisterTable
      columns={columns}
      rows={methods}
      getRowId={(m) => m.id}
      loading={loading}
      onRowClick={onEdit}
      rowActions={(m) => [
        { label: t("hplcMethod.table.actions.edit"), onClick: () => onEdit(m) },
        { label: t("hplcMethod.table.actions.viewHistory"), onClick: () => onViewHistory(m) },
        m.isActive
          ? { label: t("hplcMethod.table.actions.deactivate"), onClick: () => onToggleActive(m), danger: true }
          : { label: t("hplcMethod.table.actions.activate"), onClick: () => onToggleActive(m) }
      ]}
      empty={
        filtersActive
          ? { title: t("hplcMethod.table.emptyTitle"), description: t("hplcMethod.table.emptyFiltered") }
          : {
              title: t("hplcMethod.table.emptyTitle"),
              description: t("hplcMethod.table.emptyUnfiltered"),
              action: emptyAction
            }
      }
    />
  );
}
