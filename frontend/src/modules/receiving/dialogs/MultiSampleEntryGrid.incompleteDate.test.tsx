import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import type { ReceiveRowItem } from "../types/receivingTypes";
import { MultiSampleEntryGrid } from "./MultiSampleEntryGrid";

vi.mock("../services/ReceiveService", () => ({
  ReceiveService: {
    receiptLabs: vi.fn().mockResolvedValue([]),
    itemGrades: vi.fn().mockResolvedValue([])
  }
}));

const row: ReceiveRowItem = {
  id: "r1", itemId: "", causeOfTestingId: "", sampleQuantity: "", sampledBy: "",
  batchNumber: "", controlNumber: "", mfgDate: "", expDate: "", errors: {}
};

const masterData = {
  items: [{ id: 10, name: "Paracetamol 500mg" }],
  waterPoints: [], waterDepartments: [], departments: [], machines: [],
  causes: [{ id: 1, name: "Routine" }],
  samplers: [{ id: 1, name: "Analyst Alice" }],
  productionStages: [{ id: 1, name: "Bulk" }]
};

afterEach(cleanup);

describe("MultiSampleEntryGrid incomplete date", () => {
  it("flags the row when Mfg Date is left half-typed and does not report a date", async () => {
    const user = userEvent.setup();
    const onChangeRow = vi.fn();
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <MultiSampleEntryGrid
            category="product"
            rows={[row]}
            masterData={masterData}
            labMode={{ kind: "fixed", sectionId: 2, code: "FP" }}
            onChangeRow={onChangeRow}
            onAddRow={() => {}}
            onDeleteRow={() => {}}
            onSave={() => {}}
          />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const mfg = screen.getByRole("group", { name: /Sample 1 manufacturing date/i });
    const month = mfg.querySelector('[role="spinbutton"][aria-label="Month"]') as HTMLElement;
    await user.click(month);
    await user.keyboard("03");
    await user.click(screen.getByLabelText("Sample 1 batch number"));

    expect(onChangeRow).toHaveBeenCalledWith(0, "mfgDateIncomplete", true);
    expect(onChangeRow).not.toHaveBeenCalledWith(0, "mfgDate", expect.stringMatching(/\d/));
  });
});
