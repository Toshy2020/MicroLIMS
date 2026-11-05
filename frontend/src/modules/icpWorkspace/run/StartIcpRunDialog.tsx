import { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Typography,
  Alert,
  CircularProgress,
  Stack
} from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import BiotechIcon from "@mui/icons-material/Biotech";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import type { IcpRunDto } from "../types";

export interface StartIcpRunDialogProps {
  open: boolean;
  equipmentId: number;
  equipmentName?: string;
  equipmentCode?: string;
  onClose: () => void;
  onRunStarted: (run: IcpRunDto) => void;
}

// A run needs no method: the analyst lists the calibrated elements on the calibration step, and each
// test defines the elements and targets it reports.
export function StartIcpRunDialog({
  open,
  equipmentId,
  equipmentName,
  equipmentCode,
  onClose,
  onRunStarted
}: StartIcpRunDialogProps) {
  const [starting, setStarting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) setError(null);
  }, [open]);

  const handleStart = async () => {
    setStarting(true);
    setError(null);
    try {
      const newRun = await IcpWorkspaceService.startRun({ equipmentId });
      onRunStarted(newRun);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Failed to start ICP run.");
    } finally {
      setStarting(false);
    }
  };

  return (
    <Dialog open={open} onClose={starting ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ fontWeight: 700, pb: 1 }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
          <BiotechIcon color="primary" />
          <span>Start ICP Run</span>
        </Stack>
        {equipmentName && (
          <Typography variant="body2" sx={{ color: "text.secondary", fontWeight: 400, mt: 0.5 }}>
            Instrument: <strong>{equipmentName}</strong> ({equipmentCode})
          </Typography>
        )}
      </DialogTitle>

      <DialogContent dividers>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
            {error}
          </Alert>
        )}
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          The run starts with a pending calibration. Record the standard lot, the elements calibrated and their r,
          attach the calibration curve report and sign to declare the instrument calibrated and ready for testing.
        </Typography>
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={starting}>
          Cancel
        </Button>
        <Button
          variant="contained"
          startIcon={starting ? <CircularProgress size={16} color="inherit" /> : <PlayArrowIcon />}
          onClick={handleStart}
          disabled={starting}
          sx={{ textTransform: "none" }}
        >
          {starting ? "Starting Run..." : "Start Run"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
