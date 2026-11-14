import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { ReceiveService } from "../receiving/services/ReceiveService";
import type { SampleRecord } from "../receiving/types/receivingTypes";
import { masterDataOptions } from "../../services/masterDataOptions";
import { labelService } from "../../services/labelService";
import { lookupCache } from "../../services/lookupCache";
import { TestWorkflowService } from "../testingWorkspace/services/TestWorkflowService";
import { SampleSummaryService } from "../testingWorkspace/services/SampleSummaryService";
import { UserService } from "../users/services/UserService";
import { ReceivingTestingWorkspacePage } from "./ReceivingTestingWorkspacePage";

vi.mock("../receiving/services/ReceiveService", () => ({
  ReceiveService: {
    getRecordsPaged: vi.fn(),
    getWorkloadCounts: vi.fn(),
    getSample: vi.fn(),
    getAssignmentOptions: vi.fn(),
    assignTestAnalysts: vi.fn(),
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
    getCurrentStep: vi.fn()
  }
}));

vi.mock("../testingWorkspace/services/SampleSummaryService", () => ({
  SampleSummaryService: {
    getSummary: vi.fn()
  }
}));

vi.mock("../users/services/UserService", () => ({
  UserService: {
    getAll: vi.fn(),
    getEligibleAnalysts: vi.fn()
  }
}));

function seedPermissions(permissions: string[], role = "Analyst") {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role, permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

function makeSample(overrides: Partial<SampleRecord> = {}): SampleRecord {
  return {
    id: 42,
    sampleId: 42,
    referenceNumber: "FP-2026-0042",
    category: "FinishedProduct",
    displayName: "Amoxicillin 500mg",
    departmentId: null,
    machineId: null,
    waterDepartmentId: null,
    productionStage: null,
    causeOfTesting: "Routine",
    batchNumber: "BATCH-42",
    controlNumber: "CTRL-42",
    status: "InTesting",
    preparationStatus: "Completed",
    receivedAt: "2026-11-13T08:00:00Z",
    sampledBy: "Analyst Alice",
    assignedTests: [
      {
        testOrderId: 10,
        sectionId: 1,
        testCode: "TAMC",
        status: "InProgress",
        workflowStatus: "ReadyToRead",
        locationCount: 0,
        assignedAnalystId: 5,
        assignedAnalystName: "Analyst Alice"
      }
    ],
    ...overrides
  };
}

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="location-search">{location.search}</output>;
}

