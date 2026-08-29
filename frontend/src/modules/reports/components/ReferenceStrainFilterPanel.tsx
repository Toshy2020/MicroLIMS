import { RefObject } from "react";
import {
  Box, Paper, TextField, MenuItem, Select, InputLabel, FormControl,
  Button, Link, Typography, Stack, useTheme
} from "@mui/material";
import { ReferenceStrainFilterOptions, ReferenceStrainSearchParams } from "../types/referenceStrainTypes";
import { brandColors } from "../../../theme";

interface ReferenceStrainFilterPanelProps {
  filterOptions: ReferenceStrainFilterOptions | null;
  draft: ReferenceStrainSearchParams;
  onChange: (patch: Partial<ReferenceStrainSearchParams>) => void;
  onSearch: () => void;
  onReset: () => void;
  searchInputRef?: RefObject<HTMLInputElement>;
}

export function ReferenceStrainFilterPanel({
  filterOptions,
  draft,
  onChange,
  onSearch,
  onReset,
  searchInputRef
}: ReferenceStrainFilterPanelProps) {
  const theme = useTheme();

  return (
    <Paper sx={{ p: 2.5 }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
        <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
          Reference Strain Filters
        </Typography>
        <Link component="button" type="button" onClick={onReset} sx={{ fontSize: 13 }} underline="hover">
          Reset
        </Link>
      </Box>

      <Stack spacing={2}>
        <TextField
          fullWidth
          size="small"
          label="Search"
          placeholder="Code, Organism, ATCC, Batch, Mfr..."
          value={draft.search ?? ""}
          onChange={(e) => onChange({ search: e.target.value || undefined })}
          inputRef={searchInputRef}
        />

        <FormControl fullWidth size="small">
          <InputLabel>Organism / Strain</InputLabel>
          <Select
            label="Organism / Strain"
            value={draft.organismId ?? ""}
            onChange={(e) => onChange({ organismId: e.target.value ? Number(e.target.value) : undefined })}
          >
            <MenuItem value="">All Organisms</MenuItem>
            {(filterOptions?.organisms ?? []).map((o) => (
              <MenuItem key={o.id} value={o.id}>
                {o.scientificName} {o.atccNumber ? `(ATCC ${o.atccNumber})` : ""}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>Approval Status</InputLabel>
          <Select
            label="Approval Status"
            value={draft.approvalStatus ?? ""}
            onChange={(e) => onChange({ approvalStatus: (e.target.value || undefined) as any })}
          >
            <MenuItem value="">All Statuses</MenuItem>
            <MenuItem value="PendingReview">Pending Review</MenuItem>
            <MenuItem value="Approved">Approved</MenuItem>
            <MenuItem value="Rejected">Rejected</MenuItem>
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>Active / Destroyed</InputLabel>
          <Select
            label="Active / Destroyed"
            value={draft.isDestroyed === undefined ? "" : draft.isDestroyed ? "true" : "false"}
            onChange={(e) => onChange({ isDestroyed: e.target.value === "" ? undefined : e.target.value === "true" })}
          >
            <MenuItem value="">All Batches</MenuItem>
            <MenuItem value="false">Active Only</MenuItem>
            <MenuItem value="true">Destroyed Only</MenuItem>
          </Select>
        </FormControl>

        <Box>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.5, fontWeight: 600 }}>
            Material Receipt Date Range
          </Typography>
          <Stack direction="row" spacing={1}>
            <TextField
              size="small"
              type="date"
              label="From"
              InputLabelProps={{ shrink: true }}
              value={draft.receiptFromDate?.slice(0, 10) ?? ""}
              onChange={(e) => onChange({ receiptFromDate: e.target.value ? `${e.target.value}T00:00:00.000Z` : undefined })}
              fullWidth
            />
            <TextField
              size="small"
              type="date"
              label="To"
              InputLabelProps={{ shrink: true }}
              value={draft.receiptToDate?.slice(0, 10) ?? ""}
              onChange={(e) => onChange({ receiptToDate: e.target.value ? `${e.target.value}T23:59:59.999Z` : undefined })}
              fullWidth
            />
          </Stack>
        </Box>

        <Box>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.5, fontWeight: 600 }}>
            GPT Usage Date Range
          </Typography>
          <Stack direction="row" spacing={1}>
            <TextField
              size="small"
              type="date"
              label="From"
              InputLabelProps={{ shrink: true }}
              value={draft.usageFromDate?.slice(0, 10) ?? ""}
              onChange={(e) => onChange({ usageFromDate: e.target.value ? `${e.target.value}T00:00:00.000Z` : undefined })}
              fullWidth
            />
            <TextField
              size="small"
              type="date"
              label="To"
              InputLabelProps={{ shrink: true }}
              value={draft.usageToDate?.slice(0, 10) ?? ""}
              onChange={(e) => onChange({ usageToDate: e.target.value ? `${e.target.value}T23:59:59.999Z` : undefined })}
              fullWidth
            />
          </Stack>
        </Box>

        <Stack direction="row" spacing={1} sx={{ pt: 1 }}>
          <Button
            variant="contained"
            fullWidth
            onClick={onSearch}
            sx={{ bgcolor: brandColors.sectionTitle, "&:hover": { bgcolor: "#4a0f61" }, fontWeight: 700 }}
          >
            Search Batches
          </Button>
          <Button variant="outlined" onClick={onReset} sx={{ fontWeight: 600 }}>
            Clear
          </Button>
        </Stack>
      </Stack>
    </Paper>
  );
}
