import { useState } from "react";
import { Box, Paper, TextField, Select, MenuItem, InputAdornment, Button, FormControl, InputLabel, Popover, Chip } from "@mui/material";
import FilterListIcon from "@mui/icons-material/FilterList";
import SearchIcon from "@mui/icons-material/Search";
import FilterAltOffIcon from "@mui/icons-material/FilterAltOff";
import ViewListIcon from "@mui/icons-material/ViewList";
import ViewModuleIcon from "@mui/icons-material/ViewModule";
import ViewKanbanIcon from "@mui/icons-material/ViewKanban";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import ToggleButton from "@mui/material/ToggleButton";
import FileDownloadOutlinedIcon from "@mui/icons-material/FileDownloadOutlined";
import { RECEIVING_CATEGORIES, SAMPLE_STATUS_OPTIONS, TEST_STATUS_OPTIONS } from "../constants/receivingConstants";
import { DateField } from "../../../components/DateField";

export type SampleFilterBarViewMode = "table" | "card" | "kanban";

interface Props {
  search: string;
  onSearchChange: (v: string) => void;
  categoryFilter: string;
  onCategoryFilterChange: (v: string) => void;
  sampleStatusFilter: string;
  onSampleStatusFilterChange: (v: string) => void;
  testStatusFilter: string;
  onTestStatusFilterChange: (v: string) => void;
  fromDate: string;
  onFromDateChange: (v: string) => void;
  toDate: string;
  onToDateChange: (v: string) => void;
  onResetFilters: () => void;
  hasActiveFilters: boolean;
  // Display-mode toggle (table/card/kanban) and CSV export, shown beside
  // the search field - optional so this bar still works anywhere it's
  // used without a view switcher.
  viewMode?: SampleFilterBarViewMode;
  onViewModeChange?: (v: SampleFilterBarViewMode) => void;
  onExport?: () => void;
}

