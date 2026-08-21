import { useMemo } from "react";
import { Paper, Box, TextField, Select, MenuItem, FormControl, InputLabel, Button, InputAdornment } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import RotateLeftIcon from "@mui/icons-material/RotateLeft";
import { EquipmentItem, EquipmentFilterState } from "../types/equipmentTypes";
import { brandColors } from "../../../../theme";

interface EquipmentFilterBarProps {
  items: EquipmentItem[];
  filters: EquipmentFilterState;
  onFilterChange: (newFilters: EquipmentFilterState) => void;
  onReset: () => void;
}

export function EquipmentFilterBar({ items, filters, onFilterChange, onReset }: EquipmentFilterBarProps) {
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

  return (
    <Paper sx={{ p: 2, mb: 2, bgcolor: "background.paper", border: "1px solid", borderColor: "divider" }}>
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "2.2fr 1.3fr 1.3fr 1.3fr 1.3fr auto"
          },
          gap: 1.5,
          alignItems: "center"
        }}
      >
        <TextField
          size="small"
          placeholder="Search by type, manufacturer, serial no., code, location..."
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

        <FormControl size="small" fullWidth>
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

        <FormControl size="small" fullWidth>
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

        <FormControl size="small" fullWidth>
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

        <Box sx={{ display: "flex", gap: 1, justifyContent: { xs: "flex-end", md: "flex-start" } }}>
          <Button
            size="small"
            variant="outlined"
            onClick={onReset}
            startIcon={<RotateLeftIcon fontSize="small" />}
            sx={{ borderColor: "divider", color: "text.secondary", minWidth: 90 }}
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
