import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { resetUiStateCache } from "../../hooks/useUiState";
import { MenuGroupsProvider } from "../../modules/navigation/MenuGroupsContext";
import { TEST_MENU, resetFakeUiState, seedFavourites } from "../../modules/navigation/favouritesTestUtils";
import { recent } from "../../modules/navigation/recentTestUtils";
import { recentRecordsService } from "../../modules/navigation/services/recentRecordsService";
import { GlobalSearchDialog } from "./GlobalSearchDialog";

vi.mock("../../services/uiStateService", async () => {
  const { fakeUiStateService: service } = await import("../../modules/navigation/favouritesTestUtils");
  return { uiStateService: service };
});
vi.mock("../../api/globalSearchApi", () => ({ globalSearchApi: { search: vi.fn() } }));
vi.mock("../../services/labelService", () => ({
  labelService: { getStatus: vi.fn(), print: vi.fn(), getFormats: vi.fn(), updateFormat: vi.fn(), resolveScan: vi.fn() }
}));
vi.mock("../../modules/navigation/services/recentRecordsService", () => ({
  recentRecordsService: { list: vi.fn(), touch: vi.fn(), clear: vi.fn() }
}));

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location-probe">{location.pathname + location.search}</div>;
}

function renderDialog() {
  return render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={["/"]}>
        <MenuGroupsProvider groups={TEST_MENU}>
          <GlobalSearchDialog open onClose={() => {}} />
          <LocationProbe />
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
  vi.mocked(recentRecordsService.list).mockResolvedValue([
    recent("sample", 1, "S-2026-001", "Paracetamol 500 mg"),
    recent("solution", 4, "SP-004", "0.1 M HCl")
  ]);
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  resetUiStateCache();
  vi.clearAllMocks();
});

describe("GlobalSearchDialog recent records", () => {
  it('Ctrl+K empty query shows "Recent" after favourites', async () => {
    seedFavourites([{ t: "p", path: "/reports" }]);
    renderDialog();

    const favourites = await screen.findByRole("region", { name: "Favourites" });
    const recents = await screen.findByRole("region", { name: "Recent records" });
    expect(favourites.compareDocumentPosition(recents) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(within(recents).getAllByRole("button").map((b) => b.textContent)).toEqual([
      "S-2026-001 Paracetamol 500 mg",
      "SP-004 0.1 M HCl"
    ]);
    expect(within(recents).getByText("RECENT")).toBeTruthy();
  });

  it("a recent row opens the route of the record", async () => {
    const user = userEvent.setup();
    renderDialog();
    const recents = await screen.findByRole("region", { name: "Recent records" });
    await user.click(within(recents).getByRole("button", { name: "SP-004 0.1 M HCl" }));
    expect(screen.getByTestId("location-probe").textContent).toBe("/preparation/4");
  });

  it("typing a query replaces the recent records with search results", async () => {
    const user = userEvent.setup();
    renderDialog();
    await screen.findByRole("region", { name: "Recent records" });
    await user.type(screen.getByRole("combobox"), "ab");
    expect(screen.queryByRole("region", { name: "Recent records" })).toBeNull();
  });

  it("load failure hides the block", async () => {
    vi.mocked(recentRecordsService.list).mockRejectedValue(new Error("500"));
    renderDialog();
    await waitFor(() => expect(recentRecordsService.list).toHaveBeenCalled());
    expect(screen.queryByRole("region", { name: "Recent records" })).toBeNull();
  });
});
