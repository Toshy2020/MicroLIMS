import { Alert, Stack, Typography } from "@mui/material";
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
  const syntheticTest: TestDefinitionOption = {
    id: model.editingId ?? 0,
    version: model.editingTest?.version,
    code: model.code.trim() || "NEW",
    displayName: model.displayName.trim() || "New Test",
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
        Configure the ordered workflow steps and permitted culture media for this microbiology test. You can add steps now or after creating the test.
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

export function createStepsStep(): WizardStep<TestFormModel> {
  return {
    key: "steps",
    title: "Steps",
    applies: (model) =>
      model.workflowType === "CountTest" || model.workflowType === "Observation",
    validate: (model) => validateStep("steps", model),
    render: ({ model, set, errors }) => (
      <StepsStepView model={model} set={set} errors={errors} />
    ),
    summary: (model) => [
      {
        label: "Workflow steps",
        value:
          model.steps.length > 0
            ? model.steps
                .slice()
                .sort((a, b) => a.stepOrder - b.stepOrder)
                .map((s) => `${s.stepOrder}. ${s.stepName} (${s.stepType})`)
                .join(", ")
            : "None configured yet",
        field: "steps"
      }
    ]
  };
}
