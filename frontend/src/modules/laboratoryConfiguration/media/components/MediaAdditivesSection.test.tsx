import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { MediaAdditivesSection, UsableSolution } from "./MediaAdditivesSection";
import { AdditiveRow, buildAdditivesPayload, newAdditiveRow, validateAdditives } from "./mediaAdditives";
import type { MaterialItem } from "../../../inventory/materials/types/materialTypes";

afterEach(cleanup);

const lot = { id: 7, stockId: "STK-2026-1", materialName: "Polysorbate 80", batchNumber: "B1", quantityRemaining: 90, unit: "Milliliter" } as unknown as MaterialItem;
const solution: UsableSolution = { id: 3, code: "NT-0001/26", solutionMasterName: "Letheen", type: "Neutralizer", expiresAt: "2026-12-01T00:00:00Z" };

describe("additive payload", () => {
  it("sends nothing for no rows", () => {
    expect(validateAdditives([])).toBeNull();
    expect(buildAdditivesPayload([])).toEqual([]);
  });

  it("shapes stock-lot and prepared-solution rows", () => {
    const rows: AdditiveRow[] = [
      { key: 1, kind: "StockLot", sourceId: 7, quantity: "5", unit: "Milliliter" },
      { key: 2, kind: "PreparedSolution", sourceId: 3, quantity: "12.5", unit: " mL " }
    ];
    expect(validateAdditives(rows)).toBeNull();
    expect(buildAdditivesPayload(rows)).toEqual([
      { kind: "StockLot", materialId: 7, solutionPreparationId: null, quantity: 5, unit: "Milliliter" },
      { kind: "PreparedSolution", materialId: null, solutionPreparationId: 3, quantity: 12.5, unit: "mL" }
    ]);
  });

  it("flags an incomplete, zero-quantity or duplicate row", () => {
    expect(validateAdditives([newAdditiveRow(1)])).toMatch(/choose the stock lot/);
    expect(validateAdditives([{ key: 1, kind: "StockLot", sourceId: 7, quantity: "0", unit: "g" }])).toMatch(/quantity/);
    expect(validateAdditives([{ key: 1, kind: "PreparedSolution", sourceId: 3, quantity: "1", unit: "" }])).toMatch(/unit/);
    const dup: AdditiveRow[] = [
      { key: 1, kind: "StockLot", sourceId: 7, quantity: "1", unit: "g" },
      { key: 2, kind: "StockLot", sourceId: 7, quantity: "2", unit: "g" }
    ];
    expect(validateAdditives(dup)).toMatch(/twice/);
  });
});

describe("MediaAdditivesSection", () => {
  const Harness = ({ onRows }: { onRows: (r: AdditiveRow[]) => void }) => {
    const [rows, setRows] = useState<AdditiveRow[]>([]);
    let k = 0;
    return (
      <PinnedLightTheme>
        <MediaAdditivesSection
          rows={rows}
          onChange={(r) => { setRows(r); onRows(r); }}
          stockLots={[lot]}
          solutions={[solution]}
          nextKey={() => ++k}
        />
      </PinnedLightTheme>
    );
  };

  it("adds a row, picks a stock lot (unit follows the lot) and removes it", async () => {
    const onRows = vi.fn();
    render(<Harness onRows={onRows} />);
    expect(screen.queryByLabelText("Additive 1 quantity")).toBeNull();

    await userEvent.click(screen.getByRole("button", { name: /add additive/i }));
    await userEvent.click(screen.getByRole("combobox", { name: "Additive 1 stock lot" }));
    await userEvent.click(within(screen.getByRole("listbox")).getByText(/Polysorbate 80/));
    await userEvent.type(screen.getByLabelText("Additive 1 quantity"), "5");

    const last = onRows.mock.calls[onRows.mock.calls.length - 1][0] as AdditiveRow[];
    expect(buildAdditivesPayload(last)).toEqual([
      { kind: "StockLot", materialId: 7, solutionPreparationId: null, quantity: 5, unit: "Milliliter" }
    ]);

    await userEvent.click(screen.getByRole("button", { name: "Remove additive 1" }));
    expect(screen.queryByLabelText("Additive 1 quantity")).toBeNull();
  });

  it("switches a row to a prepared solution with an editable unit", async () => {
    const onRows = vi.fn();
    render(<Harness onRows={onRows} />);
    await userEvent.click(screen.getByRole("button", { name: /add additive/i }));
    await userEvent.click(screen.getByRole("button", { name: "Prepared solution" }));
    await userEvent.click(screen.getByRole("combobox", { name: "Additive 1 prepared solution" }));
    await userEvent.click(within(screen.getByRole("listbox")).getByText(/NT-0001\/26/));
    await userEvent.type(screen.getByLabelText("Additive 1 quantity"), "10");

    const last = onRows.mock.calls[onRows.mock.calls.length - 1][0] as AdditiveRow[];
    expect(buildAdditivesPayload(last)).toEqual([
      { kind: "PreparedSolution", materialId: null, solutionPreparationId: 3, quantity: 10, unit: "mL" }
    ]);
  });
});
