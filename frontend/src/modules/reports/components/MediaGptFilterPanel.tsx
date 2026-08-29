import { RefObject } from "react";
import {
  Box, Paper, TextField, MenuItem, Select, InputLabel, FormControl,
  Button, Link, Typography, Stack, useTheme
} from "@mui/material";
import { EvaluationOutcome, EvaluationType, MediaGptFilterOptions, MediaGptSearchParams } from "../types/mediaGptTypes";
import { brandColors } from "../../../theme";

interface MediaGptFilterPanelProps {
  filterOptions: MediaGptFilterOptions | null;
  draft: MediaGptSearchParams;
  onChange: (patch: Partial<MediaGptSearchParams>) => void;
  onSearch: () => void;
  onReset: () => void;
  searchInputRef?: RefObject<HTMLInputElement>;
}

export function MediaGptFilterPanel({
  filterOptions,
  draft,
  onChange,
  onSearch,
  onReset,
  searchInputRef
}: MediaGptFilterPanelProps) {
  const theme = useTheme();

  return (
    <Paper sx={{ p: 2.5 }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
        <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
          Media / GPT Filters
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
          placeholder="Lot Number, Media Type..."
          value={draft.search ?? ""}
          onChange={(e) => onChange({ search: e.target.value || undefined })}
          inputRef={searchInputRef}
        />

        <FormControl fullWidth size="small">
          <InputLabel>Evaluation Type</InputLabel>
          <Select
            label="Evaluation Type"
            value={draft.evaluationType ?? ""}
            onChange={(e) => onChange({ evaluationType: (e.target.value || undefined) as EvaluationType | undefined })}
          >
            <MenuItem value="">All Evaluation Types</MenuItem>
            <MenuItem value="GrowthPromotion">Growth Promotion (GPT)</MenuItem>
            <MenuItem value="IndicationInhibition">Indication / Inhibition</MenuItem>
            <MenuItem value="EnrichmentCharacteristics">Enrichment Characteristics</MenuItem>
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>Media Type</InputLabel>
          <Select
            label="Media Type"
            value={draft.mediaType ?? ""}
            onChange={(e) => onChange({ mediaType: e.target.value || undefined })}
          >
            <MenuItem value="">All Media Types</MenuItem>
            {(filterOptions?.mediaTypes ?? []).map((m) => (
              <MenuItem key={m} value={m}>{m}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>Evaluation Outcome</InputLabel>
          <Select
            label="Evaluation Outcome"
            value={draft.outcome ?? ""}
            onChange={(e) => onChange({ outcome: (e.target.value || undefined) as EvaluationOutcome | undefined })}
          >
            <MenuItem value="">All Outcomes</MenuItem>
            <MenuItem value="Conform">Conform</MenuItem>
            <MenuItem value="NonConform">NonConform</MenuItem>
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

        <Stack direction="row" spacing={1} sx={{ pt: 1 }}>
          <Button
            variant="contained"
            fullWidth
            onClick={onSearch}
            sx={{ bgcolor: brandColors.sectionTitle, "&:hover": { bgcolor: "#4a0f61" }, fontWeight: 700 }}
          >
            Search Media Lots
          </Button>
          <Button variant="outlined" onClick={onReset} sx={{ fontWeight: 600 }}>
            Clear
          </Button>
        </Stack>
      </Stack>
    </Paper>
  );
}
