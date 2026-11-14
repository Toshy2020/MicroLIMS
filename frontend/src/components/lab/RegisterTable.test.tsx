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

describe("RegisterTable server pages, trailing cell and selective expand toggles", () => {
  it("serverPaged: shows the rows it is given, pages by the server total and does not slice them", async () => {
    const user = userEvent.setup();
    renderTable(
      <RegisterTable
        columns={COLUMNS}
        rows={makeRows(30)}
        getRowId={(r) => r.id}
        empty={{ title: "No rows" }}
        serverPaged={{ totalCount: 120 }}
      />
    );
    // 30 rows were supplied: more than a client page of 25, and all of them are shown.
    expect(screen.getAllByText(/^SMP-/)).toHaveLength(30);
    expect(screen.getByText("1–25 of 120")).toBeTruthy();
    // "All" is not offered: a server page is capped.
    await user.click(screen.getByRole("combobox", { name: /rows per page/i }));
    expect(screen.queryByRole("option", { name: "All" })).toBeNull();
    await user.keyboard("{Escape}");
    await user.click(screen.getByRole("button", { name: "Go to next page" }));
    // The caller supplies the next page; the rows are not cut by a client-side slice.
    expect(screen.getAllByText(/^SMP-/)).toHaveLength(30);
    expect(screen.getByText("26–50 of 120")).toBeTruthy();
  });

  it("serverPaged: offers no column sort, since only one server page is held", () => {
    renderTable(
      <RegisterTable
        columns={COLUMNS}
        rows={makeRows(5)}
        getRowId={(r) => r.id}
        empty={{ title: "No rows" }}
        serverPaged={{ totalCount: 120 }}
      />
    );
    for (const header of screen.getAllByRole("columnheader")) {
      expect(within(header).queryByRole("button")).toBeNull();
    }
    expect(screen.queryByText("Sort by")).toBeNull();
  });

  it("serverPaged with server sort handling: shows sort controls only for allow-listed columns and delegates clicks without client sorting", async () => {
    const user = userEvent.setup();
    const onSortChange = vi.fn();
    const rows: SampleRow[] = [
      { id: 1, code: "SMP-002", name: "Zeta", count: 5 },
      { id: 2, code: "SMP-001", name: "Alpha", count: 9 }
    ];
    renderTable(
      <RegisterTable
        columns={COLUMNS}
        rows={rows}
        getRowId={(r) => r.id}
        empty={{ title: "No rows" }}
        serverPaged={{
          totalCount: 120,
          sortableColumns: ["name"],
          onSortChange
        }}
      />
    );

    const headers = screen.getAllByRole("columnheader");
    // "Code" is sortable in COLUMNS but omitted from sortableColumns -> no button.
    expect(within(headers[0]!).queryByRole("button")).toBeNull();
    // "Count" is not sortable in COLUMNS -> no button.
    expect(within(headers[2]!).queryByRole("button")).toBeNull();
    // "Name" is both sortable and in sortableColumns -> has sort button.
    const nameSortButton = within(headers[1]!).getByRole("button", { name: "Name" });

    await user.click(nameSortButton);
    expect(onSortChange).toHaveBeenLastCalledWith({ key: "name", direction: "asc" });
    // Rows stay in server order (no client-side sorting).
    expect(screen.getAllByText(/^SMP-00/).map((e) => e.textContent)).toEqual(["SMP-002", "SMP-001"]);

    await user.click(within(screen.getAllByRole("columnheader")[1]!).getByRole("button", { name: "Name" }));
    expect(onSortChange).toHaveBeenLastCalledWith({ key: "name", direction: "desc" });
  });

  it("trailingCell: adds a labelled last column; the row click reports the event so a caller can ignore controls inside", async () => {
    const user = userEvent.setup();
    const onRowClick = vi.fn();
    const onAction = vi.fn();
    renderTable(
      <RegisterTable
        columns={COLUMNS}
        rows={makeRows(2)}
        getRowId={(r) => r.id}
        empty={{ title: "No rows" }}
        onRowClick={onRowClick}
        trailingCell={{ label: "Menu", render: (r) => <button type="button" onClick={() => onAction(r.id)}>Do {r.id}</button> }}
      />
    );
    const headers = screen.getAllByRole("columnheader").map((h) => h.textContent);
    expect(headers).toEqual(["Code", "Name", "Count", "Menu"]);
    await user.click(screen.getByRole("button", { name: "Do 2" }));
    expect(onAction).toHaveBeenCalledWith(2);
    expect(onRowClick).toHaveBeenCalledTimes(1);
    const [row, event] = onRowClick.mock.calls[0] as [SampleRow, { target: Element } | undefined];
    expect(row.id).toBe(2);
    expect(event?.target.closest("button")?.textContent).toBe("Do 2");
  });

  it("isExpandable: only flagged rows get a toggle; selectionLabels and pageSelectionIds name and scope the checkboxes", async () => {
    const user = userEvent.setup();
    const toggled = vi.fn();
    const selected = vi.fn();
    const selection = {
      selected: new Set<string | number>(),
      allMatching: false,
      count: 0,
      isSelected: () => false,
      toggle: (id: string | number) => selected(id),
      togglePage: (ids: Array<string | number>) => selected(ids),
      selectAllMatching: vi.fn(),
      clear: vi.fn()
    };
    renderTable(
      <RegisterTable
        columns={COLUMNS}
        rows={makeRows(3)}
        getRowId={(r) => r.id}
        empty={{ title: "No rows" }}
        renderExpanded={() => <span>more</span>}
        expandedIds={new Set()}
        onToggleExpand={toggled}
        isExpandable={(r) => r.id === 2}
        selection={selection}
        alwaysSelectable
        selectionLabels={{ row: (r) => `Pick ${r.code}`, page: (n, all) => `${all ? "Unpick" : "Pick"} all ${n}` }}
        pageSelectionIds={(paged) => [...paged.map((r) => r.id), 99]}
      />
    );
    expect(screen.getAllByRole("button", { name: /^Expand row/ })).toHaveLength(1);
    await user.click(screen.getByRole("button", { name: "Expand row 2" }));
    expect(toggled).toHaveBeenCalledWith(2);
    await user.click(screen.getByRole("checkbox", { name: "Pick SMP-003" }));
    expect(selected).toHaveBeenLastCalledWith(3);
    await user.click(screen.getByRole("checkbox", { name: "Pick all 4" }));
    expect(selected).toHaveBeenLastCalledWith([1, 2, 3, 99]);
  });
});
