import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../contexts/AuthContext";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { AuditTrailReviewsPage } from "./AuditTrailReviewsPage";
import { auditTrailReviewService } from "../../services/auditTrailReviewService";

vi.mock("../../services/auditTrailReviewService", () => ({
  auditTrailReviewService: { list: vi.fn() }
}));

describe("AuditTrailReviewsPage in Arabic", () => {
  beforeEach(() => {
    vi.mocked(auditTrailReviewService.list).mockResolvedValue([]);
  });
  afterEach(async () => {
    cleanup();
    await resetLanguage();
  });

  it("renders the Arabic heading and empty state", async () => {
    await renderInArabic(
      <AuthProvider><MemoryRouter><AuditTrailReviewsPage /></MemoryRouter></AuthProvider>,
      ["common", "auditReview"]
    );
    expect(await screen.findByRole("heading", { name: "مراجعات سجل التدقيق" })).toBeTruthy();
    expect(await screen.findByText("لا توجد مراجعات لسجل التدقيق بعد")).toBeTruthy();
  });
});
