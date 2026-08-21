import { useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Stack,
  Box,
  Typography,
  Alert,
} from "@mui/material";
import InventoryOutlinedIcon from "@mui/icons-material/InventoryOutlined";
import { mediaClassLabel } from "../../../../services/masterDataOptions";
import { apiClient } from "../../../../services/apiClient";

interface MarkOutOfStockDialogProps {
  open: boolean;
  lot: any | null;
  onClose: () => void;
  onSuccess: () => void;
}

export function MarkOutOfStockDialog({
  open,
  lot,
  onClose,
  onSuccess,
}: MarkOutOfStockDialogProps) {
  const [comment, setComment] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!lot) return null;

  const handleConfirm = async () => {
    setSubmitting(true);
    setError(null);
    try {
      await apiClient.post(`/media/${lot.id}/mark-out-of-stock`, { comment: comment.trim() || null });
      onSuccess();
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message || err?.message || "Failed to mark media lot out of stock.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle sx={{ fontWeight: 700, fontSize: 16, display: "flex", alignItems: "center", gap: 1 }}>
        <InventoryOutlinedIcon color="action" />
        Mark Media Lot Out of Stock
      </DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2} sx={{ mt: 0.5 }}>
          {error && <Alert severity="error">{error}</Alert>}

          <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
            <Stack spacing={0.75}>
              <Box sx={{ display: "flex", justifyContent: "space-between" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600 }}>
                  Media Lot:
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 700 }}>
                  {lot.lotNumber}
                </Typography>
              </Box>

              <Box sx={{ display: "flex", justifyContent: "space-between" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600 }}>
                  Media Type:
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {mediaClassLabel(lot.mediaType?.class)}
                </Typography>
              </Box>

              <Box sx={{ display: "flex", justifyContent: "space-between" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600 }}>
                  Current Status:
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, color: "success.main" }}>
                  Released
                </Typography>
              </Box>

              <Box sx={{ display: "flex", justifyContent: "space-between" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600 }}>
                  New Status:
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 700, color: "text.primary" }}>
                  Out of Stock
                </Typography>
              </Box>
            </Stack>
          </Box>

          <Alert severity="warning" sx={{ fontSize: 12 }}>
            <strong>Warning:</strong> This media lot will no longer be available for laboratory testing.
          </Alert>

          <TextField
            size="small"
            label="Reason / Comment (Optional)"
            multiline
            rows={2}
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            placeholder="e.g. Prepared volume exhausted during routine testing."
            fullWidth
          />
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 2.5, py: 1.5 }}>
        <Button onClick={onClose} disabled={submitting} color="inherit">
          Cancel
        </Button>
        <Button variant="contained" color="warning" onClick={handleConfirm} disabled={submitting}>
          {submitting ? "Processing..." : "Confirm Out of Stock"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
