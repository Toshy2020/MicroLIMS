import { useEffect, useState } from "react";
import { Box, Typography, Stack, Alert, useTheme } from "@mui/material";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { CurrentStepResponse } from "./types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "./utils/workflowErrors";
import { BrothStepPanel } from "./pathogenSteps/BrothStepPanel";
import { BrothWaitingPanel } from "./pathogenSteps/BrothWaitingPanel";
import { SelectivePlatingPanel } from "./pathogenSteps/SelectivePlatingPanel";
import { UnsupportedStepPanel } from "./pathogenSteps/UnsupportedStepPanel";
import { InconclusiveTerminalPanel } from "./pathogenSteps/InconclusiveTerminalPanel";
import { ConfirmatoryPlatingPanel } from "./pathogenSteps/ConfirmatoryPlatingPanel";
import { BiochemicalTestPanel } from "./pathogenSteps/BiochemicalTestPanel";

interface Props { testOrderId: number; testCode: string; displayName: string; }

// Verified against backend/MicroLIMS.Application/Workflows/TestWorkflowEngine.cs:
// CompletedStepSummary.outcome for a ConfirmatoryPlating step is set directly to
// result.ConfirmatoryResult.ToString() (TestWorkflowEngine.cs ~line 1208), i.e. the
// outcome field is exactly "AllConforming" or "Inconclusive" with no surrounding text.
const INCONCLUSIVE_OUTCOME_MARKER = "Inconclusive";

// Shared lookup so the Inconclusive-terminal check and the BiochemicalTest
// panel's confirmatoryOutcome prop can never drift apart - both read the
// same completedSteps entry.
function getConfirmatoryOutcome(current: CurrentStepResponse): string | null {
  const confirmatoryStep = current.completedSteps.find((s) => s.stepType === "ConfirmatoryPlating");
  return confirmatoryStep?.outcome ?? null;
}

function isInconclusiveTerminal(current: CurrentStepResponse): boolean {
  return !!getConfirmatoryOutcome(current)?.includes(INCONCLUSIVE_OUTCOME_MARKER);
}

function StepChainStrip({ current }: { current: CurrentStepResponse }) {
  const theme = useTheme();
  const completedByOrder = new Map(current.completedSteps.map((s) => [s.stepOrder, s]));
  const currentOrder = current.step?.stepOrder ?? null;
  return (
    <Stack direction="row" spacing={1} sx={{ mb: 2, flexWrap: "wrap" }}>
      {current.allSteps.map((s) => {
        const done = completedByOrder.get(s.stepOrder);
        const isCurrent = s.stepOrder === currentOrder;
        const isInconclusive = done?.outcome?.includes(INCONCLUSIVE_OUTCOME_MARKER);
        let tone = theme.custom.status.pending, label = s.stepName;
        if (done) {
          label = `${s.stepName}: ${done.outcome}`;
          tone = isInconclusive ? theme.custom.status.detected : theme.custom.status.notDetected;
        } else if (isCurrent) {
          label = `${s.stepName}: In progress`; tone = theme.custom.status.info;
        }
        return (
          <Box key={s.stepOrder} sx={{ px: 1.25, py: 0.5, borderRadius: 999, fontSize: 12, fontWeight: 600, bgcolor: tone.bg, color: tone.text, border: `1px solid ${tone.border}` }}>
            {done ? (isInconclusive ? "✗ " : "✓ ") : ""}{label}
          </Box>
        );
      })}
    </Stack>
  );
}

export function PathogenStepDialog({ testOrderId }: Props) {
  const theme = useTheme();
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

  const handleSubmitted = () => load();

  if (error && !current) return <Alert severity="error">{error}</Alert>;
  if (!current) return <Box sx={{ py: 4 }}><LoadingSpinner /></Box>;

  if (isInconclusiveTerminal(current)) {
    return (
      <Box>
        <StepChainStrip current={current} />
        <Box sx={{ backgroundColor: theme.custom.status.action.bg, border: "1px solid", borderColor: theme.custom.status.action.border, borderRadius: 1, p: 2, mt: 1 }}>
          <Typography variant="body2" sx={{ fontWeight: 600, color: theme.custom.status.action.text, display: "flex", alignItems: "center", gap: 1 }}>
            ⚠ Confirmatory Plating: Inconclusive
          </Typography>
          <Typography variant="caption" sx={{ color: theme.custom.status.action.text, display: "block", mt: 0.5 }}>
            Media disagreement recorded. This result has been flagged for reviewer decision. No further analyst action required.
          </Typography>
        </Box>
      </Box>
    );
  }

  if (current.allStepsComplete) {
    return (
      <Box>
        <StepChainStrip current={current} />
        {current.finalResult ? (
          <Box sx={{ backgroundColor: theme.custom.status.notDetected.bg, border: "1px solid", borderColor: theme.custom.status.notDetected.border, borderRadius: 1, p: 2, mt: 1 }}>
            <Typography variant="body2" sx={{ fontWeight: 600, color: theme.custom.status.notDetected.text }}>
              ✓ Final result: {current.finalResult}
            </Typography>
          </Box>
        ) : (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>—</Typography>
        )}
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

      {step.stepType === "BrothEnrichment" || step.stepType === "SelectiveBroth" ? (
        current.incubationLock != null ? (
          <BrothWaitingPanel 
            testOrderId={testOrderId} 
            step={step} 
            current={current}
            onSubmitted={handleSubmitted} 
          />
        ) : (
          <BrothStepPanel testOrderId={testOrderId} step={step} current={current} onSubmitted={handleSubmitted} />
        )
      ) : step.stepType === "SelectivePlating" ? (
        <SelectivePlatingPanel testOrderId={testOrderId} step={step} current={current} onSubmitted={handleSubmitted} />
      ) : step.stepType === "ConfirmatoryPlating" ? (
        <ConfirmatoryPlatingPanel testOrderId={testOrderId} step={step} current={current} onSubmitted={handleSubmitted} />
      ) : step.stepType === "BiochemicalTest" ? (
        <BiochemicalTestPanel
          testOrderId={testOrderId} step={step}
          confirmatoryOutcome={getConfirmatoryOutcome(current)}
          onSubmitted={handleSubmitted}
        />
      ) : (
        <UnsupportedStepPanel stepType={step.stepType} />
      )}
    </Box>
  );
}
