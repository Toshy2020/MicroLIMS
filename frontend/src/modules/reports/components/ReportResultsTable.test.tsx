import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { exportLogService } from "../../../components/lab/table/services/exportLogService";
import { ReportingService } from "../services/ReportingService";
import type {
  ResultRecordItem,
  ResultRecordSearchParams,
  ResultRecordSearchResponse,
} from "../types/reportingTypes";
import { ReportResultsTable } from "./ReportResultsTable";

vi.mock("../services/ReportingService", () => ({
  ReportingService: {
    searchResults: vi.fn(),
    exportCsv: vi.fn(),
  },
}));

vi.mock("../../../components/lab/table/services/exportLogService", () => ({
  exportLogService: {
    log: vi.fn(),
  },
}));

const mockedSearchResults = vi.mocked(ReportingService.searchResults);
const mockedExportCsv = vi.mocked(ReportingService.exportCsv);
const mockedExportLog = vi.mocked(exportLogService.log);

function makeRecord(overrides: Partial<ResultRecordItem> & { id: number; referenceNumber: string }): ResultRecordItem {
  return {
    sampleId: overrides.id,
    testOrderId: overrides.id * 10,
    sourceTable: "CountTestReading",
    sourceId: overrides.id,
    round: 1,
    category: "FinishedProduct",
    subjectName: "Paracetamol 500mg",
    subjectDetail: "Line 1",
    batchNumber: "B-100",
    controlNumber: "C-100",
    testCode: "TAMC",
    testDisplayName: "Total Aerobic Microbial Count",
    resultKind: "Quantitative",
    numericValue: 10,
    reportedValue: "10",
    unit: "CFU/g",
    isBelowDetectionLimit: false,
    detectionLimit: null,
    alertLimit: "50",
    actionLimit: "100",
    specLimit: "1000",
    resultLevel: "WithinLimit",
    resultEnteredAt: "2026-10-01T10:15:00Z",
    resultEnteredByUserId: 1,
    resultEnteredByName: "Analyst One",
    sampleStatus: "Approved",
    approvalStatus: "Approved",
    approvedByUserId: 2,
    approvedByName: "Reviewer One",
    approvedAt: "2026-10-02T12:00:00Z",
    ...overrides,
  };
}

const APPLIED_PARAMS: ResultRecordSearchParams = {
  search: "TAMC",
  category: "FinishedProduct",
  page: 1,
  pageSize: 25,
  sortBy: "ResultEnteredAt",
  sortDescending: true,
};

function renderTable(
  response: ResultRecordSearchResponse,
  params: ResultRecordSearchParams = APPLIED_PARAMS
) {
  localStorage.setItem("microlims_permissions", JSON.stringify([PERMISSIONS.DATA_EXPORT]));
  localStorage.setItem("microlims_fullname", "Dr. Ada");
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <AuthProvider>
          <ReportResultsTable
            results={response}
            loading={false}
            error={null}
            appliedParams={params}
            onPageChange={vi.fn()}
            onPageSizeChange={vi.fn()}
            onClearFilters={vi.fn()}
          />
        </AuthProvider>
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

let writtenPdfHtml: string[] = [];
let downloadedBlobs: Blob[] = [];
let windowOpenSpy: ReturnType<typeof vi.spyOn>;
let createObjectUrlSpy: ReturnType<typeof vi.spyOn>;
let revokeObjectUrlSpy: ReturnType<typeof vi.spyOn>;
let anchorClickSpy: ReturnType<typeof vi.spyOn>;

beforeEach(() => {
  localStorage.clear();
  vi.clearAllMocks();
  writtenPdfHtml = [];
  downloadedBlobs = [];
  mockedExportLog.mockResolvedValue({
    exportId: 501,
    serverTimeUtc: "2026-11-14T12:00:00Z",
    timeZoneId: "Africa/Cairo",
    exportedBy: "Dr. Ada",
  });
  windowOpenSpy = vi.spyOn(window, "open").mockImplementation(() => {
    const fakeDoc = {
      open: vi.fn(),
      write: (html: string) => {
        writtenPdfHtml.push(html);
      },
      close: vi.fn(),
    };
    return { document: fakeDoc } as unknown as Window;
  });
  createObjectUrlSpy = vi
    .spyOn(URL, "createObjectURL")
    .mockImplementation((obj: Blob | MediaSource) => {
      if (obj instanceof Blob) downloadedBlobs.push(obj);
      return "blob:mock-url";
    });
  revokeObjectUrlSpy = vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => {});
  anchorClickSpy = vi
    .spyOn(HTMLAnchorElement.prototype, "click")
    .mockImplementation(() => {});
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  windowOpenSpy.mockRestore();
  createObjectUrlSpy.mockRestore();
  revokeObjectUrlSpy.mockRestore();
  anchorClickSpy.mockRestore();
});

