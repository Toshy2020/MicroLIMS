import { useState } from "react";
import { Button, Stack, Alert, CircularProgress } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { TestWorkflowService } from "../services/TestWorkflowService";
import { TestWorkflowStepDto, CurrentStepResponse } from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";
import { IncubationWaitingView } from "./IncubationWaitingView";

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  current: CurrentStepResponse;
  onSubmitted: (saved?: boolean) => void;
}

// BrothEnrichment/SelectiveBroth: waiting for the incubation window. Counts
// down from the minimum incubation time (e.g. 18 h of an 18-72 h window) to
// zero; at zero the next step becomes available.
export function BrothWaitingPanel({ testOrderId, step, current, onSubmitted }: Props) {
  const { t } = useTranslation("testingWorkspace");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const nextStep = (current?.allSteps ?? []).find((s) => s.stepOrder > step.stepOrder);

  const submitCompletion = async () => {
    setError(null);
    setSubmitting(true);
    try {
      await TestWorkflowService.submitBroth(testOrderId, step.stepName, null);
      onSubmitted();
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Stack spacing={2}>
      {error && <Alert severity="error">{error}</Alert>}
      <IncubationWaitingView
        testOrderId={testOrderId}
        step={step}
        current={current}
        onSubmitted={onSubmitted}
        nextLabel={nextStep ? nextStep.stepName : t("pathogenSteps.waiting.theReading")}
        renderAction={(ready) => (
          <Button
            variant="contained"
            onClick={submitCompletion}
            disabled={!ready || submitting}
            startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
          >
            {submitting
              ? t("pathogenSteps.waiting.submitting")
              : nextStep
              ? t("pathogenSteps.waiting.proceedToStep", { stepName: nextStep.stepName })
              : t("pathogenSteps.waiting.proceedToReading")}
          </Button>
        )}
      />
    </Stack>
  );
}
