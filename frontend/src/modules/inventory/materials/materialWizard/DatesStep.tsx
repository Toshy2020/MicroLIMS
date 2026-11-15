import { Box, Typography } from "@mui/material";
import type { WizardStep } from "../../../../components/config/useWizard";
import { DateField } from "../../../../components/DateField";
import type { MaterialItem } from "../types/materialTypes";
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
  const view = deriveMaterialView(model, editing, lookups);
  return (
    <>
      <StepErrorAlert errors={errors} />
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
        <DateField
          required
          label="Receiving Date"
          value={model.receivingDate}
          onChange={(v) => set({ receivingDate: v })}
          error={Boolean(errors.receivingDate)}
        />
        <DateField
          required={Boolean(view.selectedCategory?.expiryRequired)}
          label="Expiry Date"
          value={model.expiryDate}
          onChange={(v) => set({ expiryDate: v })}
          error={Boolean(errors.expiryDate)}
        />
      </Box>
      <Typography variant="body2" sx={{ mt: 2, color: "text.secondary" }}>
        Certificates and other lot documents are uploaded from the lot&apos;s details and documents once the lot is in stock.
      </Typography>
      {editing && (
        <Box sx={{ mt: 2.5, p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: 1, borderColor: "divider" }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            <strong>Note:</strong> Changing Quantity Received adjusts Quantity Remaining by the difference (a receiving correction);
            it preserves consumption already recorded by Media Preparation or Cryovials.
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
    title: "Dates",
    validate: (model) => validateStep("dates", model, getContext(model)),
    render: ({ model, set, errors }) => (
      <DatesStepView model={model} set={set} errors={errors} editing={getEditing()} lookups={getLookups()} />
    ),
    // Calendar dates, not instants: there is no time zone to show.
    summary: (model) => [
      { label: "Receiving Date", value: model.receivingDate ? formatCalendarDate(model.receivingDate) : "—", field: "receivingDate" },
      { label: "Expiry Date", value: model.expiryDate ? formatCalendarDate(model.expiryDate) : "—", field: "expiryDate" }
    ]
  };
}
