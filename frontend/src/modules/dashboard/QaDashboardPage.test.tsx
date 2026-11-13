import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider, type Role } from "../../contexts/AuthContext";
import { DashboardPage } from "./DashboardPage";
import { QaDashboardPage } from "./QaDashboardPage";
import {
  auditTrailReviewService,
  type AuditTrailReviewListItemDto
} from "../../services/auditTrailReviewService";
import { documentApprovalService } from "../documentControl/services/documentApprovalService";
import type { DocumentApprovalTaskDto } from "../documentControl/types/documentControlTypes";
import { laboratorySectionService } from "../../services/laboratorySectionService";
import { DashboardService } from "./services/DashboardService";
import type { SectionHeadDashboard } from "./types/dashboard";

vi.mock("../../services/auditTrailReviewService", () => ({
  auditTrailReviewService: {
    list: vi.fn()
  }
}));

vi.mock("../documentControl/services/documentApprovalService", () => ({
  documentApprovalService: {
    getApprovalTasks: vi.fn()
  }
}));

vi.mock("../../services/laboratorySectionService", () => ({
  laboratorySectionService: {
    getMySections: vi.fn()
  }
}));

vi.mock("./services/DashboardService", () => ({
  DashboardService: {
    getSectionHeadDashboard: vi.fn(),
    getStatusDistribution: vi.fn(),
    getMonthlyTrend: vi.fn()
  }
}));

