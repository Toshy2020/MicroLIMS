import { act, cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider, useLocation } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { BenchModeProvider } from "../../../theme/BenchModeContext";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ThemeModeProvider } from "../../../theme/ThemeModeContext";
import { TableToolbar } from "./TableToolbar";
import { useTableState } from "./useTableState";

function EquipmentHarness({ prefix }: { prefix?: string }) {
  const ts = useTableState("inventory.equipment", undefined, { prefix });
  const loc = useLocation();
  return (
    <div>
      <TableToolbar tableKey="inventory.equipment" tableState={ts} />
      <button type="button" onClick={() => ts.setFilter("status", "Active")}>
        Filter Active
      </button>
      <button type="button" onClick={() => ts.setFilter("status", "Retired")}>
        Filter Retired
      </button>
      <button type="button" onClick={() => ts.setPage(3)}>
        Go Page 3
      </button>
      <button type="button" onClick={() => ts.setDensity("compact")}>
        Set Compact
      </button>
      <div data-testid={`status-${prefix ?? "main"}`}>
        {String(ts.state.filters.status ?? "")}
      </div>
      <div data-testid={`search-${prefix ?? "main"}`}>{ts.state.search}</div>
      <div data-testid={`page-${prefix ?? "main"}`}>{ts.page}</div>
      <div data-testid={`density-${prefix ?? "main"}`}>
        {ts.state.density}
      </div>
      <div data-testid={`columns-${prefix ?? "main"}`}>
        {ts.state.columns.join(",")}
      </div>
      <div data-testid={`skipped-${prefix ?? "main"}`}>
        {ts.skipped.join(",")}
      </div>
      <div data-testid="location-search">{loc.search}</div>
    </div>
  );
}

function renderWithRouter(initialEntry = "/") {
  const router = createMemoryRouter(
    [{ path: "*", element: <EquipmentHarness /> }],
    { initialEntries: [initialEntry] }
  );
  const view = render(
    <PinnedLightTheme>
      <RouterProvider router={router} />
    </PinnedLightTheme>
  );
  return { router, ...view };
}

beforeEach(() => {
  localStorage.clear();
});

afterEach(() => {
  cleanup();
  localStorage.clear();
});

