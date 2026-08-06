import { useState } from "react";
import {
  Paper, Stack, TextField, Select, MenuItem, Button, ToggleButtonGroup, ToggleButton,
  Popover, FormGroup, FormControlLabel, Checkbox, InputAdornment, Box
} from "@mui/material";
import QrCodeScannerIcon from "@mui/icons-material/QrCodeScanner";
import SearchIcon from "@mui/icons-material/Search";
import FilterListIcon from "@mui/icons-material/FilterList";
import ViewListIcon from "@mui/icons-material/ViewList";
import ViewModuleIcon from "@mui/icons-material/ViewModule";
import ViewKanbanIcon from "@mui/icons-material/ViewKanban";
import ViewColumnIcon from "@mui/icons-material/ViewColumn";
import FileDownloadOutlinedIcon from "@mui/icons-material/FileDownloadOutlined";

export type WorkspaceView = "table" | "card" | "kanban";

export interface WorkspaceColumn { key: string; label: string }

// Item/Reference is always shown - these are the toggleable ones for the
// Columns picker (Table view only).
export const WORKSPACE_COLUMNS: WorkspaceColumn[] = [
  { key: "category", label: "Category" },
  { key: "batch", label: "Batch No." },
  { key: "control", label: "Control No." },
  { key: "cause", label: "Cause of Testing" },
  { key: "receivedAt", label: "Received At" },
  { key: "tests", label: "Tests" },
  { key: "assignedTo", label: "Assigned To" },
  { key: "status", label: "Overall Status" }
];

const SAMPLE_STATUSES = ["Received", "InTesting", "UnderReview", "UnderApproval", "Approved", "Rejected", "RetestRequested"];
const TEST_STATUSES = ["Pending", "InProgress", "ResultEntered", "Reviewed", "Approved", "Rejected", "RetestRequested"];

interface Props {
  search: string; onSearchChange: (v: string) => void;
  fromDate: string; onFromDateChange: (v: string) => void;
  toDate: string; onToDateChange: (v: string) => void;
  statusFilter: string; onStatusFilterChange: (v: string) => void;
  testStatusFilter: string; onTestStatusFilterChange: (v: string) => void;
  scope: "all" | "mine"; onScopeChange: (v: "all" | "mine") => void;
  view: WorkspaceView; onViewChange: (v: WorkspaceView) => void;
  visibleColumns: Set<string>; onVisibleColumnsChange: (cols: Set<string>) => void;
  onExport: () => void;
}

// Toolbar is fully controlled - all filter/view state lives in
// TestingWorkspacePage, same as the search/fromDate/toDate state it
// already owned before this redesign. This component only renders inputs.
export function WorkspaceToolbar({
  search, onSearchChange, fromDate, onFromDateChange, toDate, onToDateChange,
  statusFilter, onStatusFilterChange, testStatusFilter, onTestStatusFilterChange,
  scope, onScopeChange, view, onViewChange, visibleColumns, onVisibleColumnsChange, onExport
}: Props) {
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [columnsAnchor, setColumnsAnchor] = useState<HTMLElement | null>(null);

  const toggleColumn = (key: string) => {
    const next = new Set(visibleColumns);
    if (next.has(key)) next.delete(key); else next.add(key);
    onVisibleColumnsChange(next);
  };

  // A barcode scanner types the code then emits Enter - reuse the same
  // `search` filter the text search box already drives rather than a
  // second parallel filter mechanism.
  const handleScanKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter") onSearchChange((e.target as HTMLInputElement).value);
  };

  return (
    <Paper sx={{ p: 1.5, mb: 2 }}>
      <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap" rowGap={1}>
        <TextField
          size="small" placeholder="Scan barcode…" onKeyDown={handleScanKeyDown} sx={{ width: 160 }}
          InputProps={{ startAdornment: <InputAdornment position="start"><QrCodeScannerIcon fontSize="small" /></InputAdornment> }}
        />
        <TextField
          size="small" placeholder="Search by item, reference, batch, or control number…" value={search}
          onChange={(e) => onSearchChange(e.target.value)} sx={{ minWidth: 280, flex: 1 }}
          InputProps={{ startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> }}
        />
        <Select size="small" value={scope} onChange={(e) => onScopeChange(e.target.value as "all" | "mine")} sx={{ minWidth: 140 }}>
          <MenuItem value="all">All Samples</MenuItem>
          <MenuItem value="mine">My Assigned</MenuItem>
        </Select>
        <Button size="small" startIcon={<FilterListIcon />} onClick={() => setFiltersOpen((o) => !o)} variant={filtersOpen ? "contained" : "outlined"}>
          Filters
        </Button>

        <Box sx={{ flex: 1 }} />

        <ToggleButtonGroup value={view} exclusive size="small" onChange={(_, v) => v && onViewChange(v)}>
          <ToggleButton value="table"><ViewListIcon fontSize="small" /></ToggleButton>
          <ToggleButton value="card"><ViewModuleIcon fontSize="small" /></ToggleButton>
          <ToggleButton value="kanban"><ViewKanbanIcon fontSize="small" /></ToggleButton>
        </ToggleButtonGroup>

        {view === "table" && (
          <>
            <Button size="small" startIcon={<ViewColumnIcon />} onClick={(e) => setColumnsAnchor(e.currentTarget)}>Columns</Button>
            <Popover open={!!columnsAnchor} anchorEl={columnsAnchor} onClose={() => setColumnsAnchor(null)}>
              <FormGroup sx={{ p: 1.5 }}>
                {WORKSPACE_COLUMNS.map((c) => (
                  <FormControlLabel
                    key={c.key} label={c.label}
                    control={<Checkbox size="small" checked={visibleColumns.has(c.key)} onChange={() => toggleColumn(c.key)} />}
                  />
                ))}
              </FormGroup>
            </Popover>
          </>
        )}

        <Button size="small" startIcon={<FileDownloadOutlinedIcon />} onClick={onExport}>Export</Button>
      </Stack>

      {filtersOpen && (
        <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap" rowGap={1} sx={{ mt: 1.5, pt: 1.5, borderTop: "1px solid #f0f0f0" }}>
          <Select size="small" displayEmpty value={statusFilter} onChange={(e) => onStatusFilterChange(e.target.value)} sx={{ minWidth: 160 }}>
            <MenuItem value="">All Statuses</MenuItem>
            {SAMPLE_STATUSES.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
          </Select>
          <Select size="small" displayEmpty value={testStatusFilter} onChange={(e) => onTestStatusFilterChange(e.target.value)} sx={{ minWidth: 160 }}>
            <MenuItem value="">All Test Statuses</MenuItem>
            {TEST_STATUSES.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
          </Select>
          <TextField size="small" type="date" label="From" InputLabelProps={{ shrink: true }} value={fromDate} onChange={(e) => onFromDateChange(e.target.value)} />
          <TextField size="small" type="date" label="To" InputLabelProps={{ shrink: true }} value={toDate} onChange={(e) => onToDateChange(e.target.value)} />
        </Stack>
      )}
    </Paper>
  );
}
