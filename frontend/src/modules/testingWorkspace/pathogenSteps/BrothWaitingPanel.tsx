import { useState } from "react";
import { Typography, Button, Stack, Alert, Box } from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import { TestWorkflowStepDto, IncubationLock } from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";
import { useIncubationCountdown } from "../hooks/useIncubationCountdown";

interface Props {
  testOrderId: number;
  step: TestWorkflowStepDto;
  incubationLock: IncubationLock;
  incubationDetails: any;
  onSubmitted: () => void;
}

// BrothEnrichment/SelectiveBroth: waiting for incubation window to complete.
// Shows the Test Master-defined incubation range, start time, elapsed,
// and remaining time. Once the minimum duration has elapsed and the window
// has passed, the "Complete" button becomes enabled.
export function BrothWaitingPanel({ 
  testOrderId, step, incubationLock, incubationDetails, onSubmitted 
}: Props) {
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const countdown = useIncubationCountdown(incubationLock);

  const startTime = incubationDetails?.startedAt 
    ? new Date(incubationDetails.startedAt) 
    : new Date();
  const elapsedSeconds = Math.floor((Date.now() - startTime.getTime()) / 1000);
  const elapsedDisplay = formatTime(elapsedSeconds);

  // Can submit once minimum duration has passed AND window has ended
  const minReadyAt = startTime.getTime() + step.incubationMinHours * 3600 * 1000;
  const canSubmit = Date.now() >= minReadyAt && !countdown.isLocked;

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
      <Alert severity="info">
        <Typography variant="body2" sx={{ mb: 0.5 }}>
          <strong>INCUBATION IN PROGRESS</strong>
        </Typography>
        <Typography variant="body2" sx={{ mb: 0.25 }}>
          <strong>Assigned range:</strong> {step.incubationMinHours}–{step.incubationMaxHours} h
        </Typography>
        <Typography variant="body2" sx={{ mb: 0.25 }}>
          <strong>Started:</strong> {startTime.toLocaleString()}
        </Typography>
        <Typography variant="body2" sx={{ mb: 0.25 }}>
          <strong>Elapsed:</strong> {elapsedDisplay}
        </Typography>
        <Typography variant="body2">
          <strong>Remaining:</strong> {countdown.formatted}
        </Typography>
      </Alert>

      {error && <Alert severity="error">{error}</Alert>}

      {!canSubmit && (
        <Alert severity="warning">
          Not ready yet. Minimum incubation time is {step.incubationMinHours} hours from start.
          Current time: {new Date().toLocaleTimeString()}
        </Alert>
      )}

      <Stack direction="row" justifyContent="flex-end">
        <Button 
          variant="contained" 
          onClick={submitCompletion}
          disabled={!canSubmit || submitting}
        >
          {submitting ? "Submitting..." : "Complete Step"}
        </Button>
      </Stack>
    </Stack>
  );
}

function formatTime(totalSeconds: number): string {
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  if (hours > 0) return `${hours}h ${minutes}m ${seconds}s`;
  if (minutes > 0) return `${minutes}m ${seconds}s`;
  return `${seconds}s`;
}
