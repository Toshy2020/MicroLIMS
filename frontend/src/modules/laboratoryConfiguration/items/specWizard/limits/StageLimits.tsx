import {
  Box,
  Button,
  FormControl,
  FormHelperText,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import { useTranslation } from "../../../../../i18n";
import type { DosageForm } from "../../../specifications/services/SpecificationService";
import type { LimitGroupProps } from "./NumericLimits";

/**
 * Multi-stage and pharmacopoeial compendial stage/dosage limit fields
 * (MultiStage, DissolutionQ, DisintegrationTime, WeightVariation).
 */
export function StageLimits({ model, set, errors }: LimitGroupProps) {
  const { t } = useTranslation("labConfig");
  const handleAddStage = () => {
    set({
      stages: [
        ...model.stages,
        {
          stageNumber: model.stages.length + 1,
          stageLabel: `Stage ${model.stages.length + 1}`,
          acceptanceCriteriaText: ""
        }
      ]
    });
  };

  const handleRemoveStage = (index: number) => {
    const next = model.stages
      .filter((_, idx) => idx !== index)
      .map((s, idx) => ({ ...s, stageNumber: idx + 1 }));
    set({ stages: next });
  };

  const handleStageChange = (
    index: number,
    field: "stageLabel" | "acceptanceCriteriaText",
    val: string
  ) => {
    set({
      stages: model.stages.map((s, idx) => (idx === index ? { ...s, [field]: val } : s))
    });
  };

  if (model.limitType === "MultiStage") {
    return (
      <Stack spacing={1.5} data-field="stages">
        {model.stages.map((stage, idx) => (
          <Stack
            key={idx}
            direction="row"
            spacing={1}
            sx={{ alignItems: "flex-start" }}
          >
            <TextField
              size="small"
              label={t("specWizard.stageLimits.stageNumLabel")}
              type="number"
              value={stage.stageNumber}
              disabled
              sx={{ width: 75 }}
            />
            <TextField
              size="small"
              label={t("specWizard.stageLimits.stageLabelLabel")}
              value={stage.stageLabel}
              placeholder={t("specWizard.stageLimits.stageLabelPlaceholder")}
              onChange={(e) => handleStageChange(idx, "stageLabel", e.target.value)}
              sx={{ width: { xs: 140, sm: 200 } }}
            />
            <TextField
              size="small"
              label={t("specWizard.stageLimits.criteriaLabel")}
              multiline
              minRows={1}
              value={stage.acceptanceCriteriaText}
              placeholder={t("specWizard.stageLimits.criteriaPlaceholder")}
              onChange={(e) => handleStageChange(idx, "acceptanceCriteriaText", e.target.value)}
              fullWidth
            />
            <IconButton
              aria-label={t("specWizard.stageLimits.removeAriaLabel", { index: idx + 1 })}
              size="small"
              color="error"
              onClick={() => handleRemoveStage(idx)}
              disabled={model.stages.length <= 1}
              title={t("specWizard.stageLimits.removeTitle")}
              sx={{ mt: 0.5 }}
            >
              <DeleteIcon fontSize="small" />
            </IconButton>
          </Stack>
        ))}
        {errors.stages && <FormHelperText error>{errors.stages}</FormHelperText>}
        <Box>
          <Button
            type="button"
            size="small"
            startIcon={<AddIcon />}
            onClick={handleAddStage}
            sx={{ textTransform: "none", fontSize: 12, fontWeight: 600 }}
          >
            {t("specWizard.stageLimits.addStage")}
          </Button>
        </Box>
      </Stack>
    );
  }

  if (model.limitType === "DissolutionQ") {
    return (
      <Box>
        <Typography variant="body2" sx={{ fontWeight: 600, mb: 1.5 }}>
          {t("specWizard.stageLimits.dissolutionHeading")}
        </Typography>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 1.5 }}>
          <TextField
            size="small"
            name="lowerLimit"
            type="number"
            label={t("specWizard.stageLimits.qLabel")}
            placeholder={t("specWizard.stageLimits.qPlaceholder")}
            value={model.lowerLimit}
            onChange={(e) => set({ lowerLimit: e.target.value })}
            error={Boolean(errors.lowerLimit)}
            slotProps={{ htmlInput: { min: 0.01, max: 100, step: "any" } }}
            helperText={errors.lowerLimit || t("specWizard.stageLimits.qHelper")}
            required
            sx={{ flex: 1 }}
          />
          <TextField
            size="small"
            name="labelClaim"
            type="number"
            label={t("specWizard.stageLimits.labelClaimLabel")}
            placeholder={t("specWizard.stageLimits.labelClaimPlaceholder")}
            value={model.labelClaim}
            onChange={(e) => set({ labelClaim: e.target.value })}
            error={Boolean(errors.labelClaim)}
            slotProps={{ htmlInput: { min: 0.0001, step: "any" } }}
            helperText={errors.labelClaim || t("specWizard.stageLimits.labelClaimHelper")}
            required
            sx={{ flex: 1 }}
          />
          <TextField
            size="small"
            label={t("specWizard.stageLimits.unitLabel")}
            value="mg"
            disabled
            helperText={t("specWizard.stageLimits.dissolutionUnitHelper")}
            sx={{ width: 120 }}
          />
        </Stack>
        {model.lowerLimit.trim() !== "" && (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {t("specWizard.stageLimits.specLimitDisplay")} <strong>Q = {model.lowerLimit.trim()} %</strong>
          </Typography>
        )}
      </Box>
    );
  }

  if (model.limitType === "DisintegrationTime") {
    return (
      <Box>
        <Typography variant="body2" sx={{ fontWeight: 600, mb: 1.5 }}>
          {t("specWizard.stageLimits.disintegrationHeading")}
        </Typography>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 1.5 }}>
          <TextField
            size="small"
            name="upperLimit"
            type="number"
            label={t("specWizard.stageLimits.timeLimitLabel")}
            placeholder={t("specWizard.stageLimits.timeLimitPlaceholder")}
            value={model.upperLimit}
            onChange={(e) => set({ upperLimit: e.target.value })}
            error={Boolean(errors.upperLimit)}
            slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
            helperText={errors.upperLimit || t("specWizard.stageLimits.timeLimitHelper")}
            required
            sx={{ flex: 1 }}
          />
          <TextField
            size="small"
            label={t("specWizard.stageLimits.unitLabel")}
            value="min"
            disabled
            helperText={t("specWizard.stageLimits.disintegrationUnitHelper")}
            sx={{ width: 120 }}
          />
        </Stack>
        {model.upperLimit.trim() !== "" && (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {t("specWizard.stageLimits.specLimitDisplay")} <strong>{t("specWizard.stageLimits.nmtMin", { value: model.upperLimit.trim() })}</strong>
          </Typography>
        )}
      </Box>
    );
  }

  if (model.limitType === "WeightVariation") {
    return (
      <Box>
        <Typography variant="body2" sx={{ fontWeight: 600, mb: 1.5 }}>
          {t("specWizard.stageLimits.weightVariationHeading")}
        </Typography>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 1.5 }}>
          <FormControl size="small" sx={{ flex: 1 }} error={Boolean(errors.dosageForm)}>
            <InputLabel id="wv-dosage-form-label">{t("specWizard.stageLimits.dosageFormLabel")}</InputLabel>
            <Select
              name="dosageForm"
              labelId="wv-dosage-form-label"
              label={t("specWizard.stageLimits.dosageFormLabel")}
              value={model.dosageForm}
              onChange={(e) => set({ dosageForm: e.target.value as DosageForm })}
            >
              <MenuItem value="Tablet">{t("specWizard.stageLimits.dosageForms.Tablet")}</MenuItem>
              <MenuItem value="HardCapsule">{t("specWizard.stageLimits.dosageForms.HardCapsule")}</MenuItem>
              <MenuItem value="SoftCapsule">{t("specWizard.stageLimits.dosageForms.SoftCapsule")}</MenuItem>
            </Select>
            {errors.dosageForm && <FormHelperText>{errors.dosageForm}</FormHelperText>}
          </FormControl>
          <TextField
            size="small"
            label={t("specWizard.stageLimits.unitLabel")}
            value="mg"
            disabled
            helperText={t("specWizard.stageLimits.weightVariationUnitHelper")}
            sx={{ width: 120 }}
          />
        </Stack>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {t("specWizard.stageLimits.specLimitDisplay")}{" "}
          <strong>
            {model.dosageForm === "Tablet"
              ? t("specWizard.stageLimits.weightVariationTablet")
              : t("specWizard.stageLimits.weightVariationCapsule")}
          </strong>
        </Typography>
      </Box>
    );
  }

  return null;
}
