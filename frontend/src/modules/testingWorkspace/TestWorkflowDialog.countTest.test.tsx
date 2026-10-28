import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { render, screen, cleanup, act } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../theme";
import { TestWorkflowDialog } from "./TestWorkflowDialog";

const service = vi.hoisted(() => ({ getCurrentStep: vi.fn() }));
vi.mock("./services/TestWorkflowService", () => ({ TestWorkflowService: service }));
vi.mock("../../services/lookupCache", () => ({
  lookupCache: { getReleasedMedia: vi.fn().mockResolvedValue([]), getIncubators: vi.fn().mockResolvedValue([]) }
}));
vi.mock("../../contexts/AuthContext", () => ({ useAuth: () => ({ permissions: [] }) }));

beforeEach(() => { vi.useFakeTimers({ shouldAdvanceTime: true }); vi.setSystemTime(new Date("2026-10-11T09:00:00Z")); });
afterEach(() => { cleanup(); vi.useRealTimers(); vi.clearAllMocks(); });

const step = {
  id: 1, stepOrder: 1, stepName: "TAMC", stepType: "PlateCount", targetOrganismId: null,
  stepMedia: [{ mediaProductId: 5, mediaProductName: "Tryptic soy agar", tempMin: 30, tempMax: 35, incubationMinHours: 72, incubationMaxHours: 120, isRequired: true }],
  incubationMinHours: 72, incubationMaxHours: 120, temperatureMin: 30, temperatureMax: 35, isFinalStep: true
};

const incubating = (remainingMinimumSeconds: number) => ({
  testOrderId: 9, step, stepNumber: 1, totalSteps: 1, testName: "TAMC", workflowType: "CountTest",
  sampleContext: { sampleName: "Tablet", batchNumber: null, controlNumber: null, reason: null, systemReferenceNumber: null, sampleType: "FinishedProduct", configuredDilutionFactor: 10 },
  incubationLock: {
    isLocked: true, incubationEndUtc: "2026-10-16T09:00:00Z", remainingSeconds: 120 * 3600,
    minReadyAt: new Date(Date.now() + remainingMinimumSeconds * 1000).toISOString(), remainingMinimumSeconds
  },
  previousSteps: [{ stepNumber: 1, stepName: "TAMC", status: "Incubating", mediaName: "Tryptic soy agar", lotNumber: "TSA-01", incubatorName: "INC-02", incubatorSetTemp: 32.5, incubationStartUtc: "2026-10-11T07:00:00Z", incubationEndUtc: null, observation: null }],
  allStepsComplete: false, finalResult: null,
  allSteps: [{ stepOrder: 1, stepName: "TAMC" }], completedSteps: []
});

describe("TestWorkflowDialog count test", () => {
  it("counts down to the plate reading and unlocks Record Result at zero", async () => {
    service.getCurrentStep.mockResolvedValue(incubating(60));
    render(
      <ThemeProvider theme={lightTheme}>
        <TestWorkflowDialog testOrderId={9} testCode="TAMC" category="FinishedProduct" displayName="Tablet" />
      </ThemeProvider>
    );

    expect(await screen.findByText("00:01:00")).toBeTruthy();
    expect(screen.getByText("TSA-01")).toBeTruthy();
    const record = screen.getByRole("button", { name: "Record Result" }) as HTMLButtonElement;
    expect(record.disabled).toBe(true);

    service.getCurrentStep.mockResolvedValue(incubating(0));
    await act(async () => { vi.advanceTimersByTime(61_000); });
    expect(await screen.findByText("Plate reading is available now")).toBeTruthy();
    expect((screen.getByRole("button", { name: "Record Result" }) as HTMLButtonElement).disabled).toBe(false);
  });
});
