import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, within, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../theme";
import { labelService, type ScanMatch, type ScanResolveResponse } from "../../../services/labelService";
import { BrothStepPanel } from "./BrothStepPanel";
import { StepChainStrip } from "../components/StepChainStrip";
import type { CurrentStepResponse, TestWorkflowStepDto } from "../types/testWorkflowTypes";

const service = vi.hoisted(() => ({
  getPermittedConfirmatoryMedia: vi.fn(),
  getEligibleIncubators: vi.fn(),
  getSiblingPathogenOrders: vi.fn()
}));
vi.mock("../services/TestWorkflowService", () => ({ TestWorkflowService: service }));
vi.mock("../../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

function makeScanResponse(match: Partial<ScanMatch>): ScanResolveResponse {
  return {
    outcome: "Found",
    layoutCorrected: false,
    normalizedValue: match.code ?? "",
    match: {
      entityType: "MediaLot",
      id: 11,
      code: "TSB-2609-014",
      displayName: "Tryptic soy broth",
      status: "Released",
      usable: true,
      blockedReason: null,
      navigationTarget: "/media-lots?lotId=11",
      linkedEquipmentId: null,
      ...match
    },
    candidates: [],
    hint: null
  };
}

function seedBrothStepLookups() {
  service.getPermittedConfirmatoryMedia.mockResolvedValue({
    permittedMedia: [{ stepMediaId: 3, mediaProductId: 8, mediaName: "Tryptic soy broth", tempMin: 30, tempMax: 35, availableLots: [{ id: 11, lotNumber: "TSB-2609-014", expiryDate: "2026-11-28" }] }]
  });
  service.getEligibleIncubators.mockResolvedValue({ eligibleIncubators: [{ id: 21, name: "Incubator 3", code: "INC-03", setTemperature: 32.5 }] });
  service.getSiblingPathogenOrders.mockResolvedValue([]);
}

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
    const user = userEvent.setup();
    seedBrothStepLookups();

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

    await user.click(screen.getByRole("combobox", { name: /Media lot/ }));
    await user.click(within(await screen.findByRole("listbox")).getByText(/TSB-2609-014/));
    await user.click(screen.getByRole("combobox", { name: /Incubator/ }));
    await user.click(within(await screen.findByRole("listbox")).getByText(/INC-03/));

    expect(start.disabled).toBe(false);
  });

  it("scanning an offered media lot selects it", async () => {
    const user = userEvent.setup();
    seedBrothStepLookups();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeScanResponse({ entityType: "MediaLot", id: 11, code: "TSB-2609-014" })
    );

    render(
      <ThemeProvider theme={lightTheme}>
        <BrothStepPanel testOrderId={7} step={step} current={current} onSubmitted={() => {}} />
      </ThemeProvider>
    );

    expect(await screen.findByText("Tryptic soy broth")).toBeTruthy();
    await user.type(screen.getByLabelText("Scan media lot"), "MED:TSB-2609-014{Enter}");

    await waitFor(() => {
      expect(screen.getByRole("combobox", { name: /Media lot/ }).textContent).toContain("TSB-2609-014");
    });
    expect(screen.getByRole("status").textContent).toContain("Selected TSB-2609-014");
  });

  it("scanning a media lot that is not offered leaves the selection unchanged", async () => {
    const user = userEvent.setup();
    seedBrothStepLookups();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeScanResponse({ entityType: "MediaLot", id: 99, code: "TSB-OTHER-099" })
    );

    render(
      <ThemeProvider theme={lightTheme}>
        <BrothStepPanel testOrderId={7} step={step} current={current} onSubmitted={() => {}} />
      </ThemeProvider>
    );

    expect(await screen.findByText("Tryptic soy broth")).toBeTruthy();
    await user.type(screen.getByLabelText("Scan media lot"), "MED:TSB-OTHER-099{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("TSB-OTHER-099 is not available for this step.");
    expect(screen.getByRole("combobox", { name: /Media lot/ }).textContent).toContain("Select a released lot");
  });

  it("scanning an offered incubator selects it", async () => {
    const user = userEvent.setup();
    seedBrothStepLookups();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeScanResponse({
        entityType: "EquipmentAsset",
        id: 501,
        linkedEquipmentId: 21,
        code: "INC-03",
        displayName: "Incubator 3"
      })
    );

    render(
      <ThemeProvider theme={lightTheme}>
        <BrothStepPanel testOrderId={7} step={step} current={current} onSubmitted={() => {}} />
      </ThemeProvider>
    );

    expect(await screen.findByText("Tryptic soy broth")).toBeTruthy();
    await user.type(screen.getByLabelText("Scan incubator"), "EQ:INC-03{Enter}");

    await waitFor(() => {
      expect(screen.getByRole("combobox", { name: /Incubator/ }).textContent).toContain("INC-03");
    });
    expect(screen.getByRole("status").textContent).toContain("Selected INC-03");
  });

  it("scanning an incubator that is not offered leaves the selection unchanged", async () => {
    const user = userEvent.setup();
    seedBrothStepLookups();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeScanResponse({
        entityType: "EquipmentAsset",
        id: 502,
        linkedEquipmentId: 88,
        code: "INC-99",
        displayName: "Incubator 99"
      })
    );

    render(
      <ThemeProvider theme={lightTheme}>
        <BrothStepPanel testOrderId={7} step={step} current={current} onSubmitted={() => {}} />
      </ThemeProvider>
    );

    expect(await screen.findByText("Tryptic soy broth")).toBeTruthy();
    await user.type(screen.getByLabelText("Scan incubator"), "EQ:INC-99{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("INC-99 is not available for this step.");
    expect(screen.getByRole("combobox", { name: /Incubator/ }).textContent).toContain("Select an incubator");
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
