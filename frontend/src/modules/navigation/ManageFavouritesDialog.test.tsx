import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { resetUiStateCache } from "../../hooks/useUiState";
import { ManageFavouritesDialog } from "./ManageFavouritesDialog";
import { MenuGroupsProvider } from "./MenuGroupsContext";
import { TEST_MENU, fakeUiStateService, resetFakeUiState, seedFavourites, storedFavourites } from "./favouritesTestUtils";

vi.mock("../../services/uiStateService", async () => {
  const { fakeUiStateService: service } = await import("./favouritesTestUtils");
  return { uiStateService: service };
});

function renderDialog() {
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <MenuGroupsProvider groups={TEST_MENU}>
          <ManageFavouritesDialog open onClose={() => {}} />
        </MenuGroupsProvider>
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

beforeEach(() => {
  localStorage.clear();
  localStorage.setItem("microlims_user_id", "7");
  resetUiStateCache();
  resetFakeUiState();
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  resetUiStateCache();
});

describe("ManageFavouritesDialog", () => {
  it("Move up/down by keyboard persists the new order", async () => {
    const user = userEvent.setup();
    seedFavourites([
      { t: "p", path: "/my-work" },
      { t: "p", path: "/dashboard" },
      { t: "p", path: "/reports" }
    ]);
    renderDialog();

    const moveReportsUp = await screen.findByRole("button", { name: "Move Reports up" });
    moveReportsUp.focus();
    await user.keyboard("{Enter}");
    await waitFor(() =>
      expect(storedFavourites()).toEqual([
        { t: "p", path: "/my-work" },
        { t: "p", path: "/reports" },
        { t: "p", path: "/dashboard" }
      ])
    );

    const moveMyWorkDown = screen.getByRole("button", { name: "Move My Work down" });
    moveMyWorkDown.focus();
    await user.keyboard(" ");
    await waitFor(() =>
      expect(storedFavourites()).toEqual([
        { t: "p", path: "/reports" },
        { t: "p", path: "/my-work" },
        { t: "p", path: "/dashboard" }
      ])
    );

    const labels = within(screen.getByRole("list", { name: "Favourites" }))
      .getAllByRole("listitem")
      .map((li) => li.textContent);
    expect(labels[0]).toContain("Reports");
    expect(labels[2]).toContain("Dashboard");
  });

  it("moving the first item up does nothing and sends no request", async () => {
    const user = userEvent.setup();
    seedFavourites([
      { t: "p", path: "/my-work" },
      { t: "p", path: "/dashboard" }
    ]);
    renderDialog();

    await user.click(await screen.findByRole("button", { name: "Move My Work up" }));
    expect(fakeUiStateService.set).not.toHaveBeenCalled();
  });

  it("favourite page not in my menu: greyed, \"No longer available\", Remove works", async () => {
    const user = userEvent.setup();
    seedFavourites([
      { t: "p", path: "/admin/secret" },
      { t: "p", path: "/dashboard" }
    ]);
    renderDialog();

    const row = (await screen.findByText("/admin/secret")).closest("li") as HTMLElement;
    expect(within(row).getByText("No longer available")).toBeTruthy();

    await user.click(within(row).getByRole("button", { name: "Remove /admin/secret" }));
    await waitFor(() => expect(storedFavourites()).toEqual([{ t: "p", path: "/dashboard" }]));
    expect(screen.queryByText("/admin/secret")).toBeNull();
  });
});
