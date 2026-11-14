import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { BenchModeProvider } from "../../../theme/BenchModeContext";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ThemeModeProvider } from "../../../theme/ThemeModeContext";
import { RegisterTable, type RegisterColumn } from "../RegisterTable";
import { TableToolbar } from "./TableToolbar";
import { useTableState } from "./useTableState";

interface EquipmentRow {
  code: string;
  instrumentType: string;
  status: string;
  location: string;
}

const COLUMNS: RegisterColumn<EquipmentRow>[] = [
  { key: "instrumentType", label: "Type", sortable: true },
  { key: "code", label: "Code", sortable: true },
  { key: "location", label: "Location" },
  { key: "status", label: "Status" },
];

const ROWS: EquipmentRow[] = [
  {
    code: "EQ-001",
    instrumentType: "HPLC",
    status: "Active",
    location: "Room 101",
  },
  {
    code: "EQ-002",
    instrumentType: "Balance",
    status: "Due",
    location: "Room 102",
  },
];

function StatefulEquipmentTable() {
  const ts = useTableState("inventory.equipment", {
    columns: ["instrumentType", "code", "location", "status"],
  });
  return (
    <RegisterTable
      tableKey="inventory.equipment"
      tableState={ts}
      columns={COLUMNS}
      rows={ROWS}
      getRowId={(r) => r.code}
      empty={{ title: "No equipment" }}
      toolbar={<TableToolbar tableKey="inventory.equipment" tableState={ts} />}
    />
  );
}

beforeEach(() => {
  localStorage.clear();
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.restoreAllMocks();
});

