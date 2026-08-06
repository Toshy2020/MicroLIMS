import { Box, Button, TextField, Paper } from "@mui/material";
import { brandColors } from "../../../theme";
import { QUICK_PERIOD_OPTIONS, QuickPeriod, toDateInputValue } from "../utils/dateRange";

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
  const today = toDateInputValue(new Date());

  return (
    <Paper sx={{ p: 1.5, mb: 2, display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
      {QUICK_PERIOD_OPTIONS.map((opt) => (
        <Button
          key={opt.value}
          size="small"
          variant={period === opt.value ? "contained" : "outlined"}
          onClick={() => onPeriodChange(opt.value)}
          sx={period === opt.value ? {} : { color: brandColors.sectionTitle, borderColor: brandColors.sectionTitle }}
        >
          {opt.label}
        </Button>
      ))}

      {period === "custom" && (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, ml: 1 }}>
          <TextField
            size="small" type="date" label="From" InputLabelProps={{ shrink: true }}
            value={customFrom} inputProps={{ max: customTo || today }}
            onChange={(e) => onCustomChange(e.target.value, customTo)}
          />
          <TextField
            size="small" type="date" label="To" InputLabelProps={{ shrink: true }}
            value={customTo} inputProps={{ min: customFrom, max: today }}
            onChange={(e) => onCustomChange(customFrom, e.target.value)}
          />
        </Box>
      )}
    </Paper>
  );
}
