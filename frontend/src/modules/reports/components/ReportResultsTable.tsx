import { useMemo, useState } from "react";
import {
  Box, Paper, Typography, Chip, Checkbox, Tooltip, IconButton, Menu, MenuItem, Button, Alert, Link, Stack, useTheme
} from "@mui/material";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import ViewColumnIcon from "@mui/icons-material/ViewColumn";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
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
import { ResultRecordItem, ResultRecordSearchParams, ResultRecordSearchResponse, SampleCategory } from "../types/reportingTypes";
import { RegisterTable, type RegisterColumn } from "../../../components/lab/RegisterTable";
import { buildCsv, downloadCsv } from "../../../components/lab/table/exportCsv";
import { exportLogService } from "../../../components/lab/table/services/exportLogService";
import { TABLES } from "../../../components/lab/table/tableRegistry";
import { useRowSelection } from "../../../components/lab/table/useRowSelection";
import { fetchAllPages } from "../../../services/fetchAllPages";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { useTranslation } from "../../../i18n";

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
    time: d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit", hour12: false, timeZoneName: "short" })
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
  onAnalyzeTrend?: (testCode: string, subjectName: string, category: SampleCategory) => void;
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
  const { t } = useTranslation("reports");
  const theme = useTheme();
  const { fullName } = useAuth();
  const [visibleColumns, setVisibleColumns] = useState<Record<ColumnKey, boolean>>(
    Object.fromEntries(ALL_COLUMNS.map((c) => [c, true])) as Record<ColumnKey, boolean>
  );
  const [columnsMenuAnchor, setColumnsMenuAnchor] = useState<HTMLElement | null>(null);
  const [rowMenu, setRowMenu] = useState<{ anchor: HTMLElement; row: ResultRecordItem } | null>(null);
  const [detailRecord, setDetailRecord] = useState<ResultRecordItem | null>(null);

  const resetKey = JSON.stringify({
    search: appliedParams.search,
    category: appliedParams.category,
    testCode: appliedParams.testCode,
    resultLevel: appliedParams.resultLevel,
    sampleStatus: appliedParams.sampleStatus,
    approvalStatus: appliedParams.approvalStatus,
    fromDate: appliedParams.fromDate,
    toDate: appliedParams.toDate,
    subjectName: appliedParams.subjectName,
    resultKind: appliedParams.resultKind
  });
  const selection = useRowSelection<string | number>(resetKey);

  const [exportConfirmOpen, setExportConfirmOpen] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);

  const items = useMemo(() => results?.items ?? [], [results]);
  const totalCount = results?.totalCount ?? 0;
  const page = results?.page ?? appliedParams.page ?? 1;
  const pageSize = results?.pageSize ?? appliedParams.pageSize ?? 25;

  const selectionCount = selection.count;
  const selectedRecords = useMemo(
    () => items.filter((r) => selection.selected.has(r.id)),
    [items, selection.selected]
  );

  const runExport = async () => {
    setExportConfirmOpen(false);
    setExporting(true);
    setExportError(null);
    try {
      await ReportingService.exportCsv(appliedParams);
    } catch (err) {
      setExportError(err instanceof Error ? err.message : t("search.errors.exportFailed"));
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

  const handleExportSelectedCsv = async () => {
    if (selection.allMatching) {
      await runExport();
      return;
    }
    if (selectedRecords.length === 0) return;
    const headers = [
      "Date/Time", "Reference", "Subject", "Subject Detail", "Category", "Test Code", "Test Name",
      "Reported Value", "Unit", "Result Level", "Alert Limit", "Action Limit", "Spec Limit",
      "Status", "Analyst", "Approved By", "Approval Date"
    ];
    setExportError(null);
    let logResult;
    try {
      logResult = await exportLogService.log({
        tableKey: "reports.results",
        filterJson: JSON.stringify(appliedParams),
        columns: headers,
        rowCount: selectedRecords.length,
        recordIds: selectedRecords.map((r) => String(r.id))
      });
    } catch (err) {
      setExportError(err instanceof Error ? err.message : t("search.errors.exportFailed"));
      return;
    }
    const headerRows: string[][] = [
      ["Exported by", logResult.exportedBy],
      ["Exported at", `${logResult.serverTimeUtc} (${logResult.timeZoneId})`],
      ["List", TABLES["reports.results"]?.title ?? "reports.results"],
      ["Filters", summarizeFilters(appliedParams)],
      ["Rows", String(selectedRecords.length)],
      ["Export ID", String(logResult.exportId)]
    ];
    const rows = selectedRecords.map((r) => [
      r.resultEnteredAt,
      r.referenceNumber,
      r.subjectName,
      r.subjectDetail ?? "",
      r.category,
      r.testCode,
      r.testDisplayName || r.testCode,
      r.reportedValue,
      r.unit ?? "",
      r.resultLevel,
      r.alertLimit ?? "",
      r.actionLimit ?? "",
      r.specLimit ?? "",
      r.approvalStatus,
      r.resultEnteredByName ?? "",
      r.approvedByName ?? "",
      r.approvedAt ?? ""
    ]);
    const csv = buildCsv(headerRows, headers, rows);
    downloadCsv(`selected_laboratory_results_${new Date().toISOString().slice(0, 10)}.csv`, csv);
  };

  const handleExportSelectedPdf = async () => {
    if (selection.allMatching) {
      setExporting(true);
      try {
        // Every matching result, page by page - not just the first page.
        const allMatching = await fetchAllPages((page, pageSize) => ReportingService.searchResults({ ...appliedParams, page, pageSize }));
        exportResultsPdf(allMatching, {
          title: "Laboratory Results Export (All Matching)",
          criteriaSummary: summarizeFilters(appliedParams),
          generatedBy: fullName || "Authorized User",
          isSelection: true
        });
      } catch (err) {
        setExportError(err instanceof Error ? err.message : t("search.errors.exportFailed"));
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

  // Maps each toggleable ColumnKey to its real RegisterTable column definition -
  // visibleColumns (driven by the Columns menu) filters this list before
  // it's handed to RegisterTable; the trailing Actions column is appended
  // unconditionally since it isn't part of the toggle set.
  const columnDefs: { colKey: ColumnKey; column: RegisterColumn<ResultRecordItem> }[] = [
    { colKey: "dateTime", column: { key: "resultEnteredAt", label: t("search.table.columns.dateTime"), render: (row) => {
      const dt = formatDateTime(row.resultEnteredAt);
      return (
        <>
          <Typography sx={{ fontSize: 13 }}>{dt.date}</Typography>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{dt.time}</Typography>
        </>
      );
    } } },
    { colKey: "reference", column: { key: "referenceNumber", label: t("search.table.columns.reference"), render: (row) => (
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
    { colKey: "subject", column: { key: "subjectName", label: t("search.table.columns.subject"), render: (row) => (
      <>
        <Typography sx={{ fontSize: 13 }}>{row.subjectName}</Typography>
        {row.subjectDetail && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{row.subjectDetail}</Typography>}
      </>
    ) } },
    { colKey: "type", column: { key: "category", label: t("search.table.columns.type"), render: (row) => <CategoryBadge category={row.category} /> } },
    { colKey: "test", column: { key: "testCode", label: t("search.table.columns.test"), render: (row) => row.testCode } },
    { colKey: "result", column: { key: "reportedValue", label: t("search.table.columns.result"), render: (row) => <Box sx={{ fontWeight: 600 }}>{row.reportedValue}</Box> } },
    { colKey: "unit", column: { key: "unit", label: t("search.table.columns.unit"), render: (row) => row.unit ?? "—" } },
    { colKey: "resultLevel", column: { key: "resultLevel", label: t("search.table.columns.resultLevel"), render: (row) => (
      row.resultLevel === "NotApplicable" ? "—" : <StatusBadge status={row.resultLevel} />
    ) } },
    { colKey: "status", column: { key: "approvalStatus", label: t("search.table.columns.status"), render: (row) => <StatusBadge status={row.approvalStatus} /> } },
    { colKey: "approvedBy", column: { key: "approvedByName", label: t("search.table.columns.approvedBy"), render: (row) => row.approvedByName ?? "—" } },
    { colKey: "approvalDate", column: { key: "approvedAt", label: t("search.table.columns.approvalDate"), render: (row) => row.approvedAt ? formatDateTimeWithTimeZone(row.approvedAt) : "—" } }
  ];

  const tableColumns: RegisterColumn<ResultRecordItem>[] = [
    ...columnDefs.filter((d) => visibleColumns[d.colKey]).map((d) => d.column),
    {
      key: "id",
      label: "",
      align: "center",
      render: (row) => (
        <IconButton aria-label={t("search.table.rowActionsAria", { reference: row.referenceNumber })} size="small" onClick={(e) => { e.stopPropagation(); setRowMenu({ anchor: e.currentTarget, row }); }}>
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
          <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main }}>{t("search.table.title")}</Typography>
          <Chip size="small" label={t("search.table.recordsFound", { count: totalCount })} sx={{ bgcolor: brandColors.causeBadgeBg, color: brandColors.causeBadgeText, fontWeight: 700 }} />
        </Box>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Button size="small" startIcon={<ViewColumnIcon />} onClick={(e) => setColumnsMenuAnchor(e.currentTarget)}>
            {t("search.table.columnsButton")}
          </Button>
          <Button
            size="small"
            variant="outlined"
            startIcon={<PictureAsPdfIcon />}
            disabled={totalCount === 0}
            onClick={handleExportCurrentPdf}
          >
            {t("search.table.exportPdf")}
          </Button>
          <Button
            size="small"
            variant="contained"
            startIcon={<FileDownloadIcon />}
            disabled={exporting || totalCount === 0}
            onClick={() => setExportConfirmOpen(true)}
          >
            {t("search.table.exportCsv")}
          </Button>
        </Box>
      </Box>

      <Menu anchorEl={columnsMenuAnchor} open={!!columnsMenuAnchor} onClose={() => setColumnsMenuAnchor(null)}>
        {ALL_COLUMNS.map((col) => (
          <MenuItem key={col} onClick={() => setVisibleColumns((v) => ({ ...v, [col]: !v[col] }))} dense>
            <Checkbox size="small" checked={visibleColumns[col]} />
            {t(`search.table.columns.${col}`)}
          </MenuItem>
        ))}
      </Menu>

      {exportError && <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => setExportError(null)}>{exportError}</Alert>}

      {/* Multi-Select Toolbar & Mode B Export Actions (When Rows Selected) */}
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 1.5, flexWrap: "wrap", gap: 1, bgcolor: selectionCount > 0 ? theme.custom.status.purple.bg : "transparent", p: selectionCount > 0 ? 1 : 0, borderRadius: 1 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 2, flexWrap: "wrap" }}>
          <Tooltip title={t("search.table.selectAllTooltip")}>
            <Link component="button" type="button" onClick={() => selection.selectAllMatching(totalCount)} sx={{ fontSize: 13 }} underline="hover">
              {t("search.table.selectAllMatching", { count: totalCount })}
            </Link>
          </Tooltip>

          {selectionCount > 0 && (
            <Typography sx={{ fontSize: 13, color: "text.secondary", fontWeight: 600 }}>
              {selection.allMatching ? t("search.table.allSelected", { count: totalCount }) : t("search.table.someSelected", { count: selectionCount })}
              {" · "}
              <Link component="button" type="button" onClick={selection.clear} sx={{ fontSize: 13 }} underline="hover">
                {t("search.table.clear")}
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
              {t("search.table.exportSelectedPdf")}
            </Button>
            <Button
              size="small"
              variant="outlined"
              startIcon={<FileDownloadIcon />}
              onClick={handleExportSelectedCsv}
            >
              {t("search.table.exportSelectedCsv")}
            </Button>
            {onAnalyzeTrend && (
              <Button
                size="small"
                variant="outlined"
                startIcon={<ShowChartIcon />}
                onClick={() => {
                  const target = selectedRecords[0] ?? items[0];
                  if (target && onAnalyzeTrend) onAnalyzeTrend(target.testCode, target.subjectName, target.category);
                }}
              >
                {t("search.table.analyzeSelected")}
              </Button>
            )}
          </Stack>
        )}
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      {!error && !loading && items.length === 0 && (
        <Box sx={{ textAlign: "center", py: 6 }}>
          <Typography sx={{ fontWeight: 600, mb: 0.5 }}>{t("search.table.noResultsTitle")}</Typography>
          <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 2 }}>
            {t("search.table.noResultsDescription")}
          </Typography>
          <Button variant="outlined" onClick={onClearFilters}>{t("search.table.clearFilters")}</Button>
        </Box>
      )}

      {!error && (loading || items.length > 0) && (
        <>
          <Box sx={{ opacity: loading ? 0.6 : 1 }}>
            <RegisterTable
              columns={tableColumns}
              rows={items}
              getRowId={(row) => row.id}
              loading={loading}
              empty={{
                title: t("search.table.noResultsTitle"),
                description: t("search.table.noResultsDescription"),
                action: <Button variant="outlined" onClick={onClearFilters}>{t("search.table.clearFilters")}</Button>
              }}
              selection={selection}
              isSelectable={(row) => row.approvalStatus === "Approved"}
              serverPaged={{
                totalCount,
                page: Math.max(0, page - 1),
                onPageChange: (nextZeroBasedPage) => onPageChange(nextZeroBasedPage + 1),
                pageSize,
                onPageSizeChange,
                pageSizeOptions: ROWS_PER_PAGE_OPTIONS
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
              {t("search.table.viewRecordDetails")}
            </MenuItem>
            {onAnalyzeTrend && (
              <MenuItem
                onClick={() => {
                  if (rowMenu && onAnalyzeTrend) onAnalyzeTrend(rowMenu.row.testCode, rowMenu.row.subjectName, rowMenu.row.category);
                  setRowMenu(null);
                }}
              >
                <ShowChartIcon fontSize="small" sx={{ mr: 1, color: "text.secondary" }} />
                {t("search.table.analyzeTrend")}
              </MenuItem>
            )}
            <MenuItem
              onClick={() => {
                if (rowMenu) navigator.clipboard?.writeText(rowMenu.row.referenceNumber);
                setRowMenu(null);
              }}
            >
              <ContentCopyIcon fontSize="small" sx={{ mr: 1, color: "text.secondary" }} />
              {t("search.table.copyReferenceNumber")}
            </MenuItem>
          </Menu>
        </>
      )}

      <ConfirmationDialog
        open={exportConfirmOpen}
        message={t("search.table.exportConfirm", { count: totalCount })}
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
