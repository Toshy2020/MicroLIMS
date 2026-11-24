import { useState, type FocusEvent } from "react";
import { SxProps, Theme } from "@mui/material";
import { DatePicker } from "@mui/x-date-pickers/DatePicker";
import { LocalizationProvider } from "@mui/x-date-pickers/LocalizationProvider";
import { AdapterDayjs } from "@mui/x-date-pickers/AdapterDayjs";
import dayjs, { Dayjs } from "dayjs";
import { useTranslation } from "../i18n";

// The wire format every caller already speaks - the same string a native
// <input type="date"> produced, so swapping one for this is mechanical.
const VALUE_FORMAT = "YYYY-MM-DD";
// Matches how dates are displayed elsewhere in the app (DD/MM...).
const DISPLAY_FORMAT = "DD/MM/YYYY";

interface DateFieldProps {
  label?: string;
  /** "YYYY-MM-DD", or "" for no date. */
  value: string;
  /** Called with "YYYY-MM-DD", or "" when cleared or while a date is only
   *  partly typed - the same contract as the native input, so callers that
   *  must not send a half-typed date to the server keep working. */
  onChange: (value: string) => void;
  /** Called with true when focus leaves a partly typed date (so the form can
   *  refuse to save it) and false once the date is complete or cleared. */
  onIncompleteChange?: (incomplete: boolean) => void;
  /** "YYYY-MM-DD" bounds; an empty string means no bound. */
  min?: string;
  max?: string;
  error?: boolean;
  helperText?: string;
  fullWidth?: boolean;
  required?: boolean;
  disabled?: boolean;
  /** Defaults to "small" - the size of the filter bars and most forms. */
  size?: "small" | "medium";
  /** Accessible name for a field with no visible label (a table cell). */
  ariaLabel?: string;
  ariaDescribedBy?: string;
  sx?: SxProps<Theme>;
}

function parse(value: string | undefined): Dayjs | null {
  if (!value) return null;
  const d = dayjs(value);
  return d.isValid() ? d : null;
}

// Calendar date picker: a DD/MM/YYYY field with a calendar button, at the
// same height as the TextFields and Selects beside it, replacing the
// browser's own "dd----yyyy" placeholder rendering.
export function DateField({
  label, value, onChange, onIncompleteChange, min, max, error, helperText, fullWidth, required, disabled, size = "small", ariaLabel, ariaDescribedBy, sx
}: DateFieldProps) {
  const { t, i18n } = useTranslation("common");
  const isRtl = i18n.language === "ar";
  // The picker never reports a half-typed date (day, month or year missing):
  // onChange simply isn't called, so the caller keeps its old value and the
  // typed digits vanish on save. Detect it when focus leaves the field and
  // say so, instead of dropping the entry silently.
  const [partial, setPartial] = useState(false);
  const checkPartial = (e: FocusEvent<HTMLElement>) => {
    if (e.relatedTarget instanceof Node && e.currentTarget.contains(e.relatedTarget)) return;
    const sections = Array.from(e.currentTarget.querySelectorAll('[role="spinbutton"]'));
    const filled = sections.filter((s) => s.hasAttribute("aria-valuenow")).length;
    const incomplete = filled > 0 && filled < sections.length;
    setPartial(incomplete);
    onIncompleteChange?.(incomplete);
  };
  const inputAriaProps =
    ariaLabel || ariaDescribedBy
      ? {
          input: {
            ...(ariaLabel ? { "aria-label": ariaLabel } : {}),
            ...(ariaDescribedBy ? { "aria-describedby": ariaDescribedBy } : {})
          }
        }
      : undefined;

  return (
    <LocalizationProvider
      dateAdapter={AdapterDayjs}
      localeText={
        isRtl
          ? {
              clearButtonLabel: t("datePicker.clear"),
              openDatePickerDialogue: (formattedDate) =>
                formattedDate
                  ? t("datePicker.chooseDateSelected", { date: formattedDate })
                  : t("datePicker.chooseDate")
            }
          : undefined
      }
    >
      <DatePicker
        label={label}
        value={parse(value)}
        onChange={(d) => {
          setPartial(false);
          onIncompleteChange?.(false);
          onChange(d && d.isValid() ? d.format(VALUE_FORMAT) : "");
        }}
        format={DISPLAY_FORMAT}
        minDate={parse(min) ?? undefined}
        maxDate={parse(max) ?? undefined}
        disabled={disabled}
        sx={sx}
        slotProps={{
          field: { clearable: true },
          textField: {
            size,
            fullWidth,
            required,
            error: error || partial,
            helperText: partial ? t("datePicker.incomplete") : helperText,
            onBlur: checkPartial,
            slotProps: inputAriaProps
          }
        }}
      />
    </LocalizationProvider>
  );
}
