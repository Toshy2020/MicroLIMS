import { Box } from "@mui/material";

// "—" for missing values so an empty reading is never mistaken for zero.
export function formatNumber(value: number | null | undefined, decimals?: number, unit?: string): string {
  if (value === null || value === undefined || Number.isNaN(value)) return "—";
  const text = decimals === undefined ? String(value) : value.toFixed(decimals);
  return unit ? `${text} ${unit}` : text;
}

interface NumericCellProps {
  value: number | null | undefined;
  decimals?: number;
  unit?: string;
}

// Tabular figures so digits line up between rows; the unit is a muted suffix.
export function NumericCell({ value, decimals, unit }: NumericCellProps) {
  const text = formatNumber(value, decimals);
  return (
    <Box component="span" sx={{ fontVariantNumeric: "tabular-nums", whiteSpace: "nowrap" }}>
      {text}
      {unit && text !== "—" && (
        <Box component="span" sx={{ color: "text.secondary", ml: 0.5, fontSize: "0.85em" }}>{unit}</Box>
      )}
    </Box>
  );
}
