import type { TFunction } from "i18next";
import { Autocomplete, Stack, TextField, Typography } from "@mui/material";
import type { WizardStep } from "../../../../components/config/useWizard";
import { DateField } from "../../../../components/DateField";
import { i18n, useTranslation } from "../../../../i18n";
import { assetLabel, type EquipmentWizardLookup } from "./equipmentLookup";
import type { EquipmentFormModel } from "./equipmentFormModel";

interface DocumentsViewProps {
  model: EquipmentFormModel;
  set: (patch: Partial<EquipmentFormModel>) => void;
  lookup: EquipmentWizardLookup;
}

function DocumentsStepView({ model, set, lookup }: DocumentsViewProps) {
  const { t } = useTranslation("masterData");
  return (
    <Stack spacing={2}>
      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        {t("equipmentWizard.documents.intro")}
      </Typography>
      <DateField
        label={t("equipmentWizard.documents.calibrationDueLabel")}
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
            label={t("equipmentWizard.documents.assetLabel")}
            placeholder={t("equipmentWizard.documents.assetPlaceholder")}
            fullWidth
          />
        )}
        clearOnEscape
        sx={{ "& .MuiAutocomplete-clearIndicator": { visibility: "visible" } }}
      />
    </Stack>
  );
}

const defaultT = (): TFunction => i18n.getFixedT("en", "masterData");

export function createDocumentsStep(
  getLookup: () => EquipmentWizardLookup,
  getT: () => TFunction = defaultT
): WizardStep<EquipmentFormModel> {
  return {
    key: "documents",
    title: getT()("equipmentWizard.documents.stepTitle"),
    validate: () => ({}),
    render: ({ model, set }) => <DocumentsStepView model={model} set={set} lookup={getLookup()} />,
    summary: (model) => {
      const t = getT();
      const asset = getLookup().inventory.find((item) => item.id === model.equipmentInventoryId);
      return [
        { label: t("equipmentWizard.documents.calibrationDueLabel"), value: model.calibrationDueDate || t("common.dash"), field: "calibrationDueDate" },
        {
          label: t("equipmentWizard.documents.assetLabel"),
          value: asset ? assetLabel(asset) : model.equipmentInventoryId != null ? `#${model.equipmentInventoryId}` : t("common.dash"),
          field: "equipmentInventoryId"
        }
      ];
    }
  };
}
