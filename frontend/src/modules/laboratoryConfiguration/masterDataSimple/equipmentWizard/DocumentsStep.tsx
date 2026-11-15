import { Autocomplete, Stack, TextField, Typography } from "@mui/material";
import type { WizardStep } from "../../../../components/config/useWizard";
import { DateField } from "../../../../components/DateField";
import { assetLabel, type EquipmentWizardLookup } from "./equipmentLookup";
import type { EquipmentFormModel } from "./equipmentFormModel";

interface DocumentsViewProps {
  model: EquipmentFormModel;
  set: (patch: Partial<EquipmentFormModel>) => void;
  lookup: EquipmentWizardLookup;
}

function DocumentsStepView({ model, set, lookup }: DocumentsViewProps) {
  return (
    <Stack spacing={2}>
      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        Calibration and the asset register entry that holds this equipment's serial number and documents.
      </Typography>
      <DateField
        label="Calibration Due Date"
        value={model.calibrationDueDate}
        onChange={(v) => set({ calibrationDueDate: v })}
        size="small"
      />
      <Autocomplete
        options={lookup.inventory}
        value={lookup.inventory.find((item) => item.id === model.equipmentInventoryId) ?? null}
        onChange={(_event, newValue) => set({ equipmentInventoryId: newValue ? newValue.id : null })}
        getOptionLabel={assetLabel}
        isOptionEqualToValue={(option, value) => option.id === value.id}
        renderInput={(params) => (
          <TextField
            {...params}
            size="small"
            name="equipmentInventoryId"
            label="Asset register entry"
            placeholder="Select an asset register entry"
            fullWidth
          />
        )}
        clearOnEscape
        sx={{ "& .MuiAutocomplete-clearIndicator": { visibility: "visible" } }}
      />
    </Stack>
  );
}

export function createDocumentsStep(
  getLookup: () => EquipmentWizardLookup
): WizardStep<EquipmentFormModel> {
  return {
    key: "documents",
    title: "Calibration and documents",
    validate: () => ({}),
    render: ({ model, set }) => <DocumentsStepView model={model} set={set} lookup={getLookup()} />,
    summary: (model) => {
      const asset = getLookup().inventory.find((item) => item.id === model.equipmentInventoryId);
      return [
        { label: "Calibration Due Date", value: model.calibrationDueDate || "—", field: "calibrationDueDate" },
        {
          label: "Asset register entry",
          value: asset ? assetLabel(asset) : model.equipmentInventoryId != null ? `#${model.equipmentInventoryId}` : "—",
          field: "equipmentInventoryId"
        }
      ];
    }
  };
}
