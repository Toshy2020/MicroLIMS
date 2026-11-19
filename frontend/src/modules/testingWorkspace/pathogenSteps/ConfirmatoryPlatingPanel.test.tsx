import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { screen, cleanup, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosResponse } from "axios";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../../contexts/AuthContext";
import { renderWithTheme } from "../../../components/bench/benchTestUtils";
import { draftStore } from "../../../services/drafts/draftStore";
import { makeDraft, rawStoredDrafts, resetDraftDb, signInForTest } from "../../../services/drafts/draftTestUtils";
import { ConfirmatoryPlatingPanel } from "./ConfirmatoryPlatingPanel";
import type { CurrentStepResponse, TestWorkflowStepDto } from "../types/testWorkflowTypes";

const service = vi.hoisted(() => ({
  getPermittedConfirmatoryMedia: vi.fn(),
  getEligibleIncubators: vi.fn(),
  submitConfirmatorySetup: vi.fn(),
  submitConfirmatoryObservations: vi.fn(),
  recordAnalystDecision: vi.fn()
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

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const step = {
  id: 4,
  stepOrder: 4,
  stepName: "Confirmatory Plating",
  stepType: "ConfirmatoryPlating",
  targetOrganismId: 1,
  stepMedia: [],
  incubationMinHours: 18,
  incubationMaxHours: 24,
  temperatureMin: 35,
  temperatureMax: 37,
  isFinalStep: false
} as unknown as TestWorkflowStepDto;

// Plating already started (an open incubation row, minimum elapsed): the panel opens on the readout.
const readoutCurrent = {
  testOrderId: 7,
  step,
  stepNumber: 4,
  totalSteps: 5,
  testName: "Salmonella",
  workflowType: "Observation",
  sampleContext: { sampleName: "Tablet", batchNumber: null, controlNumber: null, reason: null, systemReferenceNumber: null, sampleType: "FinishedProduct" },
  incubationLock: null,
  previousSteps: [{ stepName: "Confirmatory Plating", status: "Incubating", incubationStartUtc: "2026-10-27T08:00:00Z" }],
  allStepsComplete: false,
  finalResult: null,
  allSteps: [],
  completedSteps: []
} as unknown as CurrentStepResponse;

const setupCurrent = {
  ...readoutCurrent,
  previousSteps: []
} as unknown as CurrentStepResponse;

function seedReadoutMedia() {
  service.getPermittedConfirmatoryMedia.mockResolvedValue({
    organism: { id: 1, name: "Salmonella" },
    permittedMedia: [
      {
        stepMediaId: 3,
        mediaProductId: 8,
        mediaName: "XLD agar",
        expectedAppearance: "Red colonies",
        tempMin: 35,
        tempMax: 37,
        availableLots: [
          { id: 101, lotNumber: "XLD/01/26", expiryDate: "2026-12-01", materialId: 31 },
          { id: 102, lotNumber: "XLD/02/26", expiryDate: "2026-12-15", materialId: 32 }
        ],
        selection: { materialId: 32, mediaId: 102, lotNumber: "XLD/02/26", equipmentId: 21 }
      }
    ]
  });
  service.getEligibleIncubators.mockResolvedValue({
    eligibleIncubators: [{ id: 21, name: "Incubator 1", code: "INC-01", setTemperature: 36 }]
  });
}

function renderPanel(current: CurrentStepResponse, bench = false, onSubmitted = vi.fn()) {
  return renderWithTheme(
    <AuthProvider>
      <MemoryRouter>
        <ConfirmatoryPlatingPanel testOrderId={7} step={step} current={current} onSubmitted={onSubmitted} />
      </MemoryRouter>
    </AuthProvider>,
    { bench }
  );
}

describe("ConfirmatoryPlatingPanel", () => {
  it("after a reload, records the readout against the material of the lot that was plated (second of two lots of one product)", async () => {
    const user = userEvent.setup();
    seedReadoutMedia();
    service.submitConfirmatoryObservations.mockResolvedValue({ analystDecisionRequired: false });
    const onSubmitted = vi.fn();

    renderPanel(readoutCurrent, false, onSubmitted);

    expect(await screen.findByText("XLD agar")).toBeTruthy();
    // The server knows what was plated, so there is no "was it actually plated" confirmation.
    expect(screen.queryByText("This medium was actually plated")).toBeNull();

    await user.click(screen.getByRole("radio", { name: /Growth matching expected appearance/ }));
    await user.click(screen.getByRole("button", { name: /Submit/ }));

    await waitFor(() => expect(service.submitConfirmatoryObservations).toHaveBeenCalledTimes(1));
    expect(service.submitConfirmatoryObservations).toHaveBeenCalledWith(7, "Confirmatory Plating", [
      { materialId: 32, observation: "GrowthConforming" }
    ]);
    expect(onSubmitted).toHaveBeenCalled();
  });

  it("payload to submitConfirmatorySetup is unchanged", async () => {
    const user = userEvent.setup();
    seedReadoutMedia();
    service.submitConfirmatorySetup.mockResolvedValue({ outcomeSummary: "Started" });
    const onSubmitted = vi.fn();

    renderPanel(setupCurrent, false, onSubmitted);

    expect(await screen.findByText("XLD agar")).toBeTruthy();

    await user.click(screen.getByRole("checkbox", { name: "XLD agar" }));
    await user.click(screen.getByRole("combobox", { name: /Media lot/ }));
    await user.click(await screen.findByText(/XLD\/02\/26/));
    await user.click(screen.getByRole("combobox", { name: /Incubator/ }));
    await user.click(await screen.findByText(/INC-01/));
    await user.click(screen.getByRole("button", { name: "Start Incubation" }));

    await waitFor(() => expect(service.submitConfirmatorySetup).toHaveBeenCalledTimes(1));
    const [orderId, stepName, selections, startUtc, endUtc] = service.submitConfirmatorySetup.mock.calls[0];
    expect(orderId).toBe(7);
    expect(stepName).toBe("Confirmatory Plating");
    expect(selections).toEqual([{ stepMediaId: 3, mediaLotId: 102, equipmentId: 21 }]);
    expect(typeof startUtc).toBe("string");
    expect(typeof endUtc).toBe("string");
    expect(onSubmitted).toHaveBeenCalled();
  });

  it("payload to recordAnalystDecision is unchanged", async () => {
    const user = userEvent.setup();
    seedReadoutMedia();
    service.submitConfirmatoryObservations.mockResolvedValue({ analystDecisionRequired: true });
    service.recordAnalystDecision.mockResolvedValue({ outcomeSummary: "Proceeding" });
    const onSubmitted = vi.fn();

    renderPanel(readoutCurrent, false, onSubmitted);

    expect(await screen.findByText("XLD agar")).toBeTruthy();
    await user.click(screen.getByRole("radio", { name: /Growth matching expected appearance/ }));
    await user.click(screen.getByRole("button", { name: /Submit/ }));

    const proceedBtn = await screen.findByRole("button", { name: "Proceed to Biochemical Confirmation" });
    await user.click(proceedBtn);

    await waitFor(() => expect(service.recordAnalystDecision).toHaveBeenCalledTimes(1));
    expect(service.recordAnalystDecision).toHaveBeenCalledWith(7, "ProceedToBiochemical");
    expect(onSubmitted).toHaveBeenCalled();
  });

  it("observation choices render as one column of large labelled options in bench mode and gated action shows reason", async () => {
    seedReadoutMedia();
    renderPanel(readoutCurrent, true);

    expect(await screen.findByText("XLD agar")).toBeTruthy();
    const group = screen.getByRole("radiogroup");
    expect(within(group).getByRole("radio", { name: /No growth/ })).toBeTruthy();
    expect(within(group).getByRole("radio", { name: /Growth, does not match expected appearance/ })).toBeTruthy();
    expect(within(group).getByRole("radio", { name: /Growth matching expected appearance/ })).toBeTruthy();

    expect((screen.getByRole("button", { name: "Submit Observations" }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByText("Record an observation for every plated medium to continue.")).toBeTruthy();
  });

  it("drafts restore observations but never an analyst decision, keep on network error, and mark 409 as rejected", async () => {
    const user = userEvent.setup();
    seedReadoutMedia();
    await draftStore.put(
      makeDraft({
        userId: 7,
        formKey: "pathogen-confirmatory-readout",
        recordKey: "7:Confirmatory Plating",
        startedAtDevice: "2020-01-01T00:00:00.000Z",
        values: { observations: { 32: "GrowthConforming" }, readoutChecked: {} }
      })
    );
    service.submitConfirmatoryObservations.mockRejectedValueOnce(networkError());

    renderPanel(readoutCurrent, true);

    expect(await screen.findByText("XLD agar")).toBeTruthy();
    await user.click(await screen.findByRole("button", { name: "Restore entry" }));

    await user.click(screen.getByRole("button", { name: "Submit Observations" }));
    expect(await screen.findByText(/kept on this tablet/i)).toBeTruthy();
    expect(service.submitConfirmatoryObservations).toHaveBeenCalledWith(
      7,
      "Confirmatory Plating",
      [{ materialId: 32, observation: "GrowthConforming" }],
      { headers: { "X-Draft-Started-At": "2020-01-01T00:00:00.000Z" } }
    );
    expect(await rawStoredDrafts()).toHaveLength(1);

    // 409 keeps draft as rejected and never retries
    service.submitConfirmatoryObservations.mockRejectedValueOnce(conflict("Stale confirmatory state"));
    await user.click(screen.getByRole("button", { name: "Try saving again" }));
    expect(await screen.findByText("Stale confirmatory state")).toBeTruthy();
    expect(service.submitConfirmatoryObservations).toHaveBeenCalledTimes(2);
    const stored = (await rawStoredDrafts()) as { state: string; values: Record<string, unknown> }[];
    expect(stored[0].state).toBe("rejected");
    expect("decision" in stored[0].values).toBe(false);

    // Entering decision phase never offers a draft restore notice
    service.submitConfirmatoryObservations.mockResolvedValueOnce({ analystDecisionRequired: true });
    await user.click(screen.getByRole("button", { name: "Submit Observations" }));
    expect(await screen.findByRole("button", { name: "Conclude as Detected" })).toBeTruthy();
    expect(screen.queryByRole("region", { name: "Unsaved entry found" })).toBeNull();
  });
});
