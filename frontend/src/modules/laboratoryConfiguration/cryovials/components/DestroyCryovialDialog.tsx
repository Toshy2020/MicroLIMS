import { useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Typography,
  Box,
  Alert,
  Paper,
  Stack
} from "@mui/material";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { CryovialItem } from "../types/cryovialTypes";
import { brandColors } from "../../../../theme";

interface DestroyCryovialDialogProps {
  open: boolean;
  cryovial: CryovialItem | null;
  onCancel: () => void;
  onConfirm: () => Promise<void>;
}

export function DestroyCryovialDialog({
  open,
  cryovial,
  onCancel,
  onConfirm
}: DestroyCryovialDialogProps) {
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (!cryovial) return null;

  const organismName = cryovial.organism?.scientificName ?? cryovial.organismNameSnapshot;

  const handleConfirm = async () => {
    setSubmitting(true);
    setError(null);
    try {
      await onConfirm();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Failed to destroy cryovial batch.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={submitting ? undefined : onCancel}
      maxWidth="sm"
      fullWidth
      PaperProps={{
        sx: {
          borderRadius: 2,
          p: 0.5
        }
      }}
    >
      <DialogTitle sx={{ pb: 1, display: "flex", alignItems: "center", gap: 1 }}>
        <Box
          sx={{
            width: 32,
            height: 32,
            borderRadius: 1,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            bgcolor: "#fee2e2",
            color: "#dc2626"
          }}
        >
          <DeleteOutlineIcon fontSize="small" />
        </Box>
        <Typography sx={{ fontSize: 18, fontWeight: 700, color: "#1f2937" }}>
          Confirm Batch Destruction
        </Typography>
      </DialogTitle>

      <DialogContent dividers sx={{ pt: 2 }}>
        <Stack spacing={2}>
          {error && <Alert severity="error">{error}</Alert>}

          <Alert severity="warning" icon={<WarningAmberIcon />}>
            Destroying this cryovial batch will decommission it permanently. It will no longer be available for thawing, Media Evaluation, or GPT testing.
          </Alert>

          <Paper
            variant="outlined"
            sx={{
              p: 2,
              bgcolor: "#f8fafc",
              borderColor: "#e2e8f0",
              borderRadius: 1.5
            }}
          >
            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" },
                gap: 1.5
              }}
            >
              <Box>
                <Typography sx={{ fontSize: 11, fontWeight: 600, color: "text.secondary", textTransform: "uppercase" }}>
                  Cryovial Code
                </Typography>
                <Typography sx={{ fontSize: 14, fontWeight: 700, fontFamily: "monospace", color: brandColors.sectionTitle }}>
                  {cryovial.code}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, fontWeight: 600, color: "text.secondary", textTransform: "uppercase" }}>
                  Organism
                </Typography>
                <Typography sx={{ fontSize: 13, fontWeight: 600, color: "#1f2937" }}>
                  {organismName}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, fontWeight: 600, color: "text.secondary", textTransform: "uppercase" }}>
                  Vials Remaining
                </Typography>
                <Typography sx={{ fontSize: 13, fontWeight: 700, color: "#dc2626" }}>
                  {cryovial.vialsRemaining} of {cryovial.numberOfVialsPrepared}
                </Typography>
              </Box>
            </Box>
          </Paper>
        </Stack>
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onCancel} disabled={submitting} sx={{ color: "#4b5563" }}>
          Cancel
        </Button>
        <Button
          variant="contained"
          color="error"
          onClick={handleConfirm}
          disabled={submitting}
          startIcon={<DeleteOutlineIcon />}
        >
          {submitting ? "Destroying..." : "Confirm Destruction"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
