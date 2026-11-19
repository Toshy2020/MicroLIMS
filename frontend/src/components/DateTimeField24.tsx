import { Box } from "@mui/material";
import type { SxProps, Theme } from "@mui/material";
import { useTranslation } from "../i18n";
import { DateField } from "./DateField";
import { TimeField24 } from "./TimeField24";

export function getLocalIsoString(date: Date = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export interface DateTimeField24Props {
  /** "YYYY-MM-DDTHH:mm", or "" for no date-time. */
  value: string;
  /** Called with "YYYY-MM-DDTHH:mm", or "" when the date is cleared. */
  onChange: (value: string) => void;
  /** Visible label for the date field (also used to build the default time aria-label). */
  label?: string;
  /** Accessible name for the date field when no visible label is provided. */
  dateAriaLabel?: string;
  /** Accessible name for the 24-hour time field. */
  timeAriaLabel?: string;
  required?: boolean;
  disabled?: boolean;
  error?: boolean;
  helperText?: string;
  sx?: SxProps<Theme>;
}

/**
 * Combined date + 24-hour time input storing "YYYY-MM-DDTHH:mm" (or "")
 * without the browser's locale-dependent AM/PM rendering.
 */
export function DateTimeField24({
  value,
  onChange,
  label,
  dateAriaLabel,
  timeAriaLabel,
  required,
  disabled,
  error,
  helperText,
  sx
}: DateTimeField24Props) {
  const { t } = useTranslation("common");
  const datePart = value.slice(0, 10);
  const timePart = value.slice(11, 16);
  const resolvedDateName = dateAriaLabel ?? label;
  const resolvedTimeAria =
    timeAriaLabel ?? (resolvedDateName ? t("dateTime.timeOf", { label: resolvedDateName }) : t("dateTime.time"));

  return (
    <Box
      sx={[
        { display: "flex", gap: 1, alignItems: "flex-start" },
        ...(Array.isArray(sx) ? sx : sx ? [sx] : [])
      ]}
    >
      <DateField
        label={label}
        ariaLabel={dateAriaLabel}
        value={datePart}
        onChange={(d) => {
          const nextTime = timePart || "00:00";
          onChange(d ? `${d}T${nextTime}` : "");
        }}
        required={required}
        disabled={disabled}
        error={error}
        helperText={helperText}
        fullWidth
      />
      <TimeField24
        value={timePart}
        onChange={(time) => {
          const nextDate = datePart || getLocalIsoString().slice(0, 10);
          onChange(`${nextDate}T${time}`);
        }}
        ariaLabel={resolvedTimeAria}
        disabled={disabled}
        error={error}
        sx={{ minWidth: 115 }}
      />
    </Box>
  );
}
