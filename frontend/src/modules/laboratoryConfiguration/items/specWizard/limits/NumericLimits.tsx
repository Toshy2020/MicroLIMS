import {
  Box,
  Checkbox,
  FormControl,
  FormControlLabel,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import { useTranslation } from "../../../../../i18n";
import type { ToleranceMode } from "../../../specifications/services/SpecificationService";
import type { SpecFormModel, SpecMethodFlags } from "../specFormModel";
import { getToleranceModeOptions } from "../specHelpers";

export interface LimitGroupProps {
  model: SpecFormModel;
  set: (patch: Partial<SpecFormModel>) => void;
  errors: Record<string, string>;
  flags: SpecMethodFlags;
}

/**
 * Numeric limit fields for Range, NotMoreThan, NotLessThan, and TargetWithTolerance.
 */
export function NumericLimits({ model, set, errors, flags }: LimitGroupProps) {
  const { t } = useTranslation("labConfig");
  const toleranceModeOptions = getToleranceModeOptions(t);
  const nmtLabel = flags.isRelatedSubstances
    ? t("specWizard.numericLimits.nmtPercent")
    : flags.isResidualSolvents
    ? t("specWizard.numericLimits.nmtPpm")
    : flags.isIcpElementalImpurities
    ? t("specWizard.numericLimits.nmtPpm")
    : t("specWizard.numericLimits.upperLimitLabel");

  if (model.limitType === "Range") {
    return (
      <Stack spacing={1.5}>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
          <TextField
            size="small"
            name="lowerLimit"
            label={t("specWizard.numericLimits.lowerLimitLabel")}
            type="number"
            value={model.lowerLimit}
            onChange={(e) => set({ lowerLimit: e.target.value })}
            error={Boolean(errors.lowerLimit)}
            helperText={errors.lowerLimit}
            slotProps={{ htmlInput: { step: "any" } }}
            fullWidth
          />
          <TextField
            size="small"
            name="upperLimit"
            label={t("specWizard.numericLimits.upperLimitLabel")}
            type="number"
            value={model.upperLimit}
            onChange={(e) => set({ upperLimit: e.target.value })}
            error={Boolean(errors.upperLimit)}
            helperText={errors.upperLimit}
            slotProps={{ htmlInput: { step: "any" } }}
            fullWidth
          />
        </Box>
        <Stack direction="row" spacing={3} sx={{ alignItems: "center", mt: 0.5 }}>
          <FormControlLabel
            control={
              <Checkbox
                size="small"
                checked={model.lowerInclusive}
                onChange={(e) => set({ lowerInclusive: e.target.checked })}
              />
            }
            label={<Typography variant="body2">{t("specWizard.numericLimits.lowerInclusive")}</Typography>}
          />
          <FormControlLabel
            control={
              <Checkbox
                size="small"
                checked={model.upperInclusive}
                onChange={(e) => set({ upperInclusive: e.target.checked })}
              />
            }
            label={<Typography variant="body2">{t("specWizard.numericLimits.upperInclusive")}</Typography>}
          />
        </Stack>
      </Stack>
    );
  }

  if (model.limitType === "NotMoreThan") {
    return (
      <Box sx={{ maxWidth: 300 }}>
        <TextField
          size="small"
          name="upperLimit"
          label={nmtLabel}
          type="number"
          value={model.upperLimit}
          onChange={(e) => set({ upperLimit: e.target.value })}
          error={Boolean(errors.upperLimit)}
          helperText={errors.upperLimit}
          slotProps={{ htmlInput: { step: "any" } }}
          fullWidth
        />
      </Box>
    );
  }

  if (model.limitType === "NotLessThan") {
    return (
      <Box sx={{ maxWidth: 300 }}>
        <TextField
          size="small"
          name="lowerLimit"
          label={t("specWizard.numericLimits.lowerLimitLabel")}
          type="number"
          value={model.lowerLimit}
          onChange={(e) => set({ lowerLimit: e.target.value })}
          error={Boolean(errors.lowerLimit)}
          helperText={errors.lowerLimit}
          slotProps={{ htmlInput: { step: "any" } }}
          fullWidth
        />
      </Box>
    );
  }

  if (model.limitType === "TargetWithTolerance") {
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
          name="target"
          label={t("specWizard.numericLimits.targetLabel")}
          type="number"
          value={model.target}
          onChange={(e) => set({ target: e.target.value })}
          error={Boolean(errors.target)}
          helperText={errors.target}
          slotProps={{ htmlInput: { step: "any" } }}
          fullWidth
        />
        <TextField
          size="small"
          name="tolerance"
          label={t("specWizard.numericLimits.toleranceLabel")}
          type="number"
          value={model.tolerance}
          onChange={(e) => set({ tolerance: e.target.value })}
          error={Boolean(errors.tolerance)}
          helperText={errors.tolerance}
          slotProps={{ htmlInput: { step: "any", min: "0" } }}
          fullWidth
        />
        <FormControl size="small" fullWidth error={Boolean(errors.toleranceMode)}>
          <InputLabel id="tolerance-mode-label">{t("specWizard.numericLimits.modeLabel")}</InputLabel>
          <Select
            name="toleranceMode"
            labelId="tolerance-mode-label"
            label={t("specWizard.numericLimits.modeLabel")}
            value={model.toleranceMode}
            onChange={(e) => set({ toleranceMode: e.target.value as ToleranceMode })}
          >
            {toleranceModeOptions.map((opt) => (
              <MenuItem key={opt.value} value={opt.value}>
                {opt.label}
              </MenuItem>
            ))}
          </Select>
          {errors.toleranceMode && <FormHelperText>{errors.toleranceMode}</FormHelperText>}
        </FormControl>
      </Box>
    );
  }

  return null;
}
