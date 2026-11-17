import { useEffect, useId, useRef } from "react";
import { Alert, Box, Button, CircularProgress, DialogProps } from "@mui/material";
import type { FormEvent, KeyboardEvent, ReactNode } from "react";
import { FloatingDialog } from "../FloatingDialog";
import { useTranslation } from "../../i18n";

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
  initialFocus?: "firstEmptyRequired" | "none";
}

function findFirstEmptyRequired(form: HTMLElement): HTMLElement | null {
  const candidates = form.querySelectorAll<HTMLElement>(
    "input[required], textarea[required], select[required], [aria-required='true']"
  );
  for (const el of candidates) {
    if (el.hasAttribute("disabled") || el.getAttribute("aria-disabled") === "true") continue;

    if (el instanceof HTMLInputElement) {
      if (el.type === "hidden") continue;
      if (el.classList.contains("MuiSelect-nativeInput")) {
        if (el.value.trim() !== "") continue;
        const combobox = el.parentElement?.querySelector<HTMLElement>("[role='combobox']");
        if (combobox) return combobox;
        continue;
      }
      if (el.type === "checkbox" || el.type === "radio") {
        if (!el.checked) return el;
        continue;
      }
      if (el.value.trim() === "") return el;
    } else if (el instanceof HTMLTextAreaElement) {
      if (el.value.trim() === "") return el;
    } else if (el instanceof HTMLSelectElement) {
      if (el.value === "") return el;
    } else if (el.getAttribute("role") === "combobox") {
      if ((el.textContent?.trim() ?? "") === "") return el;
    }
  }
  return null;
}

// The <form> wraps the body so Enter submits from any field. The footer sits
// outside it (FloatingDialog's actions slot), so its submit button is tied to
// the form with the `form` attribute.
export function FormDialog({
  open,
  title,
  onClose,
  onSubmit,
  submitLabel,
  submitting = false,
  error,
  children,
  maxWidth = "sm",
  submitDisabled,
  initialFocus = "firstEmptyRequired"
}: FormDialogProps) {
  const { t } = useTranslation();
  const formId = useId();
  const formRef = useRef<HTMLFormElement | null>(null);
  const wasOpenRef = useRef(false);

  useEffect(() => {
    if (!open) {
      wasOpenRef.current = false;
      return;
    }
    if (wasOpenRef.current) return;
    wasOpenRef.current = true;
    if (initialFocus === "none") return;

    const frame = requestAnimationFrame(() => {
      const form = formRef.current;
      if (!form) return;

      // Do not steal focus if a child inside the form already has autoFocus.
      const active = document.activeElement;
      if (active && active !== form && form.contains(active)) return;

      const target = findFirstEmptyRequired(form);
      target?.focus();
    });
    return () => cancelAnimationFrame(frame);
  }, [open, initialFocus]);
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
          <Button onClick={onClose} disabled={submitting}>{t("actions.cancel")}</Button>
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
      <Box component="form" ref={formRef} id={formId} onSubmit={handleSubmit} onKeyDown={handleKeyDown} noValidate sx={{ display: "flex", flexDirection: "column", gap: 2, pt: 1 }}>
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
