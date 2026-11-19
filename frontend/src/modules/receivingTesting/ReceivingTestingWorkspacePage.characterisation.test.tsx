import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { ReceiveService } from "../receiving/services/ReceiveService";
import type { TestingWorkspaceFilter } from "../receiving/services/ReceiveService";
import type { SampleRecord } from "../receiving/types/receivingTypes";
import { masterDataOptions } from "../../services/masterDataOptions";
import { labelService } from "../../services/labelService";
import { lookupCache } from "../../services/lookupCache";
import { TestWorkflowService } from "../testingWorkspace/services/TestWorkflowService";
import { SamplePreparationService } from "../testPreparation/services/SamplePreparationService";
import type { ActionableGroup } from "../testingWorkspace/types/testWorkflowTypes";
import { ReceivingTestingWorkspacePage } from "./ReceivingTestingWorkspacePage";

// Characterisation of today's workspace page (smarter-tables Task 9, step 1):
// the filter object sent to the server, the grouped-actions hand-off and the
// batch-select-media payload. These must keep passing unchanged after the
// page moves onto the shared table state.

vi.mock("../receiving/services/ReceiveService", () => ({
  ReceiveService: {
    getRecordsPaged: vi.fn(),
    getWorkloadCounts: vi.fn(),
    getSample: vi.fn(),
    receiveBatch: vi.fn(),
    receiveAfterCleaning: vi.fn(),
    receiveItemBased: vi.fn(),
    receiveWater: vi.fn(),
    receiveEM: vi.fn(),
    receiptLabs: vi.fn(),
    itemGrades: vi.fn(),
    uploadSampleDocument: vi.fn()
  }
}));

vi.mock("../../services/masterDataOptions", () => ({
  masterDataOptions: {
    getCausesOfTesting: vi.fn(),
    getSamplers: vi.fn(),
    getProductionStages: vi.fn(),
    getWaterSamplingPoints: vi.fn(),
    getWaterDepartments: vi.fn(),
    getDepartments: vi.fn(),
    getMachines: vi.fn(),
    getItems: vi.fn()
  }
}));

