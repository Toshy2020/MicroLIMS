import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Alert, Box, Button, CircularProgress, TextField, Typography } from "@mui/material";
import { FloatingDialog } from "../FloatingDialog";

export interface ChangeReasonDialogProps {
  open: boolean;
  title?: string;
  message?: ReactNode;
  confirmText?: string;
  loading?: boolean;
  error?: string | null;
  minLength?: number;
  maxLength?: number;
  onConfirm: (reason: string) => void | Promise<void>;
  onCancel: () => void;
}

const DEFAULT_MIN_LENGTH = 10;
const DEFAULT_MAX_LENGTH = 500;

/**
 * Prompts for a regulated audit change reason with a minimum trimmed length
 * and visible character counter.
 */
export function ChangeReasonDialog({
  open,
  title = "Change reason",
  message = "This change affects active laboratory work and requires a reason for the audit trail.",
  confirmText = "Confirm",
  loading = false,
  error = null,
  minLength = DEFAULT_MIN_LENGTH,
  maxLength = DEFAULT_MAX_LENGTH,
  onConfirm,
  onCancel,
}: ChangeReasonDialogProps) {
  const [reason, setReason] = useState("");
  const [attempted, setAttempted] = useState(false);

  useEffect(() => {
    if (open) {
      setReason("");
      setAttempted(false);
    }
  }, [open]);

  const trimmed = reason.trim();
  const isTooShort = trimmed.length < minLength;
  const isTooLong = trimmed.length > maxLength;
  const canConfirm = !isTooShort && !isTooLong;
  const showShortError = (attempted || reason.length > 0) && isTooShort;

  const counterText = isTooShort
    ? `${trimmed.length} / ${minLength} characters minimum`
    : `${trimmed.length} / ${maxLength} characters`;

  const handleConfirm = () => {
    setAttempted(true);
    if (!canConfirm || loading) return;
    void onConfirm(trimmed);
  };

  return (
    <FloatingDialog
      open={open}
      title={
        <Typography variant="h6" component="span">
          {title}
        </Typography>
      }
      onClose={() => {
        if (!loading) onCancel();
      }}
      maxWidth="sm"
      actions={
        <>
          <Button type="button" onClick={onCancel} disabled={loading}>
            Cancel
          </Button>
          <Button
            type="button"
            variant="contained"
            onClick={handleConfirm}
            disabled={!canConfirm || loading}
            startIcon={loading ? <CircularProgress size={16} color="inherit" /> : undefined}
          >
            {confirmText}
          </Button>
        </>
      }
    >
      <Box sx={{ display: "flex", flexDirection: "column", gap: 2, pt: 0.5 }}>
        {error && <Alert severity="error">{error}</Alert>}
        {typeof message === "string" ? (
          <Typography variant="body2" color="text.secondary">
            {message}
          </Typography>
        ) : (
          message
        )}
        <TextField
          label="Reason for change"
          name="changeReason"
          required
          multiline
          minRows={3}
          fullWidth
          autoFocus
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          disabled={loading}
          error={showShortError || isTooLong}
          slotProps={{
            htmlInput: { maxLength },
            formHelperText: { sx: { display: "flex", justifyContent: "space-between", gap: 1 } },
          }}
          helperText={
            <>
              <span>
                {showShortError
                  ? `Enter at least ${minLength} characters.`
                  : "Recorded in the audit trail."}
              </span>
              <span style={{ fontVariantNumeric: "tabular-nums" }}>{counterText}</span>
            </>
          }
        />
      </Box>
    </FloatingDialog>
  );
}
