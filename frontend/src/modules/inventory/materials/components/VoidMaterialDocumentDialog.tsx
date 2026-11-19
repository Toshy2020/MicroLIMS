import { useState } from "react";
import { Button, TextField, Alert, LinearProgress } from "@mui/material";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";
import { Ltr } from "../../../../components/Ltr";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { MaterialService } from "../services/MaterialService";
import type { MaterialDocument } from "../types/materialTypes";

interface Props {
  open: boolean;
  document: MaterialDocument;
  materialId: number;
  onClose: () => void;
  onSuccess: () => void;
}

export function VoidMaterialDocumentDialog({ open, document, materialId, onClose, onSuccess }: Props) {
  const { t } = useTranslation("inventory");
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async () => {
    setReasonError(false);
    if (!reason.trim()) { setReasonError(true); return; }

    setSubmitting(true);
    setError(null);
    try {
      await MaterialService.voidDocument(document.id, materialId, reason.trim());
      onSuccess();
    } catch (caught: unknown) {
      const err = caught as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? t("materials.void.failed"));
    } finally {
      setSubmitting(false);
    }
  };

  const handleClose = () => {
    if (submitting) return;
    setReason(""); setReasonError(false); setError(null);
    onClose();
  };

  return (
    <FloatingDialog
      open={open}
      title={t("materials.void.title")}
      onClose={handleClose}
      actions={
        <>
          <Button onClick={handleClose} disabled={submitting}>{t("common.cancel")}</Button>
          <Button
            id="void-doc-submit"
            variant="contained"
            color="error"
            onClick={handleSubmit}
            disabled={submitting || !reason.trim()}
          >
            {submitting ? t("materials.void.submitting") : t("materials.void.title")}
          </Button>
        </>
      }
    >
      {submitting && <LinearProgress sx={{ mb: 2 }} />}
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Alert severity="warning" sx={{ mb: 2, fontSize: 12 }}>
        <Trans i18nKey="materials.void.info" ns="inventory" values={{ name: document.originalFileName }} components={{ b: <strong />, ltr: <Ltr>{null}</Ltr> }} />
      </Alert>

      <TextField
        id="void-reason"
        label={t("materials.void.reason")}
        fullWidth
        size="small"
        multiline
        rows={2}
        value={reason}
        onChange={(e) => { setReason(e.target.value); setReasonError(false); }}
        error={reasonError}
        helperText={reasonError ? t("materials.supersede.reasonRequired") : undefined}
      />
    </FloatingDialog>
  );
}
