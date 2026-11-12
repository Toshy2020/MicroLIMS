import { useId } from "react";
import { Alert, Box, Button, CircularProgress, DialogProps } from "@mui/material";
import type { FormEvent, KeyboardEvent, ReactNode } from "react";
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

  // Enter in a search-and-pick field (Autocomplete) picks or searches; it must
  // not save the whole form. The field handles the key first, so by the time
  // it bubbles here only the browser's implicit submit is left to cancel.
  const handleKeyDown = (e: KeyboardEvent<HTMLElement>) => {
    if (e.key !== "Enter") return;
    const target = e.target as HTMLElement;
    if (target.tagName === "INPUT" && target.getAttribute("role") === "combobox") e.preventDefault();
  };

  return (
    <FloatingDialog
      open={open}
      title={title}
      onClose={onClose}
      maxWidth={maxWidth}
      actions={
        <>
          <Button onClick={onClose} disabled={submitting}>Cancel</Button>
          <Button
            type="submit"
            form={formId}
            variant="contained"
            disabled={submitting || submitDisabled}
            startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
          >
            {submitLabel}
          </Button>
        </>
      }
    >
      <Box component="form" id={formId} onSubmit={handleSubmit} onKeyDown={handleKeyDown} noValidate sx={{ display: "flex", flexDirection: "column", gap: 2, pt: 1 }}>
        {error && <Alert severity="error">{error}</Alert>}
        {children}
        {/* The form's default button, so Enter submits whatever fields it holds.
            It mirrors the footer button's disabled state: Enter must not save
            when that button could not. */}
        <button type="submit" hidden tabIndex={-1} aria-hidden="true" disabled={submitting || submitDisabled} />
      </Box>
    </FloatingDialog>
  );
}