vi.mock("../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

vi.mock("../../services/lookupCache", () => ({
  lookupCache: {
    getIncubators: vi.fn(),
    getReleasedMedia: vi.fn(),
    invalidate: vi.fn()
  }
}));

vi.mock("../testingWorkspace/services/TestWorkflowService", () => ({
  TestWorkflowService: {
    getCurrentStep: vi.fn(),
    getActionableGroups: vi.fn(),
    batchSelectMedia: vi.fn()
  }
}));

vi.mock("../testPreparation/services/SamplePreparationService", () => ({
  SamplePreparationService: {
    getGroups: vi.fn()
  }
}));

function seedPermissions(permissions: string[], role = "Analyst") {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role, permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

function makeSample(sampleId: number, displayName: string, referenceNumber: string): SampleRecord {
  return {
    id: sampleId,
    sampleId,
    referenceNumber,
    category: "FinishedProduct",
    displayName,
    departmentId: null,
    machineId: null,
    waterDepartmentId: null,
    productionStage: null,
    causeOfTesting: "Routine",
    batchNumber: `BATCH-${sampleId}`,
    controlNumber: `CTRL-${sampleId}`,
    status: "InTesting",
    preparationStatus: "Completed",
    receivedAt: "2026-11-13T08:00:00Z",
    sampledBy: "Analyst Alice",
    assignedTests: [
      {
        testOrderId: sampleId * 10,
        sectionId: 1,
        testCode: "TAMC",
        status: "InProgress",
        workflowStatus: "InProgress",
        locationCount: 0,
        assignedAnalystId: 5,
        assignedAnalystName: "Analyst Alice"
      }
    ]
  };
}

const SAMPLES = [
  makeSample(91, "Alpha Syrup", "FP-0091"),
  makeSample(92, "Beta Tablet", "FP-0092"),
  makeSample(93, "Gamma Cream", "FP-0093")
];

const LAB = { sectionId: 2, code: "FP", name: "Physicochemical Laboratory", area: "rmpm" } as const;

const GROUP: ActionableGroup = {
  groupKey: "plating-selective",
  actionType: "Setup",
  stepType: "Plating",
  stepName: "Selective Plating",
  testCode: "TAMC",
  sampleCount: 2,
  testOrderCount: 2,
  tempMin: 30,
  tempMax: 35,
  incubationMinHours: 24,
  incubationMaxHours: 48,
  permittedMaterialIds: [11],
  permittedMaterialNames: "TSA",
  urgency: "Normal",
  transitionType: "TRANSFER_SELECTIVE",
  transitionLabel: "Selective Plating Setup",
  targetStepName: "Selective Agar",
  predecessorStepName: "Enrichment",
  testOrders: [
    { testOrderId: 910, sampleId: 91, sampleReference: "FP-0091", displayName: "Alpha Syrup", testCode: "TAMC" },
    { testOrderId: 920, sampleId: 92, sampleReference: "FP-0092", displayName: "Beta Tablet", testCode: "TAMC" }
  ]
};

function renderWorkspace(initialUrl = "/") {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={[initialUrl]}>
          <ReceivingTestingWorkspacePage lab={LAB} />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const DEFAULT_FILTER: TestingWorkspaceFilter = {
  page: 1,
  pageSize: 50,
  search: "",
  category: "ALL",
  sampleStatus: "ALL",
  testStatus: "ALL",
  analystId: null,
  urgency: "",
  fromDate: "",
  toDate: "",
  workloadFilter: null,
  labSectionId: 2,
  area: "rmpm"
};

const lastFilter = () => vi.mocked(ReceiveService.getRecordsPaged).mock.lastCall?.[0];

async function waitForFilter(expected: TestingWorkspaceFilter) {
  await waitFor(() => expect(lastFilter()).toStrictEqual(expected));
}

describe("ReceivingTestingWorkspacePage filter sent to the server (characterisation)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.SAMPLES_ASSIGN_ANALYST]);

    vi.mocked(ReceiveService.getRecordsPaged).mockResolvedValue({
      items: SAMPLES,
      totalCount: 120,
      page: 1,
      pageSize: 50,
      totalPages: 3
    });
    vi.mocked(ReceiveService.getWorkloadCounts).mockResolvedValue({
      needsPreparation: 0, readyToRead: 4, awaitingReview: 0, overdue: 2, mine: 0, unassigned: 0
    });
    vi.mocked(ReceiveService.getSample).mockResolvedValue(null);
    vi.mocked(masterDataOptions.getCausesOfTesting).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getSamplers).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getProductionStages).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getWaterSamplingPoints).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getWaterDepartments).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getDepartments).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getMachines).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getItems).mockResolvedValue([]);
    vi.mocked(labelService.getStatus).mockResolvedValue([]);
    vi.mocked(lookupCache.getIncubators).mockResolvedValue([]);
    vi.mocked(lookupCache.getReleasedMedia).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("offers no unaudited filter-bar export; CSV export is only the audited table action on selected rows", async () => {
    const user = userEvent.setup();
    seedPermissions([PERMISSIONS.SAMPLES_ASSIGN_ANALYST, PERMISSIONS.DATA_EXPORT]);
    renderWorkspace();
    await screen.findByText("Alpha Syrup");
    expect(screen.queryByRole("button", { name: /export/i })).toBeNull();

    const row = screen.getByText("Alpha Syrup").closest("tr");
    expect(row).not.toBeNull();
    await user.click(within(row as HTMLElement).getByRole("checkbox"));
    expect(await screen.findByRole("button", { name: "Export selected" })).toBeTruthy();
  });

  it("sends the lab scope and the default filter object on first load", async () => {
    renderWorkspace();
    await screen.findByText("Alpha Syrup");
    await waitForFilter(DEFAULT_FILTER);
    expect(ReceiveService.getWorkloadCounts).toHaveBeenCalledWith(2, "rmpm");
  });

  it("builds the whole filter object from URL parameters", async () => {
    renderWorkspace(
      "/?search=amox&category=FinishedProduct&sampleStatus=InTesting&testStatus=ReadyToRead&analystId=5&urgency=Urgent" +
      "&fromDate=2026-11-01&toDate=2026-11-10&workload=readyToRead&page=2&pageSize=25"
    );
    await screen.findByText("Alpha Syrup");
    await waitForFilter({
      page: 2,
      pageSize: 25,
      search: "amox",
      category: "FinishedProduct",
      sampleStatus: "InTesting",
      testStatus: "ReadyToRead",
      analystId: 5,
      urgency: "Urgent",
      fromDate: "2026-11-01",
      toDate: "2026-11-10",
      workloadFilter: "readyToRead",
      labSectionId: 2,
      area: "rmpm"
    });
  });

  it("?status=UnderReview becomes the sample status filter; ?status=Active alone leaves the defaults", async () => {
    const first = renderWorkspace("/?status=UnderReview");
    await screen.findByText("Alpha Syrup");
    await waitForFilter({ ...DEFAULT_FILTER, sampleStatus: "UnderReview" });
    first.unmount();

    vi.clearAllMocks();
    vi.mocked(ReceiveService.getRecordsPaged).mockResolvedValue({ items: SAMPLES, totalCount: 3, page: 1, pageSize: 50, totalPages: 1 });
    vi.mocked(ReceiveService.getWorkloadCounts).mockResolvedValue({ needsPreparation: 0, readyToRead: 0, awaitingReview: 0, overdue: 0, mine: 0, unassigned: 0 });
    renderWorkspace("/?status=Active");
    await screen.findByText("Alpha Syrup");
    await waitForFilter(DEFAULT_FILTER);
  });

  it("today an explicit ?workload= still applies next to ?status=Active (the deep-link effect re-applies it)", async () => {
    renderWorkspace("/?status=Active&workload=overdue");
    await screen.findByText("Alpha Syrup");
    await waitForFilter({ ...DEFAULT_FILTER, workloadFilter: "overdue" });
  });

  it("?scope=mine sets the Assigned-to-me workload filter", async () => {
    renderWorkspace("/?scope=mine");
    await screen.findByText("Alpha Syrup");
    await waitForFilter({ ...DEFAULT_FILTER, workloadFilter: "mine" });
  });

  // Deliberate change (smarter-tables Task 9): the table state holds 25, 50 or 100 rows a page and the
  // server never returns more than 100, so an old ?pageSize= is rounded down to one of those.
  it("pageSize in the URL is rounded down to a size the table holds (25, 50 or 100)", async () => {
    renderWorkspace("/?pageSize=5000");
    await screen.findByText("Alpha Syrup");
    await waitForFilter({ ...DEFAULT_FILTER, pageSize: 100 });
  });

  it("clicking a work-queue tile sets workloadFilter and returns to page 1; clicking it again clears it", async () => {
    const user = userEvent.setup();
    renderWorkspace("/?page=3");
    await screen.findByText("Alpha Syrup");
    await waitForFilter({ ...DEFAULT_FILTER, page: 3 });

    const tile = () => screen.getByText("Overdue").closest("button") as HTMLElement;
    await user.click(tile());
    await waitForFilter({ ...DEFAULT_FILTER, workloadFilter: "overdue" });
    expect(tile().getAttribute("aria-pressed")).toBe("true");

    await user.click(tile());
    await waitForFilter(DEFAULT_FILTER);
  });

  it("typing in search is debounced and then sent with page 1", async () => {
    const user = userEvent.setup();
    renderWorkspace("/?page=2");
    await screen.findByText("Alpha Syrup");
    await waitForFilter({ ...DEFAULT_FILTER, page: 2 });
    const callsBefore = vi.mocked(ReceiveService.getRecordsPaged).mock.calls.length;

    await user.type(screen.getByRole("textbox", { name: /Search by item/ }), "syrup");
    expect(vi.mocked(ReceiveService.getRecordsPaged).mock.calls.length).toBe(callsBefore);
    await waitForFilter({ ...DEFAULT_FILTER, search: "syrup" });
  });

  it("choosing a sample status in the filter popover sends it and returns to page 1; Clear all resets everything", async () => {
    const user = userEvent.setup();
    renderWorkspace("/?page=2&urgency=Urgent");
    await screen.findByText("Alpha Syrup");
    await waitForFilter({ ...DEFAULT_FILTER, page: 2, urgency: "Urgent" });

    await user.click(screen.getByRole("button", { name: /^Filters/ }));
    await user.click(await screen.findByRole("combobox", { name: "Sample Status" }));
    await user.click(await screen.findByRole("option", { name: "Under Testing" }));
    await waitForFilter({ ...DEFAULT_FILTER, urgency: "Urgent", sampleStatus: "InTesting" });
    await user.keyboard("{Escape}");

    await user.click(await screen.findByRole("button", { name: "Clear all" }));
    await waitForFilter(DEFAULT_FILTER);
  });

  it("the next-page button asks for page 2 with the same filters", async () => {
    const user = userEvent.setup();
    renderWorkspace("/?category=FinishedProduct");
    await screen.findByText("Alpha Syrup");
    await waitForFilter({ ...DEFAULT_FILTER, category: "FinishedProduct" });
    await user.click(screen.getAllByRole("button", { name: "Go to next page" })[0]);
    await waitForFilter({ ...DEFAULT_FILTER, category: "FinishedProduct", page: 2 });
  });
});