export function SampleFilterBar({
  search,
  onSearchChange,
  categoryFilter,
  onCategoryFilterChange,
  sampleStatusFilter,
  onSampleStatusFilterChange,
  testStatusFilter,
  onTestStatusFilterChange,
  fromDate,
  onFromDateChange,
  toDate,
  onToDateChange,
  onResetFilters,
  hasActiveFilters,
  viewMode,
  onViewModeChange,
  onExport
}: Props) {
  const [filtersAnchor, setFiltersAnchor] = useState<HTMLElement | null>(null);

  // One removable chip per active filter, shown beside the search.
  const activeChips: { key: string; label: string; onDelete: () => void }[] = [];
  if (categoryFilter !== "ALL") {
    const c = RECEIVING_CATEGORIES.find((x) => x.backendCategoryName === categoryFilter);
    activeChips.push({ key: "category", label: `Item type: ${c?.label ?? categoryFilter}`, onDelete: () => onCategoryFilterChange("ALL") });
  }
  if (sampleStatusFilter !== "ALL") {
    const o = SAMPLE_STATUS_OPTIONS.find((x) => x.value === sampleStatusFilter);
    activeChips.push({ key: "sampleStatus", label: `Sample: ${o?.label ?? sampleStatusFilter}`, onDelete: () => onSampleStatusFilterChange("ALL") });
  }
  if (testStatusFilter !== "ALL") {
    const o = TEST_STATUS_OPTIONS.find((x) => x.value === testStatusFilter);
    activeChips.push({ key: "testStatus", label: `Tests: ${o?.label ?? testStatusFilter}`, onDelete: () => onTestStatusFilterChange("ALL") });
  }
  if (fromDate) activeChips.push({ key: "from", label: `From ${new Date(fromDate).toLocaleDateString()}`, onDelete: () => onFromDateChange("") });
  if (toDate) activeChips.push({ key: "to", label: `To ${new Date(toDate).toLocaleDateString()}`, onDelete: () => onToDateChange("") });

  return (
    <Paper
      elevation={0}
      sx={{
        p: 1.5,
        mb: 2,
        border: "1px solid",
        borderColor: "divider",
        borderRadius: 2,
        bgcolor: "background.paper"
      }}
    >
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <TextField
          size="small"
          placeholder="Search item, reference, batch or control number"
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          sx={{
            flex: "1 1 280px",
            "& .MuiOutlinedInput-root": { borderRadius: 1.5, bgcolor: "background.default" }
          }}
          slotProps={{ htmlInput: { "aria-label": "Search by item, reference number, control number, batch number" },
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon sx={{ color: "text.secondary", fontSize: 20 }} />
                </InputAdornment>
              )
            }
          }}
        />

        <Button
          size="small"
          variant="outlined"
          startIcon={<FilterListIcon fontSize="small" />}
          onClick={(e) => setFiltersAnchor(e.currentTarget)}
          aria-haspopup="dialog"
          aria-expanded={Boolean(filtersAnchor)}
          sx={{ height: 40, borderColor: "divider", color: "text.primary", textTransform: "none", fontWeight: 600, flexShrink: 0 }}
        >
          Filters
          {activeChips.length > 0 && (
            <Box component="span" sx={{ ml: 0.75, px: 0.75, borderRadius: 999, fontSize: 11, bgcolor: "primary.main", color: "primary.contrastText" }}>
              {activeChips.length}
            </Box>
          )}
        </Button>

        {viewMode && onViewModeChange && (
          <ToggleButtonGroup
            value={viewMode}
            exclusive
            size="small"
            onChange={(_, v) => v && onViewModeChange(v)}
            aria-label="Register display mode"
            sx={{ flexShrink: 0 }}
          >
            <ToggleButton value="table" title="Table View" aria-label="Table view">
              <ViewListIcon fontSize="small" />
            </ToggleButton>
            <ToggleButton value="card" title="Card View" aria-label="Card view">
              <ViewModuleIcon fontSize="small" />
            </ToggleButton>
            <ToggleButton value="kanban" title="Kanban View" aria-label="Kanban view">
              <ViewKanbanIcon fontSize="small" />
            </ToggleButton>
          </ToggleButtonGroup>
        )}

        {onExport && (
          <Button
            size="small"
            variant="outlined"
            startIcon={<FileDownloadOutlinedIcon />}
            onClick={onExport}
            sx={{
              height: 40,
              flexShrink: 0,
              borderColor: "divider",
              color: "text.secondary",
              fontSize: 12,
              fontWeight: 600,
              textTransform: "none",
              whiteSpace: "nowrap"
            }}
          >
            Export CSV
          </Button>
        )}
      </Box>

      {activeChips.length > 0 && (
        <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75, mt: 1.25, alignItems: "center" }}>
          {activeChips.map((c) => (
            <Chip key={c.key} size="small" label={c.label} onDelete={c.onDelete} color="primary" variant="outlined" sx={{ fontWeight: 600 }} />
          ))}
          {hasActiveFilters && (
            <Button size="small" onClick={onResetFilters} startIcon={<FilterAltOffIcon sx={{ fontSize: 16 }} />} sx={{ textTransform: "none", color: "text.secondary" }}>
              Clear all
            </Button>
          )}
        </Box>
      )}

      <Popover
        open={Boolean(filtersAnchor)}
        anchorEl={filtersAnchor}
        onClose={() => setFiltersAnchor(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "left" }}
        transformOrigin={{ vertical: "top", horizontal: "left" }}
        slotProps={{ paper: { sx: { p: 2, width: 320, maxWidth: "calc(100vw - 32px)", display: "grid", gap: 1.5 } } }}
      >
        <FormControl size="small" fullWidth>
          <InputLabel id="category-filter-label">Item Type</InputLabel>
          <Select
            labelId="category-filter-label"
            label="Item Type"
            value={categoryFilter}
            onChange={(e) => onCategoryFilterChange(e.target.value)}
          >
            <MenuItem value="ALL">
              <em>All Item Types</em>
            </MenuItem>
            {RECEIVING_CATEGORIES.map((c) => (
              <MenuItem key={c.backendCategoryName} value={c.backendCategoryName}>
                {c.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl size="small" fullWidth>
          <InputLabel id="sample-status-filter-label">Sample Status</InputLabel>
          <Select
            labelId="sample-status-filter-label"
            label="Sample Status"
            value={sampleStatusFilter}
            onChange={(e) => onSampleStatusFilterChange(e.target.value)}
          >
            {SAMPLE_STATUS_OPTIONS.map((opt) => (
              <MenuItem key={opt.value} value={opt.value}>
                {opt.value === "ALL" ? <em>{opt.label}</em> : opt.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl size="small" fullWidth>
          <InputLabel id="test-status-filter-label">Test Status</InputLabel>
          <Select
            labelId="test-status-filter-label"
            label="Test Status"
            value={testStatusFilter}
            onChange={(e) => onTestStatusFilterChange(e.target.value)}
          >
            {TEST_STATUS_OPTIONS.map((opt) => (
              <MenuItem key={opt.value} value={opt.value}>
                {opt.value === "ALL" ? <em>{opt.label}</em> : opt.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 1.5 }}>
          <DateField label="Received From" value={fromDate} onChange={onFromDateChange} max={toDate} fullWidth />
          <DateField label="Received To" value={toDate} onChange={onToDateChange} min={fromDate} fullWidth />
        </Box>

        <Box sx={{ display: "flex", justifyContent: "space-between" }}>
          <Button size="small" onClick={onResetFilters} disabled={!hasActiveFilters} sx={{ textTransform: "none" }}>
            Reset
          </Button>
          <Button size="small" variant="contained" onClick={() => setFiltersAnchor(null)} sx={{ textTransform: "none" }}>
            Done
          </Button>
        </Box>
      </Popover>
    </Paper>
  );
}
