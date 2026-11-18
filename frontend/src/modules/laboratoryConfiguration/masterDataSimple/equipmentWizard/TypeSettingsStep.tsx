import type { TFunction } from "i18next";
import {
  Alert,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import type { WizardStep } from "../../../../components/config/useWizard";
import { i18n, useTranslation } from "../../../../i18n";
import {
  CDS_SOFTWARE_OPTIONS,
  CDS_TYPES,
  CONNECTION_TYPES,
  SET_POINT_TYPES,
  equipmentTypeLabel
} from "./equipmentOptions";
import { EquipmentFormModel, EquipmentModelContext, validateStep } from "./equipmentFormModel";

interface TypeSettingsViewProps {
  model: EquipmentFormModel;
  set: (patch: Partial<EquipmentFormModel>) => void;
  errors: Record<string, string>;
}

function TypeSettingsStepView({ model, set, errors }: TypeSettingsViewProps) {
  const { t } = useTranslation("masterData");
  const hasCds = CDS_TYPES.includes(model.type);
  const hasConnection = CONNECTION_TYPES.includes(model.type);
  const hasSetPoint = SET_POINT_TYPES.includes(model.type);
  const typeName = t(`equipmentTypes.${model.type}`, { defaultValue: equipmentTypeLabel(model.type) });

  return (
    <Stack spacing={2}>
      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        {t("equipmentWizard.typeSettings.intro", { type: typeName })}
      </Typography>
      {!hasCds && !hasConnection && !hasSetPoint && (
        <Alert severity="info">{t("equipmentWizard.typeSettings.noSettings", { type: typeName })}</Alert>
      )}
      {hasCds && (
        <FormControl fullWidth required size="small" error={Boolean(errors.cdsSoftware)}>
          <InputLabel id="cds-software-select-label">{t("equipmentWizard.typeSettings.cdsLabel")}</InputLabel>
          <Select
            labelId="cds-software-select-label"
            label={t("equipmentWizard.typeSettings.cdsLabel")}
            name="cdsSoftware"
            value={model.cdsSoftware}
            onChange={(e) => set({ cdsSoftware: e.target.value })}
          >
            <MenuItem value="">
              <em>{t("equipmentWizard.typeSettings.cdsPlaceholder")}</em>
            </MenuItem>
            {CDS_SOFTWARE_OPTIONS.map((opt) => (
              <MenuItem key={opt.value} value={opt.value}>
                {opt.label}
              </MenuItem>
            ))}
          </Select>
          {errors.cdsSoftware && <FormHelperText>{errors.cdsSoftware}</FormHelperText>}
        </FormControl>
      )}
      {hasConnection && (
        <TextField
          size="small"
          name="connectionSettings"
          label={t("equipmentWizard.typeSettings.connectionLabel")}
          placeholder={t("equipmentWizard.typeSettings.connectionPlaceholder")}
          value={model.connectionSettings}
          onChange={(e) => set({ connectionSettings: e.target.value })}
          fullWidth
        />
      )}
      {hasSetPoint && (
        <TextField
          size="small"
          name="setPointTemperature"
          label={t("equipmentWizard.typeSettings.setPointLabel")}
          type="number"
          placeholder={t("equipmentWizard.typeSettings.setPointPlaceholder")}
          value={model.setPointTemperature}
          onChange={(e) => set({ setPointTemperature: e.target.value })}
          slotProps={{ htmlInput: { step: "0.1" } }}
          fullWidth
        />
      )}
    </Stack>
  );
}

const defaultT = (): TFunction => i18n.getFixedT("en", "masterData");

export function createTypeSettingsStep(
  getContext: () => EquipmentModelContext,
  getT: () => TFunction = defaultT
): WizardStep<EquipmentFormModel> {
  return {
    key: "typeSettings",
    title: getT()("equipmentWizard.typeSettings.stepTitle"),
    validate: (model) => validateStep("typeSettings", model, getContext(), getT()),
    render: ({ model, set, errors }) => <TypeSettingsStepView model={model} set={set} errors={errors} />,
    summary: (model) => {
      const t = getT();
      const rows: { label: string; value: string; field: string }[] = [];
      if (CDS_TYPES.includes(model.type)) {
        rows.push({
          label: t("equipmentWizard.typeSettings.summary.cdsSoftware"),
          value: CDS_SOFTWARE_OPTIONS.find((o) => o.value === model.cdsSoftware)?.label ?? t("common.dash"),
          field: "cdsSoftware"
        });
      }
      if (CONNECTION_TYPES.includes(model.type)) {
        rows.push({
          label: t("equipmentWizard.typeSettings.summary.connectionSettings"),
          value: model.connectionSettings.trim() || t("common.dash"),
          field: "connectionSettings"
        });
      }
      if (SET_POINT_TYPES.includes(model.type)) {
        rows.push({
          label: t("equipmentWizard.typeSettings.summary.setPointTemperature"),
          value: model.setPointTemperature !== "" ? model.setPointTemperature : t("common.dash"),
          field: "setPointTemperature"
        });
      }
      if (rows.length === 0) {
        rows.push({ label: "Type settings", value: "None for this type", field: "type" });
      }
      return rows;
    }
  };
}
