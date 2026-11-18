import { useState } from "react";
import {
  Box, Paper, Typography, Chip, IconButton, Tooltip, Button, Alert, Stack,
  FormControl, Select, MenuItem, useTheme
} from "@mui/material";
import VisibilityIcon from "@mui/icons-material/Visibility";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import { ReferenceStrainDetail, ReferenceStrainListItem, ReferenceStrainSearchParams, ReferenceStrainSearchResponse } from "../types/referenceStrainTypes";
import { ReferenceStrainReportService } from "../services/ReferenceStrainReportService";
import { ReferenceStrainDetailDialog } from "./ReferenceStrainDetailDialog";
import { exportReferenceStrainPdf } from "../utils/exportReferenceStrainPdf";
import { useAuth } from "../../../contexts/AuthContext";
import { RegisterTable, type RegisterColumn } from "../../../components/lab/RegisterTable";
import { useRowSelection } from "../../../components/lab/table/useRowSelection";
import { useTranslation } from "../../../i18n";

const ROWS_PER_PAGE_OPTIONS = [10, 25, 50, 100];

function formatDate(iso?: string | null): string {
  if (!iso || iso.startsWith("0001")) return "—";
  try {
    return new Date(iso).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" });
  } catch {
    return iso;
  }
}

interface ReferenceStrainResultsTableProps {
  results: ReferenceStrainSearchResponse | null;
  loading: boolean;
  error: string | null;
  appliedParams: ReferenceStrainSearchParams;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  onClearFilters: () => void;
}

