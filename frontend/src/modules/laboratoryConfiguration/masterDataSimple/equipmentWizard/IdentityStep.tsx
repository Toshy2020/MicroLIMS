import {
  Alert,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField
} from "@mui/material";
import type { WizardStep } from "../../../../components/config/useWizard";
import { FieldHint, hintDescribedBy } from "../../../../components/FieldHint";
import { hints } from "../hints";
import { MICRO_EQUIPMENT_TYPES, SET_POINT_TYPES, CONNECTION_TYPES, equipmentTypeLabel } from "./equipmentOptions";
import type { EquipmentWizardLookup } from "./equipmentLookup";
import { EquipmentFormModel, EquipmentModelContext, validateStep, withEquipmentType } from "./equipmentFormModel";

interface IdentityViewProps {
  model: EquipmentFormModel;
  set: (patch: Partial<EquipmentFormModel>) => void;
  errors: Record<string, string>;
  mode: "create" | "edit";
  lookup: EquipmentWizardLookup;
}

// Changing the type also drops settings the new type does not have, so a hidden
// value is never saved.
function changeType(model: EquipmentFormModel, nextType: string): EquipmentFormModel {
  const next = withEquipmentType(model, nextType);
  return {
    ...next,
    setPointTemperature: SET_POINT_TYPES.includes(nextType) ? next.setPointTemperature : "",
    connectionSettings: CONNECTION_TYPES.includes(nextType) ? next.connectionSettings : ""
  };
}

function IdentityStepView({ model, set, errors, mode, lookup }: IdentityViewProps) {
  const showSection = mode !== "edit" && lookup.mySections.length > 1;
  const nameMissing = Boolean(errors.required) && !model.name.trim();
  const codeMissing = Boolean(errors.required) && !model.code.trim();
  const typeOptions = MICRO_EQUIPMENT_TYPES.some((t) => t.value === model.type)
    ? MICRO_EQUIPMENT_TYPES
    : [...MICRO_EQUIPMENT_TYPES, { value: model.type, label: equipmentTypeLabel(model.type) }];

  return (
    <Stack spacing={2}>
      {errors.required && (
        <Alert severity="error" role="alert">
          {errors.required}
        </Alert>
      )}
      <TextField
        size="small"
        name="name"
        label="Equipment Name *"
        placeholder="e.g. Incubator 01"
        value={model.name}
        onChange={(e) => set({ name: e.target.value })}
        fullWidth
        required
        error={nameMissing}
      />
      <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start" }}>
        <TextField
          size="small"
          name="code"
          label="Equipment Code *"
          placeholder="e.g. INC-001"
          value={model.code}
          onChange={(e) => set({ code: e.target.value })}
          fullWidth
          required
          error={codeMissing}
          slotProps={{ input: { "aria-describedby": hintDescribedBy("equipment-code") } }}
        />
        <FieldHint id="equipment-code" label="Equipment Code" hint={hints.equipmentCode} />
      </Stack>
      <FormControl fullWidth required size="small">
        <InputLabel id="equipment-type-select-label">Equipment Type *</InputLabel>
        <Select
          labelId="equipment-type-select-label"
          label="Equipment Type *"
          name="type"
          value={model.type}
          onChange={(e) => set(changeType(model, e.target.value))}
        >
          {typeOptions.map((t) => (
            <MenuItem key={t.value} value={t.value}>
              {t.label}
            </MenuItem>
          ))}
        </Select>
      </FormControl>
      <TextField
        size="small"
        name="vendor"
        label="Vendor / Manufacturer"
        placeholder="e.g. Binder, Memmert"
        value={model.vendor}
        onChange={(e) => set({ vendor: e.target.value })}
        fullWidth
        slotProps={{ htmlInput: { maxLength: 100 } }}
      />
      {showSection && (
        <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start" }}>
          <FormControl size="small" fullWidth required sx={{ flex: 1 }} error={Boolean(errors.section)}>
            <InputLabel id="equipment-section-label">Laboratory Section *</InputLabel>
            <Select<number | "">
              labelId="equipment-section-label"
              label="Laboratory Section *"
              name="section"
              value={model.sectionId}
              onChange={(e) => set({ sectionId: e.target.value === "" ? "" : Number(e.target.value) })}
              inputProps={{ "aria-label": "Laboratory Section", "aria-describedby": hintDescribedBy("equipment-section") }}
            >
              <MenuItem value="">
                <em>Select Laboratory Section…</em>
              </MenuItem>
              {lookup.mySections.map((s) => (
                <MenuItem key={s.sectionId} value={s.sectionId}>
                  {s.sectionName} ({s.departmentName})
                </MenuItem>
              ))}
            </Select>
            {errors.section && <FormHelperText>{errors.section}</FormHelperText>}
          </FormControl>
          <FieldHint id="equipment-section" label="Laboratory Section" hint={hints.laboratorySection} />
        </Stack>
      )}
      <TextField
        size="small"
        name="location"
        label="Location"
        placeholder="e.g. Microbiology Lab, Room 101"
        value={model.location}
        onChange={(e) => set({ location: e.target.value })}
        fullWidth
      />
    </Stack>
  );
}

export function createIdentityStep(
  getLookup: () => EquipmentWizardLookup,
  getContext: () => EquipmentModelContext
): WizardStep<EquipmentFormModel> {
  return {
    key: "identity",
    title: "Identity",
    validate: (model) => validateStep("identity", model, getContext()),
    render: ({ model, set, errors, mode }) => (
      <IdentityStepView model={model} set={set} errors={errors} mode={mode} lookup={getLookup()} />
    ),
    summary: (model) => {
      const lookup = getLookup();
      const section = lookup.mySections.find((s) => s.sectionId === model.sectionId);
      const rows = [
        { label: "Name", value: model.name.trim() || "—", field: "name" },
        { label: "Code", value: model.code.trim() || "—", field: "code" },
        { label: "Type", value: equipmentTypeLabel(model.type), field: "type" },
        { label: "Vendor / Manufacturer", value: model.vendor.trim() || "—", field: "vendor" }
      ];
      if (lookup.mySections.length > 1) {
        rows.push({ label: "Laboratory Section", value: section?.sectionName ?? "—", field: "section" });
      }
      rows.push({ label: "Location", value: model.location.trim() || "—", field: "location" });
      return rows;
    }
  };
}
