import { useState } from "react";
import { Typography, TextField, Button, Stack, Alert, Divider, ToggleButton, ToggleButtonGroup } from "@mui/material";
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

// Mirrors TestMasterPage.tsx's PHENOTYPIC_TEST_TYPE_LABELS - kept local
// rather than shared since it's the only other place this enum is
// displayed, not worth a new shared module for one lookup.
const PHENOTYPIC_TEST_TYPE_LABELS: Record<string, string> = {
  Gram: "Gram Stain", Catalase: "Catalase", Oxidase: "Oxidase", Coagulase: "Coagulase",
  Antibiogram: "Antibiogram", IdentificationKit: "Identification Kit"
};

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
  // Never inferred from the free-text result - an earlier version of this
  // workflow always finalized as Detected regardless of what the result
  // text said, which produced a real false-positive pathogen result once
  // this step became reachable after an Inconclusive confirmatory reading.
  const [organismDetected, setOrganismDetected] = useState<boolean | null>(null);

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
    if (organismDetected === null) { setFormError("Select whether the biochemical result indicates the organism was Detected or Not Detected."); return; }
    try {
      await TestWorkflowService.submitBiochemical(testOrderId, step.stepName, text.trim(), organismDetected);
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
      {confirmatoryOutcome === "Inconclusive" && (
        <Alert severity="warning">
          Confirmatory plating was <strong>Inconclusive</strong> and has been flagged for investigation in the
          audit trail. Biochemical identification can still resolve it — record the result below.
        </Alert>
      )}
      {(() => {
        const types = step.phenotypicTestTypes?.length ? step.phenotypicTestTypes : (step.phenotypicTestType ? [step.phenotypicTestType] : []);
        if (types.length === 0) return null;
        return (
          <Alert severity="info">
            This step covers: <strong>{types.map((t) => PHENOTYPIC_TEST_TYPE_LABELS[t] ?? t).join(", ")}</strong>.
            Record the combined result for all of them below.
          </Alert>
        );
      })()}
      <Typography variant="body2" color="text.secondary">
        Record the biochemical confirmation result (e.g. IMViC pattern, API strip result).
      </Typography>
      <TextField
        multiline minRows={4} label="Biochemical Result" value={text}
        onChange={(e) => setText(e.target.value)}
      />
      <Typography variant="body2" sx={{ fontWeight: 600 }}>
        Based on this result, is the organism Detected or Not Detected? This decision — not the free-text
        result alone — determines the final workflow result.
      </Typography>
      <ToggleButtonGroup
        exclusive
        value={organismDetected === null ? null : organismDetected ? "detected" : "not-detected"}
        onChange={(_, value) => { if (value !== null) setOrganismDetected(value === "detected"); }}
      >
        <ToggleButton value="not-detected" color="success">Not Detected</ToggleButton>
        <ToggleButton value="detected" color="error">Detected</ToggleButton>
      </ToggleButtonGroup>
      <Stack direction="row" justifyContent="flex-end">
        <Button variant="contained" disabled={!text.trim() || organismDetected === null} onClick={submit}>Submit</Button>
      </Stack>
    </Stack>
  );
}
