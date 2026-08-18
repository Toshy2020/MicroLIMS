import { useState } from "react";
import { Typography, Button, Stack, Alert, CircularProgress } from "@mui/material";
import { TestWorkflowService } from "../services/TestWorkflowService";
import { TestWorkflowStepDto, CurrentStepResponse } from "../types/testWorkflowTypes";
import { parseWorkflowError, workflowErrorDisplayMessage } from "../utils/workflowErrors";

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

  // Available from: start + minHours
  const minReadyAt = incubationStartUtc && step.incubationMinHours != null
    ? new Date(incubationStartUtc.getTime() + step.incubationMinHours * 3600 * 1000)
    : null;

  // Expected reading end: from incubationLock or start + maxHours
  const expectedEndAt = current?.incubationLock?.incubationEndUtc
    ? new Date(current.incubationLock.incubationEndUtc)
    : (incubationStartUtc && step.incubationMaxHours != null
        ? new Date(incubationStartUtc.getTime() + step.incubationMaxHours * 3600 * 1000)
        : null);

  // Readiness gate: simple datetime comparison, no setInterval
  const isTimeReady = !minReadyAt || new Date() >= minReadyAt;

  // Temperature specifications from step
  const tempMin = step.temperatureMin;
  const tempMax = step.temperatureMax;

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

  console.log("=== BrothWaitingPanel Debug ===");
  console.log("step.incubationMinHours:", step?.incubationMinHours);
  console.log("step.incubationMaxHours:", step?.incubationMaxHours);
  console.log("openIncubationRow:", openIncubationRow);
  console.log("incubationStartUtc (raw):", openIncubationRow?.incubationStartUtc);
  console.log("incubationStartUtc (parsed):", incubationStartUtc);
  console.log("minReadyAt:", minReadyAt);
  console.log("expectedEndAt:", expectedEndAt);
  console.log("isTimeReady:", isTimeReady);
  console.log("now:", new Date());
  console.log("current?.previousSteps:", current?.previousSteps);
  console.log("current?.incubationLock:", current?.incubationLock);
  console.log("==============================");

  return (
    <Stack spacing={1.5}>
      {/* Incubation in progress info card */}
      <Alert severity="info">
        <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5 }}>
          Incubation in progress.
        </Typography>
        {tempMin != null && tempMax != null && (
          <Typography variant="body2">
            Temperature: <strong>{tempMin}–{tempMax} °C</strong>
          </Typography>
        )}
        <Typography variant="body2">
          Duration: <strong>{step.incubationMinHours}–{step.incubationMaxHours} hours</strong>
        </Typography>
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

      {/* Complete Step button — disabled until minReadyAt has passed */}
      <Stack direction="row" justifyContent="flex-end">
        <Button
          variant="contained"
          onClick={submitCompletion}
          disabled={!isTimeReady || submitting}
          startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {submitting ? "Submitting..." : "Complete Step"}
        </Button>
      </Stack>
    </Stack>
  );
}
