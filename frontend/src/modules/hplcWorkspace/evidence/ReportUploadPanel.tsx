import { useState, useRef } from "react";
import {
  Box,
  Paper,
  Typography,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Alert,
  CircularProgress,
  Stack,
  useTheme,
  IconButton,
  Tooltip
} from "@mui/material";
import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import VisibilityIcon from "@mui/icons-material/Visibility";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import ImageIcon from "@mui/icons-material/Image";
import InsertDriveFileIcon from "@mui/icons-material/InsertDriveFile";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { SupersedeEvidenceDialog } from "./SupersedeEvidenceDialog";
import { tableHeadSx } from "../../../theme";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import type {
  HplcEvidenceDto,
  HplcEvidenceContext,
  HplcEvidenceKind
} from "../types";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

export interface ReportUploadPanelProps {
  runId: number;
  runSampleId?: number;
  context: HplcEvidenceContext;
  kind: HplcEvidenceKind;
  evidenceList?: HplcEvidenceDto[];
  onChanged?: () => void;
}

const MAX_FILE_SIZE_BYTES = 26_214_400; // 25 MB

export function ReportUploadPanel({
  runId,
  runSampleId,
  context,
  kind,
  evidenceList = [],
  onChanged
}: ReportUploadPanelProps) {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Upload state
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [viewingId, setViewingId] = useState<number | null>(null);

  // Supersede dialog state
  const [supersedeOpen, setSupersedeOpen] = useState(false);
  const [targetEvidence, setTargetEvidence] = useState<HplcEvidenceDto | null>(null);

  // Filter evidence to match this context and runSampleId
  const relevantEvidence = evidenceList.filter((e) => {
    if (e.context !== context) return false;
    if (runSampleId !== undefined && runSampleId !== null) {
      return e.hplcRunSampleId === runSampleId;
    }
    return true;
  });

  const currentCount = relevantEvidence.filter((e) => e.isCurrent).length;

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setError(null);
    if (e.target.files && e.target.files[0]) {
      const file = e.target.files[0];
      if (file.size > MAX_FILE_SIZE_BYTES) {
        setError(t("evidence.upload.fileTooLarge"));
        setSelectedFile(null);
        return;
      }
      setSelectedFile(file);
    }
  };

  const handleUpload = async () => {
    if (!selectedFile) return;

    setUploading(true);
    setError(null);
    try {
      await HplcWorkspaceService.uploadEvidence(
        runId,
        selectedFile,
        context,
        kind,
        runSampleId
      );
      setSelectedFile(null);
      if (fileInputRef.current) fileInputRef.current.value = "";
      onChanged?.();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? t("evidence.upload.uploadFailed"));
    } finally {
      setUploading(false);
    }
  };

  const handleViewFile = async (id: number) => {
    setViewingId(id);
    try {
      await HplcWorkspaceService.openEvidenceInNewTab(id);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? t("evidence.upload.openFailed"));
    } finally {
      setViewingId(null);
    }
  };

  const openSupersede = (item: HplcEvidenceDto) => {
    setTargetEvidence(item);
    setSupersedeOpen(true);
  };

  const getFileIcon = (contentType: string) => {
    if (contentType.includes("pdf")) return <PictureAsPdfIcon color="error" fontSize="small" />;
    if (contentType.includes("image")) return <ImageIcon color="primary" fontSize="small" />;
    return <InsertDriveFileIcon color="action" fontSize="small" />;
  };

  return (
    <Paper
      elevation={0}
      sx={{
        p: 2.5,
        borderRadius: 2,
        border: `1px solid ${theme.palette.divider}`,
        backgroundColor: theme.palette.background.paper
      }}
    >
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
        <Box>
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
            {context === "Sst" ? t("evidence.upload.sstTitle") : t("evidence.upload.defaultTitle")}
          </Typography>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {context === "Sst"
              ? t("evidence.upload.sstSubtitle")
              : t("evidence.upload.defaultSubtitle")}
          </Typography>
        </Box>
        <StatusBadge
          status={currentCount > 0 ? "Active" : "Pending"}
          label={t("evidence.upload.currentReports", { count: currentCount })}
        />
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {/* Upload Box */}
      <Box
        sx={{
          p: 2,
          mb: 3,
          borderRadius: 1.5,
          border: `1px dashed ${theme.palette.primary.main}`,
          backgroundColor: theme.palette.action.hover,
          display: "flex",
          flexDirection: { xs: "column", sm: "row" },
          alignItems: "center",
          gap: 2
        }}
      >
        <input
          type="file"
          accept=".pdf,.png,.jpg,.jpeg,application/pdf,image/png,image/jpeg"
          ref={fileInputRef}
          onChange={handleFileChange}
          style={{ display: "none" }}
          id={`upload-evidence-${context}-${runSampleId ?? "run"}`}
        />
        <label htmlFor={`upload-evidence-${context}-${runSampleId ?? "run"}`}>
          <Button
            variant="outlined"
            component="span"
            size="small"
            startIcon={<CloudUploadIcon />}
            disabled={uploading}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            {t("evidence.upload.selectFile")}
          </Button>
        </label>

        <Box sx={{ flexGrow: 1, minWidth: 0 }}>
          {selectedFile ? (
            <Typography variant="body2" sx={{ fontWeight: 600, wordBreak: "break-all" }}>
              <Ltr>{`${selectedFile.name} (${(selectedFile.size / (1024 * 1024)).toFixed(2)} MB)`}</Ltr>
            </Typography>
          ) : (
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {t("evidence.upload.supportedFormats")}
            </Typography>
          )}
        </Box>

        <Button
          variant="contained"
          size="small"
          onClick={handleUpload}
          disabled={!selectedFile || uploading}
          sx={{ textTransform: "none", fontWeight: 600, minWidth: 100 }}
        >
          {uploading ? <CircularProgress size={20} color="inherit" /> : t("evidence.upload.upload")}
        </Button>
      </Box>

      {/* Evidence Table */}
      {relevantEvidence.length === 0 ? (
        <Typography variant="body2" sx={{ color: "text.secondary", textAlign: "center", py: 2 }}>
          {t("evidence.upload.noReports")}
        </Typography>
      ) : (
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell>{t("evidence.upload.columns.file")}</TableCell>
                <TableCell>{t("evidence.upload.columns.status")}</TableCell>
                <TableCell>{t("evidence.upload.columns.uploadedBy")}</TableCell>
                <TableCell>{t("evidence.upload.columns.uploadedAt")}</TableCell>
                <TableCell align="right">{t("evidence.upload.columns.actions")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {relevantEvidence.map((item) => (
                <TableRow
                  key={item.id}
                  sx={{
                    opacity: item.isCurrent ? 1 : 0.65,
                    backgroundColor: item.isCurrent ? "inherit" : theme.palette.action.hover
                  }}
                >
                  <TableCell>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                      {getFileIcon(item.contentType)}
                      <Box>
                        <Typography variant="body2" sx={{ fontWeight: item.isCurrent ? 600 : 400 }}>
                          <Ltr>{item.fileName}</Ltr>
                        </Typography>
                        {!item.isCurrent && item.supersedeReason && (
                          <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                            {t("evidence.upload.supersededPrefix", { reason: item.supersedeReason })}
                          </Typography>
                        )}
                      </Box>
                    </Box>
                  </TableCell>
                  <TableCell>
                    <StatusBadge
                      status={item.isCurrent ? "Active" : "Superseded"}
                      label={item.isCurrent ? t("evidence.upload.current") : t("evidence.upload.superseded")}
                    />
                  </TableCell>
                  <TableCell>{item.uploadedByUserName ?? t("evidence.upload.userFallback", { id: item.uploadedByUserId })}</TableCell>
                  <TableCell><Ltr>{formatDateTimeWithTimeZone(item.uploadedAt)}</Ltr></TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
                      <Tooltip title={t("evidence.upload.viewTooltip")}>
                        <span>
                          <IconButton
                            size="small"
                            onClick={() => handleViewFile(item.id)}
                            disabled={viewingId === item.id}
                            aria-label={t("evidence.upload.viewAria", { fileName: item.fileName })}
                          >
                            {viewingId === item.id ? (
                              <CircularProgress size={16} />
                            ) : (
                              <VisibilityIcon fontSize="small" />
                            )}
                          </IconButton>
                        </span>
                      </Tooltip>

                      {item.isCurrent && (
                        <Tooltip title={t("evidence.upload.replaceTooltip")}>
                          <IconButton
                            size="small"
                            color="primary"
                            onClick={() => openSupersede(item)}
                            aria-label={t("evidence.upload.replaceAria", { fileName: item.fileName })}
                          >
                            <SwapHorizIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Supersede Dialog */}
      <SupersedeEvidenceDialog
        open={supersedeOpen}
        targetEvidence={targetEvidence}
        onClose={() => setSupersedeOpen(false)}
        onSuccess={() => {
          onChanged?.();
        }}
      />
    </Paper>
  );
}
