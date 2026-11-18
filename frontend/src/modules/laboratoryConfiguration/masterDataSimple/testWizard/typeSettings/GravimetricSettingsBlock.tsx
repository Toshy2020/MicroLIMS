import {
  Box,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Switch,
  TextField,
  Typography
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { MoistureRole } from "../../../../../services/masterDataOptions";
import type { TestFormModel } from "../testFormModel";

interface Props {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
  errors: Record<string, string>;
}

export function GravimetricSettingsBlock({ model, set, errors }: Props) {
  const { t } = useTranslation("masterData");
  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
        {t("testWizard.typeBlocks.gravimetric.title")}
      </Typography>
      <Stack spacing={2}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: "center" }}>
          <TextField
            size="small"
            type="number"
            name="replicateCount"
            label={t("testWizard.typeBlocks.gravimetric.replicateCount")}
            value={model.replicateCount}
            onChange={(e) => set({ replicateCount: e.target.value })}
            slotProps={{ htmlInput: { min: 1, max: 30, step: 1 } }}
            error={Boolean(errors.replicateCount)}
            helperText={errors.replicateCount || t("testWizard.typeBlocks.gravimetric.replicateHelper")}
            required
            sx={{ flex: 1 }}
          />
          <FormControlLabel
            control={
              <Switch
                checked={model.usesTare}
                onChange={(e) => set({ usesTare: e.target.checked })}
              />
            }
            label={t("testWizard.typeBlocks.gravimetric.usesTare")}
            sx={{ flex: 1 }}
          />
        </Stack>
        <TextField
          size="small"
          name="conditionFields"
          label={t("testWizard.typeBlocks.gravimetric.conditionFields")}
          placeholder={t("testWizard.typeBlocks.gravimetric.conditionPlaceholder")}
          value={model.conditionFields}
          onChange={(e) => set({ conditionFields: e.target.value })}
          error={Boolean(errors.conditionFields)}
          helperText={errors.conditionFields || t("testWizard.typeBlocks.gravimetric.conditionHelper")}
          slotProps={{ htmlInput: { maxLength: 500 } }}
          fullWidth
        />
        {model.equationType === "GravimetricLoss" && (
          <FormControl size="small" sx={{ maxWidth: 300 }}>
            <InputLabel id="gravimetric-moisture-role-label">{t("testWizard.typeBlocks.gravimetric.moistureRole")}</InputLabel>
            <Select<MoistureRole>
              labelId="gravimetric-moisture-role-label"
              name="moistureRole"
              label={t("testWizard.typeBlocks.gravimetric.moistureRole")}
              value={model.moistureRole === "Water" ? "None" : model.moistureRole}
              onChange={(e) => set({ moistureRole: e.target.value as MoistureRole })}
            >
              <MenuItem value="None">{t("testWizard.typeBlocks.gravimetric.moistureNone")}</MenuItem>
              <MenuItem value="LossOnDrying">{t("testWizard.typeBlocks.gravimetric.moistureLod")}</MenuItem>
            </Select>
          </FormControl>
        )}
      </Stack>
    </Box>
  );
}
