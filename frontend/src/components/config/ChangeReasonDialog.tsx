import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Alert, Box, Button, CircularProgress, TextField, Typography } from "@mui/material";
import { useTranslation } from "../../i18n";
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
  title: titleProp,
  message: messageProp,
  confirmText: confirmTextProp,
  loading = false,
  error = null,
  minLength = DEFAULT_MIN_LENGTH,
  maxLength = DEFAULT_MAX_LENGTH,
  onConfirm,
  onCancel,
}: ChangeReasonDialogProps) {
  const { t } = useTranslation();
  const title = titleProp ?? t("components.changeReasonDialog.title");
  const message = messageProp ?? t("components.changeReasonDialog.message");
  const confirmText = confirmTextProp ?? t("components.changeReasonDialog.confirm");
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
    ? t("components.changeReasonDialog.counterMin", { length: trimmed.length, min: minLength })
    : t("components.changeReasonDialog.counter", { length: trimmed.length, max: maxLength });

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
            {t("components.changeReasonDialog.cancel")}
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
          label={t("components.changeReasonDialog.reasonLabel")}
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
                  ? t("components.changeReasonDialog.enterAtLeast", { min: minLength })
                  : t("components.changeReasonDialog.recorded")}
              </span>
              <span style={{ fontVariantNumeric: "tabular-nums" }}>{counterText}</span>
            </>
          }
        />
      </Box>
    </FloatingDialog>
  );
}
