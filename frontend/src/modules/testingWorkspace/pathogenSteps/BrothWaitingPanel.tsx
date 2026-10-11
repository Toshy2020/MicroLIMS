import { ReactNode, useCallback, useEffect, useState } from "react";
import { Button, Stack, Alert, CircularProgress } from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import { TestWorkflowStepDto, CurrentStepResponse } from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";
import { INCUBATION_WINDOW_NOT_CONFIGURED_MESSAGE, serverIncubationWindow } from "../utils/incubationWindow";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { StepActionBar, StepDetailList, IncubationTimeline } from "./StepLayout";
import { IncubationCountdown } from "./IncubationCountdown";

// When the minimum incubation ends, as a client timestamp. Prefers the
// server's remainingMinimumSeconds (immune to a wrong device clock) and
// falls back to the server's minReadyAt.
function minimumDeadline(current: CurrentStepResponse): number | null {
  const lock = current.incubationLock;
  if (!lock || lock.windowNotConfigured) return null;
  if (lock.remainingMinimumSeconds != null) return Date.now() + lock.remainingMinimumSeconds * 1000;
  return lock.minReadyAt ? new Date(lock.minReadyAt).getTime() : null;
}

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  current: CurrentStepResponse;
  onSubmitted: () => void;
}

// BrothEnrichment/SelectiveBroth: waiting for incubation window to complete.
// Counts down from the minimum incubation time (e.g. 18 h of an 18-72 h
// window) to zero; at zero the next step / reading becomes available.
// Also shows Started / Ready from / Read by and the recorded lot and
// incubator. Readiness is still enforced by the server on submit.
export function BrothWaitingPanel({
  testOrderId,
  step,
  current,
  onSubmitted
}: Props) {
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [skipDialogOpen, setSkipDialogOpen] = useState(false);
  const [skipping, setSkipping] = useState(false);
  const { permissions } = useAuth();
  const canOverride = permissions.includes(PERMISSIONS.TEST_WORKFLOW_SUPERVISE);
  const alreadyOverridden = current?.incubationLock?.minimumDurationOverridden ?? false;

  // Window, start, readiness and expected end exactly as the server resolved
  // them from this test's own medium - no local hour calculations.
  const {
    tempMin, tempMax, minHours: incMinHours, maxHours: incMaxHours,
    incubationStartUtc, minReadyAt, expectedEndAt, windowNotConfigured, isTimeReady
  } = serverIncubationWindow(step, current);

  // Recomputed whenever the step is reloaded (new incubationLock object).
  const lock = current?.incubationLock ?? null;
  const [deadlineMs, setDeadlineMs] = useState<number | null>(() => minimumDeadline(current));
  const [countdownReached, setCountdownReached] = useState(() => {
    const d = minimumDeadline(current);
    return d != null && d <= Date.now();
  });
  useEffect(() => {
    const d = minimumDeadline(current);
    setDeadlineMs(d);
    setCountdownReached(d != null && d <= Date.now());
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lock]);
  const handleReached = useCallback(() => setCountdownReached(true), []);

  const showCountdown = deadlineMs != null && !alreadyOverridden && !windowNotConfigured;
  const isReady = !windowNotConfigured && (alreadyOverridden || (deadlineMs != null ? countdownReached : isTimeReady));

  const steps = current?.allSteps ?? [];
  const nextStep = steps.find((s) => s.stepOrder > step.stepOrder);
  const nextLabel = nextStep ? nextStep.stepName : "the reading";

  // Lot and incubator exactly as recorded on the open incubation row.
  const openRow = (current?.previousSteps ?? []).find((p) => p.stepName === step.stepName && p.status === "Incubating");
  const fmt = (d: Date) => d.toLocaleString(undefined, { weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });

  const details: { label: string; value: ReactNode }[] = [];
  if (openRow?.lotNumber) details.push({ label: "Media lot", value: openRow.lotNumber });
  if (openRow?.incubatorName) {
    details.push({ label: "Incubator", value: openRow.incubatorSetTemp != null ? `${openRow.incubatorName} · ${openRow.incubatorSetTemp} °C` : openRow.incubatorName });
  }
  if (tempMin != null && tempMax != null && tempMin > 0) details.push({ label: "Temperature", value: `${tempMin}–${tempMax} °C` });
  if (incMinHours != null && incMaxHours != null && incMinHours > 0) details.push({ label: "Duration", value: `${incMinHours}–${incMaxHours} h` });

  const confirmSkipWait = async () => {
    setError(null);
    setSkipping(true);
    try {
      await TestWorkflowService.overrideMinimumDuration(testOrderId);
      setSkipDialogOpen(false);
      onSubmitted();
    } catch (e) {
      setError(workflowErrorDisplayMessage(parseWorkflowError(e)));
    } finally {
      setSkipping(false);
    }
  };

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
      {showCountdown && (
        <IncubationCountdown deadlineMs={deadlineMs} totalHours={incMinHours} nextLabel={nextLabel} onReached={handleReached} />
      )}

      <IncubationTimeline
        points={[
          { label: "Started", value: incubationStartUtc ? fmt(incubationStartUtc) : "—" },
          { label: "Ready from", value: minReadyAt ? fmt(minReadyAt) : "—", active: !isReady },
          { label: "Read by", value: expectedEndAt ? fmt(expectedEndAt) : "—", active: isReady }
        ]}
      />

      {details.length > 0 && <StepDetailList rows={details} />}

      {error && <Alert severity="error">{error}</Alert>}

      {windowNotConfigured && (
        <Alert severity="error">{INCUBATION_WINDOW_NOT_CONFIGURED_MESSAGE}</Alert>
      )}

      {/* Not ready yet banner - only when there is no countdown to show it */}
      {!showCountdown && !isReady && minReadyAt && (
        <Alert severity="warning">
          Not ready yet. Available from {minReadyAt.toLocaleString()}.
        </Alert>
      )}
      {alreadyOverridden && (
        <Alert severity="info">Minimum wait time was skipped by a Section Head/System Administrator.</Alert>
      )}

      {/* Complete Step button — disabled until minReadyAt has passed (or overridden) */}
      <StepActionBar>
        {canOverride && !isReady && !alreadyOverridden && !windowNotConfigured && (
          <Button variant="outlined" color="warning" onClick={() => setSkipDialogOpen(true)} disabled={skipping}>
            Skip Wait
          </Button>
        )}
        <Button
          variant="contained"
          onClick={submitCompletion}
          disabled={!isReady || submitting}
          startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {submitting ? "Submitting..." : nextStep ? `Proceed to ${nextStep.stepName}` : "Proceed to Reading"}
        </Button>
      </StepActionBar>

      <ConfirmationDialog
        open={skipDialogOpen}
        message="Skip the remaining minimum incubation wait time for this step? This bypasses the wait only; the recorded incubation window is not changed."
        onConfirm={confirmSkipWait}
        onCancel={() => setSkipDialogOpen(false)}
      />
    </Stack>
  );
}
