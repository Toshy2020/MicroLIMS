import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AccessReviewPage } from "./AccessReviewPage";
import { AccessReviewDetail, AccessReviewListItem, AccessReviewService, AccessReviewUserEntry } from "./services/AccessReviewService";

vi.mock("./services/AccessReviewService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./services/AccessReviewService")>();
  return {
    ...actual,
    AccessReviewService: { list: vi.fn(), get: vi.fn(), generate: vi.fn(), addFinding: vi.fn(), updateFinding: vi.fn(), sign: vi.fn(), exportCsv: vi.fn() }
  };
});
vi.mock("../../contexts/AuthContext", () => ({ useAuth: () => ({ fullName: "Quinn QA", username: "qa", role: "Reviewer" }) }));
vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

const summary = { totalUsers: 3, activeUsers: 2, inactiveUsers: 1, dormantUsers: 1, neverLoggedInUsers: 0, sodConflictUsers: 1 };

const user = (over: Partial<AccessReviewUserEntry>): AccessReviewUserEntry => ({
  userId: 1, username: "admin", fullName: "Ada Admin", isActive: true, deactivatedAt: null, roleId: 1, roleName: "System Administrator",
  roleType: "SystemAdministrator", permissions: [{ code: "Users.Manage", hidden: false }, { code: "Discussions.View", hidden: true }],
  sections: [], lastSuccessfulLogin: "2026-10-27T08:00:00Z", neverLoggedIn: false, dormant: false, daysSinceLastActivity: 1,
  statusHistory: [], sodConflicts: [], ...over
});

const users = [
  user({}),
  user({ userId: 2, username: "dormant", fullName: "Dora Dormant", roleName: "Analyst", roleType: "Analyst", dormant: true, daysSinceLastActivity: 200,
    permissions: [{ code: "Samples.Receive", hidden: false }], sections: [{ departmentId: 1, departmentName: "Microbiology", sectionId: 1, sectionName: "EM", physchemArea: null }] }),
  user({ userId: 3, username: "gone", fullName: "Gus Gone", isActive: false, deactivatedAt: "2026-07-30T00:00:00Z", roleName: "Analyst", roleType: "Analyst",
    statusHistory: [{ timestamp: "2026-07-30T00:00:00Z", action: "USER_DISABLED", byUserId: 1, reason: "Left", detail: null }] }),
  user({ userId: 4, username: "conflict", fullName: "Connie Conflict", sodConflicts: ["Users.Manage + Samples.Approve"] })
];

const detail = (over: Partial<AccessReviewDetail> = {}): AccessReviewDetail => ({
  id: 7, generatedAt: "2026-10-28T09:00:00Z", generatedByUserId: 1, generatedByName: "Ada Admin", periodLabel: "2026 Q4", status: "Draft",
  reviewerUserId: null, reviewerName: null, signedAt: null, electronicSignatureId: null, conclusion: null,
  snapshotSha256: "a".repeat(64), snapshotIntact: true,
  snapshot: { reportVersion: 1, generatedAt: "2026-10-28T09:00:00Z", periodLabel: "2026 Q4", dormantDays: 90, statusHistoryMonths: 12, summary, users },
  findings: [], ...over
});

const listItem: AccessReviewListItem = {
  id: 7, generatedAt: "2026-10-28T09:00:00Z", generatedByUserId: 1, generatedByName: "Ada Admin", periodLabel: "2026 Q4", status: "Draft",
  reviewerUserId: null, reviewerName: null, signedAt: null, findingCount: 0, summary
};

async function openReview(d: AccessReviewDetail = detail()) {
  vi.mocked(AccessReviewService.list).mockResolvedValue([{ ...listItem, status: d.status }]);
  vi.mocked(AccessReviewService.get).mockResolvedValue(d);
  render(<PinnedLightTheme><AccessReviewPage /></PinnedLightTheme>);
  fireEvent.click(await screen.findByRole("button", { name: "Open 2026 Q4" }));
  await screen.findByRole("table", { name: "Accounts in the report" });
}

