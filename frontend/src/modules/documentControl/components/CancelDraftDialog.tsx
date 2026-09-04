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
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import { documentControlService } from "../services/documentControlService";

interface CancelDraftDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
  revisionId: number;
  revisionNumber: string;
  companyDocumentCode: string;
}

export function CancelDraftDialog({
  open,
  onClose,
  onSuccess,
  revisionId,
  revisionNumber,
  companyDocumentCode
}: CancelDraftDialogProps) {
  const [reason, setReason] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isValid = reason.trim().length >= 10;

  const handleCancel = async () => {
    if (!isValid) return;

    setLoading(true);
    setError(null);

    try {
      await documentControlService.cancelDraftRevision(revisionId, reason.trim());
      setReason("");
      onSuccess();
      onClose();
    } catch (err: any) {
      const msg = err.response?.data?.message || err.message || "Failed to cancel draft revision.";
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
        <CancelOutlinedIcon color="warning" />
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          CANCEL DRAFT REVISION
        </Typography>
      </DialogTitle>

      <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
        <Alert severity="info">
          Cancelling will move Revision <strong>{revisionNumber}</strong> of <strong>{companyDocumentCode}</strong> to <em>Cancelled</em> status and deactivate any active draft files. This action cannot be undone.
        </Alert>

        {error && <Alert severity="error">{error}</Alert>}

        <Box>
          <Typography variant="caption" sx={{ fontWeight: 700, mb: 0.5, display: "block" }}>
            Mandatory Cancellation Reason (minimum 10 characters required) *
          </Typography>
          <TextField
            multiline
            rows={3}
            fullWidth
            placeholder="State why this draft revision is being cancelled..."
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
          Close
        </Button>
        <Button
          onClick={handleCancel}
          variant="contained"
          color="warning"
          disabled={!isValid || loading}
          startIcon={loading ? <CircularProgress size={16} color="inherit" /> : null}
        >
          {loading ? "Cancelling..." : "Confirm Cancel Draft"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
