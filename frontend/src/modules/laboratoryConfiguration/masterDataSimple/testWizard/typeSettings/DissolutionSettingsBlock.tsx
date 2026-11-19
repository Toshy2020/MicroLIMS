import {
  Box,
  FormControlLabel,
  Stack,
  Switch,
  TextField,
  Typography
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { TestFormModel } from "../testFormModel";

interface Props {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
  errors: Record<string, string>;
}

export function DissolutionSettingsBlock({ model, set, errors }: Props) {
  const { t } = useTranslation("masterData");
  return (
    <Stack spacing={2}>
      <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
        <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
          {t("testWizard.typeBlocks.dissolution.title")}
        </Typography>
        <Stack spacing={2}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              size="small"
              type="number"
              name="dissolutionS1Offset"
              label={t("testWizard.typeBlocks.dissolution.s1Offset")}
              value={model.dissolutionS1Offset}
              onChange={(e) => set({ dissolutionS1Offset: e.target.value })}
              slotProps={{ htmlInput: { min: 0, step: "any" } }}
              error={Boolean(errors.dissolutionS1Offset)}
              helperText={errors.dissolutionS1Offset || t("testWizard.typeBlocks.dissolution.default5")}
              sx={{ flex: 1 }}
            />
            <TextField
              size="small"
              type="number"
              name="dissolutionS2MinOffset"
              label={t("testWizard.typeBlocks.dissolution.s2MinOffset")}
              value={model.dissolutionS2MinOffset}
              onChange={(e) => set({ dissolutionS2MinOffset: e.target.value })}
              slotProps={{ htmlInput: { min: 0, step: "any" } }}
              error={Boolean(errors.dissolutionS2MinOffset)}
              helperText={errors.dissolutionS2MinOffset || t("testWizard.typeBlocks.dissolution.default15")}
              sx={{ flex: 1 }}
            />
          </Stack>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              size="small"
              type="number"
              name="dissolutionS3MinOffset"
              label={t("testWizard.typeBlocks.dissolution.s3MinOffset")}
              value={model.dissolutionS3MinOffset}
              onChange={(e) => set({ dissolutionS3MinOffset: e.target.value })}
              slotProps={{ htmlInput: { min: 0, step: "any" } }}
              error={Boolean(errors.dissolutionS3MinOffset)}
              helperText={errors.dissolutionS3MinOffset || t("testWizard.typeBlocks.dissolution.default25")}
              sx={{ flex: 1 }}
            />
            <TextField
              size="small"
              type="number"
              name="dissolutionS3MaxBelowS2Min"
              label={t("testWizard.typeBlocks.dissolution.s3MaxBelowS2")}
              value={model.dissolutionS3MaxBelowS2Min}
              onChange={(e) => set({ dissolutionS3MaxBelowS2Min: e.target.value })}
              slotProps={{ htmlInput: { min: 0, step: 1 } }}
              error={Boolean(errors.dissolutionS3MaxBelowS2Min)}
              helperText={errors.dissolutionS3MaxBelowS2Min || t("testWizard.typeBlocks.dissolution.default2Units")}
              sx={{ flex: 1 }}
            />
          </Stack>
          <TextField
            size="small"
            name="conditionFields"
            label={t("testWizard.typeBlocks.dissolution.conditionFields")}
            placeholder={t("testWizard.typeBlocks.dissolution.conditionPlaceholder")}
            value={model.conditionFields}
            onChange={(e) => set({ conditionFields: e.target.value })}
            error={Boolean(errors.conditionFields)}
            helperText={errors.conditionFields || t("testWizard.typeBlocks.dissolution.conditionHelper")}
            slotProps={{ htmlInput: { maxLength: 500 } }}
            fullWidth
          />
        </Stack>
      </Box>

      <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
        <FormControlLabel
          control={<Switch checked={model.requiresSystemSuitability} disabled />}
          label={t("testWizard.typeBlocks.dissolution.requiresSystemSuitability")}
        />
        <Box sx={{ mt: 2, pt: 2, borderTop: "1px dashed", borderTopColor: "divider" }}>
          <TextField
            size="small"
            name="methodAbbreviation"
            label={t("testWizard.typeBlocks.dissolution.methodAbbreviation")}
            placeholder={t("testWizard.typeBlocks.dissolution.methodAbbreviationPlaceholder")}
            value={model.methodAbbreviation}
            onChange={(e) => set({ methodAbbreviation: e.target.value.toUpperCase() })}
            required
            fullWidth
            error={Boolean(errors.methodAbbreviation)}
            slotProps={{ htmlInput: { maxLength: 20 } }}
            helperText={errors.methodAbbreviation || t("testWizard.typeBlocks.dissolution.methodAbbreviationHelper")}
          />
        </Box>
      </Box>
    </Stack>
  );
}
