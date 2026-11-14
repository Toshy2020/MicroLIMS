import { useSyncExternalStore } from "react";
import type { SyntheticEvent } from "react";
import { Box, Button } from "@mui/material";
import BackspaceOutlinedIcon from "@mui/icons-material/BackspaceOutlined";
import { useTheme } from "@mui/material/styles";
import { BENCH_MIN_TARGET, BENCH_TARGET_GAP } from "../../theme/benchTheme";
import { applyBackspace, applyDigit, applyNext, getActiveField, subscribeActiveField } from "./focusRegistry";

export interface BenchKeypadProps {
  onDigit(d: string): void;
  onBackspace(): void;
  onNext(): void;
  allowDecimal: boolean;
}

// Keys must not take focus from the field they type into.
const keepFocus = (e: SyntheticEvent) => e.preventDefault();

const keySx = { minHeight: BENCH_MIN_TARGET, minWidth: BENCH_MIN_TARGET, fontSize: 20, fontVariantNumeric: "tabular-nums" } as const;

export function BenchKeypad({ onDigit, onBackspace, onNext, allowDecimal }: BenchKeypadProps) {
  const digit = (d: string, label = d) => (
    <Button key={d} variant="outlined" aria-label={label} onPointerDown={keepFocus} onMouseDown={keepFocus} onClick={() => onDigit(d)} sx={keySx}>
      {d}
    </Button>
  );
  return (
    <Box
      role="group"
      aria-label="Number keypad"
      sx={{ display: "grid", gridTemplateColumns: "repeat(3, minmax(48px, 1fr))", gap: `${BENCH_TARGET_GAP}px`, maxWidth: 360 }}
    >
      {["1", "2", "3", "4", "5", "6", "7", "8", "9"].map((d) => digit(d))}
      {allowDecimal ? digit(".", "Decimal point") : <span aria-hidden />}
      {digit("0")}
      <Button variant="outlined" aria-label="Backspace" onPointerDown={keepFocus} onMouseDown={keepFocus} onClick={onBackspace} sx={keySx}>
        <BackspaceOutlinedIcon aria-hidden />
      </Button>
      <Button variant="contained" onPointerDown={keepFocus} onMouseDown={keepFocus} onClick={onNext} sx={{ ...keySx, gridColumn: "1 / -1" }}>
        Next
      </Button>
    </Box>
  );
}

// The keypad wired to whichever NumericEntryField has focus. Rendered only
// in bench mode; outside it hardware / system keyboards are the input.
export function RegistryBenchKeypad() {
  const theme = useTheme();
  const active = useSyncExternalStore(subscribeActiveField, getActiveField);
  if (!theme.custom?.bench) return null;
  return <BenchKeypad onDigit={applyDigit} onBackspace={applyBackspace} onNext={applyNext} allowDecimal={active?.kind === "decimal"} />;
}
