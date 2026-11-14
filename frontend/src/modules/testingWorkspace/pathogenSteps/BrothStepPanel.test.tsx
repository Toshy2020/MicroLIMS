import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { screen, cleanup, within, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosResponse } from "axios";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../../contexts/AuthContext";
import { renderWithTheme } from "../../../components/bench/benchTestUtils";
import { draftStore } from "../../../services/drafts/draftStore";
import { makeDraft, rawStoredDrafts, resetDraftDb, signInForTest } from "../../../services/drafts/draftTestUtils";
import { labelService, type ScanMatch, type ScanResolveResponse } from "../../../services/labelService";
import { BrothStepPanel } from "./BrothStepPanel";
import { StepChainStrip } from "../components/StepChainStrip";
import type { CurrentStepResponse, TestWorkflowStepDto } from "../types/testWorkflowTypes";

const service = vi.hoisted(() => ({
  getPermittedConfirmatoryMedia: vi.fn(),
  getEligibleIncubators: vi.fn(),
  getSiblingPathogenOrders: vi.fn(),
  submitBroth: vi.fn(),
  selectMedia: vi.fn()
}));
vi.mock("../services/TestWorkflowService", () => ({ TestWorkflowService: service }));
vi.mock("../../../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
}));
vi.mock("../../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

const networkError = () => new AxiosError("Network Error", "ERR_NETWORK");
const conflict = (message: string) =>
  new AxiosError("Request failed", "ERR_BAD_REQUEST", undefined, undefined, {
    status: 409,
    statusText: "Conflict",
    data: { message },
    headers: {},
    config: {}
  } as AxiosResponse);

beforeEach(async () => {
  localStorage.clear();
  await resetDraftDb();
  signInForTest(7);
});

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

function renderBroth(props: { testOrderId: number; step: TestWorkflowStepDto; current: CurrentStepResponse; onSubmitted: () => void }, bench = false) {
  return renderWithTheme(
    <AuthProvider>
      <MemoryRouter>
        <BrothStepPanel {...props} />
      </MemoryRouter>
    </AuthProvider>,
    { bench }
  );
}

describe("BrothStepPanel", () => {
  it("keeps Start Incubation disabled until a lot and an incubator are chosen, and prefetches sibling orders", async () => {
    const user = userEvent.setup();
    seedBrothStepLookups();

    renderBroth({ testOrderId: 7, step, current, onSubmitted: () => {} });

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

    renderBroth({ testOrderId: 7, step, current, onSubmitted: () => {} });

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

    renderBroth({ testOrderId: 7, step, current, onSubmitted: () => {} });

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

    renderBroth({ testOrderId: 7, step, current, onSubmitted: () => {} });

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

    renderBroth({ testOrderId: 7, step, current, onSubmitted: () => {} });

    expect(await screen.findByText("Tryptic soy broth")).toBeTruthy();
    await user.type(screen.getByLabelText("Scan incubator"), "EQ:INC-99{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("INC-99 is not available for this step.");
    expect(screen.getByRole("combobox", { name: /Incubator/ }).textContent).toContain("Select an incubator");
  });

  it("payload to submitBroth is unchanged when proceeding with shared TSB", async () => {
    const user = userEvent.setup();
    seedBrothStepLookups();
    service.submitBroth.mockResolvedValue({ outcomeSummary: "Recorded" });
    const onSubmitted = vi.fn();

    const sharedCurrent = {
      ...current,
      sharedTsbSummary: {
        mediaLotNumber: "TSB-2609-014",
        incubatorCode: "INC-03",
        incubationStartUtc: "2026-10-27T08:00:00Z",
        incubationEndUtc: "2026-10-28T08:00:00Z",
        minReadyAt: "2026-10-28T02:00:00Z",
        startedByUserName: "Analyst",
        isCompleted: true,
        windowNotConfigured: false
      }
    } as unknown as CurrentStepResponse;

    renderBroth({ testOrderId: 7, step, current: sharedCurrent, onSubmitted });

    const proceed = await screen.findByRole("button", { name: /Proceed to Next Step/ });
    await user.click(proceed);

    await waitFor(() => expect(service.submitBroth).toHaveBeenCalledTimes(1));
    expect(service.submitBroth).toHaveBeenCalledWith(7, "Broth enrichment", null);
    expect(onSubmitted).toHaveBeenCalled();
  });

  it("restores draft in bench mode, keeps on network error, and marks 409 as rejected", async () => {
    const user = userEvent.setup();
    seedBrothStepLookups();
    await draftStore.put(
      makeDraft({
        userId: 7,
        formKey: "pathogen-broth",
        recordKey: "7:Broth enrichment",
        startedAtDevice: "2020-01-01T00:00:00.000Z",
        values: { mediaLotId: 11, equipmentId: 21 }
      })
    );
    service.selectMedia.mockRejectedValueOnce(networkError());

    renderBroth({ testOrderId: 7, step, current, onSubmitted: () => {} }, true);

    expect(await screen.findByText("Tryptic soy broth")).toBeTruthy();
    await user.click(await screen.findByRole("button", { name: "Restore entry" }));

    await user.click(screen.getByRole("button", { name: "Start Incubation" }));
    expect(await screen.findByText(/kept on this tablet/i)).toBeTruthy();
    expect(service.selectMedia).toHaveBeenCalledWith(
      7,
      "Broth enrichment",
      11,
      21,
      { headers: { "X-Draft-Started-At": "2020-01-01T00:00:00.000Z" } }
    );
    expect(await rawStoredDrafts()).toHaveLength(1);

    service.selectMedia.mockRejectedValueOnce(conflict("Stale broth record"));
    await user.click(screen.getByRole("button", { name: "Try saving again" }));
    expect(await screen.findByText("Stale broth record")).toBeTruthy();
    expect(service.selectMedia).toHaveBeenCalledTimes(2);
    const stored = (await rawStoredDrafts()) as { state: string }[];
    expect(stored[0].state).toBe("rejected");
  });
});

describe("StepChainStrip", () => {
  it("shows the current position and labels each step with its state", () => {
    renderWithTheme(<StepChainStrip current={current} />, { bench: false });
    expect(screen.getByText("Step 1 of 3")).toBeTruthy();
    expect(screen.getByLabelText("Broth enrichment: In progress").getAttribute("aria-current")).toBe("step");
    expect(screen.getByLabelText("MBP: Not started")).toBeTruthy();
  });
});
