import { useEffect, useRef } from "react";
import type { Ref } from "react";
import { InputAdornment, TextField } from "@mui/material";
import { clearActiveField, NUMERIC_FIELD_ATTRIBUTE, setActiveField } from "./focusRegistry";
import type { ActiveNumericField } from "./focusRegistry";

export interface NumericEntryFieldProps {
  label: string;
  value: string;
  onChange(value: string): void;
  kind: "count" | "decimal"; // count = whole, non-negative
  maxLength?: number; // default 7
  error?: string;
  disabled?: boolean;
  required?: boolean;
  unit?: string;
  onNext?(): void;
  inputRef?: Ref<HTMLInputElement>;
  "aria-label"?: string;
}

// Format checks only. Laboratory limits and ranges are enforced by the backend.
export function parseCount(text: string): number | null {
  return /^\d+$/.test(text) ? Number(text) : null;
}

export function parseDecimal(text: string): number | null {
  return /^\d+(\.\d+)?$/.test(text) ? Number(text) : null;
}

const FORMAT_MESSAGE: Record<"count" | "decimal", string> = {
  count: "Enter a whole number, 0 or more, using digits only.",
  decimal: "Enter a number using digits and at most one decimal point."
};

export function NumericEntryField({
  label,
  value,
  onChange,
  kind,
  maxLength = 7,
  error,
  disabled,
  required,
  unit,
  onNext,
  inputRef,
  "aria-label": ariaLabel
}: NumericEntryFieldProps) {
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
  const formatError = value !== "" && parsed === null ? FORMAT_MESSAGE[kind] : undefined;
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
      helperText={message}
      fullWidth
      slotProps={{
        htmlInput: {
          inputMode: kind === "count" ? "numeric" : "decimal",
          maxLength,
          autoComplete: "off",
          "aria-label": ariaLabel,
          [NUMERIC_FIELD_ATTRIBUTE]: "true",
          ref: setRefs,
          onFocus: (e: React.FocusEvent<HTMLInputElement>) => {
            e.target.select();
            register(e.target);
          },
          onBlur: (e: React.FocusEvent<HTMLInputElement>) => clearActiveField(e.target),
          onKeyDown: (e: React.KeyboardEvent<HTMLInputElement>) => {
            if (e.key === "Enter" && onNext) {
              e.preventDefault();
              onNext();
            }
          }
        },
        formHelperText: { role: "alert" },
        input: unit ? { endAdornment: <InputAdornment position="end">{unit}</InputAdornment> } : undefined
      }}
    />
  );
}
