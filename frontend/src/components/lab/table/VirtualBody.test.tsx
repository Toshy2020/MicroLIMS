import { useState } from "react";
import { act, cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { RegisterTable, type RegisterColumn } from "../RegisterTable";
import { tableViewService } from "./services/tableViewService";
import { useRowSelection } from "./useRowSelection";
import { useTableState } from "./useTableState";

vi.mock("./services/tableViewService", () => ({
  tableViewService: {
    list: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
    setDefault: vi.fn(),
  },
}));

const mockedViewsList = vi.mocked(tableViewService.list);

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
    name: `Sample ${i + 1}`,
    count: (i + 1) * 10,
  }));
}

function makeRect(width: number, height: number): DOMRect {
  return {
    width,
    height,
    top: 0,
    left: 0,
    bottom: height,
    right: width,
    x: 0,
    y: 0,
    toJSON: () => ({}),
  };
}

function scrollContainerTo(container: HTMLElement, scrollTop: number): void {
  act(() => {
    container.scrollTop = scrollTop;
    container.dispatchEvent(new Event("scroll"));
  });
}

function getScrollContainer(): HTMLElement {
  const table = screen.getByRole("table");
  const container = table.closest(".MuiTableContainer-root");
  if (!(container instanceof HTMLElement)) {
    throw new Error("Expected MuiTableContainer-root ancestor");
  }
  return container;
}

let origClientHeightDesc: PropertyDescriptor | undefined;
let origScrollHeightDesc: PropertyDescriptor | undefined;
let origOffsetHeightDesc: PropertyDescriptor | undefined;
let getBoundingClientRectSpy: ReturnType<typeof vi.spyOn> | undefined;

beforeEach(() => {
  localStorage.clear();
  vi.clearAllMocks();
  mockedViewsList.mockResolvedValue([]);

  origClientHeightDesc = Object.getOwnPropertyDescriptor(
    HTMLElement.prototype,
    "clientHeight"
  );
  origScrollHeightDesc = Object.getOwnPropertyDescriptor(
    HTMLElement.prototype,
    "scrollHeight"
  );
  origOffsetHeightDesc = Object.getOwnPropertyDescriptor(
    HTMLElement.prototype,
    "offsetHeight"
  );

  Object.defineProperty(HTMLElement.prototype, "clientHeight", {
    configurable: true,
    get(this: HTMLElement) {
      if (this.classList.contains("MuiTableContainer-root")) {
        return 320;
      }
      return 0;
    },
  });

  Object.defineProperty(HTMLElement.prototype, "scrollHeight", {
    configurable: true,
    get(this: HTMLElement) {
      if (this.classList.contains("MuiTableContainer-root")) {
        return 10000;
      }
      return 0;
    },
  });

  Object.defineProperty(HTMLElement.prototype, "offsetHeight", {
    configurable: true,
    get(this: HTMLElement) {
      if (this.getAttribute("data-expanded-for") !== null) {
        return 80;
      }
      if (this.tagName === "TR") {
        return 40;
      }
      return 0;
    },
  });

  getBoundingClientRectSpy = vi
    .spyOn(Element.prototype, "getBoundingClientRect")
    .mockImplementation(function (this: Element) {
      if (this.classList.contains("MuiTableContainer-root")) {
        return makeRect(800, 320);
      }
      if (this.getAttribute("data-expanded-for") !== null) {
        return makeRect(800, 80);
      }
      if (this.tagName === "TR") {
        return makeRect(800, 40);
      }
      return makeRect(0, 0);
    });
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.unstubAllGlobals();
  getBoundingClientRectSpy?.mockRestore();
  if (origClientHeightDesc) {
    Object.defineProperty(HTMLElement.prototype, "clientHeight", origClientHeightDesc);
  }
  if (origScrollHeightDesc) {
    Object.defineProperty(HTMLElement.prototype, "scrollHeight", origScrollHeightDesc);
  }
  if (origOffsetHeightDesc) {
    Object.defineProperty(HTMLElement.prototype, "offsetHeight", origOffsetHeightDesc);
  }
});

