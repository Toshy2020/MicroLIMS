import { Box, Paper, TextField, Select, MenuItem, InputAdornment, Button, FormControl, InputLabel } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import FilterAltOffIcon from "@mui/icons-material/FilterAltOff";
import { RECEIVING_CATEGORIES, SAMPLE_STATUS_OPTIONS, TEST_STATUS_OPTIONS } from "../constants/receivingConstants";

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
  hasActiveFilters
}: Props) {
  return (
    <Paper
      elevation={0}
      sx={{
        p: 2,
        mb: 2.5,
        border: "1px solid",
        borderColor: "divider",
        borderRadius: 2,
        bgcolor: "background.paper"
      }}
    >
      <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
        {/* Main Search Bar */}
        <TextField
          fullWidth
          size="small"
          placeholder="Search by item, reference number, control number, batch number..."
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon sx={{ color: "text.secondary", fontSize: 20 }} />
              </InputAdornment>
            )
          }}
          sx={{
            "& .MuiOutlinedInput-root": {
              borderRadius: 1.5,
              bgcolor: "background.default"
            }
          }}
        />

        {/* Filter Dropdowns & Date Pickers */}
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: {
              xs: "1fr",
              sm: "repeat(2, 1fr)",
              md: "repeat(5, 1fr) auto"
            },
            gap: 1.5,
            alignItems: "center"
          }}
        >
          {/* Category / Item Type Filter */}
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

          {/* Sample Status Filter */}
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

          {/* Test Status Filter */}
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

          {/* Received From Date */}
          <TextField
            size="small"
            type="date"
            label="Received From"
            InputLabelProps={{ shrink: true }}
            value={fromDate}
            onChange={(e) => onFromDateChange(e.target.value)}
            fullWidth
          />

          {/* Received To Date */}
          <TextField
            size="small"
            type="date"
            label="Received To"
            InputLabelProps={{ shrink: true }}
            value={toDate}
            onChange={(e) => onToDateChange(e.target.value)}
            fullWidth
          />

          {/* Clear / Reset Filters Button */}
          {hasActiveFilters && (
            <Button
              variant="outlined"
              size="small"
              color="inherit"
              onClick={onResetFilters}
              startIcon={<FilterAltOffIcon sx={{ fontSize: 18 }} />}
              sx={{
                height: 40,
                borderColor: "divider",
                color: "text.secondary",
                whiteSpace: "nowrap",
                "&:hover": {
                  bgcolor: "background.default",
                  borderColor: "text.secondary"
                }
              }}
            >
              Reset Filters
            </Button>
          )}
        </Box>
      </Box>
    </Paper>
  );
}
