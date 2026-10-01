import { useId } from "react";
import { Alert, Box, Button, CircularProgress, DialogProps } from "@mui/material";
import type { FormEvent, ReactNode } from "react";
import { FloatingDialog } from "../FloatingDialog";

interface FormDialogProps {
  open: boolean;
  title: ReactNode;
  onClose: () => void;
  onSubmit: () => void | Promise<void>;
  submitLabel: string;
  submitting?: boolean;
  // Non-field errors only (server failures); field errors go on the inputs as helperText.
  error?: string | null;
  children: ReactNode;
  maxWidth?: DialogProps["maxWidth"];
  // Lets a caller block submit on its own validity without spinning.
  submitDisabled?: boolean;
}

// The <form> wraps the body so Enter submits from any field. The footer sits
// outside it (FloatingDialog's actions slot), so its submit button is tied to
// the form with the `form` attribute.
export function FormDialog({ open, title, onClose, onSubmit, submitLabel, submitting = false, error, children, maxWidth = "sm", submitDisabled }: FormDialogProps) {
  const formId = useId();
  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (!submitting) void onSubmit();
  };

  return (
    <FloatingDialog
      open={open}
      title={title}
      onClose={onClose}
      maxWidth={maxWidth}
      actions={
        <>
          <Button onClick={onClose} disabled={submitting} sx={{ textTransform: "none" }}>Cancel</Button>
          <Button
            type="submit"
            form={formId}
            variant="contained"
            disabled={submitting || submitDisabled}
            startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
            sx={{ textTransform: "none" }}
          >
            {submitLabel}
          </Button>
        </>
      }
    >
      <Box component="form" id={formId} onSubmit={handleSubmit} noValidate sx={{ display: "flex", flexDirection: "column", gap: 2, pt: 1 }}>
        {error && <Alert severity="error">{error}</Alert>}
        {children}
      </Box>
    </FloatingDialog>
  );
}
