import { Box, Stack, TextField, Typography } from "@mui/material";
import type { TestFormModel } from "../testFormModel";

interface Props {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
  errors: Record<string, string>;
}

export function DisintegrationSettingsBlock({ model, set, errors }: Props) {
  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
        Disintegration Acceptance (USP &lt;701&gt; / EP 2.9.1)
      </Typography>
      <Stack spacing={2}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          <TextField
            size="small"
            type="number"
            name="disintegrationStage1Units"
            label="Stage 1 Units"
            value={model.disintegrationStage1Units}
            onChange={(e) => set({ disintegrationStage1Units: e.target.value })}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
            error={Boolean(errors.disintegrationStage1Units)}
            helperText={errors.disintegrationStage1Units || "Default: 6"}
            sx={{ flex: 1 }}
          />
          <TextField
            size="small"
            type="number"
            name="disintegrationStage2Units"
            label="Stage 2 Units"
            value={model.disintegrationStage2Units}
            onChange={(e) => set({ disintegrationStage2Units: e.target.value })}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
            error={Boolean(errors.disintegrationStage2Units)}
            helperText={errors.disintegrationStage2Units || "Default: 12"}
            sx={{ flex: 1 }}
          />
        </Stack>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          <TextField
            size="small"
            type="number"
            name="disintegrationMaxStage1Failures"
            label="Max S1 Failures"
            value={model.disintegrationMaxStage1Failures}
            onChange={(e) => set({ disintegrationMaxStage1Failures: e.target.value })}
            slotProps={{ htmlInput: { min: 0, step: 1 } }}
            error={Boolean(errors.disintegrationMaxStage1Failures)}
            helperText={errors.disintegrationMaxStage1Failures || "Default: 2 (0..S1-1)"}
            sx={{ flex: 1 }}
          />
          <TextField
            size="small"
            type="number"
            name="disintegrationMinPassTotal"
            label="Min Pass Total"
            value={model.disintegrationMinPassTotal}
            onChange={(e) => set({ disintegrationMinPassTotal: e.target.value })}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
            error={Boolean(errors.disintegrationMinPassTotal)}
            helperText={errors.disintegrationMinPassTotal || "Default: 16 (1..S1+S2)"}
            sx={{ flex: 1 }}
          />
        </Stack>
        <TextField
          size="small"
          name="conditionFields"
          label="Condition fields"
          placeholder="Medium,Temperature (°C),Discs"
          value={model.conditionFields}
          onChange={(e) => set({ conditionFields: e.target.value })}
          error={Boolean(errors.conditionFields)}
          helperText={errors.conditionFields || "comma-separated, e.g. Medium,Temperature (°C),Discs"}
          slotProps={{ htmlInput: { maxLength: 500 } }}
          fullWidth
        />
      </Stack>
    </Box>
  );
}
