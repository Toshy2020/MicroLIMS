import { useMemo } from "react";
import { Select, MenuItem, FormControl, InputLabel, Button } from "@mui/material";
import RotateLeftIcon from "@mui/icons-material/RotateLeft";
import { EquipmentItem, EquipmentFilterState } from "../types/equipmentTypes";
import { FilterBar } from "../../../../components/lab";

interface EquipmentFilterBarProps {
  items: EquipmentItem[];
  filters: EquipmentFilterState;
  onFilterChange: (newFilters: EquipmentFilterState) => void;
  onReset: () => void;
  // True when a filter outside this bar (the KPI shortcut) is active, so Reset stays reachable.
  extraActive?: boolean;
  resultCount?: number;
  onRefresh?: () => void;
  refreshing?: boolean;
}

export function EquipmentFilterBar({ items, filters, onFilterChange, onReset, extraActive, resultCount, onRefresh, refreshing }: EquipmentFilterBarProps) {
  // Extract unique instrument types and locations from current dataset
  const instrumentTypes = useMemo(() => {
    const set = new Set<string>();
    items.forEach((i) => {
      if (i.instrumentType?.trim()) set.add(i.instrumentType.trim());
    });
    return Array.from(set).sort();
  }, [items]);

  const locations = useMemo(() => {
    const set = new Set<string>();
    items.forEach((i) => {
      if (i.location?.trim()) set.add(i.location.trim());
    });
    return Array.from(set).sort();
  }, [items]);

  const updateField = (field: keyof EquipmentFilterState, value: string) => {
    onFilterChange({ ...filters, [field]: value });
  };

  const hasActiveFilters = Object.values(filters).some((v) => v !== "");

  return (
    <FilterBar
      search={filters.search}
      onSearch={(value) => updateField("search", value)}
      placeholder="Search by type, manufacturer, serial no., code, location..."
      resultCount={resultCount}
      onRefresh={onRefresh}
      refreshing={refreshing}
    >
      <FormControl size="small" sx={{ minWidth: 160 }}>
        <InputLabel id="equip-type-filter-label">Instrument Type</InputLabel>
        <Select
          labelId="equip-type-filter-label"
          label="Instrument Type"
          value={filters.instrumentType}
          onChange={(e) => updateField("instrumentType", e.target.value)}
        >
          <MenuItem value="">
            <em>All Types</em>
          </MenuItem>
          {instrumentTypes.map((type) => (
            <MenuItem key={type} value={type}>
              {type}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <FormControl size="small" sx={{ minWidth: 140 }}>
        <InputLabel id="equip-status-filter-label">Status</InputLabel>
        <Select
          labelId="equip-status-filter-label"
          label="Status"
          value={filters.status}
          onChange={(e) => updateField("status", e.target.value)}
        >
          <MenuItem value="">
            <em>All Statuses</em>
          </MenuItem>
          <MenuItem value="InService">In Service</MenuItem>
          <MenuItem value="OutOfService">Out of Service</MenuItem>
          <MenuItem value="Retired">Retired</MenuItem>
        </Select>
      </FormControl>

      <FormControl size="small" sx={{ minWidth: 140 }}>
        <InputLabel id="equip-loc-filter-label">Location</InputLabel>
        <Select
          labelId="equip-loc-filter-label"
          label="Location"
          value={filters.location}
          onChange={(e) => updateField("location", e.target.value)}
        >
          <MenuItem value="">
            <em>All Locations</em>
          </MenuItem>
          {locations.map((loc) => (
            <MenuItem key={loc} value={loc}>
              {loc}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <FormControl size="small" sx={{ minWidth: 160 }}>
        <InputLabel id="equip-calib-filter-label">Calibration Due</InputLabel>
        <Select
          labelId="equip-calib-filter-label"
          label="Calibration Due"
          value={filters.calibrationRange}
          onChange={(e) => updateField("calibrationRange", e.target.value)}
        >
          <MenuItem value="">
            <em>All Calibration</em>
          </MenuItem>
          <MenuItem value="overdue">Calibration Overdue</MenuItem>
          <MenuItem value="due_30">Due in 30 Days</MenuItem>
          <MenuItem value="due_60">Due in 60 Days</MenuItem>
          <MenuItem value="valid">Valid / Not Overdue</MenuItem>
        </Select>
      </FormControl>

      {(hasActiveFilters || extraActive) && (
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
