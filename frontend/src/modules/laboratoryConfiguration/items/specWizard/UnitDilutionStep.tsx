import { Box, Stack, TextField } from "@mui/material";
import type { TFunction } from "i18next";
import { i18n, useTranslation } from "../../../../i18n";
import type { WizardStep } from "../../../../components/config/useWizard";
import { resolveSpecMethodFlags, validateStep, type SpecFormModel } from "./specFormModel";

function effectiveUnitValue(model: SpecFormModel): string {
  const flags = resolveSpecMethodFlags(model);
  if (flags.isRelatedSubstances) return "%";
  if (flags.isResidualSolvents || flags.isIcpElementalImpurities) return "ppm";
  if (model.limitType === "WeightVariation") return "mg";
  if (model.limitType === "DisintegrationTime") return "min";
  return model.unit;
}

function UnitDilutionStepView({
  model,
  set,
  errors
}: {
  model: SpecFormModel;
  set: (patch: Partial<SpecFormModel>) => void;
  errors: Record<string, string>;
}) {
  const { t } = useTranslation("labConfig");
  const flags = resolveSpecMethodFlags(model);
  const unitLocked =
    flags.isRelatedSubstances ||
    flags.isResidualSolvents ||
    flags.isIcpElementalImpurities ||
    model.limitType === "DisintegrationTime" ||
    model.limitType === "WeightVariation";

  const unitHelperText = flags.isResidualSolvents
    ? t("specWizard.unitStep.fixedResidual")
    : flags.isIcpElementalImpurities
    ? t("specWizard.unitStep.fixedIcp")
    : model.limitType === "WeightVariation"
    ? t("specWizard.unitStep.fixedWeightVariation")
    : model.limitType === "DisintegrationTime"
    ? t("specWizard.unitStep.fixedDisintegration")
    : undefined;

  const showDilution =
    !flags.isHplcMethodAssay &&
    !flags.isIcpMethodAssay &&
    !flags.isTitration &&
    !flags.isUv &&
    model.limitType !== "WeightVariation";

  return (
    <Stack spacing={2.5}>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
        <TextField
          size="small"
          name="unit"
          label={t("specWizard.unitStep.unitLabel")}
          value={effectiveUnitValue(model)}
          onChange={(e) => set({ unit: e.target.value })}
          disabled={unitLocked}
          error={Boolean(errors.unit)}
          helperText={errors.unit || unitHelperText}
          placeholder={t("specWizard.unitStep.unitPlaceholder")}
          fullWidth
        />
        <TextField
          size="small"
          name="referenceStandard"
          label={t("specWizard.unitStep.referenceLabel")}
          value={model.referenceStandard}
          onChange={(e) => set({ referenceStandard: e.target.value })}
          error={Boolean(errors.referenceStandard)}
          helperText={errors.referenceStandard}
          placeholder={t("specWizard.unitStep.referencePlaceholder")}
          fullWidth
        />
      </Box>

      {showDilution && (
        <TextField
          size="small"
          name="dilutionFactor"
          label={t("specWizard.unitStep.dilutionLabel")}
          type="number"
          value={model.dilutionFactor}
          onChange={(e) => set({ dilutionFactor: e.target.value })}
          disabled={model.limitType !== "CountTiered"}
          error={Boolean(errors.dilutionFactor)}
          helperText={
            errors.dilutionFactor ||
            t("specWizard.unitStep.dilutionHelper")
          }
          placeholder={model.limitType === "CountTiered" ? t("specWizard.unitStep.dilutionPlaceholder") : "\u2014"}
          slotProps={{ htmlInput: { step: "1", min: "1" } }}
          fullWidth
        />
      )}
    </Stack>
  );
}

/**
 * Builds the Unit & Reference (and Dilution Factor for Count-Tiered) wizard step.
 */
export function createUnitDilutionStep(
  t: TFunction<"labConfig"> = i18n.getFixedT("en", "labConfig")
): WizardStep<SpecFormModel> {
  return {
    key: "unit",
    title: t("specWizard.unitStep.title"),
    validate: (model) => validateStep("unit", model, i18n.getFixedT(i18n.language, "labConfig")),
    render: (ctx) => <UnitDilutionStepView {...ctx} />,
    summary: (model) => {
      const rows = [
        { field: "unit", label: t("specWizard.unitStep.summary.unit"), value: effectiveUnitValue(model).trim() },
        {
          field: "referenceStandard",
          label: t("specWizard.unitStep.summary.referenceStandard"),
          value: model.referenceStandard.trim()
        }
      ];
      if (model.limitType === "CountTiered") {
        rows.push({
          field: "dilutionFactor",
          label: t("specWizard.unitStep.summary.dilutionFactor"),
          value: model.dilutionFactor.trim() ? `\u00D7${model.dilutionFactor.trim()}` : ""
        });
      }
      return rows;
    }
  };
}
