import { useState, useEffect } from "react";
import { Trans } from "react-i18next";
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
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
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
  const { t } = useTranslation("icpWorkspace");
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
      setError(e.response?.data?.message ?? e.message ?? t("run.startDialog.loadError"));
    } finally {
      setStarting(false);
    }
  };

  return (
    <Dialog open={open} onClose={starting ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ fontWeight: 700, pb: 1 }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
          <BiotechIcon color="primary" />
          <span>{t("run.startDialog.title")}</span>
        </Stack>
        {equipmentName && (
          <Typography variant="body2" sx={{ color: "text.secondary", fontWeight: 400, mt: 0.5 }}>
            <Trans
              i18nKey="run.startDialog.instrument"
              ns="icpWorkspace"
              values={{ name: equipmentName, code: equipmentCode ?? "" }}
              components={{ ltr: <Ltr>{null}</Ltr>, strong: <strong /> }}
            />
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
          {t("run.startDialog.description")}
        </Typography>
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={starting}>
          {t("run.startDialog.cancel")}
        </Button>
        <Button
          variant="contained"
          startIcon={starting ? <CircularProgress size={16} color="inherit" /> : <PlayArrowIcon />}
          onClick={handleStart}
          disabled={starting}
          sx={{ textTransform: "none" }}
        >
          {starting ? t("run.startDialog.starting") : t("run.startDialog.start")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
