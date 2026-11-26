import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { sectionSelfAssignmentService } from "../../services/sectionSelfAssignmentService";
import { qualityDashboardService } from "../../services/qualityDashboardService";
import { DashboardService } from "./services/DashboardService";
import { DashboardLabProvider, makeDashboardLab } from "./DashboardLabContext";
import { SectionHeadDashboardPage } from "./SectionHeadDashboardPage";
import type { SectionHeadDashboard } from "./types/dashboard";

vi.mock("./services/DashboardService", () => ({
  DashboardService: {
    getSectionHeadDashboard: vi.fn(),
    getStatusDistribution: vi.fn(),
    getMonthlyTrend: vi.fn()
  }
}));

vi.mock("../../services/sectionSelfAssignmentService", () => ({
  sectionSelfAssignmentService: {
    getAll: vi.fn(),
    update: vi.fn()
  }
}));

vi.mock("../../services/qualityDashboardService", () => ({
  qualityDashboardService: {
    getDashboard: vi.fn()
  }
}));

function seedAuth(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "SectionHead", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
  localStorage.setItem("microlims_full_name", "Sam Head");
  localStorage.setItem("microlims_username", "shead");
  localStorage.setItem("microlims_user_id", "7");
}

function makeDashboard(): SectionHeadDashboard {
  return {
    activeTests: 4,
    incubating: 1,
    readyToRead: 2,
    pendingReview: 1,
    pendingApproval: 0,
    overdue: 0,
    attentionCount: 0,
    testingBottleneck: 1,
    incubationBottleneck: 1,
    readyToReadBottleneck: 2,
    reviewBottleneck: 1,
    approvalBottleneck: 0,
    attentionItems: [],
    reviewQueueCount: 0,
    reviewQueueOverdueCount: 0,
    reviewQueueOldestHours: 0,
    reviewQueueItems: [],
    approvalQueueCount: 0,
    approvalQueueOverdueCount: 0,
    approvalQueueOldestHours: 0,
    approvalQueueItems: [],
    incubationSummary: [],
    analystWorkloads: []
  };
}

function renderSectionHeadDashboard() {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <DashboardLabProvider value={makeDashboardLab("MICRO")}>
            <SectionHeadDashboardPage />
          </DashboardLabProvider>
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

describe("SectionHeadDashboardPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(DashboardService.getSectionHeadDashboard).mockResolvedValue(makeDashboard());
    vi.mocked(DashboardService.getStatusDistribution).mockResolvedValue([]);
    vi.mocked(DashboardService.getMonthlyTrend).mockResolvedValue([]);
    vi.mocked(sectionSelfAssignmentService.getAll).mockResolvedValue([
      {
        sectionId: 1,
        sectionName: "Microbiology",
        enabled: true,
        changedAt: null,
        changedByName: null,
        version: 0
      }
    ]);
    vi.mocked(qualityDashboardService.getDashboard).mockResolvedValue({
      waitingForMyDecision: [],
      myOpenRecords: [],
      myOpenActions: [],
      overdueRecords: [],
      overdueActions: [],
      effectivenessChecksDue: [],
      waitingForMyDecisionTotalCount: 0,
      myOpenRecordsTotalCount: 2,
      myOpenActionsTotalCount: 1,
      overdueRecordsTotalCount: 1,
      overdueActionsTotalCount: 0,
      effectivenessChecksDueTotalCount: 0,
      laboratoryOpenRecords: [],
      laboratoryOpenRecordsTotalCount: 4,
      laboratoryOpenActions: [],
      laboratoryOpenActionsTotalCount: 3
    });
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("shows the Taking tests from My Work card for users holding Samples.AssignAnalyst", async () => {
    seedAuth([PERMISSIONS.DASHBOARDS_LAB_OVERVIEW, PERMISSIONS.SAMPLES_ASSIGN_ANALYST]);
    renderSectionHeadDashboard();

    expect(await screen.findByText("Laboratory Analyst Workload Allocation")).toBeTruthy();
    expect(await screen.findByRole("heading", { name: "Taking tests from My Work" })).toBeTruthy();
    expect(await screen.findByText("Microbiology")).toBeTruthy();
  });

  it("hides the Taking tests from My Work card when the user lacks Samples.AssignAnalyst", async () => {
    seedAuth([PERMISSIONS.DASHBOARDS_LAB_OVERVIEW]);
    renderSectionHeadDashboard();

    expect(await screen.findByText("Laboratory Analyst Workload Allocation")).toBeTruthy();
    expect(screen.queryByRole("heading", { name: "Taking tests from My Work" })).toBeNull();
    expect(sectionSelfAssignmentService.getAll).not.toHaveBeenCalled();
  });

  it("shows Section Head quality tiles with laboratory open count and partial outage when quality fails", async () => {
    seedAuth([PERMISSIONS.DASHBOARDS_LAB_OVERVIEW, PERMISSIONS.QUALITY_VIEW]);
    renderSectionHeadDashboard();

    const labOpenLink = await screen.findByRole("link", { name: /Lab open records & actions/i });
    expect(labOpenLink.textContent).toContain("7");
    expect(labOpenLink.getAttribute("href")).toBe("/quality");

    expect(screen.getByRole("link", { name: /My open records & actions/i }).textContent).toContain("3");
    expect(screen.getByRole("link", { name: /Overdue quality items/i }).textContent).toContain("1");

    cleanup();
    vi.mocked(qualityDashboardService.getDashboard).mockRejectedValueOnce(new Error("Down"));
    renderSectionHeadDashboard();

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Quality events");
    expect(screen.getByRole("link", { name: /Lab open records & actions/i }).textContent).toContain("—");
  });
});
