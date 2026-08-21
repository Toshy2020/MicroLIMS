import { useState, useEffect } from "react";
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
  Paper,
  Stack,
  useTheme
} from "@mui/material";
import AcUnitIcon from "@mui/icons-material/AcUnit";
import { CryovialItem } from "../types/cryovialTypes";
import { brandColors } from "../../../../theme";

interface ThawVialReasonDialogProps {
  open: boolean;
  cryovial: CryovialItem | null;
  onCancel: () => void;
  onConfirm: (reason: string) => Promise<void>;
}

export function ThawVialReasonDialog({
  open,
  cryovial,
  onCancel,
  onConfirm
}: ThawVialReasonDialogProps) {
  const theme = useTheme();
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open) {
      setReason("");
      setError(null);
      setSubmitting(false);
    }
  }, [open, cryovial]);

  if (!cryovial) return null;

  const organismName = cryovial.organism?.scientificName ?? cryovial.organismNameSnapshot;
  const isReasonValid = reason.trim().length >= 5 && reason.length <= 500;

  const handleConfirm = async () => {
    if (!isReasonValid || submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      await onConfirm(reason.trim());
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Failed to thaw cryovial.");
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
            bgcolor: theme.custom.status.info.bg,
            color: theme.custom.status.info.text
          }}
        >
          <AcUnitIcon fontSize="small" />
        </Box>
        <Typography sx={{ fontSize: 18, fontWeight: 700, color: "text.primary" }}>
          Thaw Vial — Provide Reason
        </Typography>
      </DialogTitle>

      <DialogContent dividers sx={{ pt: 2 }}>
        <Stack spacing={2.5}>
          {error && <Alert severity="error">{error}</Alert>}

          {/* Cryovial Identity Box */}
          <Paper
            variant="outlined"
            sx={{
              p: 2,
              bgcolor: "background.default",
              borderColor: "divider",
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
                <Typography sx={{ fontSize: 14, fontWeight: 700, fontFamily: "monospace", color: theme.palette.primary.main }}>
                  {cryovial.code}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, fontWeight: 600, color: "text.secondary", textTransform: "uppercase" }}>
                  Organism
                </Typography>
                <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                  {organismName}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 11, fontWeight: 600, color: "text.secondary", textTransform: "uppercase" }}>
                  Available Vials
                </Typography>
                <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.custom.status.notDetected.text }}>
                  {cryovial.vialsRemaining} of {cryovial.numberOfVialsPrepared} vials
                </Typography>
              </Box>
            </Box>
          </Paper>

          {/* Reason for Thawing (Free Text Only) */}
          <Box>
            <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary", mb: 0.75 }}>
              Reason for Thawing <Box component="span" sx={{ color: theme.custom.status.detected.text }}>*</Box>
            </Typography>
            <TextField
              fullWidth
              multiline
              rows={3}
              placeholder="Enter reason for thawing this cryovial..."
              value={reason}
              onChange={(e) => setReason(e.target.value.slice(0, 500))}
              disabled={submitting}
              autoFocus
              error={reason.length > 0 && reason.trim().length < 5}
              helperText={
                reason.length > 0 && reason.trim().length < 5
                  ? "Reason must be at least 5 characters."
                  : ""
              }
            />
            <Box sx={{ display: "flex", justifyContent: "space-between", mt: 0.5 }}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                GMP justification for vial retrieval
              </Typography>
              <Typography
                sx={{
                  fontSize: 11,
                  fontWeight: 600,
                  color: reason.length >= 450 ? theme.custom.status.action.text : "text.secondary"
                }}
              >
                {reason.length} / 500 characters
              </Typography>
            </Box>
          </Box>
        </Stack>
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onCancel} disabled={submitting} sx={{ color: "text.secondary" }}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleConfirm}
          disabled={!isReasonValid || submitting}
          startIcon={<AcUnitIcon />}
          sx={{
            bgcolor: brandColors.sectionTitle,
            "&:hover": { bgcolor: brandColors.pageTitle }
          }}
        >
          {submitting ? "Thawing..." : "Confirm Thaw"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
