import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { resetUiStateCache } from "../../hooks/useUiState";
import { Sidebar } from "../../components/Sidebar";
import { myWorkService } from "../myWork/services/myWorkService";
import { TEST_MENU, resetFakeUiState, uiStateStore } from "./favouritesTestUtils";
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

const SEVEN = [
  recent("sample", 1, "S-2026-001", "Paracetamol 500 mg"),
  recent("document", 2, "SOP-010", "Cleaning"),
  recent("equipment", 3, "EQ-003", "Balance"),
  recent("solution", 4, "SP-004", "0.1 M HCl"),
  recent("item", 5, "ITM-005", "Ibuprofen"),
  recent("media", 6, "ML-006", "TSA"),
  recent("material", 7, "MT-007", "Peptone")
];

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location-probe">{location.pathname + location.search}</div>;
}

function renderSidebar() {
  return render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={["/my-work"]}>
        <Sidebar
          mobileOpen={false}
          onMobileClose={() => {}}
          collapsed={false}
          onToggleCollapse={() => {}}
          groups={TEST_MENU}
          activeTrail={null}
        />
        <LocationProbe />
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
  vi.mocked(recentRecordsService.list).mockResolvedValue(SEVEN);
  vi.mocked(recentRecordsService.clear).mockResolvedValue(undefined);
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  resetUiStateCache();
  vi.clearAllMocks();
});

describe("Sidebar recently opened", () => {
  it("keeps the identifier left-to-right and apart from the title", async () => {
    vi.mocked(recentRecordsService.list).mockResolvedValue([recent("sample", 1, "FP1126003", "pH")]);
    renderSidebar();
    const list = await screen.findByRole("list", { name: "Recent records" });
    const link = within(list).getByRole("link");
    const identifier = within(link).getByText("FP1126003");
    expect(identifier.tagName).toBe("BDI");
    expect(identifier.getAttribute("dir")).toBe("ltr");
    // A visible separator, so the two never read as one word.
    expect(link.textContent).toBe("FP1126003·pH");
  });

  it("sidebar section shows 5 rows, newest first", async () => {
    renderSidebar();
    const list = await screen.findByRole("list", { name: "Recent records" });
    const links = within(list).getAllByRole("link");
    expect(links).toHaveLength(5);
    expect(links.map((l) => l.getAttribute("aria-label"))).toEqual([
      "S-2026-001 Paracetamol 500 mg",
      "SOP-010 Cleaning",
      "EQ-003 Balance",
      "SP-004 0.1 M HCl",
      "ITM-005 Ibuprofen"
    ]);
  });

  it("is collapsible and the state is remembered", async () => {
    const user = userEvent.setup();
    renderSidebar();
    const toggle = await screen.findByRole("button", { name: "Recent" });
    expect(toggle.getAttribute("aria-expanded")).toBe("true");

    await user.click(toggle);
    await waitFor(() => expect(screen.queryByRole("list", { name: "Recent records" })).toBeNull());
    expect(screen.getByRole("button", { name: "Recent" }).getAttribute("aria-expanded")).toBe("false");
    expect(uiStateStore.get("nav.recentsOpen")).toBe("0");

    // A fresh mount (page reload) reads the remembered state.
    cleanup();
    resetUiStateCache();
    renderSidebar();
    const again = await screen.findByRole("button", { name: "Recent" });
    expect(again.getAttribute("aria-expanded")).toBe("false");
    expect(screen.queryByRole("list", { name: "Recent records" })).toBeNull();
  });

  it("a row opens the route of the record", async () => {
    const user = userEvent.setup();
    renderSidebar();
    const list = await screen.findByRole("list", { name: "Recent records" });
    await user.click(within(list).getByRole("link", { name: "SOP-010 Cleaning" }));
    expect(screen.getByTestId("location-probe").textContent).toBe("/document-control/documents/2");
    await user.click(within(list).getByRole("link", { name: "S-2026-001 Paracetamol 500 mg" }));
    expect(screen.getByTestId("location-probe").textContent).toBe("/receiving-testing?sampleId=1");
  });

  it("a row follows the route the server sent, and a route outside the application is left out", async () => {
    const user = userEvent.setup();
    vi.mocked(recentRecordsService.list).mockResolvedValue([
      recent("testOrder", 11, "S-2026-011", "TAMC", 1, "/receiving-testing?sampleId=31&testOrderId=11"),
      recent("document", 12, "SOP-012", "Gowning", 2, "https://elsewhere.example/documents/12"),
      recent("item", 13, "ITM-013", "Aspirin", 3, "//elsewhere.example/items/13")
    ]);
    renderSidebar();
    const list = await screen.findByRole("list", { name: "Recent records" });
    expect(within(list).getAllByRole("link")).toHaveLength(1);
    await user.click(within(list).getByRole("link", { name: "S-2026-011 TAMC" }));
    expect(screen.getByTestId("location-probe").textContent).toBe("/receiving-testing?sampleId=31&testOrderId=11");
  });

  it("Clear asks for confirmation, Cancel keeps the list", async () => {
    const user = userEvent.setup();
    renderSidebar();
    await user.click(await screen.findByRole("button", { name: "Clear" }));
    const dialog = await screen.findByRole("alertdialog");
    expect(recentRecordsService.clear).not.toHaveBeenCalled();
    expect(document.activeElement).toBe(within(dialog).getByRole("button", { name: "Cancel" }));
    await user.click(within(dialog).getByRole("button", { name: "Cancel" }));
    await waitFor(() => expect(screen.queryByRole("alertdialog")).toBeNull());
    expect(recentRecordsService.clear).not.toHaveBeenCalled();
    expect(screen.getByRole("list", { name: "Recent records" })).toBeTruthy();
  });

  it("confirming Clear removes the section", async () => {
    const user = userEvent.setup();
    // The server list is empty once cleared, as the real endpoint would answer.
    vi.mocked(recentRecordsService.clear).mockImplementation(async () => {
      vi.mocked(recentRecordsService.list).mockResolvedValue([]);
    });
    renderSidebar();
    await user.click(await screen.findByRole("button", { name: "Clear" }));
    const dialog = await screen.findByRole("alertdialog");
    await user.click(within(dialog).getByRole("button", { name: "Clear" }));
    await waitFor(() => expect(recentRecordsService.clear).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.queryByRole("list", { name: "Recent records" })).toBeNull());
  });

  it("load failure hides the section", async () => {
    vi.mocked(recentRecordsService.list).mockRejectedValue(new Error("500"));
    renderSidebar();
    await waitFor(() => expect(recentRecordsService.list).toHaveBeenCalled());
    await screen.findByRole("link", { name: "My Work" });
    await waitFor(() => expect(screen.queryByRole("region", { name: "Recently opened" })).toBeNull());
    expect(screen.queryByRole("button", { name: "Recent" })).toBeNull();
  });
});
