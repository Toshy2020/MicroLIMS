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
import { brandColors } from "../../../theme";
import { DataTable, Column } from "../../../components/DataTable";

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
  const theme = useTheme();
  const { fullName } = useAuth();
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [detailBatch, setDetailBatch] = useState<ReferenceStrainDetail | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [exportingCsv, setExportingCsv] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  const items = results?.items ?? [];
  const totalCount = results?.totalCount ?? 0;
  const page = results?.page ?? 1;
  const pageSize = results?.pageSize ?? 25;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const allSelected = items.length > 0 && items.every((r) => selectedIds.includes(r.id));
  const someSelected = items.some((r) => selectedIds.includes(r.id)) && !allSelected;

  const handleSelectAll = () => {
    if (allSelected) {
      setSelectedIds([]);
    } else {
      setSelectedIds(items.map((r) => r.id));
    }
  };

  const handleToggleRow = (id: number) => {
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  };

  const handleViewDetail = async (id: number) => {
    setDetailLoading(true);
    try {
      const detail = await ReferenceStrainReportService.getById(id);
      setDetailBatch(detail);
      setDetailOpen(true);
    } catch {
      // Error loading detail
    } finally {
      setDetailLoading(false);
    }
  };

  const handleExportCsv = async () => {
    setExportingCsv(true);
    setExportError(null);
    try {
      await ReferenceStrainReportService.exportCsv(appliedParams);
    } catch (err: any) {
      setExportError(err.message || "Export failed.");
    } finally {
      setExportingCsv(false);
    }
  };

  const handleExportPdf = () => {
    const recordsToExport = selectedIds.length > 0
      ? items.filter((r) => selectedIds.includes(r.id))
      : items;

    exportReferenceStrainPdf(recordsToExport, {
      title: "Reference Microorganism Strains Report",
      criteriaSummary: `Filters: ${appliedParams.search ? `Search: "${appliedParams.search}"` : "All Strains"} • ${appliedParams.approvalStatus || "All Statuses"}`,
      generatedBy: fullName || "Authorized User",
      isSelection: selectedIds.length > 0
    });
  };

  const columns: Column<ReferenceStrainListItem>[] = [
    { key: "strainName", label: "Strain / ATCC", render: (r) => (
      <Box>
        <Typography sx={{ fontWeight: 700, fontSize: 12.5, color: theme.palette.primary.main }}>
          {r.strainName}
        </Typography>
        {r.atccNumber && (
          <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
            ATCC {r.atccNumber}
          </Typography>
        )}
      </Box>
    ) },
    { key: "cryovialCode", label: "Cryovial Code", render: (r) => <Box sx={{ fontWeight: 700 }}>{r.cryovialCode}</Box> },
    { key: "manufacturerName", label: "Manufacturer", render: (r) => (
      <Box sx={{ fontSize: 12 }}>
        {r.manufacturerName || "—"}
        {r.sourceMaterialBatchNumber && (
          <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontSize: 10 }}>
            Batch: {r.sourceMaterialBatchNumber}
          </Typography>
        )}
      </Box>
    ) },
    { key: "receiptDate", label: "Receipt Date", render: (r) => formatDate(r.receiptDate) },
    { key: "preparedAt", label: "Prep Date", render: (r) => formatDate(r.preparedAt) },
    { key: "expiryDate", label: "Expiry Date", render: (r) => formatDate(r.expiryDate) },
    { key: "vialsRemaining", label: "Vials Remaining", align: "center", render: (r) => (
      <Chip
        size="small"
        label={`${r.vialsRemaining} / ${r.numberOfVialsPrepared}`}
        color={r.vialsRemaining > 0 ? "success" : "default"}
        sx={{ fontSize: 11, height: 20, fontWeight: 700 }}
      />
    ) },
    { key: "storageCondition", label: "Storage", render: (r) => <Box sx={{ fontSize: 11.5 }}>{r.storageCondition || "—"}</Box> },
    { key: "approvalStatus", label: "Approval Status", render: (r) => (
      <>
        <Chip
          size="small"
          label={r.approvalStatus}
          color={r.approvalStatus === "Approved" ? "success" : r.approvalStatus === "Rejected" ? "error" : "warning"}
          sx={{ fontSize: 10.5, height: 20, fontWeight: 600 }}
        />
        {r.isDestroyed && (
          <Typography variant="caption" sx={{ display: "block", color: brandColors.err, fontWeight: 700, fontSize: 10 }}>
            Destroyed
          </Typography>
        )}
      </>
    ) },
    { key: "preparedByName", label: "Prepared By", render: (r) => <Box sx={{ fontSize: 11.5 }}>{r.preparedByName}</Box> },
    { key: "approvedByName", label: "Approved By", render: (r) => (
      <Box sx={{ fontSize: 11.5 }}>
        {r.approvedByName || "—"}
        {r.approvedAt && (
          <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontSize: 10 }}>
            {formatDate(r.approvedAt)}
          </Typography>
        )}
      </Box>
    ) },
    { key: "directUsageCount", label: "GPT Usage", align: "center", render: (r) => (
      <Chip
        size="small"
        label={`${r.directUsageCount} GPT`}
        sx={{ fontSize: 10, height: 20, bgcolor: theme.custom.status.purple.bg, color: theme.custom.status.purple.text, fontWeight: 700 }}
      />
    ) },
    { key: "id", label: "Actions", align: "center", render: (r) => (
      <Box onClick={(e) => e.stopPropagation()}>
        <Tooltip title="View Batch Traceability">
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
            Reference Strains & Working Culture Batches
          </Typography>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Showing {totalCount === 0 ? 0 : (page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} of {totalCount.toLocaleString()} batches
          </Typography>
        </Box>

        <Stack direction="row" spacing={1} alignItems="center">
          <Button
            size="small"
            variant="outlined"
            startIcon={<FileDownloadIcon />}
            onClick={handleExportCsv}
            disabled={exportingCsv || totalCount === 0}
            sx={{ fontWeight: 600 }}
          >
            {exportingCsv ? "Exporting..." : "Export CSV"}
          </Button>
          <Button
            size="small"
            variant="contained"
            startIcon={<PictureAsPdfIcon />}
            onClick={handleExportPdf}
            disabled={items.length === 0}
            sx={{ bgcolor: brandColors.sectionTitle, "&:hover": { bgcolor: "#4a0f61" }, fontWeight: 600 }}
          >
            Print / PDF View {selectedIds.length > 0 ? `(${selectedIds.length})` : ""}
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
      <DataTable
        columns={columns}
        rows={items}
        getRowId={(r) => r.id}
        onRowClick={(r) => handleViewDetail(r.id)}
        loading={loading}
        emptyMessage={
          <>
            <Typography sx={{ color: "text.secondary", fontSize: 13, mb: 1 }}>
              No reference strain batches matched your filters.
            </Typography>
            <Button size="small" variant="text" onClick={onClearFilters}>
              Clear Filters
            </Button>
          </>
        }
        selection={{
          isSelected: (r) => selectedIds.includes(r.id),
          onToggle: (r) => handleToggleRow(r.id),
          headerChecked: allSelected,
          headerIndeterminate: someSelected,
          onToggleAll: handleSelectAll
        }}
      />

      {/* Pagination Footer */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 2, flexWrap: "wrap", gap: 1 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Rows per page:</Typography>
          <FormControl size="small">
            <Select
              value={pageSize}
              onChange={(e) => onPageSizeChange(Number(e.target.value))}
              sx={{ fontSize: 12, height: 32 }}
            >
              {ROWS_PER_PAGE_OPTIONS.map((opt) => (
                <MenuItem key={opt} value={opt} sx={{ fontSize: 12 }}>{opt}</MenuItem>
              ))}
            </Select>
          </FormControl>
        </Box>

        <Stack direction="row" spacing={1} alignItems="center">
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            Page {page} of {totalPages}
          </Typography>
          <IconButton
            size="small"
            disabled={page <= 1 || loading}
            onClick={() => onPageChange(page - 1)}
          >
            <ChevronLeftIcon />
          </IconButton>
          <IconButton
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
