import { Box, Stack, TextField, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { TestFormModel } from "../testFormModel";

interface Props {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
  errors: Record<string, string>;
}

export function DisintegrationSettingsBlock({ model, set, errors }: Props) {
  const { t } = useTranslation("masterData");
  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
        {t("testWizard.typeBlocks.disintegration.title")}
      </Typography>
      <Stack spacing={2}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          <TextField
            size="small"
            type="number"
            name="disintegrationStage1Units"
            label={t("testWizard.typeBlocks.disintegration.stage1Units")}
            value={model.disintegrationStage1Units}
            onChange={(e) => set({ disintegrationStage1Units: e.target.value })}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
            error={Boolean(errors.disintegrationStage1Units)}
            helperText={errors.disintegrationStage1Units || t("testWizard.typeBlocks.disintegration.default6")}
            sx={{ flex: 1 }}
          />
          <TextField
            size="small"
            type="number"
            name="disintegrationStage2Units"
            label={t("testWizard.typeBlocks.disintegration.stage2Units")}
            value={model.disintegrationStage2Units}
            onChange={(e) => set({ disintegrationStage2Units: e.target.value })}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
            error={Boolean(errors.disintegrationStage2Units)}
            helperText={errors.disintegrationStage2Units || t("testWizard.typeBlocks.disintegration.default12")}
            sx={{ flex: 1 }}
          />
        </Stack>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          <TextField
            size="small"
            type="number"
            name="disintegrationMaxStage1Failures"
            label={t("testWizard.typeBlocks.disintegration.maxS1Failures")}
            value={model.disintegrationMaxStage1Failures}
            onChange={(e) => set({ disintegrationMaxStage1Failures: e.target.value })}
            slotProps={{ htmlInput: { min: 0, step: 1 } }}
            error={Boolean(errors.disintegrationMaxStage1Failures)}
            helperText={errors.disintegrationMaxStage1Failures || t("testWizard.typeBlocks.disintegration.default2")}
            sx={{ flex: 1 }}
          />
          <TextField
            size="small"
            type="number"
            name="disintegrationMinPassTotal"
            label={t("testWizard.typeBlocks.disintegration.minPassTotal")}
            value={model.disintegrationMinPassTotal}
            onChange={(e) => set({ disintegrationMinPassTotal: e.target.value })}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
            error={Boolean(errors.disintegrationMinPassTotal)}
            helperText={errors.disintegrationMinPassTotal || t("testWizard.typeBlocks.disintegration.default16")}
            sx={{ flex: 1 }}
          />
        </Stack>
        <TextField
          size="small"
          name="conditionFields"
          label={t("testWizard.typeBlocks.disintegration.conditionFields")}
          placeholder={t("testWizard.typeBlocks.disintegration.conditionPlaceholder")}
          value={model.conditionFields}
          onChange={(e) => set({ conditionFields: e.target.value })}
          error={Boolean(errors.conditionFields)}
          helperText={errors.conditionFields || t("testWizard.typeBlocks.disintegration.conditionHelper")}
          slotProps={{ htmlInput: { maxLength: 500 } }}
          fullWidth
        />
      </Stack>
    </Box>
  );
}
