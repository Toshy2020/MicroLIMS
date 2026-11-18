import { useMemo } from "react";
import { Select, MenuItem, FormControl, InputLabel, Button } from "@mui/material";
import RotateLeftIcon from "@mui/icons-material/RotateLeft";
import { EquipmentItem, EquipmentFilterState } from "../types/equipmentTypes";
import { FilterBar } from "../../../../components/lab";
import { useTranslation } from "../../../../i18n";

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
  const { t } = useTranslation("inventory");
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
      placeholder={t("equipment.filters.searchPlaceholder")}
      resultCount={resultCount}
      onRefresh={onRefresh}
      refreshing={refreshing}
    >
      <FormControl size="small" sx={{ minWidth: 160 }}>
        <InputLabel id="equip-type-filter-label">{t("equipment.filters.instrumentType")}</InputLabel>
        <Select
          labelId="equip-type-filter-label"
          label={t("equipment.filters.instrumentType")}
          value={filters.instrumentType}
          onChange={(e) => updateField("instrumentType", e.target.value)}
        >
          <MenuItem value="">
            <em>{t("equipment.filters.allTypes")}</em>
          </MenuItem>
          {instrumentTypes.map((type) => (
            <MenuItem key={type} value={type}>
              {type}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <FormControl size="small" sx={{ minWidth: 140 }}>
        <InputLabel id="equip-status-filter-label">{t("equipment.filters.status")}</InputLabel>
        <Select
          labelId="equip-status-filter-label"
          label={t("equipment.filters.status")}
          value={filters.status}
          onChange={(e) => updateField("status", e.target.value)}
        >
          <MenuItem value="">
            <em>{t("equipment.filters.allStatuses")}</em>
          </MenuItem>
          <MenuItem value="InService">{t("equipment.status.InService")}</MenuItem>
          <MenuItem value="OutOfService">{t("equipment.status.OutOfService")}</MenuItem>
          <MenuItem value="Retired">{t("equipment.status.Retired")}</MenuItem>
        </Select>
      </FormControl>

      <FormControl size="small" sx={{ minWidth: 140 }}>
        <InputLabel id="equip-loc-filter-label">{t("equipment.filters.location")}</InputLabel>
        <Select
          labelId="equip-loc-filter-label"
          label={t("equipment.filters.location")}
          value={filters.location}
          onChange={(e) => updateField("location", e.target.value)}
        >
          <MenuItem value="">
            <em>{t("equipment.filters.allLocations")}</em>
          </MenuItem>
          {locations.map((loc) => (
            <MenuItem key={loc} value={loc}>
              {loc}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <FormControl size="small" sx={{ minWidth: 160 }}>
        <InputLabel id="equip-calib-filter-label">{t("equipment.filters.calibrationDue")}</InputLabel>
        <Select
          labelId="equip-calib-filter-label"
          label={t("equipment.filters.calibrationDue")}
          value={filters.calibrationRange}
          onChange={(e) => updateField("calibrationRange", e.target.value)}
        >
          <MenuItem value="">
            <em>{t("equipment.filters.allCalibration")}</em>
          </MenuItem>
          <MenuItem value="overdue">{t("equipment.filters.overdue")}</MenuItem>
          <MenuItem value="due_30">{t("equipment.filters.due30")}</MenuItem>
          <MenuItem value="due_60">{t("equipment.filters.due60")}</MenuItem>
          <MenuItem value="valid">{t("equipment.filters.valid")}</MenuItem>
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
          {t("equipment.filters.reset")}
        </Button>
      )}
    </FilterBar>
  );
}
