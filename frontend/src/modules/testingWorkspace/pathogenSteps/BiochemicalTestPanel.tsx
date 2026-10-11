import { useState } from "react";
import { Typography, TextField, Button, Stack, Alert } from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import { TestWorkflowStepDto, AnalystDecision } from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";
import { StepActionBar, FixedMediumRow, ObservationOptions } from "./StepLayout";

type DetectionChoice = "not-detected" | "detected";
const DETECTION_OPTIONS: { value: DetectionChoice; title: string; detail: string }[] = [
  { value: "not-detected", title: "Not Detected", detail: "The biochemical result does not identify the target organism" },
  { value: "detected", title: "Detected", detail: "The biochemical result identifies the target organism" }
];

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
      <Stack spacing={2}>
        {decisionError && <Alert severity="error">{decisionError}</Alert>}
        <Alert severity="success">
          Confirmatory result: <strong>All Conforming</strong>. An analyst decision is required before this
          workflow proceeds.
        </Alert>
        <Alert severity="warning">
          Submitting as Detected without biochemical confirmation will be flagged for the reviewer.
        </Alert>
        <StepActionBar>
          <Button variant="outlined" color="warning" onClick={() => decide("SubmitAsDetected")}>
            Submit as Detected (skip biochemical)
          </Button>
          <Button variant="contained" onClick={() => decide("ProceedToBiochemical")}>
            Proceed to Biochemical Test
          </Button>
        </StepActionBar>
      </Stack>
    );
  }

  const types = step.phenotypicTestTypes?.length ? step.phenotypicTestTypes : (step.phenotypicTestType ? [step.phenotypicTestType] : []);
  const canSubmit = Boolean(text.trim()) && organismDetected !== null;

  return (
    <Stack spacing={2}>
      {formError && <Alert severity="error">{formError}</Alert>}
      {confirmatoryOutcome === "Inconclusive" && (
        <Alert severity="warning">
          Confirmatory plating was <strong>Inconclusive</strong> and has been flagged for investigation in the
          audit trail. Biochemical identification can still resolve it; record the result below.
        </Alert>
      )}
      {types.length > 0 && (
        <FixedMediumRow
          label={types.length === 1 ? "Test" : "Tests covered - record one combined result"}
          mediaName={types.map((t) => PHENOTYPIC_TEST_TYPE_LABELS[t] ?? t).join(", ")}
        />
      )}
      <TextField
        id="biochemical-result"
        multiline minRows={4} required label="Biochemical result" value={text}
        onChange={(e) => setText(e.target.value)}
        helperText="For example an IMViC pattern or API strip result."
        slotProps={{ inputLabel: { shrink: true } }}
      />
      <Typography variant="body2" sx={{ fontWeight: 600 }}>
        Based on this result, is the organism Detected or Not Detected? This decision, not the free-text
        result alone, determines the final workflow result.
      </Typography>
      <ObservationOptions
        name="biochemical-detection"
        value={organismDetected === null ? "" : organismDetected ? "detected" : "not-detected"}
        onChange={(v) => setOrganismDetected(v === "detected")}
        options={DETECTION_OPTIONS}
      />
      <StepActionBar note={canSubmit ? "The result and your decision are recorded in the audit trail." : "Enter the result and choose Detected or Not Detected to continue."}>
        <Button variant="contained" disabled={!canSubmit} onClick={submit}>Submit Result</Button>
      </StepActionBar>
    </Stack>
  );
}
