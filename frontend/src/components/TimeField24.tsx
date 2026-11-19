import { useState } from "react";
import { TextField } from "@mui/material";
import type { SxProps, Theme } from "@mui/material";

// "HH:mm" on the 24-hour clock: the same wire format a native <input type="time"> produced,
// without the browser's locale-dependent 12-hour AM/PM rendering.
// Format notation, not prose: identical in every language.
const TIME_FORMAT_HINT = "HH:mm";
const TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;

export function isValidTime24(value: string): boolean {
  return TIME_PATTERN.test(value);
}

interface TimeField24Props {
  /** "HH:mm", or "" for no time. */
  value: string;
  /** Called with "HH:mm" as soon as a complete, valid time is typed. */
  onChange: (value: string) => void;
  ariaLabel: string;
  sx?: SxProps<Theme>;
}

function autoColon(raw: string): string {
  const digits = raw.replace(/[^\d]/g, "").slice(0, 4);
  return digits.length > 2 ? `${digits.slice(0, 2)}:${digits.slice(2)}` : digits;
}

export function TimeField24({ value, onChange, ariaLabel, sx }: TimeField24Props) {
  // The draft is what is typed; it follows the value again whenever the field is not being edited.
  const [draft, setDraft] = useState<string | null>(null);
  const shown = draft ?? value;
  const invalid = draft !== null && draft !== "" && !isValidTime24(draft);
  return (
    <TextField
      size="small"
      value={shown}
      placeholder={TIME_FORMAT_HINT}
      error={invalid}
      onChange={(e) => {
        const next = autoColon(e.target.value);
        setDraft(next);
        if (isValidTime24(next)) onChange(next);
      }}
      onBlur={() => setDraft(null)}
      slotProps={{
        htmlInput: {
          "aria-label": ariaLabel,
          "aria-invalid": invalid || undefined,
          dir: "ltr",
          inputMode: "numeric",
          maxLength: 5,
          pattern: "([01][0-9]|2[0-3]):[0-5][0-9]"
        }
      }}
      sx={sx}
    />
  );
}
