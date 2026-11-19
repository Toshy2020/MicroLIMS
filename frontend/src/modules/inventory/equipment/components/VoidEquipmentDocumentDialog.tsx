import { useState } from "react";
import { Button, TextField, Alert, LinearProgress } from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { EquipmentInventoryService } from "../services/EquipmentInventoryService";
import type { EquipmentDocument } from "../types/equipmentTypes";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";

interface Props {
  open: boolean;
  document: EquipmentDocument;
  equipmentId: number;
  onClose: () => void;
  onSuccess: () => void;
}

export function VoidEquipmentDocumentDialog({ open, document, equipmentId, onClose, onSuccess }: Props) {
  const { t } = useTranslation("inventory");
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async () => {
    setReasonError(false);
    if (!reason.trim()) {
      setReasonError(true);
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await EquipmentInventoryService.voidDocument(document.id, equipmentId, reason.trim());
      onSuccess();
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { message?: string } } };
      setError(apiErr?.response?.data?.message ?? t("equipment.docDialogs.void.failed"));
    } finally {
      setSubmitting(false);
    }
  };

  const handleClose = () => {
    if (submitting) return;
    setReason("");
    setReasonError(false);
    setError(null);
    onClose();
  };

  return (
    <FloatingDialog
      open={open}
      title={t("equipment.docDialogs.void.title")}
      onClose={handleClose}
      actions={
        <>
          <Button onClick={handleClose} disabled={submitting}>
            {t("common.cancel")}
          </Button>
          <Button
            id="void-equip-doc-submit"
            variant="contained"
            color="error"
            onClick={handleSubmit}
            disabled={submitting || !reason.trim()}
          >
            {submitting ? t("equipment.docDialogs.void.submitting") : t("equipment.docDialogs.void.submit")}
          </Button>
        </>
      }
    >
      {submitting && <LinearProgress sx={{ mb: 2 }} />}
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Alert severity="warning" sx={{ mb: 2, fontSize: 12 }}>
        <Trans
          i18nKey="equipment.docDialogs.void.warning"
          ns="inventory"
          values={{ fileName: document.originalFileName }}
          components={{ bold: <strong /> }}
        />
      </Alert>

      <TextField
        id="void-equip-reason"
        label={t("equipment.docDialogs.void.reasonLabel")}
        fullWidth
        size="small"
        multiline
        rows={2}
        value={reason}
        onChange={(e) => {
          setReason(e.target.value);
          setReasonError(false);
        }}
        error={reasonError}
        helperText={reasonError ? t("equipment.docDialogs.reasonRequired") : undefined}
      />
    </FloatingDialog>
  );
}