describe("ColumnsMenu, DensityToggle, and RegisterTable integration", () => {
  it("columns menu: hide, show, Move up/Move down by keyboard; id column checkbox disabled with reason; Reset to default", async () => {
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <StatefulEquipmentTable />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const columnsBtn = screen.getByRole("button", { name: "Columns" });
    columnsBtn.focus();
    await user.keyboard("{Enter}");

    const region = screen.getByRole("region", { name: "Columns" });
    const codeCheckbox = within(region).getByRole("checkbox", {
      name: "Code",
    }) as HTMLInputElement;
    expect(codeCheckbox.disabled).toBe(true);
    expect(codeCheckbox.checked).toBe(true);
    // Visible & accessible reason text for disabled ID column checkbox
    const describedBy = codeCheckbox.getAttribute("aria-describedby");
    expect(describedBy).toBeTruthy();
    expect(
      within(region).getByText("Required identifier column").id
    ).toBe(describedBy);

    // Hide "Location" via keyboard, then close popover to inspect table
    const locationCheckbox = within(region).getByRole("checkbox", {
      name: "Location",
    }) as HTMLInputElement;
    expect(locationCheckbox.checked).toBe(true);
    locationCheckbox.focus();
    await user.keyboard(" ");
    expect(locationCheckbox.checked).toBe(false);
    await user.keyboard("{Escape}");
    expect(document.activeElement).toBe(columnsBtn);
    expect(
      screen.queryByRole("columnheader", { name: "Location" })
    ).toBeNull();

    // Re-open and show "Location" again via keyboard
    await user.keyboard("{Enter}");
    const region2 = screen.getByRole("region", { name: "Columns" });
    const locationCheckbox2 = within(region2).getByRole("checkbox", {
      name: "Location",
    }) as HTMLInputElement;
    locationCheckbox2.focus();
    await user.keyboard(" ");
    expect(locationCheckbox2.checked).toBe(true);

    // Move "Status" up by keyboard
    const moveStatusUp = within(region2).getByRole("button", {
      name: "Move Status up",
    });
    moveStatusUp.focus();
    await user.keyboard("{Enter}");
    await user.keyboard("{Escape}");

    const headersAfterMoveUp = screen
      .getAllByRole("columnheader")
      .map((h) => h.textContent?.trim());
    expect(headersAfterMoveUp).toEqual(["Type", "Status", "Code", "Location"]);

    // Re-open and move "Status" back down by keyboard
    columnsBtn.focus();
    await user.keyboard("{Enter}");
    const region3 = screen.getByRole("region", { name: "Columns" });
    const moveStatusDown = within(region3).getByRole("button", {
      name: "Move Status down",
    });
    moveStatusDown.focus();
    await user.keyboard("{Enter}");
    await user.keyboard("{Escape}");

    const headersAfterMoveDown = screen
      .getAllByRole("columnheader")
      .map((h) => h.textContent?.trim());
    expect(headersAfterMoveDown).toEqual(["Type", "Code", "Status", "Location"]);

    // Re-open, hide Location, then Reset to default
    columnsBtn.focus();
    await user.keyboard("{Enter}");
    const region4 = screen.getByRole("region", { name: "Columns" });
    const locationCheckbox4 = within(region4).getByRole("checkbox", {
      name: "Location",
    }) as HTMLInputElement;
    locationCheckbox4.focus();
    await user.keyboard(" ");
    expect(locationCheckbox4.checked).toBe(false);

    const resetBtn = within(region4).getByRole("button", {
      name: "Reset to default",
    });
    resetBtn.focus();
    await user.keyboard("{Enter}");
    await user.keyboard("{Escape}");
    expect(
      screen.getByRole("columnheader", { name: "Location" })
    ).toBeTruthy();
    expect(document.activeElement).toBe(columnsBtn);
  });

  it("column order and visibility drive header and cells", async () => {
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <StatefulEquipmentTable />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const getFirstRowCells = () =>
      within(screen.getAllByRole("row")[1])
        .getAllByRole("cell")
        .map((c) => c.textContent);

    expect(getFirstRowCells()).toEqual([
      "HPLC",
      "EQ-001",
      "Room 101",
      "Active",
    ]);

    await user.click(screen.getByRole("button", { name: "Columns" }));
    const region = screen.getByRole("region", { name: "Columns" });

    // Hide Type and move Status up before Location, then close popover
    await user.click(within(region).getByRole("checkbox", { name: "Type" }));
    await user.click(
      within(region).getByRole("button", { name: "Move Status up" })
    );
    await user.keyboard("{Escape}");

    const headers = screen
      .getAllByRole("columnheader")
      .map((h) => h.textContent?.trim());
    expect(headers).toEqual(["Code", "Status", "Location"]);
    expect(getFirstRowCells()).toEqual(["EQ-001", "Active", "Room 101"]);
  });

  it("density compact changes row padding; bench mode ignores compact", async () => {
    const user = userEvent.setup();
    const { unmount } = render(
      <PinnedLightTheme>
        <MemoryRouter>
          <StatefulEquipmentTable />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const firstCell = () =>
      within(screen.getAllByRole("row")[1]).getAllByRole("cell")[0];

    const comfortablePadding = parseFloat(
      getComputedStyle(firstCell()).paddingTop
    );

    await user.click(
      screen.getByRole("button", { name: "Compact density" })
    );
    const compactPadding = parseFloat(
      getComputedStyle(firstCell()).paddingTop
    );
    expect(compactPadding).toBeLessThan(comfortablePadding);

    unmount();

    // Bench mode ignores compact even when compact was previously persisted.
    localStorage.setItem("microlims_bench_mode", "on");
    render(
      <BenchModeProvider>
        <ThemeModeProvider>
          <MemoryRouter>
            <StatefulEquipmentTable />
          </MemoryRouter>
        </ThemeModeProvider>
      </BenchModeProvider>
    );

    const benchCompactBtn = screen.getByRole("button", {
      name: /Compact density/i,
    }) as HTMLButtonElement;
    expect(benchCompactBtn.disabled).toBe(true);
    const benchPadding = parseFloat(
      getComputedStyle(firstCell()).paddingTop
    );
    expect(benchPadding).toBeGreaterThanOrEqual(comfortablePadding);
  });

  it("RegisterTable without tableKey does not read localStorage", () => {
    const getItemSpy = vi.spyOn(Storage.prototype, "getItem");
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <RegisterTable
            columns={COLUMNS}
            rows={ROWS}
            getRowId={(r) => r.code}
            empty={{ title: "No equipment" }}
          />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    expect(getItemSpy).not.toHaveBeenCalled();
  });
});
