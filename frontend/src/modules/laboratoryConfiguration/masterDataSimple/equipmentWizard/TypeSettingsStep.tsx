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
  const hasCds = CDS_TYPES.includes(model.type);
  const hasConnection = CONNECTION_TYPES.includes(model.type);
  const hasSetPoint = SET_POINT_TYPES.includes(model.type);
  const typeName = equipmentTypeLabel(model.type);

  return (
    <Stack spacing={2}>
      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        Settings for a {typeName}.
      </Typography>
      {!hasCds && !hasConnection && !hasSetPoint && (
        <Alert severity="info">A {typeName} has no type-specific settings. Continue to the next step.</Alert>
      )}
      {hasCds && (
        <FormControl fullWidth required size="small" error={Boolean(errors.cdsSoftware)}>
          <InputLabel id="cds-software-select-label">CDS Software *</InputLabel>
          <Select
            labelId="cds-software-select-label"
            label="CDS Software *"
            name="cdsSoftware"
            value={model.cdsSoftware}
            onChange={(e) => set({ cdsSoftware: e.target.value })}
          >
            <MenuItem value="">
              <em>Select CDS Software…</em>
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
          label="Connection Settings (optional)"
          placeholder="e.g. COM3 / 192.168.1.50"
          value={model.connectionSettings}
          onChange={(e) => set({ connectionSettings: e.target.value })}
          fullWidth
        />
      )}
      {hasSetPoint && (
        <TextField
          size="small"
          name="setPointTemperature"
          label="Set Point Temp (°C)"
          type="number"
          placeholder="e.g. 37.0"
          value={model.setPointTemperature}
          onChange={(e) => set({ setPointTemperature: e.target.value })}
          slotProps={{ htmlInput: { step: "0.1" } }}
          fullWidth
        />
      )}
    </Stack>
  );
}

export function createTypeSettingsStep(
  getContext: () => EquipmentModelContext
): WizardStep<EquipmentFormModel> {
  return {
    key: "typeSettings",
    title: "Type settings",
    validate: (model) => validateStep("typeSettings", model, getContext()),
    render: ({ model, set, errors }) => <TypeSettingsStepView model={model} set={set} errors={errors} />,
    summary: (model) => {
      const rows: { label: string; value: string; field: string }[] = [];
      if (CDS_TYPES.includes(model.type)) {
        rows.push({
          label: "CDS Software",
          value: CDS_SOFTWARE_OPTIONS.find((o) => o.value === model.cdsSoftware)?.label ?? "—",
          field: "cdsSoftware"
        });
      }
      if (CONNECTION_TYPES.includes(model.type)) {
        rows.push({
          label: "Connection Settings",
          value: model.connectionSettings.trim() || "—",
          field: "connectionSettings"
        });
      }
      if (SET_POINT_TYPES.includes(model.type)) {
        rows.push({
          label: "Set Point Temp (°C)",
          value: model.setPointTemperature !== "" ? model.setPointTemperature : "—",
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
