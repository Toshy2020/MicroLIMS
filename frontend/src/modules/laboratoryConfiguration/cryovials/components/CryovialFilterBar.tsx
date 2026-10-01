import { useMemo } from "react";
import { Select, MenuItem, FormControl, InputLabel, Button } from "@mui/material";
import RotateLeftIcon from "@mui/icons-material/RotateLeft";
import { CryovialItem, CryovialFilterState } from "../types/cryovialTypes";
import { FilterBar } from "../../../../components/lab";

interface CryovialFilterBarProps {
  items: CryovialItem[];
  filters: CryovialFilterState;
  onFilterChange: (newFilters: CryovialFilterState) => void;
  onReset: () => void;
  resultCount?: number;
  onRefresh?: () => void;
  refreshing?: boolean;
}

export function CryovialFilterBar({ items, filters, onFilterChange, onReset, resultCount, onRefresh, refreshing }: CryovialFilterBarProps) {
  // Extract unique organisms from current dataset
  const organisms = useMemo(() => {
    const set = new Set<string>();
    items.forEach((c) => {
      const name = c.organism?.scientificName ?? c.organismNameSnapshot;
      if (name?.trim()) set.add(name.trim());
    });
    return Array.from(set).sort();
  }, [items]);

  const updateField = (field: keyof CryovialFilterState, value: string) => {
    onFilterChange({ ...filters, [field]: value });
  };

  const hasActiveFilters = Object.values(filters).some((v) => v !== "");

  return (
    <FilterBar
      search={filters.search}
      onSearch={(value) => updateField("search", value)}
      placeholder="Search by code, organism, material..."
      resultCount={resultCount}
      onRefresh={onRefresh}
      refreshing={refreshing}
    >
      <FormControl size="small" sx={{ minWidth: 170 }}>
        <InputLabel id="cryovial-organism-filter-label">Organism</InputLabel>
        <Select
          labelId="cryovial-organism-filter-label"
          label="Organism"
          value={filters.organism}
          onChange={(e) => updateField("organism", e.target.value)}
        >
          <MenuItem value="">
            <em>All Organisms</em>
          </MenuItem>
          {organisms.map((org) => (
            <MenuItem key={org} value={org}>
              {org}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <FormControl size="small" sx={{ minWidth: 150 }}>
        <InputLabel id="cryovial-status-filter-label">Status</InputLabel>
        <Select
          labelId="cryovial-status-filter-label"
          label="Status"
          value={filters.status}
          onChange={(e) => updateField("status", e.target.value)}
        >
          <MenuItem value="">
            <em>All Statuses</em>
          </MenuItem>
          <MenuItem value="Approved">Approved</MenuItem>
          <MenuItem value="PendingReview">Pending Review</MenuItem>
          <MenuItem value="Rejected">Rejected</MenuItem>
          <MenuItem value="Destroyed">Destroyed</MenuItem>
          <MenuItem value="Depleted">Depleted (0 Vials)</MenuItem>
        </Select>
      </FormControl>

      <FormControl size="small" sx={{ minWidth: 160 }}>
        <InputLabel id="cryovial-expiry-filter-label">Expiry</InputLabel>
        <Select
          labelId="cryovial-expiry-filter-label"
          label="Expiry"
          value={filters.expiryRange}
          onChange={(e) => updateField("expiryRange", e.target.value)}
        >
          <MenuItem value="">
            <em>All Expiry</em>
          </MenuItem>
          <MenuItem value="expiring_30">Expiring in 30 Days</MenuItem>
          <MenuItem value="expired">Expired</MenuItem>
          <MenuItem value="valid">Valid / Unexpired</MenuItem>
        </Select>
      </FormControl>

      {hasActiveFilters && (
        <Button
          size="small"
          variant="outlined"
          onClick={onReset}
          startIcon={<RotateLeftIcon fontSize="small" />}
          sx={{ borderColor: "divider", color: "text.secondary" }}
        >
          Reset
        </Button>
      )}
    </FilterBar>
  );
}
