import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, within, fireEvent } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../theme";
import { BrothStepPanel } from "./BrothStepPanel";
import { StepChainStrip } from "../components/StepChainStrip";
import type { CurrentStepResponse, TestWorkflowStepDto } from "../types/testWorkflowTypes";

const service = vi.hoisted(() => ({
  getPermittedConfirmatoryMedia: vi.fn(),
  getEligibleIncubators: vi.fn(),
  getSiblingPathogenOrders: vi.fn()
}));
vi.mock("../services/TestWorkflowService", () => ({ TestWorkflowService: service }));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

const step = {
  id: 1, stepOrder: 1, stepName: "Broth enrichment", stepType: "BrothEnrichment", targetOrganismId: null,
  stepMedia: [], incubationMinHours: 18, incubationMaxHours: 72, temperatureMin: 30, temperatureMax: 35, isFinalStep: false
} as TestWorkflowStepDto;

const current = {
  testOrderId: 7, step, stepNumber: 1, totalSteps: 3, testName: "E. coli", workflowType: "Observation",
  sampleContext: { sampleName: "Tablet", batchNumber: null, controlNumber: null, reason: null, systemReferenceNumber: null, sampleType: "FinishedProduct" },
  incubationLock: null, previousSteps: [], allStepsComplete: false, finalResult: null,
  allSteps: [{ stepOrder: 1, stepName: "Broth enrichment" }, { stepOrder: 2, stepName: "MBP" }, { stepOrder: 3, stepName: "MAR" }],
  completedSteps: []
} as unknown as CurrentStepResponse;

describe("BrothStepPanel", () => {
  it("keeps Start Incubation disabled until a lot and an incubator are chosen, and prefetches sibling orders", async () => {
    service.getPermittedConfirmatoryMedia.mockResolvedValue({
      permittedMedia: [{ stepMediaId: 3, mediaName: "Tryptic soy broth", tempMin: 30, tempMax: 35, availableLots: [{ id: 11, lotNumber: "TSB-2609-014", expiryDate: "2026-11-28" }] }]
    });
    service.getEligibleIncubators.mockResolvedValue({ eligibleIncubators: [{ id: 21, name: "Incubator 3", code: "INC-03", setTemperature: 32.5 }] });
    service.getSiblingPathogenOrders.mockResolvedValue([]);

    render(
      <ThemeProvider theme={lightTheme}>
        <BrothStepPanel testOrderId={7} step={step} current={current} onSubmitted={() => {}} />
      </ThemeProvider>
    );

    expect(await screen.findByText("Tryptic soy broth")).toBeTruthy();
    expect(service.getSiblingPathogenOrders).toHaveBeenCalledWith(7);
    expect(screen.getByText("1 released lot available")).toBeTruthy();

    const start = screen.getByRole("button", { name: "Start Incubation" }) as HTMLButtonElement;
    expect(start.disabled).toBe(true);
    expect(screen.getByText("Select a media lot and an incubator to continue.")).toBeTruthy();

    fireEvent.mouseDown(screen.getByRole("combobox", { name: /Media lot/ }));
    fireEvent.click(within(await screen.findByRole("listbox")).getByText(/TSB-2609-014/));
    fireEvent.mouseDown(screen.getByRole("combobox", { name: /Incubator/ }));
    fireEvent.click(within(await screen.findByRole("listbox")).getByText(/INC-03/));

    expect(start.disabled).toBe(false);
  });
});

describe("StepChainStrip", () => {
  it("shows the current position and labels each step with its state", () => {
    render(
      <ThemeProvider theme={lightTheme}>
        <StepChainStrip current={current} />
      </ThemeProvider>
    );
    expect(screen.getByText("Step 1 of 3")).toBeTruthy();
    expect(screen.getByLabelText("Broth enrichment: In progress").getAttribute("aria-current")).toBe("step");
    expect(screen.getByLabelText("MBP: Not started")).toBeTruthy();
  });
});
