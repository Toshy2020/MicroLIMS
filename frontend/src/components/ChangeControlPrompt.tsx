import { useEffect, useId, useRef, useState } from "react";
import {
  Alert,
  AlertTitle,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle
} from "@mui/material";
import { useTranslation } from "../i18n";
import { ChangeControlNumberField, normaliseChangeControlNumber } from "./ChangeControlNumberField";

interface ChangeControlPromptProps {
  open: boolean;
  // The laboratory (section) whose approved change controls are listed.
  sectionId: number | null;
  // The server's refusal sentence (already in the interface language), or null when the prompt is
  // shown before the first save because the laboratory is known to require a change control.
  refusal: string | null;
  // The number tried last, so a refused number can be corrected rather than retyped.
  initialNumber: string;
  onSubmit: (changeControlNumber: string) => void;
  onCancel: () => void;
}

// Asks which approved change control covers a master-data change (CONF-227). The list is a help:
// a person who may not view quality records still types the number, and the server decides.
export function ChangeControlPrompt({ open, sectionId, refusal, initialNumber, onSubmit, onCancel }: ChangeControlPromptProps) {
  const { t } = useTranslation("changeControl");
  const titleId = useId();
  const [input, setInput] = useState(initialNumber);
  const fieldRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (open) setInput(initialNumber);
  }, [open, initialNumber]);

  const { number, valid } = normaliseChangeControlNumber(input);
  const submitLabel = refusal ? t("prompt.retryWith") : t("prompt.saveWith");

  const submit = () => {
    if (valid) onSubmit(number);
  };

  return (
    <Dialog
      open={open}
      onClose={onCancel}
      maxWidth="sm"
      fullWidth
      aria-labelledby={titleId}
      slotProps={{ transition: { onEntered: () => fieldRef.current?.focus() } }}
    >
      <DialogTitle id={titleId}>{t("prompt.title")}</DialogTitle>
      <DialogContent>
        {refusal ? (
          <Alert severity="warning" sx={{ mb: 2 }}>
            <AlertTitle>{t("prompt.refusalHeading")}</AlertTitle>
            {refusal}
          </Alert>
        ) : (
          <DialogContentText sx={{ mb: 2 }}>{t("prompt.beforeSave")}</DialogContentText>
        )}

        <ChangeControlNumberField sectionId={sectionId} value={input} onChange={setInput} autoFocus inputRef={fieldRef} reloadKey={initialNumber} />
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel}>{t("prompt.cancel")}</Button>
        <Button variant="contained" onClick={submit} disabled={!valid}>
          {submitLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
