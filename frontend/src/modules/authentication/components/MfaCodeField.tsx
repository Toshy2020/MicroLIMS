import type { Ref } from "react";
import { TextField } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";

export interface MfaCodeFieldProps {
  value: string;
  onChange: (value: string) => void;
  error?: string;
  disabled?: boolean;
  autoFocus?: boolean;
  inputRef?: Ref<HTMLInputElement>;
}

/**
 * Six-digit TOTP entry input for second-factor login and enrolment confirmation.
 */
export function MfaCodeField({
  value,
  onChange,
  error,
  disabled = false,
  autoFocus = false,
  inputRef
}: MfaCodeFieldProps) {
  const { t } = useTranslation("account");
  return (
    <TextField
      label={t("mfaCode.label")}
      value={value}
      onChange={(e) => onChange(e.target.value.replace(/\D/g, "").slice(0, 6))}
      error={Boolean(error)}
      helperText={error}
      disabled={disabled}
      autoFocus={autoFocus}
      inputRef={inputRef}
      fullWidth
      slotProps={{
        inputLabel: { required: false },
        htmlInput: {
          required: true,
          inputMode: "numeric",
          autoComplete: "one-time-code",
          maxLength: 6
        },
        input: {
          sx: {
            fontFamily: monospaceFontFamily,
            fontVariantNumeric: "tabular-nums"
          }
        }
      }}
    />
  );
}
