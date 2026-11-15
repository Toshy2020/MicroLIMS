import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { resetUiStateCache } from "../../hooks/useUiState";
import { Sidebar } from "../../components/Sidebar";
import { myWorkService } from "../myWork/services/myWorkService";
import { TEST_MENU, resetFakeUiState, seedFavourites, storedFavourites } from "./favouritesTestUtils";

vi.mock("../../services/uiStateService", async () => {
  const { fakeUiStateService: service } = await import("./favouritesTestUtils");
  return { uiStateService: service };
});
vi.mock("../myWork/services/myWorkService", () => ({
  myWorkService: { get: vi.fn(), summary: vi.fn(), take: vi.fn(), release: vi.fn() }
}));

function renderSidebar(collapsed = false) {
  return render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={["/my-work"]}>
        <Sidebar
          mobileOpen={false}
          onMobileClose={() => {}}
          collapsed={collapsed}
          onToggleCollapse={() => {}}
          groups={TEST_MENU}
          activeTrail={null}
        />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

beforeEach(() => {
  localStorage.clear();
  localStorage.setItem("microlims_user_id", "7");
  resetUiStateCache();
  resetFakeUiState();
  vi.mocked(myWorkService.summary).mockResolvedValue({ total: 0, overdue: 0, now: 0, complete: true });
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  resetUiStateCache();
});

describe("Sidebar favourites", () => {
  it("sidebar shows favourites under My Work in stored order", async () => {
    seedFavourites([
      { t: "p", path: "/reports" },
      { t: "p", path: "/dashboard" }
    ]);
    renderSidebar();

    const list = await screen.findByRole("list", { name: "Favourites" });
    const links = within(list).getAllByRole("link");
    expect(links.map((l) => l.textContent)).toEqual(["Reports", "Dashboard"]);
    expect(links[0].getAttribute("href")).toBe("/reports");

    // The section sits directly after the My Work entry, before the next menu entry.
    const myWork = screen.getByRole("link", { name: "My Work" });
    const dashboardEntry = screen.getAllByRole("link", { name: "Dashboard" }).find((l) => !list.contains(l)) as HTMLElement;
    expect(myWork.compareDocumentPosition(list) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(list.compareDocumentPosition(dashboardEntry) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("collapsed sidebar exposes favourites by accessible name", async () => {
    seedFavourites([{ t: "p", path: "/reports" }]);
    renderSidebar(true);

    const list = await screen.findByRole("list", { name: "Favourites" });
    const link = within(list).getByRole("link", { name: "Reports" });
    expect(link.getAttribute("href")).toBe("/reports");
    expect(link.textContent).toBe("");
  });

  it("a favourite that is no longer in my menu is not offered in the sidebar", async () => {
    seedFavourites([
      { t: "p", path: "/gone" },
      { t: "p", path: "/reports" }
    ]);
    renderSidebar();

    const list = await screen.findByRole("list", { name: "Favourites" });
    expect(within(list).getAllByRole("link").map((l) => l.textContent)).toEqual(["Reports"]);
  });

  it("Manage opens the dialog and Remove from there updates the sidebar", async () => {
    const user = userEvent.setup();
    seedFavourites([{ t: "p", path: "/reports" }]);
    renderSidebar();

    await user.click(await screen.findByRole("button", { name: "Manage" }));
    const dialog = await screen.findByRole("dialog", { name: "Manage favourites" });
    await user.click(within(dialog).getByRole("button", { name: "Remove Reports" }));

    await waitFor(() => expect(storedFavourites()).toEqual([]));
    await waitFor(() => expect(screen.queryByRole("list", { name: "Favourites" })).toBeNull());
  });
});
