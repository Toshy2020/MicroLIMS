import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { RegisterTable, type RegisterColumn } from "./RegisterTable";

interface SampleRow {
  id: number;
  code: string;
  name: string;
  count: number;
}

const COLUMNS: RegisterColumn<SampleRow>[] = [
  { key: "code", label: "Code", sortable: true },
  { key: "name", label: "Name", sortable: true },
  { key: "count", label: "Count", sortable: true, numeric: true },
];

function makeRows(n: number): SampleRow[] {
  return Array.from({ length: n }, (_, i) => ({
    id: i + 1,
    code: `SMP-${String(i + 1).padStart(3, "0")}`,
    name: `Sample ${String.fromCharCode(65 + (i % 26))}${i + 1}`,
    count: (n - i) * 10,
  }));
}

function renderTable(ui: React.ReactElement) {
  return render(
    <PinnedLightTheme>
      <MemoryRouter>{ui}</MemoryRouter>
    </PinnedLightTheme>
  );
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

describe("RegisterTable", () => {
  it("sort cycles asc -> desc -> none and sets aria-sort", async () => {
    const user = userEvent.setup();
    const rows: SampleRow[] = [
      { id: 1, code: "B-02", name: "Beta", count: 20 },
      { id: 2, code: "A-01", name: "Alpha", count: 10 },
      { id: 3, code: "C-03", name: "Gamma", count: 30 },
    ];

    renderTable(
      <RegisterTable
        columns={COLUMNS}
        rows={rows}
        getRowId={(r) => r.id}
        empty={{ title: "No rows" }}
      />
    );

    const codeHeader = screen.getByRole("columnheader", { name: "Code" });
    const sortButton = within(codeHeader).getByRole("button", { name: "Code" });

    expect(codeHeader.getAttribute("aria-sort")).toBeNull();
    const getBodyCodes = () =>
      screen
        .getAllByRole("row")
        .slice(1)
        .map((r) => within(r).getAllByRole("cell")[0].textContent);

    expect(getBodyCodes()).toEqual(["B-02", "A-01", "C-03"]);

    await user.click(sortButton);
    expect(codeHeader.getAttribute("aria-sort")).toBe("ascending");
    expect(getBodyCodes()).toEqual(["A-01", "B-02", "C-03"]);

    await user.click(sortButton);
    expect(codeHeader.getAttribute("aria-sort")).toBe("descending");
    expect(getBodyCodes()).toEqual(["C-03", "B-02", "A-01"]);

    await user.click(sortButton);
    expect(codeHeader.getAttribute("aria-sort")).toBeNull();
    expect(getBodyCodes()).toEqual(["B-02", "A-01", "C-03"]);
  });

  it("defaultSort applies before any click", () => {
    const rows: SampleRow[] = [
      { id: 1, code: "B-02", name: "Beta", count: 20 },
      { id: 2, code: "A-01", name: "Alpha", count: 10 },
      { id: 3, code: "C-03", name: "Gamma", count: 30 },
    ];

    renderTable(
      <RegisterTable
        columns={COLUMNS}
        rows={rows}
        getRowId={(r) => r.id}
        defaultSort={{ key: "code", direction: "desc" }}
        empty={{ title: "No rows" }}
      />
    );

    const codeHeader = screen.getByRole("columnheader", { name: "Code" });
    expect(codeHeader.getAttribute("aria-sort")).toBe("descending");

    const codes = screen
      .getAllByRole("row")
      .slice(1)
      .map((r) => within(r).getAllByRole("cell")[0].textContent);
    expect(codes).toEqual(["C-03", "B-02", "A-01"]);
  });

  it("pagination appears above 25 rows; options 25/50/100; page resets when row count changes", async () => {
    const user = userEvent.setup();
    const { rerender } = renderTable(
      <RegisterTable
        columns={COLUMNS}
        rows={makeRows(25)}
        getRowId={(r) => r.id}
        empty={{ title: "No rows" }}
      />
    );

    expect(screen.queryByRole("button", { name: /next page/i })).toBeNull();

    // Appears at 26 rows (above 25)
    rerender(
      <PinnedLightTheme>
        <MemoryRouter>
          <RegisterTable
            columns={COLUMNS}
            rows={makeRows(26)}
            getRowId={(r) => r.id}
            empty={{ title: "No rows" }}
          />
        </MemoryRouter>
      </PinnedLightTheme>
    );
    expect(screen.getByText("1–25 of 26")).toBeTruthy();

    rerender(
      <PinnedLightTheme>
        <MemoryRouter>
          <RegisterTable
            columns={COLUMNS}
            rows={makeRows(30)}
            getRowId={(r) => r.id}
            empty={{ title: "No rows" }}
          />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    expect(screen.getByText("1–25 of 30")).toBeTruthy();

    // Go to page 2
    await user.click(screen.getByRole("button", { name: /next page/i }));
    expect(screen.getByText("26–30 of 30")).toBeTruthy();

    // Changing row count resets page to 0
    rerender(
      <PinnedLightTheme>
        <MemoryRouter>
          <RegisterTable
            columns={COLUMNS}
            rows={makeRows(28)}
            getRowId={(r) => r.id}
            empty={{ title: "No rows" }}
          />
        </MemoryRouter>
      </PinnedLightTheme>
    );
    expect(screen.getByText("1–25 of 28")).toBeTruthy();

    // Check rows-per-page options 25/50/100
    const rowsPerPageSelect = screen.getByRole("combobox", { name: /rows per page/i });
    await user.click(rowsPerPageSelect);
    const listbox = screen.getByRole("listbox");
    expect(within(listbox).getByRole("option", { name: "25" })).toBeTruthy();
    expect(within(listbox).getByRole("option", { name: "50" })).toBeTruthy();
    expect(within(listbox).getByRole("option", { name: "100" })).toBeTruthy();

    await user.click(within(listbox).getByRole("option", { name: "50" }));
    expect(screen.getByText("1–28 of 28")).toBeTruthy();
  });

  it("row actions menu opens from 'Row actions for {id}' and calls onClick; disabled action cannot be chosen", async () => {
    const user = userEvent.setup();
    const onInspect = vi.fn();
    const onArchive = vi.fn();

    renderTable(
      <RegisterTable
        columns={COLUMNS}
        rows={[makeRows(1)[0]]}
        getRowId={(r) => r.id}
        rowActions={() => [
          { label: "Inspect", onClick: onInspect },
          { label: "Archive", onClick: onArchive, disabled: true, danger: true },
        ]}
        empty={{ title: "No rows" }}
      />
    );

    await user.click(screen.getByRole("button", { name: "Row actions for 1" }));

    const inspectItem = screen.getByRole("menuitem", { name: "Inspect" });
    const archiveItem = screen.getByRole("menuitem", { name: "Archive" });
    expect(archiveItem.getAttribute("aria-disabled")).toBe("true");

    await expect(user.click(archiveItem)).rejects.toThrow(/pointer-events: none/);
    expect(onArchive).not.toHaveBeenCalled();

    await user.click(inspectItem);
    expect(onInspect).toHaveBeenCalledTimes(1);
  });

  it("Enter on a focused row calls onRowClick", async () => {
    const user = userEvent.setup();
    const onRowClick = vi.fn();
    const rows = makeRows(2);

    renderTable(
      <RegisterTable
        columns={COLUMNS}
        rows={rows}
        getRowId={(r) => r.id}
        onRowClick={onRowClick}
        empty={{ title: "No rows" }}
      />
    );

    const dataRows = screen.getAllByRole("row").slice(1);
    dataRows[1].focus();
    await user.keyboard("{Enter}");

    expect(onRowClick).toHaveBeenCalledTimes(1);
    expect(onRowClick).toHaveBeenCalledWith(rows[1]);
  });

  it("phone width renders cards with a Sort by select", async () => {
    const user = userEvent.setup();
    vi.stubGlobal(
      "matchMedia",
      vi.fn().mockImplementation((query: string) => ({
        matches: true,
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      }))
    );

    const rows: SampleRow[] = [
      { id: 1, code: "B-02", name: "Beta", count: 20 },
      { id: 2, code: "A-01", name: "Alpha", count: 10 },
    ];

    renderTable(
      <RegisterTable
        columns={COLUMNS}
        rows={rows}
        getRowId={(r) => r.id}
        empty={{ title: "No rows" }}
      />
    );

    // No table rendered in phone width; list of cards instead
    expect(screen.queryByRole("table")).toBeNull();
    const list = screen.getByRole("list");
    expect(within(list).getAllByRole("listitem")).toHaveLength(2);

    expect(screen.getByText("Sort by")).toBeTruthy();
    const sortSelect = screen.getByRole("combobox", { name: "Sort records by" });
    await user.click(sortSelect);
    await user.click(screen.getByRole("option", { name: "Code (ascending)" }));

    const items = within(screen.getByRole("list")).getAllByRole("listitem");
    expect(items[0].textContent).toContain("A-01");
    expect(items[1].textContent).toContain("B-02");
  });

  it("renders empty and loading states", () => {
    const { rerender } = renderTable(
      <RegisterTable
        columns={COLUMNS}
        rows={[]}
        getRowId={(r) => r.id}
        loading
        empty={{ title: "Nothing here", description: "Try clearing filters." }}
      />
    );

    expect(screen.queryByRole("status")).toBeNull();
    expect(screen.getAllByRole("row").length).toBeGreaterThan(1);

    rerender(
      <PinnedLightTheme>
        <MemoryRouter>
          <RegisterTable
            columns={COLUMNS}
            rows={[]}
            getRowId={(r) => r.id}
            loading={false}
            empty={{
              title: "Nothing here",
              description: "Try clearing filters.",
              action: <button type="button">Reset</button>,
            }}
          />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const status = screen.getByRole("status");
    expect(status.textContent).toContain("Nothing here");
    expect(status.textContent).toContain("Try clearing filters.");
    expect(within(status).getByRole("button", { name: "Reset" })).toBeTruthy();
  });
});
