import { useMemo, useState } from "react";
import {
  Box, Paper, Typography, Chip, Checkbox, Tooltip, IconButton, Menu, MenuItem, Select, FormControl, Button, Alert, Link, Stack, useTheme
} from "@mui/material";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import ViewColumnIcon from "@mui/icons-material/ViewColumn";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import ShowChartIcon from "@mui/icons-material/ShowChart";
import VisibilityIcon from "@mui/icons-material/Visibility";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import { brandColors } from "../../../theme";
import { StatusBadge, CategoryBadge } from "../../../components/StatusBadge";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { RecordDetailDialog } from "./RecordDetailDialog";
import { ReportingService } from "../services/ReportingService";
import { exportResultsPdf } from "../utils/exportPdf";
import { useAuth } from "../../../contexts/AuthContext";
import { ResultRecordItem, ResultRecordSearchParams, ResultRecordSearchResponse } from "../types/reportingTypes";
import { DataTable, Column } from "../../../components/DataTable";

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

function summarizeFilters(params: ResultRecordSearchParams): string {
  const parts: string[] = [];
  if (params.search) parts.push(`Search: "${params.search}"`);
  if (params.category) parts.push(`Category: ${params.category}`);
  if (params.testCode) parts.push(`Test: ${params.testCode}`);
  if (params.resultLevel) parts.push(`Level: ${params.resultLevel}`);
  if (params.approvalStatus) parts.push(`Status: ${params.approvalStatus}`);
  if (params.fromDate || params.toDate) {
    const from = params.fromDate ? new Date(params.fromDate).toLocaleDateString("en-GB") : "Start";
    const to = params.toDate ? new Date(params.toDate).toLocaleDateString("en-GB") : "Present";
    parts.push(`Date Range: ${from} – ${to}`);
  }
  return parts.length > 0 ? parts.join(" | ") : "All Active Results";
}

interface ReportResultsTableProps {
  results: ResultRecordSearchResponse | null;
  loading: boolean;
  error: string | null;
  appliedParams: ResultRecordSearchParams;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  onClearFilters: () => void;
  onAnalyzeTrend?: (testCode: string, subjectName: string) => void;
}

