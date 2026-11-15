import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { resetUiStateCache } from "../../hooks/useUiState";
import { FavouriteStar } from "./FavouriteStar";
import { MenuGroupsProvider } from "./MenuGroupsContext";
import { useFavourites } from "./useFavourites";
import {
  TEST_MENU,
  fakeUiStateService,
  resetFakeUiState,
  seedFavourites,
  storedFavourites
} from "./favouritesTestUtils";
import { toast } from "sonner";

vi.mock("../../services/uiStateService", async () => {
  const { fakeUiStateService: service } = await import("./favouritesTestUtils");
  return { uiStateService: service };
});
vi.mock("sonner", () => ({ toast: { error: vi.fn(), success: vi.fn(), info: vi.fn() } }));

function Probe() {
  const { items, loaded } = useFavourites();
  return <p data-testid="favourites">{loaded ? items.map((i) => i.label).join(",") : "loading"}</p>;
}

function renderStar(path: string, name: string) {
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <MenuGroupsProvider groups={TEST_MENU}>
          <FavouriteStar favourite={{ t: "p", path }} name={name} />
          <Probe />
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
  vi.mocked(toast.error).mockClear();
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  resetUiStateCache();
});

describe("useFavourites", () => {
  it("sixteenth is refused with \"You can have up to 15 favourites\" and no request is sent", async () => {
    const user = userEvent.setup();
    seedFavourites(Array.from({ length: 15 }, (_, i) => ({ t: "p", path: `/page-${i}` })));
    renderStar("/dashboard", "Dashboard");

    const star = await screen.findByRole("button", { name: "Add Dashboard to favourites" });
    await waitFor(() => expect((star as HTMLButtonElement).disabled).toBe(false));
    await user.click(star);

    expect(screen.getByRole("status").textContent).toBe("You can have up to 15 favourites");
    expect(toast.error).toHaveBeenCalledWith("You can have up to 15 favourites");
    expect(fakeUiStateService.set).not.toHaveBeenCalled();
    expect((storedFavourites() as unknown[]).length).toBe(15);
  });

  it("store failure falls back to localStorage and still works", async () => {
    const user = userEvent.setup();
    fakeUiStateService.getAll.mockRejectedValue(new Error("network down"));
    fakeUiStateService.set.mockRejectedValue(new Error("network down"));
    renderStar("/dashboard", "Dashboard");

    const star = await screen.findByRole("button", { name: "Add Dashboard to favourites" });
    await waitFor(() => expect((star as HTMLButtonElement).disabled).toBe(false));
    await user.click(star);

    await screen.findByRole("button", { name: "Remove Dashboard from favourites" });
    expect(localStorage.getItem("microlims.uiState.7.nav.favourites")).toBe('[{"t":"p","path":"/dashboard"}]');
    expect(screen.getByTestId("favourites").textContent).toBe("Dashboard");
    expect(toast.error).not.toHaveBeenCalled();
  });

  it("a favourite saved in this browser is read back when the store is down", async () => {
    fakeUiStateService.getAll.mockRejectedValue(new Error("network down"));
    localStorage.setItem("microlims.uiState.7.nav.favourites", '[{"t":"p","path":"/reports"}]');
    renderStar("/dashboard", "Dashboard");

    await waitFor(() => expect(screen.getByTestId("favourites").textContent).toBe("Reports"));
  });
});
