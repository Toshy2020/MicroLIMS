import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { resetUiStateCache } from "../hooks/useUiState";
import { MenuGroupsProvider } from "../modules/navigation/MenuGroupsContext";
import {
  TEST_MENU,
  fakeUiStateService,
  resetFakeUiState,
  storedFavourites
} from "../modules/navigation/favouritesTestUtils";
import { PageHeader } from "./PageHeader";

vi.mock("../services/uiStateService", async () => {
  const { fakeUiStateService: service } = await import("../modules/navigation/favouritesTestUtils");
  return { uiStateService: service };
});

function renderHeader(path: string, title = "Dashboard") {
  return render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={[path]}>
        <MenuGroupsProvider groups={TEST_MENU}>
          <PageHeader title={title} />
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

describe("PageHeader favourite star", () => {
  it("star on a menu page toggles and announces \"Added to favourites\"", async () => {
    const user = userEvent.setup();
    renderHeader("/dashboard");

    const add = await screen.findByRole("button", { name: "Add Dashboard to favourites" });
    await waitFor(() => expect((add as HTMLButtonElement).disabled).toBe(false));
    await user.click(add);

    await waitFor(() => expect(screen.getByRole("status").textContent).toBe("Added to favourites"));
    expect(storedFavourites()).toEqual([{ t: "p", path: "/dashboard" }]);

    await user.click(screen.getByRole("button", { name: "Remove Dashboard from favourites" }));
    await waitFor(() => expect(screen.getByRole("status").textContent).toBe("Removed from favourites"));
    expect(storedFavourites()).toEqual([]);
  });

  it("no star on a page that is not a menu path", () => {
    renderHeader("/samples/55", "Sample 55");
    expect(screen.queryByRole("button", { name: /favourites/i })).toBeNull();
    cleanup();
    // A page below a menu page (a record) is not itself a menu path either.
    renderHeader("/reports/123", "Report 123");
    expect(screen.queryByRole("button", { name: /favourites/i })).toBeNull();
    expect(fakeUiStateService.getAll).not.toHaveBeenCalled();
  });

  it("renders the title with no star and no router or menu around it", () => {
    render(
      <PinnedLightTheme>
        <PageHeader title="Plain" />
      </PinnedLightTheme>
    );
    expect(screen.getByRole("heading", { level: 1, name: "Plain" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /favourites/i })).toBeNull();
  });
});
