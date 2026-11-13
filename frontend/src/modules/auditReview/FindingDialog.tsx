import { useState } from "react";
import { Box, TextField, Typography } from "@mui/material";
import { FormDialog } from "../../components/lab";
import type { AuditLogItem } from "../auditSearch/types/auditTypes";
import { monospaceFontFamily } from "../../theme/palette";
import { getErrorMessage, serverMessage } from "../../utils/errorMessage";
import { formatDateTimeWithZone } from "./formatAuditDate";

interface FindingDialogProps {
  open: boolean;
  entry: AuditLogItem | null;
  onClose: () => void;
  onSubmit: (note: string) => Promise<void>;
}

const MAX_NOTE_LENGTH = 2000;

export function FindingDialog({ open, entry, onClose, onSubmit }: FindingDialogProps) {
  const [note, setNote] = useState("");
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const reset = () => {
    setNote("");
    setFieldError(null);
    setSubmitError(null);
  };

  const handleClose = () => {
    if (submitting) return;
    reset();
    onClose();
  };

  const handleSubmit = async () => {
    setSubmitError(null);
    const trimmed = note.trim();
    if (!trimmed) {
      setFieldError("Finding note is required.");
      return;
    }
    if (trimmed.length > MAX_NOTE_LENGTH) {
      setFieldError(`Finding note must be ${MAX_NOTE_LENGTH} characters or fewer.`);
      return;
    }
    setFieldError(null);

    setSubmitting(true);
    try {
      await onSubmit(trimmed);
      reset();
    } catch (err: unknown) {
      setSubmitError(serverMessage(err) ?? getErrorMessage(err, "Could not record the finding."));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      title="Mark entry as finding"
      onClose={handleClose}
      onSubmit={handleSubmit}
      submitLabel="Record finding"
      submitting={submitting}
      error={submitError}
    >
      {entry && (
        <Box sx={{ p: 1.5, borderRadius: 1, bgcolor: "action.hover" }}>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            Audit Entry{" "}
            <Box component="span" sx={{ fontFamily: monospaceFontFamily, fontVariantNumeric: "tabular-nums" }}>
              #{entry.id}
            </Box>{" "}
            · {entry.entityName}{" "}
            <Box component="span" sx={{ fontFamily: monospaceFontFamily }}>
              {entry.entityId}
            </Box>
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ fontVariantNumeric: "tabular-nums" }}>
            {entry.action} by {entry.userName} on {formatDateTimeWithZone(entry.timestamp)}
          </Typography>
        </Box>
      )}

      <TextField
        label="Finding note"
        size="small"
        required
        multiline
        minRows={3}
        fullWidth
        value={note}
        onChange={(e) => {
          setNote(e.target.value);
          if (fieldError) setFieldError(null);
        }}
        error={Boolean(fieldError)}
        helperText={fieldError ?? `${note.trim().length}/${MAX_NOTE_LENGTH}`}
        slotProps={{ htmlInput: { maxLength: MAX_NOTE_LENGTH } }}
      />
    </FormDialog>
  );
}