function renderWorkspaceAt(initialUrl: string) {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={[initialUrl]}>
          <LocationProbe />
          <ReceivingTestingWorkspacePage
            lab={{ sectionId: 1, code: "MICRO", name: "Microbiology Laboratory" }}
          />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

describe("ReceivingTestingWorkspacePage deep links", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([
      PERMISSIONS.TEST_WORKFLOW_EXECUTE,
      PERMISSIONS.SAMPLES_REVIEW,
      PERMISSIONS.SAMPLES_APPROVE,
      PERMISSIONS.SAMPLES_ASSIGN_ANALYST
    ]);

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
    vi.mocked(ReceiveService.getAssignmentOptions).mockResolvedValue({
      sampleId: 42,
      tests: [],
      users: []
    });
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
    vi.mocked(UserService.getAll).mockResolvedValue([]);
    vi.mocked(UserService.getEligibleAnalysts).mockResolvedValue([]);
    vi.mocked(TestWorkflowService.getCurrentStep).mockReturnValue(new Promise(() => undefined));
    vi.mocked(SampleSummaryService.getSummary).mockReturnValue(new Promise(() => undefined));
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("?sampleId=&testOrderId=&open=step opens the step dialog once and clears the parameter", async () => {
    const sample = makeSample();
    vi.mocked(ReceiveService.getSample).mockResolvedValue(sample);

    renderWorkspaceAt("/microbiology/workspace?sampleId=42&testOrderId=10&open=step");

    expect(await screen.findByText("TAMC Workflow")).toBeTruthy();
    await waitFor(() => {
      expect(screen.getByTestId("location-search").textContent).toBe("?sampleId=42");
    });
  });

  it("?sampleId=&testOrderId=&open=incubation&incubationId= opens the workflow dialog once and clears both parameters", async () => {
    const sample = makeSample();
    vi.mocked(ReceiveService.getSample).mockResolvedValue(sample);

    renderWorkspaceAt("/microbiology/workspace?sampleId=42&testOrderId=10&open=incubation&incubationId=56");

    expect(await screen.findByText("TAMC Workflow")).toBeTruthy();
    await waitFor(() => {
      expect(screen.getByTestId("location-search").textContent).toBe("?sampleId=42");
    });
  });

  it("?sampleId=&open=review opens the sample summary dialog when UnderReview and user has Samples.Review, then clears open", async () => {
    const sample = makeSample({ status: "UnderReview" });
    vi.mocked(ReceiveService.getSample).mockResolvedValue(sample);

    renderWorkspaceAt("/microbiology/workspace?sampleId=42&open=review");

    expect(await screen.findByRole("dialog", { name: /Sample Summary/i })).toBeTruthy();
    await waitFor(() => {
      expect(screen.getByTestId("location-search").textContent).toBe("?sampleId=42");
    });
  });

  it("?sampleId=&open=approval opens the sample summary dialog when UnderApproval and user has Samples.Approve, then clears open", async () => {
    const sample = makeSample({ status: "UnderApproval" });
    vi.mocked(ReceiveService.getSample).mockResolvedValue(sample);

    renderWorkspaceAt("/microbiology/workspace?sampleId=42&open=approval");

    expect(await screen.findByRole("dialog", { name: /Sample Summary/i })).toBeTruthy();
    await waitFor(() => {
      expect(screen.getByTestId("location-search").textContent).toBe("?sampleId=42");
    });
  });

  it("?sampleId=&testOrderId=&open=assign opens the assign analyst dialog when eligible, then clears open", async () => {
    const sample = makeSample();
    vi.mocked(ReceiveService.getSample).mockResolvedValue(sample);

    renderWorkspaceAt("/microbiology/workspace?sampleId=42&testOrderId=10&open=assign");

    expect(await screen.findByText("Assign Analysts")).toBeTruthy();
    await waitFor(() => {
      expect(screen.getByTestId("location-search").textContent).toBe("?sampleId=42");
    });
  });

  it("not eligible shows a non-blocking notice, does not open the dialog, and clears the parameter", async () => {
    const approvedSample = makeSample({
      status: "Approved",
      assignedTests: [
        {
          testOrderId: 10,
          sectionId: 1,
          testCode: "TAMC",
          status: "Approved",
          workflowStatus: "Completed",
          locationCount: 0,
          assignedAnalystId: 5,
          assignedAnalystName: "Analyst Alice"
        }
      ]
    });
    vi.mocked(ReceiveService.getSample).mockResolvedValue(approvedSample);

    renderWorkspaceAt("/microbiology/workspace?sampleId=42&testOrderId=10&open=step");

    expect(await screen.findByText("This test is no longer waiting for this step.")).toBeTruthy();
    expect(screen.queryByText("TAMC Workflow")).toBeNull();
    await waitFor(() => {
      expect(screen.getByTestId("location-search").textContent).toBe("?sampleId=42");
    });
  });

  it.each([
    {
      openValue: "review",
      status: "UnderReview",
      permissions: [PERMISSIONS.TEST_WORKFLOW_EXECUTE, PERMISSIONS.SAMPLES_APPROVE, PERMISSIONS.SAMPLES_ASSIGN_ANALYST],
      notice: "You do not have permission to review this sample."
    },
    {
      openValue: "approval",
      status: "UnderApproval",
      permissions: [PERMISSIONS.TEST_WORKFLOW_EXECUTE, PERMISSIONS.SAMPLES_REVIEW, PERMISSIONS.SAMPLES_ASSIGN_ANALYST],
      notice: "You do not have permission to approve this sample."
    },
    {
      openValue: "assign",
      status: "InTesting",
      permissions: [PERMISSIONS.TEST_WORKFLOW_EXECUTE, PERMISSIONS.SAMPLES_REVIEW, PERMISSIONS.SAMPLES_APPROVE],
      notice: "You do not have permission to assign analysts."
    }
  ])("not eligible by missing permission for open=$openValue shows a notice, does not open the dialog, and clears parameters", async ({
    openValue,
    status,
    permissions,
    notice
  }) => {
    seedPermissions(permissions);
    const sample = makeSample({ status });
    vi.mocked(ReceiveService.getSample).mockResolvedValue(sample);

    renderWorkspaceAt(`/microbiology/workspace?sampleId=42&testOrderId=10&open=${openValue}`);

    expect(await screen.findByText(notice)).toBeTruthy();
    expect(screen.queryByRole("dialog")).toBeNull();
    await waitFor(() => {
      expect(screen.getByTestId("location-search").textContent).toBe("?sampleId=42");
    });
  });

  it("unknown open value is ignored and cleared without opening a dialog", async () => {
    const sample = makeSample();
    vi.mocked(ReceiveService.getSample).mockResolvedValue(sample);

    renderWorkspaceAt("/microbiology/workspace?sampleId=42&open=unknown-action");

    await waitFor(() => {
      expect(screen.getByTestId("location-search").textContent).toBe("?sampleId=42");
    });
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("legacy link ?sampleId=&testOrderId= (no open) still opens the test dialog", async () => {
    const sample = makeSample();
    vi.mocked(ReceiveService.getSample).mockResolvedValue(sample);

    renderWorkspaceAt("/microbiology/workspace?sampleId=42&testOrderId=10");

    expect(await screen.findByText("TAMC Workflow")).toBeTruthy();
  });

  it("refresh after opening does not reopen the dialog", async () => {
    const sample = makeSample();
    vi.mocked(ReceiveService.getSample).mockResolvedValue(sample);

    renderWorkspaceAt("/microbiology/workspace?sampleId=42&testOrderId=10&open=step");

    expect(await screen.findByText("TAMC Workflow")).toBeTruthy();
    await waitFor(() => {
      expect(screen.getByTestId("location-search").textContent).toBe("?sampleId=42");
    });

    const clearedSearch = screen.getByTestId("location-search").textContent ?? "";
    expect(clearedSearch).not.toContain("testOrderId");
    cleanup();

    renderWorkspaceAt(`/microbiology/workspace${clearedSearch}`);

    expect(await screen.findByText(/Amoxicillin 500mg/)).toBeTruthy();
    expect(screen.queryByRole("dialog")).toBeNull();
  });
});
