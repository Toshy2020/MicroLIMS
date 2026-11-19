import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { exportRegisterRows } from "../../../components/lab/table/exportRows";
import { exportLogService } from "../../../components/lab/table/services/exportLogService";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { i18n } from "../../../i18n";
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
  assignedTests: []
};

const KEYS = ["item", "receivedAt", "category", "batchControl", "assignedTo", "sampleStatus", "due", "tests", "nextAction"];

let csv = "";

async function exportIt(): Promise<string> {
  csv = "";
  await exportRegisterRows({
    tableKey: "receiving.register",
    rows: [SAMPLE],
    columns: sampleExportColumns(KEYS, { canSelectTests: false, canAssign: true }),
    getRowId: (s) => s.sampleId,
    filters: {},
    search: "",
    t: (k) => k
  });
  await vi.waitFor(() => expect(csv).not.toBe(""));
  return csv;
}

beforeEach(async () => {
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
  await i18n.loadLanguages("ar");
  await i18n.loadNamespaces(["receiving", "common"]);
});

afterEach(async () => {
  vi.restoreAllMocks();
  await i18n.changeLanguage("en");
});

describe("register CSV export is English in every interface language", () => {
  it("is identical in Arabic and English, with no Arabic characters or bidi marks", async () => {
    await i18n.changeLanguage("en");
    const english = await exportIt();
    await i18n.changeLanguage("ar");
    const arabic = await exportIt();

    expect(arabic).toBe(english);
    expect(arabic).not.toMatch(/[\u0600-\u06FF]/);
    expect(arabic).not.toMatch(/[\u200E\u200F\u202A-\u202E\u2066-\u2069]/);
    expect(arabic).toContain(formatDateTimeWithTimeZone(DUE, { english: true }));
    expect(arabic).toContain("Under Testing");
  });
});
