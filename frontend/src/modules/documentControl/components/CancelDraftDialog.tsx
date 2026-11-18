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
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";

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
  const { t } = useTranslation("documentControl");
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
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("cancelDraft.cancelError")));
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
          {t("cancelDraft.title")}
        </Typography>
      </DialogTitle>

      <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
        <Alert severity="info">
          <Trans
            i18nKey="cancelDraft.warning"
            ns="documentControl"
            values={{ rev: revisionNumber, code: companyDocumentCode }}
            components={{ strong: <strong />, em: <em /> }}
          />
        </Alert>

        {error && <Alert severity="error">{error}</Alert>}

        <Box>
          <Typography variant="caption" sx={{ fontWeight: 700, mb: 0.5, display: "block" }}>
            {t("cancelDraft.reasonLabel")}
          </Typography>
          <TextField
            multiline
            rows={3}
            fullWidth
            placeholder={t("cancelDraft.reasonPlaceholder")}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            disabled={loading}
            helperText={t("cancelDraft.charsHelper", { count: reason.trim().length })}
            error={reason.length > 0 && !isValid}
            slotProps={{ htmlInput: { "aria-label": t("cancelDraft.reasonAria") } }}
          />
        </Box>
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={handleClose} disabled={loading} color="inherit">
          {t("cancelDraft.close")}
        </Button>
        <Button
          onClick={handleCancel}
          variant="contained"
          color="warning"
          disabled={!isValid || loading}
          startIcon={loading ? <CircularProgress size={16} color="inherit" /> : null}
        >
          {loading ? t("cancelDraft.cancelling") : t("cancelDraft.submit")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
