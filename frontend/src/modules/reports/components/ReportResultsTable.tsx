import { useMemo, useState } from "react";
import {
  Box, Paper, Typography, Table, TableHead, TableBody, TableRow, TableCell, TableContainer,
  Checkbox, Chip, Tooltip, IconButton, Menu, MenuItem, Select, FormControl, Button, Alert, Link
} from "@mui/material";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import ViewColumnIcon from "@mui/icons-material/ViewColumn";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import { brandColors } from "../../../theme";
import { StatusBadge, CategoryBadge } from "../../../components/StatusBadge";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { ReportingService } from "../services/ReportingService";
import { ResultRecordItem, ResultRecordSearchParams, ResultRecordSearchResponse } from "../types/reportingTypes";

const ROWS_PER_PAGE_OPTIONS = [10, 25, 50, 100];

type ColumnKey =
  | "dateTime" | "reference" | "subject" | "type" | "test" | "result" | "unit"
  | "resultLevel" | "status" | "approvedBy" | "approvalDate";

const COLUMN_LABELS: Record<ColumnKey, string> = {
  dateTime: "Date/Time",
  reference: "Sample/Reference",
  subject: "Item/Location",
  type: "Type",
  test: "Test",
  result: "Result",
  unit: "Unit",
  resultLevel: "Result Level",
  status: "Status",
  approvedBy: "Approved By",
  approvalDate: "Approval Date"
};
const ALL_COLUMNS = Object.keys(COLUMN_LABELS) as ColumnKey[];

function formatDateTime(iso: string): { date: string; time: string } {
  const d = new Date(iso);
  return {
    date: d.toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" }),
    time: d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit", hour12: false })
  };
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" });
}

interface ReportResultsTableProps {
  results: ResultRecordSearchResponse | null;
  loading: boolean;
  error: string | null;
  appliedParams: ResultRecordSearchParams;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  onClearFilters: () => void;
}

