import { Box, Button, MenuItem, Paper, Stack, TextField } from "@mui/material";
import FileDownloadOutlinedIcon from "@mui/icons-material/FileDownloadOutlined";
import RestartAltIcon from "@mui/icons-material/RestartAlt";
import type { IncidentFilterState } from "../types/errorMonitoringTypes";
import { SEVERITIES, SOURCES, STATUSES } from "../types/errorMonitoringTypes";

interface Props {
  filters: IncidentFilterState;
  onChange: (filters: IncidentFilterState) => void;
  onReset: () => void;
  onExport: () => void;
  exporting: boolean;
  disabled: boolean;
}

export function IncidentFilterBar({ filters, onChange, onReset, onExport, exporting, disabled }: Props) {
  const set = <K extends keyof IncidentFilterState>(key: K, value: IncidentFilterState[K]) =>
    onChange({ ...filters, [key]: value });

  return (
    <Paper sx={{ p: 2, mb: 2 }}>
      <Stack
        direction="row"
        spacing={2}
        useFlexGap
        sx={{
          flexWrap: "wrap",
          alignItems: "flex-end"
        }}>
        <TextField
          select size="small" label="Severity" sx={{ minWidth: 140 }}
          value={filters.severity}
          onChange={(e) => set("severity", e.target.value as IncidentFilterState["severity"])}
        >
          <MenuItem value="">All</MenuItem>
          {SEVERITIES.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
        </TextField>

        <TextField
          select size="small" label="Source" sx={{ minWidth: 140 }}
          value={filters.source}
          onChange={(e) => set("source", e.target.value as IncidentFilterState["source"])}
        >
          <MenuItem value="">All</MenuItem>
          {SOURCES.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
        </TextField>

        <TextField
          select size="small" label="Status" sx={{ minWidth: 140 }}
          value={filters.status}
          onChange={(e) => set("status", e.target.value as IncidentFilterState["status"])}
        >
          <MenuItem value="">All</MenuItem>
          {STATUSES.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
        </TextField>

        <TextField
          size="small" type="date" label="Active from" value={filters.fromDate}
          onChange={(e) => set("fromDate", e.target.value)} slotProps={{
          inputLabel: { shrink: true }
        }}
        />
        <TextField
          size="small" type="date" label="Active to" value={filters.toDate}
          onChange={(e) => set("toDate", e.target.value)} slotProps={{
          inputLabel: { shrink: true }
        }}
        />

        <TextField
          size="small" label="Search summary or correlation id" sx={{ minWidth: 280, flexGrow: 1 }}
          value={filters.search} onChange={(e) => set("search", e.target.value)}
        />

        <Box sx={{ display: "flex", gap: 1 }}>
          <Button size="small" startIcon={<RestartAltIcon />} onClick={onReset} disabled={disabled}>
            Reset
          </Button>
          <Button
            size="small" variant="outlined" startIcon={<FileDownloadOutlinedIcon />}
            onClick={onExport} disabled={disabled || exporting}
          >
            {exporting ? "Exporting..." : "Export CSV"}
          </Button>
        </Box>
      </Stack>
    </Paper>
  );
}
