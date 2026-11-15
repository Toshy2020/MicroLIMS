import { Box, TextField } from "@mui/material";
import type { LimitGroupProps } from "./NumericLimits";

/**
 * Count-tiered limit fields (Alert, Action, Specification Limit).
 */
export function CountTieredLimits({ model, set, errors }: LimitGroupProps) {
  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" },
        gap: 2
      }}
    >
      <TextField
        size="small"
        name="alertLimit"
        label="Alert Limit"
        placeholder="e.g. 100"
        value={model.alertLimit}
        onChange={(e) => set({ alertLimit: e.target.value })}
        error={Boolean(errors.alertLimit)}
        helperText={errors.alertLimit}
        fullWidth
      />
      <TextField
        size="small"
        name="actionLimit"
        label="Action Limit"
        placeholder="e.g. 500"
        value={model.actionLimit}
        onChange={(e) => set({ actionLimit: e.target.value })}
        error={Boolean(errors.actionLimit)}
        helperText={errors.actionLimit}
        fullWidth
      />
      <TextField
        size="small"
        name="specLimit"
        label="Specification Limit *"
        placeholder="e.g. 1000"
        value={model.specLimit}
        onChange={(e) => set({ specLimit: e.target.value })}
        error={Boolean(errors.specLimit)}
        helperText={errors.specLimit}
        fullWidth
        required
      />
    </Box>
  );
}
