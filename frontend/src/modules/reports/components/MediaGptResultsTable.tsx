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
import { MediaGptDetail, MediaGptListItem, MediaGptSearchParams, MediaGptSearchResponse } from "../types/mediaGptTypes";
import { MediaGptReportService } from "../services/MediaGptReportService";
import { MediaGptDetailDialog } from "./MediaGptDetailDialog";
import { exportMediaGptPdf } from "../utils/exportMediaGptPdf";
import { useAuth } from "../../../contexts/AuthContext";
import { brandColors } from "../../../theme";
import { DataTable, Column } from "../../../components/DataTable";

const ROWS_PER_PAGE_OPTIONS = [10, 25, 50, 100];

function formatDate(iso?: string | null): string {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" });
  } catch {
    return iso;
  }
}

interface MediaGptResultsTableProps {
  results: MediaGptSearchResponse | null;
  loading: boolean;
  error: string | null;
  appliedParams: MediaGptSearchParams;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  onClearFilters: () => void;
}

export function MediaGptResultsTable({
  results,
  loading,
  error,
  appliedParams,
  onPageChange,
  onPageSizeChange,
  onClearFilters
}: MediaGptResultsTableProps) {
  const theme = useTheme();
  const { fullName } = useAuth();
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [detailLot, setDetailLot] = useState<MediaGptDetail | null>(null);
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
      const detail = await MediaGptReportService.getById(id);
      setDetailLot(detail);
      setDetailOpen(true);
    } catch {
      // Error fetching details
    } finally {
      setDetailLoading(false);
    }
  };

  const handleExportCsv = async () => {
    setExportingCsv(true);
    setExportError(null);
    try {
      await MediaGptReportService.exportCsv(appliedParams);
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

    exportMediaGptPdf(recordsToExport, {
      title: "Media & Growth Promotion Test (GPT) Report",
      criteriaSummary: `Filters: ${appliedParams.mediaType || "All Media Types"} • ${appliedParams.evaluationType || "All Evaluation Types"}`,
      generatedBy: fullName || "Authorized User",
      isSelection: selectedIds.length > 0
    });
  };

  const columns: Column<MediaGptListItem>[] = [
    { key: "lotNumber", label: "Lot Number", render: (r) => (
      <Box sx={{ fontWeight: 700, color: theme.palette.primary.main }}>{r.lotNumber}</Box>
    ) },
    { key: "mediaType", label: "Media Type", render: (r) => <Box sx={{ fontWeight: 600 }}>{r.mediaType}</Box> },
    { key: "preparedAt", label: "Prep Date", render: (r) => formatDate(r.preparedAt) },
    { key: "expiryDate", label: "Expiry Date", render: (r) => formatDate(r.expiryDate) },
    { key: "evaluationType", label: "Evaluation", render: (r) => (
      <Chip
        size="small"
        label={r.evaluationType === "GrowthPromotion" ? "GPT" : r.evaluationType}
        sx={{ fontSize: 10, height: 20, bgcolor: theme.custom.status.purple.bg, color: theme.custom.status.purple.text, fontWeight: 700 }}
      />
    ) },
    { key: "evaluationOutcome", label: "Outcome", render: (r) => {
      const isConform = r.evaluationOutcome === "Conform";
      const isNonConform = r.evaluationOutcome === "NonConform";
      return (
        <>
          <Chip
            size="small"
            label={r.evaluationOutcome || r.evaluationStatus}
            color={isConform ? "success" : isNonConform ? "error" : "warning"}
            sx={{ fontSize: 10.5, height: 20, fontWeight: 700 }}
          />
          {r.challengeCount > 0 && (
            <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontSize: 10 }}>
              {r.conformedChallengeCount}/{r.challengeCount} Challenges
            </Typography>
          )}
        </>
      );
    } },
    { key: "approvalStatus", label: "Approval Status", render: (r) => (
      <>
        <Chip
          size="small"
          label={r.approvalStatus}
          color={r.approvalStatus === "Approved" ? "success" : r.approvalStatus === "Rejected" ? "error" : "default"}
          sx={{ fontSize: 10.5, height: 20, fontWeight: 600 }}
        />
        {r.isReleasedForUse && (
          <Typography variant="caption" sx={{ display: "block", color: brandColors.ok, fontWeight: 700, fontSize: 10 }}>
            Released
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
    { key: "id", label: "Actions", align: "center", render: (r) => (
      <Box onClick={(e) => e.stopPropagation()}>
        <Tooltip title="View Traceability Details">
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
            Media Preparation & GPT Records
          </Typography>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Showing {totalCount === 0 ? 0 : (page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} of {totalCount.toLocaleString()} lots
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
            {exportingCsv ? "Exporting..." : "Export CSV (Flattened)"}
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
              No media lots matched your filters.
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
      <MediaGptDetailDialog
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        detail={detailLot}
      />
    </Paper>
  );
}
