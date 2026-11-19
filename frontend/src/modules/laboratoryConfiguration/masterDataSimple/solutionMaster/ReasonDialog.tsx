import React from "react";
import {
  Button,
  TextField,
  Stack,
  Alert
} from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { useTranslation } from "../../../../i18n";

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
  confirmText,
  confirmColor = "primary",
  loading = false,
  loadingText,
  error,
  reason,
  onReasonChange,
  label,
  placeholder,
  disabled,
  maxLength = 500,
  children
}: ReasonDialogProps) {
  const { t } = useTranslation("masterData");
  const isConfirmDisabled = disabled !== undefined ? disabled : (!reason.trim() || loading);
  const resolvedConfirmText = confirmText ?? t("solutionMaster.reasonDialog.defaultConfirm");
  const resolvedLoadingText = loadingText ?? t("solutionMaster.reasonDialog.defaultLoading");
  const resolvedLabel = label ?? t("solutionMaster.reasonDialog.defaultLabel");
  const resolvedPlaceholder = placeholder ?? t("solutionMaster.reasonDialog.defaultPlaceholder");

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
          <Button onClick={onClose} disabled={loading}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="contained"
            color={confirmColor}
            onClick={onConfirm}
            disabled={isConfirmDisabled}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            {loading ? resolvedLoadingText : resolvedConfirmText}
          </Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ pt: 1 }}>
        {error && <Alert severity="error">{error}</Alert>}
        {children}
        <TextField
          label={resolvedLabel}
          placeholder={resolvedPlaceholder}
          multiline
          rows={3}
          fullWidth
          required
          autoFocus
          size="small"
          value={reason}
          onChange={(e) => onReasonChange(e.target.value.slice(0, maxLength))}
          helperText={t("solutionMaster.reasonDialog.charCount", { count: reason.trim().length, max: maxLength })}
        />
      </Stack>
    </FloatingDialog>
  );
}
