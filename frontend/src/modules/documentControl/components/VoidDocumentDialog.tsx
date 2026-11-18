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
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";

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
  const { t } = useTranslation("documentControl");
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
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("voidDocument.voidError")));
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
          {t("voidDocument.title")}
        </Typography>
      </DialogTitle>

      <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
        <Alert severity="warning">
          <Trans
            i18nKey="voidDocument.warning"
            ns="documentControl"
            values={{ code: companyDocumentCode }}
            components={{ strong: <strong />, code: <code /> }}
          />
        </Alert>

        <Box sx={{ p: 1.5, bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography variant="body2">
            <Trans
              i18nKey="voidDocument.codeLine"
              ns="documentControl"
              values={{ code: companyDocumentCode }}
              components={{ strong: <strong /> }}
            />
          </Typography>
          <Typography variant="body2">
            <Trans
              i18nKey="voidDocument.titleLine"
              ns="documentControl"
              values={{ title: documentTitle }}
              components={{ strong: <strong /> }}
            />
          </Typography>
        </Box>

        {error && <Alert severity="error">{error}</Alert>}

        <Box>
          <Typography variant="caption" sx={{ fontWeight: 700, mb: 0.5, display: "block" }}>
            {t("voidDocument.reasonLabel")}
          </Typography>
          <TextField
            multiline
            rows={3}
            fullWidth
            placeholder={t("voidDocument.reasonPlaceholder")}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            disabled={loading}
            helperText={t("voidDocument.charsHelper", { count: reason.trim().length })}
            error={reason.length > 0 && !isValid}
            slotProps={{ htmlInput: { "aria-label": t("voidDocument.reasonAria") } }}
          />
        </Box>
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={handleClose} disabled={loading} color="inherit">
          {t("common.cancel")}
        </Button>
        <Button
          onClick={handleVoid}
          variant="contained"
          color="error"
          disabled={!isValid || loading}
          startIcon={loading ? <CircularProgress size={16} color="inherit" /> : null}
        >
          {loading ? t("voidDocument.voiding") : t("voidDocument.submit")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
