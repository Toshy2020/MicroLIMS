import { useMemo } from "react";
import { Paper, Box, TextField, Select, MenuItem, FormControl, InputLabel, Button, InputAdornment } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import RotateLeftIcon from "@mui/icons-material/RotateLeft";
import { MaterialItem, MaterialFilterState, MaterialType } from "../types/materialTypes";
import { brandColors } from "../../../../theme";

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
  { label: "Other", value: "Other" }
];

interface MaterialFilterBarProps {
  items: MaterialItem[];
  filters: MaterialFilterState;
  onFilterChange: (newFilters: MaterialFilterState) => void;
  onReset: () => void;
}

export function MaterialFilterBar({ items, filters, onFilterChange, onReset }: MaterialFilterBarProps) {
  // Extract unique dynamic dropdown options from current dataset
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

  return (
    <Paper sx={{ p: 2, mb: 2, bgcolor: "#ffffff", border: "1px solid #e5e7eb" }}>
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "2fr 1.2fr 1.2fr 1.2fr 1.2fr 1.2fr auto"
          },
          gap: 1.5,
          alignItems: "center"
        }}
      >
        <TextField
          size="small"
          placeholder="Search by name, lot no., code..."
          value={filters.search}
          onChange={(e) => updateField("search", e.target.value)}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon fontSize="small" sx={{ color: "text.secondary" }} />
              </InputAdornment>
            )
          }}
        />

        <FormControl size="small" fullWidth>
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
            {MATERIAL_TYPE_OPTIONS.map((opt) => (
              <MenuItem key={opt.value} value={opt.value}>
                {opt.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl size="small" fullWidth>
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

        <FormControl size="small" fullWidth>
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

        <FormControl size="small" fullWidth>
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

        <FormControl size="small" fullWidth>
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

        <Box sx={{ display: "flex", gap: 1, justifyContent: { xs: "flex-end", md: "flex-start" } }}>
          <Button
            size="small"
            variant="outlined"
            onClick={onReset}
            startIcon={<RotateLeftIcon fontSize="small" />}
            sx={{ borderColor: "#d1d5db", color: "#4b5563", minWidth: 90 }}
          >
            Reset
          </Button>
          <Button
            size="small"
            variant="contained"
            sx={{ bgcolor: brandColors.sectionTitle, minWidth: 90, "&:hover": { bgcolor: brandColors.pageTitle } }}
          >
            Search
          </Button>
        </Box>
      </Box>
    </Paper>
  );
}
