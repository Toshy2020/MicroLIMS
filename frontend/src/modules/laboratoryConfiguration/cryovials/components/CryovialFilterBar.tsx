import { useMemo } from "react";
import { useTranslation } from "../../../../i18n";
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
  const { t } = useTranslation("labConfig");
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
  const organismSelectValue = organisms.includes(filters.organism) ? filters.organism : "";

  return (
    <FilterBar
      search={filters.search}
      onSearch={(value) => updateField("search", value)}
      placeholder={t("cryovials.filters.searchPlaceholder")}
      resultCount={resultCount}
      onRefresh={onRefresh}
      refreshing={refreshing}
    >
      <FormControl size="small" sx={{ minWidth: 170 }}>
        <InputLabel id="cryovial-organism-filter-label">{t("cryovials.filters.organism")}</InputLabel>
        <Select
          labelId="cryovial-organism-filter-label"
          label={t("cryovials.filters.organism")}
          value={organismSelectValue}
          onChange={(e) => updateField("organism", e.target.value)}
        >
          <MenuItem value="">
            <em>{t("cryovials.filters.allOrganisms")}</em>
          </MenuItem>
          {organisms.map((org) => (
            <MenuItem key={org} value={org}>
              {org}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <FormControl size="small" sx={{ minWidth: 150 }}>
        <InputLabel id="cryovial-status-filter-label">{t("cryovials.filters.status")}</InputLabel>
        <Select
          labelId="cryovial-status-filter-label"
          label={t("cryovials.filters.status")}
          value={filters.status}
          onChange={(e) => updateField("status", e.target.value)}
        >
          <MenuItem value="">
            <em>{t("cryovials.filters.allStatuses")}</em>
          </MenuItem>
          <MenuItem value="Approved">{t("cryovials.filters.statuses.Approved")}</MenuItem>
          <MenuItem value="PendingReview">{t("cryovials.filters.statuses.PendingReview")}</MenuItem>
          <MenuItem value="Rejected">{t("cryovials.filters.statuses.Rejected")}</MenuItem>
          <MenuItem value="Destroyed">{t("cryovials.filters.statuses.Destroyed")}</MenuItem>
          <MenuItem value="Depleted">{t("cryovials.filters.statuses.Depleted")}</MenuItem>
        </Select>
      </FormControl>

      <FormControl size="small" sx={{ minWidth: 160 }}>
        <InputLabel id="cryovial-expiry-filter-label">{t("cryovials.filters.expiry")}</InputLabel>
        <Select
          labelId="cryovial-expiry-filter-label"
          label={t("cryovials.filters.expiry")}
          value={filters.expiryRange}
          onChange={(e) => updateField("expiryRange", e.target.value)}
        >
          <MenuItem value="">
            <em>{t("cryovials.filters.allExpiry")}</em>
          </MenuItem>
          <MenuItem value="expiring_30">{t("cryovials.filters.expiryOptions.expiring_30")}</MenuItem>
          <MenuItem value="expired">{t("cryovials.filters.expiryOptions.expired")}</MenuItem>
          <MenuItem value="valid">{t("cryovials.filters.expiryOptions.valid")}</MenuItem>
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
          {t("cryovials.filters.reset")}
        </Button>
      )}
    </FilterBar>
  );
}
