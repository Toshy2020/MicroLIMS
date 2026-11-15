import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { resetUiStateCache } from "../../hooks/useUiState";
import { MenuGroupsProvider } from "../../modules/navigation/MenuGroupsContext";
import { TEST_MENU, resetFakeUiState, seedFavourites } from "../../modules/navigation/favouritesTestUtils";
import { GlobalSearchDialog } from "./GlobalSearchDialog";

vi.mock("../../services/uiStateService", async () => {
  const { fakeUiStateService: service } = await import("../../modules/navigation/favouritesTestUtils");
  return { uiStateService: service };
});
vi.mock("../../api/globalSearchApi", () => ({ globalSearchApi: { search: vi.fn() } }));
vi.mock("../../services/labelService", () => ({
  labelService: { getStatus: vi.fn(), print: vi.fn(), getFormats: vi.fn(), updateFormat: vi.fn(), resolveScan: vi.fn() }
}));

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location-probe">{location.pathname}</div>;
}

beforeEach(() => {
  localStorage.clear();
  localStorage.setItem("microlims_user_id", "7");
  localStorage.setItem("microlims_recent_searches", JSON.stringify(["S-2026-001"]));
  resetUiStateCache();
  resetFakeUiState();
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  resetUiStateCache();
});

describe("GlobalSearchDialog favourites", () => {
  it("Ctrl+K with an empty query lists favourites first", async () => {
    const user = userEvent.setup();
    seedFavourites([
      { t: "p", path: "/reports" },
      { t: "p", path: "/dashboard" }
    ]);
    render(
      <PinnedLightTheme>
        <MemoryRouter initialEntries={["/"]}>
          <MenuGroupsProvider groups={TEST_MENU}>
            <GlobalSearchDialog open onClose={() => {}} />
            <LocationProbe />
          </MenuGroupsProvider>
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const region = await screen.findByRole("region", { name: "Favourites" });
    expect(within(region).getAllByRole("button").map((b) => b.textContent)).toEqual(["Reports", "Dashboard"]);

    // Favourites come before the remembered search words.
    const recent = screen.queryByText(/recent searches/i);
    if (recent) expect(region.compareDocumentPosition(recent) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    await user.click(within(region).getByRole("button", { name: "Dashboard" }));
    expect(screen.getByTestId("location-probe").textContent).toBe("/dashboard");
  });

  it("typing a query replaces the favourites with search results", async () => {
    const user = userEvent.setup();
    seedFavourites([{ t: "p", path: "/reports" }]);
    render(
      <PinnedLightTheme>
        <MemoryRouter initialEntries={["/"]}>
          <MenuGroupsProvider groups={TEST_MENU}>
            <GlobalSearchDialog open onClose={() => {}} />
          </MenuGroupsProvider>
        </MemoryRouter>
      </PinnedLightTheme>
    );
    await screen.findByRole("region", { name: "Favourites" });
    await user.type(screen.getByRole("combobox"), "ab");
    expect(screen.queryByRole("region", { name: "Favourites" })).toBeNull();
  });
});
