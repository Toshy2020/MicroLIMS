import type { ReactNode } from "react";
import { Chip, Typography } from "@mui/material";
import { RegisterTable, RegisterColumn } from "../../../../components/lab";
import { StatusBadge } from "../../../../components/StatusBadge";
import type { IcpMethodListItem } from "../services/IcpMethodService";

export interface IcpMethodTableProps {
  methods: IcpMethodListItem[];
  loading: boolean;
  searchQuery: string;
  modeFilter: string;
  statusFilter: string;
  sectionFilter: string;
  resolveSectionDisplay: (sectionName: string) => string;
  onViewHistory: (method: IcpMethodListItem) => void;
  onEdit: (method: IcpMethodListItem) => void;
  onToggleActive: (method: IcpMethodListItem) => void;
  emptyAction?: ReactNode;
}

export function IcpMethodTable({
  methods,
  loading,
  searchQuery,
  modeFilter,
  statusFilter,
  sectionFilter,
  resolveSectionDisplay,
  onViewHistory,
  onEdit,
  onToggleActive,
  emptyAction
}: IcpMethodTableProps) {
  const columns: RegisterColumn<IcpMethodListItem>[] = [
    {
      key: "name",
      label: "Name",
      sortable: true,
      render: (m) => (
        <Typography component="span" sx={{ fontWeight: 600, fontSize: "0.875rem" }}>
          {m.name}
        </Typography>
      )
    },
    {
      key: "abbreviation",
      label: "Abbreviation",
      sortable: true,
      render: (m) => (
        <Chip
          label={m.abbreviation}
          size="small"
          color="primary"
          variant="outlined"
          sx={{ fontSize: 12, fontWeight: 700 }}
        />
      )
    },
    {
      key: "mode",
      label: "Mode",
      sortable: true,
      render: (m) => {
        const isMineral = m.mode === "MineralAssay";
        return (
          <Chip
            label={isMineral ? "Mineral assay" : "Elemental impurities"}
            size="small"
            color={isMineral ? "primary" : "secondary"}
            variant="outlined"
            sx={{ fontSize: 12, fontWeight: 600 }}
          />
        );
      }
    },
    {
      key: "elementCount",
      label: "Elements",
      sortable: true,
      render: (m) => (
        <Chip
          label={`${m.elementCount} ${m.elementCount === 1 ? "element" : "elements"}`}
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
      render: (m) => (
        <Chip
          label={resolveSectionDisplay(m.sectionName)}
          size="small"
          variant="outlined"
          sx={{ fontSize: 12 }}
        />
      )
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
    modeFilter !== "ALL" ||
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
          ? {
              title: "No ICP methods found",
              description: "Try adjusting your search or filters."
            }
          : {
              title: "No ICP methods found",
              description: "Register your first ICP optical emission spectrometry method master.",
              action: emptyAction
            }
      }
    />
  );
}
