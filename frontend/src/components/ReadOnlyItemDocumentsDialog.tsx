import { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  Stack,
  Paper,
  Chip,
  Divider,
  CircularProgress,
  Alert,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  useTheme,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import DescriptionIcon from "@mui/icons-material/Description";
import VerifiedIcon from "@mui/icons-material/Verified";
import DownloadIcon from "@mui/icons-material/Download";
import VisibilityIcon from "@mui/icons-material/Visibility";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { CategoryBadge } from "./StatusBadge";
import {
  ItemDocumentService,
  ItemDocumentDto,
  ItemDocumentType,
  MaterialDocumentStatus,
} from "../modules/laboratoryConfiguration/items/services/ItemDocumentService";

interface ReadOnlyItemDocumentsDialogProps {
  open: boolean;
  itemId: number | null;
  itemName: string;
  itemCode?: string;
  category?: string;
  onClose: () => void;
}

export function ReadOnlyItemDocumentsDialog({
  open,
  itemId,
  itemName,
  itemCode,
  category,
  onClose,
}: ReadOnlyItemDocumentsDialogProps) {
  const theme = useTheme();
  const [documents, setDocuments] = useState<ItemDocumentDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open && itemId) {
      setLoading(true);
      setError(null);
      ItemDocumentService.getDocumentsForItem(itemId)
        .then(setDocuments)
        .catch(() => setError("Failed to load item documents."))
        .finally(() => setLoading(false));
    }
  }, [open, itemId]);

  const currentSop = documents.find((d) => d.documentType === ItemDocumentType.Sop && d.status === MaterialDocumentStatus.Current);
  const currentVr = documents.find(
    (d) => d.documentType === ItemDocumentType.VerificationReport && d.status === MaterialDocumentStatus.Current
  );

  const historicalSops = documents.filter((d) => d.documentType === ItemDocumentType.Sop && d.status !== MaterialDocumentStatus.Current);
  const historicalVrs = documents.filter(
    (d) => d.documentType === ItemDocumentType.VerificationReport && d.status !== MaterialDocumentStatus.Current
  );

  const formatFileSize = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  };

  const formatDate = (dateStr?: string | null) => {
    if (!dateStr) return "—";
    return new Date(dateStr).toLocaleDateString("en-US", { year: "numeric", month: "short", day: "2-digit" });
  };

  const formatDateTime = (dateStr?: string | null) => {
    if (!dateStr) return "—";
    return new Date(dateStr).toLocaleString("en-US", {
      year: "numeric",
      month: "short",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", pb: 1 }}>
        <Box>
          <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 16 }}>
            Controlled Item Documents
          </Typography>
          <Stack direction="row" spacing={1} alignItems="center" sx={{ mt: 0.5 }}>
            <Typography variant="body2" sx={{ color: "text.primary", fontWeight: 700 }}>
              {itemName}
            </Typography>
            {itemCode && (
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                ({itemCode})
              </Typography>
            )}
            {category && <CategoryBadge category={category} />}
          </Stack>
        </Box>
        <Button size="small" onClick={onClose} color="inherit">
          <CloseIcon fontSize="small" />
        </Button>
      </DialogTitle>

      <DialogContent dividers>
        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
            <CircularProgress size={32} />
          </Box>
        ) : error ? (
          <Alert severity="error">{error}</Alert>
        ) : documents.length === 0 ? (
          <Paper sx={{ p: 3, textAlign: "center", border: "1px dashed", borderColor: "divider" }}>
            <Typography variant="body2" sx={{ color: "text.secondary", fontWeight: 500 }}>
              No controlled documents are attached to this item.
            </Typography>
          </Paper>
        ) : (
          <Stack spacing={2.5}>
            {/* SOP Section */}
            <Box>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1, display: "flex", alignItems: "center", gap: 1 }}>
                <DescriptionIcon fontSize="small" sx={{ color: theme.custom.status.purple.text }} />
                SOP (Standard Operating Procedure)
              </Typography>

              {currentSop ? (
                <ReadOnlyDocumentCard
                  doc={currentSop}
                  formatFileSize={formatFileSize}
                  formatDate={formatDate}
                  formatDateTime={formatDateTime}
                />
              ) : (
                <Paper sx={{ p: 1.5, border: "1px dashed", borderColor: "divider", bgcolor: "action.hover" }}>
                  <Typography variant="body2" sx={{ color: "text.secondary", fontSize: 12 }}>
                    No active SOP uploaded for this item.
                  </Typography>
                </Paper>
              )}

              {historicalSops.length > 0 && (
                <Accordion sx={{ mt: 1, border: "1px solid", borderColor: "divider", boxShadow: "none" }}>
                  <AccordionSummary expandIcon={<ExpandMoreIcon fontSize="small" />}>
                    <Typography variant="caption" sx={{ fontWeight: 600, color: "text.secondary" }}>
                      Historical SOP Versions ({historicalSops.length})
                    </Typography>
                  </AccordionSummary>
                  <AccordionDetails sx={{ pt: 0 }}>
                    <Stack spacing={1}>
                      {historicalSops.map((doc) => (
                        <ReadOnlyDocumentCard
                          key={doc.id}
                          doc={doc}
                          formatFileSize={formatFileSize}
                          formatDate={formatDate}
                          formatDateTime={formatDateTime}
                          isHistorical
                        />
                      ))}
                    </Stack>
                  </AccordionDetails>
                </Accordion>
              )}
            </Box>

            <Divider />

            {/* Verification Report Section */}
            <Box>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1, display: "flex", alignItems: "center", gap: 1 }}>
                <VerifiedIcon fontSize="small" sx={{ color: "success.main" }} />
                Verification Report
              </Typography>

              {currentVr ? (
                <ReadOnlyDocumentCard
                  doc={currentVr}
                  formatFileSize={formatFileSize}
                  formatDate={formatDate}
                  formatDateTime={formatDateTime}
                />
              ) : (
                <Paper sx={{ p: 1.5, border: "1px dashed", borderColor: "divider", bgcolor: "action.hover" }}>
                  <Typography variant="body2" sx={{ color: "text.secondary", fontSize: 12 }}>
                    No active Verification Report uploaded for this item.
                  </Typography>
                </Paper>
              )}

              {historicalVrs.length > 0 && (
                <Accordion sx={{ mt: 1, border: "1px solid", borderColor: "divider", boxShadow: "none" }}>
                  <AccordionSummary expandIcon={<ExpandMoreIcon fontSize="small" />}>
                    <Typography variant="caption" sx={{ fontWeight: 600, color: "text.secondary" }}>
                      Historical Verification Reports ({historicalVrs.length})
                    </Typography>
                  </AccordionSummary>
                  <AccordionDetails sx={{ pt: 0 }}>
                    <Stack spacing={1}>
                      {historicalVrs.map((doc) => (
                        <ReadOnlyDocumentCard
                          key={doc.id}
                          doc={doc}
                          formatFileSize={formatFileSize}
                          formatDate={formatDate}
                          formatDateTime={formatDateTime}
                          isHistorical
                        />
                      ))}
                    </Stack>
                  </AccordionDetails>
                </Accordion>
              )}
            </Box>
          </Stack>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 2.5, py: 1.5 }}>
        <Button onClick={onClose} variant="contained" color="primary">
          Close
        </Button>
      </DialogActions>
    </Dialog>
  );
}

