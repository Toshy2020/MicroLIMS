import { useState } from "react";
import { Typography, TextField, Button, Stack, Alert } from "@mui/material";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { TestWorkflowService } from "../services/TestWorkflowService";
import { TestWorkflowStepDto, AnalystDecision } from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";
import { DraftRestoreNotice } from "../../../components/bench/DraftRestoreNotice";
import { useBenchDraft } from "../../../hooks/useBenchDraft";
import { useBenchMode } from "../../../theme/BenchModeContext";
import { OfflineDraftNotice, classifySaveFailure, draftHeaderArgs, useKeptOffline } from "../benchSave";
import { StepActionBar, FixedMediumRow, ObservationOptions } from "./StepLayout";

interface BiochemicalDraftValues {
  text: string;
  organismDetected: boolean | null;
}

// Explicit allow-list: analyst decision / phase is never stored in a draft.
function pickBiochemicalDraft(v: BiochemicalDraftValues): Partial<BiochemicalDraftValues> {
  return { text: v.text, organismDetected: v.organismDetected };
}

type DetectionChoice = "not-detected" | "detected";

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
  const { t } = useTranslation("testingWorkspace");
  const { benchMode } = useBenchMode();
  const [keptOffline, setKeptOffline] = useKeptOffline();
  const [phase, setPhase] = useState<Phase>(confirmatoryOutcome === "AllConforming" ? "decision" : "form");
  const [decisionError, setDecisionError] = useState<string | null>(null);
  const [text, setText] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  // Never inferred from the free-text result - an earlier version of this
  // workflow always finalized as Detected regardless of what the result
  // text said, which produced a real false-positive pathogen result once
  // this step became reachable after an Inconclusive confirmatory reading.
  const [organismDetected, setOrganismDetected] = useState<boolean | null>(null);

  const detectionOptions: { value: DetectionChoice; title: string; detail: string }[] = [
    { value: "not-detected", title: t("pathogenSteps.biochemical.notDetected"), detail: t("pathogenSteps.biochemical.notDetectedDetail") },
    { value: "detected", title: t("pathogenSteps.biochemical.detected"), detail: t("pathogenSteps.biochemical.detectedDetail") }
  ];

  const draft = useBenchDraft<BiochemicalDraftValues>({
    formKey: "pathogen-biochemical",
    recordKey: `${testOrderId}:${step.stepName}`,
    recordVersion: null,
    title: `${step.stepName}: #${testOrderId}`,
    values: { text, organismDetected },
    pick: pickBiochemicalDraft,
    enabled: benchMode && phase === "form"
  });

  const restoreDraft = () => {
    const saved = draft.acceptRestore();
    if (!saved) return;
    if (typeof saved.text === "string") setText(saved.text);
    if (typeof saved.organismDetected === "boolean" || saved.organismDetected === null) {
      setOrganismDetected(saved.organismDetected);
    }
  };

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
    if (!text.trim()) { setFormError(t("pathogenSteps.biochemical.enterResult")); return; }
    if (organismDetected === null) { setFormError(t("pathogenSteps.biochemical.selectDetected")); return; }
    try {
      await TestWorkflowService.submitBiochemical(
        testOrderId, step.stepName, text.trim(), organismDetected, ...draftHeaderArgs(draft.submitHeaders())
      );
      await draft.onSubmitted();
      setKeptOffline(false);
      onSubmitted();
    } catch (e) {
      const failure = classifySaveFailure(e);
      if (failure.kind === "network") {
        setKeptOffline(true);
      } else if (failure.kind === "conflict") {
        await draft.onRejected(failure.message);
        setFormError(failure.message);
      } else {
        setFormError(workflowErrorDisplayMessage(parseWorkflowError(e)));
      }
    }
  };

  if (phase === "decision") {
    return (
      <Stack spacing={2}>
        {decisionError && <Alert severity="error">{decisionError}</Alert>}
        <Alert severity="success">
          <Trans i18nKey="pathogenSteps.biochemical.decisionAlert" ns="testingWorkspace" components={{ strong: <strong /> }} />
        </Alert>
        <Alert severity="warning">
          {t("pathogenSteps.biochemical.decisionWarning")}
        </Alert>
        <StepActionBar>
          <Button variant="outlined" color="warning" onClick={() => decide("SubmitAsDetected")}>
            {t("pathogenSteps.biochemical.submitAsDetected")}
          </Button>
          <Button variant="contained" onClick={() => decide("ProceedToBiochemical")}>
            {t("pathogenSteps.biochemical.proceedToBiochemical")}
          </Button>
        </StepActionBar>
      </Stack>
    );
  }

  const types = step.phenotypicTestTypes?.length ? step.phenotypicTestTypes : (step.phenotypicTestType ? [step.phenotypicTestType] : []);
  const canSubmit = Boolean(text.trim()) && organismDetected !== null;

  return (
    <Stack spacing={2}>
      {benchMode && <DraftRestoreNotice startedAt={draft.restoredStartedAt} onRestore={restoreDraft} onDiscard={() => void draft.discard()} />}
      {benchMode && keptOffline && <OfflineDraftNotice onTryAgain={() => void submit()} />}
      {formError && <Alert severity="error">{formError}</Alert>}
      {confirmatoryOutcome === "Inconclusive" && (
        <Alert severity="warning">
          <Trans i18nKey="pathogenSteps.biochemical.inconclusiveAlert" ns="testingWorkspace" components={{ strong: <strong /> }} />
        </Alert>
      )}
      {types.length > 0 && (
        <FixedMediumRow
          label={types.length === 1 ? t("pathogenSteps.biochemical.singleTest") : t("pathogenSteps.biochemical.combinedTests")}
          mediaName={types.map((tp) => t(`pathogenSteps.biochemical.types.${tp}`, { defaultValue: tp })).join(", ")}
        />
      )}
      <TextField
        id="biochemical-result"
        multiline minRows={4} required label={t("pathogenSteps.biochemical.resultLabel")} value={text}
        onChange={(e) => setText(e.target.value)}
        helperText={t("pathogenSteps.biochemical.resultHelper")}
        slotProps={{ inputLabel: { shrink: true } }}
      />
      <Typography variant="body2" sx={{ fontWeight: 600 }}>
        {t("pathogenSteps.biochemical.decisionQuestion")}
      </Typography>
      <ObservationOptions
        name="biochemical-detection"
        value={organismDetected === null ? "" : organismDetected ? "detected" : "not-detected"}
        onChange={(v) => setOrganismDetected(v === "detected")}
        options={detectionOptions}
      />
      <StepActionBar note={canSubmit ? t("pathogenSteps.biochemical.noteReady") : t("pathogenSteps.biochemical.noteIncomplete")}>
        <Button variant="contained" disabled={!canSubmit} onClick={submit}>{t("pathogenSteps.biochemical.submitResult")}</Button>
      </StepActionBar>
    </Stack>
  );
}
