import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import type { Mock } from "vitest";
import { render, screen, cleanup, within, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../theme";
import { SampleRegisterTable, dueLabel, clearSampleDocCountCache } from "./SampleRegisterTable";
import { useRowSelection } from "../../../components/lab/table/useRowSelection";
import type { SampleRecord, TestOrderSummary } from "../types/receivingTypes";
import { ItemDocumentService } from "../../laboratoryConfiguration/items/services/ItemDocumentService";
import { actionAvailabilityService } from "../../../services/actionAvailabilityService";
import { ItemStmService } from "../../laboratoryConfiguration/items/services/ItemStmService";

const authState = vi.hoisted(() => ({ permissions: [] as string[], role: null as string | null }));
vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => ({ permissions: authState.permissions, role: authState.role }),
  useOptionalAuth: () => ({ permissions: authState.permissions, role: authState.role })
}));
// The saved-views service is the network edge of the table state; no views exist in these tests.
vi.mock("../../../components/lab/table/services/tableViewService", () => ({
  tableViewService: { list: vi.fn().mockResolvedValue([]), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setDefault: vi.fn() }
}));
// Network boundary: the server's answer about void and assign for the row whose menu is open.
vi.mock("../../../services/actionAvailabilityService", () => ({
  actionAvailabilityService: { get: vi.fn() }
}));
vi.mock("../../laboratoryConfiguration/items/services/ItemDocumentService", () => ({
  ItemDocumentService: { getDocumentCountForItem: vi.fn().mockResolvedValue(0) }
}));
vi.mock("../../laboratoryConfiguration/items/services/ItemStmService", () => ({
  ItemStmService: { getForItem: vi.fn().mockResolvedValue({ status: "NotLinked" }) }
}));

beforeEach(() => {
  try { localStorage.clear(); } catch { /* ignore */ }
  authState.permissions = [];
  authState.role = null;
  clearSampleDocCountCache();
  vi.clearAllMocks();
  vi.mocked(actionAvailabilityService.get).mockResolvedValue([]);
});
afterEach(() => cleanup());

const sample = (over: Partial<SampleRecord>): SampleRecord => ({
  sampleId: 91, referenceNumber: "FP0926014", category: "FinishedProduct", displayName: "Osteocare Liquid",
  departmentId: null, machineId: null, waterDepartmentId: null, productionStage: null, causeOfTesting: "Routine",
  batchNumber: "26092136", controlNumber: "SF3216529", status: "InTesting", preparationStatus: "Prepared",
  receivedAt: "2026-09-15T19:45:00Z", sampledBy: "IPQA",
  assignedTests: [
    { testOrderId: 1, sectionId: 1, testCode: "TAMC", status: "InProgress", workflowStatus: "ReadyToRead", locationCount: 0, assignedAnalystId: 5, assignedAnalystName: "Mohamed Mahmoud" },
    { testOrderId: 2, sectionId: 1, testCode: "TYMC", status: "Approved", workflowStatus: "Completed", locationCount: 0, assignedAnalystId: 5, assignedAnalystName: "Mohamed Mahmoud" }
  ],
  ...over
});

const renderTable = (samples: SampleRecord[], onTestClick = vi.fn()) => render(
  <ThemeProvider theme={lightTheme}>
    <MemoryRouter>
      <SampleRegisterTable
        samples={samples}
        tableKey="samples.receiving"
        legacyColumnsKey="microlims.sampleRegister.columns"
        onTestClick={onTestClick}
        onViewSummary={vi.fn()}
        onViewReport={vi.fn()}
        onViewAuditHistory={vi.fn()}
        onPrepareSample={vi.fn()}
      />
    </MemoryRouter>
  </ThemeProvider>
);

