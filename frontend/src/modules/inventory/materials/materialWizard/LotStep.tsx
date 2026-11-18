import { Box, FormControl, InputLabel, MenuItem, Select, TextField } from "@mui/material";
import { useTranslation } from "../../../../i18n";
import type { WizardStep } from "../../../../components/config/useWizard";
import { LoadFailuresAlert } from "../../../../components/LoadErrorAlert";
import { MATERIAL_UNITS, type MaterialItem, type MaterialUnit } from "../types/materialTypes";
import { wizardText as tw } from "./wizardText";
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
  const { t } = useTranslation("inventory");
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
          label={t("wizard.lot.batchLot")}
          placeholder={t("wizard.lot.batchExample")}
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
          label={t("wizard.lot.quantity")}
          value={model.quantityReceived}
          onChange={(e) => set({ quantityReceived: e.target.value })}
          error={Boolean(errors.quantityReceived)}
          slotProps={{ htmlInput: { min: 0, step: "any" } }}
        />
        <FormControl size="small" fullWidth required>
          <InputLabel id="material-unit-label">{t("wizard.lot.unit")}</InputLabel>
          <Select
            labelId="material-unit-label"
            label={t("wizard.lot.unit")}
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
          label={t("wizard.lot.minLevelOptional")}
          placeholder={t("wizard.lot.minExample")}
          value={model.minimumStockLevel}
          onChange={(e) => set({ minimumStockLevel: e.target.value })}
          slotProps={{ htmlInput: { min: 0, step: "any" } }}
        />
        <TextField
          size="small"
          name="storageCondition"
          label={t("wizard.lot.storageCondition")}
          placeholder={t("wizard.lot.storageExample")}
          value={model.storageCondition}
          onChange={(e) => set({ storageCondition: e.target.value })}
          helperText={
            view.selectedCategory?.defaultStorageCondition ? t("wizard.lot.prefilled") : undefined
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
    title: tw("wizard.steps.lot"),
    validate: (model) => validateStep("lot", model, getContext(model)),
    render: ({ model, set, errors }) => (
      <LotStepView model={model} set={set} errors={errors} editing={getEditing()} lookups={getLookups()} />
    ),
    summary: (model) => {
      const quantity = model.quantityReceived === "" ? "—" : `${model.quantityReceived} ${model.unit}`;
      return [
        { label: tw("wizard.lot.batchLot"), value: model.batchNumber.trim() || "—", field: "batchNumber" },
        { label: tw("wizard.lot.storageLocation"), value: model.location.trim() || "—", field: "location" },
        { label: tw("wizard.lot.quantity"), value: quantity, field: "quantityReceived" },
        {
          label: tw("wizard.lot.minLevel"),
          value: model.minimumStockLevel === "" ? "—" : `${model.minimumStockLevel} ${model.unit}`,
          field: "minimumStockLevel"
        },
        { label: tw("wizard.lot.storageCondition"), value: model.storageCondition.trim() || "—", field: "storageCondition" }
      ];
    }
  };
}
