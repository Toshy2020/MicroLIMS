import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ReceiveRowItem } from "../types/receivingTypes";
import { MultiSampleEntryGrid } from "./MultiSampleEntryGrid";

vi.mock("../services/ReceiveService", () => ({
  ReceiveService: {
    receiptLabs: vi.fn().mockResolvedValue([]),
    itemGrades: vi.fn().mockResolvedValue([])
  }
}));

function makeRow(over: Partial<ReceiveRowItem> = {}): ReceiveRowItem {
  return {
    id: Math.random().toString(36).slice(2, 9),
    itemId: "",
    causeOfTestingId: "",
    sampleQuantity: "",
    sampledBy: "",
    batchNumber: "",
    controlNumber: "",
    mfgDate: "",
    expDate: "",
    errors: {},
    ...over
  };
}

const masterData = {
  items: [
    { id: 10, name: "Paracetamol 500mg" },
    { id: 11, name: "Ibuprofen 400mg" }
  ],
  waterPoints: [],
  waterDepartments: [],
  departments: [],
  machines: [],
  causes: [
    { id: 1, name: "Routine" },
    { id: 2, name: "Stability" }
  ],
  samplers: [{ id: 1, name: "Analyst Alice" }],
  productionStages: [{ id: 1, name: "Bulk" }]
};

function Harness({
  initialRows,
  category = "product",
  onSave = vi.fn()
}: {
  initialRows: ReceiveRowItem[];
  category?: "product" | "em";
  onSave?: () => void;
}) {
  const [rows, setRows] = useState<ReceiveRowItem[]>(initialRows);

  const onChangeRow = <K extends keyof ReceiveRowItem>(
    index: number,
    field: K,
    value: ReceiveRowItem[K]
  ) => {
    setRows((prev) => {
      const next = [...prev];
      const row = { ...next[index], [field]: value };
      if (row.errors && row.errors[field]) {
        const nextErrors = { ...row.errors };
        delete nextErrors[field];
        row.errors = nextErrors;
      }
      next[index] = row;
      return next;
    });
  };

  return (
    <PinnedLightTheme>
      <MemoryRouter>
        <MultiSampleEntryGrid
          category={category}
          rows={rows}
          masterData={masterData}
          labMode={{ kind: "fixed", sectionId: 2, code: "FP" }}
          onChangeRow={onChangeRow}
          onAddRow={() => setRows((prev) => [...prev, makeRow()])}
          onDeleteRow={(idx) => setRows((prev) => prev.filter((_, i) => i !== idx))}
          onSave={onSave}
        />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

afterEach(cleanup);

describe("MultiSampleEntryGrid — keyboard navigation and Ctrl+D", () => {
  it("Enter and ArrowDown move down, ArrowUp moves up, ArrowLeft/Right move across cells, Ctrl+Enter calls onSave", async () => {
    const user = userEvent.setup();
    const onSave = vi.fn();
    render(<Harness initialRows={[makeRow(), makeRow()]} onSave={onSave} />);

    const qty1 = screen.getByLabelText("Sample 1 quantity");
    const qty2 = screen.getByLabelText("Sample 2 quantity");
    const batch1 = screen.getByLabelText("Sample 1 batch number");

    await user.click(qty1);
    expect(document.activeElement).toBe(qty1);

    await user.keyboard("{Enter}");
    expect(document.activeElement).toBe(qty2);

    await user.keyboard("{ArrowUp}");
    expect(document.activeElement).toBe(qty1);

    await user.keyboard("{ArrowDown}");
    expect(document.activeElement).toBe(qty2);

    await user.click(batch1);
    expect(document.activeElement).toBe(batch1);

    await user.keyboard("{Control>}{Enter}{/Control}");
    expect(onSave).toHaveBeenCalledTimes(1);
  });

  it("Ctrl+D copies the cell above including choice columns and is ignored on row 0", async () => {
    const user = userEvent.setup();
    render(
      <Harness
        initialRows={[
          makeRow({
            itemId: 10,
            productionStage: "Bulk",
            causeOfTestingId: 1,
            sampleQuantity: "50g",
            batchNumber: "B-100"
          }),
          makeRow()
        ]}
      />
    );

    // Ignored on row 0
    const batch1 = screen.getByLabelText("Sample 1 batch number") as HTMLInputElement;
    await user.click(batch1);
    await user.keyboard("{Control>}d{/Control}");
    expect(batch1.value).toBe("B-100");

    // Text column copy-down on row 1
    const batch2 = screen.getByLabelText("Sample 2 batch number") as HTMLInputElement;
    await user.click(batch2);
    await user.keyboard("{Control>}d{/Control}");
    expect(batch2.value).toBe("B-100");

    // Choice column copy-down on row 1 (Item)
    const item2 = screen.getByLabelText("Sample 2 item");
    await user.click(item2);
    // Close select menu if opened, or focus via tab/arrow
    await user.keyboard("{Escape}");
    item2.focus();
    await user.keyboard("{Control>}d{/Control}");
    expect(item2.textContent).toContain("Paracetamol 500mg");

    // Choice column copy-down on row 1 (Cause of Testing)
    const cause2 = screen.getByLabelText("Sample 2 cause of testing");
    cause2.focus();
    await user.keyboard("{Control>}d{/Control}");
    expect(cause2.textContent).toContain("Routine");
  });

  it("disables Add Another Sample at 100 rows with the reason", () => {
    const hundredRows = Array.from({ length: 100 }, (_, i) => makeRow({ id: `r-${i}` }));
    render(<Harness initialRows={hundredRows} category="em" />);

    const addBtn = screen.getByRole("button", { name: /Add Another Sample/i });
    expect((addBtn as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByText(/Maximum 100 rows/i)).toBeTruthy();
    const describedBy = addBtn.getAttribute("aria-describedby");
    expect(describedBy).toBeTruthy();
    expect(document.getElementById(describedBy!)?.textContent).toMatch(/100 rows/i);
  });
});
