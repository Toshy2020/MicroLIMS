import { useState } from "react";
import {
  Paper,
  Box,
  TextField,
  Button,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Collapse,
  Typography
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import RestartAltIcon from "@mui/icons-material/RestartAlt";
import FilterListIcon from "@mui/icons-material/FilterList";
import type { AuditSearchFilterState } from "../types/auditTypes";
import { ENTITY_DISPLAY_NAMES } from "../types/auditTypes";
import { brandColors } from "../../../theme";

interface Props {
  filters: AuditSearchFilterState;
  onFilterChange: (filters: AuditSearchFilterState) => void;
  onSearch: () => void;
  onReset: () => void;
  loading: boolean;
}

export function AuditFilterBar({
  filters,
  onFilterChange,
  onSearch,
  onReset,
  loading
}: Props) {
  const [showAdvanced, setShowAdvanced] = useState(false);

  const handleChange = (key: keyof AuditSearchFilterState, val: string) => {
    onFilterChange({ ...filters, [key]: val });
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter") {
      onSearch();
    }
  };

  return (
    <Paper
      sx={{
        p: 2.5,
        mb: 3,
        border: "1px solid #e5e7eb",
        borderRadius: 2,
        boxShadow: "0 1px 3px rgba(0,0,0,0.04)"
      }}
    >
      {/* Primary quick filters */}
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "repeat(4, 1fr)"
          },
          gap: 2,
          mb: 2
        }}
        onKeyDown={handleKeyDown}
      >
        <TextField
          size="small"
          type="date"
          label="From Date"
          InputLabelProps={{ shrink: true }}
          value={filters.fromDate}
          onChange={(e) => handleChange("fromDate", e.target.value)}
        />

        <TextField
          size="small"
          type="date"
          label="To Date"
          InputLabelProps={{ shrink: true }}
          value={filters.toDate}
          onChange={(e) => handleChange("toDate", e.target.value)}
        />

        <FormControl size="small" fullWidth>
          <InputLabel id="audit-filter-action-label">Action</InputLabel>
          <Select
            labelId="audit-filter-action-label"
            label="Action"
            value={filters.action}
            onChange={(e) => handleChange("action", e.target.value)}
          >
            <MenuItem value="">All Actions</MenuItem>
            <MenuItem value="Create">Create</MenuItem>
            <MenuItem value="Update">Update</MenuItem>
            <MenuItem value="Delete">Delete</MenuItem>
          </Select>
        </FormControl>

        <FormControl size="small" fullWidth>
          <InputLabel id="audit-filter-entity-label">Entity Category</InputLabel>
          <Select
            labelId="audit-filter-entity-label"
            label="Entity Category"
            value={filters.entityName}
            onChange={(e) => handleChange("entityName", e.target.value)}
          >
            <MenuItem value="">All Entities</MenuItem>
            {Object.entries(ENTITY_DISPLAY_NAMES).map(([key, label]) => (
              <MenuItem key={key} value={key}>
                {label} ({key})
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </Box>

      {/* Advanced laboratory reference filters */}
      <Collapse in={showAdvanced}>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: {
              xs: "1fr",
              sm: "repeat(2, 1fr)",
              md: "repeat(4, 1fr)"
            },
            gap: 2,
            pt: 1,
            pb: 2,
            borderTop: "1px dashed #e5e7eb"
          }}
          onKeyDown={handleKeyDown}
        >
          <TextField
            size="small"
            label="Sample Reference"
            placeholder="e.g. FP0107026"
            value={filters.sampleReferenceNumber}
            onChange={(e) => handleChange("sampleReferenceNumber", e.target.value)}
          />

          <TextField
            size="small"
            label="Batch Number"
            placeholder="e.g. B-2026-088"
            value={filters.batchNumber}
            onChange={(e) => handleChange("batchNumber", e.target.value)}
          />

          <TextField
            size="small"
            label="Control Number"
            placeholder="e.g. CTRL-904"
            value={filters.controlNumber}
            onChange={(e) => handleChange("controlNumber", e.target.value)}
          />

          <TextField
            size="small"
            label="Media Lot Number"
            placeholder="e.g. MED-044"
            value={filters.mediaLotNumber}
            onChange={(e) => handleChange("mediaLotNumber", e.target.value)}
          />

          <TextField
            size="small"
            label="Cryovial / RS Code"
            placeholder="e.g. CRYO-019"
            value={filters.cryovialCode}
            onChange={(e) => handleChange("cryovialCode", e.target.value)}
          />

          <TextField
            size="small"
            label="User ID"
            placeholder="e.g. 5"
            value={filters.userId}
            onChange={(e) => handleChange("userId", e.target.value)}
          />
        </Box>
      </Collapse>

      {/* Footer controls */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Button
          size="small"
          variant="text"
          startIcon={<FilterListIcon />}
          onClick={() => setShowAdvanced(!showAdvanced)}
          sx={{ textTransform: "none", fontSize: 12, color: "text.secondary" }}
        >
          {showAdvanced ? "Hide Laboratory Identifiers" : "More Laboratory Identifiers (Batch, Sample, Media...)"}
        </Button>

        <Box sx={{ display: "flex", gap: 1 }}>
          <Button
            size="small"
            variant="outlined"
            startIcon={<RestartAltIcon />}
            onClick={onReset}
            disabled={loading}
            sx={{ textTransform: "none", color: "text.secondary" }}
          >
            Reset
          </Button>

          <Button
            id="audit-search-submit-btn"
            size="small"
            variant="contained"
            startIcon={<SearchIcon />}
            onClick={onSearch}
            disabled={loading}
            sx={{
              bgcolor: brandColors.sectionTitle,
              px: 2.5,
              textTransform: "none",
              fontWeight: 600,
              "&:hover": { bgcolor: brandColors.pageTitle }
            }}
          >
            {loading ? "Searching..." : "Search Audit Trail"}
          </Button>
        </Box>
      </Box>
    </Paper>
  );
}
