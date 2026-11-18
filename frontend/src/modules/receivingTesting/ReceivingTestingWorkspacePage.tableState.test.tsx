import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { ReceiveService } from "../receiving/services/ReceiveService";
import type { TestingWorkspaceFilter } from "../receiving/services/ReceiveService";
import type { SampleRecord } from "../receiving/types/receivingTypes";
import { masterDataOptions } from "../../services/masterDataOptions";
import { labelService } from "../../services/labelService";
import { lookupCache } from "../../services/lookupCache";
import { clientSettingsService } from "../../services/clientSettingsService";
import { tableViewService, type TableViewDto } from "../../components/lab/table/services/tableViewService";
import { exportLogService } from "../../components/lab/table/services/exportLogService";
import { TestWorkflowService } from "../testingWorkspace/services/TestWorkflowService";
import { SamplePreparationService } from "../testPreparation/services/SamplePreparationService";
import type { ActionableGroup } from "../testingWorkspace/types/testWorkflowTypes";
import { ReceivingTestingWorkspacePage } from "./ReceivingTestingWorkspacePage";

// The workspace register on the shared table state (smarter-tables Task 9, step 2):
// filters, sort and page live in the address, the old column key is migrated, the
// checked samples are a row selection, and the bulk actions and saved views work.

vi.mock("../receiving/services/ReceiveService", () => ({
  ReceiveService: {
    getRecordsPaged: vi.fn(),
    getWorkloadCounts: vi.fn(),
    getSample: vi.fn(),
    getAssignmentOptions: vi.fn(),
    bulkAssign: vi.fn(),
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

vi.mock("../../services/clientSettingsService", () => ({
  clientSettingsService: { get: vi.fn() }
}));

// Network boundary: the export log is the audit entry of an export.
vi.mock("../../components/lab/table/services/exportLogService", () => ({ exportLogService: { log: vi.fn() } }));
vi.mock("../../components/lab/table/services/tableViewService", () => ({
  tableViewService: { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setDefault: vi.fn() }
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

function seedPermissions(permissions: string[], role = "SectionHead") {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role, permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

function makeSample(sampleId: number, displayName: string, referenceNumber: string, status = "InTesting"): SampleRecord {
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
    status,
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
  makeSample(91, "Alpha Syrup", "FP-0091", "UnderReview"),
  makeSample(92, "Beta Tablet", "FP-0092", "InTesting"),
  makeSample(93, "Gamma Cream", "FP-0093", "Received")
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

const TABLE_KEY = "samples.workspace.fp";

function LocationProbe() {
  const location = useLocation();
  return <output aria-label="Address">{location.search}</output>;
}

function renderWorkspace(initialUrl = "/") {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={[initialUrl]}>
          <ReceivingTestingWorkspacePage lab={LAB} />
          <LocationProbe />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const lastFilter = (): TestingWorkspaceFilter | undefined => vi.mocked(ReceiveService.getRecordsPaged).mock.lastCall?.[0];
const address = () => new URLSearchParams(screen.getByLabelText("Address").textContent ?? "");

function savedView(over: Partial<TableViewDto> & { stateJson: string }): TableViewDto {
  return {
    id: 7,
    tableKey: TABLE_KEY,
    name: "My open work",
    scope: "Personal",
    sectionId: null,
    sectionName: null,
    isMine: true,
    canEdit: true,
    isDefault: false,
    ownerName: "Section Head",
    ...over
  };
}

const check = (user: ReturnType<typeof userEvent.setup>, name: string, ref: string) =>
  user.click(screen.getByRole("checkbox", { name: `Select ${name} (${ref}) for grouped actions` }));

describe("ReceivingTestingWorkspacePage on the shared table state", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.SAMPLES_ASSIGN_ANALYST, PERMISSIONS.TEST_WORKFLOW_EXECUTE]);

    vi.mocked(ReceiveService.getRecordsPaged).mockResolvedValue({
      items: SAMPLES,
      totalCount: 120,
      page: 1,
      pageSize: 50,
      totalPages: 3
    });
    vi.mocked(ReceiveService.getWorkloadCounts).mockResolvedValue({
      needsPreparation: 0, readyToRead: 0, awaitingReview: 0, overdue: 0, mine: 0, unassigned: 0
    });
    vi.mocked(ReceiveService.getSample).mockResolvedValue(null);
    vi.mocked(ReceiveService.getAssignmentOptions).mockResolvedValue({ users: [], tests: [] } as never);
    vi.mocked(masterDataOptions.getCausesOfTesting).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getSamplers).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getProductionStages).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getWaterSamplingPoints).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getWaterDepartments).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getDepartments).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getMachines).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getItems).mockResolvedValue([]);
    vi.mocked(labelService.getStatus).mockResolvedValue([]);
    vi.mocked(lookupCache.getIncubators).mockResolvedValue([
      { id: 701, code: "INC-1", name: "Incubator One", type: "Incubator", setPointTemperature: 32, calibrationStatus: "Valid" }
    ]);
    vi.mocked(lookupCache.getReleasedMedia).mockResolvedValue([
      {
        id: 501, lotNumber: "LOT-A", materialId: 11, materialName: "TSA",
        expiryDate: "2099-01-01T00:00:00Z", isReleasedForUse: true, status: "Released"
      }
    ]);
    vi.mocked(TestWorkflowService.getActionableGroups).mockResolvedValue({ groups: [GROUP] });
    vi.mocked(SamplePreparationService.getGroups).mockResolvedValue({ groups: [], excluded: [], excludedCount: 0 });
    vi.mocked(TestWorkflowService.batchSelectMedia).mockResolvedValue({
      totalRequested: 2, succeededCount: 2, skippedCount: 0, succeeded: [], skipped: []
    });
    vi.mocked(clientSettingsService.get).mockResolvedValue({ benchDraftRetentionHours: 24, features: [] });
    vi.mocked(tableViewService.list).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("filters and page are in the URL and restore on reload", async () => {
    const user = userEvent.setup();
    const first = renderWorkspace();
    await screen.findByText("Alpha Syrup");

    await user.click(screen.getByRole("button", { name: /^Filters/ }));
    await user.click(await screen.findByRole("combobox", { name: "Sample Status" }));
    await user.click(await screen.findByRole("option", { name: "Under Testing" }));
    await user.keyboard("{Escape}");
    await user.click(screen.getAllByRole("button", { name: "Go to next page" })[0]);

    await waitFor(() => expect(address().get("page")).toBe("2"));
    expect(address().get("f.sampleStatus")).toBe("InTesting");
    const url = `/${screen.getByLabelText("Address").textContent}`;
    await waitFor(() => expect(lastFilter()).toMatchObject({ page: 2, sampleStatus: "InTesting" }));
    first.unmount();

    // A reload: the same address gives the same filters and page.
    vi.mocked(ReceiveService.getRecordsPaged).mockClear();
    renderWorkspace(url);
    await screen.findByText("Alpha Syrup");
    await waitFor(() => expect(lastFilter()).toMatchObject({ page: 2, sampleStatus: "InTesting", pageSize: 50 }));
    expect(screen.getByRole("button", { name: /^Filters/ }).textContent).toContain("1");
  });

  it("an old link's parameters become table state in the address, and are not applied a second time", async () => {
    const user = userEvent.setup();
    renderWorkspace("/?category=FinishedProduct&workload=overdue&view=table&other=kept");
    await screen.findByText("Alpha Syrup");
    await waitFor(() => expect(lastFilter()).toMatchObject({ category: "FinishedProduct", workloadFilter: "overdue" }));
    await waitFor(() => expect(address().get("f.category")).toBe("FinishedProduct"));
    expect(address().get("f.workload")).toBe("overdue");
    expect(address().has("category")).toBe(false);
    expect(address().has("workload")).toBe(false);
    // The old display-mode parameter is consumed; parameters that are not table state stay.
    expect(address().has("view")).toBe(false);
    expect(address().get("other")).toBe("kept");

    // Clearing the tile sticks: nothing re-applies ?workload=overdue afterwards.
    await user.click(screen.getByText("Overdue").closest("button") as HTMLElement);
    await waitFor(() => expect(lastFilter()?.workloadFilter).toBeNull());
    expect(address().has("f.workload")).toBe(false);
  });

  it("old column preference is migrated once and the old key removed", async () => {
    localStorage.setItem("microlims.sampleRegister.columns", JSON.stringify(["receivedAt", "tests", "due:off"]));
    renderWorkspace();
    await screen.findByText("Alpha Syrup");
    const headers = screen.getAllByRole("columnheader").map((h) => h.textContent);
    expect(headers).toEqual(["", "Received At", "Item / Reference", "Tests", "Actions"]);
    expect(localStorage.getItem("microlims.sampleRegister.columns")).toBeNull();
    expect(JSON.parse(localStorage.getItem(`microlims.table.${TABLE_KEY}`) ?? "null").columns).toEqual(["receivedAt", "item", "tests"]);
  });

  it("selecting 2 samples still shows GroupedActionsPanel with the same sample ids; Deselect all clears the new selection", async () => {
    const user = userEvent.setup();
    renderWorkspace();
    await screen.findByText("Alpha Syrup");

    await check(user, "Alpha Syrup", "FP-0091");
    expect(screen.queryByText("Grouped Actions")).toBeNull();
    await check(user, "Beta Tablet", "FP-0092");
    expect(await screen.findByText("Grouped Actions")).toBeTruthy();
    await waitFor(() => expect(TestWorkflowService.getActionableGroups).toHaveBeenLastCalledWith({
      scope: "all",
      actionType: undefined,
      sampleIds: "91,92"
    }));
    // The compact register beside the panel shows the same two as ticked.
    const ticked = (screen.getAllByRole("checkbox", { name: /for grouped actions$/ }) as HTMLInputElement[])
      .filter((b) => b.checked).map((b) => b.getAttribute("aria-label"));
    expect(ticked).toEqual([
      "Select Alpha Syrup (FP-0091) for grouped actions",
      "Select Beta Tablet (FP-0092) for grouped actions"
    ]);

    await user.click(screen.getByRole("button", { name: "Deselect All" }));
    await waitFor(() => expect(screen.queryByText("Grouped Actions")).toBeNull());
    // Back in the full table, nothing is ticked and the bulk bar is gone.
    const boxes = await screen.findAllByRole("checkbox", { name: /for grouped actions$/ }) as HTMLInputElement[];
    expect(boxes.every((b) => !b.checked)).toBe(true);
    expect(screen.queryByText(/\d+ selected$/)).toBeNull();
  });

  it("the page checkbox selects every row on the page and batch-select-media is called with the same payload as before", async () => {
    const user = userEvent.setup();
    renderWorkspace();
    await screen.findByText("Alpha Syrup");

    await user.click(screen.getByRole("checkbox", { name: "Select all 3 samples on this page" }));
    await screen.findByText("Grouped Actions");
    expect(screen.getByText("3 samples selected across workspace")).toBeTruthy();

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
  });

  it("Assign analyst opens BulkAssignDialog with the selected ids", async () => {
    const user = userEvent.setup();
    renderWorkspace();
    await screen.findByText("Alpha Syrup");

    await check(user, "Alpha Syrup", "FP-0091");
    // One sample ticked: the table's own bulk bar offers the action, and only one such button exists.
    const bar = (await screen.findByText("1 selected")).parentElement?.parentElement as HTMLElement;
    await user.click(within(bar).getByRole("button", { name: "Assign analyst" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("Bulk Assign Analyst")).toBeTruthy();
    expect(within(dialog).getByText((_c, el) => el?.tagName === "P" && el.textContent === "1 sample selected")).toBeTruthy();
    expect(ReceiveService.getAssignmentOptions).toHaveBeenCalledWith(91);
  });

  it("Assign analyst from the grouped-actions layout opens the dialog with every selected id", async () => {
    const user = userEvent.setup();
    renderWorkspace();
    await screen.findByText("Alpha Syrup");
    await check(user, "Alpha Syrup", "FP-0091");
    await check(user, "Gamma Cream", "FP-0093");
    await screen.findByText("Grouped Actions");

    await user.click(screen.getByRole("button", { name: "Assign analyst" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("Bulk Assign Analyst")).toBeTruthy();
    expect(within(dialog).getByText((_c, el) => el?.tagName === "P" && el.textContent === "2 samples selected")).toBeTruthy();
    expect(ReceiveService.getAssignmentOptions).toHaveBeenCalledWith(91);
  });

  it("Print labels is absent when the feature flag is off", async () => {
    const user = userEvent.setup();
    renderWorkspace();
    await screen.findByText("Alpha Syrup");
    await waitFor(() => expect(clientSettingsService.get).toHaveBeenCalled());
    await check(user, "Alpha Syrup", "FP-0091");
    await screen.findByText("1 selected");
    expect(screen.queryByRole("button", { name: "Print labels" })).toBeNull();
    expect(screen.getByRole("button", { name: "Assign analyst" })).toBeTruthy();
  });

  it("Print labels is offered when the server lists the labels feature, and opens the dialog for the selected samples", async () => {
    const user = userEvent.setup();
    vi.mocked(clientSettingsService.get).mockResolvedValue({ benchDraftRetentionHours: 24, features: ["labels"] });
    renderWorkspace();
    await screen.findByText("Alpha Syrup");
    await check(user, "Alpha Syrup", "FP-0091");
    await user.click(await screen.findByRole("button", { name: "Print labels" }));
    await waitFor(() => expect(labelService.getStatus).toHaveBeenCalledWith(
      [{ entityType: "Sample", entityId: 91 }],
      expect.anything()
    ));
  });

  it("Export selected is the audited export and needs the export permission", async () => {
    const user = userEvent.setup();
    renderWorkspace();
    await screen.findByText("Alpha Syrup");
    await check(user, "Alpha Syrup", "FP-0091");
    await screen.findByText("1 selected");
    expect(screen.queryByRole("button", { name: "Export selected" })).toBeNull();
    cleanup();

    seedPermissions([PERMISSIONS.SAMPLES_ASSIGN_ANALYST, PERMISSIONS.DATA_EXPORT]);
    renderWorkspace();
    await screen.findByText("Alpha Syrup");
    await check(user, "Alpha Syrup", "FP-0091");
    expect(await screen.findByRole("button", { name: "Export selected" })).toBeTruthy();
  });

  it("Export selected stays available in the grouped-actions layout (2 or more checked) and is logged with the server", async () => {
    const user = userEvent.setup();
    seedPermissions([PERMISSIONS.SAMPLES_ASSIGN_ANALYST, PERMISSIONS.DATA_EXPORT]);
    vi.mocked(exportLogService.log).mockResolvedValue({
      exportId: 12, serverTimeUtc: "2026-11-17T10:00:00Z", timeZoneId: "UTC", exportedBy: "Hana Head"
    });
    const createUrl = vi.spyOn(URL, "createObjectURL").mockReturnValue("blob:x");
    vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => {});
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});
    renderWorkspace();
    await screen.findByText("Alpha Syrup");
    await check(user, "Alpha Syrup", "FP-0091");
    await check(user, "Gamma Cream", "FP-0093");
    await screen.findByText("Grouped Actions");

    await user.click(screen.getByRole("button", { name: "Export selected" }));

    await waitFor(() => expect(exportLogService.log).toHaveBeenCalledTimes(1));
    const logged = vi.mocked(exportLogService.log).mock.calls[0][0];
    expect(logged.tableKey).toMatch(/^samples\.workspace\./);
    expect(logged.rowCount).toBe(2);
    expect([...logged.recordIds].sort()).toEqual(["91", "93"]);
    expect(createUrl).toHaveBeenCalled();
    createUrl.mockRestore();
  });

  it("Export selected is not offered in the grouped-actions layout without the export permission", async () => {
    const user = userEvent.setup();
    renderWorkspace();
    await screen.findByText("Alpha Syrup");
    await check(user, "Alpha Syrup", "FP-0091");
    await check(user, "Gamma Cream", "FP-0093");
    await screen.findByText("Grouped Actions");
    expect(screen.queryByRole("button", { name: "Export selected" })).toBeNull();
  });

  it("saved view restores workloadFilter and analyst filter", async () => {
    vi.mocked(tableViewService.list).mockResolvedValue([
      savedView({
        stateJson: JSON.stringify({
          v: 1,
          filters: { workload: "mine", analystId: "5" },
          search: "",
          sort: null,
          pageSize: 50,
          columns: ["item", "tests"],
          density: "compact"
        })
      })
    ]);
    renderWorkspace("/?view=7");
    await screen.findByText("Alpha Syrup");
    await waitFor(() => expect(lastFilter()).toMatchObject({ workloadFilter: "mine", analystId: 5, page: 1 }));
    expect(screen.getByText("Assigned to Me").closest("button")?.getAttribute("aria-pressed")).toBe("true");
  });

  it("header sort sends sortBy/sortDir with page 1, flips direction, hides sort on excluded columns, and restores from URL", async () => {
    const user = userEvent.setup();
    const first = renderWorkspace();
    await screen.findByText("Alpha Syrup");

    // Excluded / computed columns must not show a sort button.
    const headers = screen.getAllByRole("columnheader");
    for (const excludedLabel of ["Assigned To", "Tests", "Next Action"]) {
      const col = headers.find((h) => h.textContent?.trim() === excludedLabel);
      expect(col).toBeTruthy();
      expect(within(col!).queryByRole("button")).toBeNull();
    }

    // Move to page 2 first to verify clicking a sort header resets page back to 1.
    await user.click(screen.getAllByRole("button", { name: "Go to next page" })[0]);
    await waitFor(() => expect(lastFilter()).toMatchObject({ page: 2 }));

    await user.click(screen.getByRole("button", { name: "Batch / Control No." }));
    await waitFor(() => expect(lastFilter()).toMatchObject({ sortBy: "batchControl", sortDir: "asc", page: 1 }));
    expect(address().get("sort")).toBe("batchControl:asc");

    await user.click(screen.getByRole("button", { name: "Batch / Control No." }));
    await waitFor(() => expect(lastFilter()).toMatchObject({ sortBy: "batchControl", sortDir: "desc", page: 1 }));
    expect(address().get("sort")).toBe("batchControl:desc");

    const url = `/${screen.getByLabelText("Address").textContent}`;
    first.unmount();

    vi.mocked(ReceiveService.getRecordsPaged).mockClear();
    renderWorkspace(url);
    await screen.findByText("Alpha Syrup");
    await waitFor(() => expect(lastFilter()).toMatchObject({ sortBy: "batchControl", sortDir: "desc" }));
    const restoredCol = screen.getAllByRole("columnheader").find((h) => h.textContent?.trim() === "Batch / Control No.");
    expect(restoredCol?.getAttribute("aria-sort")).toBe("descending");
  });
});
