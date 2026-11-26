import { useEffect, useId, useMemo, useRef, useState } from "react";
import {
  Alert,
  AlertTitle,
  Autocomplete,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  TextField,
  Typography
} from "@mui/material";
import { useTranslation } from "../i18n";
import { monospaceFontFamily } from "../theme/palette";
import { CHANGE_CONTROL_NUMBER_PATTERN } from "../services/changeControlGate";
import { changeControlLookupService, type ApprovedChangeControl } from "../services/changeControlLookupService";

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

type LoadState = "loading" | "ready" | "failed";

function matches(option: ApprovedChangeControl, search: string): boolean {
  const needle = search.trim().toLowerCase();
  if (!needle) return true;
  return option.number.toLowerCase().includes(needle) || option.title.toLowerCase().includes(needle);
}

// Asks which approved change control covers a master-data change (CONF-227). The list is a help:
// a person who may not view quality records still types the number, and the server decides.
export function ChangeControlPrompt({ open, sectionId, refusal, initialNumber, onSubmit, onCancel }: ChangeControlPromptProps) {
  const { t } = useTranslation("changeControl");
  const titleId = useId();
  const [options, setOptions] = useState<ApprovedChangeControl[]>([]);
  const [load, setLoad] = useState<LoadState>("loading");
  const [input, setInput] = useState(initialNumber);
  const fieldRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!open) return;
    setInput(initialNumber);
    setLoad("loading");
    const controller = new AbortController();
    changeControlLookupService
      .listApproved(sectionId, controller.signal)
      .then((items) => {
        if (controller.signal.aborted) return;
        setOptions(items);
        setLoad("ready");
      })
      .catch(() => {
        if (controller.signal.aborted) return;
        setOptions([]);
        setLoad("failed");
      });
    return () => controller.abort();
  }, [open, sectionId, initialNumber]);

  const number = input.trim().toUpperCase();
  const valid = CHANGE_CONTROL_NUMBER_PATTERN.test(number);
  const showInvalid = input.trim() !== "" && !valid;
  const submitLabel = refusal ? t("prompt.retryWith") : t("prompt.saveWith");
  const selected = useMemo(() => options.find((o) => o.number === number) ?? null, [options, number]);

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

        <Autocomplete
          freeSolo
          fullWidth
          options={options}
          loading={load === "loading"}
          inputValue={input}
          onInputChange={(_event, value) => setInput(value)}
          getOptionLabel={(option) => (typeof option === "string" ? option : option.number)}
          filterOptions={(all, state) => all.filter((o) => matches(o, state.inputValue))}
          noOptionsText={t("prompt.noMatch")}
          renderOption={(props, option) => {
            const { key, ...rest } = props;
            return (
              <li key={key} {...rest}>
                <Box>
                  <Typography component="span" sx={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>
                    {option.number}
                  </Typography>
                  <Typography component="span" sx={{ ml: 1 }}>
                    {option.title}
                  </Typography>
                </Box>
              </li>
            );
          }}
          renderInput={(params) => (
            <TextField
              {...params}
              label={t("prompt.field")}
              inputRef={fieldRef}
              autoFocus
              error={showInvalid}
              helperText={showInvalid ? t("prompt.invalidNumber") : t("prompt.fieldHelp")}
            />
          )}
        />

        <Box aria-live="polite" sx={{ mt: 1.5 }}>
          {load === "loading" && <Typography role="status" variant="body2">{t("prompt.loading")}</Typography>}
          {load === "failed" && (
            <Alert severity="info" role="status">
              {t("prompt.loadFailed")}
            </Alert>
          )}
          {load === "ready" && options.length === 0 && (
            <Alert severity="info" role="status">
              {t("prompt.empty")}
            </Alert>
          )}
          {selected && (
            <Typography variant="body2" sx={{ mt: 1 }}>
              {selected.title}
            </Typography>
          )}
        </Box>
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
