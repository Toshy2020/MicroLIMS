import { useEffect, useRef } from "react";
import type { Ref } from "react";
import { InputAdornment, TextField } from "@mui/material";
import { useTranslation } from "../../i18n";
import { clearActiveField, NUMERIC_FIELD_ATTRIBUTE, setActiveField } from "./focusRegistry";
import type { ActiveNumericField } from "./focusRegistry";

export interface NumericEntryFieldProps {
  label: string;
  value: string;
  onChange(value: string): void;
  kind: "count" | "decimal"; // count = whole, non-negative; decimal may start with a minus sign
  maxLength?: number; // default 7
  error?: string;
  helperText?: string;
  disabled?: boolean;
  readOnly?: boolean;
  required?: boolean;
  unit?: string;
  onNext?(): void;
  onBlur?(): void;
  inputRef?: Ref<HTMLInputElement>;
  "aria-label"?: string;
  "aria-describedby"?: string;
}

// Format checks only. Laboratory limits and ranges are enforced by the backend.
export function parseCount(text: string): number | null {
  return /^\d+$/.test(text) ? Number(text) : null;
}

export function parseDecimal(text: string): number | null {
  return /^-?\d+(\.\d+)?$/.test(text) ? Number(text) : null;
}

const FORMAT_MESSAGE_KEY: Record<"count" | "decimal", string> = {
  count: "components.numericEntryField.countFormat",
  decimal: "components.numericEntryField.decimalFormat"
};

export function NumericEntryField({
  label,
  value,
  onChange,
  kind,
  maxLength = 7,
  error,
  helperText,
  disabled,
  readOnly,
  required,
  unit,
  onNext,
  onBlur,
  inputRef,
  "aria-label": ariaLabel,
  "aria-describedby": ariaDescribedBy
}: NumericEntryFieldProps) {
  const { t } = useTranslation();
  const elementRef = useRef<HTMLInputElement | null>(null);
  const latest = useRef({ value, onChange, onNext, kind, maxLength });
  useEffect(() => {
    latest.current = { value, onChange, onNext, kind, maxLength };
  });

  // Never leave a stale entry in the registry when this field goes away.
  useEffect(() => {
    const element = elementRef.current;
    return () => {
      if (element) clearActiveField(element);
    };
  }, []);

  const parsed = kind === "count" ? parseCount(value) : parseDecimal(value);
  const formatError = !readOnly && value !== "" && parsed === null ? t(FORMAT_MESSAGE_KEY[kind]) : undefined;
  const message = error ?? formatError;

  const setRefs = (node: HTMLInputElement | null) => {
    elementRef.current = node;
    if (typeof inputRef === "function") inputRef(node);
    else if (inputRef) inputRef.current = node;
  };

  const register = (element: HTMLInputElement) => {
    const entry: ActiveNumericField = {
      element,
      kind,
      maxLength,
      getValue: () => latest.current.value,
      onChange: (next) => latest.current.onChange(next),
      onNext: () => latest.current.onNext?.()
    };
    setActiveField(entry);
  };

  return (
    <TextField
      label={label}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      disabled={disabled}
      required={required}
      error={Boolean(message)}
      helperText={message ?? helperText}
      fullWidth
      slotProps={{
        htmlInput: {
          inputMode: kind === "count" ? "numeric" : "decimal",
          maxLength,
          readOnly,
          autoComplete: "off",
          "aria-label": ariaLabel,
          "aria-describedby": ariaDescribedBy,
          [NUMERIC_FIELD_ATTRIBUTE]: "true",
          ref: setRefs,
          onFocus: (e: React.FocusEvent<HTMLInputElement>) => {
            if (readOnly) return;
            e.target.select();
            register(e.target);
          },
          onBlur: (e: React.FocusEvent<HTMLInputElement>) => {
            clearActiveField(e.target);
            onBlur?.();
          },
          onKeyDown: (e: React.KeyboardEvent<HTMLInputElement>) => {
            if (e.key === "Enter" && onNext) {
              e.preventDefault();
              onNext();
            }
          }
        },
        formHelperText: message ? { role: "alert" } : undefined,
        input: unit ? { endAdornment: <InputAdornment position="end">{unit}</InputAdornment> } : undefined
      }}
    />
  );
}
