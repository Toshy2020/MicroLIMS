import {
  Box,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  TextField
} from "@mui/material";
import { useTranslation } from "../../../../../i18n";
import type { ExpectedPresence } from "../../../specifications/services/SpecificationService";
import type { LimitGroupProps } from "./NumericLimits";

/**
 * Qualitative and Presence/Absence limit fields.
 */
export function QualitativeLimits({ model, set, errors }: LimitGroupProps) {
  const { t } = useTranslation("labConfig");
  if (model.limitType === "Qualitative") {
    return (
      <TextField
        size="small"
        name="expectedResultText"
        label={t("specWizard.qualitativeLimits.expectedTextLabel")}
        multiline
        minRows={2}
        placeholder={t("specWizard.qualitativeLimits.expectedTextPlaceholder")}
        value={model.expectedResultText}
        onChange={(e) => set({ expectedResultText: e.target.value })}
        error={Boolean(errors.expectedResultText)}
        helperText={errors.expectedResultText}
        fullWidth
      />
    );
  }

  if (model.limitType === "PresenceAbsence") {
    return (
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" },
          gap: 2
        }}
      >
        <FormControl size="small" fullWidth error={Boolean(errors.expectedState)}>
          <InputLabel id="expected-state-label">{t("specWizard.qualitativeLimits.expectedStateLabel")}</InputLabel>
          <Select
            name="expectedState"
            labelId="expected-state-label"
            label={t("specWizard.qualitativeLimits.expectedStateLabel")}
            value={model.expectedState}
            onChange={(e) => set({ expectedState: e.target.value as ExpectedPresence })}
          >
            <MenuItem value="Absence">{t("specWizard.qualitativeLimits.absence")}</MenuItem>
            <MenuItem value="Presence">{t("specWizard.qualitativeLimits.presence")}</MenuItem>
          </Select>
          {errors.expectedState && <FormHelperText>{errors.expectedState}</FormHelperText>}
        </FormControl>
        <TextField
          size="small"
          name="sampleQuantity"
          label={t("specWizard.qualitativeLimits.sampleQtyLabel")}
          type="number"
          placeholder={t("specWizard.qualitativeLimits.sampleQtyPlaceholder")}
          value={model.sampleQuantity}
          onChange={(e) => set({ sampleQuantity: e.target.value })}
          error={Boolean(errors.sampleQuantity)}
          helperText={errors.sampleQuantity}
          slotProps={{ htmlInput: { step: "any" } }}
          fullWidth
        />
        <TextField
          size="small"
          name="sampleQuantityUnit"
          label={t("specWizard.qualitativeLimits.qtyUnitLabel")}
          placeholder={t("specWizard.qualitativeLimits.qtyUnitPlaceholder")}
          value={model.sampleQuantityUnit}
          onChange={(e) => set({ sampleQuantityUnit: e.target.value })}
          error={Boolean(errors.sampleQuantityUnit)}
          helperText={errors.sampleQuantityUnit}
          fullWidth
        />
      </Box>
    );
  }

  return null;
}
