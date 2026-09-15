import { CurrentStepResponse, TestWorkflowStepDto } from "../types/testWorkflowTypes";

export const INCUBATION_WINDOW_NOT_CONFIGURED_MESSAGE =
  "The incubation window for this step's medium is not configured in Test Master. Its incubation hours and temperature must be set before this step can continue.";

// The incubation window exactly as the server resolved it: the test's own
// medium from Test Master (backend IncubationWindowResolver). Panels display
// these values and never compute hours or readiness from step media or
// hardcoded defaults - the server enforces the same window.
export interface ServerIncubationWindow {
  tempMin: number;
  tempMax: number;
  minHours: number;
  maxHours: number;
  incubationStartUtc: Date | null;
  minReadyAt: Date | null;
  expectedEndAt: Date | null;
  windowNotConfigured: boolean;
  minimumDurationOverridden: boolean;
  isTimeReady: boolean;
}

export function serverIncubationWindow(
  step: TestWorkflowStepDto | null | undefined,
  current: CurrentStepResponse | null | undefined
): ServerIncubationWindow {
  const lock = current?.incubationLock ?? null;
  const openRow = step
    ? (current?.previousSteps ?? []).find((p) => p.stepName === step.stepName && p.status === "Incubating")
    : undefined;

  const minReadyAt = lock?.minReadyAt ? new Date(lock.minReadyAt) : null;
  const windowNotConfigured = lock?.windowNotConfigured ?? false;
  const minimumDurationOverridden = lock?.minimumDurationOverridden ?? false;
  const minimumElapsed = minReadyAt ? new Date() >= minReadyAt : (lock ? lock.remainingSeconds <= 0 : true);

  return {
    tempMin: step?.temperatureMin ?? 0,
    tempMax: step?.temperatureMax ?? 0,
    minHours: step?.incubationMinHours ?? 0,
    maxHours: step?.incubationMaxHours ?? 0,
    incubationStartUtc: openRow?.incubationStartUtc ? new Date(openRow.incubationStartUtc) : null,
    minReadyAt,
    expectedEndAt: lock?.incubationEndUtc ? new Date(lock.incubationEndUtc) : null,
    windowNotConfigured,
    minimumDurationOverridden,
    // An unconfigured window is never ready - not even after a skipped wait.
    isTimeReady: !windowNotConfigured && (minimumDurationOverridden || minimumElapsed)
  };
}