describe("ReportResultsTable", () => {
  it("renders all columns in order and toggles column visibility from the Columns menu", async () => {
    const user = userEvent.setup();
    const items = [makeRecord({ id: 1, referenceNumber: "FP-001" })];
    renderTable({ items, totalCount: 1, page: 1, pageSize: 25 });

    const headers = screen
      .getAllByRole("columnheader")
      .map((h) => h.textContent?.trim() ?? "")
      .filter((t) => t.length > 0 && t !== "Actions");

    expect(headers).toEqual([
      "Date/Time",
      "Sample/Reference",
      "Item/Location",
      "Type",
      "Test",
      "Result",
      "Unit",
      "Result Level",
      "Status",
      "Approved By",
      "Approval Date",
    ]);

    await user.click(screen.getByRole("button", { name: "Columns" }));
    await user.click(screen.getByRole("menuitem", { name: "Unit" }));
    await user.keyboard("{Escape}");

    const updatedHeaders = screen
      .getAllByRole("columnheader")
      .map((h) => h.textContent?.trim() ?? "")
      .filter((t) => t.length > 0 && t !== "Actions");
    expect(updatedHeaders).not.toContain("Unit");
  });

  it("preserves row sort order from results", () => {
    const items = [
      makeRecord({ id: 2, referenceNumber: "FP-002" }),
      makeRecord({ id: 1, referenceNumber: "FP-001" }),
      makeRecord({ id: 3, referenceNumber: "FP-003" }),
    ];
    renderTable({ items, totalCount: 3, page: 1, pageSize: 25 });

    const rows = screen.getAllByRole("row").slice(1);
    expect(within(rows[0]).getByText("FP-002")).toBeTruthy();
    expect(within(rows[1]).getByText("FP-001")).toBeTruthy();
    expect(within(rows[2]).getByText("FP-003")).toBeTruthy();
  });

  it("rows are selectable only when approvalStatus === Approved", async () => {
    const user = userEvent.setup();
    const items = [
      makeRecord({ id: 101, referenceNumber: "FP-101", approvalStatus: "Approved" }),
      makeRecord({ id: 102, referenceNumber: "FP-102", approvalStatus: "Pending" }),
      makeRecord({ id: 103, referenceNumber: "FP-103", approvalStatus: "Rejected" }),
    ];
    renderTable({ items, totalCount: 3, page: 1, pageSize: 25 });

    const cb101 = screen.getByRole("checkbox", { name: "Select row 101" }) as HTMLInputElement;
    const cb102 = screen.getByRole("checkbox", { name: "Select row 102" }) as HTMLInputElement;
    const cb103 = screen.getByRole("checkbox", { name: "Select row 103" }) as HTMLInputElement;

    expect(cb101.disabled).toBe(false);
    expect(cb102.disabled).toBe(true);
    expect(cb103.disabled).toBe(true);

    const selectAllOnPage = screen.getByRole("checkbox", { name: /select all rows/i });
    await user.click(selectAllOnPage);

    expect(cb101.checked).toBe(true);
    expect(cb102.checked).toBe(false);
    expect(cb103.checked).toBe(false);
    expect(screen.getByText(/1 selected/i)).toBeTruthy();
  });

  it("select-all-matching uses fetchAllPages when exporting selected PDF", async () => {
    const user = userEvent.setup();
    const page1Items = Array.from({ length: 100 }, (_, i) =>
      makeRecord({ id: i + 1, referenceNumber: `FP-P1-${i + 1}` })
    );
    const page2Items = [makeRecord({ id: 101, referenceNumber: "FP-P2-101" })];

    mockedSearchResults.mockImplementation(async (params) => {
      if (params.page === 1) {
        return { items: page1Items, totalCount: 101, page: 1, pageSize: 100 };
      }
      return { items: page2Items, totalCount: 101, page: 2, pageSize: 100 };
    });

    renderTable({ items: page1Items.slice(0, 25), totalCount: 101, page: 1, pageSize: 25 });

    await user.click(screen.getByText(/Select All 101 Matching Records/i));
    expect(screen.getByText(/All 101 selected/i)).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Export Selected PDF" }));

    await waitFor(() => {
      expect(mockedSearchResults).toHaveBeenCalledTimes(2);
    });
    expect(mockedSearchResults).toHaveBeenNthCalledWith(
      1,
      expect.objectContaining({ page: 1, pageSize: 100 })
    );
    expect(mockedSearchResults).toHaveBeenNthCalledWith(
      2,
      expect.objectContaining({ page: 2, pageSize: 100 })
    );
    expect(writtenPdfHtml).toHaveLength(1);
    expect(writtenPdfHtml[0]).toContain("FP-P1-1");
    expect(writtenPdfHtml[0]).toContain("FP-P2-101");
  });

  it("server export call ReportingService.exportCsv(appliedParams) is unchanged", async () => {
    const user = userEvent.setup();
    mockedExportCsv.mockResolvedValue(undefined);
    const items = [makeRecord({ id: 1, referenceNumber: "FP-001" })];
    renderTable({ items, totalCount: 1, page: 1, pageSize: 25 }, APPLIED_PARAMS);

    await user.click(screen.getByRole("button", { name: "Export CSV" }));
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => {
      expect(mockedExportCsv).toHaveBeenCalledTimes(1);
    });
    expect(mockedExportCsv).toHaveBeenCalledWith(APPLIED_PARAMS);
  });

  it("PDF export receives only the selected records", async () => {
    const user = userEvent.setup();
    const items = [
      makeRecord({ id: 101, referenceNumber: "FP-SEL-101", approvalStatus: "Approved" }),
      makeRecord({ id: 102, referenceNumber: "FP-UNSEL-102", approvalStatus: "Approved" }),
    ];
    renderTable({ items, totalCount: 2, page: 1, pageSize: 25 });

    await user.click(screen.getByRole("checkbox", { name: "Select row 101" }));
    await user.click(screen.getByRole("button", { name: "Export Selected PDF" }));

    expect(writtenPdfHtml).toHaveLength(1);
    expect(writtenPdfHtml[0]).toContain("FP-SEL-101");
    expect(writtenPdfHtml[0]).not.toContain("FP-UNSEL-102");
  });

  it("Export Selected CSV logs through exportLogService and builds CSV with exportCsv.ts keeping the same headers", async () => {
    const user = userEvent.setup();
    const items = [
      makeRecord({ id: 101, referenceNumber: "=FP-SEL-101", approvalStatus: "Approved" }),
      makeRecord({ id: 102, referenceNumber: "FP-UNSEL-102", approvalStatus: "Approved" }),
    ];
    renderTable({ items, totalCount: 2, page: 1, pageSize: 25 });

    await user.click(screen.getByRole("checkbox", { name: "Select row 101" }));
    await user.click(screen.getByRole("button", { name: "Export Selected CSV" }));

    await waitFor(() => {
      expect(mockedExportLog).toHaveBeenCalledTimes(1);
    });
    expect(mockedExportLog).toHaveBeenCalledWith({
      tableKey: "reports.results",
      filterJson: JSON.stringify(APPLIED_PARAMS),
      columns: [
        "Date/Time",
        "Reference",
        "Subject",
        "Subject Detail",
        "Category",
        "Test Code",
        "Test Name",
        "Reported Value",
        "Unit",
        "Result Level",
        "Alert Limit",
        "Action Limit",
        "Spec Limit",
        "Status",
        "Analyst",
        "Approved By",
        "Approval Date",
      ],
      rowCount: 1,
      recordIds: ["101"],
    });

    await waitFor(() => {
      expect(downloadedBlobs).toHaveLength(1);
    });
    const bytes = new Uint8Array(await downloadedBlobs[0].arrayBuffer());
    const text = new TextDecoder("utf-8", { ignoreBOM: true }).decode(bytes);
    expect(text.startsWith("\uFEFF")).toBe(true);
    const lines = text.slice(1).split("\r\n");
    expect(lines[0]).toBe("Exported by,Dr. Ada");
    expect(lines[5]).toBe("Export ID,501");
    expect(lines[6]).toBe("");
    expect(lines[7]).toBe(
      "Date/Time,Reference,Subject,Subject Detail,Category,Test Code,Test Name,Reported Value,Unit,Result Level,Alert Limit,Action Limit,Spec Limit,Status,Analyst,Approved By,Approval Date"
    );
    expect(lines[8]).toContain("'=FP-SEL-101");
  });
});
