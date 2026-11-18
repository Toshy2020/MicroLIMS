import { Box, Stack, TextField, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { TestFormModel } from "../testFormModel";

interface Props {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
  errors: Record<string, string>;
}

export function WeightVariationSettingsBlock({ model, set, errors }: Props) {
  const { t } = useTranslation("masterData");
  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
        {t("testWizard.typeBlocks.weightVariation.title")}
      </Typography>
      <Stack spacing={2}>
        <TextField
          size="small"
          type="number"
          name="wvUnitCount"
          label={t("testWizard.typeBlocks.weightVariation.unitCount")}
          value={model.wvUnitCount}
          onChange={(e) => set({ wvUnitCount: e.target.value })}
          slotProps={{ htmlInput: { min: 1, step: 1 } }}
          error={Boolean(errors.wvUnitCount)}
          helperText={errors.wvUnitCount || t("testWizard.typeBlocks.weightVariation.default20")}
          sx={{ maxWidth: 220 }}
        />

        <Box sx={{ p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1.5, color: "text.secondary" }}>
            {t("testWizard.typeBlocks.weightVariation.tablets")}
          </Typography>
          <Stack spacing={2}>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                type="number"
                name="wvTabletBand1MaxMg"
                label={t("testWizard.typeBlocks.weightVariation.band1MaxMg")}
                value={model.wvTabletBand1MaxMg}
                onChange={(e) => set({ wvTabletBand1MaxMg: e.target.value })}
                slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                error={Boolean(errors.wvTabletBand1MaxMg)}
                helperText={errors.wvTabletBand1MaxMg || t("testWizard.typeBlocks.weightVariation.default130")}
                sx={{ flex: 1 }}
              />
              <TextField
                size="small"
                type="number"
                name="wvTabletBand1Percent"
                label={t("testWizard.typeBlocks.weightVariation.band1Percent")}
                value={model.wvTabletBand1Percent}
                onChange={(e) => set({ wvTabletBand1Percent: e.target.value })}
                slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                error={Boolean(errors.wvTabletBand1Percent)}
                helperText={errors.wvTabletBand1Percent || t("testWizard.typeBlocks.weightVariation.default10")}
                sx={{ flex: 1 }}
              />
            </Stack>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                type="number"
                name="wvTabletBand2MaxMg"
                label={t("testWizard.typeBlocks.weightVariation.band2MaxMg")}
                value={model.wvTabletBand2MaxMg}
                onChange={(e) => set({ wvTabletBand2MaxMg: e.target.value })}
                slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                error={Boolean(errors.wvTabletBand2MaxMg)}
                helperText={errors.wvTabletBand2MaxMg || t("testWizard.typeBlocks.weightVariation.default324")}
                sx={{ flex: 1 }}
              />
              <TextField
                size="small"
                type="number"
                name="wvTabletBand2Percent"
                label={t("testWizard.typeBlocks.weightVariation.band2Percent")}
                value={model.wvTabletBand2Percent}
                onChange={(e) => set({ wvTabletBand2Percent: e.target.value })}
                slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                error={Boolean(errors.wvTabletBand2Percent)}
                helperText={errors.wvTabletBand2Percent || t("testWizard.typeBlocks.weightVariation.default7_5")}
                sx={{ flex: 1 }}
              />
            </Stack>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                type="number"
                name="wvTabletBand3Percent"
                label={t("testWizard.typeBlocks.weightVariation.band3Percent")}
                value={model.wvTabletBand3Percent}
                onChange={(e) => set({ wvTabletBand3Percent: e.target.value })}
                slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                error={Boolean(errors.wvTabletBand3Percent)}
                helperText={errors.wvTabletBand3Percent || t("testWizard.typeBlocks.weightVariation.default5")}
                sx={{ flex: 1 }}
              />
              <TextField
                size="small"
                type="number"
                name="wvTabletMaxOutside"
                label={t("testWizard.typeBlocks.weightVariation.maxOutside")}
                value={model.wvTabletMaxOutside}
                onChange={(e) => set({ wvTabletMaxOutside: e.target.value })}
                slotProps={{ htmlInput: { min: 0, step: 1 } }}
                error={Boolean(errors.wvTabletMaxOutside)}
                helperText={errors.wvTabletMaxOutside || t("testWizard.typeBlocks.weightVariation.default2")}
                sx={{ flex: 1 }}
              />
            </Stack>
          </Stack>
        </Box>

        <Box sx={{ p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1.5, color: "text.secondary" }}>
            {t("testWizard.typeBlocks.weightVariation.capsules")}
          </Typography>
          <Stack spacing={2}>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                type="number"
                name="wvCapsuleInnerPercent"
                label={t("testWizard.typeBlocks.weightVariation.innerLimit")}
                value={model.wvCapsuleInnerPercent}
                onChange={(e) => set({ wvCapsuleInnerPercent: e.target.value })}
                slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                error={Boolean(errors.wvCapsuleInnerPercent)}
                helperText={errors.wvCapsuleInnerPercent || t("testWizard.typeBlocks.weightVariation.default10")}
                sx={{ flex: 1 }}
              />
              <TextField
                size="small"
                type="number"
                name="wvCapsuleOuterPercent"
                label={t("testWizard.typeBlocks.weightVariation.outerLimit")}
                value={model.wvCapsuleOuterPercent}
                onChange={(e) => set({ wvCapsuleOuterPercent: e.target.value })}
                slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                error={Boolean(errors.wvCapsuleOuterPercent)}
                helperText={errors.wvCapsuleOuterPercent || t("testWizard.typeBlocks.weightVariation.default25")}
                sx={{ flex: 1 }}
              />
            </Stack>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                type="number"
                name="wvCapsuleS1MaxOutside"
                label={t("testWizard.typeBlocks.weightVariation.s1MaxOutside")}
                value={model.wvCapsuleS1MaxOutside}
                onChange={(e) => set({ wvCapsuleS1MaxOutside: e.target.value })}
                slotProps={{ htmlInput: { min: 0, step: 1 } }}
                error={Boolean(errors.wvCapsuleS1MaxOutside)}
                helperText={errors.wvCapsuleS1MaxOutside || t("testWizard.typeBlocks.weightVariation.default2Pass")}
                sx={{ flex: 1 }}
              />
              <TextField
                size="small"
                type="number"
                name="wvCapsuleS1MaxForRetest"
                label={t("testWizard.typeBlocks.weightVariation.s1MaxForRetest")}
                value={model.wvCapsuleS1MaxForRetest}
                onChange={(e) => set({ wvCapsuleS1MaxForRetest: e.target.value })}
                slotProps={{ htmlInput: { min: 0, step: 1 } }}
                error={Boolean(errors.wvCapsuleS1MaxForRetest)}
                helperText={errors.wvCapsuleS1MaxForRetest || t("testWizard.typeBlocks.weightVariation.default6Retest")}
                sx={{ flex: 1 }}
              />
            </Stack>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                type="number"
                name="wvCapsuleS2ExtraUnits"
                label={t("testWizard.typeBlocks.weightVariation.s2ExtraUnits")}
                value={model.wvCapsuleS2ExtraUnits}
                onChange={(e) => set({ wvCapsuleS2ExtraUnits: e.target.value })}
                slotProps={{ htmlInput: { min: 1, step: 1 } }}
                error={Boolean(errors.wvCapsuleS2ExtraUnits)}
                helperText={errors.wvCapsuleS2ExtraUnits || t("testWizard.typeBlocks.weightVariation.default40")}
                sx={{ flex: 1 }}
              />
              <TextField
                size="small"
                type="number"
                name="wvCapsuleS2MaxOutside"
                label={t("testWizard.typeBlocks.weightVariation.s2MaxOutside")}
                value={model.wvCapsuleS2MaxOutside}
                onChange={(e) => set({ wvCapsuleS2MaxOutside: e.target.value })}
                slotProps={{ htmlInput: { min: 0, step: 1 } }}
                error={Boolean(errors.wvCapsuleS2MaxOutside)}
                helperText={errors.wvCapsuleS2MaxOutside || t("testWizard.typeBlocks.weightVariation.default6Total")}
                sx={{ flex: 1 }}
              />
            </Stack>
          </Stack>
        </Box>

        <TextField
          size="small"
          name="conditionFields"
          label={t("testWizard.typeBlocks.weightVariation.conditionFields")}
          placeholder={t("testWizard.typeBlocks.weightVariation.conditionPlaceholder")}
          value={model.conditionFields}
          onChange={(e) => set({ conditionFields: e.target.value })}
          error={Boolean(errors.conditionFields)}
          helperText={errors.conditionFields || t("testWizard.typeBlocks.weightVariation.conditionHelper")}
          slotProps={{ htmlInput: { maxLength: 500 } }}
          fullWidth
        />
      </Stack>
    </Box>
  );
}
