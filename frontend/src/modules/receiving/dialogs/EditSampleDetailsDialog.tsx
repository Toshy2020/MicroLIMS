import { useEffect, useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Box,
  Typography,
  Alert,
  CircularProgress
} from "@mui/material";
import { SampleRecord } from "../types/receivingTypes";
import { ReceiveService } from "../services/ReceiveService";
import { brandColors } from "../../../theme";

interface Props {
  open: boolean;
  sample: SampleRecord | null;
  onClose: () => void;
  onSuccess: () => void;
}

export function EditSampleDetailsDialog({ open, sample, onClose, onSuccess }: Props) {
  const [batchNumber, setBatchNumber] = useState("");
  const [controlNumber, setControlNumber] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (sample) {
      setBatchNumber(sample.batchNumber || "");
      setControlNumber(sample.controlNumber || "");
      setError(null);
    }
  }, [sample, open]);

  if (!sample) return null;

  const handleSave = async () => {
    setError(null);
    setLoading(true);
    try {
      await ReceiveService.correctSample(sample.sampleId, batchNumber, controlNumber);
      onSuccess();
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message || err?.message || "Failed to update sample details.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onClose={loading ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        <Typography sx={{ fontSize: 18, fontWeight: 700, color: brandColors.pageTitle }}>
          Edit Sample Information
        </Typography>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          Sample #{sample.sampleId} — {sample.displayName} ({sample.referenceNumber})
        </Typography>
      </DialogTitle>

      <DialogContent>
        {error && (
          <Alert severity="error" sx={{ mb: 2, fontSize: 13 }}>
            {error}
          </Alert>
        )}

        <Box sx={{ display: "flex", flexDirection: "column", gap: 2, pt: 1 }}>
          <TextField
            label="Batch Number"
            size="small"
            fullWidth
            value={batchNumber}
            onChange={(e) => setBatchNumber(e.target.value)}
          />

          <TextField
            label="Control Number"
            size="small"
            fullWidth
            value={controlNumber}
            onChange={(e) => setControlNumber(e.target.value)}
          />

          <Typography sx={{ fontSize: 11, color: "text.secondary", bgcolor: "#f9fafb", p: 1.25, borderRadius: 1 }}>
            Note: Corrections are logged in the audit trail. Once incubation has started, sample identifiers are frozen.
          </Typography>
        </Box>
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={loading} color="inherit">
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSave}
          disabled={loading}
          sx={{
            bgcolor: brandColors.sectionTitle,
            fontWeight: 600,
            "&:hover": { bgcolor: "#631f74" }
          }}
        >
          {loading ? <CircularProgress size={20} color="inherit" /> : "Save Changes"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
