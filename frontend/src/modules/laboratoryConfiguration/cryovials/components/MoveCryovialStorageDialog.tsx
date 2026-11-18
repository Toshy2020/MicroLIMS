import { useEffect, useState } from "react";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";
import { Alert, Button, Stack, TextField, Typography } from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import {
  EMPTY_STORAGE_LOCATION,
  StorageLocationPicker,
  StorageLocationValue,
  isStorageLocationValid,
  storageLocationPayload
} from "../../../../components/StorageLocationPicker";
import { CryovialItem } from "../types/cryovialTypes";
import { CryovialService } from "../services/CryovialService";

export function MoveCryovialStorageDialog({ cryovial, onClose, onMoved }: {
  cryovial: CryovialItem | null;
  onClose: () => void;
  onMoved: (message: string) => void;
}) {
  const { t } = useTranslation("labConfig");
  const [location, setLocation] = useState<StorageLocationValue>(EMPTY_STORAGE_LOCATION);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    setLocation(EMPTY_STORAGE_LOCATION);
    setReason("");
    setError(null);
  }, [cryovial]);

  if (!cryovial) return null;
  const valid = isStorageLocationValid(location) && reason.trim().length >= 5;

  const save = async () => {
    setSubmitting(true);
    setError(null);
    try {
      await CryovialService.moveStorage(cryovial.id, { ...storageLocationPayload(location), reason: reason.trim() });
      onMoved(t("cryovials.move.moved", { code: cryovial.code }));
    } catch (e: any) {
      setError(e?.response?.data?.message ?? t("cryovials.move.error"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FloatingDialog
      open
      maxWidth="sm"
      onClose={() => { if (!submitting) onClose(); }}
      title={t("cryovials.move.title")}
      actions={
        <>
          <Button onClick={onClose} disabled={submitting}>{t("cryovials.move.cancel")}</Button>
          <Button variant="contained" onClick={save} disabled={!valid || submitting}>
            {submitting ? t("cryovials.move.saving") : t("cryovials.move.move")}
          </Button>
        </>
      }
    >
      <Stack spacing={2}>
        {error && <Alert severity="error">{error}</Alert>}
        <Typography sx={{ fontSize: 13 }}>
          <Trans
            i18nKey="cryovials.move.batchLine"
            ns="labConfig"
            values={{ code: cryovial.code, current: cryovial.storageCondition || t("cryovials.move.notRecorded") }}
            components={{ bold: <b /> }}
          />
        </Typography>
        <StorageLocationPicker value={location} onChange={setLocation} disabled={submitting} />
        <TextField
          size="small"
          fullWidth
          multiline
          rows={2}
          label={t("cryovials.move.reasonLabel")}
          value={reason}
          onChange={(e) => setReason(e.target.value.slice(0, 500))}
          error={reason.length > 0 && reason.trim().length < 5}
          helperText={reason.length > 0 && reason.trim().length < 5 ? t("cryovials.move.reasonShort") : t("cryovials.move.reasonHelper")}
        />
      </Stack>
    </FloatingDialog>
  );
}
