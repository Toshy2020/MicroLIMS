import {
  Box,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { MeasurementEvaluationBasis, TestFormModel } from "../testFormModel";

interface Props {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
  errors: Record<string, string>;
}

export function MeasurementSettingsBlock({ model, set, errors }: Props) {
  const { t } = useTranslation("masterData");
  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
        {t("testWizard.typeBlocks.measurement.title")}
      </Typography>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
        <TextField
          size="small"
          type="number"
          name="replicateCount"
          label={t("testWizard.typeBlocks.measurement.replicateCount")}
          value={model.replicateCount}
          onChange={(e) => set({ replicateCount: e.target.value })}
          slotProps={{ htmlInput: { min: 1, max: 30, step: 1 } }}
          error={Boolean(errors.replicateCount)}
          helperText={errors.replicateCount || t("testWizard.typeBlocks.measurement.replicateHelper")}
          required
          sx={{ flex: 1 }}
        />
        <FormControl size="small" sx={{ flex: 1 }} required error={Boolean(errors.evaluationBasis)}>
          <InputLabel id="dialog-eval-basis-label">{t("testWizard.typeBlocks.measurement.evaluationBasis")}</InputLabel>
          <Select
            labelId="dialog-eval-basis-label"
            name="evaluationBasis"
            label={t("testWizard.typeBlocks.measurement.evaluationBasis")}
            value={model.evaluationBasis}
            onChange={(e) => set({ evaluationBasis: e.target.value as MeasurementEvaluationBasis })}
          >
            <MenuItem value="Mean">{t("testWizard.typeBlocks.measurement.basisMean")}</MenuItem>
            <MenuItem value="EachValue">{t("testWizard.typeBlocks.measurement.basisEachValue")}</MenuItem>
            <MenuItem value="Min">{t("testWizard.typeBlocks.measurement.basisMin")}</MenuItem>
            <MenuItem value="Max">{t("testWizard.typeBlocks.measurement.basisMax")}</MenuItem>
          </Select>
        </FormControl>
      </Stack>
    </Box>
  );
}
