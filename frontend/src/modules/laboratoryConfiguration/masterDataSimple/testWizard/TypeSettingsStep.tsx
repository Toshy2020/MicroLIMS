import {
  Alert,
  Box,
  FormControl,
  FormControlLabel,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Switch,
  TextField,
  Typography
} from "@mui/material";
import type { TFunction } from "i18next";
import { useTranslation } from "react-i18next";
import { i18n } from "../../../../i18n";
import type { WizardStep } from "../../../../components/config/useWizard";
import { TestAreaField } from "../TestAreaField";
import {
  TitrationChangeReasonField,
  hasTitrationSettingsChanged
} from "../TitrationChangeReasonField";
import { EQUATION_TYPES, EQUATION_TYPE_LABELS } from "../testMaster/testMasterConstants";
import { TestFormModel, validateStep, withEquationType } from "./testFormModel";
import { hplcMethodLabel, type TestWizardLookupData } from "./testHelpers";
import { DisintegrationSettingsBlock } from "./typeSettings/DisintegrationSettingsBlock";
import { DissolutionSettingsBlock } from "./typeSettings/DissolutionSettingsBlock";
import { GravimetricSettingsBlock } from "./typeSettings/GravimetricSettingsBlock";
import { IcpSettingsBlock } from "./typeSettings/IcpSettingsBlock";
import { MeasurementSettingsBlock } from "./typeSettings/MeasurementSettingsBlock";
import { TitrationSettingsBlock } from "./typeSettings/TitrationSettingsBlock";
import { UvSettingsBlock } from "./typeSettings/UvSettingsBlock";
import { WeightVariationSettingsBlock } from "./typeSettings/WeightVariationSettingsBlock";

interface TypeSettingsViewProps {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
  errors: Record<string, string>;
  lookup: TestWizardLookupData;
}

const OTHER_WORKFLOW_EQUATIONS = new Set([
  "Measurement",
  "GravimetricLoss",
  "GravimetricResidue",
  "Qualitative",
  "Dissolution",
  "Disintegration",
  "WeightVariation",
  "Titration",
  "HplcMethodAssay",
  "IcpMethodAssay",
  "UvSpectrophotometry"
]);

function allowedEquationsFor(workflowType: string): string[] {
  if (workflowType === "Gravimetric") {
    return EQUATION_TYPES.filter((eq) => eq === "GravimetricLoss" || eq === "GravimetricResidue");
  }
  if (OTHER_WORKFLOW_EQUATIONS.has(workflowType)) {
    return EQUATION_TYPES.filter((eq) => eq === workflowType);
  }
  return EQUATION_TYPES.filter((eq) => !OTHER_WORKFLOW_EQUATIONS.has(eq));
}

