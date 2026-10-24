import { SxProps, Theme } from "@mui/material";
import { DatePicker } from "@mui/x-date-pickers/DatePicker";
import { LocalizationProvider } from "@mui/x-date-pickers/LocalizationProvider";
import { AdapterDayjs } from "@mui/x-date-pickers/AdapterDayjs";
import dayjs, { Dayjs } from "dayjs";

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
  /** "YYYY-MM-DD" bounds; an empty string means no bound. */
  min?: string;
  max?: string;
  error?: boolean;
  helperText?: string;
  fullWidth?: boolean;
  sx?: SxProps<Theme>;
}

function parse(value: string | undefined): Dayjs | null {
  if (!value) return null;
  const d = dayjs(value);
  return d.isValid() ? d : null;
}

// Calendar date picker for filter bars: a DD/MM/YYYY field with a calendar
// button, at the same small height as the TextFields and Selects beside it,
// replacing the browser's own "dd----yyyy" placeholder rendering.
export function DateField({ label, value, onChange, min, max, error, helperText, fullWidth, sx }: DateFieldProps) {
  return (
    <LocalizationProvider dateAdapter={AdapterDayjs}>
      <DatePicker
        label={label}
        value={parse(value)}
        onChange={(d) => onChange(d && d.isValid() ? d.format(VALUE_FORMAT) : "")}
        format={DISPLAY_FORMAT}
        minDate={parse(min) ?? undefined}
        maxDate={parse(max) ?? undefined}
        sx={sx}
        slotProps={{
          field: { clearable: true },
          textField: {
            size: "small",
            fullWidth,
            error,
            helperText
          }
        }}
      />
    </LocalizationProvider>
  );
}
