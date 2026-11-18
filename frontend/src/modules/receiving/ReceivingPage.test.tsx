import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { MemoryRouter } from "react-router-dom";
import { lightTheme } from "../../theme";
import { AuthProvider } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { labelService } from "../../services/labelService";
import { masterDataOptions } from "../../services/masterDataOptions";
import { ReceiveService } from "./services/ReceiveService";
import type { SampleRecord } from "./types/receivingTypes";
import { ReceivingPage } from "./ReceivingPage";
import { exportLogService } from "../../components/lab/table/services/exportLogService";

vi.mock("./services/ReceiveService", () => ({
  ReceiveService: {
    getRecordsPaged: vi.fn(),
    receiveBatch: vi.fn(),
    receiveItemBased: vi.fn(),
    receiveWater: vi.fn(),
    receiveEM: vi.fn(),
    receiveAfterCleaning: vi.fn(),
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

// Network boundaries of the table: the export log (the audit entry of an export) and the saved views.
vi.mock("../../components/lab/table/services/exportLogService", () => ({ exportLogService: { log: vi.fn() } }));
vi.mock("../../components/lab/table/services/tableViewService", () => ({
  tableViewService: { list: vi.fn().mockResolvedValue([]), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setDefault: vi.fn() }
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

function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "SectionHead", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

const createdSample: SampleRecord = {
  id: 501,
  sampleId: 501,
  referenceNumber: "RM-2026-0501",
  category: "RawMaterial",
  displayName: "Lactose Monohydrate",
  departmentId: null,
  machineId: null,
  waterDepartmentId: null,
  productionStage: null,
  causeOfTesting: "Routine",
  batchNumber: "LOT-LM-01",
  controlNumber: "CTRL-01",
  status: "Received",
  preparationStatus: "NotRequired",
  receivedAt: "2026-11-13T08:00:00Z",
  sampledBy: "Analyst Alice",
  assignedTests: []
};

function renderPage() {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter>
          <ReceivingPage />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

async function receiveRawMaterialSample(user: ReturnType<typeof userEvent.setup>) {
  const receiveButton = await screen.findByRole("button", { name: "Receive" });
  await user.click(receiveButton);

  await user.click(await screen.findByText("Raw Material"));
  await user.click(screen.getByRole("button", { name: /Next/i }));

  const itemSelect = await screen.findByLabelText("Sample 1 item");
  await user.click(itemSelect);
  await user.click(await screen.findByRole("option", { name: "Lactose Monohydrate" }));

  const causeSelect = screen.getByLabelText("Sample 1 cause of testing");
  await user.click(causeSelect);
  await user.click(await screen.findByRole("option", { name: "Routine" }));

  const sampledByInput = screen.getByPlaceholderText("Sampled By");
  await user.type(sampledByInput, "Analyst Alice");

  await user.click(screen.getByRole("button", { name: /Save All Samples/i }));
}

describe("ReceivingPage label printing offer", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.SAMPLES_RECEIVE]);

    vi.mocked(ReceiveService.getRecordsPaged).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 100,
      totalPages: 0
    });
    vi.mocked(ReceiveService.receiveBatch).mockResolvedValue({
      samples: [{ row: 0, sampleId: 501, referenceNumber: "RM-2026-0501" }]
    });
    vi.mocked(ReceiveService.receiveItemBased).mockResolvedValue(createdSample);
    vi.mocked(ReceiveService.receiptLabs).mockResolvedValue([
      { sectionId: 2, sectionCode: "FP", sectionName: "Physicochemical", testCount: 1 }
    ]);
    vi.mocked(ReceiveService.itemGrades).mockResolvedValue([]);

    vi.mocked(masterDataOptions.getCausesOfTesting).mockResolvedValue([{ id: 1, name: "Routine" }]);
    vi.mocked(masterDataOptions.getSamplers).mockResolvedValue([{ id: 1, name: "Analyst Alice" }]);
    vi.mocked(masterDataOptions.getProductionStages).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getWaterSamplingPoints).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getWaterDepartments).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getDepartments).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getMachines).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getItems).mockResolvedValue([
      { id: 11, name: "Lactose Monohydrate", category: "RawMaterial" }
    ]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("offers label printing after sample is created", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      {
        entityType: "Sample",
        entityId: 501,
        canPrint: true,
        blockedReason: null,
        printCount: 0,
        defaultCopies: 1
      }
    ]);

    renderPage();
    await receiveRawMaterialSample(user);

    expect(await screen.findByText("Print labels")).toBeTruthy();
    expect(await screen.findByText("RM-2026-0501")).toBeTruthy();
  }, 15000);

  it("does not open the print dialog when printing is unavailable, while keeping the success flow", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      {
        entityType: "Sample",
        entityId: 501,
        canPrint: false,
        blockedReason: "Label printing is disabled.",
        printCount: 0,
        defaultCopies: 1
      }
    ]);

    renderPage();
    await receiveRawMaterialSample(user);

    expect(
      await screen.findByText("Successfully received 1 sample. Test orders generated automatically.")
    ).toBeTruthy();
    await waitFor(() => {
      // Initial load (3 categories) + reload after receipt (3 categories)
      expect(vi.mocked(ReceiveService.getRecordsPaged).mock.calls.length).toBeGreaterThanOrEqual(6);
    });
    expect(screen.queryByText("Print labels")).toBeNull();
  }, 15000);
});

describe("ReceivingPage export", () => {
  const rows = (category: string): SampleRecord[] =>
    category === "RawMaterial" ? [createdSample] : [];

  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(ReceiveService.getRecordsPaged).mockImplementation(async (filter) => {
      const items = rows(filter?.category ?? "");
      return { items, totalCount: items.length, page: 1, pageSize: 100, totalPages: items.length ? 1 : 0 };
    });
    vi.mocked(exportLogService.log).mockResolvedValue({
      exportId: 77, serverTimeUtc: "2026-11-17T10:00:00Z", timeZoneId: "UTC", exportedBy: "Hana Head"
    });
    vi.spyOn(URL, "createObjectURL").mockReturnValue("blob:x");
    vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => {});
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it("lets a user holding Data.Export select rows and export them, logging the export with the server", async () => {
    const user = userEvent.setup();
    seedPermissions([PERMISSIONS.SAMPLES_RECEIVE, PERMISSIONS.DATA_EXPORT]);
    renderPage();

    await user.click(await screen.findByRole("checkbox", { name: /Select Lactose Monohydrate/ }));
    await user.click(await screen.findByRole("button", { name: "Export selected" }));

    await waitFor(() => expect(exportLogService.log).toHaveBeenCalledTimes(1));
    expect(exportLogService.log).toHaveBeenCalledWith(
      expect.objectContaining({ tableKey: "samples.receiving", rowCount: 1, recordIds: ["501"] })
    );
  });

  it("offers no row selection to a user without Data.Export", async () => {
    seedPermissions([PERMISSIONS.SAMPLES_RECEIVE]);
    renderPage();

    await screen.findByText("Lactose Monohydrate");
    expect(screen.queryByRole("checkbox", { name: /Select Lactose Monohydrate/ })).toBeNull();
    expect(screen.queryByRole("button", { name: "Export selected" })).toBeNull();
  });
});