describe("useTableState", () => {
  it("Back restores the previous filter", async () => {
    const user = userEvent.setup();
    const { router } = renderWithRouter("/");

    expect(screen.getByTestId("status-main").textContent).toBe("");

    await user.click(screen.getByRole("button", { name: "Filter Active" }));
    expect(screen.getByTestId("status-main").textContent).toBe("Active");

    await user.click(screen.getByRole("button", { name: "Filter Retired" }));
    expect(screen.getByTestId("status-main").textContent).toBe("Retired");

    await act(async () => {
      await router.navigate(-1);
    });
    expect(screen.getByTestId("status-main").textContent).toBe("Active");

    await act(async () => {
      await router.navigate(-1);
    });
    expect(screen.getByTestId("status-main").textContent).toBe("");
  });

  it("search typing does not add history entries", async () => {
    const user = userEvent.setup();
    const { router } = renderWithRouter("/");

    // Push one history entry via filter so Back has a clear target.
    await user.click(screen.getByRole("button", { name: "Filter Active" }));
    expect(screen.getByTestId("status-main").textContent).toBe("Active");

    const searchInput = screen.getByRole("textbox", {
      name: "Search Equipment Inventory",
    });
    await user.type(searchInput, "HPLC");
    expect(screen.getByTestId("search-main").textContent).toBe("HPLC");

    // One Back step must return straight to "/", skipping all search keystrokes.
    await act(async () => {
      await router.navigate(-1);
    });
    expect(screen.getByTestId("status-main").textContent).toBe("");
    expect(screen.getByTestId("search-main").textContent).toBe("");
  });

  it("filter change resets page to 1", async () => {
    const user = userEvent.setup();
    renderWithRouter("/");

    await user.click(screen.getByRole("button", { name: "Go Page 3" }));
    expect(screen.getByTestId("page-main").textContent).toBe("3");

    await user.click(screen.getByRole("button", { name: "Filter Active" }));
    expect(screen.getByTestId("page-main").textContent).toBe("1");
  });

  it("unknown filter and unknown column are skipped and listed", () => {
    localStorage.setItem(
      "microlims.table.inventory.equipment",
      JSON.stringify({
        columns: ["code", "removedColumn", "status"],
        density: "comfortable",
      })
    );

    renderWithRouter("/?f.unknownFilter=1&f.status=Active&sort=noSuchCol:asc");

    expect(screen.getByTestId("status-main").textContent).toBe("Active");
    expect(screen.getByTestId("columns-main").textContent).toBe("code,status");
    const skipped = screen.getByTestId("skipped-main").textContent;
    expect(skipped).toContain("f.unknownFilter");
    expect(skipped).toContain("sort:noSuchCol:asc");
    expect(skipped).toContain("column:removedColumn");
  });

  it("two tables on one page do not share URL parameters", async () => {
    const user = userEvent.setup();

    function TwoTablesPage() {
      const t1 = useTableState("inventory.equipment");
      const t2 = useTableState("inventory.materials", undefined, {
        prefix: "mat",
      });
      return (
        <div>
          <button
            type="button"
            onClick={() => t1.setFilter("status", "Active")}
          >
            T1 Status Active
          </button>
          <button type="button" onClick={() => t1.setSearch("Balance")}>
            T1 Search Balance
          </button>
          <button
            type="button"
            onClick={() => t2.setFilter("status", "Quarantine")}
          >
            T2 Status Quarantine
          </button>
          <button type="button" onClick={() => t2.setSearch("Agar")}>
            T2 Search Agar
          </button>
          <div data-testid="t1-status">{String(t1.state.filters.status ?? "")}</div>
          <div data-testid="t1-search">{t1.state.search}</div>
          <div data-testid="t2-status">{String(t2.state.filters.status ?? "")}</div>
          <div data-testid="t2-search">{t2.state.search}</div>
        </div>
      );
    }

    const router = createMemoryRouter([{ path: "*", element: <TwoTablesPage /> }], {
      initialEntries: ["/"],
    });
    render(
      <PinnedLightTheme>
        <RouterProvider router={router} />
      </PinnedLightTheme>
    );

    await user.click(screen.getByRole("button", { name: "T1 Status Active" }));
    await user.click(screen.getByRole("button", { name: "T1 Search Balance" }));
    expect(screen.getByTestId("t1-status").textContent).toBe("Active");
    expect(screen.getByTestId("t1-search").textContent).toBe("Balance");
    expect(screen.getByTestId("t2-status").textContent).toBe("");
    expect(screen.getByTestId("t2-search").textContent).toBe("");

    await user.click(
      screen.getByRole("button", { name: "T2 Status Quarantine" })
    );
    await user.click(screen.getByRole("button", { name: "T2 Search Agar" }));
    expect(screen.getByTestId("t2-status").textContent).toBe("Quarantine");
    expect(screen.getByTestId("t2-search").textContent).toBe("Agar");
    // Table 1 state remains intact.
    expect(screen.getByTestId("t1-status").textContent).toBe("Active");
    expect(screen.getByTestId("t1-search").textContent).toBe("Balance");
  });

  it("bench mode ignores compact", async () => {
    const user = userEvent.setup();
    localStorage.setItem("microlims_bench_mode", "on");
    localStorage.setItem(
      "microlims.table.inventory.equipment",
      JSON.stringify({ columns: ["code", "status"], density: "compact" })
    );

    const router = createMemoryRouter(
      [{ path: "*", element: <EquipmentHarness /> }],
      { initialEntries: ["/"] }
    );
    render(
      <BenchModeProvider>
        <ThemeModeProvider>
          <RouterProvider router={router} />
        </ThemeModeProvider>
      </BenchModeProvider>
    );

    expect(screen.getByTestId("density-main").textContent).toBe("comfortable");
    await user.click(screen.getByRole("button", { name: "Set Compact" }));
    expect(screen.getByTestId("density-main").textContent).toBe("comfortable");
  });
});
