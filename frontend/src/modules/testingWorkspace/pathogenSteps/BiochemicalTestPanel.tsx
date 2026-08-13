import { useState } from "react";
import { Typography, TextField, Button, Stack, Alert, Divider } from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import { TestWorkflowStepDto, AnalystDecision } from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  // Outcome of the completed ConfirmatoryPlating step, exactly as reported
  // by CompletedStepSummary.outcome ("AllConforming" | "Inconclusive" | null)
  // - computed once by PathogenStepDialog and passed down rather than
  // re-derived here.
  confirmatoryOutcome: string | null;
  onSubmitted: () => void;
}

type Phase = "decision" | "form";

// GMP decision-recovery note (Task 8 scope addition):
// After an all-conforming confirmatory result, the analyst must record
// SubmitAsDetected/ProceedToBiochemical via recordAnalystDecision. That
// call is single-shot server-side (WorkflowErrorCodes.
// AnalystDecisionAlreadyRecorded) - a second attempt throws rather than
// silently succeeding. The only way this panel can be re-mounted with
// confirmatoryOutcome === "AllConforming" *and* a decision already on
// record is if ProceedToBiochemical was chosen in an earlier session
// (SubmitAsDetected finalizes the whole workflow immediately via
// FinalizeWorkflowAsync, so allStepsComplete would be true and this panel
// would never mount again). So on that specific error code, the correct
// recovery is unconditional: stop showing the decision UI and fall
// through to the biochemical form - never string-match the message.
export function BiochemicalTestPanel({ testOrderId, step, confirmatoryOutcome, onSubmitted }: Props) {
  const [phase, setPhase] = useState<Phase>(confirmatoryOutcome === "AllConforming" ? "decision" : "form");
  const [decisionError, setDecisionError] = useState<string | null>(null);
  const [text, setText] = useState("");
  const [formError, setFormError] = useState<string | null>(null);

  const decide = async (decision: AnalystDecision) => {
    setDecisionError(null);
    try {
      await TestWorkflowService.recordAnalystDecision(testOrderId, decision);
      if (decision === "SubmitAsDetected") {
        onSubmitted();
      } else {
        setPhase("form");
      }
    } catch (e) {
      const parsed = parseWorkflowError(e);
      if (parsed.code === "ANALYST_DECISION_ALREADY_RECORDED") {
        setPhase("form");
        return;
      }
      setDecisionError(workflowErrorDisplayMessage(parsed));
    }
  };

  const submit = async () => {
    setFormError(null);
    if (!text.trim()) { setFormError("Enter the biochemical confirmation result."); return; }
    try {
      await TestWorkflowService.submitBiochemical(testOrderId, step.stepName, text.trim());
      onSubmitted();
    } catch (e) {
      setFormError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    }
  };

  if (phase === "decision") {
    return (
      <Stack spacing={1.5}>
        {decisionError && <Alert severity="error">{decisionError}</Alert>}
        <Alert severity="success">
          Confirmatory result: <strong>All Conforming</strong>. An analyst decision is required before this
          workflow proceeds.
        </Alert>
        <Divider />
        <Alert severity="warning">
          Submitting as Detected without biochemical confirmation will be flagged for the reviewer.
        </Alert>
        <Stack direction="row" spacing={1.5} justifyContent="flex-end">
          <Button variant="outlined" color="warning" onClick={() => decide("SubmitAsDetected")}>
            Submit as Detected (skip biochemical)
          </Button>
          <Button variant="contained" onClick={() => decide("ProceedToBiochemical")}>
            Proceed to Biochemical Test
          </Button>
        </Stack>
      </Stack>
    );
  }

  return (
    <Stack spacing={1.5}>
      {formError && <Alert severity="error">{formError}</Alert>}
      <Typography variant="body2" color="text.secondary">
        Record the biochemical confirmation result (e.g. IMViC pattern, API strip result).
      </Typography>
      <TextField
        multiline minRows={4} label="Biochemical Result" value={text}
        onChange={(e) => setText(e.target.value)}
      />
      <Stack direction="row" justifyContent="flex-end">
        <Button variant="contained" disabled={!text.trim()} onClick={submit}>Submit</Button>
      </Stack>
    </Stack>
  );
}
