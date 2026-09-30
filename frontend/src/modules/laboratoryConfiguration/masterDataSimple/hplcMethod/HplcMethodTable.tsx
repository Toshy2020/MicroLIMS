import type { ReactNode } from "react";
import { Chip, Typography } from "@mui/material";
import { RegisterTable, RegisterColumn } from "../../../../components/lab";
import { StatusBadge } from "../../../../components/StatusBadge";
import { HplcMethodListItem } from "../services/HplcMethodService";

export interface HplcMethodTableProps {
  methods: HplcMethodListItem[];
  loading: boolean;
  searchQuery: string;
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
        <Chip label={m.abbreviation} size="small" color="primary" variant="outlined" sx={{ fontSize: 11, fontWeight: 700 }} />
      )
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
          sx={{ fontSize: 11 }}
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

  const filtersActive = Boolean(searchQuery.trim()) || statusFilter !== "ALL" || sectionFilter !== "ALL";

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
          ? { title: "No HPLC methods found", description: "Try adjusting your search or filters." }
          : {
              title: "No HPLC methods found",
              description: "Register your first HPLC analytical method master.",
              action: emptyAction
            }
      }
    />
  );
}
