import { Alert, Stack, Typography } from "@mui/material";
import type { TFunction } from "i18next";
import { useTranslation } from "react-i18next";
import { i18n } from "../../../../i18n";
import type { WizardStep } from "../../../../components/config/useWizard";
import type { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";
import { WORKFLOW_TYPES_BY_LAB } from "../testMaster/testMasterConstants";
import { WorkflowStepsSection } from "../testMaster/WorkflowStepsSection";
import { TestFormModel, validateStep, withWorkflowType } from "./testFormModel";

interface StepsViewProps {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
  errors: Record<string, string>;
}

function StepsStepView({ model, set, errors }: StepsViewProps) {
  const { t } = useTranslation("masterData");
  const syntheticTest: TestDefinitionOption = {
    id: model.editingId ?? 0,
    version: model.editingTest?.version,
    code: model.code.trim() || "NEW",
    displayName: model.displayName.trim() || t("testWizard.steps.newTestFallback"),
    isActive: true,
    workflowType: model.workflowType,
    equationType: model.equationType
  };

  return (
    <Stack spacing={2}>
      {errors.steps && (
        <Alert severity="error" role="alert">
          {errors.steps}
        </Alert>
      )}
      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        {t("testWizard.steps.description")}
      </Typography>
      <WorkflowStepsSection
        test={syntheticTest}
        workflowTypes={WORKFLOW_TYPES_BY_LAB[model.lab]}
        onWorkflowTypeChanged={() => {}}
        draftSteps={model.steps}
        onDraftStepsChange={(steps) => set({ steps })}
        onDraftWorkflowTypeChange={(wf) => set(withWorkflowType(model, wf))}
      />
    </Stack>
  );
}

export function createStepsStep(
  getT: () => TFunction = () => i18n.getFixedT("en", "masterData")
): WizardStep<TestFormModel> {
  return {
    key: "steps",
    title: getT()("testWizard.steps.title"),
    applies: (model) =>
      model.workflowType === "CountTest" || model.workflowType === "Observation",
    validate: (model) => validateStep("steps", model, getT()),
    render: ({ model, set, errors }) => (
      <StepsStepView model={model} set={set} errors={errors} />
    ),
    summary: (model) => {
      const t = getT();
      return [
        {
          label: t("testWizard.steps.summary.label"),
          value:
            model.steps.length > 0
              ? model.steps
                  .slice()
                  .sort((a, b) => a.stepOrder - b.stepOrder)
                  .map((s) => `${s.stepOrder}. ${s.stepName} (${s.stepType})`)
                  .join(", ")
              : t("testWizard.steps.summary.none"),
          field: "steps"
        }
      ];
    }
  };
}
