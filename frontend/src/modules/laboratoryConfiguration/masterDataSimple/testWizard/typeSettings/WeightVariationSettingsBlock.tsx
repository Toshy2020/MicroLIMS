import { Box, Stack, TextField, Typography } from "@mui/material";
import type { TestFormModel } from "../testFormModel";

interface Props {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
  errors: Record<string, string>;
}

export function WeightVariationSettingsBlock({ model, set, errors }: Props) {
  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
        Weight Variation Acceptance (USP &lt;2091&gt;)
      </Typography>
      <Stack spacing={2}>
        <TextField
          size="small"
          type="number"
          name="wvUnitCount"
          label="Unit Count (Stage 1)"
          value={model.wvUnitCount}
          onChange={(e) => set({ wvUnitCount: e.target.value })}
          slotProps={{ htmlInput: { min: 1, step: 1 } }}
          error={Boolean(errors.wvUnitCount)}
          helperText={errors.wvUnitCount || "Default: 20"}
          sx={{ maxWidth: 220 }}
        />

        <Box sx={{ p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1.5, color: "text.secondary" }}>
            Tablets
          </Typography>
          <Stack spacing={2}>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                type="number"
                name="wvTabletBand1MaxMg"
                label="Band 1 Max (mg)"
                value={model.wvTabletBand1MaxMg}
                onChange={(e) => set({ wvTabletBand1MaxMg: e.target.value })}
                slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                error={Boolean(errors.wvTabletBand1MaxMg)}
                helperText={errors.wvTabletBand1MaxMg || "Default: 130"}
                sx={{ flex: 1 }}
              />
              <TextField
                size="small"
                type="number"
                name="wvTabletBand1Percent"
                label="Band 1 Deviation (%)"
                value={model.wvTabletBand1Percent}
                onChange={(e) => set({ wvTabletBand1Percent: e.target.value })}
                slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                error={Boolean(errors.wvTabletBand1Percent)}
                helperText={errors.wvTabletBand1Percent || "Default: 10"}
                sx={{ flex: 1 }}
              />
            </Stack>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                type="number"
                name="wvTabletBand2MaxMg"
                label="Band 2 Max (mg)"
                value={model.wvTabletBand2MaxMg}
                onChange={(e) => set({ wvTabletBand2MaxMg: e.target.value })}
                slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                error={Boolean(errors.wvTabletBand2MaxMg)}
                helperText={errors.wvTabletBand2MaxMg || "Default: 324"}
                sx={{ flex: 1 }}
              />
              <TextField
                size="small"
                type="number"
                name="wvTabletBand2Percent"
                label="Band 2 Deviation (%)"
                value={model.wvTabletBand2Percent}
                onChange={(e) => set({ wvTabletBand2Percent: e.target.value })}
                slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                error={Boolean(errors.wvTabletBand2Percent)}
                helperText={errors.wvTabletBand2Percent || "Default: 7.5"}
                sx={{ flex: 1 }}
              />
            </Stack>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                type="number"
                name="wvTabletBand3Percent"
                label="Band 3 Deviation (%)"
                value={model.wvTabletBand3Percent}
                onChange={(e) => set({ wvTabletBand3Percent: e.target.value })}
                slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                error={Boolean(errors.wvTabletBand3Percent)}
                helperText={errors.wvTabletBand3Percent || "Default: 5 (> Band 2)"}
                sx={{ flex: 1 }}
              />
              <TextField
                size="small"
                type="number"
                name="wvTabletMaxOutside"
                label="Max Outside Count"
                value={model.wvTabletMaxOutside}
                onChange={(e) => set({ wvTabletMaxOutside: e.target.value })}
                slotProps={{ htmlInput: { min: 0, step: 1 } }}
                error={Boolean(errors.wvTabletMaxOutside)}
                helperText={errors.wvTabletMaxOutside || "Default: 2"}
                sx={{ flex: 1 }}
              />
            </Stack>
          </Stack>
        </Box>

        <Box sx={{ p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1.5, color: "text.secondary" }}>
            Capsules
          </Typography>
          <Stack spacing={2}>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                type="number"
                name="wvCapsuleInnerPercent"
                label="Inner Limit (%)"
                value={model.wvCapsuleInnerPercent}
                onChange={(e) => set({ wvCapsuleInnerPercent: e.target.value })}
                slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                error={Boolean(errors.wvCapsuleInnerPercent)}
                helperText={errors.wvCapsuleInnerPercent || "Default: 10"}
                sx={{ flex: 1 }}
              />
              <TextField
                size="small"
                type="number"
                name="wvCapsuleOuterPercent"
                label="Outer Limit (%)"
                value={model.wvCapsuleOuterPercent}
                onChange={(e) => set({ wvCapsuleOuterPercent: e.target.value })}
                slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                error={Boolean(errors.wvCapsuleOuterPercent)}
                helperText={errors.wvCapsuleOuterPercent || "Default: 25"}
                sx={{ flex: 1 }}
              />
            </Stack>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                type="number"
                name="wvCapsuleS1MaxOutside"
                label="Stage 1 Max Outside"
                value={model.wvCapsuleS1MaxOutside}
                onChange={(e) => set({ wvCapsuleS1MaxOutside: e.target.value })}
                slotProps={{ htmlInput: { min: 0, step: 1 } }}
                error={Boolean(errors.wvCapsuleS1MaxOutside)}
                helperText={errors.wvCapsuleS1MaxOutside || "Default: 2 (pass without retest)"}
                sx={{ flex: 1 }}
              />
              <TextField
                size="small"
                type="number"
                name="wvCapsuleS1MaxForRetest"
                label="Stage 1 Max For Retest"
                value={model.wvCapsuleS1MaxForRetest}
                onChange={(e) => set({ wvCapsuleS1MaxForRetest: e.target.value })}
                slotProps={{ htmlInput: { min: 0, step: 1 } }}
                error={Boolean(errors.wvCapsuleS1MaxForRetest)}
                helperText={errors.wvCapsuleS1MaxForRetest || "Default: 6 (qualifies for S2)"}
                sx={{ flex: 1 }}
              />
            </Stack>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                type="number"
                name="wvCapsuleS2ExtraUnits"
                label="Stage 2 Extra Units"
                value={model.wvCapsuleS2ExtraUnits}
                onChange={(e) => set({ wvCapsuleS2ExtraUnits: e.target.value })}
                slotProps={{ htmlInput: { min: 1, step: 1 } }}
                error={Boolean(errors.wvCapsuleS2ExtraUnits)}
                helperText={errors.wvCapsuleS2ExtraUnits || "Default: 40"}
                sx={{ flex: 1 }}
              />
              <TextField
                size="small"
                type="number"
                name="wvCapsuleS2MaxOutside"
                label="Stage 2 Max Outside"
                value={model.wvCapsuleS2MaxOutside}
                onChange={(e) => set({ wvCapsuleS2MaxOutside: e.target.value })}
                slotProps={{ htmlInput: { min: 0, step: 1 } }}
                error={Boolean(errors.wvCapsuleS2MaxOutside)}
                helperText={errors.wvCapsuleS2MaxOutside || "Default: 6 (across S1+S2)"}
                sx={{ flex: 1 }}
              />
            </Stack>
          </Stack>
        </Box>

        <TextField
          size="small"
          name="conditionFields"
          label="Condition fields"
          placeholder="Balance ID,Temperature (°C)"
          value={model.conditionFields}
          onChange={(e) => set({ conditionFields: e.target.value })}
          error={Boolean(errors.conditionFields)}
          helperText={errors.conditionFields || "comma-separated, e.g. Balance ID,Temperature (°C)"}
          slotProps={{ htmlInput: { maxLength: 500 } }}
          fullWidth
        />
      </Stack>
    </Box>
  );
}
