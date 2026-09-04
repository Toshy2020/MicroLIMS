import React, { useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Typography,
  Box,
  Alert,
  CircularProgress,
  Paper,
  Divider,
  FormControlLabel,
  Checkbox,
  LinearProgress,
  Chip
} from "@mui/material";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";
import MenuBookIcon from "@mui/icons-material/MenuBook";
import InfoIcon from "@mui/icons-material/Info";
import type { AcknowledgementPresentationDto } from "../types/acknowledgementTypes";

interface DocumentAcknowledgementDialogProps {
  open: boolean;
  context: AcknowledgementPresentationDto | null;
  loading: boolean;
  readingProgress?: number;
  onClose: () => void;
  onConfirmAcknowledgement: (confirmed: boolean, comments?: string) => Promise<void>;
}

export const DocumentAcknowledgementDialog: React.FC<DocumentAcknowledgementDialogProps> = ({
  open,
  context,
  loading,
  readingProgress = 100,
  onClose,
  onConfirmAcknowledgement
}) => {
  const [confirmedStatement, setConfirmedStatement] = useState(false);
  const [comments, setComments] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!confirmedStatement) {
      setError("You must explicitly verify and accept the legal acknowledgement statement.");
      return;
    }

    try {
      setSubmitting(true);
      setError(null);
      await onConfirmAcknowledgement(confirmedStatement, comments.trim() || undefined);
      setConfirmedStatement(false);
      setComments("");
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message || err?.message || "Failed to submit acknowledgement.");
    } finally {
      setSubmitting(false);
    }
  };

  if (!context) {
    return null;
  }

  return (
    <Dialog open={open} onClose={submitting ? undefined : onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ pb: 1, display: "flex", alignItems: "center", gap: 1.5 }}>
        <VerifiedUserIcon color="primary" />
        <Typography variant="h6" component="span" fontWeight="bold">
          Controlled Document Training Acknowledgement
        </Typography>
      </DialogTitle>

      <DialogContent dividers>
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          {error && <Alert severity="error">{error}</Alert>}

          {!context.canAcknowledge && (
            <Alert severity="warning">
              {context.validationMessage || "This document assignment cannot currently be acknowledged."}
            </Alert>
          )}

          {/* Document & Revision Details */}
          <Paper variant="outlined" sx={{ p: 2, bgcolor: "background.default" }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 1.5 }}>
              <div>
                <Typography variant="subtitle2" color="text.secondary">
                  Document Code & Revision
                </Typography>
                <Typography variant="h6" fontWeight="bold">
                  {context.companyDocumentCode} (Rev {context.revisionNumber})
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {context.documentTitle}
                </Typography>
              </div>
              <Chip
                label={`Status: ${context.assignmentStatus}`}
                color={context.assignmentStatus === "Acknowledged" ? "success" : "primary"}
                size="small"
              />
            </Box>

            <Divider sx={{ my: 1.5 }} />

            <Box sx={{ display: "grid", gridTemplateColumns: "repeat(2, 1fr)", gap: 1.5 }}>
              <div>
                <Typography variant="caption" color="text.secondary">Assigned User</Typography>
                <Typography variant="body2" fontWeight="medium">{context.assignedUserName}</Typography>
              </div>
              <div>
                <Typography variant="caption" color="text.secondary">Due Date (UTC)</Typography>
                <Typography variant="body2">
                  {context.dueDateUtc ? new Date(context.dueDateUtc).toLocaleDateString() : "No fixed due date"}
                </Typography>
              </div>
              {context.controlledFileName && (
                <div style={{ gridColumn: "span 2" }}>
                  <Typography variant="caption" color="text.secondary">Controlled File Artifact</Typography>
                  <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.8rem" }}>
                    {context.controlledFileName}
                  </Typography>
                </div>
              )}
            </Box>
          </Paper>

          {/* Reading Progress Indicator (Informational Only - DC-URS-190) */}
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
              <MenuBookIcon color="action" fontSize="small" />
              <Typography variant="subtitle2" fontWeight="bold">
                Reading Verification Progress
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ ml: "auto" }}>
                {readingProgress}% View Complete
              </Typography>
            </Box>
            <LinearProgress
              variant="determinate"
              value={Math.min(100, Math.max(0, readingProgress))}
              sx={{ height: 8, borderRadius: 1 }}
              color={readingProgress >= 100 ? "success" : "primary"}
            />
            <Box sx={{ display: "flex", alignItems: "center", gap: 0.5, mt: 1 }}>
              <InfoIcon fontSize="inherit" color="action" />
              <Typography variant="caption" color="text.secondary">
                Progress tracks viewport/scroll interaction. Legal acknowledgement requires the explicit confirmation below.
              </Typography>
            </Box>
          </Paper>

          {/* Legal Evidentiary Statement (DC-URS-191, DC-URS-189) */}
          <Paper
            variant="outlined"
            sx={{
              p: 2,
              bgcolor: "#f8f9fa",
              borderColor: "primary.main",
              borderLeftWidth: 4
            }}
          >
            <Typography variant="subtitle2" color="primary" fontWeight="bold" gutterBottom>
              Legal & Regulatory Evidentiary Statement (GMP / 21 CFR Part 11)
            </Typography>
            <Typography
              variant="body2"
              sx={{
                fontStyle: "italic",
                lineHeight: 1.6,
                color: "text.primary"
              }}
            >
              "{context.legalStatementText}"
            </Typography>

            <Divider sx={{ my: 1.5 }} />

            <FormControlLabel
              control={
                <Checkbox
                  checked={confirmedStatement}
                  onChange={(e) => setConfirmedStatement(e.target.checked)}
                  color="primary"
                  disabled={!context.canAcknowledge || submitting}
                />
              }
              label={
                <Typography variant="body2" fontWeight="bold">
                  I explicitly confirm that I have read, understood, and agree to comply with this controlled document revision.
                </Typography>
              }
            />
          </Paper>

          {/* Optional Comments */}
          <TextField
            label="Acknowledgement Remarks (Optional)"
            multiline
            rows={2}
            fullWidth
            size="small"
            value={comments}
            onChange={(e) => setComments(e.target.value)}
            disabled={!context.canAcknowledge || submitting}
            placeholder="Enter any relevant training or understanding notes (will be recorded in audit log)..."
          />
        </Box>
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={submitting} color="inherit">
          Cancel
        </Button>
        <Button
          onClick={() => handleSubmit()}
          variant="contained"
          color="primary"
          disabled={!context.canAcknowledge || !confirmedStatement || submitting}
          startIcon={submitting ? <CircularProgress size={18} color="inherit" /> : <VerifiedUserIcon />}
        >
          {submitting ? "Recording Evidence..." : "Consciously Acknowledge"}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
