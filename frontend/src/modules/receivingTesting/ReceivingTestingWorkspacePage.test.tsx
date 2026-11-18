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
import { ReceiveService } from "../receiving/services/ReceiveService";
import type { SampleRecord } from "../receiving/types/receivingTypes";
import { ReceivingTestingWorkspacePage } from "./ReceivingTestingWorkspacePage";

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

function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "Analyst", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

const createdSample: SampleRecord = {
  id: 612,
  sampleId: 612,
  referenceNumber: "RM-2026-0612",
  category: "RawMaterial",
  displayName: "Starch",
  departmentId: null,
  machineId: null,
  waterDepartmentId: null,
  productionStage: null,
  causeOfTesting: "Routine",
  batchNumber: "LOT-ST-01",
  controlNumber: "CTRL-02",
  status: "Received",
  preparationStatus: "NotRequired",
  receivedAt: "2026-11-13T08:00:00Z",
  sampledBy: "Analyst Bob",
  assignedTests: []
};

function renderWorkspace() {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter>
          <ReceivingTestingWorkspacePage
            lab={{ sectionId: 2, code: "FP", name: "Physicochemical Laboratory", area: "rmpm" }}
          />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

async function receiveSampleFromWorkspace(user: ReturnType<typeof userEvent.setup>) {
  const newBtn = await screen.findByRole("button", { name: /\+ New Sample/i });
  await user.click(newBtn);

  await user.click(await screen.findByText("Raw Material"));
  await user.click(screen.getByRole("button", { name: /Next/i }));

  const itemSelect = await screen.findByLabelText("Sample 1 item");
  await user.click(itemSelect);
  await user.click(await screen.findByRole("option", { name: "Starch" }));

  const causeSelect = screen.getByLabelText("Sample 1 cause of testing");
  await user.click(causeSelect);
  await user.click(await screen.findByRole("option", { name: "Routine" }));

  const sampledByInput = screen.getByPlaceholderText("Sampled By");
  await user.type(sampledByInput, "Analyst Bob");

  await user.click(screen.getByRole("button", { name: /Save All Samples/i }));
}

describe("ReceivingTestingWorkspacePage label printing offer", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.SAMPLES_RECEIVE_OWN_LAB]);

    vi.mocked(ReceiveService.getRecordsPaged).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 50,
      totalPages: 0
    });
    vi.mocked(ReceiveService.getWorkloadCounts).mockResolvedValue({
      needsPreparation: 0,
      readyToRead: 0,
      awaitingReview: 0,
      overdue: 0,
      mine: 0,
      unassigned: 0
    });
    vi.mocked(ReceiveService.receiveBatch).mockResolvedValue({
      samples: [{ row: 0, sampleId: 612, referenceNumber: "RM-2026-0612" }]
    });
    vi.mocked(ReceiveService.receiveItemBased).mockResolvedValue(createdSample);
    vi.mocked(ReceiveService.itemGrades).mockResolvedValue([]);

    vi.mocked(masterDataOptions.getCausesOfTesting).mockResolvedValue([{ id: 1, name: "Routine" }]);
    vi.mocked(masterDataOptions.getSamplers).mockResolvedValue([{ id: 1, name: "Analyst Bob" }]);
    vi.mocked(masterDataOptions.getProductionStages).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getWaterSamplingPoints).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getWaterDepartments).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getDepartments).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getMachines).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getItems).mockResolvedValue([
      { id: 19, name: "Starch", category: "RawMaterial" }
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
        entityId: 612,
        canPrint: true,
        blockedReason: null,
        printCount: 0,
        defaultCopies: 1
      }
    ]);

    renderWorkspace();
    await receiveSampleFromWorkspace(user);

    expect(await screen.findByText("Print labels")).toBeTruthy();
    expect(await screen.findByText("RM-2026-0612")).toBeTruthy();
  }, 15000);

  it("does not open the print dialog when printing is unavailable, while keeping the success flow", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      {
        entityType: "Sample",
        entityId: 612,
        canPrint: false,
        blockedReason: "Label printing is disabled.",
        printCount: 0,
        defaultCopies: 1
      }
    ]);

    renderWorkspace();
    await receiveSampleFromWorkspace(user);

    expect(
      await screen.findByText(
        (_c, el) =>
          el?.classList.contains("MuiAlert-message") === true &&
          el.textContent === "Successfully received 1 sample. Test orders generated automatically."
      )
    ).toBeTruthy();
    await waitFor(() => {
      expect(vi.mocked(ReceiveService.getRecordsPaged).mock.calls.length).toBeGreaterThanOrEqual(2);
    });
    expect(screen.queryByText("Print labels")).toBeNull();
  }, 15000);
});
