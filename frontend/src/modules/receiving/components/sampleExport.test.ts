import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { exportRegisterRows } from "../../../components/lab/table/exportRows";
import { exportLogService } from "../../../components/lab/table/services/exportLogService";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import type { SampleRecord } from "../types/receivingTypes";
import { sampleExportColumns } from "./SampleRegisterTable";

vi.mock("../../../components/lab/table/services/exportLogService", () => ({
  exportLogService: { log: vi.fn() }
}));

const DUE = "2026-11-20T09:30:00Z";

const SAMPLE: SampleRecord = {
  sampleId: 42,
  referenceNumber: "FP-2026-0042",
  category: "Product",
  displayName: "Paracetamol 500mg",
  departmentId: null,
  machineId: null,
  waterDepartmentId: null,
  productionStage: null,
  causeOfTesting: "Routine",
  batchNumber: "B-77",
  controlNumber: "C-9",
  status: "InTesting",
  preparationStatus: "Prepared",
  receivedAt: "2026-11-18T08:00:00Z",
  dueAt: DUE,
  isOverdue: false,
  sampledBy: "QA",
  assignedTests: [
    {
      testOrderId: 1,
      sectionId: 2,
      testCode: "ASSAY",
      status: "Pending",
      workflowStatus: "Pending",
      locationCount: 0,
      assignedAnalystId: 5,
      assignedAnalystName: "Mona Ali"
    }
  ]
};

let csv = "";

beforeEach(() => {
  csv = "";
  vi.mocked(exportLogService.log).mockResolvedValue({
    exportId: 7,
    serverTimeUtc: "2026-11-18T18:13:48Z",
    timeZoneId: "Africa/Cairo",
    exportedBy: "Admin"
  });
  vi.spyOn(URL, "createObjectURL").mockImplementation((blob) => {
    void (blob as Blob).text().then((text) => {
      csv = text;
    });
    return "blob:test";
  });
  vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => undefined);
  vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => undefined);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("register CSV export", () => {
  it("carries what the screen shows, not raw epochs or enums", async () => {
    const keys = ["item", "assignedTo", "sampleStatus", "due", "tests", "nextAction"];
    await exportRegisterRows({
      tableKey: "receiving.register",
      rows: [SAMPLE],
      columns: sampleExportColumns(keys, { canSelectTests: false, canAssign: true }),
      getRowId: (s) => s.sampleId,
      filters: {},
      search: "",
      t: (k) => k
    });
    await vi.waitFor(() => expect(csv).not.toBe(""));

    expect(csv).toContain(formatDateTimeWithTimeZone(DUE));
    expect(csv).not.toContain(String(new Date(DUE).getTime()));
    expect(csv).toContain("Mona Ali");
    expect(csv).toContain("FP-2026-0042");
    expect(csv).toContain("Paracetamol 500mg");
    expect(csv).toContain("Under Testing");
    expect(csv).not.toContain("InTesting");
    expect(csv).toContain("Start Test");
    expect(csv).toContain("Exported at,2026-11-18T18:13:48Z (UTC)");
    expect(csv).not.toContain("Africa/Cairo");
    expect(csv).toContain(",1 Tests,"); // the register's own summary chip text
  });
});
