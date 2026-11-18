import { useSyncExternalStore } from "react";
import type { SyntheticEvent } from "react";
import { Box, Button } from "@mui/material";
import BackspaceOutlinedIcon from "@mui/icons-material/BackspaceOutlined";
import { useTheme } from "@mui/material/styles";
import { useTranslation } from "../../i18n";
import { BENCH_MIN_TARGET, BENCH_TARGET_GAP } from "../../theme/benchTheme";
import { applyBackspace, applyDigit, applyMinus, applyNext, getActiveField, subscribeActiveField } from "./focusRegistry";

const KEYPAD_DIGITS = ["1", "2", "3", "4", "5", "6", "7", "8", "9"] as const;
const DECIMAL_KEY = ".";
const ZERO_KEY = "0";
const MINUS_KEY = "−";

export interface BenchKeypadProps {
  onDigit(d: string): void;
  onBackspace(): void;
  onNext(): void;
  // Shown, with the decimal point, only for decimal fields.
  onMinus?(): void;
  allowDecimal: boolean;
}

// Keys must not take focus from the field they type into.
const keepFocus = (e: SyntheticEvent) => e.preventDefault();

const keySx = { minHeight: BENCH_MIN_TARGET, minWidth: BENCH_MIN_TARGET, fontSize: 20, fontVariantNumeric: "tabular-nums" } as const;

export function BenchKeypad({ onDigit, onBackspace, onNext, onMinus, allowDecimal }: BenchKeypadProps) {
  const { t } = useTranslation();
  const digit = (d: string, label = d) => (
    <Button key={d} variant="outlined" aria-label={label} onPointerDown={keepFocus} onMouseDown={keepFocus} onClick={() => onDigit(d)} sx={keySx}>
      {d}
    </Button>
  );
  return (
    <Box
      role="group"
      aria-label={t("components.benchKeypad.ariaLabel")}
      sx={{ display: "grid", gridTemplateColumns: "repeat(3, minmax(48px, 1fr))", gap: `${BENCH_TARGET_GAP}px`, maxWidth: 360 }}
    >
      {KEYPAD_DIGITS.map((d) => digit(d))}
      {allowDecimal ? digit(DECIMAL_KEY, t("components.benchKeypad.decimalPoint")) : <span aria-hidden />}
      {digit(ZERO_KEY)}
      <Button variant="outlined" aria-label={t("components.benchKeypad.backspace")} onPointerDown={keepFocus} onMouseDown={keepFocus} onClick={onBackspace} sx={keySx}>
        <BackspaceOutlinedIcon aria-hidden />
      </Button>
      {allowDecimal && onMinus && (
        <Button variant="outlined" aria-label={t("components.benchKeypad.minusSign")} onPointerDown={keepFocus} onMouseDown={keepFocus} onClick={onMinus} sx={{ ...keySx, gridColumn: "1 / -1" }}>
          {MINUS_KEY}
        </Button>
      )}
      <Button variant="contained" onPointerDown={keepFocus} onMouseDown={keepFocus} onClick={onNext} sx={{ ...keySx, gridColumn: "1 / -1" }}>
        {t("components.benchKeypad.next")}
      </Button>
    </Box>
  );
}

// The keypad wired to whichever NumericEntryField has focus. Rendered only
// in bench mode and only while a numeric field has focus; outside bench mode it hardware / system keyboards are the input.
export function RegistryBenchKeypad() {
  const theme = useTheme();
  const active = useSyncExternalStore(subscribeActiveField, getActiveField);
  // Only while a numeric entry field has focus; otherwise it would cover forms that have nothing to type.
  if (!theme.custom?.bench || !active) return null;
  return <BenchKeypad onDigit={applyDigit} onBackspace={applyBackspace} onNext={applyNext} onMinus={applyMinus} allowDecimal={active?.kind === "decimal"} />;
}
