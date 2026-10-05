import type { ReactNode } from "react";
import { Chip, Typography, Stack } from "@mui/material";
import { RegisterTable, RegisterColumn } from "../../../../components/lab";
import { StatusBadge } from "../../../../components/StatusBadge";
import { HplcMethodListItem } from "../services/HplcMethodService";

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
  const columns: RegisterColumn<HplcMethodListItem>[] = [
    {
      key: "name",
      label: "Name",
      sortable: true,
      render: (m) => <Typography component="span" sx={{ fontWeight: 600, fontSize: "0.875rem" }}>{m.name}</Typography>
    },
    {
      key: "abbreviation",
      label: "Abbreviation",
      sortable: true,
      render: (m) => (
        <Chip label={m.abbreviation} size="small" color="primary" variant="outlined" sx={{ fontSize: 12, fontWeight: 700 }} />
      )
    },
    {
      key: "technique",
      label: "Technique",
      sortable: true,
      sortValue: (m) => m.technique ?? "Hplc",
      render: (m) => {
        const isGc = m.technique === "Gc";
        const isResidual = isGc && m.resultMode === "ResidualSolvents";
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
                label="Residual solvents"
                size="small"
                color="warning"
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
      label: "Analytes",
      sortable: true,
      render: (m) => (
        <Chip
          label={`${m.analyteCount} ${m.analyteCount === 1 ? "analyte" : "analytes"}`}
          size="small"
          variant="outlined"
          sx={{ fontSize: 12 }}
        />
      )
    },
    {
      key: "sectionName",
      label: "Section",
      sortable: true,
      render: (m) => <Chip label={resolveSectionDisplay(m.sectionName)} size="small" variant="outlined" sx={{ fontSize: 12 }} />
    },
    {
      key: "isActive",
      label: "Status",
      sortable: true,
      sortValue: (m) => (m.isActive ? 0 : 1),
      render: (m) => <StatusBadge status={m.isActive ? "Active" : "Inactive"} />
    },
    {
      key: "lastModifiedAt",
      label: "Last Modified",
      sortable: true,
      sortValue: (m) => new Date(m.lastModifiedAt).getTime(),
      render: (m) => (
        <Typography component="span" sx={{ fontSize: 12, color: "text.secondary" }}>
          {new Date(m.lastModifiedAt).toLocaleDateString()}
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
        { label: "Edit", onClick: () => onEdit(m) },
        { label: "View audit history", onClick: () => onViewHistory(m) },
        m.isActive
          ? { label: "Deactivate", onClick: () => onToggleActive(m), danger: true }
          : { label: "Activate", onClick: () => onToggleActive(m) }
      ]}
      empty={
        filtersActive
          ? { title: "No chromatography methods found", description: "Try adjusting your search or filters." }
          : {
              title: "No chromatography methods found",
              description: "Register your first chromatography analytical method master.",
              action: emptyAction
            }
      }
    />
  );
}