export function ReportResultsTable({ results, loading, error, appliedParams, onPageChange, onPageSizeChange, onClearFilters }: ReportResultsTableProps) {
  const [visibleColumns, setVisibleColumns] = useState<Record<ColumnKey, boolean>>(
    Object.fromEntries(ALL_COLUMNS.map((c) => [c, true])) as Record<ColumnKey, boolean>
  );
  const [columnsMenuAnchor, setColumnsMenuAnchor] = useState<HTMLElement | null>(null);
  const [rowMenu, setRowMenu] = useState<{ anchor: HTMLElement; row: ResultRecordItem } | null>(null);

  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [selectAllMatching, setSelectAllMatching] = useState(false);

  const [exportConfirmOpen, setExportConfirmOpen] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);

  const items = results?.items ?? [];
  const totalCount = results?.totalCount ?? 0;
  const page = appliedParams.page ?? 1;
  const pageSize = appliedParams.pageSize ?? 25;

  const approvedVisibleIds = useMemo(() => items.filter((r) => r.approvalStatus === "Approved").map((r) => r.id), [items]);
  const allVisibleSelected = approvedVisibleIds.length > 0 && approvedVisibleIds.every((id) => selectedIds.has(id));
  const someVisibleSelected = approvedVisibleIds.some((id) => selectedIds.has(id));

  const selectionCount = selectAllMatching ? totalCount : selectedIds.size;

  const toggleRow = (row: ResultRecordItem) => {
    if (row.approvalStatus !== "Approved") return;
    setSelectAllMatching(false);
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(row.id)) next.delete(row.id); else next.add(row.id);
      return next;
    });
  };

  const toggleVisiblePage = () => {
    setSelectAllMatching(false);
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (allVisibleSelected) approvedVisibleIds.forEach((id) => next.delete(id));
      else approvedVisibleIds.forEach((id) => next.add(id));
      return next;
    });
  };

  const selectAllMatchingRecords = () => {
    setSelectedIds(new Set());
    setSelectAllMatching(true);
  };

  const clearSelection = () => {
    setSelectedIds(new Set());
    setSelectAllMatching(false);
  };

  const runExport = async () => {
    setExportConfirmOpen(false);
    setExporting(true);
    setExportError(null);
    try {
      await ReportingService.exportCsv(appliedParams);
    } catch (err) {
      setExportError(err instanceof Error ? err.message : "Export failed.");
    } finally {
      setExporting(false);
    }
  };

  const pageStart = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const pageEnd = Math.min(page * pageSize, totalCount);
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <Paper sx={{ p: 2.5 }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 1, mb: 1.5 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.25 }}>
          <Typography sx={{ fontSize: 16, fontWeight: 700, color: brandColors.sectionTitle }}>Search Results</Typography>
          <Chip size="small" label={`${totalCount} Records Found`} sx={{ bgcolor: brandColors.causeBadgeBg, color: brandColors.causeBadgeText, fontWeight: 700 }} />
        </Box>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Button size="small" startIcon={<ViewColumnIcon />} onClick={(e) => setColumnsMenuAnchor(e.currentTarget)}>
            Columns
          </Button>
          <Button
            size="small" variant="contained" startIcon={<FileDownloadIcon />}
            disabled={exporting || totalCount === 0}
            onClick={() => setExportConfirmOpen(true)}
          >
            Export Data (CSV)
          </Button>
        </Box>
      </Box>

      <Menu anchorEl={columnsMenuAnchor} open={!!columnsMenuAnchor} onClose={() => setColumnsMenuAnchor(null)}>
        {ALL_COLUMNS.map((col) => (
          <MenuItem key={col} onClick={() => setVisibleColumns((v) => ({ ...v, [col]: !v[col] }))} dense>
            <Checkbox size="small" checked={visibleColumns[col]} />
            {COLUMN_LABELS[col]}
          </MenuItem>
        ))}
      </Menu>

      {exportError && <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => setExportError(null)}>{exportError}</Alert>}

      <Box sx={{ display: "flex", alignItems: "center", gap: 2, mb: 1, flexWrap: "wrap" }}>
        <Box sx={{ display: "flex", alignItems: "center" }}>
          <Checkbox
            size="small"
            checked={allVisibleSelected}
            indeterminate={!allVisibleSelected && someVisibleSelected}
            onChange={toggleVisiblePage}
            disabled={approvedVisibleIds.length === 0}
          />
          <Typography sx={{ fontSize: 13 }}>Select All (Visible)</Typography>
        </Box>

        <Tooltip title="Selects every Approved result matching your current filters, not just the loaded page.">
          <Link component="button" type="button" onClick={selectAllMatchingRecords} sx={{ fontSize: 13 }} underline="hover">
            Select All {totalCount} Matching Records
          </Link>
        </Tooltip>

        {selectionCount > 0 && (
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
            {selectAllMatching ? `All ${totalCount} selected` : `${selectionCount} selected`}
            {" · "}
            <Link component="button" type="button" onClick={clearSelection} sx={{ fontSize: 13 }} underline="hover">
              Clear
            </Link>
          </Typography>
        )}
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      {!error && !loading && items.length === 0 && (
        <Box sx={{ textAlign: "center", py: 6 }}>
          <Typography sx={{ fontWeight: 600, mb: 0.5 }}>No results found</Typography>
          <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 2 }}>
            Try widening the date range or clearing some filters.
          </Typography>
          <Button variant="outlined" onClick={onClearFilters}>Clear Filters</Button>
        </Box>
      )}

      {!error && (loading || items.length > 0) && (
        <>
          <TableContainer sx={{ opacity: loading ? 0.6 : 1 }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell padding="checkbox" />
                  {visibleColumns.dateTime && <TableCell>{COLUMN_LABELS.dateTime}</TableCell>}
                  {visibleColumns.reference && <TableCell>{COLUMN_LABELS.reference}</TableCell>}
                  {visibleColumns.subject && <TableCell>{COLUMN_LABELS.subject}</TableCell>}
                  {visibleColumns.type && <TableCell>{COLUMN_LABELS.type}</TableCell>}
                  {visibleColumns.test && <TableCell>{COLUMN_LABELS.test}</TableCell>}
                  {visibleColumns.result && <TableCell>{COLUMN_LABELS.result}</TableCell>}
                  {visibleColumns.unit && <TableCell>{COLUMN_LABELS.unit}</TableCell>}
                  {visibleColumns.resultLevel && <TableCell>{COLUMN_LABELS.resultLevel}</TableCell>}
                  {visibleColumns.status && <TableCell>{COLUMN_LABELS.status}</TableCell>}
                  {visibleColumns.approvedBy && <TableCell>{COLUMN_LABELS.approvedBy}</TableCell>}
                  {visibleColumns.approvalDate && <TableCell>{COLUMN_LABELS.approvalDate}</TableCell>}
                  <TableCell padding="checkbox" />
                </TableRow>
              </TableHead>
              <TableBody>
                {items.map((row) => {
                  const isApproved = row.approvalStatus === "Approved";
                  const dt = formatDateTime(row.resultEnteredAt);
                  const checkbox = (
                    <Checkbox
                      size="small" checked={selectAllMatching || selectedIds.has(row.id)}
                      disabled={!isApproved} onChange={() => toggleRow(row)}
                    />
                  );

                  return (
                    <TableRow key={row.id} hover>
                      <TableCell padding="checkbox">
                        {isApproved ? checkbox : (
                          <Tooltip title="Only approved results can be added to a report">
                            <span>{checkbox}</span>
                          </Tooltip>
                        )}
                      </TableCell>
                      {visibleColumns.dateTime && (
                        <TableCell>
                          <Typography sx={{ fontSize: 13 }}>{dt.date}</Typography>
                          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>{dt.time}</Typography>
                        </TableCell>
                      )}
                      {visibleColumns.reference && <TableCell>{row.referenceNumber}</TableCell>}
                      {visibleColumns.subject && (
                        <TableCell>
                          <Typography sx={{ fontSize: 13 }}>{row.subjectName}</Typography>
                          {row.subjectDetail && <Typography sx={{ fontSize: 11, color: "text.secondary" }}>{row.subjectDetail}</Typography>}
                        </TableCell>
                      )}
                      {visibleColumns.type && <TableCell><CategoryBadge category={row.category} /></TableCell>}
                      {visibleColumns.test && <TableCell>{row.testCode}</TableCell>}
                      {visibleColumns.result && <TableCell>{row.reportedValue}</TableCell>}
                      {visibleColumns.unit && <TableCell>{row.unit ?? "—"}</TableCell>}
                      {visibleColumns.resultLevel && (
                        <TableCell>
                          {row.resultLevel === "NotApplicable" ? "—" : <StatusBadge status={row.resultLevel} />}
                        </TableCell>
                      )}
                      {visibleColumns.status && <TableCell><StatusBadge status={row.approvalStatus} /></TableCell>}
                      {visibleColumns.approvedBy && <TableCell>{row.approvedByName ?? "—"}</TableCell>}
                      {visibleColumns.approvalDate && <TableCell>{row.approvedAt ? formatDate(row.approvedAt) : "—"}</TableCell>}
                      <TableCell padding="checkbox">
                        <IconButton size="small" onClick={(e) => setRowMenu({ anchor: e.currentTarget, row })}>
                          <MoreVertIcon fontSize="small" />
                        </IconButton>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>

          <Menu anchorEl={rowMenu?.anchor} open={!!rowMenu} onClose={() => setRowMenu(null)}>
            <MenuItem
              onClick={() => {
                if (rowMenu) navigator.clipboard?.writeText(rowMenu.row.referenceNumber);
                setRowMenu(null);
              }}
            >
              Copy Reference Number
            </MenuItem>
          </Menu>

          <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mt: 2, flexWrap: "wrap", gap: 1 }}>
            <FormControl size="small">
              <Select value={pageSize} onChange={(e) => onPageSizeChange(Number(e.target.value))}>
                {ROWS_PER_PAGE_OPTIONS.map((n) => <MenuItem key={n} value={n}>{n} / page</MenuItem>)}
              </Select>
            </FormControl>

            <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                {pageStart} - {pageEnd} of {totalCount}
              </Typography>
              <IconButton size="small" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
                <ChevronLeftIcon fontSize="small" />
              </IconButton>
              <IconButton size="small" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
                <ChevronRightIcon fontSize="small" />
              </IconButton>
            </Box>
          </Box>
        </>
      )}

      <ConfirmationDialog
        open={exportConfirmOpen}
        message={`This will export ${totalCount} row${totalCount === 1 ? "" : "s"} matching your current filters as a CSV file. This export will be logged.`}
        onConfirm={runExport}
        onCancel={() => setExportConfirmOpen(false)}
      />
    </Paper>
  );
}
