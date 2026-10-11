import { ReactNode, useCallback, useEffect, useRef, useState } from "react";
import { Alert, Button, Stack } from "@mui/material";
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

const fmt = (d: Date) => d.toLocaleString(undefined, { weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  current: CurrentStepResponse;
  onSubmitted: () => void;
  // What unlocks at zero, e.g. "MBP" or "plate reading".
  nextLabel: string;
  // Called once when the minimum incubation has elapsed (countdown at zero,
  // or a supervisor skipped the wait).
  onReady?: () => void;
  // Stage-aware window when it differs from the step's own (two-stage count
  // tests); otherwise the server-resolved step window is used.
  incubationWindow?: { tempMin: number; tempMax: number; minHours: number; maxHours: number };
  // The step's primary action, rendered in the action bar; receives whether
  // the minimum incubation has elapsed.
  renderAction: (ready: boolean) => ReactNode;
}

// The waiting screen shared by every incubating step: a countdown from the
// minimum incubation time to zero, the Started / Ready from / Read by
// timeline, the recorded lot and incubator, and the supervisor Skip Wait.
// Readiness is still enforced by the server on submit.
export function IncubationWaitingView({ testOrderId, step, current, onSubmitted, nextLabel, onReady, incubationWindow, renderAction }: Props) {
  const [error, setError] = useState<string | null>(null);
  const [skipDialogOpen, setSkipDialogOpen] = useState(false);
  const [skipping, setSkipping] = useState(false);
  const { permissions } = useAuth();
  const canOverride = permissions.includes(PERMISSIONS.TEST_WORKFLOW_SUPERVISE);
  const alreadyOverridden = current?.incubationLock?.minimumDurationOverridden ?? false;

  // Window, start, readiness and expected end exactly as the server resolved
  // them from this test's own medium - no local hour calculations.
  const server = serverIncubationWindow(step, current);
  const { incubationStartUtc, minReadyAt, expectedEndAt, windowNotConfigured, isTimeReady } = server;
  const { tempMin, tempMax, minHours, maxHours } = incubationWindow ?? server;

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
  // A countdown that runs out while the screen is open reloads the step, so
  // the server's readiness (and the dialog's status chip) catch up at zero.
  const countedDown = useRef(false);
  const handleReached = useCallback(() => setCountdownReached(true), []);
  useEffect(() => {
    if (!countdownReached) countedDown.current = true;
    else if (countedDown.current) { countedDown.current = false; onSubmitted(); }
  }, [countdownReached, onSubmitted]);

  const showCountdown = deadlineMs != null && !alreadyOverridden && !windowNotConfigured;
  const isReady = !windowNotConfigured && (alreadyOverridden || (deadlineMs != null ? countdownReached : isTimeReady));

  useEffect(() => {
    if (isReady) onReady?.();
  }, [isReady, onReady]);

  const openRow = (current?.previousSteps ?? []).find((p) => p.stepName === step.stepName && p.status === "Incubating");
  const details: { label: string; value: ReactNode }[] = [];
  if (openRow?.lotNumber) details.push({ label: "Media lot", value: openRow.lotNumber });
  if (openRow?.incubatorName) {
    details.push({ label: "Incubator", value: openRow.incubatorSetTemp != null ? `${openRow.incubatorName} · ${openRow.incubatorSetTemp} °C` : openRow.incubatorName });
  }
  if (tempMin > 0) details.push({ label: "Temperature", value: `${tempMin}–${tempMax} °C` });
  if (minHours > 0) details.push({ label: "Duration", value: `${minHours}–${maxHours} h` });

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

  return (
    <Stack spacing={2}>
      {showCountdown && (
        <IncubationCountdown deadlineMs={deadlineMs} totalHours={minHours} nextLabel={nextLabel} onReached={handleReached} />
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
      {windowNotConfigured && <Alert severity="error">{INCUBATION_WINDOW_NOT_CONFIGURED_MESSAGE}</Alert>}
      {!showCountdown && !isReady && minReadyAt && (
        <Alert severity="warning">Not ready yet. Available from {minReadyAt.toLocaleString()}.</Alert>
      )}
      {alreadyOverridden && (
        <Alert severity="info">Minimum wait time was skipped by a Section Head/System Administrator.</Alert>
      )}

      <StepActionBar>
        {canOverride && !isReady && !alreadyOverridden && !windowNotConfigured && (
          <Button variant="outlined" color="warning" onClick={() => setSkipDialogOpen(true)} disabled={skipping}>
            Skip Wait
          </Button>
        )}
        {renderAction(isReady)}
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
