import { Box, FormControl, InputLabel, MenuItem, Select, TextField } from "@mui/material";
import type { WizardStep } from "../../../../components/config/useWizard";
import { LoadFailuresAlert } from "../../../../components/LoadErrorAlert";
import { MATERIAL_UNITS, type MaterialItem, type MaterialUnit } from "../types/materialTypes";
import { StepErrorAlert } from "./StepErrorAlert";
import { StorageLocationField } from "./StorageLocationField";
import type { MaterialLookups } from "./materialLookups";
import { validateStep, type MaterialFormModel, type MaterialModelContext } from "./materialFormModel";
import { deriveMaterialView } from "./materialSelections";
import { storageChoices } from "./materialStorage";

interface LotStepProps {
  model: MaterialFormModel;
  set: (patch: Partial<MaterialFormModel>) => void;
  errors: Record<string, string>;
  editing: MaterialItem | null;
  lookups: MaterialLookups;
}

function activeSectionOf(model: MaterialFormModel, editing: MaterialItem | null): number | undefined {
  if (editing) return editing.sectionId;
  return model.sectionId !== "" ? Number(model.sectionId) : undefined;
}

function LotStepView({ model, set, errors, editing, lookups }: LotStepProps) {
  const choices = storageChoices(lookups.equipment, lookups.sections, activeSectionOf(model, editing), model.location);
  const view = deriveMaterialView(model, editing, lookups);

  return (
    <>
      <LoadFailuresAlert failed={lookups.failed} sx={{ mb: 2.5 }} />
      <StepErrorAlert errors={errors} />
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" }, gap: 2 }}>
        <TextField
          size="small"
          required
          name="batchNumber"
          label="Batch / Lot No."
          placeholder="e.g. 3458921"
          value={model.batchNumber}
          onChange={(e) => set({ batchNumber: e.target.value })}
          error={Boolean(errors.batchNumber)}
        />
        <StorageLocationField
          location={model.location}
          onChange={(location) => set({ location })}
          choices={choices}
          loading={lookups.equipmentLoading}
          error={Boolean(errors.location)}
        />
        <TextField
          size="small"
          required
          type="number"
          name="quantityReceived"
          label="Quantity Received"
          value={model.quantityReceived}
          onChange={(e) => set({ quantityReceived: e.target.value })}
          error={Boolean(errors.quantityReceived)}
          slotProps={{ htmlInput: { min: 0, step: "any" } }}
        />
        <FormControl size="small" fullWidth required>
          <InputLabel id="material-unit-label">Unit</InputLabel>
          <Select
            labelId="material-unit-label"
            label="Unit"
            name="unit"
            value={model.unit}
            onChange={(e) => set({ unit: e.target.value as MaterialUnit })}
          >
            {MATERIAL_UNITS.map((u) => (
              <MenuItem key={u} value={u}>
                {u}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <TextField
          size="small"
          type="number"
          name="minimumStockLevel"
          label="Min. Stock Level (optional)"
          placeholder="e.g. 500"
          value={model.minimumStockLevel}
          onChange={(e) => set({ minimumStockLevel: e.target.value })}
          slotProps={{ htmlInput: { min: 0, step: "any" } }}
        />
        <TextField
          size="small"
          name="storageCondition"
          label="Storage Condition"
          placeholder="e.g. 2-8 °C, protect from light"
          value={model.storageCondition}
          onChange={(e) => set({ storageCondition: e.target.value })}
          helperText={
            view.selectedCategory?.defaultStorageCondition ? "Prefilled from the category; change it if this lot differs." : undefined
          }
          sx={{ gridColumn: { xs: "1", sm: "1 / -1" } }}
        />
      </Box>
    </>
  );
}

export function createLotStep(
  getEditing: () => MaterialItem | null,
  getLookups: () => MaterialLookups,
  getContext: (model: MaterialFormModel) => MaterialModelContext
): WizardStep<MaterialFormModel> {
  return {
    key: "lot",
    title: "Batch and storage",
    validate: (model) => validateStep("lot", model, getContext(model)),
    render: ({ model, set, errors }) => (
      <LotStepView model={model} set={set} errors={errors} editing={getEditing()} lookups={getLookups()} />
    ),
    summary: (model) => {
      const quantity = model.quantityReceived === "" ? "—" : `${model.quantityReceived} ${model.unit}`;
      return [
        { label: "Batch / Lot No.", value: model.batchNumber.trim() || "—", field: "batchNumber" },
        { label: "Storage Location", value: model.location.trim() || "—", field: "location" },
        { label: "Quantity Received", value: quantity, field: "quantityReceived" },
        {
          label: "Min. Stock Level",
          value: model.minimumStockLevel === "" ? "—" : `${model.minimumStockLevel} ${model.unit}`,
          field: "minimumStockLevel"
        },
        { label: "Storage Condition", value: model.storageCondition.trim() || "—", field: "storageCondition" }
      ];
    }
  };
}
