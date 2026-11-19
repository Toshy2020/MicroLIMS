import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { NotificationsPage } from "./NotificationsPage";
import { notificationService } from "./services/notificationService";

vi.mock("./services/notificationService", () => ({
  notificationService: {
    list: vi.fn(),
    markRead: vi.fn(),
    markAllRead: vi.fn()
  }
}));

describe("NotificationsPage Arabic", () => {
  afterEach(async () => {
    cleanup();
    await resetLanguage();
  });

  it("shows the title, state tabs and empty state in Arabic", async () => {
    vi.mocked(notificationService.list).mockResolvedValue({
      items: [],
      page: 1,
      pageNumber: 1,
      pageSize: 25,
      totalCount: 0,
      totalPages: 1
    });

    await renderInArabic(
      <MemoryRouter>
        <NotificationsPage />
      </MemoryRouter>,
      ["notifications"]
    );

    expect(await screen.findByText("لا توجد إشعارات")).toBeTruthy();
    expect(screen.getByText("الإشعارات")).toBeTruthy();
    expect(screen.getByRole("tab", { name: "غير مقروءة" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "تحديد الكل كمقروء" })).toBeTruthy();
  });
});