function TypeSettingsStepView({ model, set, errors, lookup }: TypeSettingsViewProps) {
  const { t } = useTranslation("masterData");
  const isFp = model.lab === "fp";
  const isDissolutionMethod = model.workflowType === "Dissolution";
  const firstError = Object.values(errors)[0];

  return (
    <Stack spacing={2}>
      {firstError && (
        <Alert severity="error" role="alert">
          {firstError}
        </Alert>
      )}

      {isFp && (
        <FormControl size="small" fullWidth error={Boolean(errors.equationType)}>
          <InputLabel id="dialog-equation-type-label">{t("testWizard.typeSettings.equationTypeLabel")}</InputLabel>
          <Select
            labelId="dialog-equation-type-label"
            name="equationType"
            label={t("testWizard.typeSettings.equationTypeLabel")}
            value={model.equationType}
            onChange={(e) => set(withEquationType(model, e.target.value))}
          >
            {allowedEquationsFor(model.workflowType).map((eq) => (
              <MenuItem key={eq} value={eq}>
                {t(`testMaster.constants.equationTypes.${eq}`, { defaultValue: EQUATION_TYPE_LABELS[eq] ?? eq })}
              </MenuItem>
            ))}
          </Select>
          {errors.equationType && <FormHelperText>{errors.equationType}</FormHelperText>}
        </FormControl>
      )}

      {(model.workflowType === "HplcMethodAssay" || model.workflowType === "Dissolution") && (
        <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
            {isDissolutionMethod ? t("testWizard.typeSettings.hplcHeaderDissolution") : t("testWizard.typeSettings.hplcHeaderAssay")}
          </Typography>
          <Stack spacing={2}>
            <FormControl size="small" fullWidth required={!isDissolutionMethod} error={Boolean(errors.hplcMethodId)}>
              <InputLabel id="dialog-hplc-method-label">{isDissolutionMethod ? t("testWizard.typeSettings.hplcMethodLabel") : t("testWizard.typeSettings.hplcMethodRequiredLabel")}</InputLabel>
              <Select<number | "">
                labelId="dialog-hplc-method-label"
                name="hplcMethodId"
                label={isDissolutionMethod ? t("testWizard.typeSettings.hplcMethodLabel") : t("testWizard.typeSettings.hplcMethodRequiredLabel")}
                value={model.hplcMethodId}
                onChange={(e) => {
                  const val = e.target.value === "" ? "" : Number(e.target.value);
                  const chosen = lookup.hplcMethods.find((m) => m.id === val);
                  set({
                    hplcMethodId: val,
                    methodAbbreviation:
                      chosen && model.workflowType === "HplcMethodAssay"
                        ? chosen.abbreviation
                        : model.methodAbbreviation
                  });
                }}
                inputProps={{ "aria-label": t("testWizard.typeSettings.hplcMethodLabel") }}
              >
                <MenuItem value="">
                  <em>{t("testWizard.typeSettings.selectHplcMethod")}</em>
                </MenuItem>
                {lookup.hplcMethods
                  .filter((m) => m.isActive || m.id === model.hplcMethodId)
                  .map((m) => (
                    <MenuItem key={m.id} value={m.id}>
                      {m.name} ({m.abbreviation}){!m.isActive ? t("testWizard.typeSettings.inactiveSuffix") : ""}
                    </MenuItem>
                  ))}
              </Select>
              <FormHelperText>
                {errors.hplcMethodId ||
                  t("testWizard.typeSettings.hplcMethodHelper")}
              </FormHelperText>
            </FormControl>

            <TextField
              size="small"
              label={t("testWizard.typeSettings.methodAbbreviationLabel")}
              value={model.methodAbbreviation}
              slotProps={{ input: { readOnly: true } }}
              helperText={
                isDissolutionMethod
                  ? t("testWizard.typeSettings.methodAbbreviationHelperDissolution")
                  : t("testWizard.typeSettings.methodAbbreviationHelperAssay")
              }
              fullWidth
            />
          </Stack>
        </Box>
      )}

      {model.workflowType === "IcpMethodAssay" && <IcpSettingsBlock model={model} set={set} />}
      {model.workflowType === "Measurement" && (
        <MeasurementSettingsBlock model={model} set={set} errors={errors} />
      )}
      {model.workflowType === "Gravimetric" && (
        <GravimetricSettingsBlock model={model} set={set} errors={errors} />
      )}
      {model.workflowType === "Titration" && <TitrationSettingsBlock model={model} set={set} />}
      {model.workflowType === "UvSpectrophotometry" && <UvSettingsBlock model={model} set={set} />}
      {model.workflowType === "Dissolution" && (
        <DissolutionSettingsBlock model={model} set={set} errors={errors} />
      )}
      {model.workflowType === "Disintegration" && (
        <DisintegrationSettingsBlock model={model} set={set} errors={errors} />
      )}
      {model.workflowType === "WeightVariation" && (
        <WeightVariationSettingsBlock model={model} set={set} errors={errors} />
      )}

      {model.workflowType === "Qualitative" && (
        <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 600, fontSize: 13, color: "text.secondary", mb: 1.5 }}>
            {t("testWizard.typeSettings.qualitativeInfo")}
          </Typography>
          <FormControlLabel
            control={
              <Switch
                checked={model.perContainer}
                onChange={(e) => set({ perContainer: e.target.checked })}
              />
            }
            label={t("testWizard.typeSettings.recordedPerContainer")}
          />
        </Box>
      )}

      {isFp && model.area && (
        <TestAreaField
          pageArea={model.area}
          value={model.physchemArea}
          onChange={(nextArea) => set({ physchemArea: nextArea })}
        />
      )}

      {model.editingId &&
        hasTitrationSettingsChanged(
          model.editingTest,
          model.titration,
          model.workflowType,
          isFp ? model.physchemArea : null,
          model.moistureRole
        ) && (
          <TitrationChangeReasonField
            value={model.changeReason}
            onChange={(nextReason) => set({ changeReason: nextReason })}
            error={errors.changeReason}
          />
        )}
    </Stack>
  );
}

export function createTypeSettingsStep(
  getLookup: () => TestWizardLookupData,
  getT: () => TFunction = () => i18n.getFixedT("en", "masterData")
): WizardStep<TestFormModel> {
  return {
    key: "typeSettings",
    title: getT()("testWizard.typeSettings.title"),
    applies: (model) => model.lab === "fp",
    validate: (model) => validateStep("typeSettings", model, getT()),
    render: ({ model, set, errors }) => (
      <TypeSettingsStepView model={model} set={set} errors={errors} lookup={getLookup()} />
    ),
    summary: (model) => {
      const t = getT();
      const lookup = getLookup();
      const rows = [
        {
          label: t("testWizard.typeSettings.summary.equationType"),
          value: t(`testMaster.constants.equationTypes.${model.equationType}`, {
            defaultValue: EQUATION_TYPE_LABELS[model.equationType] ?? model.equationType
          }),
          field: "equationType"
        }
      ];
      if (model.workflowType === "HplcMethodAssay" || model.workflowType === "Dissolution") {
        rows.push({
          label: t("testWizard.typeSettings.summary.hplcMethod"),
          value: hplcMethodLabel(lookup.hplcMethods, model.hplcMethodId, t),
          field: "hplcMethodId"
        });
      }
      if (
        model.workflowType === "Measurement" ||
        model.workflowType === "Gravimetric" ||
        model.workflowType === "UvSpectrophotometry"
      ) {
        rows.push({ label: t("testWizard.typeSettings.summary.replicateCount"), value: model.replicateCount || "—", field: "replicateCount" });
      }
      if (model.workflowType === "Titration") {
        rows.push({ label: t("testWizard.typeSettings.summary.titrationType"), value: model.titration.type || "—", field: "titration" });
        rows.push({ label: t("testWizard.typeSettings.summary.replicateCount"), value: model.titration.replicateCount || "—", field: "titration" });
      }
      if (model.workflowType === "IcpMethodAssay") {
        rows.push({
          label: t("testWizard.typeSettings.summary.icpElements"),
          value: model.icp.elements.filter((e) => e.symbol.trim()).map((e) => e.symbol.trim()).join(", ") || "—",
          field: "icp"
        });
      }
      if (model.physchemArea) {
        rows.push({ label: t("testWizard.typeSettings.summary.area"), value: model.physchemArea, field: "physchemArea" });
      }
      return rows;
    }
  };
}
