import { useEffect, useMemo, useState } from "react";
import type { Ref } from "react";
import { Alert, Autocomplete, Box, TextField, Typography } from "@mui/material";
import { useTranslation } from "../i18n";
import { monospaceFontFamily } from "../theme/palette";
import { CHANGE_CONTROL_NUMBER_PATTERN } from "../services/changeControlGate";
import { changeControlLookupService, type ApprovedChangeControl } from "../services/changeControlLookupService";

interface ChangeControlNumberFieldProps {
  // The laboratory whose approved change controls are listed; null lists every laboratory.
  sectionId: number | null;
  // What is typed or chosen; the number is this, trimmed and upper-cased.
  value: string;
  onChange: (value: string) => void;
  // false when the person cannot view quality records: no list is requested, the number is just typed.
  listEnabled?: boolean;
  autoFocus?: boolean;
  inputRef?: Ref<HTMLInputElement>;
  // Re-runs the list request, e.g. each time a dialog opens.
  reloadKey?: unknown;
}

type LoadState = "idle" | "loading" | "ready" | "failed";

function matches(option: ApprovedChangeControl, search: string): boolean {
  const needle = search.trim().toLowerCase();
  if (!needle) return true;
  return option.number.toLowerCase().includes(needle) || option.title.toLowerCase().includes(needle);
}

// The normalised header value for what was typed, and whether the server header would accept it.
export function normaliseChangeControlNumber(input: string): { number: string; valid: boolean } {
  const number = input.trim().toUpperCase();
  return { number, valid: CHANGE_CONTROL_NUMBER_PATTERN.test(number) };
}

// Search-or-type field for an approved change control number (CONF-227), shared by the save prompt
// and the workbook import screen. The list is a help; the server decides.
export function ChangeControlNumberField({
  sectionId,
  value,
  onChange,
  listEnabled = true,
  autoFocus = false,
  inputRef,
  reloadKey
}: ChangeControlNumberFieldProps) {
  const { t } = useTranslation("changeControl");
  const [options, setOptions] = useState<ApprovedChangeControl[]>([]);
  const [load, setLoad] = useState<LoadState>(listEnabled ? "loading" : "idle");

  useEffect(() => {
    if (!listEnabled) {
      setLoad("idle");
      return;
    }
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
  }, [listEnabled, sectionId, reloadKey]);

  const { number, valid } = normaliseChangeControlNumber(value);
  const showInvalid = value.trim() !== "" && !valid;
  const selected = useMemo(() => options.find((o) => o.number === number) ?? null, [options, number]);

  return (
    <>
      <Autocomplete
        freeSolo
        fullWidth
        options={options}
        loading={load === "loading"}
        inputValue={value}
        onInputChange={(_event, next) => onChange(next)}
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
            inputRef={inputRef}
            autoFocus={autoFocus}
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
    </>
  );
}
