import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { AuditTrailReviewDetailPage } from "./AuditTrailReviewDetailPage";
import {
  auditTrailReviewService,
  type AuditTrailReviewDto,
  type AuditTrailReviewFindingDto
} from "../../services/auditTrailReviewService";
import { qualityEntryService, type DeviationDto } from "../../services/qualityEntryService";
import type { AuditLogItem } from "../auditSearch/types/auditTypes";

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

vi.mock("../../services/qualityEntryService", () => ({
  qualityEntryService: {
    forTarget: vi.fn(),
    raiseDeviation: vi.fn(),
    raiseFromOosRequest: vi.fn(),
    escalateMonitoringDeviation: vi.fn(),
    raiseFromAuditFinding: vi.fn()
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

const makeEntry = (id = 101): AuditLogItem => ({
  id,
  entityName: "Sample",
  entityId: "55",
  action: "Update",
  previousValue: '{"Status":"InTesting"}',
  newValue: '{"Status":"UnderReview"}',
  userId: 3,
  userName: "Bob Analyst",
  userCurrentRole: "Analyst",
  userUsername: "bob",
  timestamp: "2026-10-05T14:20:00Z",
  batchNumber: null,
  controlNumber: null,
  sampleReferenceNumber: "SMP-55",
  mediaLotNumber: null,
  referenceStrainCode: null,
  cryovialCode: null,
  sampleId: 55,
  testOrderId: null,
  actionCategory: "Review",
  actorType: "User",
  correlationId: null,
  reason: "Submitted for review",
  entityIdNegative: false,
  correctedByAuditLogId: null
});

const makeFinding = (over: Partial<AuditTrailReviewFindingDto> = {}): AuditTrailReviewFindingDto => ({
  id: 301,
  reviewId: 7,
  auditLogId: 101,
  note: "Missing comment on correction",
  status: "Open",
  closedComment: null,
  closedByUserId: null,
  closedByName: null,
  closedAtUtc: null,
  recordedByUserId: 10,
  recordedByName: "Quinn QA",
  recordedAtUtc: "2026-10-16T09:00:00Z",
  version: 0,
  ...over
});

const makeReview = (over: Partial<AuditTrailReviewDto> = {}): AuditTrailReviewDto => ({
  id: 7,
  title: "Q4 Sample Audit Review",
  periodFromUtc: "2026-10-01T00:00:00Z",
  periodToUtc: "2026-10-15T23:59:00Z",
  scopeKind: "EntityName",
  scopeEntityName: "Sample",
  scopeRecordId: null,
  cutoffAuditLogId: 500,
  entryCount: 1,
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
  version: 4,
  findings: [],
  ...over
});

function renderPage(id = 7) {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={[`/audit-trail-reviews/${id}`]}>
          <Routes>
            <Route path="/audit-trail-reviews/:id" element={<AuditTrailReviewDetailPage />} />
            <Route path="/audit-trail-reviews" element={<div>Reviews list</div>} />
            <Route path="/quality/:id" element={<div>Deviation page opened</div>} />
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

describe("AuditTrailReviewDetailPage", () => {
  it("marks an entry as a finding with a note", async () => {
    const user = userEvent.setup();
    seedAuth(["Audit.View", "Audit.Review"], "Qa", 10);
    const createdFinding = makeFinding();
    vi.mocked(auditTrailReviewService.get)
      .mockResolvedValueOnce(makeReview())
      .mockResolvedValueOnce(makeReview({ findings: [createdFinding] }));
    vi.mocked(auditTrailReviewService.getEntries).mockResolvedValue({
      items: [makeEntry(101)],
      page: 1,
      pageSize: 25,
      totalCount: 1,
      totalPages: 1
    });
    vi.mocked(auditTrailReviewService.addFinding).mockResolvedValue(createdFinding);

    renderPage();

    const markBtn = await screen.findByRole("button", { name: /Mark.*101.*finding/i });
    await user.click(markBtn);

    await user.type(screen.getByLabelText(/Finding note/i), "Missing comment on correction");
    await user.click(screen.getByRole("button", { name: "Record finding" }));

    await waitFor(() => {
      expect(auditTrailReviewService.addFinding).toHaveBeenCalledWith(7, {
        auditLogId: 101,
        note: "Missing comment on correction"
      });
    });
    expect(await screen.findByText("Missing comment on correction")).toBeTruthy();
  });

  it("sign sends password, conclusion and version", async () => {
    const user = userEvent.setup();
    seedAuth(["Audit.View", "Audit.Review"], "Qa", 10);
    const signedReview = makeReview({
      status: "Signed",
      outcome: "NoFindings",
      conclusion: "All entries verified.",
      signedAtUtc: "2026-10-16T10:30:00Z",
      electronicSignatureId: 88,
      signerFullName: "Quinn QA",
      signerUsername: "qa",
      signatureMeaningText: "Audit trail review",
      version: 5
    });
    vi.mocked(auditTrailReviewService.get).mockResolvedValue(makeReview({ version: 4 }));
    vi.mocked(auditTrailReviewService.getEntries).mockResolvedValue({
      items: [makeEntry(101)],
      page: 1,
      pageSize: 25,
      totalCount: 1,
      totalPages: 1
    });
    vi.mocked(auditTrailReviewService.sign).mockResolvedValue(signedReview);

    renderPage();

    await user.click(await screen.findByRole("button", { name: "Sign review" }));
    await user.type(screen.getByLabelText(/Conclusion/i), "All entries verified.");
    await user.type(screen.getByLabelText(/^Password/i), "Secret#123");
    await user.click(screen.getByRole("button", { name: /^Sign$/ }));

    await waitFor(() => {
      expect(auditTrailReviewService.sign).toHaveBeenCalledWith(
        7,
        { password: "Secret#123", conclusion: "All entries verified." },
        4
      );
    });
    expect(await screen.findByText(/All entries verified\./)).toBeTruthy();
    expect(screen.getByText("Audit trail review")).toBeTruthy();
  });

  it("wrong password shows the server error and stays open", async () => {
    const user = userEvent.setup();
    seedAuth(["Audit.View", "Audit.Review"], "Qa", 10);
    vi.mocked(auditTrailReviewService.get).mockResolvedValue(makeReview({ version: 4 }));
    vi.mocked(auditTrailReviewService.getEntries).mockResolvedValue({
      items: [makeEntry(101)],
      page: 1,
      pageSize: 25,
      totalCount: 1,
      totalPages: 1
    });
    vi.mocked(auditTrailReviewService.sign).mockRejectedValue({
      response: { data: { message: "Invalid password for electronic signature." } }
    });

    renderPage();

    await user.click(await screen.findByRole("button", { name: "Sign review" }));
    await user.type(screen.getByLabelText(/Conclusion/i), "Checked.");
    await user.type(screen.getByLabelText(/^Password/i), "wrong-pw");
    await user.click(screen.getByRole("button", { name: /^Sign$/ }));

    expect(await screen.findByText("Invalid password for electronic signature.")).toBeTruthy();
    expect(screen.getByRole("button", { name: /^Sign$/ })).toBeTruthy();
  });

  it("signed review is read-only and shows the signature", async () => {
    seedAuth(["Audit.View", "Audit.Review"], "Qa", 10);
    vi.mocked(auditTrailReviewService.get).mockResolvedValue(
      makeReview({
        status: "Signed",
        outcome: "FindingsRecorded",
        conclusion: "One deviation raised.",
        signedAtUtc: "2026-10-16T10:30:00Z",
        electronicSignatureId: 88,
        signerFullName: "Quinn QA",
        signerUsername: "qa",
        signatureMeaningText: "Audit trail review",
        findings: [makeFinding()]
      })
    );
    vi.mocked(auditTrailReviewService.getEntries).mockResolvedValue({
      items: [makeEntry(101)],
      page: 1,
      pageSize: 25,
      totalCount: 1,
      totalPages: 1
    });

    renderPage();

    expect(await screen.findByText("Quinn QA")).toBeTruthy();
    expect(screen.getByText("Audit trail review")).toBeTruthy();
    expect(screen.getByText(/One deviation raised\./)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Sign review" })).toBeNull();
    expect(screen.queryByRole("button", { name: /Mark.*finding/i })).toBeNull();
    expect(screen.queryByRole("button", { name: /Close finding/i })).toBeNull();
    expect(screen.getByRole("button", { name: /Export PDF/i })).toBeTruthy();
  });

  it("newer entries notice", async () => {
    seedAuth(["Audit.View"], "Reviewer", 20);
    vi.mocked(auditTrailReviewService.get).mockResolvedValue(makeReview({ newerEntriesCount: 3 }));
    vi.mocked(auditTrailReviewService.getEntries).mockResolvedValue({
      items: [makeEntry(101)],
      page: 1,
      pageSize: 25,
      totalCount: 1,
      totalPages: 1
    });

    renderPage();

    expect(await screen.findByText(/3 newer entries exist after this review's cut-off/i)).toBeTruthy();
  });

  it("user without Audit.Review sees reviews but no actions", async () => {
    seedAuth(["Audit.View"], "Reviewer", 20);
    vi.mocked(auditTrailReviewService.get).mockResolvedValue(makeReview({ findings: [makeFinding()] }));
    vi.mocked(auditTrailReviewService.getEntries).mockResolvedValue({
      items: [makeEntry(101)],
      page: 1,
      pageSize: 25,
      totalCount: 1,
      totalPages: 1
    });

    renderPage();

    expect(await screen.findByText("Q4 Sample Audit Review")).toBeTruthy();
    expect(screen.getByText("Missing comment on correction")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Sign review" })).toBeNull();
    expect(screen.queryByRole("button", { name: /Mark.*finding/i })).toBeNull();
    expect(screen.queryByRole("button", { name: /Close finding/i })).toBeNull();
    expect(screen.getByRole("button", { name: /Export PDF/i })).toBeTruthy();
  });

  it("entries load failure is reported", async () => {
    seedAuth(["Audit.View"], "Reviewer", 20);
    vi.mocked(auditTrailReviewService.get).mockResolvedValue(makeReview());
    vi.mocked(auditTrailReviewService.getEntries).mockRejectedValue(new Error("Network failure"));

    renderPage();

    expect(await screen.findByText("Q4 Sample Audit Review")).toBeTruthy();
    expect(await screen.findByRole("alert")).toBeTruthy();
    expect(screen.getByText(/Could not load audit trail entries/i)).toBeTruthy();
  });

  describe("raising a deviation from a finding", () => {
    const raised = { id: 91, record: { number: "DEV-2026-0091" } } as DeviationDto;

    function serveFinding(finding: AuditTrailReviewFindingDto) {
      vi.mocked(auditTrailReviewService.get).mockResolvedValue(makeReview({ findings: [finding] }));
      vi.mocked(auditTrailReviewService.getEntries).mockResolvedValue({
        items: [makeEntry(101)],
        page: 1,
        pageSize: 25,
        totalCount: 1,
        totalPages: 1
      });
    }

    it("sends the finding and its version to the server, then opens the new deviation", async () => {
      const user = userEvent.setup();
      seedAuth(["Audit.View", "Quality.View", "Quality.Approve"], "Qa", 10);
      serveFinding(makeFinding({ id: 301, version: 0 }));
      vi.mocked(qualityEntryService.raiseFromAuditFinding).mockResolvedValue(raised);
      renderPage();

      await user.click(await screen.findByRole("button", { name: /Raise deviation from finding 301/i }));
      const title = screen.getByLabelText(/Title/i) as HTMLInputElement;
      expect(title.value).toBe("Deviation for audit finding 301");
      await user.clear(title);
      await user.type(title, "Correction without comment");
      await user.type(screen.getByLabelText(/Immediate action/i), "Author asked to add the comment");
      await user.click(screen.getByRole("button", { name: /^Raise deviation$/i }));

      await waitFor(() =>
        expect(qualityEntryService.raiseFromAuditFinding).toHaveBeenCalledWith(
          301,
          expect.objectContaining({
            title: "Correction without comment",
            immediateAction: "Author asked to add the comment"
          }),
          0
        )
      );
      expect(await screen.findByText("Deviation page opened")).toBeTruthy();
    });

    it("stays on the review and shows the server's refusal", async () => {
      const user = userEvent.setup();
      seedAuth(["Audit.View", "Quality.View", "Quality.Approve"], "Qa", 10);
      serveFinding(makeFinding({ id: 301, version: 1, status: "Closed" }));
      vi.mocked(qualityEntryService.raiseFromAuditFinding).mockRejectedValue({
        response: { data: { message: "Deviation DEV-2026-0090 already links this source." } }
      });
      renderPage();

      await user.click(await screen.findByRole("button", { name: /Raise deviation from finding 301/i }));
      await user.type(screen.getByLabelText(/Title/i), "Again");
      await user.type(screen.getByLabelText(/Immediate action/i), "None");
      await user.click(screen.getByRole("button", { name: /^Raise deviation$/i }));

      expect(await screen.findByText("Deviation DEV-2026-0090 already links this source.")).toBeTruthy();
      expect(qualityEntryService.raiseFromAuditFinding).toHaveBeenCalledWith(301, expect.anything(), 1);
      expect(screen.queryByText("Deviation page opened")).toBeNull();
    });

    it("is not offered without Quality.Approve", async () => {
      seedAuth(["Audit.View", "Audit.Review", "Quality.View", "Quality.Raise"], "Qa", 10);
      serveFinding(makeFinding({ id: 301 }));
      renderPage();

      expect(await screen.findByText("Missing comment on correction")).toBeTruthy();
      expect(screen.queryByRole("button", { name: /Raise deviation from finding/i })).toBeNull();
    });
  });
});
