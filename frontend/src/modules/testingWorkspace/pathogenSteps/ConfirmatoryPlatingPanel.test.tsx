import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../theme";
import { ConfirmatoryPlatingPanel } from "./ConfirmatoryPlatingPanel";
import type { CurrentStepResponse, TestWorkflowStepDto } from "../types/testWorkflowTypes";

const service = vi.hoisted(() => ({
  getPermittedConfirmatoryMedia: vi.fn(),
  getEligibleIncubators: vi.fn(),
  submitConfirmatoryObservations: vi.fn()
}));
vi.mock("../services/TestWorkflowService", () => ({ TestWorkflowService: service }));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

const step = {
  id: 4, stepOrder: 4, stepName: "Confirmatory Plating", stepType: "ConfirmatoryPlating", targetOrganismId: 1,
  stepMedia: [], incubationMinHours: 18, incubationMaxHours: 24, temperatureMin: 35, temperatureMax: 37, isFinalStep: false
} as unknown as TestWorkflowStepDto;

// Plating already started (an open incubation row, minimum elapsed): the panel opens on the readout.
const current = {
  testOrderId: 7, step, stepNumber: 4, totalSteps: 5, testName: "Salmonella", workflowType: "Observation",
  sampleContext: { sampleName: "Tablet", batchNumber: null, controlNumber: null, reason: null, systemReferenceNumber: null, sampleType: "FinishedProduct" },
  incubationLock: null,
  previousSteps: [{ stepName: "Confirmatory Plating", status: "Incubating", incubationStartUtc: "2026-10-27T08:00:00Z" }],
  allStepsComplete: false, finalResult: null, allSteps: [], completedSteps: []
} as unknown as CurrentStepResponse;

describe("ConfirmatoryPlatingPanel", () => {
  it("after a reload, records the readout against the material of the lot that was plated (second of two lots of one product)", async () => {
    service.getPermittedConfirmatoryMedia.mockResolvedValue({
      organism: { id: 1, name: "Salmonella" },
      permittedMedia: [{
        stepMediaId: 3, mediaProductId: 8, mediaName: "XLD agar", expectedAppearance: "Red colonies", tempMin: 35, tempMax: 37,
        availableLots: [
          { id: 101, lotNumber: "XLD/01/26", expiryDate: "2026-12-01", materialId: 31 },
          { id: 102, lotNumber: "XLD/02/26", expiryDate: "2026-12-15", materialId: 32 }
        ],
        selection: { materialId: 32, mediaId: 102, lotNumber: "XLD/02/26", equipmentId: 21 }
      }]
    });
    service.getEligibleIncubators.mockResolvedValue({ eligibleIncubators: [] });
    service.submitConfirmatoryObservations.mockResolvedValue({ analystDecisionRequired: false });
    const onSubmitted = vi.fn();

    render(
      <ThemeProvider theme={lightTheme}>
        <ConfirmatoryPlatingPanel testOrderId={7} step={step} current={current} onSubmitted={onSubmitted} />
      </ThemeProvider>
    );

    expect(await screen.findByText("XLD agar")).toBeTruthy();
    // The server knows what was plated, so there is no "was it actually plated" confirmation.
    expect(screen.queryByText("This medium was actually plated")).toBeNull();

    fireEvent.click(screen.getByRole("radio", { name: /Growth matching expected appearance/ }));
    fireEvent.click(screen.getByRole("button", { name: /Submit/ }));

    await waitFor(() => expect(service.submitConfirmatoryObservations).toHaveBeenCalledTimes(1));
    expect(service.submitConfirmatoryObservations).toHaveBeenCalledWith(7, "Confirmatory Plating", [
      { materialId: 32, observation: "GrowthConforming" }
    ]);
    expect(onSubmitted).toHaveBeenCalled();
  });
});
