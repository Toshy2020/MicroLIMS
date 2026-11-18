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
import type { TFunction } from "i18next";
import { useTranslation } from "react-i18next";
import { i18n } from "../../../../i18n";
import type { WizardStep } from "../../../../components/config/useWizard";
import { WORKFLOW_TYPES_BY_LAB, WORKFLOW_TYPE_LABELS } from "../testMaster/testMasterConstants";
import { TestFormModel, validateStep, withWorkflowType } from "./testFormModel";
import { sectionLabel, type TestWizardLookupData } from "./testHelpers";

interface BasicsViewProps {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
  errors: Record<string, string>;
  mode: "create" | "edit";
  lookup: TestWizardLookupData;
}

function BasicsStepView({ model, set, errors, mode, lookup }: BasicsViewProps) {
  const { t } = useTranslation("masterData");
  const isEditing = mode === "edit" || Boolean(model.editingId);
  const selectableSections = lookup.mySections.length > 0 ? lookup.mySections : lookup.sections;
  const workflowTypes = WORKFLOW_TYPES_BY_LAB[model.lab];
  const distinctErrors = Array.from(new Set(Object.values(errors)));

  return (
    <Stack spacing={2}>
      {distinctErrors.length > 0 && (
        <Alert severity="error" role="alert">
          {distinctErrors[0]}
        </Alert>
      )}
      <TextField
        size="small"
        name="code"
        label={t("testWizard.basics.codeLabel")}
        value={model.code}
        onChange={(e) => set({ code: e.target.value })}
        disabled={isEditing}
        required
        fullWidth
        error={Boolean(errors.code)}
        helperText={
          errors.code ||
          (isEditing
            ? t("testWizard.basics.codeImmutableHelper")
            : t("testWizard.basics.codeNewHelper"))
        }
      />

      <TextField
        size="small"
        name="displayName"
        label={t("testWizard.basics.displayNameLabel")}
        value={model.displayName}
        onChange={(e) => set({ displayName: e.target.value })}
        required
        fullWidth
        error={Boolean(errors.displayName)}
        helperText={errors.displayName || undefined}
      />

      <TextField
        size="small"
        name="coaDisplayOrder"
        label={t("testWizard.basics.coaOrderLabel")}
        placeholder={t("testWizard.basics.coaOrderPlaceholder")}
        value={model.coaDisplayOrder}
        onChange={(e) => set({ coaDisplayOrder: e.target.value })}
        fullWidth
        error={Boolean(errors.coaDisplayOrder)}
        helperText={
          errors.coaDisplayOrder ||
          t("testWizard.basics.coaOrderHelper")
        }
      />

      <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
        <FormControl
          size="small"
          sx={{ flex: 1, minWidth: 200 }}
          required
          error={Boolean(errors.sectionId)}
        >
          <InputLabel id="dialog-section-label">{t("testWizard.basics.sectionLabel")}</InputLabel>
          <Select<number | "">
            labelId="dialog-section-label"
            name="sectionId"
            label={t("testWizard.basics.sectionLabel")}
            value={model.sectionId}
            onChange={(e) => set({ sectionId: e.target.value === "" ? "" : Number(e.target.value) })}
          >
            <MenuItem value="">
              <em>{t("testWizard.basics.selectSection")}</em>
            </MenuItem>
            {selectableSections.map((s) => (
              <MenuItem key={s.sectionId} value={s.sectionId}>
                {s.sectionName} ({s.sectionCode})
              </MenuItem>
            ))}
          </Select>
          {errors.sectionId && <FormHelperText>{errors.sectionId}</FormHelperText>}
          {model.editingId &&
            model.editingSectionId !== null &&
            model.sectionId !== "" &&
            model.sectionId !== model.editingSectionId && (
              <FormHelperText>{t("testWizard.basics.sectionChangeHint")}</FormHelperText>
            )}
        </FormControl>

        <FormControl size="small" sx={{ flex: 1, minWidth: 200 }}>
          <InputLabel id="dialog-workflow-type-label">{t("testWizard.basics.workflowTypeLabel")}</InputLabel>
          <Select
            labelId="dialog-workflow-type-label"
            name="workflowType"
            label={t("testWizard.basics.workflowTypeLabel")}
            value={model.workflowType}
            onChange={(e) => set(withWorkflowType(model, e.target.value))}
          >
            {workflowTypes.map((w) => (
              <MenuItem key={w} value={w}>
                {t(`testMaster.constants.workflowTypes.${w}`, { defaultValue: WORKFLOW_TYPE_LABELS[w] ?? w })}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </Stack>
    </Stack>
  );
}

export function createBasicsStep(
  getLookup: () => TestWizardLookupData,
  getT: () => TFunction = () => i18n.getFixedT("en", "masterData")
): WizardStep<TestFormModel> {
  return {
    key: "basics",
    title: getT()("testWizard.basics.title"),
    validate: (model) => validateStep("basics", model, getT()),
    render: ({ model, set, errors, mode }) => (
      <BasicsStepView
        model={model}
        set={set}
        errors={errors}
        mode={mode}
        lookup={getLookup()}
      />
    ),
    summary: (model) => {
      const t = getT();
      const lookup = getLookup();
      const selectableSections = lookup.mySections.length > 0 ? lookup.mySections : lookup.sections;
      return [
        { label: t("testWizard.basics.summary.code"), value: model.code.trim() || "—", field: "code" },
        { label: t("testWizard.basics.summary.displayName"), value: model.displayName.trim() || "—", field: "displayName" },
        { label: t("testWizard.basics.summary.coaOrder"), value: model.coaDisplayOrder.trim() || "—", field: "coaDisplayOrder" },
        { label: t("testWizard.basics.summary.section"), value: sectionLabel(selectableSections, model.sectionId, t), field: "sectionId" },
        {
          label: t("testWizard.basics.summary.workflowType"),
          value: t(`testMaster.constants.workflowTypes.${model.workflowType}`, {
            defaultValue: WORKFLOW_TYPE_LABELS[model.workflowType] ?? model.workflowType
          }),
          field: "workflowType"
        }
      ];
    }
  };
}
