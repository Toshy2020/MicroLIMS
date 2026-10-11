import { useEffect, useState } from "react";
import { Box, Typography, Alert, Button, Collapse, Link, useTheme } from "@mui/material";
import { DialogBodySkeleton } from "../../components/DialogBodySkeleton";
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
import { serverIncubationWindow } from "./utils/incubationWindow";

interface Props { testOrderId: number; testCode: string; displayName: string; onClose?: () => void; }

// Shared lookup so the Inconclusive-terminal check and the BiochemicalTest
// panel's confirmatoryOutcome prop can never drift apart - both read the
// same completedSteps entry.
function getConfirmatoryOutcome(current: CurrentStepResponse): string | null {
  const confirmatoryStep = current.completedSteps.find((s) => s.stepType === "ConfirmatoryPlating");
  return confirmatoryStep?.outcome ?? null;
}

// One-line explanation per step type, behind "What happens in this step?".
const STEP_HELP: Record<string, string> = {
  BrothEnrichment: "This step is preparation only - it is not a pass/fail result. The workflow proceeds to the next step once the assigned incubation window completes.",
  SelectiveBroth: "This step is preparation only - it is not a pass/fail result. The workflow proceeds to the next step once the assigned incubation window completes.",
  SelectivePlating: "Plate onto the selective medium and start incubation. Once the minimum incubation has passed, read the plate and record what grew against the expected appearance.",
  ConfirmatoryPlating: "Plate onto each confirmatory medium the method permits and start incubation. Once the minimum incubation has passed, record an observation for every plate.",
  BiochemicalTest: "Record the biochemical identification result and whether it shows the target organism. Your Detected / Not Detected decision sets the final result."
};

const chipSx = { px: 1, py: 0.25, borderRadius: 999, fontSize: 11.5, fontWeight: 600, border: "1px solid", lineHeight: 1.5 } as const;
const toneSx = (tone: { bg: string; text: string; border: string }) => ({ bgcolor: tone.bg, color: tone.text, borderColor: tone.border });

export function PathogenStepDialog({ testOrderId, onClose }: Props) {
  const theme = useTheme();
  const [current, setCurrent] = useState<CurrentStepResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showStepHelp, setShowStepHelp] = useState(false);

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
        {error ? <Alert severity="error">{error}</Alert> : <DialogBodySkeleton blocks={3} blockHeight={56} />}
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

  const isBrothStep = step.stepType === "BrothEnrichment" || step.stepType === "SelectiveBroth";
  const isPlatingStep = step.stepType === "SelectivePlating" || step.stepType === "ConfirmatoryPlating";
  const isIncubating = current.incubationLock != null
    || (current.previousSteps ?? []).some((p) => p.stepName === step.stepName && p.status === "Incubating");
  const { isTimeReady } = serverIncubationWindow(step, current);

  const status = theme.custom.status;
  const stepChip: { label: string; tone: { bg: string; text: string; border: string } } | null =
    isBrothStep ? (isIncubating ? { label: "Incubating", tone: status.purple } : { label: "Preparation · no pass/fail", tone: status.info })
    : isPlatingStep ? (!isIncubating ? { label: "Set up", tone: status.info } : isTimeReady ? { label: "Ready to read", tone: status.notDetected } : { label: "Incubating", tone: status.purple })
    : step.stepType === "BiochemicalTest" ? { label: "Result entry", tone: status.info }
    : null;
  const stepHelp = STEP_HELP[step.stepType];

  return (
    <Box>
      <StepChainStrip current={current} />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Box sx={{ display: "flex", alignItems: "center", flexWrap: "wrap", columnGap: 1, rowGap: 0.5, mb: stepHelp ? 0.5 : 1.5 }}>
        <Typography component="h3" sx={{ fontWeight: 700, fontSize: 16 }}>{step.stepName}</Typography>
        {stepChip && (
          <Box component="span" sx={{ ...chipSx, ...toneSx(stepChip.tone) }}>{stepChip.label}</Box>
        )}
        {step.isFinalStep && <Typography component="span" variant="caption" sx={{
          color: "text.secondary"
        }}>(determines the final result)</Typography>}
      </Box>
      {stepHelp && (
        <Box sx={{ mb: 2 }}>
          <Link
            component="button"
            type="button"
            variant="caption"
            underline="hover"
            aria-expanded={showStepHelp}
            onClick={() => setShowStepHelp((v) => !v)}
          >
            {showStepHelp ? "Hide step details" : "What happens in this step?"}
          </Link>
          <Collapse in={showStepHelp} unmountOnExit>
            <Typography variant="caption" component="p" sx={{ color: "text.secondary", mt: 0.5, mb: 0 }}>
              {stepHelp}
            </Typography>
          </Collapse>
        </Box>
      )}

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
