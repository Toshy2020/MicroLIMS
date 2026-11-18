import { useState } from "react";
import { useTranslation } from "../../../../i18n";
import {
  Button,
  Typography,
  Box,
  Alert,
  Paper,
  Stack,
  useTheme
} from "@mui/material";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { CryovialItem } from "../types/cryovialTypes";
import { FloatingDialog } from "../../../../components/FloatingDialog";

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
  const { t } = useTranslation("labConfig");
  const theme = useTheme();
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
      setError(e?.response?.data?.message ?? t("cryovials.destroy.error"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      onClose={() => { if (!submitting) onCancel(); }}
      maxWidth="sm"
      paperSx={{ borderRadius: 2, p: 0.5 }}
      titleSx={{ pb: 1, display: "flex", alignItems: "center", gap: 1 }}
      title={
        <>
          <Box
            sx={{
              width: 32,
              height: 32,
              borderRadius: 1,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              bgcolor: theme.custom.status.detected.bg,
              color: theme.custom.status.detected.text
            }}
          >
            <DeleteOutlineIcon fontSize="small" />
          </Box>
          <Typography sx={{ fontSize: 18, fontWeight: 700, color: "text.primary" }}>
            {t("cryovials.destroy.title")}
          </Typography>
        </>
      }
      actions={
        <>
          <Button onClick={onCancel} disabled={submitting} sx={{ color: "text.secondary" }}>
            {t("cryovials.destroy.cancel")}
          </Button>
          <Button
            variant="contained"
            color="error"
            onClick={handleConfirm}
            disabled={submitting}
            startIcon={<DeleteOutlineIcon />}
          >
            {submitting ? t("cryovials.destroy.destroying") : t("cryovials.destroy.confirm")}
          </Button>
        </>
      }
    >
        <Stack spacing={2}>
          {error && <Alert severity="error">{error}</Alert>}

          <Alert severity="warning" icon={<WarningAmberIcon />}>
            {t("cryovials.destroy.warning")}
          </Alert>

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
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary", textTransform: "uppercase" }}>
                  {t("cryovials.destroy.code")}
                </Typography>
                <Typography sx={{ fontSize: 14, fontWeight: 700, fontFamily: "monospace", color: theme.palette.primary.main }}>
                  {cryovial.code}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary", textTransform: "uppercase" }}>
                  {t("cryovials.destroy.organism")}
                </Typography>
                <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                  {organismName}
                </Typography>
              </Box>

              <Box>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary", textTransform: "uppercase" }}>
                  {t("cryovials.destroy.vialsRemaining")}
                </Typography>
                <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.custom.status.detected.text }}>
                  {t("cryovials.destroy.ofPrepared", { remaining: cryovial.vialsRemaining, prepared: cryovial.numberOfVialsPrepared })}
                </Typography>
              </Box>
            </Box>
          </Paper>
        </Stack>
    </FloatingDialog>
  );
}
