import { Box, TextField } from "@mui/material";
import { useTranslation } from "../../../../../i18n";
import type { LimitGroupProps } from "./NumericLimits";

/**
 * Count-tiered limit fields (Alert, Action, Specification Limit).
 */
export function CountTieredLimits({ model, set, errors }: LimitGroupProps) {
  const { t } = useTranslation("labConfig");
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
        label={t("specWizard.countTieredLimits.alertLabel")}
        placeholder={t("specWizard.countTieredLimits.alertPlaceholder")}
        value={model.alertLimit}
        onChange={(e) => set({ alertLimit: e.target.value })}
        error={Boolean(errors.alertLimit)}
        helperText={errors.alertLimit}
        fullWidth
      />
      <TextField
        size="small"
        name="actionLimit"
        label={t("specWizard.countTieredLimits.actionLabel")}
        placeholder={t("specWizard.countTieredLimits.actionPlaceholder")}
        value={model.actionLimit}
        onChange={(e) => set({ actionLimit: e.target.value })}
        error={Boolean(errors.actionLimit)}
        helperText={errors.actionLimit}
        fullWidth
      />
      <TextField
        size="small"
        name="specLimit"
        label={t("specWizard.countTieredLimits.specLabel")}
        placeholder={t("specWizard.countTieredLimits.specPlaceholder")}
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
