import { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  CircularProgress,
  Alert,
  Chip,
  LinearProgress
} from "@mui/material";
import DownloadOutlinedIcon from "@mui/icons-material/DownloadOutlined";
import CloseIcon from "@mui/icons-material/Close";
import SecurityIcon from "@mui/icons-material/Security";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import MenuBookIcon from "@mui/icons-material/MenuBook";
import { documentControlService } from "../services/documentControlService";
import { StatusBadge } from "../../../components/StatusBadge";
import { toast } from "sonner";
import { Trans } from "react-i18next";
import { MIN_LABEL_FONT_SIZE, compactChipStrongSx, compactChipSx } from "../documentControlStyles";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";

export interface ControlledPdfViewerProps {
  open: boolean;
  onClose: () => void;
  fileId: number | null;
  fileName?: string;
  companyDocumentCode: string;
  microLimsDocumentId: string;
  revisionNumber: string;
  title: string;
  revisionStatus: string;
  canDownload?: boolean;
  // WP6 Training & Reading Progress Extensions
  assignmentId?: number;
  readingProgress?: number;
  onProgressUpdate?: (progress: number) => void;
  onAcknowledgeClick?: () => void;
  isAcknowledged?: boolean;
  canAcknowledge?: boolean;
}

export function ControlledPdfViewer({
  open,
  onClose,
  fileId,
  fileName,
  companyDocumentCode,
  microLimsDocumentId,
  revisionNumber,
  title,
  revisionStatus,
  canDownload = true,
  assignmentId,
  readingProgress = 0,
  onProgressUpdate,
  onAcknowledgeClick,
  isAcknowledged = false,
  canAcknowledge = false
}: ControlledPdfViewerProps) {
  const { t } = useTranslation("documentControl");
  const [loading, setLoading] = useState(false);
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isIntegrityError, setIsIntegrityError] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [localProgress, setLocalProgress] = useState(readingProgress);

  useEffect(() => {
    setLocalProgress(readingProgress);
  }, [readingProgress]);

  useEffect(() => {
    if (!open || !fileId) {
      if (pdfUrl) {
        window.URL.revokeObjectURL(pdfUrl);
        setPdfUrl(null);
      }
      setError(null);
      setIsIntegrityError(false);
      return;
    }

    let active = true;
    setLoading(true);
    setError(null);
    setIsIntegrityError(false);

    documentControlService
      .viewFileBlob(fileId)
      .then(({ url }) => {
        if (!active) return;
        setPdfUrl(url);
        setLoading(false);
      })
      .catch((err: unknown) => {
        if (!active) return;
        setLoading(false);
        const errMsg = serverMessage(err) ?? getErrorMessage(err, t("pdfViewer.loadError"));
        if (errMsg.toLowerCase().includes("integrity")) {
          setIsIntegrityError(true);
          setError(t("pdfViewer.integrityError"));
        } else {
          setError(errMsg);
        }
      });

    return () => {
      active = false;
      if (pdfUrl) {
        window.URL.revokeObjectURL(pdfUrl);
      }
    };
  }, [open, fileId]);

  const handleDownload = async () => {
    if (!fileId) return;
    setDownloading(true);
    try {
      await documentControlService.downloadFile(fileId, fileName || `${companyDocumentCode}_rev${revisionNumber}.pdf`);
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? getErrorMessage(err, t("pdfViewer.downloadError")));
    } finally {
      setDownloading(false);
    }
  };

  const handleMarkProgress = (percent: number) => {
    setLocalProgress(percent);
    if (onProgressUpdate) {
      onProgressUpdate(percent);
    }
  };

  const isSupersededOrObsolete = revisionStatus === "Superseded" || revisionStatus === "Obsolete" || revisionStatus === "Cancelled";

  return (
    <Dialog open={open} onClose={onClose} maxWidth="lg" fullWidth slotProps={{
      paper: { sx: { height: "92vh" } }
    }}>
      <DialogTitle sx={{ pb: 1, borderBottom: "1px solid", borderColor: "divider" }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
          <Box>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 0.5 }}>
              <SecurityIcon color="primary" fontSize="small" />
              <Typography variant="caption" sx={{ fontWeight: 700, color: "primary.main", letterSpacing: 1 }}>
                {t("pdfViewer.viewerTag")}
              </Typography>
              <Chip label={t("pdfViewer.officialCopy")} size="small" color="primary" variant="outlined" sx={compactChipSx} />
              <StatusBadge status={revisionStatus} />
              {isSupersededOrObsolete && (
                <Chip
                  icon={<WarningAmberIcon />}
                  label={t("pdfViewer.historicalWarning")}
                  size="small"
                  color="warning"
                  sx={compactChipStrongSx}
                />
              )}
            </Box>
            <Typography variant="h6" sx={{ fontWeight: 700, lineHeight: 1.2 }}>
              {companyDocumentCode}: {title}
            </Typography>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>
              <Trans
                i18nKey="pdfViewer.metaLine"
                ns="documentControl"
                values={{ id: microLimsDocumentId, rev: revisionNumber }}
                components={{ strong: <strong /> }}
              />
              {assignmentId ? t("pdfViewer.assignmentSuffix", { id: assignmentId }) : ""}
            </Typography>
          </Box>
          <Button onClick={onClose} color="inherit" size="small" startIcon={<CloseIcon />}>
            {t("pdfViewer.close")}
          </Button>
        </Box>

        {/* Informational Reading Progress Bar for Training Assignments */}
        {assignmentId && (
          <Box sx={{ mt: 1.5, pt: 1, borderTop: "1px dashed", borderColor: "divider" }}>
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 0.5 }}>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                <MenuBookIcon fontSize="small" color="action" />
                <Typography
                  variant="caption"
                  sx={{
                    fontWeight: "bold",
                    color: "text.secondary"
                  }}>
                  {t("pdfViewer.readingProgress", { percent: localProgress })}
                </Typography>
                {isAcknowledged && (
                  <Chip
                    icon={<VerifiedUserIcon />}
                    label={t("pdfViewer.acknowledged")}
                    size="small"
                    color="success"
                    sx={compactChipSx}
                  />
                )}
              </Box>
              {!isAcknowledged && (
                <Box sx={{ display: "flex", gap: 0.5 }}>
                  <Button
                    size="small"
                    variant={localProgress >= 50 ? "contained" : "outlined"}
                    sx={{ py: 0.2, px: 1, fontSize: MIN_LABEL_FONT_SIZE, minWidth: 0 }}
                    onClick={() => handleMarkProgress(50)}
                  >
                    {t("pdfViewer.percent50")}
                  </Button>
                  <Button
                    size="small"
                    variant={localProgress >= 100 ? "contained" : "outlined"}
                    sx={{ py: 0.2, px: 1, fontSize: MIN_LABEL_FONT_SIZE, minWidth: 0 }}
                    onClick={() => handleMarkProgress(100)}
                  >
                    {t("pdfViewer.percent100Read")}
                  </Button>
                </Box>
              )}
            </Box>
            <LinearProgress
              variant="determinate"
              value={localProgress}
              sx={{ height: 6, borderRadius: 1 }}
              color={localProgress >= 100 ? "success" : "primary"}
            />
          </Box>
        )}
      </DialogTitle>

      <DialogContent sx={{ p: 0, height: "100%", display: "flex", flexDirection: "column", bgcolor: (t) => t.palette.mode === "dark" ? "background.default" : "grey.100" }}>
        {loading && (
          <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", height: "100%", gap: 2 }}>
            <CircularProgress />
            <Typography variant="body2" sx={{
              color: "text.secondary"
            }}>
              {t("pdfViewer.loading")}
            </Typography>
          </Box>
        )}

        {error && (
          <Box sx={{ p: 4, display: "flex", justifyContent: "center", alignItems: "center", height: "100%" }}>
            <Alert severity={isIntegrityError ? "error" : "warning"} sx={{ maxWidth: 600 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
                {isIntegrityError ? t("pdfViewer.integrityTitle") : t("pdfViewer.deliveryErrorTitle")}
              </Typography>
              {error}
            </Alert>
          </Box>
        )}

        {!loading && !error && pdfUrl && (
          <iframe
            src={`${pdfUrl}#toolbar=0`}
            title={title}
            width="100%"
            height="100%"
            style={{ border: "none", flexGrow: 1 }}
          />
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 1.5, borderTop: "1px solid", borderColor: "divider", justifyContent: "space-between" }}>
        <Typography variant="caption" sx={{
          color: "text.secondary"
        }}>
          {t("pdfViewer.footer")}
        </Typography>
        <Box sx={{ display: "flex", gap: 1, alignItems: "center" }}>
          {canDownload && (
            <Button
              variant="outlined"
              size="small"
              startIcon={<DownloadOutlinedIcon />}
              onClick={handleDownload}
              disabled={loading || !!error || downloading}
            >
              {downloading ? t("pdfViewer.downloading") : t("pdfViewer.downloadCopy")}
            </Button>
          )}

          {assignmentId && !isAcknowledged && onAcknowledgeClick && (
            <Button
              variant="contained"
              color="primary"
              size="small"
              startIcon={<VerifiedUserIcon />}
              onClick={onAcknowledgeClick}
              disabled={!canAcknowledge}
            >
              {t("pdfViewer.acknowledgeTraining")}
            </Button>
          )}

          <Button onClick={onClose} variant={assignmentId && !isAcknowledged ? "outlined" : "contained"} size="small">
            Done
          </Button>
        </Box>
      </DialogActions>
    </Dialog>
  );
}
