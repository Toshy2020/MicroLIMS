import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { render, screen, cleanup, fireEvent, within, waitFor } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../theme";
import { SampleRegisterTable, dueLabel, clearSampleDocCountCache } from "./SampleRegisterTable";
import type { SampleRecord } from "../types/receivingTypes";
import { ItemDocumentService } from "../../laboratoryConfiguration/items/services/ItemDocumentService";
import { ItemStmService } from "../../laboratoryConfiguration/items/services/ItemStmService";

vi.mock("../../../contexts/AuthContext", () => ({ useAuth: () => ({ permissions: [] }) }));
vi.mock("../../laboratoryConfiguration/items/services/ItemDocumentService", () => ({
  ItemDocumentService: { getDocumentCountForItem: vi.fn().mockResolvedValue(0) }
}));
vi.mock("../../laboratoryConfiguration/items/services/ItemStmService", () => ({
  ItemStmService: { getForItem: vi.fn().mockResolvedValue({ status: "NotLinked" }) }
}));

beforeEach(() => {
  try { localStorage.clear(); } catch { /* ignore */ }
  clearSampleDocCountCache();
  vi.clearAllMocks();
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
    <SampleRegisterTable
      samples={samples}
      onTestClick={onTestClick}
      onViewSummary={vi.fn()}
      onViewReport={vi.fn()}
      onViewAuditHistory={vi.fn()}
      onPrepareSample={vi.fn()}
    />
  </ThemeProvider>
);

describe("SampleRegisterTable", () => {
  it("shows the default columns, folds hidden ones under the item and offers the next action", () => {
    const onTestClick = vi.fn();
    renderTable([sample({})], onTestClick);

    expect(screen.getByRole("columnheader", { name: /Next Action/ })).toBeTruthy();
    expect(screen.queryByRole("columnheader", { name: "Received At" })).toBeNull();
    expect(screen.getByText(/#91 · Product · Routine · IPQA/)).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Record TAMC" }));
    expect(onTestClick).toHaveBeenCalledWith(expect.objectContaining({ testCode: "TAMC" }), expect.anything());
  });

  it("lets the user show a hidden column and remembers the choice", () => {
    const { unmount } = renderTable([sample({})]);
    fireEvent.click(screen.getByRole("button", { name: "Choose visible columns" }));
    const picker = screen.getByRole("presentation");
    fireEvent.click(within(picker).getByLabelText("Received At"));
    fireEvent.keyDown(picker, { key: "Escape" });
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
        <SampleRegisterTable
          samples={[sample({ dueAt: new Date(Date.now() - 3600e3).toISOString(), isOverdue: true })]}
          selectedSampleId={91}
          onTestClick={vi.fn()} onViewSummary={vi.fn()} onViewReport={vi.fn()} onViewAuditHistory={vi.fn()} onPrepareSample={vi.fn()}
        />
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
