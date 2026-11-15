import { Box, Stack, TextField } from "@mui/material";
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
  const flags = resolveSpecMethodFlags(model);
  const unitLocked =
    flags.isRelatedSubstances ||
    flags.isResidualSolvents ||
    flags.isIcpElementalImpurities ||
    model.limitType === "DisintegrationTime" ||
    model.limitType === "WeightVariation";

  const unitHelperText = flags.isResidualSolvents
    ? "Fixed unit for residual solvents"
    : flags.isIcpElementalImpurities
    ? "Fixed unit for elemental impurities"
    : model.limitType === "WeightVariation"
    ? "Fixed unit for weight variation"
    : model.limitType === "DisintegrationTime"
    ? "Fixed unit for disintegration time"
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
          label="Unit"
          value={effectiveUnitValue(model)}
          onChange={(e) => set({ unit: e.target.value })}
          disabled={unitLocked}
          error={Boolean(errors.unit)}
          helperText={errors.unit || unitHelperText}
          placeholder="e.g. % w/w, CFU/g, pH units"
          fullWidth
        />
        <TextField
          size="small"
          name="referenceStandard"
          label="Reference Standard"
          value={model.referenceStandard}
          onChange={(e) => set({ referenceStandard: e.target.value })}
          error={Boolean(errors.referenceStandard)}
          helperText={errors.referenceStandard}
          placeholder="e.g. USP <711>, EP 2.2.29, STP-VITC-01"
          fullWidth
        />
      </Box>

      {showDilution && (
        <TextField
          size="small"
          name="dilutionFactor"
          label="Dilution Factor"
          type="number"
          value={model.dilutionFactor}
          onChange={(e) => set({ dilutionFactor: e.target.value })}
          disabled={model.limitType !== "CountTiered"}
          error={Boolean(errors.dilutionFactor)}
          helperText={
            errors.dilutionFactor ||
            "DILUTION FACTOR: editable only for Count-Tiered parameters"
          }
          placeholder={model.limitType === "CountTiered" ? "e.g. 10" : "\u2014"}
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
export function createUnitDilutionStep(): WizardStep<SpecFormModel> {
  return {
    key: "unit",
    title: "Unit & Reference",
    validate: (model) => validateStep("unit", model),
    render: (ctx) => <UnitDilutionStepView {...ctx} />,
    summary: (model) => {
      const rows = [
        { field: "unit", label: "Unit", value: effectiveUnitValue(model).trim() },
        {
          field: "referenceStandard",
          label: "Reference standard",
          value: model.referenceStandard.trim()
        }
      ];
      if (model.limitType === "CountTiered") {
        rows.push({
          field: "dilutionFactor",
          label: "Dilution factor",
          value: model.dilutionFactor.trim() ? `\u00D7${model.dilutionFactor.trim()}` : ""
        });
      }
      return rows;
    }
  };
}