function ReadOnlyDocumentCard({
  doc,
  formatFileSize,
  formatDate,
  formatDateTime,
  isHistorical = false,
}: {
  doc: ItemDocumentDto;
  formatFileSize: (b: number) => string;
  formatDate: (d?: string | null) => string;
  formatDateTime: (d?: string | null) => string;
  isHistorical?: boolean;
}) {
  return (
    <Paper
      sx={{
        p: 1.75,
        border: "1px solid",
        borderColor: isHistorical ? "divider" : "primary.main",
        borderRadius: 1.5,
        bgcolor: isHistorical ? "action.hover" : "background.paper",
      }}
    >
      <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1}>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" sx={{ mb: 0.5 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, fontSize: 13, color: "text.primary" }}>
              {doc.originalFileName}
            </Typography>
            <Chip
              label={doc.version}
              size="small"
              color={isHistorical ? "default" : "primary"}
              sx={{ fontWeight: 700, fontSize: 11, height: 20 }}
            />
            {isHistorical && (
              <Chip label="Superseded" size="small" color="warning" sx={{ fontWeight: 600, fontSize: 10, height: 18 }} />
            )}
          </Stack>

          <Typography variant="caption" component="div" sx={{ color: "text.secondary", fontSize: 11 }}>
            Effective: <strong>{formatDate(doc.effectiveDate)}</strong> • Size: {formatFileSize(doc.fileSizeBytes)}
          </Typography>
          <Typography variant="caption" component="div" sx={{ color: "text.secondary", fontSize: 11 }}>
            Uploaded by: <strong>{doc.uploadedByUserName}</strong> on {formatDateTime(doc.uploadedAt)}
          </Typography>
        </Box>

        <Stack direction="row" spacing={1}>
          <Button
            size="small"
            variant="outlined"
            startIcon={<VisibilityIcon fontSize="small" />}
            href={ItemDocumentService.getContentUrl(doc.id)}
            target="_blank"
            rel="noopener noreferrer"
            sx={{ textTransform: "none", fontSize: 11 }}
          >
            View
          </Button>
          <Button
            size="small"
            variant="contained"
            color="primary"
            startIcon={<DownloadIcon fontSize="small" />}
            href={ItemDocumentService.getContentUrl(doc.id, true)}
            sx={{ textTransform: "none", fontSize: 11 }}
          >
            Download
          </Button>
        </Stack>
      </Stack>
    </Paper>
  );
}
