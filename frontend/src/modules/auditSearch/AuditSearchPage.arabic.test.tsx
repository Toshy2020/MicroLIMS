import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../contexts/AuthContext";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { AuditSearchPage } from "./AuditSearchPage";
import { AuditSearchService } from "./services/AuditSearchService";

vi.mock("./services/AuditSearchService", () => ({ AuditSearchService: { searchPage: vi.fn() } }));

describe("AuditSearchPage in Arabic", () => {
  beforeEach(() => {
    vi.mocked(AuditSearchService.searchPage).mockResolvedValue({ items: [], nextCursor: null, hasMore: false });
  });
  afterEach(async () => {
    cleanup();
    await resetLanguage();
  });

  it("renders the Arabic title, search button and empty state", async () => {
    await renderInArabic(<AuthProvider><MemoryRouter><AuditSearchPage /></MemoryRouter></AuthProvider>, ["common", "auditSearch"]);
    expect(await screen.findByRole("heading", { name: "سجل التدقيق" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "البحث في سجل التدقيق" })).toBeTruthy();
    expect(await screen.findByText("لم يُعثر على أحداث تدقيق")).toBeTruthy();
  });
});
