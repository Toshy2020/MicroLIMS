import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { render, screen, cleanup, act, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../theme";
import { AuthProvider } from "../../contexts/AuthContext";
import { TestWorkflowDialog } from "./TestWorkflowDialog";

const service = vi.hoisted(() => ({ getCurrentStep: vi.fn(), recordCountResult: vi.fn() }));
vi.mock("./services/TestWorkflowService", () => ({ TestWorkflowService: service }));
vi.mock("../../services/lookupCache", () => ({
  lookupCache: { getReleasedMedia: vi.fn().mockResolvedValue([]), getIncubators: vi.fn().mockResolvedValue([]) }
}));
vi.mock("../../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
}));

beforeEach(() => {
  localStorage.clear();
  vi.useFakeTimers({ shouldAdvanceTime: true });
  vi.setSystemTime(new Date("2026-10-11T09:00:00Z"));
});
afterEach(() => { cleanup(); vi.useRealTimers(); vi.clearAllMocks(); localStorage.clear(); });

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

function renderDialog() {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter>
          <TestWorkflowDialog testOrderId={9} testCode="TAMC" category="FinishedProduct" displayName="Tablet" />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

describe("TestWorkflowDialog count test", () => {
  it("counts down to the plate reading and unlocks Record Result at zero", async () => {
    service.getCurrentStep.mockResolvedValue(incubating(60));
    renderDialog();

    expect(await screen.findByText("00:01:00")).toBeTruthy();
    expect(screen.getByText("TSA-01")).toBeTruthy();
    const record = screen.getByRole("button", { name: "Record Result" }) as HTMLButtonElement;
    expect(record.disabled).toBe(true);

    service.getCurrentStep.mockResolvedValue(incubating(0));
    await act(async () => { vi.advanceTimersByTime(61_000); });
    expect(await screen.findByText("Plate reading is available now")).toBeTruthy();
    expect((screen.getByRole("button", { name: "Record Result" }) as HTMLButtonElement).disabled).toBe(false);
  });

  it("renders LocationResultsPanel when current.locationEntry is true", async () => {
    service.getCurrentStep.mockResolvedValue({
      testOrderId: 42,
      testName: "PH",
      workflowType: "Measurement",
      step: null,
      stepNumber: 1,
      totalSteps: 1,
      sampleContext: { sampleName: "Water Sample" },
      incubationLock: null,
      previousSteps: [],
      allStepsComplete: false,
      finalResult: null,
      allSteps: [],
      completedSteps: [],
      locationEntry: true,
      locations: [
        {
          sampleLocationId: 101,
          name: "PW-01 - Point 1",
          limitType: "Range",
          lowerLimit: 6.5,
          upperLimit: 8.5,
          expectedResultText: null,
          unit: "pH",
          specLimit: "6.5 – 8.5",
          value: null,
          text: null
        }
      ]
    });

    render(
      <ThemeProvider theme={lightTheme}>
        <AuthProvider>
          <MemoryRouter>
            <TestWorkflowDialog testOrderId={42} testCode="PH" category="Water" displayName="Purified Water pH" />
          </MemoryRouter>
        </AuthProvider>
      </ThemeProvider>
    );

    expect(await screen.findByText("PW-01 - Point 1")).toBeTruthy();
    expect(screen.getByText("6.5 – 8.5")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Submit" })).toBeTruthy();
  });

  // Payload pin: written against the code before the bench work and unchanged since.
  describe("payload to TestWorkflowService.recordCountResult", () => {
    it("count: payload is exactly { stepName, rawPlateReadings, dilutionFactor, dilutionFactorOverrideNote? } as before", async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      service.getCurrentStep.mockResolvedValue(incubating(0));
      service.recordCountResult.mockResolvedValue({ allStepsComplete: true, isDefinitive: true, outcomeSummary: "ok" });
      renderDialog();

      await user.click(await screen.findByRole("button", { name: "Record Result" }));
      await user.type(screen.getByLabelText("Plate 1"), "12");
      await user.type(screen.getByLabelText("Plate 2"), "TNTC");
      await user.click(screen.getByRole("button", { name: "Calculate" }));

      await waitFor(() => expect(service.recordCountResult).toHaveBeenCalledTimes(1));
      const [orderId, payload, ...rest] = service.recordCountResult.mock.calls[0];
      expect(orderId).toBe(9);
      expect(rest).toEqual([]);
      expect(payload).toEqual({ stepName: "TAMC", rawPlateReadings: ["12", "TNTC"], dilutionFactor: 10, dilutionFactorOverrideNote: undefined });
      expect(Object.keys(payload).sort()).toEqual(["dilutionFactor", "dilutionFactorOverrideNote", "rawPlateReadings", "stepName"]);
    });

    it("count: an overridden dilution factor sends the trimmed justification note", async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      service.getCurrentStep.mockResolvedValue(incubating(0));
      service.recordCountResult.mockResolvedValue({ allStepsComplete: true, isDefinitive: true, outcomeSummary: "ok" });
      renderDialog();

      await user.click(await screen.findByRole("button", { name: "Record Result" }));
      await user.click(screen.getByRole("button", { name: "Override dilution factor" }));
      const factor = screen.getByLabelText("Dilution factor");
      await user.clear(factor);
      await user.type(factor, "100");
      await user.type(screen.getByLabelText(/Justification for override/), "  Sample too concentrated  ");
      await user.type(screen.getByLabelText("Plate 1"), "7");
      await user.click(screen.getByRole("button", { name: "Calculate" }));

      await waitFor(() => expect(service.recordCountResult).toHaveBeenCalledTimes(1));
      expect(service.recordCountResult.mock.calls[0]).toEqual([
        9,
        { stepName: "TAMC", rawPlateReadings: ["7"], dilutionFactor: 100, dilutionFactorOverrideNote: "Sample too concentrated" }
      ]);
    });
  });
});
