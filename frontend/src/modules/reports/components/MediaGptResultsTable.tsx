import { useState } from "react";
import {
  Box, Paper, Typography, Chip, IconButton, Tooltip, Button, Alert, Stack, useTheme
} from "@mui/material";
import VisibilityIcon from "@mui/icons-material/Visibility";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import { MediaGptDetail, MediaGptListItem, MediaGptSearchParams, MediaGptSearchResponse } from "../types/mediaGptTypes";
import { MediaGptReportService } from "../services/MediaGptReportService";
import { MediaGptDetailDialog } from "./MediaGptDetailDialog";
import { exportMediaGptPdf } from "../utils/exportMediaGptPdf";
import { useAuth } from "../../../contexts/AuthContext";
import { RegisterTable, type RegisterColumn } from "../../../components/lab/RegisterTable";
import { useRowSelection } from "../../../components/lab/table/useRowSelection";
import { useTranslation } from "../../../i18n";
import { formatCount } from "../../../utils/formatCount";

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
  const { t } = useTranslation("reports");
  const theme = useTheme();
  const { fullName } = useAuth();
  const resetKey = JSON.stringify({
    search: appliedParams.search,
    mediaType: appliedParams.mediaType,
    evaluationType: appliedParams.evaluationType,
    outcome: appliedParams.outcome,
    approvalStatus: appliedParams.approvalStatus,
    fromDate: appliedParams.fromDate,
    toDate: appliedParams.toDate
  });
  const selection = useRowSelection<string | number>(resetKey);
  const [detailLot, setDetailLot] = useState<MediaGptDetail | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);
  const [exportingCsv, setExportingCsv] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  const items = results?.items ?? [];
  const totalCount = results?.totalCount ?? 0;
  const page = results?.page ?? appliedParams.page ?? 1;
  const pageSize = results?.pageSize ?? appliedParams.pageSize ?? 25;

  const handleViewDetail = async (id: number) => {
    try {
      const detail = await MediaGptReportService.getById(id);
      setDetailLot(detail);
      setDetailOpen(true);
    } catch {
      // Error fetching details
    }
  };

  const handleExportCsv = async () => {
    setExportingCsv(true);
    setExportError(null);
    try {
      await MediaGptReportService.exportCsv(appliedParams);
    } catch (err) {
      setExportError(err instanceof Error ? err.message : t("mediaGpt.errors.exportFailed"));
    } finally {
      setExportingCsv(false);
    }
  };

  const handleExportPdf = () => {
    const recordsToExport = selection.count > 0
      ? items.filter((r) => selection.isSelected(r.id))
      : items;

    exportMediaGptPdf(recordsToExport, {
      title: "Media & Growth Promotion Test (GPT) Report",
      criteriaSummary: `Filters: ${appliedParams.mediaType || "All Media Types"} • ${appliedParams.evaluationType || "All Evaluation Types"}`,
      generatedBy: fullName || "Authorized User",
      isSelection: selection.count > 0
    });
  };

  const columns: RegisterColumn<MediaGptListItem>[] = [
    { key: "lotNumber", label: t("mediaGpt.table.columns.lotNumber"), render: (r) => (
      <Box sx={{ fontWeight: 700, color: theme.palette.primary.main }}>{r.lotNumber}</Box>
    ) },
    { key: "mediaType", label: t("mediaGpt.table.columns.mediaType"), render: (r) => <Box sx={{ fontWeight: 600 }}>{r.mediaType}</Box> },
    { key: "preparedAt", label: t("mediaGpt.table.columns.prepDate"), render: (r) => formatDate(r.preparedAt) },
    { key: "expiryDate", label: t("mediaGpt.table.columns.expiryDate"), render: (r) => formatDate(r.expiryDate) },
    { key: "evaluationType", label: t("mediaGpt.table.columns.evaluation"), render: (r) => (
      <Chip
        size="small"
        label={r.evaluationType === "GrowthPromotion" ? "GPT" : r.evaluationType}
        sx={{ fontSize: 12, height: 20, bgcolor: theme.custom.status.purple.bg, color: theme.custom.status.purple.text, fontWeight: 700 }}
      />
    ) },
    { key: "evaluationOutcome", label: t("mediaGpt.table.columns.outcome"), render: (r) => {
      const isConform = r.evaluationOutcome === "Conform";
      const isNonConform = r.evaluationOutcome === "NonConform";
      return (
        <>
          <Chip
            size="small"
            label={r.evaluationOutcome || r.evaluationStatus}
            color={isConform ? "success" : isNonConform ? "error" : "warning"}
            sx={{ fontSize: 12, height: 20, fontWeight: 700 }}
          />
          {r.challengeCount > 0 && (
            <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontSize: 12 }}>
              {t("mediaGpt.table.challengesCount", { conformed: r.conformedChallengeCount, total: r.challengeCount })}
            </Typography>
          )}
        </>
      );
    } },
    { key: "approvalStatus", label: t("mediaGpt.table.columns.approvalStatus"), render: (r) => (
      <>
        <Chip
          size="small"
          label={r.approvalStatus}
          color={r.approvalStatus === "Approved" ? "success" : r.approvalStatus === "Rejected" ? "error" : "default"}
          sx={{ fontSize: 12, height: 20, fontWeight: 600 }}
        />
        {r.isReleasedForUse && (
          <Typography variant="caption" sx={{ display: "block", color: "success.main", fontWeight: 700, fontSize: 12 }}>
            {t("mediaGpt.table.released")}
          </Typography>
        )}
      </>
    ) },
    { key: "preparedByName", label: t("mediaGpt.table.columns.preparedBy"), render: (r) => <Box sx={{ fontSize: 12 }}>{r.preparedByName}</Box> },
    { key: "approvedByName", label: t("mediaGpt.table.columns.approvedBy"), render: (r) => (
      <Box sx={{ fontSize: 12 }}>
        {r.approvedByName || "—"}
        {r.approvedAt && (
          <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontSize: 12 }}>
            {formatDate(r.approvedAt)}
          </Typography>
        )}
      </Box>
    ) },
    { key: "id", label: t("mediaGpt.table.columns.actions"), align: "center", render: (r) => (
      <Box onClick={(e) => e.stopPropagation()}>
        <Tooltip title={t("mediaGpt.table.viewTooltip")}>
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
            {t("mediaGpt.table.title")}
          </Typography>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {t("mediaGpt.table.showing", {
              from: totalCount === 0 ? 0 : (page - 1) * pageSize + 1,
              to: Math.min(page * pageSize, totalCount),
              total: formatCount(totalCount)
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
            {exportingCsv ? t("mediaGpt.table.exporting") : t("mediaGpt.table.exportCsv")}
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
            {selection.count > 0 ? t("mediaGpt.table.printPdfSelected", { count: selection.count }) : t("mediaGpt.table.printPdf")}
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
          title: t("mediaGpt.table.emptyTitle"),
          action: (
            <Button size="small" variant="text" onClick={onClearFilters}>
              {t("mediaGpt.table.clearFilters")}
            </Button>
          )
        }}
        selection={selection}
        serverPaged={{
          totalCount,
          page: Math.max(0, page - 1),
          onPageChange: (nextZeroBasedPage) => onPageChange(nextZeroBasedPage + 1),
          pageSize,
          onPageSizeChange,
          pageSizeOptions: ROWS_PER_PAGE_OPTIONS
        }}
      />

      {/* Detail Dialog */}
      <MediaGptDetailDialog
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        detail={detailLot}
      />
    </Paper>
  );
}
