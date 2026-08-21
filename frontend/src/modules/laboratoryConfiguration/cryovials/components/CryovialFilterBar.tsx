import { useMemo } from "react";
import {
  Paper,
  Box,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Button,
  InputAdornment,
  ButtonGroup,
  useTheme
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import RotateLeftIcon from "@mui/icons-material/RotateLeft";
import { CryovialItem, CryovialFilterState } from "../types/cryovialTypes";
import { brandColors } from "../../../../theme";

interface CryovialFilterBarProps {
  items: CryovialItem[];
  filters: CryovialFilterState;
  onFilterChange: (newFilters: CryovialFilterState) => void;
  onReset: () => void;
}

export function CryovialFilterBar({ items, filters, onFilterChange, onReset }: CryovialFilterBarProps) {
  const theme = useTheme();
  const { notDetected, action, detected } = theme.custom.status;

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

  const handleShortcutStatus = (statusValue: string) => {
    updateField("status", statusValue);
  };

  return (
    <Paper sx={{ p: 2, mb: 2, bgcolor: "background.paper", border: "1px solid", borderColor: "divider" }}>
      {/* Top row: Status shortcuts & search */}
      <Box
        sx={{
          display: "flex",
          flexWrap: "wrap",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 1.5,
          mb: 1.5
        }}
      >
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
          <ButtonGroup size="small" variant="outlined">
            <Button
              variant={filters.status === "" ? "contained" : "outlined"}
              onClick={() => handleShortcutStatus("")}
              sx={{
                bgcolor: filters.status === "" ? brandColors.sectionTitle : undefined,
                color: filters.status === "" ? "#ffffff" : "text.secondary",
                borderColor: "divider",
                "&:hover": { bgcolor: filters.status === "" ? brandColors.pageTitle : "background.default" }
              }}
            >
              All
            </Button>
            <Button
              variant={filters.status === "Approved" ? "contained" : "outlined"}
              onClick={() => handleShortcutStatus("Approved")}
              sx={{
                bgcolor: filters.status === "Approved" ? notDetected.text : undefined,
                color: filters.status === "Approved" ? "#ffffff" : notDetected.text,
                borderColor: "divider",
                "&:hover": { bgcolor: filters.status === "Approved" ? notDetected.text : notDetected.bg }
              }}
            >
              Approved
            </Button>
            <Button
              variant={filters.status === "PendingReview" ? "contained" : "outlined"}
              onClick={() => handleShortcutStatus("PendingReview")}
              sx={{
                bgcolor: filters.status === "PendingReview" ? action.text : undefined,
                color: filters.status === "PendingReview" ? "#ffffff" : action.text,
                borderColor: "divider",
                "&:hover": { bgcolor: filters.status === "PendingReview" ? action.text : action.bg }
              }}
            >
              Pending Review
            </Button>
            <Button
              variant={filters.status === "Rejected" ? "contained" : "outlined"}
              onClick={() => handleShortcutStatus("Rejected")}
              sx={{
                bgcolor: filters.status === "Rejected" ? detected.text : undefined,
                color: filters.status === "Rejected" ? "#ffffff" : detected.text,
                borderColor: "divider",
                "&:hover": { bgcolor: filters.status === "Rejected" ? detected.text : detected.bg }
              }}
            >
              Rejected
            </Button>
          </ButtonGroup>
        </Box>

        <Box sx={{ display: "flex", gap: 1, flexGrow: 1, maxWidth: { xs: "100%", sm: 380 } }}>
          <TextField
            size="small"
            fullWidth
            placeholder="Search by code, organism, material..."
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
        </Box>
      </Box>

      {/* Bottom row: Detailed Dropdown Filters & Reset */}
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "1.5fr 1.2fr 1.2fr auto"
          },
          gap: 1.5,
          alignItems: "center"
        }}
      >
        <FormControl size="small" fullWidth>
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

        <FormControl size="small" fullWidth>
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

        <FormControl size="small" fullWidth>
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
        </Box>
      </Box>
    </Paper>
  );
}