describe("ReceivingTestingWorkspacePage grouped actions (characterisation)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.SAMPLES_ASSIGN_ANALYST, PERMISSIONS.TEST_WORKFLOW_EXECUTE]);

    vi.mocked(ReceiveService.getRecordsPaged).mockResolvedValue({
      items: SAMPLES,
      totalCount: 3,
      page: 1,
      pageSize: 50,
      totalPages: 1
    });
    vi.mocked(ReceiveService.getWorkloadCounts).mockResolvedValue({
      needsPreparation: 0, readyToRead: 0, awaitingReview: 0, overdue: 0, mine: 0, unassigned: 0
    });
    vi.mocked(ReceiveService.getSample).mockResolvedValue(null);
    vi.mocked(masterDataOptions.getCausesOfTesting).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getSamplers).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getProductionStages).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getWaterSamplingPoints).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getWaterDepartments).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getDepartments).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getMachines).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getItems).mockResolvedValue([]);
    vi.mocked(labelService.getStatus).mockResolvedValue([]);
    vi.mocked(lookupCache.getReleasedMedia).mockResolvedValue([
      {
        id: 501, lotNumber: "LOT-A", materialId: 11, materialName: "TSA",
        expiryDate: "2099-01-01T00:00:00Z", isReleasedForUse: true, status: "Released"
      }
    ]);
    vi.mocked(lookupCache.getIncubators).mockResolvedValue([
      { id: 701, code: "INC-1", name: "Incubator One", type: "Incubator", setPointTemperature: 32, calibrationStatus: "Valid" }
    ]);
    vi.mocked(TestWorkflowService.getActionableGroups).mockResolvedValue({ groups: [GROUP] });
    vi.mocked(SamplePreparationService.getGroups).mockResolvedValue({ groups: [], excluded: [], excludedCount: 0 });
    vi.mocked(TestWorkflowService.batchSelectMedia).mockResolvedValue({
      totalRequested: 2, succeededCount: 2, skippedCount: 0, succeeded: [], skipped: []
    });
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  const check = (user: ReturnType<typeof userEvent.setup>, name: string, ref: string) =>
    user.click(screen.getByRole("checkbox", { name: `Select ${name} (${ref}) for grouped actions` }));

  it("one checked sample does not open the panel; the second does, and it receives exactly those ids", async () => {
    const user = userEvent.setup();
    renderWorkspace();
    await screen.findByText("Alpha Syrup");

    await check(user, "Alpha Syrup", "FP-0091");
    expect(screen.queryByText("Grouped Actions")).toBeNull();
    expect(TestWorkflowService.getActionableGroups).not.toHaveBeenCalled();

    await check(user, "Beta Tablet", "FP-0092");
    expect(await screen.findByText("Grouped Actions")).toBeTruthy();
    expect(screen.getByText("2 samples selected across workspace")).toBeTruthy();
    await waitFor(() => expect(TestWorkflowService.getActionableGroups).toHaveBeenCalledWith({
      scope: "all",
      actionType: undefined,
      sampleIds: "91,92"
    }));
    expect(SamplePreparationService.getGroups).toHaveBeenCalledWith([91, 92]);
  });

  it("adding a third sample re-asks for all three ids", async () => {
    const user = userEvent.setup();
    renderWorkspace();
    await screen.findByText("Alpha Syrup");
    await check(user, "Alpha Syrup", "FP-0091");
    await check(user, "Beta Tablet", "FP-0092");
    await screen.findByText("Grouped Actions");
    await check(user, "Gamma Cream", "FP-0093");
    expect(await screen.findByText("3 samples selected across workspace")).toBeTruthy();
    await waitFor(() => expect(TestWorkflowService.getActionableGroups).toHaveBeenLastCalledWith({
      scope: "all",
      actionType: undefined,
      sampleIds: "91,92,93"
    }));
  });

  it("Deselect All closes the panel and clears every checkbox", async () => {
    const user = userEvent.setup();
    renderWorkspace();
    await screen.findByText("Alpha Syrup");
    await check(user, "Alpha Syrup", "FP-0091");
    await check(user, "Beta Tablet", "FP-0092");
    await screen.findByText("Grouped Actions");

    await user.click(screen.getByRole("button", { name: "Deselect All" }));
    await waitFor(() => expect(screen.queryByText("Grouped Actions")).toBeNull());
    const boxes = screen.getAllByRole("checkbox", { name: /for grouped actions$/ }) as HTMLInputElement[];
    expect(boxes).toHaveLength(3);
    expect(boxes.every((b) => !b.checked)).toBe(true);
  });

  it("Start Incubation sends the batch-select-media payload for the group's test orders, the chosen lot and incubator", async () => {
    const user = userEvent.setup();
    renderWorkspace();
    await screen.findByText("Alpha Syrup");
    await check(user, "Alpha Syrup", "FP-0091");
    await check(user, "Beta Tablet", "FP-0092");
    await screen.findByText("Grouped Actions");

    await user.click(await screen.findByRole("button", { name: "Expand group" }));
    const submit = await screen.findByRole("button", { name: "Transfer & Start Incubation (2 Tests)" });
    await waitFor(() => expect((submit as HTMLButtonElement).disabled).toBe(false));
    await user.click(submit);

    await waitFor(() => expect(TestWorkflowService.batchSelectMedia).toHaveBeenCalledTimes(1));
    expect(vi.mocked(TestWorkflowService.batchSelectMedia).mock.calls[0][0]).toStrictEqual({
      testOrderIds: [910, 920],
      stepName: "Selective Plating",
      mediaLotId: 501,
      incubatorEquipmentId: 701,
      transitionType: "TRANSFER_SELECTIVE",
      targetStepName: "Selective Agar",
      predecessorStepName: "Enrichment"
    });
    expect(await screen.findByText("2 tests started in incubation.")).toBeTruthy();
  });

  it("an incubator-only transfer sends no media lot", async () => {
    const user = userEvent.setup();
    vi.mocked(TestWorkflowService.getActionableGroups).mockResolvedValue({
      groups: [{ ...GROUP, groupKey: "transfer", transitionType: "TRANSFER_INCUBATOR", targetStepName: undefined, predecessorStepName: "Enrichment" }]
    });
    renderWorkspace();
    await screen.findByText("Alpha Syrup");
    await check(user, "Alpha Syrup", "FP-0091");
    await check(user, "Beta Tablet", "FP-0092");
    await screen.findByText("Grouped Actions");

    await user.click(await screen.findByRole("button", { name: "Expand group" }));
    const submit = await screen.findByRole("button", { name: "Transfer to Incubator (2 Tests)" });
    await waitFor(() => expect((submit as HTMLButtonElement).disabled).toBe(false));
    await user.click(submit);

    await waitFor(() => expect(TestWorkflowService.batchSelectMedia).toHaveBeenCalledTimes(1));
    expect(vi.mocked(TestWorkflowService.batchSelectMedia).mock.calls[0][0]).toStrictEqual({
      testOrderIds: [910, 920],
      stepName: "Selective Plating",
      mediaLotId: undefined,
      incubatorEquipmentId: 701,
      transitionType: "TRANSFER_INCUBATOR",
      targetStepName: undefined,
      predecessorStepName: "Enrichment"
    });
  });

  it("the compact register beside the panel lists the same page and keeps the checkboxes ticked", async () => {
    const user = userEvent.setup();
    renderWorkspace();
    await screen.findByText("Alpha Syrup");
    await check(user, "Alpha Syrup", "FP-0091");
    await check(user, "Beta Tablet", "FP-0092");
    await screen.findByText("Grouped Actions");
    const boxes = screen.getAllByRole("checkbox", { name: /for grouped actions$/ }) as HTMLInputElement[];
    const ticked = boxes.filter((b) => b.checked).map((b) => b.getAttribute("aria-label"));
    expect(ticked).toEqual([
      "Select Alpha Syrup (FP-0091) for grouped actions",
      "Select Beta Tablet (FP-0092) for grouped actions"
    ]);
    expect(
      within(
        screen.getByText((_c, el) => el?.tagName === "P" && /Laboratory Register \(/.test(el.textContent ?? ""))
          .parentElement as HTMLElement
      ).getByText((_c, el) => el?.tagName === "P" && el.textContent === "2 checked")
    ).toBeTruthy();
  });
});
