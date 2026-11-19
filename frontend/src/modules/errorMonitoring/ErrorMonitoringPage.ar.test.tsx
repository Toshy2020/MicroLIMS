import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { ErrorMonitoringPage } from "./ErrorMonitoringPage";
import { ErrorMonitoringService } from "./services/ErrorMonitoringService";

vi.mock("./services/ErrorMonitoringService", () => ({
  ErrorMonitoringService: {
    search: vi.fn(),
    exportCsv: vi.fn(),
    get: vi.fn(),
    resolve: vi.fn(),
    reopen: vi.fn(),
    overrideSeverity: vi.fn()
  }
}));

describe("ErrorMonitoringPage Arabic", () => {
  afterEach(async () => {
    cleanup();
    await resetLanguage();
  });

  it("shows the title, filter labels and empty table message in Arabic", async () => {
    vi.mocked(ErrorMonitoringService.search).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 0,
      pageSize: 25
    });

    await renderInArabic(<ErrorMonitoringPage />, ["errorMonitoring"]);

    expect(await screen.findByText("لا توجد حوادث تطابق عوامل التصفية هذه.")).toBeTruthy();
    expect(screen.getByText("مراقبة الأخطاء")).toBeTruthy();
    expect(screen.getByRole("button", { name: "تصدير CSV" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "إعادة ضبط" })).toBeTruthy();
  });
});
