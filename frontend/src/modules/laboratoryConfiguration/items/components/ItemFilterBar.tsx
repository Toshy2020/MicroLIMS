import { Box, TextField, Select, MenuItem, Button, Stack, InputAdornment } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import RestartAltIcon from "@mui/icons-material/RestartAlt";

interface ItemFilterBarProps {
  searchQuery: string;
  onSearchChange: (value: string) => void;
  categoryFilter: string;
  onCategoryChange: (value: string) => void;
  statusFilter: string;
  onStatusChange: (value: string) => void;
  onReset: () => void;
}

const CATEGORIES = [
  { value: "ALL", label: "All Categories" },
  { value: "FinishedProduct", label: "Product" },
  { value: "RawMaterial", label: "Raw Material" },
  { value: "PackagingMaterial", label: "Packaging Material" },
];

const STATUSES = [
  { value: "ALL", label: "All Statuses" },
  { value: "Active", label: "Active" },
  { value: "Frozen", label: "Frozen" },
];

export function ItemFilterBar({
  searchQuery,
  onSearchChange,
  categoryFilter,
  onCategoryChange,
  statusFilter,
  onStatusChange,
  onReset,
}: ItemFilterBarProps) {
  const isFiltered = searchQuery !== "" || categoryFilter !== "ALL" || statusFilter !== "ALL";

  return (
    <Box sx={{ mb: 2.5, p: 2, bgcolor: "background.paper", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} alignItems="center">
        <TextField
          size="small"
          placeholder="Search by item name, item code, SOP number..."
          value={searchQuery}
          onChange={(e) => onSearchChange(e.target.value)}
          sx={{ flexGrow: 1, minWidth: { xs: "100%", sm: 280 } }}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon fontSize="small" sx={{ color: "text.secondary" }} />
              </InputAdornment>
            ),
          }}
        />

        <Select
          size="small"
          value={categoryFilter}
          onChange={(e) => onCategoryChange(e.target.value)}
          sx={{ minWidth: 180, xs: "100%" }}
        >
          {CATEGORIES.map((c) => (
            <MenuItem key={c.value} value={c.value}>
              {c.label}
            </MenuItem>
          ))}
        </Select>

        <Select
          size="small"
          value={statusFilter}
          onChange={(e) => onStatusChange(e.target.value)}
          sx={{ minWidth: 150, xs: "100%" }}
        >
          {STATUSES.map((s) => (
            <MenuItem key={s.value} value={s.value}>
              {s.label}
            </MenuItem>
          ))}
        </Select>

        {isFiltered && (
          <Button
            size="small"
            variant="outlined"
            color="inherit"
            startIcon={<RestartAltIcon fontSize="small" />}
            onClick={onReset}
            sx={{ textTransform: "none", height: 40 }}
          >
            Reset
          </Button>
        )}
      </Stack>
    </Box>
  );
}