describe("SampleRegisterTable", () => {
  it("shows the default columns, folds hidden ones under the item and offers the next action", async () => {
    const user = userEvent.setup();
    const onTestClick = vi.fn();
    renderTable([sample({})], onTestClick);

    expect(screen.getByRole("columnheader", { name: /Next Action/ })).toBeTruthy();
    expect(screen.queryByRole("columnheader", { name: "Received At" })).toBeNull();
    expect(screen.getByText(/#91 · Product · Routine · IPQA/)).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Record TAMC" }));
    expect(onTestClick).toHaveBeenCalledWith(expect.objectContaining({ testCode: "TAMC" }), expect.anything());
  });

  it("lets the user show a hidden column and remembers the choice", async () => {
    const user = userEvent.setup();
    const { unmount } = renderTable([sample({})]);
    await user.click(screen.getByRole("button", { name: "Columns" }));
    const picker = screen.getByRole("presentation");
    await user.click(within(picker).getByRole("checkbox", { name: "Received At" }));
    await user.keyboard("{Escape}");
    expect(screen.getByRole("columnheader", { name: "Received At" })).toBeTruthy();
    expect(screen.queryByText(/#91 · Product · Routine · IPQA · /)).toBeNull();

    unmount();
    renderTable([sample({})]);
    expect(screen.getByRole("columnheader", { name: "Received At" })).toBeTruthy();
  });

  it("asks for preparation before anything else", () => {
    renderTable([sample({ preparationStatus: "NeedsPreparation" })]);
    expect(screen.getByRole("button", { name: "Prepare" })).toBeTruthy();
  });

  it("marks a periodic retest that has no tests yet as awaiting test selection", () => {
    renderTable([sample({ status: "Received", preparationStatus: "Ready", awaitingTestSelection: true, assignedTests: [] })]);
    expect(screen.getByRole("button", { name: "Awaiting test selection" })).toBeTruthy();
    expect(screen.getByText("Received")).toBeTruthy();
  });

  it("shows the server's due time and marks overdue samples", () => {
    const now = Date.now();
    renderTable([
      sample({ sampleId: 91, dueAt: new Date(now - 3 * 3600e3).toISOString(), isOverdue: true }),
      sample({ sampleId: 92, referenceNumber: "FP09260145", dueAt: new Date(now + 5.5 * 3600e3).toISOString(), isOverdue: false })
    ]);
    expect(screen.getByRole("columnheader", { name: "Due" })).toBeTruthy();
    expect(screen.getByText("Overdue by 3 h")).toBeTruthy();
    expect(screen.getByText("Due in 5 h")).toBeTruthy();
  });

  it("keeps an older saved column choice but adds the new Due column once", () => {
    localStorage.setItem("microlims.sampleRegister.columns", JSON.stringify(["batchControl", "tests"]));
    renderTable([sample({ dueAt: new Date(Date.now() + 3600e3 * 10).toISOString(), isOverdue: false })]);
    expect(screen.getByRole("columnheader", { name: "Due" })).toBeTruthy();
    expect(screen.queryByRole("columnheader", { name: "Assigned To" })).toBeNull();
  });
});

describe("SampleRegisterTable row edge", () => {
  it("keeps the red overdue edge on a selected row", () => {
    render(
      <ThemeProvider theme={lightTheme}>
        <MemoryRouter>
          <SampleRegisterTable
            samples={[sample({ dueAt: new Date(Date.now() - 3600e3).toISOString(), isOverdue: true })]}
            tableKey="samples.receiving"
            selectedSampleId={91}
            onTestClick={vi.fn()} onViewSummary={vi.fn()} onViewReport={vi.fn()} onViewAuditHistory={vi.fn()} onPrepareSample={vi.fn()}
          />
        </MemoryRouter>
      </ThemeProvider>
    );
    const firstCell = screen.getByText("Osteocare Liquid").closest("td") as HTMLElement;
    expect(getComputedStyle(firstCell).boxShadow.toLowerCase()).toContain(lightTheme.custom.status.detected.text.toLowerCase());
  });
});

describe("SampleRegisterTable document badge", () => {
  it("shows badge with verification count + 1 when STM status is Effective", async () => {
    vi.mocked(ItemDocumentService.getDocumentCountForItem).mockResolvedValue(1);
    vi.mocked(ItemStmService.getForItem).mockResolvedValue({
      status: "Effective",
      sopNumber: "STM-RM-102",
      documentMasterId: 42,
      companyDocumentCode: "STM-RM-102",
      title: "Spec",
      documentTypeCode: "STM",
      revisionNumber: "01",
      effectiveDate: "2026-10-15T12:00:00Z",
      controlledPdfFileId: 101,
      controlledPdfFileName: "STM-RM-102_rev01.pdf",
      canDownload: true,
    });

    renderTable([sample({ itemId: 101 })]);

    await waitFor(() => {
      expect(screen.getByText("2 Docs")).toBeTruthy();
    });
  });

  it("shows badge with only verification count when STM status is not Effective", async () => {
    vi.mocked(ItemDocumentService.getDocumentCountForItem).mockResolvedValue(1);
    vi.mocked(ItemStmService.getForItem).mockResolvedValue({
      status: "NotFound",
      sopNumber: "STM-999",
      documentMasterId: null,
      companyDocumentCode: null,
      title: null,
      documentTypeCode: null,
      revisionNumber: null,
      effectiveDate: null,
      controlledPdfFileId: null,
      controlledPdfFileName: null,
      canDownload: false,
    });

    renderTable([sample({ itemId: 102 })]);

    await waitFor(() => {
      expect(screen.getByText("1 Docs")).toBeTruthy();
    });
  });
});

describe("dueLabel", () => {
  const now = Date.parse("2026-10-12T09:00:00Z");
  it("words the time left or overdue", () => {
    expect(dueLabel("2026-10-12T09:30:00Z", false, now)).toBe("Due in 30 min");
    expect(dueLabel("2026-10-14T12:00:00Z", false, now)).toBe("Due in 2 d");
    expect(dueLabel("2026-10-12T06:00:00Z", true, now)).toBe("Overdue by 3 h");
    expect(dueLabel(null, false, now)).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Characterisation of today's table (smarter-tables Task 9, step 1). These pin
// the behaviour the RegisterTable migration must keep.
// ---------------------------------------------------------------------------

const ALL_COLUMN_LABELS = [
  "Received At", "Sample #", "Item Type", "Cause", "Sampled By", "Batch / Control No.",
  "Assigned To", "Sample Status", "Due", "Tests", "Next Action"
];
const DEFAULT_COLUMN_LABELS = ["Batch / Control No.", "Assigned To", "Sample Status", "Due", "Tests", "Next Action"];

type SampleFn = (sample: SampleRecord) => void;
type TestFn = (test: TestOrderSummary, sample: SampleRecord) => void;

interface Handlers {
  onSelectSample: Mock<SampleFn>;
  onTestClick: Mock<TestFn>;
  onViewSummary: Mock<SampleFn>;
  onEdit: Mock<SampleFn>;
  onViewReport: Mock<SampleFn>;
  onViewAuditHistory: Mock<SampleFn>;
  onPrepareSample: Mock<SampleFn>;
  onAssignAnalyst: Mock<SampleFn>;
  onVoid: Mock<SampleFn>;
  onAddLaboratory: Mock<SampleFn>;
  onSelectTests: Mock<SampleFn>;
}

const makeHandlers = (): Handlers => ({
  onSelectSample: vi.fn<SampleFn>(), onTestClick: vi.fn<TestFn>(), onViewSummary: vi.fn<SampleFn>(),
  onEdit: vi.fn<SampleFn>(), onViewReport: vi.fn<SampleFn>(), onViewAuditHistory: vi.fn<SampleFn>(),
  onPrepareSample: vi.fn<SampleFn>(), onAssignAnalyst: vi.fn<SampleFn>(), onVoid: vi.fn<SampleFn>(),
  onAddLaboratory: vi.fn<SampleFn>(), onSelectTests: vi.fn<SampleFn>()
});

const renderFull = (samples: SampleRecord[], h: Handlers, legacyColumnsKey = "microlims.sampleRegister.columns") => render(
  <ThemeProvider theme={lightTheme}>
    <MemoryRouter>
      <SampleRegisterTable samples={samples} tableKey="samples.receiving" legacyColumnsKey={legacyColumnsKey} {...h} />
    </MemoryRouter>
  </ThemeProvider>
);

// The table's checkboxes feed a real selection; the output shows what it holds.
function SelectableRegister({ samples, h }: { samples: SampleRecord[]; h: Handlers }) {
  const selection = useRowSelection<number>("test");
  return (
    <>
      <SampleRegisterTable samples={samples} tableKey="samples.receiving" selection={selection} {...h} />
      <output aria-label="Selected sample ids">{[...selection.selected].sort((a, b) => a - b).join(",")}</output>
    </>
  );
}

const renderSelectable = (samples: SampleRecord[], h: Handlers) => render(
  <ThemeProvider theme={lightTheme}>
    <MemoryRouter>
      <SelectableRegister samples={samples} h={h} />
    </MemoryRouter>
  </ThemeProvider>
);
const selectedIds = () => screen.getByLabelText("Selected sample ids").textContent;

const headerTexts = () => screen.getAllByRole("columnheader").map((h) => h.textContent);

const openColumns = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click(screen.getByRole("button", { name: "Columns" }));
  return screen.getByRole("presentation");
};
const columnBoxes = (picker: HTMLElement) => within(picker).getAllByRole("checkbox") as HTMLInputElement[];

describe("SampleRegisterTable columns (characterisation)", () => {
  // The column menu is now the shared one: visible columns first (in their order), then the hidden ones, and the item column is fixed.
  it("offers all 12 registry columns, the visible seven first and ticked, the other five unticked", async () => {
    const user = userEvent.setup();
    renderFull([sample({})], makeHandlers());
    const boxes = columnBoxes(await openColumns(user));
    expect(boxes).toHaveLength(12);
    expect(boxes.map((b) => b.getAttribute("aria-label"))).toEqual([
      "Item / Reference", ...DEFAULT_COLUMN_LABELS,
      "Received At", "Sample #", "Item Type", "Cause", "Sampled By"
    ]);
    expect(boxes.filter((b) => b.checked).map((b) => b.getAttribute("aria-label"))).toEqual(["Item / Reference", ...DEFAULT_COLUMN_LABELS]);
    expect(boxes[0].disabled).toBe(true);
  });

  it("renders the item column, the six default columns and Actions as the header row", () => {
    renderFull([sample({})], makeHandlers());
    expect(headerTexts()).toEqual(["Item / Reference", ...DEFAULT_COLUMN_LABELS, "Actions"]);
  });

  it("shows all 11 columns once every one is ticked; each ticked column is added after the existing ones", async () => {
    const user = userEvent.setup();
    renderFull([sample({})], makeHandlers());
    const picker = await openColumns(user);
    for (const label of ALL_COLUMN_LABELS.filter((l) => !DEFAULT_COLUMN_LABELS.includes(l))) {
      await user.click(within(picker).getByRole("checkbox", { name: label }));
    }
    await user.keyboard("{Escape}");
    expect(headerTexts()).toEqual([
      "Item / Reference", "Batch / Control No.", "Assigned To", "Sample Status", "Due", "Tests", "Next Action",
      "Received At", "#", "Item Type", "Cause", "Sampled By", "Actions"
    ]);
  });

  it("Reset to default restores the default six", async () => {
    const user = userEvent.setup();
    localStorage.setItem("microlims.sampleRegister.columns", JSON.stringify(["receivedAt", "cause", "due:off"]));
    renderFull([sample({})], makeHandlers());
    expect(headerTexts()).toEqual(["Received At", "Item / Reference", "Cause", "Actions"]);
    await openColumns(user);
    await user.click(within(screen.getByRole("presentation")).getByRole("button", { name: "Reset to default" }));
    await user.keyboard("{Escape}");
    expect(headerTexts()).toEqual(["Item / Reference", ...DEFAULT_COLUMN_LABELS, "Actions"]);
  });
});

describe("SampleRegisterTable stored column preference (characterisation)", () => {
  it("honours microlims.sampleRegister.columns, including the due:off marker", () => {
    localStorage.setItem("microlims.sampleRegister.columns", JSON.stringify(["category", "cause", "tests", "due:off"]));
    renderFull([sample({})], makeHandlers());
    expect(headerTexts()).toEqual(["Item / Reference", "Item Type", "Cause", "Tests", "Actions"]);
    expect(screen.queryByRole("columnheader", { name: "Due" })).toBeNull();
  });

  it("adds Due once to a stored choice that has neither due nor due:off", () => {
    localStorage.setItem("microlims.sampleRegister.columns", JSON.stringify(["category", "tests"]));
    renderFull([sample({})], makeHandlers());
    expect(headerTexts()).toEqual(["Item / Reference", "Item Type", "Due", "Tests", "Actions"]);
  });

  it("ignores unknown keys in a stored choice", () => {
    localStorage.setItem("microlims.sampleRegister.columns", JSON.stringify(["category", "bogus", "due:off"]));
    renderFull([sample({})], makeHandlers());
    expect(headerTexts()).toEqual(["Item / Reference", "Item Type", "Actions"]);
  });

  it("falls back to the default six when the stored value is corrupt", () => {
    localStorage.setItem("microlims.sampleRegister.columns", "{not json");
    renderFull([sample({})], makeHandlers());
    expect(headerTexts()).toEqual(["Item / Reference", ...DEFAULT_COLUMN_LABELS, "Actions"]);
  });

  it("honours microlims.receivingRegister.columns for the receiving page and keeps the two keys apart", () => {
    localStorage.setItem("microlims.receivingRegister.columns", JSON.stringify(["sampleId", "due:off"]));
    localStorage.setItem("microlims.sampleRegister.columns", JSON.stringify(["tests", "due:off"]));
    renderFull([sample({})], makeHandlers(), "microlims.receivingRegister.columns");
    expect(headerTexts()).toEqual(["#", "Item / Reference", "Actions"]);
  });

  it("old column preference is migrated once and the old key removed", async () => {
    const user = userEvent.setup();
    localStorage.setItem("microlims.receivingRegister.columns", JSON.stringify(["sampleId", "tests", "due:off"]));
    const first = renderFull([sample({})], makeHandlers(), "microlims.receivingRegister.columns");
    expect(headerTexts()).toEqual(["#", "Item / Reference", "Tests", "Actions"]);
    expect(localStorage.getItem("microlims.receivingRegister.columns")).toBeNull();
    expect(JSON.parse(localStorage.getItem("microlims.table.samples.receiving") ?? "null")).toMatchObject({
      columns: ["sampleId", "item", "tests"]
    });
    first.unmount();

    // A later visit reads the new state, not the old key (which is gone) - and a stale old key never overrides it.
    localStorage.setItem("microlims.receivingRegister.columns", JSON.stringify(["category", "due:off"]));
    renderFull([sample({})], makeHandlers(), "microlims.receivingRegister.columns");
    expect(headerTexts()).toEqual(["#", "Item / Reference", "Tests", "Actions"]);
    expect(localStorage.getItem("microlims.receivingRegister.columns")).toBeNull();

    // A choice made now is kept in the new state only.
    await user.click(screen.getByRole("button", { name: "Columns" }));
    await user.click(within(screen.getByRole("presentation")).getByRole("checkbox", { name: "Due" }));
    expect(JSON.parse(localStorage.getItem("microlims.table.samples.receiving") ?? "null").columns).toEqual(["sampleId", "item", "tests", "due"]);
    expect(localStorage.getItem("microlims.sampleRegister.columns")).toBeNull();
  });
});

describe("SampleRegisterTable row actions (characterisation)", () => {
  const rowOf = (name: string) => screen.getByText(name).closest("tr") as HTMLElement;

  it("clicking a row selects that sample; a click on a control inside it does not", async () => {
    const user = userEvent.setup();
    const h = makeHandlers();
    renderFull([sample({ sampleId: 91 }), sample({ sampleId: 92, referenceNumber: "FP2", displayName: "Second Item" })], h);
    await user.click(rowOf("Second Item"));
    expect(h.onSelectSample).toHaveBeenCalledTimes(1);
    expect(h.onSelectSample).toHaveBeenCalledWith(expect.objectContaining({ sampleId: 92 }));
    await user.click(within(rowOf("Osteocare Liquid")).getByRole("button", { name: "Record TAMC" }));
    expect(h.onSelectSample).toHaveBeenCalledTimes(1);
  });

  it("Enter on a focused row selects that sample", async () => {
    const user = userEvent.setup();
    const h = makeHandlers();
    renderFull([sample({ sampleId: 91 })], h);
    rowOf("Osteocare Liquid").focus();
    await user.keyboard("{Enter}");
    expect(h.onSelectSample).toHaveBeenCalledWith(expect.objectContaining({ sampleId: 91 }));
  });

  it("the eye icon calls onViewSummary with the row's sample", async () => {
    const user = userEvent.setup();
    const h = makeHandlers();
    renderFull([sample({ sampleId: 91 }), sample({ sampleId: 92, referenceNumber: "FP2", displayName: "Second Item" })], h);
    await user.click(within(rowOf("Second Item")).getByRole("button", { name: "View Sample Details & Workflow" }));
    expect(h.onViewSummary).toHaveBeenCalledTimes(1);
    expect(h.onViewSummary).toHaveBeenCalledWith(expect.objectContaining({ sampleId: 92 }));
  });

  it("the Edit icon calls onEdit with the row's sample when permitted", async () => {
    const user = userEvent.setup();
    authState.permissions = ["Samples.Correct"];
    const h = makeHandlers();
    renderFull([sample({ sampleId: 91, canEditDetails: true })], h);
    await user.click(screen.getByRole("button", { name: "Edit Sample Details" }));
    expect(h.onEdit).toHaveBeenCalledWith(expect.objectContaining({ sampleId: 91 }));
  });

  it("hides Edit without Samples.Correct, and locks Edit when the details cannot be edited", () => {
    const h = makeHandlers();
    const { unmount } = renderFull([sample({ canEditDetails: true })], h);
    expect(screen.queryByRole("button", { name: "Edit Sample Details" })).toBeNull();
    unmount();
    authState.permissions = ["Samples.Correct"];
    renderFull([sample({ canEditDetails: false })], h);
    const locked = screen.getByRole("button", { name: "Locked: the sample has been submitted for review" });
    expect((locked as HTMLButtonElement).disabled).toBe(true);
  });

  it("the Sample actions menu fires Assign Analyst, Add Laboratory, Prepare Sample, Void Sample and Audit History with the row's sample", async () => {
    const user = userEvent.setup();
    authState.permissions = ["Samples.Correct", "Samples.AssignAnalyst", "Samples.Receive"];
    const h = makeHandlers();
    renderFull([sample({ sampleId: 91, preparationStatus: "NeedsPreparation", assignedTests: [] })], h);
    const pick = async (name: string) => {
      await user.click(screen.getByRole("button", { name: "Sample actions for FP0926014" }));
      await user.click(await screen.findByRole("menuitem", { name }));
    };
    await pick("Assign Analyst");
    expect(h.onAssignAnalyst).toHaveBeenCalledWith(expect.objectContaining({ sampleId: 91 }));
    await pick("Add Laboratory");
    expect(h.onAddLaboratory).toHaveBeenCalledWith(expect.objectContaining({ sampleId: 91 }));
    await pick("Prepare Sample");
    expect(h.onPrepareSample).toHaveBeenCalledWith(expect.objectContaining({ sampleId: 91 }));
    await pick("Void Sample");
    expect(h.onVoid).toHaveBeenCalledWith(expect.objectContaining({ sampleId: 91 }));
    await pick("Audit History");
    expect(h.onViewAuditHistory).toHaveBeenCalledWith(expect.objectContaining({ sampleId: 91 }));
  });

  it("the Sample actions menu offers Select Tests to a Section Head on a retest awaiting its tests", async () => {
    const user = userEvent.setup();
    authState.permissions = ["Samples.Correct"];
    authState.role = "SectionHead";
    const h = makeHandlers();
    renderFull([sample({ sampleId: 91, status: "Received", awaitingTestSelection: true, assignedTests: [] })], h);
    await user.click(screen.getByRole("button", { name: "Sample actions for FP0926014" }));
    await user.click(await screen.findByRole("menuitem", { name: "Select Tests" }));
    expect(h.onSelectTests).toHaveBeenCalledWith(expect.objectContaining({ sampleId: 91 }));
  });

  it("the menu links the full report", async () => {
    const user = userEvent.setup();
    const h = makeHandlers();
    renderFull([sample({ sampleId: 91 })], h);
    await user.click(screen.getByRole("button", { name: "Sample actions for FP0926014" }));
    expect(screen.getByRole("menuitem", { name: "View Full Report" }).getAttribute("href")).toBe("/samples/91/report");
  });

  it("asks the server about the row whose menu is opened, and only then", async () => {
    const user = userEvent.setup();
    const h = makeHandlers();
    renderFull([sample({ sampleId: 91 })], h);
    expect(actionAvailabilityService.get).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: "Sample actions for FP0926014" }));
    await waitFor(() => expect(actionAvailabilityService.get).toHaveBeenCalledWith("sample", 91, undefined, expect.any(AbortSignal)));
  });

  it("a refused Void Sample shows the server's sentence as secondary text and does nothing when chosen", async () => {
    const user = userEvent.setup();
    const sentence = "A sample that has results entered cannot be voided.";
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      { action: "sample.void", allowed: false, reason: sentence },
      { action: "sample.assign", allowed: true, reason: null }
    ]);
    const h = makeHandlers();
    renderFull([sample({ sampleId: 91 })], h);
    await user.click(screen.getByRole("button", { name: "Sample actions for FP0926014" }));
    const reason = await screen.findByText(sentence);
    const item = screen.getByRole("menuitem", { name: /Void Sample/ });
    expect(item.getAttribute("aria-disabled")).toBe("true");
    expect(item.contains(reason)).toBe(true);
    expect(document.getElementById(item.getAttribute("aria-describedby") ?? "")?.textContent).toBe(sentence);
    await user.click(item);
    expect(h.onVoid).not.toHaveBeenCalled();
    // The assign item the server allows still works.
    await user.click(screen.getByRole("menuitem", { name: "Assign Analyst" }));
    expect(h.onAssignAnalyst).toHaveBeenCalledWith(expect.objectContaining({ sampleId: 91 }));
  });

  it("a refused Assign Analyst shows the role sentence even without Samples.AssignAnalyst", async () => {
    const user = userEvent.setup();
    const sentence = "Your role does not include this action.";
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([
      { action: "sample.assign", allowed: false, reason: sentence }
    ]);
    const h = makeHandlers();
    renderFull([sample({ sampleId: 91 })], h);
    await user.click(screen.getByRole("button", { name: "Sample actions for FP0926014" }));
    expect(await screen.findByText(sentence)).toBeTruthy();
    await user.click(screen.getByRole("menuitem", { name: /Assign Analyst/ }));
    expect(h.onAssignAnalyst).not.toHaveBeenCalled();
  });

  it("each next-action button calls its own callback with the row's sample", async () => {
    const user = userEvent.setup();
    authState.permissions = ["Samples.AssignAnalyst"];
    const h = makeHandlers();
    const tests = (status: string) => [{ testOrderId: 1, sectionId: 1, testCode: "TAMC", status, workflowStatus: status, locationCount: 0, assignedAnalystId: null, assignedAnalystName: null }];
    renderFull([
      sample({ sampleId: 1, displayName: "Prep", preparationStatus: "NeedsPreparation" }),
      sample({ sampleId: 2, displayName: "Rev", status: "UnderReview" }),
      sample({ sampleId: 3, displayName: "Appr", status: "UnderApproval" }),
      sample({ sampleId: 4, displayName: "Done", status: "Approved" }),
      sample({ sampleId: 5, displayName: "Asg", assignedAnalystName: null, assignedTests: tests("InProgress") }),
      sample({ sampleId: 6, displayName: "Strt", assignedAnalystName: "Someone", assignedTests: tests("Pending") })
    ], h);
    await user.click(within(rowOf("Prep")).getByRole("button", { name: "Prepare" }));
    expect(h.onPrepareSample).toHaveBeenLastCalledWith(expect.objectContaining({ sampleId: 1 }));
    await user.click(within(rowOf("Rev")).getByRole("button", { name: "Review" }));
    expect(h.onViewSummary).toHaveBeenLastCalledWith(expect.objectContaining({ sampleId: 2 }));
    await user.click(within(rowOf("Appr")).getByRole("button", { name: "Approve" }));
    expect(h.onViewSummary).toHaveBeenLastCalledWith(expect.objectContaining({ sampleId: 3 }));
    await user.click(within(rowOf("Done")).getByRole("button", { name: "View Report" }));
    expect(h.onViewReport).toHaveBeenCalledWith(expect.objectContaining({ sampleId: 4 }));
    await user.click(within(rowOf("Asg")).getByRole("button", { name: "Assign" }));
    expect(h.onAssignAnalyst).toHaveBeenLastCalledWith(expect.objectContaining({ sampleId: 5 }));
    await user.click(within(rowOf("Strt")).getByRole("button", { name: "Start Test" }));
    expect(h.onTestClick).toHaveBeenLastCalledWith(expect.objectContaining({ testOrderId: 1 }), expect.objectContaining({ sampleId: 6 }));
  });

  it("a Review or Approve the server refuses shows its sentence and does nothing; an allowed one still works", async () => {
    const user = userEvent.setup();
    const reviewSentence = "You cannot review a sample you tested.";
    const approveSentence = "You cannot approve a sample you reviewed.";
    vi.mocked(actionAvailabilityService.get).mockImplementation(async (_kind, id) =>
      id === 2
        ? [{ action: "review.complete", allowed: false, reason: reviewSentence }]
        : id === 3
          ? [{ action: "approval.approve", allowed: false, reason: approveSentence }]
          : [{ action: "review.complete", allowed: true, reason: null }, { action: "approval.approve", allowed: true, reason: null }]
    );
    const h = makeHandlers();
    renderFull([
      sample({ sampleId: 2, displayName: "RevNo", status: "UnderReview" }),
      sample({ sampleId: 3, displayName: "ApprNo", status: "UnderApproval" }),
      sample({ sampleId: 4, displayName: "RevOk", status: "UnderReview" })
    ], h);

    expect(await within(rowOf("RevNo")).findByText(reviewSentence)).toBeTruthy();
    expect(await within(rowOf("ApprNo")).findByText(approveSentence)).toBeTruthy();
    const refusedReview = within(rowOf("RevNo")).getByRole("button", { name: "Review" });
    expect(refusedReview.getAttribute("aria-disabled")).toBe("true");
    await user.click(refusedReview);
    await user.click(within(rowOf("ApprNo")).getByRole("button", { name: "Approve" }));
    expect(h.onViewSummary).not.toHaveBeenCalled();

    await user.click(within(rowOf("RevOk")).getByRole("button", { name: "Review" }));
    expect(h.onViewSummary).toHaveBeenCalledWith(expect.objectContaining({ sampleId: 4 }));
    expect(actionAvailabilityService.get).toHaveBeenCalledWith("sample", 2, undefined, expect.any(AbortSignal));
  });

  it("offers no assign control on a voided or approved sample, but does on an open one without an analyst", () => {
    authState.permissions = ["Samples.AssignAnalyst"];
    localStorage.setItem("microlims.sampleRegister.columns", JSON.stringify(["item", "assignedTo", "sampleStatus"]));
    const h = makeHandlers();
    renderFull([
      sample({ sampleId: 11, displayName: "VoidedOne", status: "Voided", assignedTests: [] }),
      sample({ sampleId: 12, displayName: "ApprovedOne", status: "Approved", assignedTests: [] }),
      sample({ sampleId: 13, displayName: "OpenOne", status: "InTesting", assignedTests: [] })
    ], h);
    expect(within(rowOf("VoidedOne")).queryByRole("button", { name: /Assign responsible analyst/ })).toBeNull();
    expect(within(rowOf("ApprovedOne")).queryByRole("button", { name: /Assign responsible analyst/ })).toBeNull();
    expect(within(rowOf("OpenOne")).getByRole("button", { name: /Assign responsible analyst/ })).toBeTruthy();
  });

  it("Select tests on the next-action cell calls onSelectTests for a Section Head", async () => {
    const user = userEvent.setup();
    authState.permissions = ["Samples.Correct"];
    authState.role = "SectionHead";
    const h = makeHandlers();
    renderFull([sample({ sampleId: 91, status: "Received", awaitingTestSelection: true, assignedTests: [] })], h);
    await user.click(screen.getAllByRole("button", { name: "Select tests" })[0]);
    expect(h.onSelectTests).toHaveBeenCalledWith(expect.objectContaining({ sampleId: 91 }));
  });

  it("the row checkbox selects that sample id and unselects it again; the header checkbox selects every id on the page", async () => {
    const user = userEvent.setup();
    renderSelectable([sample({ sampleId: 91 }), sample({ sampleId: 92, referenceNumber: "FP2", displayName: "Second Item" })], makeHandlers());
    const row = screen.getByRole("checkbox", { name: "Select Second Item (FP2) for grouped actions" }) as HTMLInputElement;
    await user.click(row);
    expect(selectedIds()).toBe("92");
    expect(row.checked).toBe(true);
    await user.click(row);
    expect(selectedIds()).toBe("");
    await user.click(screen.getByRole("checkbox", { name: "Select all 2 samples on this page" }));
    expect(selectedIds()).toBe("91,92");
    expect(screen.getByRole("checkbox", { name: "Deselect all 2 samples on this page" })).toBeTruthy();
  });
});

describe("SampleRegisterTable test list (characterisation)", () => {
  it("the Tests cell opens the assigned tests and choosing one calls onTestClick with that test and the sample", async () => {
    const user = userEvent.setup();
    const h = makeHandlers();
    renderFull([sample({ sampleId: 91 })], h);
    await user.click(screen.getByRole("button", { name: "1 In Progress" }));
    expect(screen.getByText("Assigned Tests (2)")).toBeTruthy();
    const popover = screen.getByRole("presentation");
    await user.click(within(popover).getByRole("button", { name: /TYMC/ }));
    expect(h.onTestClick).toHaveBeenCalledTimes(1);
    expect(h.onTestClick).toHaveBeenCalledWith(
      expect.objectContaining({ testOrderId: 2, testCode: "TYMC" }),
      expect.objectContaining({ sampleId: 91 })
    );
  });

  it("View All Tests in that list calls onViewSummary with the sample", async () => {
    const user = userEvent.setup();
    const h = makeHandlers();
    renderFull([sample({ sampleId: 91 })], h);
    await user.click(screen.getByRole("button", { name: "1 In Progress" }));
    await user.click(within(screen.getByRole("presentation")).getByRole("button", { name: /View All Tests \(2\)/ }));
    expect(h.onViewSummary).toHaveBeenCalledWith(expect.objectContaining({ sampleId: 91 }));
  });

  it("the Record button names the ready test and passes that test, not the first one", async () => {
    const user = userEvent.setup();
    const h = makeHandlers();
    const tests = [
      { testOrderId: 7, sectionId: 1, testCode: "AAA", status: "Approved", workflowStatus: "Completed", locationCount: 0, assignedAnalystId: 5, assignedAnalystName: "X" },
      { testOrderId: 8, sectionId: 1, testCode: "BBB", status: "InProgress", workflowStatus: "ReadyToRead", locationCount: 0, assignedAnalystId: 5, assignedAnalystName: "X" }
    ];
    renderFull([sample({ sampleId: 91, assignedTests: tests })], h);
    await user.click(screen.getByRole("button", { name: "Record BBB" }));
    expect(h.onTestClick).toHaveBeenCalledWith(expect.objectContaining({ testOrderId: 8 }), expect.objectContaining({ sampleId: 91 }));
  });
});

describe("SampleRegisterTable retest chain expand and collapse (characterisation)", () => {
  const family = () => [
    sample({ sampleId: 91, displayName: "Parent Item" }),
    sample({ sampleId: 92, referenceNumber: "FP-RT1", displayName: "Retest Child", originSampleId: 91 })
  ];

  it("shows a retest under its parent only after expanding, and collapses it again", async () => {
    const user = userEvent.setup();
    renderFull(family(), makeHandlers());
    expect(screen.queryByText("Retest Child")).toBeNull();
    await user.click(screen.getByText("1 retest"));
    expect(screen.getByText("Retest Child")).toBeTruthy();
    expect(screen.getByText("Parent Item")).toBeTruthy();
    await user.click(screen.getByText("1 retest"));
    expect(screen.queryByText("Retest Child")).toBeNull();
  });

  it("expanding does not select the parent row", async () => {
    const user = userEvent.setup();
    const h = makeHandlers();
    renderFull(family(), h);
    await user.click(screen.getByText("1 retest"));
    expect(h.onSelectSample).not.toHaveBeenCalled();
  });

  it("select-all counts only the rows on screen: the collapsed retest is not included, the expanded one is", async () => {
    const user = userEvent.setup();
    renderSelectable(family(), makeHandlers());
    await user.click(screen.getByRole("checkbox", { name: "Select all 1 samples on this page" }));
    expect(selectedIds()).toBe("91");
    await user.click(screen.getByRole("checkbox", { name: "Deselect all 1 samples on this page" }));
    await user.click(screen.getByText("1 retest"));
    await user.click(screen.getByRole("checkbox", { name: "Select all 2 samples on this page" }));
    expect(selectedIds()).toBe("91,92");
    // The retest's own checkbox is ticked as well.
    expect((screen.getByRole("checkbox", { name: "Select Retest Child (FP-RT1) for grouped actions" }) as HTMLInputElement).checked).toBe(true);
  });

  it("the toggle button on a parent row expands and collapses it and keeps its place; rows without retests have none", async () => {
    const user = userEvent.setup();
    renderFull([...family(), sample({ sampleId: 93, referenceNumber: "FP3", displayName: "Lonely Item" })], makeHandlers());
    expect(screen.getAllByRole("button", { name: /^(Expand|Collapse) row/ })).toHaveLength(1);
    const toggle = screen.getByRole("button", { name: "Expand row 91" });
    await user.click(toggle);
    expect(screen.getByText("Retest Child")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Collapse row 91" }).getAttribute("aria-expanded")).toBe("true");
    await user.click(screen.getByRole("button", { name: "Collapse row 91" }));
    expect(screen.queryByText("Retest Child")).toBeNull();
  });

});
