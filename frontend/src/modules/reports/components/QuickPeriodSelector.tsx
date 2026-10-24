import { Box, Button, Paper, useTheme } from "@mui/material";
import { QUICK_PERIOD_OPTIONS, QuickPeriod, toDateInputValue } from "../utils/dateRange";
import { DateField } from "../../../components/DateField";

interface QuickPeriodSelectorProps {
  period: QuickPeriod;
  customFrom: string;
  customTo: string;
  onPeriodChange: (period: QuickPeriod) => void;
  onCustomChange: (from: string, to: string) => void;
}

// The period buttons take effect immediately on click (no separate
// "Apply") - only the Custom range needs an explicit pair of date
// pickers, since there's nothing to click until both ends are chosen.
export function QuickPeriodSelector({ period, customFrom, customTo, onPeriodChange, onCustomChange }: QuickPeriodSelectorProps) {
  const theme = useTheme();
  const today = toDateInputValue(new Date());

  return (
    <Paper sx={{ p: 1.5, mb: 2, display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
      {QUICK_PERIOD_OPTIONS.map((opt) => (
        <Button
          key={opt.value}
          size="small"
          variant={period === opt.value ? "contained" : "outlined"}
          onClick={() => onPeriodChange(opt.value)}
          sx={period === opt.value ? {} : { color: theme.palette.primary.main, borderColor: theme.palette.primary.main }}
        >
          {opt.label}
        </Button>
      ))}

      {period === "custom" && (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, ml: 1 }}>
          <DateField
            label="From"
            value={customFrom}
            onChange={(v) => onCustomChange(v, customTo)}
            max={customTo || today} />
          <DateField
            label="To"
            value={customTo}
            onChange={(v) => onCustomChange(customFrom, v)}
            min={customFrom}
            max={today} />
        </Box>
      )}
    </Paper>
  );
}
