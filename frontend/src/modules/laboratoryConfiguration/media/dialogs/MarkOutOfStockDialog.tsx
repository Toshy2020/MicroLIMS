import { useState } from "react";
import {
  Button,
  TextField,
  Stack,
  Box,
  Typography,
  Alert,
} from "@mui/material";
import InventoryOutlinedIcon from "@mui/icons-material/InventoryOutlined";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";
import { mediaClassLabel } from "../../../../services/masterDataOptions";
import { apiClient } from "../../../../services/apiClient";
import { FloatingDialog } from "../../../../components/FloatingDialog";

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
  onSuccess
}: MarkOutOfStockDialogProps) {
  const { t } = useTranslation("labConfig");
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
      setError(err?.response?.data?.message || err?.message || t("media.markOutOfStock.error"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      maxWidth="xs"
      titleSx={{ fontWeight: 700, fontSize: 16, display: "flex", alignItems: "center", gap: 1 }}
      title={
        <>
          <InventoryOutlinedIcon color="action" />
          {t("media.markOutOfStock.title")}
        </>
      }
      actions={
        <>
          <Button onClick={onClose} disabled={submitting} color="inherit">
            {t("media.markOutOfStock.cancel")}
          </Button>
          <Button variant="contained" color="warning" onClick={handleConfirm} disabled={submitting}>
            {submitting ? t("media.markOutOfStock.processing") : t("media.markOutOfStock.confirm")}
          </Button>
        </>
      }
    >
        <Stack spacing={2} sx={{ mt: 0.5 }}>
          {error && <Alert severity="error">{error}</Alert>}

          <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
            <Stack spacing={0.75}>
              <Box sx={{ display: "flex", justifyContent: "space-between" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600 }}>
                  {t("media.markOutOfStock.mediaLotLabel")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 700 }}>
                  {lot.lotNumber}
                </Typography>
              </Box>

              <Box sx={{ display: "flex", justifyContent: "space-between" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600 }}>
                  {t("media.markOutOfStock.mediaTypeLabel")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {mediaClassLabel(lot.mediaType?.class)}
                </Typography>
              </Box>

              <Box sx={{ display: "flex", justifyContent: "space-between" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600 }}>
                  {t("media.markOutOfStock.currentStatusLabel")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, color: "success.main" }}>
                  {t("media.markOutOfStock.released")}
                </Typography>
              </Box>

              <Box sx={{ display: "flex", justifyContent: "space-between" }}>
                <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600 }}>
                  {t("media.markOutOfStock.newStatusLabel")}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 700, color: "text.primary" }}>
                  {t("media.markOutOfStock.outOfStock")}
                </Typography>
              </Box>
            </Stack>
          </Box>

          <Alert severity="warning" sx={{ fontSize: 12 }}>
            <Trans
              i18nKey="media.markOutOfStock.warning"
              ns="labConfig"
              components={{ bold: <strong /> }}
            />
          </Alert>

          <TextField
            size="small"
            label={t("media.markOutOfStock.reasonLabel")}
            multiline
            rows={2}
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            placeholder={t("media.markOutOfStock.reasonPlaceholder")}
            fullWidth
          />
        </Stack>
    </FloatingDialog>
  );
}
