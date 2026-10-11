import { useEffect, useState } from "react";
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
      onMoved(`Storage location of cryovial batch ${cryovial.code} changed.`);
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Failed to change storage location.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FloatingDialog
      open
      maxWidth="sm"
      onClose={() => { if (!submitting) onClose(); }}
      title="Change Storage Location"
      actions={
        <>
          <Button onClick={onClose} disabled={submitting}>Cancel</Button>
          <Button variant="contained" onClick={save} disabled={!valid || submitting}>
            {submitting ? "Saving..." : "Move"}
          </Button>
        </>
      }
    >
      <Stack spacing={2}>
        {error && <Alert severity="error">{error}</Alert>}
        <Typography sx={{ fontSize: 13 }}>
          Batch <b>{cryovial.code}</b> - currently: {cryovial.storageCondition || "not recorded"}
        </Typography>
        <StorageLocationPicker value={location} onChange={setLocation} disabled={submitting} />
        <TextField
          size="small"
          fullWidth
          multiline
          rows={2}
          label="Reason for move *"
          value={reason}
          onChange={(e) => setReason(e.target.value.slice(0, 500))}
          error={reason.length > 0 && reason.trim().length < 5}
          helperText={reason.length > 0 && reason.trim().length < 5 ? "Reason must be at least 5 characters." : "Recorded in the audit trail."}
        />
      </Stack>
    </FloatingDialog>
  );
}
