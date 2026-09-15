import { useEffect, useState } from "react";
import { Box, Typography, Alert, Button, useTheme } from "@mui/material";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { CurrentStepResponse } from "./types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "./utils/workflowErrors";
import { BrothStepPanel } from "./pathogenSteps/BrothStepPanel";
import { BrothWaitingPanel } from "./pathogenSteps/BrothWaitingPanel";
import { SelectivePlatingPanel } from "./pathogenSteps/SelectivePlatingPanel";
import { UnsupportedStepPanel } from "./pathogenSteps/UnsupportedStepPanel";
import { ConfirmatoryPlatingPanel } from "./pathogenSteps/ConfirmatoryPlatingPanel";
import { BiochemicalTestPanel } from "./pathogenSteps/BiochemicalTestPanel";
import { StepChainStrip } from "./components/StepChainStrip";

interface Props { testOrderId: number; testCode: string; displayName: string; onClose?: () => void; }

// Shared lookup so the Inconclusive-terminal check and the BiochemicalTest
// panel's confirmatoryOutcome prop can never drift apart - both read the
// same completedSteps entry.
function getConfirmatoryOutcome(current: CurrentStepResponse): string | null {
  const confirmatoryStep = current.completedSteps.find((s) => s.stepType === "ConfirmatoryPlating");
  return confirmatoryStep?.outcome ?? null;
}

export function PathogenStepDialog({ testOrderId, onClose }: Props) {
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

  useEffect(() => { load(); }, [testOrderId]);

  const handleSubmitted = () => {
    load();
  };

  if (!current) {
    return (
      <Box sx={{ py: 4 }}>
        {error ? <Alert severity="error">{error}</Alert> : <LoadingSpinner />}
      </Box>
    );
  }

  if (current.allStepsComplete) {
    return (
      <Box>
        <StepChainStrip current={current} />
        {current.finalResult ? (
          <Box sx={{ backgroundColor: theme.custom.status.notDetected.bg, border: "1px solid", borderColor: theme.custom.status.notDetected.border, borderRadius: 1, p: 2, mt: 1, mb: 2 }}>
            <Typography variant="body2" sx={{ fontWeight: 600, color: theme.custom.status.notDetected.text }}>
              ✓ Final result: {current.finalResult}
            </Typography>
          </Box>
        ) : (
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>—</Typography>
        )}
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 2 }}>
          <Box />
          {onClose && (
            <Button variant="contained" onClick={onClose} sx={{ fontWeight: 600, textTransform: "none" }}>
              Done / Close
            </Button>
          )}
        </Box>
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
        {step.isFinalStep && <Typography component="span" variant="caption" sx={{
          color: "text.secondary"
        }}> — determines the final result</Typography>}
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
