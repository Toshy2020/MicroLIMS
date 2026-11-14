import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { screen, cleanup, within, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosResponse } from "axios";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../../contexts/AuthContext";
import { renderWithTheme } from "../../../components/bench/benchTestUtils";
import { draftStore } from "../../../services/drafts/draftStore";
import { makeDraft, rawStoredDrafts, resetDraftDb, signInForTest } from "../../../services/drafts/draftTestUtils";
import { SelectivePlatingPanel } from "./SelectivePlatingPanel";
import type { CurrentStepResponse, TestWorkflowStepDto } from "../types/testWorkflowTypes";

const service = vi.hoisted(() => ({
  getPermittedConfirmatoryMedia: vi.fn(),
  getEligibleIncubators: vi.fn(),
  startSelectivePlatingIncubation: vi.fn(),
  submitSelectivePlatingObservation: vi.fn()
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
  id: 3,
  stepOrder: 3,
  stepName: "Selective Plating",
  stepType: "SelectivePlating",
  targetOrganismId: 1,
  stepMedia: [],
  incubationMinHours: 18,
  incubationMaxHours: 24,
  temperatureMin: 35,
  temperatureMax: 37,
  isFinalStep: false
} as unknown as TestWorkflowStepDto;

function seedLookups() {
  service.getPermittedConfirmatoryMedia.mockResolvedValue({
    permittedMedia: [
      {
        stepMediaId: 5,
        mediaProductId: 9,
        mediaName: "MacConkey agar",
        expectedAppearance: "Pink colonies",
        tempMin: 35,
        tempMax: 37,
        availableLots: [{ id: 12, lotNumber: "MAC-01", expiryDate: "2027-01-01", materialId: 41 }]
      }
    ]
  });
  service.getEligibleIncubators.mockResolvedValue({
    eligibleIncubators: [{ id: 22, name: "Incubator 2", code: "INC-02", setTemperature: 36 }]
  });
}

function renderPanel(current: CurrentStepResponse, bench = false, onSubmitted = vi.fn()) {
  return renderWithTheme(
    <AuthProvider>
      <MemoryRouter>
        <SelectivePlatingPanel testOrderId={7} step={step} current={current} onSubmitted={onSubmitted} />
      </MemoryRouter>
    </AuthProvider>,
    { bench }
  );
}

describe("SelectivePlatingPanel", () => {
  it("payload to startSelectivePlatingIncubation is unchanged", async () => {
    const user = userEvent.setup();
    seedLookups();
    service.startSelectivePlatingIncubation.mockResolvedValue({ ok: true });
    const onSubmitted = vi.fn();

    const setupCurrent = {
      testOrderId: 7,
      step,
      stepNumber: 3,
      totalSteps: 5,
      testName: "E. coli",
      workflowType: "Observation",
      sampleContext: { sampleName: "Tablet", batchNumber: null, controlNumber: null, reason: null, systemReferenceNumber: null, sampleType: "FinishedProduct" },
      incubationLock: null,
      previousSteps: [],
      allStepsComplete: false,
      finalResult: null,
      allSteps: [],
      completedSteps: []
    } as unknown as CurrentStepResponse;

    renderPanel(setupCurrent, false, onSubmitted);

    expect(await screen.findByText("MacConkey agar")).toBeTruthy();

    await user.click(screen.getByRole("combobox", { name: /Media lot/ }));
    await user.click(within(await screen.findByRole("listbox")).getByText(/MAC-01/));
    await user.click(screen.getByRole("combobox", { name: /Incubator/ }));
    await user.click(within(await screen.findByRole("listbox")).getByText(/INC-02/));
    await user.click(screen.getByRole("button", { name: "Start Incubation" }));

    await waitFor(() => expect(service.startSelectivePlatingIncubation).toHaveBeenCalledTimes(1));
    expect(service.startSelectivePlatingIncubation).toHaveBeenCalledWith(7, "Selective Plating", 12, 22);
    expect(onSubmitted).toHaveBeenCalled();
  });

  it("payload to submitSelectivePlatingObservation is unchanged", async () => {
    const user = userEvent.setup();
    seedLookups();
    service.submitSelectivePlatingObservation.mockResolvedValue({ outcomeSummary: "Recorded" });
    const onSubmitted = vi.fn();

    const readoutCurrent = {
      testOrderId: 7,
      step,
      stepNumber: 3,
      totalSteps: 5,
      testName: "E. coli",
      workflowType: "Observation",
      sampleContext: { sampleName: "Tablet", batchNumber: null, controlNumber: null, reason: null, systemReferenceNumber: null, sampleType: "FinishedProduct" },
      incubationLock: null,
      previousSteps: [
        {
          stepName: "Selective Plating",
          status: "Incubating",
          incubationStartUtc: "2026-10-20T08:00:00Z",
          minReadyAt: "2026-10-21T02:00:00Z"
        }
      ],
      allStepsComplete: false,
      finalResult: null,
      allSteps: [],
      completedSteps: []
    } as unknown as CurrentStepResponse;

    renderPanel(readoutCurrent, false, onSubmitted);

    expect(await screen.findByText("MacConkey agar")).toBeTruthy();

    await user.click(screen.getByRole("radio", { name: /Growth matching expected appearance/ }));
    await user.type(screen.getByLabelText(/Observed appearance note/), "Pink mucoid colonies");
    await user.click(screen.getByRole("button", { name: "Record Observation" }));

    await waitFor(() => expect(service.submitSelectivePlatingObservation).toHaveBeenCalledTimes(1));
    expect(service.submitSelectivePlatingObservation).toHaveBeenCalledWith(
      7,
      "Selective Plating",
      "GrowthConforming",
      "Pink mucoid colonies"
    );
    expect(onSubmitted).toHaveBeenCalled();
  });

  it("observation choices render as one column of large labelled options in bench mode and gated action shows reason", async () => {
    seedLookups();
    const readoutCurrent = {
      testOrderId: 7,
      step,
      stepNumber: 3,
      totalSteps: 5,
      testName: "E. coli",
      workflowType: "Observation",
      sampleContext: { sampleName: "Tablet", batchNumber: null, controlNumber: null, reason: null, systemReferenceNumber: null, sampleType: "FinishedProduct" },
      incubationLock: null,
      previousSteps: [
        {
          stepName: "Selective Plating",
          status: "Incubating",
          incubationStartUtc: "2026-10-20T08:00:00Z",
          minReadyAt: "2026-10-21T02:00:00Z"
        }
      ],
      allStepsComplete: false,
      finalResult: null,
      allSteps: [],
      completedSteps: []
    } as unknown as CurrentStepResponse;

    renderPanel(readoutCurrent, true);

    expect(await screen.findByText("MacConkey agar")).toBeTruthy();
    const group = screen.getByRole("radiogroup");
    expect(within(group).getByRole("radio", { name: /No growth/ })).toBeTruthy();
    expect(within(group).getByRole("radio", { name: /Growth, does not match expected appearance/ })).toBeTruthy();
    expect(within(group).getByRole("radio", { name: /Growth matching expected appearance/ })).toBeTruthy();

    expect((screen.getByRole("button", { name: "Record Observation" }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByText("Select an observation to continue.")).toBeTruthy();
  });

  it("restores readout draft, sends X-Draft-Started-At, keeps on network error, and marks 409 as rejected", async () => {
    const user = userEvent.setup();
    seedLookups();
    await draftStore.put(
      makeDraft({
        userId: 7,
        formKey: "pathogen-selective-readout",
        recordKey: "7:Selective Plating",
        startedAtDevice: "2020-01-01T00:00:00.000Z",
        values: { observation: "NoGrowth", appearanceNote: "Clear plate" }
      })
    );
    service.submitSelectivePlatingObservation.mockRejectedValueOnce(networkError());

    const readoutCurrent = {
      testOrderId: 7,
      step,
      stepNumber: 3,
      totalSteps: 5,
      testName: "E. coli",
      workflowType: "Observation",
      sampleContext: { sampleName: "Tablet", batchNumber: null, controlNumber: null, reason: null, systemReferenceNumber: null, sampleType: "FinishedProduct" },
      incubationLock: null,
      previousSteps: [
        {
          stepName: "Selective Plating",
          status: "Incubating",
          incubationStartUtc: "2026-10-20T08:00:00Z",
          minReadyAt: "2026-10-21T02:00:00Z"
        }
      ],
      allStepsComplete: false,
      finalResult: null,
      allSteps: [],
      completedSteps: []
    } as unknown as CurrentStepResponse;

    renderPanel(readoutCurrent, true);

    expect(await screen.findByText("MacConkey agar")).toBeTruthy();
    await user.click(await screen.findByRole("button", { name: "Restore entry" }));

    await user.click(screen.getByRole("button", { name: "Record Observation" }));
    expect(await screen.findByText(/kept on this tablet/i)).toBeTruthy();
    expect(service.submitSelectivePlatingObservation).toHaveBeenCalledWith(
      7,
      "Selective Plating",
      "NoGrowth",
      "Clear plate",
      { headers: { "X-Draft-Started-At": "2020-01-01T00:00:00.000Z" } }
    );
    expect(await rawStoredDrafts()).toHaveLength(1);

    service.submitSelectivePlatingObservation.mockRejectedValueOnce(conflict("Already recorded"));
    await user.click(screen.getByRole("button", { name: "Try saving again" }));
    expect(await screen.findByText("Already recorded")).toBeTruthy();
    expect(service.submitSelectivePlatingObservation).toHaveBeenCalledTimes(2);
    const stored = (await rawStoredDrafts()) as { state: string }[];
    expect(stored[0].state).toBe("rejected");
  });
});
