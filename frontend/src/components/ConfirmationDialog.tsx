import { useId, useRef } from "react";
import type { ReactNode } from "react";
import { Box, Dialog, DialogTitle, DialogContent, DialogActions, Button, CircularProgress, Typography } from "@mui/material";
import WarningAmberRoundedIcon from "@mui/icons-material/WarningAmberRounded";
import { useTranslation } from "../i18n";

interface ConfirmationDialogProps {
  open: boolean;
  message: ReactNode;
  title?: string;
  confirmText?: string;
  cancelText?: string;
  destructive?: boolean;
  // Disables both buttons and shows progress on the confirm button while the
  // confirmed action runs, so it cannot be submitted twice.
  loading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
  // Runs once the dialog has finished closing, so a caller can move focus after the modal has let go of it.
  onClosed?: () => void;
}

// The one confirmation dialog for the app. Destructive confirmations put the
// initial focus on Cancel, so an accidental Enter never deletes or rejects.
export function ConfirmationDialog({
  open,
  message,
  title,
  confirmText,
  cancelText,
  destructive = false,
  loading = false,
  onConfirm,
  onCancel,
  onClosed
}: ConfirmationDialogProps) {
  const { t } = useTranslation();
  const messageId = useId();
  const cancelRef = useRef<HTMLButtonElement>(null);
  return (
    <Dialog
      open={open}
      onClose={loading ? undefined : onCancel}
      maxWidth="xs"
      fullWidth
      aria-describedby={messageId}
      role={destructive ? "alertdialog" : "dialog"}
      // autoFocus alone can lose to the dialog's own focus handling while it fades in, which left
      // focus on the container in a real browser. Once the dialog is in place, put it on Cancel.
      slotProps={{ transition: { onEntered: () => cancelRef.current?.focus(), onExited: onClosed } }}
    >
      <DialogTitle sx={{ display: "flex", alignItems: "center", gap: 1 }}>
        {destructive && <WarningAmberRoundedIcon color="error" aria-hidden />}
        {title ?? t("dialog.confirmTitle")}
      </DialogTitle>
      <DialogContent>
        {typeof message === "string" ? (
          <Typography id={messageId} variant="body2">{message}</Typography>
        ) : (
          <Box id={messageId} sx={{ typography: "body2" }}>{message}</Box>
        )}
      </DialogContent>
      <DialogActions>
        <Button ref={cancelRef} onClick={onCancel} disabled={loading} autoFocus>{cancelText ?? t("actions.cancel")}</Button>
        <Button
          onClick={onConfirm}
          variant="contained"
          color={destructive ? "error" : "primary"}
          disabled={loading}
          startIcon={loading ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {confirmText ?? t("actions.confirm")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
