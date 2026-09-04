import { useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  TextField,
  Alert,
  CircularProgress
} from "@mui/material";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import { documentControlService } from "../services/documentControlService";

interface VoidDocumentDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
  documentMasterId: number;
  companyDocumentCode: string;
  documentTitle: string;
}

export function VoidDocumentDialog({
  open,
  onClose,
  onSuccess,
  documentMasterId,
  companyDocumentCode,
  documentTitle
}: VoidDocumentDialogProps) {
  const [reason, setReason] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isValid = reason.trim().length >= 10;

  const handleVoid = async () => {
    if (!isValid) return;

    setLoading(true);
    setError(null);

    try {
      await documentControlService.voidMaster(documentMasterId, reason.trim());
      setReason("");
      onSuccess();
      onClose();
    } catch (err: any) {
      const msg = err.response?.data?.message || err.message || "Failed to void Document Master.";
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const handleClose = () => {
    if (!loading) {
      setReason("");
      setError(null);
      onClose();
    }
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ pb: 1, display: "flex", alignItems: "center", gap: 1 }}>
        <WarningAmberOutlinedIcon color="error" />
        <Typography variant="h6" sx={{ fontWeight: 700, color: "error.main" }}>
          VOID DOCUMENT MASTER
        </Typography>
      </DialogTitle>

      <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
        <Alert severity="warning">
          <strong>Quality Governance Action:</strong> Voiding permanently removes this Document Master from the active register and marks it as Void. It releases the Company Code <code>{companyDocumentCode}</code> for controlled reuse. The document and its complete revision history remain permanently preserved in the immutable audit trail.
        </Alert>

        <Box sx={{ p: 1.5, bgcolor: "grey.50", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography variant="body2">
            Document Code: <strong>{companyDocumentCode}</strong>
          </Typography>
          <Typography variant="body2">
            Title: <strong>{documentTitle}</strong>
          </Typography>
        </Box>

        {error && <Alert severity="error">{error}</Alert>}

        <Box>
          <Typography variant="caption" sx={{ fontWeight: 700, mb: 0.5, display: "block" }}>
            Mandatory Void Reason (minimum 10 characters required) *
          </Typography>
          <TextField
            multiline
            rows={3}
            fullWidth
            placeholder="State the regulatory / administrative justification for voiding this document..."
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            disabled={loading}
            helperText={`${reason.trim().length}/10 characters minimum`}
            error={reason.length > 0 && !isValid}
          />
        </Box>
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={handleClose} disabled={loading} color="inherit">
          Cancel
        </Button>
        <Button
          onClick={handleVoid}
          variant="contained"
          color="error"
          disabled={!isValid || loading}
          startIcon={loading ? <CircularProgress size={16} color="inherit" /> : null}
        >
          {loading ? "Voiding Record..." : "Confirm Void Record"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
