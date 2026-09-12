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
  Grid,
  Tooltip,
  IconButton
} from "@mui/material";
import DownloadOutlinedIcon from "@mui/icons-material/DownloadOutlined";
import CloseIcon from "@mui/icons-material/Close";
import CompareArrowsIcon from "@mui/icons-material/CompareArrows";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";
import SecurityIcon from "@mui/icons-material/Security";
import { documentControlService } from "../services/documentControlService";
import { StatusBadge } from "../../../components/StatusBadge";
import { toast } from "sonner";
import { compactChipStrongSx, compactChipSx, documentCodeSx } from "../documentControlStyles";

export interface DualPdfComparisonViewerProps {
  open: boolean;
  onClose: () => void;
  effectiveFileId: number | null;
  effectiveRevisionNumber: string;
  effectiveFileName?: string;
  effectiveSha256?: string;
  proposedFileId: number | null;
  proposedRevisionNumber: string;
  proposedFileName?: string;
  proposedSha256?: string;
  companyDocumentCode: string;
  documentTitle: string;
  canDownload?: boolean;
}

export function DualPdfComparisonViewer({
  open,
  onClose,
  effectiveFileId,
  effectiveRevisionNumber,
  effectiveFileName,
  effectiveSha256,
  proposedFileId,
  proposedRevisionNumber,
  proposedFileName,
  proposedSha256,
  companyDocumentCode,
  documentTitle,
  canDownload = true
}: DualPdfComparisonViewerProps) {
  // Left PDF (Effective)
  const [loadingEffective, setLoadingEffective] = useState(false);
  const [effectivePdfUrl, setEffectivePdfUrl] = useState<string | null>(null);
  const [effectiveError, setEffectiveError] = useState<string | null>(null);

  // Right PDF (Proposed Revision)
  const [loadingProposed, setLoadingProposed] = useState(false);
  const [proposedPdfUrl, setProposedPdfUrl] = useState<string | null>(null);
  const [proposedError, setProposedError] = useState<string | null>(null);

  const [downloading, setDownloading] = useState(false);

  useEffect(() => {
    if (!open) {
      if (effectivePdfUrl) {
        window.URL.revokeObjectURL(effectivePdfUrl);
        setEffectivePdfUrl(null);
      }
      if (proposedPdfUrl) {
        window.URL.revokeObjectURL(proposedPdfUrl);
        setProposedPdfUrl(null);
      }
      setEffectiveError(null);
      setProposedError(null);
      return;
    }

    let active = true;

    // Load Effective PDF
    if (effectiveFileId) {
      setLoadingEffective(true);
      setEffectiveError(null);
      documentControlService
        .viewFileBlob(effectiveFileId)
        .then(({ url }) => {
          if (!active) return;
          setEffectivePdfUrl(url);
          setLoadingEffective(false);
        })
        .catch((err) => {
          if (!active) return;
          setLoadingEffective(false);
          const errMsg = err.response?.data?.message || err.message || "Failed to load effective PDF.";
          setEffectiveError(errMsg);
        });
    } else {
      setEffectivePdfUrl(null);
      setLoadingEffective(false);
    }

    // Load Proposed PDF
    if (proposedFileId) {
      setLoadingProposed(true);
      setProposedError(null);
      documentControlService
        .viewFileBlob(proposedFileId)
        .then(({ url }) => {
          if (!active) return;
          setProposedPdfUrl(url);
          setLoadingProposed(false);
        })
        .catch((err) => {
          if (!active) return;
          setLoadingProposed(false);
          const errMsg = err.response?.data?.message || err.message || "Failed to load proposed revision PDF.";
          setProposedError(errMsg);
        });
    } else {
      setProposedPdfUrl(null);
      setLoadingProposed(false);
    }

    return () => {
      active = false;
      if (effectivePdfUrl) {
        window.URL.revokeObjectURL(effectivePdfUrl);
      }
      if (proposedPdfUrl) {
        window.URL.revokeObjectURL(proposedPdfUrl);
      }
    };
  }, [open, effectiveFileId, proposedFileId]);

  const handleDownload = async (fileId: number | null, fallbackName: string) => {
    if (!fileId) return;
    setDownloading(true);
    try {
      await documentControlService.downloadFile(fileId, fallbackName);
    } catch (err: any) {
      const errMsg = err.response?.data?.message || err.message || "Download failed.";
      toast.error(errMsg);
    } finally {
      setDownloading(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth={false}
      fullWidth
      slotProps={{
        paper: {
          sx: {
            width: "96vw",
            height: "94vh",
            maxWidth: "1920px",
            display: "flex",
            flexDirection: "column"
          }
        }
      }}
    >
      <DialogTitle sx={{ py: 1.5, px: 3, borderBottom: "1px solid", borderColor: "divider", bgcolor: "background.paper" }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <Box>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 0.5 }}>
              <CompareArrowsIcon color="primary" />
              <Typography variant="caption" sx={{ fontWeight: 800, color: "primary.main", letterSpacing: 1.2 }}>
                SIDE-BY-SIDE REVISION INSPECTION (DC-URS-067)
              </Typography>
              <Chip
                label="TECHNICAL REVIEW WORKSPACE"
                size="small"
                color="secondary"
                variant="outlined"
                sx={compactChipStrongSx}
              />
            </Box>
            <Typography variant="h6" sx={{ fontWeight: 700, lineHeight: 1.2 }}>
              {companyDocumentCode} — {documentTitle}
            </Typography>
          </Box>
          <IconButton onClick={onClose} size="small" aria-label="close">
            <CloseIcon />
          </IconButton>
        </Box>
      </DialogTitle>

      <DialogContent sx={{ p: 0, flexGrow: 1, display: "flex", flexDirection: "column", bgcolor: (t) => t.palette.mode === "dark" ? "background.default" : "grey.100" }}>
        <Grid container sx={{ flexGrow: 1, height: "100%" }}>
          {/* LEFT PANE: Currently Effective Controlled Document */}
          <Grid
            sx={{
              height: "100%",
              display: "flex",
              flexDirection: "column",
              borderRight: { md: "2px solid" },
              borderColor: { md: "divider" }
            }}
            size={{
              xs: 12,
              md: 6
            }}>
            {/* Header Banner */}
            <Box
              sx={{
                px: 2,
                py: 1,
                bgcolor: (t) => (t.palette.mode === "dark" ? "#1B2A1E" : "#E8F5E9"),
                borderBottom: "1px solid",
                borderColor: (t) => (t.palette.mode === "dark" ? "#2E5A36" : "#C8E6C9"),
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center"
              }}
            >
              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                <StatusBadge status="Effective" />
                <Typography variant="subtitle2" sx={{ fontWeight: 700, color: (t) => t.palette.mode === "dark" ? "success.light" : "success.dark" }}>
                  Current Effective SOP (Rev {effectiveRevisionNumber})
                </Typography>
              </Box>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                {effectiveSha256 && (
                  <Tooltip title={`SHA-256: ${effectiveSha256}`}>
                    <Chip
                      icon={<SecurityIcon style={{ fontSize: 14 }} />}
                      label={`SHA-256: ${effectiveSha256.substring(0, 8)}...`}
                      size="small"
                      color="success"
                      variant="outlined"
                      sx={{ ...compactChipSx, ...documentCodeSx }}
                    />
                  </Tooltip>
                )}
                {canDownload && effectiveFileId && (
                  <Button
                    size="small"
                    variant="outlined"
                    startIcon={<DownloadOutlinedIcon />}
                    onClick={() =>
                      handleDownload(
                        effectiveFileId,
                        effectiveFileName || `${companyDocumentCode}_rev${effectiveRevisionNumber}_effective.pdf`
                      )
                    }
                    disabled={loadingEffective || downloading}
                    sx={compactChipSx}
                  >
                    Download
                  </Button>
                )}
              </Box>
            </Box>

            {/* Left Viewer Body */}
            <Box sx={{ flexGrow: 1, position: "relative", bgcolor: (t) => t.palette.mode === "dark" ? "background.default" : "grey.200", height: "calc(100% - 45px)" }}>
              {loadingEffective && (
                <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", height: "100%", gap: 2 }}>
                  <CircularProgress size={36} />
                  <Typography variant="body2" sx={{
                    color: "text.secondary"
                  }}>
                    Verifying SHA-256 and loading effective PDF...
                  </Typography>
                </Box>
              )}

              {effectiveError && (
                <Box sx={{ p: 4, display: "flex", justifyContent: "center", alignItems: "center", height: "100%" }}>
                  <Alert severity="warning" sx={{ maxWidth: 450 }}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                      Effective PDF Notice
                    </Typography>
                    {effectiveError}
                  </Alert>
                </Box>
              )}

              {!effectiveFileId && !loadingEffective && (
                <Box sx={{ p: 4, display: "flex", justifyContent: "center", alignItems: "center", height: "100%" }}>
                  <Alert severity="info" sx={{ maxWidth: 450 }}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                      Initial Revision Sequence #1
                    </Typography>
                    This document is sequence #1; no prior effective revision exists for comparative inspection.
                  </Alert>
                </Box>
              )}

              {!loadingEffective && !effectiveError && effectivePdfUrl && (
                <iframe
                  src={`${effectivePdfUrl}#toolbar=0`}
                  title={`Effective Rev ${effectiveRevisionNumber}`}
                  width="100%"
                  height="100%"
                  style={{ border: "none" }}
                />
              )}
            </Box>
          </Grid>

          {/* RIGHT PANE: Proposed Draft Revision Document */}
          <Grid
            sx={{
              height: "100%",
              display: "flex",
              flexDirection: "column"
            }}
            size={{
              xs: 12,
              md: 6
            }}>
            {/* Header Banner */}
            <Box
              sx={{
                px: 2,
                py: 1,
                bgcolor: (t) => (t.palette.mode === "dark" ? "#102A43" : "#E3F2FD"),
                borderBottom: "1px solid",
                borderColor: (t) => (t.palette.mode === "dark" ? "#244E72" : "#BBDEFB"),
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center"
              }}
            >
              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                <StatusBadge status="InReview" />
                <Typography variant="subtitle2" sx={{ fontWeight: 700, color: (t) => t.palette.mode === "dark" ? "info.light" : "primary.dark" }}>
                  Proposed Revision Under Review (Rev {proposedRevisionNumber})
                </Typography>
              </Box>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                {proposedSha256 && (
                  <Tooltip title={`SHA-256: ${proposedSha256}`}>
                    <Chip
                      icon={<SecurityIcon style={{ fontSize: 14 }} />}
                      label={`SHA-256: ${proposedSha256.substring(0, 8)}...`}
                      size="small"
                      color="primary"
                      variant="outlined"
                      sx={{ ...compactChipSx, ...documentCodeSx }}
                    />
                  </Tooltip>
                )}
                {canDownload && proposedFileId && (
                  <Button
                    size="small"
                    variant="outlined"
                    startIcon={<DownloadOutlinedIcon />}
                    onClick={() =>
                      handleDownload(
                        proposedFileId,
                        proposedFileName || `${companyDocumentCode}_rev${proposedRevisionNumber}_proposed.pdf`
                      )
                    }
                    disabled={loadingProposed || downloading}
                    sx={compactChipSx}
                  >
                    Download
                  </Button>
                )}
              </Box>
            </Box>

            {/* Right Viewer Body */}
            <Box sx={{ flexGrow: 1, position: "relative", bgcolor: (t) => t.palette.mode === "dark" ? "background.default" : "grey.200", height: "calc(100% - 45px)" }}>
              {loadingProposed && (
                <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", height: "100%", gap: 2 }}>
                  <CircularProgress size={36} />
                  <Typography variant="body2" sx={{
                    color: "text.secondary"
                  }}>
                    Verifying SHA-256 and loading proposed revision PDF...
                  </Typography>
                </Box>
              )}

              {proposedError && (
                <Box sx={{ p: 4, display: "flex", justifyContent: "center", alignItems: "center", height: "100%" }}>
                  <Alert severity="error" sx={{ maxWidth: 450 }}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                      Proposed Revision PDF Error
                    </Typography>
                    {proposedError}
                  </Alert>
                </Box>
              )}

              {!proposedFileId && !loadingProposed && (
                <Box sx={{ p: 4, display: "flex", justifyContent: "center", alignItems: "center", height: "100%" }}>
                  <Alert severity="warning" sx={{ maxWidth: 450 }}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                      Missing Controlled PDF
                    </Typography>
                    No controlled PDF is currently attached to proposed revision {proposedRevisionNumber}.
                  </Alert>
                </Box>
              )}

              {!loadingProposed && !proposedError && proposedPdfUrl && (
                <iframe
                  src={`${proposedPdfUrl}#toolbar=0`}
                  title={`Proposed Rev ${proposedRevisionNumber}`}
                  width="100%"
                  height="100%"
                  style={{ border: "none" }}
                />
              )}
            </Box>
          </Grid>
        </Grid>
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 1.5, borderTop: "1px solid", borderColor: "divider", justifyContent: "space-between" }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <VerifiedUserIcon color="primary" fontSize="small" />
          <Typography variant="caption" sx={{
            color: "text.secondary"
          }}>
            MicroLIMS Controlled Dual Inspection Workspace • Cryptographic Integrity Verified
          </Typography>
        </Box>
        <Button onClick={onClose} variant="contained" size="small">
          Done Inspecting
        </Button>
      </DialogActions>
    </Dialog>
  );
}
