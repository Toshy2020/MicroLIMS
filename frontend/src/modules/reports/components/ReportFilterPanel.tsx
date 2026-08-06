import { RefObject } from "react";
import { Box, Paper, TextField, MenuItem, Select, InputLabel, FormControl, Button, Link, Typography, Stack } from "@mui/material";
import { brandColors } from "../../../theme";
import { statusColor, categoryLabel } from "../../../components/StatusBadge";
import { FilterOptionsResponse, ResultLevel, ResultRecordSearchParams } from "../types/reportingTypes";

const RESULT_LEVEL_SEGMENTS: { value: ResultLevel | ""; label: string }[] = [
  { value: "", label: "All" },
  { value: "WithinLimit", label: "Within Limit" },
  { value: "AlertLevel", label: "Alert Level" },
  { value: "ActionLevel", label: "Action Level" },
  { value: "OutOfSpecification", label: "Out of Spec" }
];

const SAMPLE_STATUS_OPTIONS = ["Received", "InTesting", "UnderReview", "UnderApproval", "Approved", "Rejected", "RetestRequested"];
const APPROVAL_STATUS_OPTIONS = ["Approved", "Pending", "Rejected"];

// yyyy-MM-dd out of a full ISO timestamp, for <input type="date"> value.
function isoToDateInput(iso?: string): string {
  return iso ? iso.slice(0, 10) : "";
}

interface ReportFilterPanelProps {
  filterOptions: FilterOptionsResponse | null;
  draft: ResultRecordSearchParams;
  onChange: (patch: Partial<ResultRecordSearchParams>) => void;
  onSearch: () => void;
  onReset: () => void;
  searchInputRef?: RefObject<HTMLInputElement>;
}

// Left-column filter panel for the Record Search tab. Every field here
// only edits draft state - nothing queries the API until "Search
// Records" is clicked (RecordSearchTab applies the draft then).
export function ReportFilterPanel({ filterOptions, draft, onChange, onSearch, onReset, searchInputRef }: ReportFilterPanelProps) {
  return (
    <Paper sx={{ p: 2.5 }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
        <Typography sx={{ fontSize: 15, fontWeight: 700, color: brandColors.sectionTitle }}>Filters</Typography>
        <Link component="button" type="button" onClick={onReset} sx={{ fontSize: 13 }} underline="hover">
          Reset
        </Link>
      </Box>

      <Stack spacing={2}>
        <TextField
          fullWidth size="small" label="Search"
          placeholder="Search Sample ID, Batch No., Item, Test, Location..."
          value={draft.search ?? ""}
          onChange={(e) => onChange({ search: e.target.value || undefined })}
          inputRef={searchInputRef}
        />

        <FormControl fullWidth size="small">
          <InputLabel>Result Type</InputLabel>
          <Select
            label="Result Type" value={draft.resultKind ?? ""}
            onChange={(e) => onChange({ resultKind: (e.target.value || undefined) as ResultRecordSearchParams["resultKind"] })}
          >
            <MenuItem value="">All</MenuItem>
            <MenuItem value="Quantitative">Quantitative</MenuItem>
            <MenuItem value="Qualitative">Qualitative</MenuItem>
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>Category</InputLabel>
          <Select
            label="Category" value={draft.category ?? ""}
            onChange={(e) => onChange({ category: (e.target.value || undefined) as ResultRecordSearchParams["category"] })}
          >
            <MenuItem value="">All</MenuItem>
            {(filterOptions?.categories ?? []).map((c) => (
              <MenuItem key={c} value={c}>{categoryLabel(c)}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>Test</InputLabel>
          <Select
            label="Test" value={draft.testCode ?? ""}
            onChange={(e) => onChange({ testCode: e.target.value || undefined })}
          >
            <MenuItem value="">All</MenuItem>
            {(filterOptions?.testCodes ?? []).map((t) => (
              <MenuItem key={t.testCode} value={t.testCode}>{t.testCode} - {t.testDisplayName}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <Box>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.75 }}>Result Level</Typography>
          <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75 }}>
            {RESULT_LEVEL_SEGMENTS.map((seg) => {
              const selected = (draft.resultLevel ?? "") === seg.value;
              const color = seg.value ? statusColor(seg.value) : brandColors.sectionTitle;
              return (
                <Button
                  key={seg.label} size="small"
                  onClick={() => onChange({ resultLevel: (seg.value || undefined) as ResultRecordSearchParams["resultLevel"] })}
                  sx={{
                    fontSize: 11.5, minWidth: 0, px: 1.25, py: 0.5, textTransform: "none",
                    color: selected ? "#fff" : color,
                    bgcolor: selected ? color : "transparent",
                    border: `1px solid ${color}`,
                    "&:hover": { bgcolor: selected ? color : `${color}1a` }
                  }}
                >
                  {seg.label}
                </Button>
              );
            })}
          </Box>
        </Box>

        <FormControl fullWidth size="small">
          <InputLabel>Status</InputLabel>
          <Select
            label="Status" value={draft.sampleStatus ?? ""}
            onChange={(e) => onChange({ sampleStatus: (e.target.value || undefined) as ResultRecordSearchParams["sampleStatus"] })}
          >
            <MenuItem value="">All</MenuItem>
            {SAMPLE_STATUS_OPTIONS.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
          </Select>
        </FormControl>

        <FormControl fullWidth size="small">
          <InputLabel>Approval Status</InputLabel>
          <Select
            label="Approval Status" value={draft.approvalStatus ?? ""}
            onChange={(e) => onChange({ approvalStatus: (e.target.value || undefined) as ResultRecordSearchParams["approvalStatus"] })}
          >
            <MenuItem value="">All</MenuItem>
            {APPROVAL_STATUS_OPTIONS.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
          </Select>
        </FormControl>

        <Box>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.75 }}>Date Range</Typography>
          <Box sx={{ display: "flex", gap: 1 }}>
            <TextField
              size="small" type="date" fullWidth InputLabelProps={{ shrink: true }}
              value={isoToDateInput(draft.fromDate)}
              onChange={(e) => onChange({ fromDate: e.target.value ? `${e.target.value}T00:00:00.000Z` : undefined })}
            />
            <TextField
              size="small" type="date" fullWidth InputLabelProps={{ shrink: true }}
              value={isoToDateInput(draft.toDate)}
              onChange={(e) => onChange({ toDate: e.target.value ? `${e.target.value}T23:59:59.999Z` : undefined })}
            />
          </Box>
        </Box>

        <FormControl fullWidth size="small">
          <InputLabel>Location / Point</InputLabel>
          <Select
            label="Location / Point" value={draft.subjectName ?? ""}
            onChange={(e) => onChange({ subjectName: e.target.value || undefined })}
          >
            <MenuItem value="">All</MenuItem>
            {(filterOptions?.subjectNames ?? []).map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
          </Select>
        </FormControl>

        <Button variant="contained" fullWidth onClick={onSearch}>Search Records</Button>
      </Stack>
    </Paper>
  );
}