describe("VirtualBody and expandable rows in RegisterTable", () => {
  it("100 rows render all; 101 render a window", () => {
    const { rerender } = render(
      <PinnedLightTheme>
        <MemoryRouter>
          <RegisterTable
            columns={COLUMNS}
            rows={makeRows(100)}
            getRowId={(r) => r.id}
            pageSize="all"
            empty={{ title: "No rows" }}
          />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const rowsFor100 = screen.getAllByRole("row");
    // 1 header row + 100 data rows
    expect(rowsFor100).toHaveLength(101);
    expect(screen.getByText("SMP-001")).toBeTruthy();
    expect(screen.getByText("SMP-100")).toBeTruthy();

    rerender(
      <PinnedLightTheme>
        <MemoryRouter>
          <RegisterTable
            columns={COLUMNS}
            rows={makeRows(101)}
            getRowId={(r) => r.id}
            pageSize="all"
            empty={{ title: "No rows" }}
          />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const rowsFor101 = screen.getAllByRole("row");
    expect(rowsFor101.length).toBeGreaterThan(1);
    expect(rowsFor101.length).toBeLessThan(30);
    expect(screen.getByText("SMP-001")).toBeTruthy();
    expect(screen.queryByText("SMP-101")).toBeNull();
  });

  it("aria-rowcount equals the total and each rendered row has the right aria-rowindex", () => {
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <RegisterTable
            columns={COLUMNS}
            rows={makeRows(150)}
            getRowId={(r) => r.id}
            pageSize="all"
            empty={{ title: "No rows" }}
          />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const table = screen.getByRole("table");
    expect(table.getAttribute("aria-rowcount")).toBe("151");

    const initialRows = screen.getAllByRole("row");
    expect(initialRows[0].getAttribute("aria-rowindex")).toBe("1");
    expect(initialRows[1].getAttribute("aria-rowindex")).toBe("2");
    expect(within(initialRows[1]).getByText("SMP-001")).toBeTruthy();

    initialRows.slice(1).forEach((rowEl, idx) => {
      expect(rowEl.getAttribute("aria-rowindex")).toBe(String(idx + 2));
    });

    const container = getScrollContainer();
    scrollContainerTo(container, 2000);

    const scrolledRows = screen.getAllByRole("row").slice(1);
    expect(scrolledRows.length).toBeGreaterThan(0);
    for (const rowEl of scrolledRows) {
      const rowIndexAttr = Number(rowEl.getAttribute("aria-rowindex"));
      // Header row is 1, so row with id=k has aria-rowindex = k + 1
      const expectedCode = `SMP-${String(rowIndexAttr - 1).padStart(3, "0")}`;
      expect(within(rowEl).getByText(expectedCode)).toBeTruthy();
    }
  });

  it('page size "All" is offered and persists in state', async () => {
    const user = userEvent.setup();
    const rows = makeRows(120);

    function StateHarness() {
      const ts = useTableState("inventory.materials", {
        columns: ["code", "name", "count"],
      });
      const location = useLocation();
      return (
        <div>
          <span data-testid="page-size-state">{String(ts.state.pageSize)}</span>
          <span data-testid="location-search">{location.search}</span>
          <RegisterTable
            tableKey="inventory.materials"
            tableState={ts}
            columns={COLUMNS}
            rows={rows}
            getRowId={(r) => r.id}
            empty={{ title: "No rows" }}
          />
        </div>
      );
    }

    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <StateHarness />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    expect(screen.getByTestId("page-size-state").textContent).toBe("25");
    expect(screen.getAllByRole("row")).toHaveLength(26);

    const rowsPerPageSelect = screen.getByRole("combobox", {
      name: /rows per page/i,
    });
    await user.click(rowsPerPageSelect);
    const listbox = screen.getByRole("listbox");
    const allOption = within(listbox).getByRole("option", { name: "All" });
    await user.click(allOption);

    expect(screen.getByTestId("page-size-state").textContent).toBe("all");
    expect(screen.getByTestId("location-search").textContent).toContain("size=all");

    const table = screen.getByRole("table");
    expect(table.getAttribute("aria-rowcount")).toBe("121");
    expect(screen.getAllByRole("row").length).toBeLessThan(30);
  });

  it("arrow-key focus moves beyond the rendered window and scrolls it into view", async () => {
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <RegisterTable
            columns={COLUMNS}
            rows={makeRows(120)}
            getRowId={(r) => r.id}
            pageSize="all"
            empty={{ title: "No rows" }}
          />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const container = getScrollContainer();
    expect(container.scrollTop).toBe(0);
    // Row 15 is outside the initial window (8 visible + 3 overscan = 11 rows)
    expect(screen.queryByText("SMP-015")).toBeNull();

    const firstDataRow = screen.getAllByRole("row")[1];
    firstDataRow.focus();
    expect(document.activeElement).toBe(firstDataRow);

    for (let i = 0; i < 14; i++) {
      await user.keyboard("{ArrowDown}");
    }

    const row15Cell = screen.getByText("SMP-015");
    const row15 = row15Cell.closest("tr");
    expect(row15).not.toBeNull();
    expect(row15?.getAttribute("aria-rowindex")).toBe("16");
    expect(document.activeElement).toBe(row15);
    expect(container.scrollTop).toBeGreaterThan(0);

    // Focused row remains mounted even if user scrolls the container far away
    scrollContainerTo(container, 3200);
    expect(screen.getByText("SMP-015")).toBeTruthy();
    expect(document.activeElement).toBe(row15);
  });

  it("expanding a row renders renderExpanded beneath it and does not shift scroll position", async () => {
    const user = userEvent.setup();
    const rows = makeRows(120);

    function ExpandHarness() {
      const [expanded, setExpanded] = useState<Set<string | number>>(new Set());
      const toggle = (id: string | number) => {
        setExpanded((prev) => {
          const next = new Set(prev);
          if (next.has(id)) next.delete(id);
          else next.add(id);
          return next;
        });
      };
      return (
        <RegisterTable
          columns={COLUMNS}
          rows={rows}
          getRowId={(r) => r.id}
          pageSize="all"
          expandedIds={expanded}
          onToggleExpand={toggle}
          renderExpanded={(row) => (
            <div data-testid={`expanded-content-${row.id}`}>
              Expanded tests for {row.code}
            </div>
          )}
          empty={{ title: "No rows" }}
        />
      );
    }

    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <ExpandHarness />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const container = getScrollContainer();
    scrollContainerTo(container, 400);
    expect(container.scrollTop).toBe(400);

    // Row 12 (index 11, top = 440) is visible at scrollTop = 400
    const expandBtn = screen.getByRole("button", { name: "Expand row 12" });
    expect(expandBtn.getAttribute("aria-expanded")).toBe("false");

    await user.click(expandBtn);

    const collapseBtn = screen.getByRole("button", { name: "Collapse row 12" });
    expect(collapseBtn.getAttribute("aria-expanded")).toBe("true");

    const expandedContent = screen.getByTestId("expanded-content-12");
    expect(expandedContent.textContent).toContain("Expanded tests for SMP-012");

    const mainRow = screen.getByText("SMP-012").closest("tr");
    const expandedRow = expandedContent.closest("tr");
    expect(mainRow?.nextElementSibling).toBe(expandedRow);

    // Scroll position must not shift when expanding a visible row
    expect(container.scrollTop).toBe(400);
  });

  it("expands and collapses a row in the plain (not virtualised) body", async () => {
    const user = userEvent.setup();
    const rows = makeRows(10);

    function SmallHarness() {
      const [expanded, setExpanded] = useState<Set<string | number>>(new Set());
      const toggle = (id: string | number) => {
        setExpanded((prev) => {
          const next = new Set(prev);
          if (next.has(id)) next.delete(id);
          else next.add(id);
          return next;
        });
      };
      return (
        <RegisterTable
          columns={COLUMNS}
          rows={rows}
          getRowId={(r) => r.id}
          expandedIds={expanded}
          onToggleExpand={toggle}
          renderExpanded={(row) => <div data-testid={`expanded-content-${row.id}`}>Tests for {row.code}</div>}
          empty={{ title: "No rows" }}
        />
      );
    }

    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <SmallHarness />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    expect(screen.getByRole("table").getAttribute("aria-rowcount")).toBeNull();
    await user.click(screen.getByRole("button", { name: "Expand row 3" }));

    const content = screen.getByTestId("expanded-content-3");
    expect(screen.getByText("SMP-003").closest("tr")?.nextElementSibling).toBe(content.closest("tr"));
    expect(screen.getByRole("button", { name: "Collapse row 3" }).getAttribute("aria-expanded")).toBe("true");

    await user.click(screen.getByRole("button", { name: "Collapse row 3" }));
    expect(screen.queryByTestId("expanded-content-3")).toBeNull();
  });

  it("expanded row stays expanded after scrolling away and back", async () => {
    const user = userEvent.setup();
    const rows = makeRows(120);

    function ExpandHarness() {
      const [expanded, setExpanded] = useState<Set<string | number>>(new Set());
      const toggle = (id: string | number) => {
        setExpanded((prev) => {
          const next = new Set(prev);
          if (next.has(id)) next.delete(id);
          else next.add(id);
          return next;
        });
      };
      return (
        <RegisterTable
          columns={COLUMNS}
          rows={rows}
          getRowId={(r) => r.id}
          pageSize="all"
          expandedIds={expanded}
          onToggleExpand={toggle}
          renderExpanded={(row) => (
            <div>Expanded details for {row.code}</div>
          )}
          empty={{ title: "No rows" }}
        />
      );
    }

    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <ExpandHarness />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const expandBtn = screen.getByRole("button", { name: "Expand row 2" });
    await user.click(expandBtn);
    expect(screen.getByText("Expanded details for SMP-002")).toBeTruthy();

    const container = getScrollContainer();
    // Scroll far down so row 2 is unmounted
    scrollContainerTo(container, 3200);
    expect(screen.queryByText("SMP-002")).toBeNull();
    expect(screen.queryByText("Expanded details for SMP-002")).toBeNull();

    // Scroll back to top
    scrollContainerTo(container, 0);
    expect(screen.getByText("SMP-002")).toBeTruthy();
    expect(screen.getByText("Expanded details for SMP-002")).toBeTruthy();
    const collapseBtn = screen.getByRole("button", { name: "Collapse row 2" });
    expect(collapseBtn.getAttribute("aria-expanded")).toBe("true");
  });

  it("selection checkbox state is correct for rows that were unmounted and remounted", async () => {
    const user = userEvent.setup();
    const rows = makeRows(120);
    localStorage.setItem(
      "microlims_permissions",
      JSON.stringify([PERMISSIONS.DATA_EXPORT])
    );

    function SelectionHarness() {
      const selection = useRowSelection<string | number>("virtual-test");
      return (
        <RegisterTable
          columns={COLUMNS}
          rows={rows}
          getRowId={(r) => r.id}
          pageSize="all"
          selection={selection}
          bulkActions={[{ key: "act", label: "Act", run: vi.fn() }]}
          empty={{ title: "No rows" }}
        />
      );
    }

    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <AuthProvider>
            <SelectionHarness />
          </AuthProvider>
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const checkbox1 = screen.getByRole("checkbox", {
      name: "Select row 1",
    }) as HTMLInputElement;
    expect(checkbox1.checked).toBe(false);
    await user.click(checkbox1);
    expect(checkbox1.checked).toBe(true);

    const container = getScrollContainer();
    // Scroll far away so row 1 unmounts
    scrollContainerTo(container, 3200);
    expect(screen.queryByRole("checkbox", { name: "Select row 1" })).toBeNull();

    // Scroll back to top so row 1 remounts
    scrollContainerTo(container, 0);
    const remountedCheckbox1 = screen.getByRole("checkbox", {
      name: "Select row 1",
    }) as HTMLInputElement;
    const remountedCheckbox2 = screen.getByRole("checkbox", {
      name: "Select row 2",
    }) as HTMLInputElement;
    expect(remountedCheckbox1.checked).toBe(true);
    expect(remountedCheckbox2.checked).toBe(false);
  });

  it("phone card mode is not virtualised and still paginates", async () => {
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

    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <RegisterTable
            columns={COLUMNS}
            rows={makeRows(120)}
            getRowId={(r) => r.id}
            pageSize="all"
            empty={{ title: "No rows" }}
          />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    expect(screen.queryByRole("table")).toBeNull();
    const itemsPage1 = screen.getAllByRole("listitem");
    expect(itemsPage1).toHaveLength(25);
    expect(itemsPage1[0].textContent).toContain("SMP-001");
    expect(screen.getByText("1–25 of 120")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: /next page/i }));
    expect(screen.getByText("26–50 of 120")).toBeTruthy();
    const itemsPage2 = screen.getAllByRole("listitem");
    expect(itemsPage2).toHaveLength(25);
    expect(itemsPage2[0].textContent).toContain("SMP-026");
  });
});
