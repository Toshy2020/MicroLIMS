import { Select, MenuItem, Button } from "@mui/material";
import FilterAltOffOutlinedIcon from "@mui/icons-material/FilterAltOffOutlined";
import { FilterBar } from "../../../../components/lab";

interface Props {
  search: string;
  onSearchChange: (v: string) => void;
  selectedMaterialId: string;
  onMaterialChange: (v: string) => void;
  selectedStatus: string;
  onStatusChange: (v: string) => void;
  materials: any[];
  onResetFilters: () => void;
  resultCount?: number;
  onRefresh?: () => void;
  refreshing?: boolean;
}

const STATUS_OPTIONS = [
  { value: "Pending Evaluation", label: "Pending Evaluation" },
  { value: "Awaiting Approval", label: "Awaiting Approval" },
  { value: "Released", label: "Released" },
  { value: "Rejected", label: "Rejected" },
  { value: "Out of Stock", label: "Out of Stock" }
];

export function MediaLotFilterBar({
  search,
  onSearchChange,
  selectedMaterialId,
  onMaterialChange,
  selectedStatus,
  onStatusChange,
  materials,
  onResetFilters,
  resultCount,
  onRefresh,
  refreshing
}: Props) {
  const hasActiveFilters = Boolean(search || selectedMaterialId || selectedStatus);

  return (
    <FilterBar
      search={search}
      onSearch={onSearchChange}
      placeholder="Search by lot number, dehydrated material, batch…"
      resultCount={resultCount}
      onRefresh={onRefresh}
      refreshing={refreshing}
    >
      <Select
        size="small"
        displayEmpty
        value={selectedMaterialId}
        onChange={(e) => onMaterialChange(e.target.value)}
        sx={{ minWidth: 180 }}
        inputProps={{ "aria-label": "Material" }}
      >
        <MenuItem value="">
          <em>All Materials</em>
        </MenuItem>
        {materials.map((m) => (
          <MenuItem key={m.id} value={String(m.id)}>
            {m.materialName}
          </MenuItem>
        ))}
      </Select>

      <Select
        size="small"
        displayEmpty
        value={selectedStatus}
        onChange={(e) => onStatusChange(e.target.value)}
        sx={{ minWidth: 180 }}
        inputProps={{ "aria-label": "Status" }}
      >
        <MenuItem value="">
          <em>All Statuses</em>
        </MenuItem>
        {STATUS_OPTIONS.map((s) => (
          <MenuItem key={s.value} value={s.value}>
            {s.label}
          </MenuItem>
        ))}
      </Select>

      {hasActiveFilters && (
        <Button
          size="small"
          variant="outlined"
          onClick={onResetFilters}
          startIcon={<FilterAltOffOutlinedIcon fontSize="small" />}
          sx={{ borderColor: "divider", color: "text.secondary" }}
        >
          Reset Filters
        </Button>
      )}
    </FilterBar>
  );
}
