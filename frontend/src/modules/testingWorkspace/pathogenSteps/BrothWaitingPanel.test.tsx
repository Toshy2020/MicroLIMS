import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { render, screen, cleanup, act } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../theme";
import { BrothWaitingPanel } from "./BrothWaitingPanel";
import type { CurrentStepResponse, TestWorkflowStepDto } from "../types/testWorkflowTypes";

vi.mock("../services/TestWorkflowService", () => ({ TestWorkflowService: {} }));
vi.mock("../../../contexts/AuthContext", () => ({ useAuth: () => ({ permissions: [] }) }));

beforeEach(() => { vi.useFakeTimers(); vi.setSystemTime(new Date("2026-10-11T09:00:00Z")); });
afterEach(() => { cleanup(); vi.useRealTimers(); });

const step = {
  id: 1, stepOrder: 1, stepName: "Broth enrichment", stepType: "BrothEnrichment", targetOrganismId: null,
  stepMedia: [], incubationMinHours: 18, incubationMaxHours: 72, temperatureMin: 30, temperatureMax: 35, isFinalStep: false
} as TestWorkflowStepDto;

const current = (remainingMinimumSeconds: number) => ({
  testOrderId: 7, step, stepNumber: 1, totalSteps: 2, testName: "E. coli", workflowType: "Observation",
  sampleContext: { sampleName: "Tablet", batchNumber: null, controlNumber: null, reason: null, systemReferenceNumber: null, sampleType: "FinishedProduct" },
  incubationLock: {
    isLocked: true, incubationEndUtc: "2026-10-14T09:00:00Z", remainingSeconds: 72 * 3600,
    minReadyAt: new Date(Date.now() + remainingMinimumSeconds * 1000).toISOString(), remainingMinimumSeconds
  },
  previousSteps: [], allStepsComplete: false, finalResult: null,
  allSteps: [{ stepOrder: 1, stepName: "Broth enrichment" }, { stepOrder: 2, stepName: "MBP" }],
  completedSteps: []
}) as unknown as CurrentStepResponse;

const renderPanel = (c: CurrentStepResponse) => render(
  <ThemeProvider theme={lightTheme}>
    <BrothWaitingPanel testOrderId={7} step={step} current={c} onSubmitted={() => {}} />
  </ThemeProvider>
);

describe("BrothWaitingPanel countdown", () => {
  it("counts down from the minimum incubation and unlocks the next step at zero", () => {
    renderPanel(current(18 * 3600));
    expect(screen.getByText("18:00:00")).toBeTruthy();
    const proceed = screen.getByRole("button", { name: "Proceed to MBP" }) as HTMLButtonElement;
    expect(proceed.disabled).toBe(true);

    act(() => { vi.advanceTimersByTime(65_000); });
    expect(screen.getByText("17:58:55")).toBeTruthy();

    act(() => { vi.advanceTimersByTime(18 * 3600 * 1000); });
    expect(screen.getByText("Ready")).toBeTruthy();
    expect(screen.getByText("MBP is available now")).toBeTruthy();
    expect(proceed.disabled).toBe(false);
  });
});

describe("SelectivePlatingPanel countdown", () => {
  it("shows the shared countdown while incubating and switches to plate reading at zero", async () => {
    const { SelectivePlatingPanel } = await import("./SelectivePlatingPanel");
    const { TestWorkflowService } = await import("../services/TestWorkflowService");
    Object.assign(TestWorkflowService, {
      getPermittedConfirmatoryMedia: vi.fn().mockResolvedValue({
        permittedMedia: [{ stepMediaId: 3, mediaName: "MacConkey agar", tempMin: 30, tempMax: 35, expectedAppearance: "Pink colonies", availableLots: [] }]
      }),
      getEligibleIncubators: vi.fn().mockResolvedValue({ eligibleIncubators: [] })
    });
    const platingStep = { ...step, stepName: "MAR", stepType: "SelectivePlating", incubationMinHours: 1, incubationMaxHours: 2 } as TestWorkflowStepDto;
    const c = { ...current(3600), step: platingStep } as CurrentStepResponse;
    render(
      <ThemeProvider theme={lightTheme}>
        <SelectivePlatingPanel testOrderId={7} step={platingStep} current={c} onSubmitted={() => {}} />
      </ThemeProvider>
    );
    await act(async () => { await vi.runOnlyPendingTimersAsync(); });
    expect(screen.getByText("01:00:00")).toBeTruthy();
    expect(screen.getByText("Pink colonies")).toBeTruthy();

    act(() => { vi.advanceTimersByTime(3600 * 1000); });
    expect(screen.getByText("Growth matching expected appearance")).toBeTruthy();
  });
});