export function ReferenceStrainResultsTable({
  results,
  loading,
  error,
  appliedParams,
  onPageChange,
  onPageSizeChange,
  onClearFilters
}: ReferenceStrainResultsTableProps) {
  const { t } = useTranslation("reports");
  const theme = useTheme();
  const { fullName } = useAuth();
  const resetKey = JSON.stringify({
    search: appliedParams.search,
    organismId: appliedParams.organismId,
    approvalStatus: appliedParams.approvalStatus,
    isDestroyed: appliedParams.isDestroyed,
    receiptFromDate: appliedParams.receiptFromDate,
    receiptToDate: appliedParams.receiptToDate,
    usageFromDate: appliedParams.usageFromDate,
    usageToDate: appliedParams.usageToDate
  });
  const selection = useRowSelection<string | number>(resetKey);
  const [detailBatch, setDetailBatch] = useState<ReferenceStrainDetail | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);
  const [exportingCsv, setExportingCsv] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  const items = results?.items ?? [];
  const totalCount = results?.totalCount ?? 0;
  const page = results?.page ?? 1;
  const pageSize = results?.pageSize ?? 25;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const handleViewDetail = async (id: number) => {
    try {
      const detail = await ReferenceStrainReportService.getById(id);
      setDetailBatch(detail);
      setDetailOpen(true);
    } catch {
      // Error loading detail
    }
  };

  const handleExportCsv = async () => {
    setExportingCsv(true);
    setExportError(null);
    try {
      await ReferenceStrainReportService.exportCsv(appliedParams);
    } catch (err) {
      setExportError(err instanceof Error ? err.message : t("referenceStrain.errors.exportFailed"));
    } finally {
      setExportingCsv(false);
    }
  };

  const handleExportPdf = () => {
    const recordsToExport = selection.count > 0
      ? items.filter((r) => selection.isSelected(r.id))
      : items;

    exportReferenceStrainPdf(recordsToExport, {
      title: "Reference Microorganism Strains Report",
      criteriaSummary: `Filters: ${appliedParams.search ? `Search: "${appliedParams.search}"` : "All Strains"} • ${appliedParams.approvalStatus || "All Statuses"}`,
      generatedBy: fullName || "Authorized User",
      isSelection: selection.count > 0
    });
  };

  const columns: RegisterColumn<ReferenceStrainListItem>[] = [
    { key: "strainName", label: t("referenceStrain.table.columns.strainAtcc"), render: (r) => (
      <Box>
        <Typography sx={{ fontWeight: 700, fontSize: 12.5, color: theme.palette.primary.main }}>
          {r.strainName}
        </Typography>
        {r.atccNumber && (
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
            ATCC {r.atccNumber}
          </Typography>
        )}
        {r.strainCode && (
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
            {r.strainCode} · {r.cultureCollection}{r.strainNumber ? ` ${r.strainNumber}` : ""}
          </Typography>
        )}
      </Box>
    ) },
    { key: "cryovialCode", label: t("referenceStrain.table.columns.cryovialCode"), render: (r) => <Box sx={{ fontWeight: 700 }}>{r.cryovialCode}</Box> },
    { key: "manufacturerName", label: t("referenceStrain.table.columns.manufacturer"), render: (r) => (
      <Box sx={{ fontSize: 12 }}>
        {r.manufacturerName || "—"}
        {r.sourceMaterialBatchNumber && (
          <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontSize: 12 }}>
            {t("referenceStrain.table.batchPrefix", { batch: r.sourceMaterialBatchNumber })}
          </Typography>
        )}
      </Box>
    ) },
    { key: "receiptDate", label: t("referenceStrain.table.columns.receiptDate"), render: (r) => formatDate(r.receiptDate) },
    { key: "preparedAt", label: t("referenceStrain.table.columns.prepDate"), render: (r) => formatDate(r.preparedAt) },
    { key: "expiryDate", label: t("referenceStrain.table.columns.expiryDate"), render: (r) => formatDate(r.expiryDate) },
    { key: "vialsRemaining", label: t("referenceStrain.table.columns.vialsRemaining"), align: "center", render: (r) => (
      <Chip
        size="small"
        label={`${r.vialsRemaining} / ${r.numberOfVialsPrepared}`}
        color={r.vialsRemaining > 0 ? "success" : "default"}
        sx={{ fontSize: 12, height: 20, fontWeight: 700 }}
      />
    ) },
    { key: "storageCondition", label: t("referenceStrain.table.columns.storage"), render: (r) => <Box sx={{ fontSize: 12 }}>{r.storageCondition || "—"}</Box> },
    { key: "approvalStatus", label: t("referenceStrain.table.columns.approvalStatus"), render: (r) => (
      <>
        <Chip
          size="small"
          label={r.approvalStatus}
          color={r.approvalStatus === "Approved" ? "success" : r.approvalStatus === "Rejected" ? "error" : "warning"}
          sx={{ fontSize: 12, height: 20, fontWeight: 600 }}
        />
        {r.isDestroyed && (
          <Typography variant="caption" sx={{ display: "block", color: "error.main", fontWeight: 700, fontSize: 12 }}>
            {t("referenceStrain.table.destroyed")}
          </Typography>
        )}
      </>
    ) },
    { key: "preparedByName", label: t("referenceStrain.table.columns.preparedBy"), render: (r) => <Box sx={{ fontSize: 12 }}>{r.preparedByName}</Box> },
    { key: "approvedByName", label: t("referenceStrain.table.columns.approvedBy"), render: (r) => (
      <Box sx={{ fontSize: 12 }}>
        {r.approvedByName || "—"}
        {r.approvedAt && (
          <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontSize: 12 }}>
            {formatDate(r.approvedAt)}
          </Typography>
        )}
      </Box>
    ) },
    { key: "directUsageCount", label: t("referenceStrain.table.columns.gptUsage"), align: "center", render: (r) => (
      <Chip
        size="small"
        label={t("referenceStrain.table.gptChip", { count: r.directUsageCount })}
        sx={{ fontSize: 12, height: 20, bgcolor: theme.custom.status.purple.bg, color: theme.custom.status.purple.text, fontWeight: 700 }}
      />
    ) },
    { key: "id", label: t("referenceStrain.table.columns.actions"), align: "center", render: (r) => (
      <Box onClick={(e) => e.stopPropagation()}>
        <Tooltip title={t("referenceStrain.table.viewTooltip")}>
          <IconButton size="small" onClick={() => handleViewDetail(r.id)} color="primary">
            <VisibilityIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Box>
    ) }
  ];

  return (
    <Paper sx={{ p: 2 }}>
      {/* Header Controls */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2, flexWrap: "wrap", gap: 1 }}>
        <Box>
          <Typography sx={{ fontSize: 16, fontWeight: 800, color: theme.palette.primary.main }}>
            {t("referenceStrain.table.title")}
          </Typography>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {t("referenceStrain.table.showing", {
              from: totalCount === 0 ? 0 : (page - 1) * pageSize + 1,
              to: Math.min(page * pageSize, totalCount),
              total: totalCount.toLocaleString()
            })}
          </Typography>
        </Box>

        <Stack direction="row" spacing={1} sx={{
          alignItems: "center"
        }}>
          <Button
            size="small"
            variant="outlined"
            startIcon={<FileDownloadIcon />}
            onClick={handleExportCsv}
            disabled={exportingCsv || totalCount === 0}
            sx={{ fontWeight: 600 }}
          >
            {exportingCsv ? t("referenceStrain.table.exporting") : t("referenceStrain.table.exportCsv")}
          </Button>
          <Button
            size="small"
            variant="contained"
            color="primary"
            startIcon={<PictureAsPdfIcon />}
            onClick={handleExportPdf}
            disabled={items.length === 0}
            sx={{ fontWeight: 600 }}
          >
            {selection.count > 0 ? t("referenceStrain.table.printPdfSelected", { count: selection.count }) : t("referenceStrain.table.printPdf")}
          </Button>
        </Stack>
      </Box>

      {exportError && (
        <Alert severity="error" onClose={() => setExportError(null)} sx={{ mb: 2 }}>
          {exportError}
        </Alert>
      )}

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {/* Table */}
      <RegisterTable
        columns={columns}
        rows={items}
        getRowId={(r) => r.id}
        onRowClick={(r) => void handleViewDetail(r.id)}
        loading={loading}
        empty={{
          title: t("referenceStrain.table.emptyTitle"),
          action: (
            <Button size="small" variant="text" onClick={onClearFilters}>
              {t("referenceStrain.table.clearFilters")}
            </Button>
          )
        }}
        selection={selection}
      />

      {/* Pagination Footer */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 2, flexWrap: "wrap", gap: 1 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("referenceStrain.table.rowsPerPage")}</Typography>
          <FormControl size="small">
            <Select
              value={pageSize}
              onChange={(e) => onPageSizeChange(Number(e.target.value))}
              sx={{ fontSize: 12, height: 32 }}
              inputProps={{ "aria-label": t("referenceStrain.table.rowsPerPage") }}
            >
              {ROWS_PER_PAGE_OPTIONS.map((opt) => (
                <MenuItem key={opt} value={opt} sx={{ fontSize: 12 }}>{opt}</MenuItem>
              ))}
            </Select>
          </FormControl>
        </Box>

        <Stack direction="row" spacing={1} sx={{
          alignItems: "center"
        }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            {t("referenceStrain.table.pageOf", { page, totalPages })}
          </Typography>
          <IconButton aria-label={t("referenceStrain.table.previousPage")}
            size="small"
            disabled={page <= 1 || loading}
            onClick={() => onPageChange(page - 1)}
          >
            <ChevronLeftIcon />
          </IconButton>
          <IconButton aria-label={t("referenceStrain.table.nextPage")}
            size="small"
            disabled={page >= totalPages || loading}
            onClick={() => onPageChange(page + 1)}
          >
            <ChevronRightIcon />
          </IconButton>
        </Stack>
      </Box>

      {/* Detail Dialog */}
      <ReferenceStrainDetailDialog
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        detail={detailBatch}
      />
    </Paper>
  );
}
