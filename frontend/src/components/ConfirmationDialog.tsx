import { useId } from "react";
import type { ReactNode } from "react";
import { Box, Dialog, DialogTitle, DialogContent, DialogActions, Button, CircularProgress, Typography } from "@mui/material";
import WarningAmberRoundedIcon from "@mui/icons-material/WarningAmberRounded";

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
}

// The one confirmation dialog for the app. Destructive confirmations put the
// initial focus on Cancel, so an accidental Enter never deletes or rejects.
export function ConfirmationDialog({
  open,
  message,
  title = "Confirm",
  confirmText = "Confirm",
  cancelText = "Cancel",
  destructive = false,
  loading = false,
  onConfirm,
  onCancel
}: ConfirmationDialogProps) {
  const messageId = useId();
  return (
    <Dialog
      open={open}
      onClose={loading ? undefined : onCancel}
      maxWidth="xs"
      fullWidth
      aria-describedby={messageId}
      role={destructive ? "alertdialog" : "dialog"}
    >
      <DialogTitle sx={{ display: "flex", alignItems: "center", gap: 1 }}>
        {destructive && <WarningAmberRoundedIcon color="error" aria-hidden />}
        {title}
      </DialogTitle>
      <DialogContent>
        {typeof message === "string" ? (
          <Typography id={messageId} variant="body2">{message}</Typography>
        ) : (
          <Box id={messageId} sx={{ typography: "body2" }}>{message}</Box>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={loading} autoFocus={destructive}>{cancelText}</Button>
        <Button
          onClick={onConfirm}
          variant="contained"
          color={destructive ? "error" : "primary"}
          disabled={loading}
          autoFocus={!destructive}
          startIcon={loading ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {confirmText}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
