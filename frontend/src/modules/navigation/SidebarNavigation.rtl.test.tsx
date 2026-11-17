import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { resetUiStateCache } from "../../hooks/useUiState";
import { Sidebar } from "../../components/Sidebar";
import { myWorkService } from "../myWork/services/myWorkService";
import { TEST_MENU, resetFakeUiState, seedFavourites } from "./favouritesTestUtils";
import { recent } from "./recentTestUtils";
import { recentRecordsService } from "./services/recentRecordsService";

vi.mock("../../services/uiStateService", async () => {
  const { fakeUiStateService: service } = await import("./favouritesTestUtils");
  return { uiStateService: service };
});
vi.mock("../myWork/services/myWorkService", () => ({
  myWorkService: { get: vi.fn(), summary: vi.fn(), take: vi.fn(), release: vi.fn() }
}));
vi.mock("./services/recentRecordsService", () => ({
  recentRecordsService: { list: vi.fn(), touch: vi.fn(), clear: vi.fn() }
}));

beforeEach(() => {
  localStorage.clear();
  localStorage.setItem("microlims_user_id", "7");
  resetUiStateCache();
  resetFakeUiState();
  vi.mocked(myWorkService.summary).mockResolvedValue({ total: 0, overdue: 0, now: 0, complete: true });
  vi.mocked(recentRecordsService.list).mockResolvedValue([recent("sample", 1, "FP1126003", "pH")]);
});

afterEach(async () => {
  cleanup();
  localStorage.clear();
  resetUiStateCache();
  await resetLanguage();
});

describe("Sidebar favourites and recent records in Arabic", () => {
  it("shows the Arabic headings, links and favourite page names", async () => {
    seedFavourites([{ t: "p", path: "/reports" }]);
    await renderInArabic(
      <MemoryRouter initialEntries={["/my-work"]}>
        <Sidebar mobileOpen={false} onMobileClose={() => {}} collapsed={false} onToggleCollapse={() => {}} groups={TEST_MENU} activeTrail={null} />
      </MemoryRouter>,
      ["common", "navigation"]
    );

    const favourites = await screen.findByRole("list", { name: "المفضلة" });
    // The same Arabic name as the menu entry, not the English label.
    expect(within(favourites).getByRole("link", { name: "التقارير" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "إدارة" })).toBeTruthy();

    expect(await screen.findByRole("button", { name: "الأخيرة" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "مسح" })).toBeTruthy();
    expect(screen.getByRole("region", { name: "المفتوحة مؤخرًا" })).toBeTruthy();
    expect(screen.queryByText("Favourites")).toBeNull();
    expect(screen.queryByText("Manage")).toBeNull();
  });
});
