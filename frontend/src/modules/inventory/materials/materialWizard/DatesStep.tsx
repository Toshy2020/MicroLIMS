import { Box, Typography } from "@mui/material";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";

import type { WizardStep } from "../../../../components/config/useWizard";
import { DateField } from "../../../../components/DateField";
import type { MaterialItem } from "../types/materialTypes";
import { wizardText as tw } from "./wizardText";
import { StepErrorAlert } from "./StepErrorAlert";
import type { MaterialLookups } from "./materialLookups";
import { formatCalendarDate, validateStep, type MaterialFormModel, type MaterialModelContext } from "./materialFormModel";
import { deriveMaterialView } from "./materialSelections";

interface DatesStepProps {
  model: MaterialFormModel;
  set: (patch: Partial<MaterialFormModel>) => void;
  errors: Record<string, string>;
  editing: MaterialItem | null;
  lookups: MaterialLookups;
}

function DatesStepView({ model, set, errors, editing, lookups }: DatesStepProps) {
  const { t } = useTranslation("inventory");
  const view = deriveMaterialView(model, editing, lookups);
  return (
    <>
      <StepErrorAlert errors={errors} />
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
        <DateField
          required
          label={t("wizard.dates.receiving")}
          value={model.receivingDate}
          onChange={(v) => set({ receivingDate: v })}
          error={Boolean(errors.receivingDate)}
        />
        <DateField
          required={Boolean(view.selectedCategory?.expiryRequired)}
          label={t("wizard.dates.expiry")}
          value={model.expiryDate}
          onChange={(v) => set({ expiryDate: v })}
          error={Boolean(errors.expiryDate)}
        />
      </Box>
      <Typography variant="body2" sx={{ mt: 2, color: "text.secondary" }}>
        {t("wizard.dates.documentsNote")}
      </Typography>
      {editing && (
        <Box sx={{ mt: 2.5, p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: 1, borderColor: "divider" }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            <Trans i18nKey="wizard.dates.editNote" ns="inventory" components={{ b: <strong /> }} />
          </Typography>
        </Box>
      )}
    </>
  );
}

export function createDatesStep(
  getEditing: () => MaterialItem | null,
  getLookups: () => MaterialLookups,
  getContext: (model: MaterialFormModel) => MaterialModelContext
): WizardStep<MaterialFormModel> {
  return {
    key: "dates",
    title: tw("wizard.steps.dates"),
    validate: (model) => validateStep("dates", model, getContext(model)),
    render: ({ model, set, errors }) => (
      <DatesStepView model={model} set={set} errors={errors} editing={getEditing()} lookups={getLookups()} />
    ),
    // Calendar dates, not instants: there is no time zone to show.
    summary: (model) => [
      { label: tw("wizard.dates.receiving"), value: model.receivingDate ? formatCalendarDate(model.receivingDate) : "—", field: "receivingDate" },
      { label: tw("wizard.dates.expiry"), value: model.expiryDate ? formatCalendarDate(model.expiryDate) : "—", field: "expiryDate" }
    ]
  };
}
