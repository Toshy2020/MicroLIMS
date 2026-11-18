import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { AuthProvider } from "../../contexts/AuthContext";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { AccessReviewPage } from "./AccessReviewPage";
import { AccessReviewListItem, AccessReviewService } from "./services/AccessReviewService";

vi.mock("./services/AccessReviewService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./services/AccessReviewService")>();
  return { ...actual, AccessReviewService: { list: vi.fn(), get: vi.fn() } };
});

const summary = { totalUsers: 3, activeUsers: 2, inactiveUsers: 1, dormantUsers: 1, neverLoggedInUsers: 0, sodConflictUsers: 1 };
const listItem: AccessReviewListItem = {
  id: 7, generatedAt: "2026-10-28T09:00:00Z", generatedByUserId: 1, generatedByName: "Ada Admin", periodLabel: "2026 Q4", status: "Draft",
  reviewerUserId: null, reviewerName: null, signedAt: null, findingCount: 0, summary, canDecide: true
};

afterEach(async () => {
  cleanup();
  await resetLanguage();
});

describe("AccessReviewPage in Arabic", () => {
  it("renders the Arabic title, table and row action name", async () => {
    vi.mocked(AccessReviewService.list).mockResolvedValue([listItem]);
    await renderInArabic(<AuthProvider><AccessReviewPage /></AuthProvider>, ["common", "admin"]);
    expect(await screen.findByRole("heading", { name: "مراجعة الصلاحيات" })).toBeTruthy();
    expect(await screen.findByRole("table", { name: "مراجعات الصلاحيات" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "فتح 2026 Q4" })).toBeTruthy();
    expect(screen.getByText("1 خامل، 0 لم يسجل الدخول أبدًا، 1 فصل مهام")).toBeTruthy();
  });
});