function seedAuth(role: Role, permissions: string[], userId = 10) {
  const b64 = (o: object) => btoa(JSON.stringify(o)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role, permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
  localStorage.setItem("microlims_full_name", "Quinn QA");
  localStorage.setItem("microlims_username", "qa");
  localStorage.setItem("microlims_user_id", String(userId));
}

const makeReviewItem = (over: Partial<AuditTrailReviewListItemDto> = {}): AuditTrailReviewListItemDto => ({
  id: 1,
  title: "Q4 Audit Review",
  periodFromUtc: "2026-10-01T00:00:00Z",
  periodToUtc: "2026-10-15T23:59:00Z",
  scopeKind: "All",
  scopeEntityName: null,
  scopeRecordId: null,
  cutoffAuditLogId: 500,
  entryCount: 42,
  newerEntriesCount: 0,
  findingCount: 2,
  openFindingCount: 1,
  startedByUserId: 10,
  startedByName: "Quinn QA",
  startedAtUtc: "2026-10-16T08:00:00Z",
  status: "InProgress",
  outcome: null,
  signedAtUtc: null,
  signerFullName: null,
  signatureMeaningText: null,
  version: 1,
  ...over
});

const makeApprovalTask = (id: number): DocumentApprovalTaskDto => ({
  id,
  documentRevisionId: 100 + id,
  documentMasterId: 50 + id,
  companyDocumentCode: `SOP-00${id}`,
  documentTitle: `Procedure ${id}`,
  revisionNumber: "01",
  revisionSequence: 1,
  assignedApproverUserId: 10,
  assignedApproverUsername: "qa",
  assignedApproverFullName: "Quinn QA",
  assignedByUserId: 2,
  assignedByUsername: "author",
  assignedByFullName: "Alex Author",
  assignedAt: "2026-10-16T08:00:00Z",
  dueDate: null,
  status: "Pending",
  decision: null,
  decisionAt: null,
  decisionByUserId: null,
  decisionByUsername: null,
  decisionByFullName: null,
  decisionNotes: null,
  submissionNotes: null,
  targetEffectiveDate: null
});

const makeSectionHeadDashboard = (): SectionHeadDashboard => ({
  activeTests: 4,
  incubating: 1,
  readyToRead: 2,
  pendingReview: 3,
  pendingApproval: 1,
  overdue: 0,
  attentionCount: 0,
  testingBottleneck: 1,
  incubationBottleneck: 1,
  readyToReadBottleneck: 1,
  reviewBottleneck: 1,
  approvalBottleneck: 1,
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
});

function renderQaDashboard() {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={["/dashboard"]}>
          <Routes>
            <Route path="/dashboard" element={<QaDashboardPage />} />
            <Route path="/audit-trail-reviews" element={<div>Audit trail reviews page</div>} />
            <Route path="/document-control/library" element={<div>Document library page</div>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

function renderDashboardRoute() {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={["/dashboard"]}>
          <Routes>
            <Route path="/dashboard" element={<DashboardPage />} />
            <Route path="/laboratory-configuration/items" element={<div>Items page</div>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

beforeEach(() => {
  localStorage.clear();
  vi.clearAllMocks();
  vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([]);
  vi.mocked(auditTrailReviewService.list).mockResolvedValue([]);
  vi.mocked(documentApprovalService.getApprovalTasks).mockResolvedValue([]);
  vi.mocked(DashboardService.getSectionHeadDashboard).mockResolvedValue(makeSectionHeadDashboard());
  vi.mocked(DashboardService.getStatusDistribution).mockResolvedValue([]);
  vi.mocked(DashboardService.getMonthlyTrend).mockResolvedValue([]);
});

afterEach(() => {
  cleanup();
});

describe("QaDashboardPage and dashboard routing", () => {
  it("Qa lands on the QA dashboard", async () => {
    seedAuth("Qa", ["Audit.View", "Audit.Review", "Documents.Approve"], 10);

    renderDashboardRoute();

    expect(await screen.findByRole("heading", { name: /Quality Assurance/i })).toBeTruthy();
    expect(screen.getByText("Audit trail reviews in progress")).toBeTruthy();
    expect(screen.queryByRole("heading", { name: /Section Head Command Center/i })).toBeNull();
  });

  it("QcManager still lands on the Section Head dashboard", async () => {
    seedAuth("QcManager", ["Dashboards.LabOverview"], 11);

    renderDashboardRoute();

    expect(await screen.findByRole("heading", { name: /Section Head Command Center/i })).toBeTruthy();
    expect(screen.queryByRole("heading", { name: /Quality Assurance/i })).toBeNull();
  });

  it("shows the three counts", async () => {
    seedAuth("Qa", ["Audit.View", "Audit.Review", "Documents.Approve"], 10);
    vi.mocked(auditTrailReviewService.list).mockResolvedValue([
      makeReviewItem({ id: 1, status: "InProgress", openFindingCount: 2 }),
      makeReviewItem({ id: 2, status: "InProgress", openFindingCount: 3 }),
      makeReviewItem({ id: 3, status: "Signed", openFindingCount: 1 })
    ]);
    vi.mocked(documentApprovalService.getApprovalTasks).mockResolvedValue([
      makeApprovalTask(10),
      makeApprovalTask(11),
      makeApprovalTask(12),
      makeApprovalTask(13)
    ]);

    renderQaDashboard();

    const reviewsLink = await screen.findByRole("link", { name: /Audit trail reviews in progress/i });
    expect(reviewsLink.textContent).toContain("2");
    expect(reviewsLink.getAttribute("href")).toBe("/audit-trail-reviews");

    const findingsLink = screen.getByRole("link", { name: /Open findings/i });
    expect(findingsLink.textContent).toContain("6");
    expect(findingsLink.getAttribute("href")).toBe("/audit-trail-reviews");

    const approvalsLink = screen.getByRole("link", { name: /Documents waiting for my approval/i });
    expect(approvalsLink.textContent).toContain("4");
    expect(approvalsLink.getAttribute("href")).toBe("/document-control/library");

    expect(documentApprovalService.getApprovalTasks).toHaveBeenCalledWith(
      { approverUserId: 10, status: "Pending" },
      expect.any(AbortSignal)
    );
  });

  it("dashboard shows partial outage when one call fails", async () => {
    seedAuth("Qa", ["Audit.View", "Audit.Review", "Documents.Approve"], 10);
    vi.mocked(auditTrailReviewService.list).mockRejectedValue(new Error("Service unavailable"));
    vi.mocked(documentApprovalService.getApprovalTasks).mockResolvedValue([makeApprovalTask(21)]);

    renderQaDashboard();

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Audit trail reviews");

    const approvalsLink = screen.getByRole("link", { name: /Documents waiting for my approval/i });
    expect(approvalsLink.textContent).toContain("1");

    const reviewsLink = screen.getByRole("link", { name: /Audit trail reviews in progress/i });
    expect(reviewsLink.textContent).toContain("—");
  });
});
