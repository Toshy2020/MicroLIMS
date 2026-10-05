import { useEffect, useMemo, useState } from "react";
import { Select, MenuItem, FormControl, InputLabel, Button, Divider } from "@mui/material";
import RotateLeftIcon from "@mui/icons-material/RotateLeft";
import { MaterialItem, MaterialFilterState, MaterialType, MATERIAL_TYPE_LABELS } from "../types/materialTypes";
import { MaterialService } from "../services/MaterialService";
import { FilterBar } from "../../../../components/lab";

export const MATERIAL_TYPE_OPTIONS: { label: string; value: MaterialType }[] = [
  { label: "Dehydrated Media", value: "DehydratedMedia" },
  { label: "Lyophilized Microorganism", value: "LyophilizedMicroorganism" },
  { label: "Supplement", value: "Supplement" },
  { label: "Antibiotic Disc", value: "AntibioticDisc" },
  { label: "Identification Kit", value: "IdentificationKit" },
  { label: "Identification Reagent", value: "IdentificationReagent" },
  { label: "Chemical", value: "Chemical" },
  { label: "Indicator", value: "Indicator" },
  { label: "Reference Buffer", value: "ReferenceBuffer" },
  { label: "Disposable Tool", value: "DisposableTool" },
  { label: "Other", value: "Other" },
  { label: "Reference Standard", value: "ReferenceStandard" },
  { label: "Working Standard", value: "WorkingStandard" },
  { label: "Primary Standard", value: "PrimaryStandard" }
];

interface MaterialFilterBarProps {
  items: MaterialItem[];
  filters: MaterialFilterState;
  onFilterChange: (newFilters: MaterialFilterState) => void;
  onReset: () => void;
  // True when a filter outside this bar (the KPI shortcut) is active, so Reset stays reachable.
  extraActive?: boolean;
  resultCount?: number;
  onRefresh?: () => void;
  refreshing?: boolean;
  sectionId?: number;
}

export function MaterialFilterBar({ items, filters, onFilterChange, onReset, extraActive, resultCount, onRefresh, refreshing, sectionId }: MaterialFilterBarProps) {
  const [typeOptionsData, setTypeOptionsData] = useState<{
    builtIn: MaterialType[];
    custom: string[];
  } | null>(null);

  useEffect(() => {
    let canceled = false;
    MaterialService.getTypeOptions(sectionId)
      .then((data) => {
        if (!canceled && data) {
          setTypeOptionsData(data);
        }
      })
      .catch(() => {
        if (!canceled) {
          setTypeOptionsData(null);
        }
      });
    return () => {
      canceled = true;
    };
  }, [sectionId]);

  const typeOptions = useMemo(() => {
    if (typeOptionsData) {
      return typeOptionsData.builtIn.map((bt) => ({
        value: bt,
        label: MATERIAL_TYPE_LABELS[bt] ?? MATERIAL_TYPE_OPTIONS.find((o) => o.value === bt)?.label ?? bt
      }));
    }
    return MATERIAL_TYPE_OPTIONS;
  }, [typeOptionsData]);

  // Extract unique dynamic dropdown options from current dataset + section options
  const customTypes = useMemo(() => {
    const set = new Set<string>();
    if (typeOptionsData?.custom) {
      typeOptionsData.custom.forEach((ct) => {
        if (ct?.trim()) set.add(ct.trim());
      });
    }
    items.forEach((i) => {
      if (i.customType?.trim()) set.add(i.customType.trim());
    });
    return Array.from(set).sort((a, b) => a.localeCompare(b));
  }, [typeOptionsData, items]);

  const manufacturers = useMemo(() => {
    const set = new Set<string>();
    items.forEach((i) => {
      if (i.manufacturerName?.trim()) set.add(i.manufacturerName.trim());
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

  const updateField = (field: keyof MaterialFilterState, value: string) => {
    onFilterChange({ ...filters, [field]: value });
  };

  const hasActiveFilters = Object.values(filters).some((v) => v !== "");

  return (
    <FilterBar
      search={filters.search}
      onSearch={(value) => updateField("search", value)}
      placeholder="Search by name, lot no., code..."
      resultCount={resultCount}
      onRefresh={onRefresh}
      refreshing={refreshing}
    >
      <FormControl size="small" sx={{ minWidth: 150 }}>
        <InputLabel id="material-type-filter-label">Type</InputLabel>
        <Select
          labelId="material-type-filter-label"
          label="Type"
          value={filters.materialType}
          onChange={(e) => updateField("materialType", e.target.value)}
        >
          <MenuItem value="">
            <em>All Types</em>
          </MenuItem>
          {typeOptions.map((opt) => (
            <MenuItem key={opt.value} value={opt.value}>
              {opt.label}
            </MenuItem>
          ))}
          {customTypes.length > 0 && <Divider sx={{ my: 0.5 }} />}
          {customTypes.map((ct) => (
            <MenuItem key={`custom:${ct}`} value={`custom:${ct}`}>
              {ct}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <FormControl size="small" sx={{ minWidth: 160 }}>
        <InputLabel id="material-mfg-filter-label">Manufacturer</InputLabel>
        <Select
          labelId="material-mfg-filter-label"
          label="Manufacturer"
          value={filters.manufacturer}
          onChange={(e) => updateField("manufacturer", e.target.value)}
        >
          <MenuItem value="">
            <em>All Manufacturers</em>
          </MenuItem>
          {manufacturers.map((mfg) => (
            <MenuItem key={mfg} value={mfg}>
              {mfg}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <FormControl size="small" sx={{ minWidth: 140 }}>
        <InputLabel id="material-loc-filter-label">Location</InputLabel>
        <Select
          labelId="material-loc-filter-label"
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

      <FormControl size="small" sx={{ minWidth: 130 }}>
        <InputLabel id="material-status-filter-label">Status</InputLabel>
        <Select
          labelId="material-status-filter-label"
          label="Status"
          value={filters.status}
          onChange={(e) => updateField("status", e.target.value)}
        >
          <MenuItem value="">
            <em>All Statuses</em>
          </MenuItem>
          <MenuItem value="InStock">In Stock</MenuItem>
          <MenuItem value="LowStock">Low Stock</MenuItem>
          <MenuItem value="Depleted">Depleted</MenuItem>
          <MenuItem value="Expired">Expired</MenuItem>
        </Select>
      </FormControl>

      <FormControl size="small" sx={{ minWidth: 150 }}>
        <InputLabel id="material-expiry-filter-label">Expiry</InputLabel>
        <Select
          labelId="material-expiry-filter-label"
          label="Expiry"
          value={filters.expiryRange}
          onChange={(e) => updateField("expiryRange", e.target.value)}
        >
          <MenuItem value="">
            <em>All Expiry</em>
          </MenuItem>
          <MenuItem value="expiring_30">Expiring in 30 Days</MenuItem>
          <MenuItem value="expiring_60">Expiring in 60 Days</MenuItem>
          <MenuItem value="expired">Expired</MenuItem>
          <MenuItem value="valid">Valid / Unexpired</MenuItem>
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
