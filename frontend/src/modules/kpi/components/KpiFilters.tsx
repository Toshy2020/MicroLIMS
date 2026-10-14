import { useEffect, useState } from "react";
import { Alert, Button, MenuItem, Paper, Stack, TextField } from "@mui/material";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import TuneIcon from "@mui/icons-material/Tune";
import { ReportingService } from "../../reports/services/ReportingService";
import { CATEGORY_LABELS } from "../../reports/services/OverviewService";
import { filtersError } from "../utils/kpiFilters";
import type { KpiFiltersValue } from "../utils/kpiFilters";

interface KpiFiltersProps {
  value: KpiFiltersValue;
  onChange(v: KpiFiltersValue): void;
  onOpenDefinitions(): void;
  onOpenProfiles(): void;
  onExport(): void;
  exportDisabled: boolean;
}

export function KpiFilters({ value, onChange, onOpenDefinitions, onOpenProfiles, onExport, exportDisabled }: KpiFiltersProps) {
  const [options, setOptions] = useState<{ categories: string[]; locations: string[]; testCodes: string[] }>({
    categories: [], locations: [], testCodes: []
  });
  const [optionsFailed, setOptionsFailed] = useState(false);
  // The date inputs hold their own draft: <input type="date"> reports "" while
  // a date is half typed, and a half-typed range must not reach the server.
  const [from, setFrom] = useState(value.from);
  const [to, setTo] = useState(value.to);
  const dateError = filtersError({ from, to });

  useEffect(() => {
    let live = true;
    ReportingService.getFilterOptions()
      .then((o) => {
        if (live && o) {
          setOptions({
            categories: o.categories ?? [],
            locations: o.subjectNames ?? [],
            testCodes: (o.testCodes ?? []).map((t) => t.testCode)
          });
        }
      })
      .catch(() => { if (live) setOptionsFailed(true); });
    return () => { live = false; };
  }, []);

  const changeDates = (nextFrom: string, nextTo: string) => {
    setFrom(nextFrom);
    setTo(nextTo);
    if (!filtersError({ from: nextFrom, to: nextTo })) onChange({ ...value, from: nextFrom, to: nextTo });
  };

  return (
    <Paper sx={{ p: 2, mb: 2.5 }}>
      <Stack direction={{ xs: "column", md: "row" }} spacing={1.5} useFlexGap sx={{ flexWrap: "wrap", alignItems: { md: "flex-start" } }}>
        <TextField
          size="small" type="date" label="From" value={from}
          onChange={(e) => changeDates(e.target.value, to)}
          slotProps={{ inputLabel: { shrink: true } }} error={!!dateError}
        />
        <TextField
          size="small" type="date" label="To" value={to}
          onChange={(e) => changeDates(from, e.target.value)}
          slotProps={{ inputLabel: { shrink: true } }} error={!!dateError} helperText={dateError ?? undefined}
        />
        <TextField
          select size="small" label="Category" value={value.category ?? ""} sx={{ minWidth: 180 }}
          onChange={(e) => onChange({ ...value, category: e.target.value || null })}
        >
          <MenuItem value="">All categories</MenuItem>
          {options.categories.map((c) => <MenuItem key={c} value={c}>{CATEGORY_LABELS[c] ?? c}</MenuItem>)}
        </TextField>
        <TextField
          select size="small" label="Location / subject" value={value.location ?? ""} sx={{ minWidth: 180 }}
          onChange={(e) => onChange({ ...value, location: e.target.value || null })}
        >
          <MenuItem value="">All locations</MenuItem>
          {options.locations.map((l) => <MenuItem key={l} value={l}>{l}</MenuItem>)}
        </TextField>
        <TextField
          select size="small" label="Test" value={value.testCode ?? ""} sx={{ minWidth: 140 }}
          onChange={(e) => onChange({ ...value, testCode: e.target.value || null })}
        >
          <MenuItem value="">All tests</MenuItem>
          {options.testCodes.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
        </TextField>

        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", ml: { md: "auto" } }}>
          <Button size="small" variant="outlined" startIcon={<InfoOutlinedIcon />} onClick={onOpenDefinitions}>Definitions</Button>
          <Button size="small" variant="outlined" startIcon={<TuneIcon />} onClick={onOpenProfiles}>Workflow Profiles</Button>
          <Button size="small" variant="contained" startIcon={<PictureAsPdfIcon />} onClick={onExport} disabled={exportDisabled}>Export PDF</Button>
        </Stack>
      </Stack>
      {optionsFailed && (
        <Alert severity="error" sx={{ mt: 1.5 }}>
          The category, location and test lists could not be loaded. Reload the page to try again.
        </Alert>
      )}
    </Paper>
  );
}