describe("AccessReviewPage", () => {
  it("lists the reviews with status and flag counts", async () => {
    vi.mocked(AccessReviewService.list).mockResolvedValue([listItem]);
    render(<PinnedLightTheme><AccessReviewPage /></PinnedLightTheme>);
    expect(await screen.findByText("2026 Q4")).toBeTruthy();
    expect(screen.getByText("Draft")).toBeTruthy();
    expect(screen.getByText(/1 dormant, 0 never signed in, 1 SoD/)).toBeTruthy();
  });

  it("requires a reason to generate, then opens the new draft", async () => {
    vi.mocked(AccessReviewService.list).mockResolvedValue([]);
    vi.mocked(AccessReviewService.generate).mockResolvedValue(detail());
    render(<PinnedLightTheme><AccessReviewPage /></PinnedLightTheme>);
    fireEvent.click(await screen.findByRole("button", { name: "Generate review" }));
    const generate = screen.getByRole("button", { name: "Generate" }) as HTMLButtonElement;
    expect(generate.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText(/Period label/), { target: { value: "2026 Q4" } });
    fireEvent.change(screen.getByLabelText(/^Reason/), { target: { value: "Quarterly review" } });
    fireEvent.click(screen.getByRole("button", { name: "Generate" }));
    await waitFor(() => expect(AccessReviewService.generate).toHaveBeenCalledWith("2026 Q4", "Quarterly review"));
    expect(await screen.findByRole("table", { name: "Accounts in the report" })).toBeTruthy();
  });

  it("shows each account with role, permissions (hidden marked), sections and flags", async () => {
    await openReview();
    const table = screen.getByRole("table", { name: "Accounts in the report" });
    expect(within(table).getByText("Dora Dormant")).toBeTruthy();
    expect(within(table).getAllByText(/Users.Manage, Discussions.View \(hidden\)/).length).toBe(3);
    expect(within(table).getByText("Microbiology / EM")).toBeTruthy();
    expect(within(table).getByText("Dormant")).toBeTruthy();
    expect(within(table).getByText("SoD: Users.Manage + Samples.Approve")).toBeTruthy();
    expect(within(table).getByText(/USER_DISABLED/)).toBeTruthy();
  });

  it("filters by text, status and flagged-only", async () => {
    await openReview();
    const table = () => screen.getByRole("table", { name: "Accounts in the report" });

    fireEvent.click(screen.getByLabelText("Flagged only"));
    expect(within(table()).queryByText("Ada Admin")).toBeNull();
    expect(within(table()).getByText("Dora Dormant")).toBeTruthy();
    expect(within(table()).getByText("Connie Conflict")).toBeTruthy();
    fireEvent.click(screen.getByLabelText("Flagged only"));

    fireEvent.change(screen.getByLabelText("Search accounts"), { target: { value: "gus" } });
    expect(within(table()).getByText("Gus Gone")).toBeTruthy();
    expect(within(table()).queryByText("Ada Admin")).toBeNull();
  });

  it("records a finding while the review is a draft", async () => {
    await openReview();
    vi.mocked(AccessReviewService.addFinding).mockResolvedValue({
      id: 1, reviewId: 7, userId: 2, username: "dormant", kind: "Dormant", detail: "Not used", action: "ChangeRequested", note: "Disable",
      recordedByUserId: 5, recordedAt: "2026-10-28T10:00:00Z"
    });
    fireEvent.click(screen.getByRole("button", { name: "Add finding for dormant" }));
    const record = screen.getByRole("button", { name: "Record finding" }) as HTMLButtonElement;
    expect(record.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText(/^Detail/), { target: { value: "Not used" } });
    fireEvent.mouseDown(screen.getByRole("combobox", { name: /Action/ }));
    fireEvent.click(await screen.findByRole("option", { name: "Change requested" }));
    expect((screen.getByRole("button", { name: "Record finding" }) as HTMLButtonElement).disabled).toBe(true); // note required
    fireEvent.change(screen.getByLabelText(/^Note/), { target: { value: "Disable" } });
    fireEvent.click(screen.getByRole("button", { name: "Record finding" }));
    await waitFor(() => expect(AccessReviewService.addFinding).toHaveBeenCalledWith(7, {
      userId: 2, kind: "Dormant", detail: "Not used", action: "ChangeRequested", note: "Disable"
    }));
  });

  it("signs with the conclusion and password", async () => {
    await openReview();
    vi.mocked(AccessReviewService.sign).mockResolvedValue(detail({ status: "Signed", reviewerName: "Quinn QA", signedAt: "2026-10-28T11:00:00Z", conclusion: "Appropriate" }));
    fireEvent.click(screen.getByRole("button", { name: "Sign review" }));
    fireEvent.change(await screen.findByLabelText("Conclusion"), { target: { value: "Appropriate" } });
    fireEvent.change(screen.getByLabelText(/Password/), { target: { value: "Secret#1" } });
    fireEvent.click(screen.getByRole("button", { name: /^Sign$/ }));
    await waitFor(() => expect(AccessReviewService.sign).toHaveBeenCalledWith(7, "Secret#1", "Appropriate"));
    expect(await screen.findByText(/Signed by Quinn QA/)).toBeTruthy();
    // A signed review offers neither signing nor findings.
    expect(screen.queryByRole("button", { name: "Sign review" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Add finding for dormant" })).toBeNull();
  });

  it("offers no signing or findings on a signed review, and exports it", async () => {
    await openReview(detail({ status: "Signed", reviewerName: "Quinn QA", signedAt: "2026-10-28T11:00:00Z", conclusion: "Appropriate" }));
    expect(screen.queryByRole("button", { name: "Sign review" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Add finding for dormant" })).toBeNull();
    vi.mocked(AccessReviewService.exportCsv).mockResolvedValue();
    fireEvent.click(screen.getByRole("button", { name: "Export CSV" }));
    expect(AccessReviewService.exportCsv).toHaveBeenCalledWith(7);
  });

  it("warns when the stored report no longer matches its checksum", async () => {
    await openReview(detail({ snapshotIntact: false }));
    expect(screen.getByText(/no longer matches its checksum/)).toBeTruthy();
  });
});
