import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { screen, cleanup, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosResponse } from "axios";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../../contexts/AuthContext";
import { renderWithTheme } from "../../../components/bench/benchTestUtils";
import { draftStore } from "../../../services/drafts/draftStore";
import { makeDraft, rawStoredDrafts, resetDraftDb, signInForTest } from "../../../services/drafts/draftTestUtils";
import { PrimaryObservationMatrixPanel } from "./PrimaryObservationMatrixPanel";
import { GrowthObservation, type PathogenTestingSessionDto } from "../types/pathogenSessionTypes";

const sessionService = vi.hoisted(() => ({
  savePrimaryObservations: vi.fn(),
  saveResultMatrix: vi.fn(),
  getEligibleConfirmations: vi.fn()
}));
vi.mock("../services/PathogenSessionService", () => ({ PathogenSessionService: sessionService }));
vi.mock("../../../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
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
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

function makeSession(overrides?: Partial<PathogenTestingSessionDto>): PathogenTestingSessionDto {
  return {
    sessionId: "sess-42",
    sampleId: 42,
    sampleReferenceNumber: "SMP-042",
    category: "Water",
    programName: "Routine",
    departmentOrAreaName: "QC Lab",
    controlNumber: "CTRL-042",
    batchNumber: null,
    samplingDate: "2026-10-10",
    overallSessionStatus: "InProgress",
    overallSessionStatusDisplay: "In Progress",
    totalLocations: 1,
    totalAssignedTests: 2,
    requiredResultCount: 2,
    completedResultCount: 0,
    availableResultCount: 2,
    lockedResultCount: 0,
    pendingResultCount: 2,
    sharedTsb: {
      isStarted: true,
      isIncubating: false,
      isCompleted: true,
      isLocked: false,
      mediaLotId: 11,
      mediaLotNumber: "TSB-01",
      mediaMaterialName: "Tryptic Soy Broth",
      gptStatus: "Passed",
      sterilityStatus: "Passed",
      incubatorEquipmentId: 21,
      incubatorCode: "INC-01",
      requiredTemperatureRange: "30-35 C",
      requiredDurationRange: "18-24 h",
      temperature: "32.5 C",
      incubationDurationHours: 24,
      actualStartUtc: "2026-10-10T08:00:00Z",
      minReadyAt: "2026-10-11T02:00:00Z",
      expectedCompletionUtc: "2026-10-11T08:00:00Z",
      completedAtUtc: "2026-10-11T08:00:00Z",
      startedByUserId: 7,
      startedByUserName: "Analyst",
      applicableTestCodes: ["ECOLI", "TAMC"],
      applicableLocationCount: 1
    },
    locations: [
      {
        id: 1,
        primarySampleLocationId: 501,
        locationName: "Point A",
        locationType: "WaterPoint",
        gradeClassification: null,
        testLocationMap: { ECOLI: 501, TAMC: 502 }
      }
    ],
    assignedTests: [
      {
        testOrderId: 101,
        testCode: "ECOLI",
        displayName: "E. coli",
        workflowType: "Observation",
        status: "InProgress",
        currentStep: "Selective Plating",
        assignedAnalystName: "Analyst",
        requiresTsb: true,
        testSessionState: "ReadyForObservation",
        testSessionStateDisplay: "Ready",
        isResultEntryAllowed: true,
        isWorkflowLocked: false,
        lockReason: null,
        steps: []
      },
      {
        testOrderId: 102,
        testCode: "TAMC",
        displayName: "TAMC",
        workflowType: "CountTest",
        status: "InProgress",
        currentStep: "Plate Count",
        assignedAnalystName: "Analyst",
        requiresTsb: false,
        testSessionState: "ReadyForResult",
        testSessionStateDisplay: "Ready",
        isResultEntryAllowed: true,
        isWorkflowLocked: false,
        lockReason: null,
        steps: []
      }
    ],
    resultMatrix: [],
    missingResults: [],
    ...overrides
  };
}

function renderPanel(session: PathogenTestingSessionDto, bench = false) {
  return renderWithTheme(
    <AuthProvider>
      <MemoryRouter>
        <PrimaryObservationMatrixPanel session={session} onUpdated={vi.fn()} onNext={vi.fn()} />
      </MemoryRouter>
    </AuthProvider>,
    { bench }
  );
}

describe("PrimaryObservationMatrixPanel", () => {
  it("payload to savePrimaryObservations and saveResultMatrix is unchanged", async () => {
    const user = userEvent.setup();
    const session = makeSession();
    sessionService.savePrimaryObservations.mockResolvedValue(session);
    sessionService.saveResultMatrix.mockResolvedValue(session);
    sessionService.getEligibleConfirmations.mockResolvedValue([]);

    renderPanel(session, false);

    // Wait for initial table row
    expect(await screen.findByText("Point A")).toBeTruthy();

    // Select qualitative observation for E. coli
    const ecoliInput = screen.getByLabelText("E. coli observation for Point A");
    const ecoliSelect = ecoliInput.closest(".MuiInputBase-root")!.querySelector('[role="combobox"]')!;
    await user.click(ecoliSelect);
    await user.click(within(await screen.findByRole("listbox")).getByText("No Growth (-)"));

    // Enter quantitative count for TAMC
    const tamcInput = screen.getByLabelText("TAMC count (CFU) for Point A");
    await user.type(tamcInput, "15");

    await user.click(screen.getByRole("button", { name: "Save Observations" }));

    await waitFor(() => expect(sessionService.savePrimaryObservations).toHaveBeenCalledTimes(1));
    expect(sessionService.savePrimaryObservations).toHaveBeenCalledWith(42, {
      observations: [
        {
          sampleLocationId: 501,
          testCode: "ECOLI",
          observation: GrowthObservation.NoGrowth
        }
      ]
    });

    await waitFor(() => expect(sessionService.saveResultMatrix).toHaveBeenCalledTimes(1));
    expect(sessionService.saveResultMatrix).toHaveBeenCalledWith(42, {
      cells: [
        {
          sampleLocationId: 502,
          testCode: "TAMC",
          resultCode: "15",
          resultDisplay: "15 CFU",
          numericValue: 15,
          resultType: "Quantitative"
        }
      ]
    });
  });

  it("observation choices render as one column of large labelled options in bench mode", async () => {
    const session = makeSession();
    renderPanel(session, true);

    expect(await screen.findByText("Point A")).toBeTruthy();
    const group = screen.getByRole("radiogroup");
    expect(group).toBeTruthy();
    expect(within(group).getByRole("radio", { name: "No Growth (-)" })).toBeTruthy();
    expect(within(group).getByRole("radio", { name: "Growth Non-Conforming" })).toBeTruthy();
    expect(within(group).getByRole("radio", { name: "Growth Conforming (+)" })).toBeTruthy();
  });

  it("matrix shows one plate per row below 1100 px and the existing table above", async () => {
    const session = makeSession();
    // Above 1100 px (default jsdom matchMedia false)
    renderPanel(session, true);
    expect(await screen.findByText("Point A")).toBeTruthy();
    expect(screen.getByRole("table")).toBeTruthy();
    cleanup();

    // Below 1100 px
    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: query.includes("1099.95"),
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn()
    }));

    renderPanel(session, true);
    expect(await screen.findByText("Point A")).toBeTruthy();
    expect(screen.queryByRole("table")).toBeNull();
    expect(screen.getByText("E. coli (ECOLI)")).toBeTruthy();
    expect(screen.getByText("TAMC (TAMC)")).toBeTruthy();
  });

  it("count cell uses NumericEntryField with the kind the result type requires", async () => {
    const user = userEvent.setup();
    const session = makeSession();
    renderPanel(session, true);

    expect(await screen.findByText("Point A")).toBeTruthy();
    const tamcInput = screen.getByLabelText("TAMC count (CFU) for Point A") as HTMLInputElement;
    // The backend stores the matrix value as a decimal CFU result and does not
    // require a whole number (PathogenSessionService, NumericValue decimal?), so
    // the field keeps decimals and checks the format only.
    expect(tamcInput.getAttribute("inputmode")).toBe("decimal");

    await user.type(tamcInput, "1.5");
    expect(screen.queryByText(/Enter a number using digits/)).toBeNull();

    await user.clear(tamcInput);
    await user.type(tamcInput, "1.2.3");
    expect(await screen.findByText(/Enter a number using digits, at most one decimal point/)).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Save Observations" }));
    expect(sessionService.saveResultMatrix).not.toHaveBeenCalled();
  });

  it("gated action shows its reason as text", async () => {
    const session = makeSession({
      assignedTests: [
        {
          ...makeSession().assignedTests[0],
          isResultEntryAllowed: false,
          lockReason: "Waiting for TSB enrichment"
        }
      ],
      totalAssignedTests: 1
    });
    renderPanel(session, true);

    expect(await screen.findByText("Point A")).toBeTruthy();
    expect(screen.getByText("Waiting for TSB enrichment")).toBeTruthy();
    expect(screen.getByText("All assigned tests are locked until upstream workflow steps complete.")).toBeTruthy();
  });

  it("restores draft observations, sends X-Draft-Started-At, keeps draft on network error, and marks 409 as rejected", async () => {
    const user = userEvent.setup();
    const session = makeSession();
    await draftStore.put(
      makeDraft({
        userId: 7,
        formKey: "pathogen-primary-matrix",
        recordKey: "42",
        startedAtDevice: "2020-01-01T00:00:00.000Z",
        values: {
          entries: {
            "501_ECOLI": { observation: GrowthObservation.NoGrowth, numericText: "" },
            "502_TAMC": { observation: "", numericText: "12" }
          }
        }
      })
    );

    sessionService.savePrimaryObservations.mockRejectedValueOnce(networkError());
    renderPanel(session, true);

    expect(await screen.findByText("Point A")).toBeTruthy();
    await user.click(await screen.findByRole("button", { name: "Restore entry" }));

    const tamcInput = screen.getByLabelText("TAMC count (CFU) for Point A") as HTMLInputElement;
    expect(tamcInput.value).toBe("12");

    // Network error keeps draft and shows offline notice
    await user.click(screen.getByRole("button", { name: "Save Observations" }));
    expect(await screen.findByText(/kept on this tablet/i)).toBeTruthy();
    expect(sessionService.savePrimaryObservations).toHaveBeenCalledWith(
      42,
      { observations: [{ sampleLocationId: 501, testCode: "ECOLI", observation: GrowthObservation.NoGrowth }] },
      { headers: { "X-Draft-Started-At": "2020-01-01T00:00:00.000Z" } }
    );
    expect(await rawStoredDrafts()).toHaveLength(1);

    // 409 marks draft rejected and never retries
    sessionService.savePrimaryObservations.mockRejectedValueOnce(conflict("Changed on server"));
    await user.click(screen.getByRole("button", { name: "Try saving again" }));
    expect(await screen.findByText("Changed on server")).toBeTruthy();
    expect(sessionService.savePrimaryObservations).toHaveBeenCalledTimes(2);
    const stored = (await rawStoredDrafts()) as { state: string }[];
    expect(stored[0].state).toBe("rejected");
  });
});
