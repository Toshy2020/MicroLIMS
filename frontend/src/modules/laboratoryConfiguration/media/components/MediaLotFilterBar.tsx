import React from "react";
import {
  Paper,
  Stack,
  TextField,
  Select,
  MenuItem,
  Button,
  InputAdornment,
  Box
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import FilterAltOffOutlinedIcon from "@mui/icons-material/FilterAltOffOutlined";
import { mediaClassLabel } from "../../../../services/masterDataOptions";

interface Props {
  search: string;
  onSearchChange: (v: string) => void;
  selectedMediaTypeId: string;
  onMediaTypeChange: (v: string) => void;
  selectedStatus: string;
  onStatusChange: (v: string) => void;
  mediaTypes: any[];
  onResetFilters: () => void;
}

const STATUS_OPTIONS = [
  { value: "Pending Evaluation", label: "Pending Evaluation" },
  { value: "Awaiting Approval", label: "Awaiting Approval" },
  { value: "Released", label: "Released for Use" },
  { value: "Quarantined", label: "Quarantined" }
];

export function MediaLotFilterBar({
  search,
  onSearchChange,
  selectedMediaTypeId,
  onMediaTypeChange,
  selectedStatus,
  onStatusChange,
  mediaTypes,
  onResetFilters
}: Props) {
  const hasActiveFilters = Boolean(search || selectedMediaTypeId || selectedStatus);

  return (
    <Paper
      elevation={0}
      sx={{
        p: 1.5,
        mb: 2,
        border: "1px solid #e5e7eb",
        borderRadius: 2,
        bgcolor: "#ffffff"
      }}
    >
      <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap" rowGap={1}>
        <TextField
          size="small"
          placeholder="Search by lot number, media type, material, batch…"
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          sx={{ minWidth: 280, flex: 1 }}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon fontSize="small" sx={{ color: "text.secondary" }} />
              </InputAdornment>
            )
          }}
        />

        <Select
          size="small"
          displayEmpty
          value={selectedMediaTypeId}
          onChange={(e) => onMediaTypeChange(e.target.value)}
          sx={{ minWidth: 180 }}
        >
          <MenuItem value="">
            <em>All Media Types</em>
          </MenuItem>
          {mediaTypes.map((m) => (
            <MenuItem key={m.id} value={String(m.id)}>
              {mediaClassLabel(m.class)}
            </MenuItem>
          ))}
        </Select>

        <Select
          size="small"
          displayEmpty
          value={selectedStatus}
          onChange={(e) => onStatusChange(e.target.value)}
          sx={{ minWidth: 180 }}
        >
          <MenuItem value="">
            <em>All Statuses</em>
          </MenuItem>
          {STATUS_OPTIONS.map((s) => (
            <MenuItem key={s.value} value={s.value}>
              {s.label}
            </MenuItem>
          ))}
        </Select>

        {hasActiveFilters && (
          <Button
            size="small"
            variant="outlined"
            onClick={onResetFilters}
            startIcon={<FilterAltOffOutlinedIcon fontSize="small" />}
            sx={{
              borderColor: "#d1d5db",
              color: "#4b5563",
              fontSize: 12,
              fontWeight: 600,
              "&:hover": { borderColor: "#9ca3af", bgcolor: "#f3f4f6" }
            }}
          >
            Reset Filters
          </Button>
        )}
      </Stack>
    </Paper>
  );
}
