import { useEffect, useState } from "react";
import { Box, Typography, Stack, Alert } from "@mui/material";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { CurrentStepResponse } from "./types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "./utils/workflowErrors";
import { useIncubationCountdown } from "./hooks/useIncubationCountdown";
import { BrothStepPanel } from "./pathogenSteps/BrothStepPanel";
import { SelectivePlatingPanel } from "./pathogenSteps/SelectivePlatingPanel";
import { UnsupportedStepPanel } from "./pathogenSteps/UnsupportedStepPanel";
import { InconclusiveTerminalPanel } from "./pathogenSteps/InconclusiveTerminalPanel";
import { ConfirmatoryPlatingPanel } from "./pathogenSteps/ConfirmatoryPlatingPanel";
// Task 8 adds this import and switch branch:
// import { BiochemicalTestPanel } from "./pathogenSteps/BiochemicalTestPanel";

interface Props { testOrderId: number; testCode: string; displayName: string; }

// Verified against backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs:
// CompletedStepSummary.outcome for a ConfirmatoryPlating step is set directly to
// result.ConfirmatoryResult.ToString() (TestWorkflowEngine.cs ~line 1208), i.e. the
// outcome field is exactly "AllConforming" or "Inconclusive" with no surrounding text.
const INCONCLUSIVE_OUTCOME_MARKER = "Inconclusive";

function isInconclusiveTerminal(current: CurrentStepResponse): boolean {
  if (current.step?.stepType !== "BiochemicalTest") return false;
  const confirmatoryStep = current.completedSteps.find((s) => s.stepType === "ConfirmatoryPlating");
  return !!confirmatoryStep?.outcome?.includes(INCONCLUSIVE_OUTCOME_MARKER);
}

function StepChainStrip({ current }: { current: CurrentStepResponse }) {
  const completedByOrder = new Map(current.completedSteps.map((s) => [s.stepOrder, s]));
  const currentOrder = current.step?.stepOrder ?? null;
  return (
    <Stack direction="row" spacing={1} sx={{ mb: 2, flexWrap: "wrap" }}>
      {current.allSteps.map((s) => {
        const done = completedByOrder.get(s.stepOrder);
        const isCurrent = s.stepOrder === currentOrder;
        const isInconclusive = done?.outcome?.includes(INCONCLUSIVE_OUTCOME_MARKER);
        let bg = "#eef0f4", color = "#6b7280", border = "1px solid #d9dce3", label = s.stepName;
        if (done) {
          label = `${s.stepName}: ${done.outcome}`;
          bg = isInconclusive ? "#fdecea" : "#e8f6ec";
          color = isInconclusive ? "#b3261e" : "#1e7a34";
          border = `1px solid ${isInconclusive ? "#f3b7b2" : "#a8ddb5"}`;
        } else if (isCurrent) {
          label = `${s.stepName}: In progress`; bg = "#eaf1fd"; color = "#1a56db"; border = "1px solid #a9c6f5";
        }
        return (
          <Box key={s.stepOrder} sx={{ px: 1.25, py: 0.5, borderRadius: 999, fontSize: 12, fontWeight: 600, bgcolor: bg, color, border }}>
            {done ? (isInconclusive ? "✗ " : "✓ ") : ""}{label}
          </Box>
        );
      })}
    </Stack>
  );
}

export function PathogenStepDialog({ testOrderId }: Props) {
  const [current, setCurrent] = useState<CurrentStepResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    setError(null);
    try {
      const data = await TestWorkflowService.getCurrentStep(testOrderId);
      setCurrent(data);
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    }
  };

  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [testOrderId]);

  const countdown = useIncubationCountdown(current?.incubationLock ?? null, load);

  const handleSubmitted = () => load();

  if (error && !current) return <Alert severity="error">{error}</Alert>;
  if (!current) return <Box sx={{ py: 4 }}><LoadingSpinner /></Box>;

  if (current.allStepsComplete) {
    return (
      <Box>
        <StepChainStrip current={current} />
        <Alert severity={current.finalResult === "Detected" ? "error" : "success"}>
          Final result: <strong>{current.finalResult}</strong>
        </Alert>
      </Box>
    );
  }

  if (isInconclusiveTerminal(current)) {
    return (
      <Box>
        <StepChainStrip current={current} />
        <InconclusiveTerminalPanel />
      </Box>
    );
  }

  const step = current.step;
  if (!step) return <Alert severity="error">No current step is available for this test order.</Alert>;

  return (
    <Box>
      <StepChainStrip current={current} />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Typography sx={{ fontWeight: 700, mb: 0.5 }}>
        Step {step.stepOrder}: {step.stepName}
        {step.isFinalStep && <Typography component="span" variant="caption" color="text.secondary"> — determines the final result</Typography>}
      </Typography>
      {current.incubationLock?.isLocked && (
        <Alert severity="warning" sx={{ mb: 1.5 }}>
          Incubation in progress — {countdown.formatted} remaining. Submission unlocks automatically.
        </Alert>
      )}

      {step.stepType === "BrothEnrichment" || step.stepType === "SelectiveBroth" ? (
        <BrothStepPanel testOrderId={testOrderId} step={step} onSubmitted={handleSubmitted} />
      ) : step.stepType === "SelectivePlating" ? (
        <SelectivePlatingPanel testOrderId={testOrderId} step={step} onSubmitted={handleSubmitted} />
      ) : step.stepType === "ConfirmatoryPlating" ? (
        <ConfirmatoryPlatingPanel testOrderId={testOrderId} step={step} onSubmitted={handleSubmitted} />
      ) : step.stepType === "BiochemicalTest" ? (
        <UnsupportedStepPanel stepType={step.stepType} /> /* Task 8 replaces this branch */
      ) : (
        <UnsupportedStepPanel stepType={step.stepType} />
      )}
    </Box>
  );
}