export function ReportResultsTable({
  results,
  loading,
  error,
  appliedParams,
  onPageChange,
  onPageSizeChange,
  onClearFilters,
  onAnalyzeTrend
}: ReportResultsTableProps) {
  const theme = useTheme();
  const { fullName } = useAuth();
  const [visibleColumns, setVisibleColumns] = useState<Record<ColumnKey, boolean>>(
    Object.fromEntries(ALL_COLUMNS.map((c) => [c, true])) as Record<ColumnKey, boolean>
  );
  const [columnsMenuAnchor, setColumnsMenuAnchor] = useState<HTMLElement | null>(null);
  const [rowMenu, setRowMenu] = useState<{ anchor: HTMLElement; row: ResultRecordItem } | null>(null);
  const [detailRecord, setDetailRecord] = useState<ResultRecordItem | null>(null);

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
  const selectedRecords = useMemo(() => items.filter((r) => selectedIds.has(r.id)), [items, selectedIds]);

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

  const handleExportCurrentPdf = () => {
    exportResultsPdf(items, {
      title: "Laboratory Results Export",
      criteriaSummary: summarizeFilters(appliedParams),
      generatedBy: fullName || "Authorized User",
      isSelection: false
    });
  };

  const handleExportSelectedCsv = () => {
    if (selectAllMatching) {
      runExport();
      return;
    }
    if (selectedRecords.length === 0) return;
    const headers = [
      "Date/Time", "Reference", "Subject", "Subject Detail", "Category", "Test Code", "Test Name",
      "Reported Value", "Unit", "Result Level", "Alert Limit", "Action Limit", "Spec Limit",
      "Status", "Analyst", "Approved By", "Approval Date"
    ];
    const rows = selectedRecords.map((r) => [
      `"${r.resultEnteredAt}"`,
      `"${r.referenceNumber}"`,
      `"${r.subjectName}"`,
      `"${r.subjectDetail ?? ""}"`,
      `"${r.category}"`,
      `"${r.testCode}"`,
      `"${r.testDisplayName || r.testCode}"`,
      `"${r.reportedValue}"`,
      `"${r.unit ?? ""}"`,
      `"${r.resultLevel}"`,
      `"${r.alertLimit ?? ""}"`,
      `"${r.actionLimit ?? ""}"`,
      `"${r.specLimit ?? ""}"`,
      `"${r.approvalStatus}"`,
      `"${r.resultEnteredByName ?? ""}"`,
      `"${r.approvedByName ?? ""}"`,
      `"${r.approvedAt ?? ""}"`
    ]);
    const csvContent = [headers.join(","), ...rows.map((row) => row.join(","))].join("\n");
    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `selected_laboratory_results_${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  };

  const handleExportSelectedPdf = async () => {
    if (selectAllMatching) {
      setExporting(true);
      try {
        const res = await ReportingService.searchResults({ ...appliedParams, page: 1, pageSize: 200 });
        exportResultsPdf(res.items, {
          title: "Laboratory Results Export (All Matching)",
          criteriaSummary: summarizeFilters(appliedParams),
          generatedBy: fullName || "Authorized User",
          isSelection: true
        });
      } catch (err) {
        setExportError(err instanceof Error ? err.message : "Export failed.");
      } finally {
        setExporting(false);
      }
      return;
    }

    exportResultsPdf(selectedRecords, {
      title: "Laboratory Results Export (Selected Records)",
      criteriaSummary: `Export of ${selectedRecords.length} Selected Record${selectedRecords.length === 1 ? "" : "s"}`,
      generatedBy: fullName || "Authorized User",
      isSelection: true
    });
  };

  const pageStart = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const pageEnd = Math.min(page * pageSize, totalCount);
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  // Maps each toggleable ColumnKey to its real DataTable column definition -
  // visibleColumns (driven by the Columns menu) filters this list before
  // it's handed to DataTable; the trailing Actions column is appended
  // unconditionally since it isn't part of the toggle set.
  const columnDefs: { colKey: ColumnKey; column: Column<ResultRecordItem> }[] = [
    { colKey: "dateTime", column: { key: "resultEnteredAt", label: COLUMN_LABELS.dateTime, render: (row) => {
      const dt = formatDateTime(row.resultEnteredAt);
      return (
        <>
          <Typography sx={{ fontSize: 13 }}>{dt.date}</Typography>
          <Typography sx={{ fontSize: 11, color: "text.secondary" }}>{dt.time}</Typography>
        </>
      );
    } } },
    { colKey: "reference", column: { key: "referenceNumber", label: COLUMN_LABELS.reference, render: (row) => (
      <Link
        component="button"
        type="button"
        onClick={() => setDetailRecord(row)}
        sx={{ fontSize: 13, fontWeight: 600, color: theme.palette.primary.main }}
        underline="hover"
      >
        {row.referenceNumber}
      </Link>
    ) } },
    { colKey: "subject", column: { key: "subjectName", label: COLUMN_LABELS.subject, render: (row) => (
      <>
        <Typography sx={{ fontSize: 13 }}>{row.subjectName}</Typography>
        {row.subjectDetail && <Typography sx={{ fontSize: 11, color: "text.secondary" }}>{row.subjectDetail}</Typography>}
      </>
    ) } },
    { colKey: "type", column: { key: "category", label: COLUMN_LABELS.type, render: (row) => <CategoryBadge category={row.category} /> } },
    { colKey: "test", column: { key: "testCode", label: COLUMN_LABELS.test, render: (row) => row.testCode } },
    { colKey: "result", column: { key: "reportedValue", label: COLUMN_LABELS.result, render: (row) => <Box sx={{ fontWeight: 600 }}>{row.reportedValue}</Box> } },
    { colKey: "unit", column: { key: "unit", label: COLUMN_LABELS.unit, render: (row) => row.unit ?? "—" } },
    { colKey: "resultLevel", column: { key: "resultLevel", label: COLUMN_LABELS.resultLevel, render: (row) => (
      row.resultLevel === "NotApplicable" ? "—" : <StatusBadge status={row.resultLevel} />
    ) } },
    { colKey: "status", column: { key: "approvalStatus", label: COLUMN_LABELS.status, render: (row) => <StatusBadge status={row.approvalStatus} /> } },
    { colKey: "approvedBy", column: { key: "approvedByName", label: COLUMN_LABELS.approvedBy, render: (row) => row.approvedByName ?? "—" } },
    { colKey: "approvalDate", column: { key: "approvedAt", label: COLUMN_LABELS.approvalDate, render: (row) => row.approvedAt ? formatDate(row.approvedAt) : "—" } }
  ];

  const tableColumns: Column<ResultRecordItem>[] = [
    ...columnDefs.filter((d) => visibleColumns[d.colKey]).map((d) => d.column),
    {
      key: "id",
      label: "",
      align: "center",
      render: (row) => (
        <IconButton size="small" onClick={(e) => { e.stopPropagation(); setRowMenu({ anchor: e.currentTarget, row }); }}>
          <MoreVertIcon fontSize="small" />
        </IconButton>
      )
    }
  ];

  return (
    <Paper sx={{ p: 2.5 }}>
      {/* Top Title & Mode A Export Toolbar (When No Rows Selected) */}
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 1, mb: 1.5 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.25 }}>
          <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main }}>Search Results</Typography>
          <Chip size="small" label={`${totalCount} Records Found`} sx={{ bgcolor: brandColors.causeBadgeBg, color: brandColors.causeBadgeText, fontWeight: 700 }} />
        </Box>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Button size="small" startIcon={<ViewColumnIcon />} onClick={(e) => setColumnsMenuAnchor(e.currentTarget)}>
            Columns
          </Button>
          <Button
            size="small"
            variant="outlined"
            startIcon={<PictureAsPdfIcon />}
            disabled={totalCount === 0}
            onClick={handleExportCurrentPdf}
          >
            Export PDF
          </Button>
          <Button
            size="small"
            variant="contained"
            startIcon={<FileDownloadIcon />}
            disabled={exporting || totalCount === 0}
            onClick={() => setExportConfirmOpen(true)}
          >
            Export CSV
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

      {/* Multi-Select Toolbar & Mode B Export Actions (When Rows Selected) */}
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 1.5, flexWrap: "wrap", gap: 1, bgcolor: selectionCount > 0 ? theme.custom.status.purple.bg : "transparent", p: selectionCount > 0 ? 1 : 0, borderRadius: 1 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 2, flexWrap: "wrap" }}>
          <Tooltip title="Selects every Approved result matching your current filters, not just the loaded page.">
            <Link component="button" type="button" onClick={selectAllMatchingRecords} sx={{ fontSize: 13 }} underline="hover">
              Select All {totalCount} Matching Records
            </Link>
          </Tooltip>

          {selectionCount > 0 && (
            <Typography sx={{ fontSize: 13, color: "text.secondary", fontWeight: 600 }}>
              {selectAllMatching ? `All ${totalCount} selected` : `${selectionCount} selected`}
              {" · "}
              <Link component="button" type="button" onClick={clearSelection} sx={{ fontSize: 13 }} underline="hover">
                Clear
              </Link>
            </Typography>
          )}
        </Box>

        {selectionCount > 0 && (
          <Stack direction="row" spacing={1}>
            <Button
              size="small"
              variant="contained"
              startIcon={<PictureAsPdfIcon />}
              onClick={handleExportSelectedPdf}
            >
              Export Selected PDF
            </Button>
            <Button
              size="small"
              variant="outlined"
              startIcon={<FileDownloadIcon />}
              onClick={handleExportSelectedCsv}
            >
              Export Selected CSV
            </Button>
            {onAnalyzeTrend && (
              <Button
                size="small"
                variant="outlined"
                startIcon={<ShowChartIcon />}
                onClick={() => {
                  const target = selectedRecords[0] ?? items[0];
                  if (target && onAnalyzeTrend) onAnalyzeTrend(target.testCode, target.subjectName);
                }}
              >
                Analyze Selected
              </Button>
            )}
          </Stack>
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
          <Box sx={{ opacity: loading ? 0.6 : 1 }}>
            <DataTable
              columns={tableColumns}
              rows={items}
              getRowId={(row) => row.id}
              selection={{
                isSelected: (row) => selectAllMatching || selectedIds.has(row.id),
                onToggle: (row) => toggleRow(row),
                isSelectable: (row) => row.approvalStatus === "Approved",
                headerChecked: allVisibleSelected,
                headerIndeterminate: !allVisibleSelected && someVisibleSelected,
                onToggleAll: toggleVisiblePage
              }}
            />
          </Box>

          <Menu anchorEl={rowMenu?.anchor} open={!!rowMenu} onClose={() => setRowMenu(null)}>
            <MenuItem
              onClick={() => {
                if (rowMenu) setDetailRecord(rowMenu.row);
                setRowMenu(null);
              }}
            >
              <VisibilityIcon fontSize="small" sx={{ mr: 1, color: "text.secondary" }} />
              View Record Details
            </MenuItem>
            {onAnalyzeTrend && (
              <MenuItem
                onClick={() => {
                  if (rowMenu && onAnalyzeTrend) onAnalyzeTrend(rowMenu.row.testCode, rowMenu.row.subjectName);
                  setRowMenu(null);
                }}
              >
                <ShowChartIcon fontSize="small" sx={{ mr: 1, color: "text.secondary" }} />
                Analyze Trend
              </MenuItem>
            )}
            <MenuItem
              onClick={() => {
                if (rowMenu) navigator.clipboard?.writeText(rowMenu.row.referenceNumber);
                setRowMenu(null);
              }}
            >
              <ContentCopyIcon fontSize="small" sx={{ mr: 1, color: "text.secondary" }} />
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

      <RecordDetailDialog
        open={!!detailRecord}
        onClose={() => setDetailRecord(null)}
        record={detailRecord}
      />
    </Paper>
  );
}
