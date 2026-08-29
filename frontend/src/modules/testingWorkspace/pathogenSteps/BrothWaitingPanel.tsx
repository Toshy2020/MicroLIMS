import { useState } from "react";
import { Typography, Button, Stack, Alert, CircularProgress } from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import { TestWorkflowStepDto, CurrentStepResponse } from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";
import { useAuth } from "../../../contexts/AuthContext";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  current: CurrentStepResponse;
  onSubmitted: () => void;
}

// BrothEnrichment/SelectiveBroth: waiting for incubation window to complete.
// Replaced ticking countdown with the static TAMC pattern:
// Displays Temperature range, Duration range, Started timestamp, Expected reading timestamp,
// and warning banner "Not ready yet — available from [datetime]" until minimum incubation has elapsed.
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
  const { role } = useAuth();
  const canOverride = role === "SectionHead" || role === "SystemAdministrator";
  const alreadyOverridden = current?.incubationLock?.minimumDurationOverridden ?? false;

  // Match TAMC pattern: find open incubation row from previousSteps
  const openIncubationRow = step
    ? (current?.previousSteps ?? []).find(
        (p) => p.stepName === step.stepName && p.status === "Incubating"
      )
    : null;

  // Derive timestamps from incubationStartUtc
  const incubationStartUtc = openIncubationRow?.incubationStartUtc
    ? new Date(openIncubationRow.incubationStartUtc)
    : null;

  // Incubation specifications: stepMedia is authoritative, step fallback
  const firstMedia = step?.stepMedia?.[0];
  const tempMin = (firstMedia && firstMedia.tempMin > 0) ? firstMedia.tempMin : step?.temperatureMin;
  const tempMax = (firstMedia && firstMedia.tempMax > 0) ? firstMedia.tempMax : step?.temperatureMax;
  const incMinHours = (firstMedia && (firstMedia.incubationMinHours ?? 0) > 0) ? firstMedia.incubationMinHours! : step?.incubationMinHours;
  const incMaxHours = (firstMedia && (firstMedia.incubationMaxHours ?? 0) > 0) ? firstMedia.incubationMaxHours! : step?.incubationMaxHours;

  // Available from: start + minHours
  const minReadyAt = incubationStartUtc && incMinHours != null
    ? new Date(incubationStartUtc.getTime() + incMinHours * 3600 * 1000)
    : null;

  // Expected reading end: from incubationLock or start + maxHours
  const expectedEndAt = current?.incubationLock?.incubationEndUtc
    ? new Date(current.incubationLock.incubationEndUtc)
    : (incubationStartUtc && incMaxHours != null
        ? new Date(incubationStartUtc.getTime() + incMaxHours * 3600 * 1000)
        : null);

  // Readiness gate: simple datetime comparison, no setInterval
  const isTimeReady = !minReadyAt || new Date() >= minReadyAt || alreadyOverridden;

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
    <Stack spacing={1.5}>
      {/* Incubation in progress info card */}
      <Alert severity="info">
        <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5 }}>
          Incubation in progress.
        </Typography>
        {tempMin != null && tempMax != null && tempMin > 0 && (
          <Typography variant="body2">
            Temperature: <strong>{tempMin}–{tempMax} °C</strong>
          </Typography>
        )}
        {incMinHours != null && incMaxHours != null && incMinHours > 0 && (
          <Typography variant="body2">
            Duration: <strong>{incMinHours}–{incMaxHours} hours</strong>
          </Typography>
        )}
        {incubationStartUtc && (
          <Typography variant="body2">
            Started: <strong>{incubationStartUtc.toLocaleString()}</strong>
          </Typography>
        )}
        {expectedEndAt && (
          <Typography variant="body2">
            Expected reading: <strong>{expectedEndAt.toLocaleString()}</strong>
          </Typography>
        )}
      </Alert>

      {error && <Alert severity="error">{error}</Alert>}

      {/* Not ready yet warning banner */}
      {!isTimeReady && minReadyAt && (
        <Alert severity="warning">
          Not ready yet — available from {minReadyAt.toLocaleString()}.
        </Alert>
      )}
      {alreadyOverridden && (
        <Alert severity="info">Minimum wait time was skipped by a Section Head/System Administrator.</Alert>
      )}

      {/* Complete Step button — disabled until minReadyAt has passed (or overridden) */}
      <Stack direction="row" justifyContent="space-between" alignItems="center">
        {canOverride && !isTimeReady && !alreadyOverridden ? (
          <Button variant="outlined" color="warning" onClick={() => setSkipDialogOpen(true)} disabled={skipping}>
            Skip Wait
          </Button>
        ) : <span />}
        <Button
          variant="contained"
          onClick={submitCompletion}
          disabled={!isTimeReady || submitting}
          startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {submitting ? "Submitting..." : "Complete Step"}
        </Button>
      </Stack>

      <ConfirmationDialog
        open={skipDialogOpen}
        message="Skip the remaining minimum incubation wait time for this step? This bypasses the wait only — the recorded incubation window is not changed."
        onConfirm={confirmSkipWait}
        onCancel={() => setSkipDialogOpen(false)}
      />
    </Stack>
  );
}
