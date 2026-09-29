import React from "react";
import {
  Button,
  TextField,
  Stack,
  Alert
} from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";

export interface ReasonDialogProps {
  open: boolean;
  title: string;
  onClose: () => void;
  onConfirm: () => void;
  confirmText?: string;
  confirmColor?: "primary" | "secondary" | "error" | "info" | "success" | "warning";
  loading?: boolean;
  loadingText?: string;
  error?: string | null;
  reason: string;
  onReasonChange: (reason: string) => void;
  label?: string;
  placeholder?: string;
  disabled?: boolean;
  maxLength?: number;
  children?: React.ReactNode;
}

export function ReasonDialog({
  open,
  title,
  onClose,
  onConfirm,
  confirmText = "Confirm",
  confirmColor = "primary",
  loading = false,
  loadingText = "Saving...",
  error,
  reason,
  onReasonChange,
  label = "Reason *",
  placeholder = "Explain why this action is being taken...",
  disabled,
  maxLength = 500,
  children
}: ReasonDialogProps) {
  const isConfirmDisabled = disabled !== undefined ? disabled : (!reason.trim() || loading);

  return (
    <FloatingDialog
      open={open}
      title={title}
      onClose={() => {
        if (!loading) onClose();
      }}
      maxWidth="sm"
      actions={
        <>
          <Button onClick={onClose} disabled={loading} sx={{ textTransform: "none" }}>
            Cancel
          </Button>
          <Button
            variant="contained"
            color={confirmColor}
            onClick={onConfirm}
            disabled={isConfirmDisabled}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            {loading ? loadingText : confirmText}
          </Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ pt: 1 }}>
        {error && <Alert severity="error">{error}</Alert>}
        {children}
        <TextField
          label={label}
          placeholder={placeholder}
          multiline
          rows={3}
          fullWidth
          required
          autoFocus
          size="small"
          value={reason}
          onChange={(e) => onReasonChange(e.target.value.slice(0, maxLength))}
          helperText={`${reason.trim().length} / ${maxLength} characters (required)`}
        />
      </Stack>
    </FloatingDialog>
  );
}
