import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { AuditTrailReviewsPage } from "./AuditTrailReviewsPage";
import {
  auditTrailReviewService,
  type AuditTrailReviewDto,
  type AuditTrailReviewListItemDto
} from "../../services/auditTrailReviewService";

vi.mock("../../services/auditTrailReviewService", () => ({
  auditTrailReviewService: {
    list: vi.fn(),
    get: vi.fn(),
    getEntries: vi.fn(),
    start: vi.fn(),
    addFinding: vi.fn(),
    sign: vi.fn(),
    closeFinding: vi.fn(),
    exportPdf: vi.fn()
  }
}));

function seedAuth(permissions: string[], role: "Qa" | "Reviewer" = "Qa", userId = 10) {
  const b64 = (o: object) => btoa(JSON.stringify(o)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role, permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
  localStorage.setItem("microlims_full_name", "Quinn QA");
  localStorage.setItem("microlims_username", "qa");
  localStorage.setItem("microlims_user_id", String(userId));
}

const makeListItem = (over: Partial<AuditTrailReviewListItemDto> = {}): AuditTrailReviewListItemDto => ({
  id: 1,
  title: "Q4 Periodic Audit Review",
  periodFromUtc: "2026-10-01T00:00:00Z",
  periodToUtc: "2026-10-15T23:59:00Z",
  scopeKind: "All",
  scopeEntityName: null,
  scopeRecordId: null,
  cutoffAuditLogId: 500,
  entryCount: 42,
  newerEntriesCount: 0,
  findingCount: 1,
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

const makeCreatedDetail = (id = 99): AuditTrailReviewDto => ({
  id,
  title: "October Sample Review",
  periodFromUtc: "2026-10-01T09:00:00Z",
  periodToUtc: "2026-10-10T10:00:00Z",
  scopeKind: "All",
  scopeEntityName: null,
  scopeRecordId: null,
  cutoffAuditLogId: 750,
  entryCount: 12,
  newerEntriesCount: 0,
  startedByUserId: 10,
  startedByName: "Quinn QA",
  startedAtUtc: "2026-10-16T08:00:00Z",
  status: "InProgress",
  outcome: null,
  conclusion: null,
  signedAtUtc: null,
  electronicSignatureId: null,
  signerFullName: null,
  signerUsername: null,
  signatureMeaningText: null,
  version: 1,
  findings: []
});

function renderPage() {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={["/audit-trail-reviews"]}>
          <Routes>
            <Route path="/audit-trail-reviews" element={<AuditTrailReviewsPage />} />
            <Route path="/audit-trail-reviews/:id" element={<div>Review detail destination</div>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

beforeEach(() => {
  localStorage.clear();
  vi.clearAllMocks();
});

afterEach(() => {
  cleanup();
});

describe("AuditTrailReviewsPage", () => {
  it("starts a review and opens it", async () => {
    const user = userEvent.setup();
    seedAuth(["Audit.View", "Audit.Review"], "Qa");
    vi.mocked(auditTrailReviewService.list).mockResolvedValue([makeListItem()]);
    vi.mocked(auditTrailReviewService.start).mockResolvedValue(makeCreatedDetail(99));

    renderPage();
    expect(await screen.findByText("Q4 Periodic Audit Review")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Start review" }));
    await user.type(screen.getByLabelText(/Title/i), "October Sample Review");
    await user.type(screen.getByLabelText(/Period from/i), "2026-10-01T09:00");
    await user.type(screen.getByLabelText(/Period to/i), "2026-10-10T10:00");
    const submitButtons = screen.getAllByRole("button", { name: "Start review" });
    await user.click(submitButtons[submitButtons.length - 1]);

    await waitFor(() => {
      expect(auditTrailReviewService.start).toHaveBeenCalledWith({
        title: "October Sample Review",
        periodFromUtc: new Date("2026-10-01T09:00").toISOString(),
        periodToUtc: new Date("2026-10-10T10:00").toISOString(),
        scopeKind: "All",
        entityName: null,
        recordId: null
      });
    });
    expect(await screen.findByText("Review detail destination")).toBeTruthy();
  });

  it("user without Audit.Review sees reviews but no actions", async () => {
    seedAuth(["Audit.View"], "Reviewer");
    vi.mocked(auditTrailReviewService.list).mockResolvedValue([makeListItem()]);

    renderPage();
    expect(await screen.findByText("Q4 Periodic Audit Review")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Start review" })).toBeNull();
  });

  it("empty list shows the empty state", async () => {
    seedAuth(["Audit.View"], "Reviewer");
    vi.mocked(auditTrailReviewService.list).mockResolvedValue([]);

    renderPage();
    expect(await screen.findByRole("status")).toBeTruthy();
    expect(screen.getByText(/No audit trail reviews/i)).toBeTruthy();
  });
});
