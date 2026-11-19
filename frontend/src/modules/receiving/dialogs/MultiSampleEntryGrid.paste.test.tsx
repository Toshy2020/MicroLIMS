import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { MemoryRouter } from "react-router-dom";
import { dateFieldValue } from "../../../components/DateField.testUtils";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ReceiveRowItem } from "../types/receivingTypes";
import { MAX_RECEIVE_ROWS, MultiSampleEntryGrid } from "./MultiSampleEntryGrid";

vi.mock("../services/ReceiveService", () => ({
  ReceiveService: {
    receiptLabs: vi.fn().mockResolvedValue([]),
    itemGrades: vi.fn().mockResolvedValue([])
  }
}));

let nextId = 0;
function makeRow(over: Partial<ReceiveRowItem> = {}): ReceiveRowItem {
  nextId += 1;
  return {
    id: `row-${nextId}`,
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
  items: [{ id: 10, name: "Paracetamol 500mg" }],
  waterPoints: [],
  waterDepartments: [],
  departments: [],
  machines: [],
  causes: [{ id: 1, name: "Routine" }],
  samplers: [{ id: 1, name: "Analyst Alice" }],
  productionStages: [{ id: 1, name: "Bulk" }]
};

function Harness({ initialRows }: { initialRows: ReceiveRowItem[] }) {
  const [rows, setRows] = useState<ReceiveRowItem[]>(initialRows);
  const onChangeRow = <K extends keyof ReceiveRowItem>(index: number, field: K, value: ReceiveRowItem[K]) => {
    setRows((prev) => {
      const next = [...prev];
      next[index] = { ...next[index], [field]: value };
      return next;
    });
  };
  return (
    <PinnedLightTheme>
      <MemoryRouter>
        <MultiSampleEntryGrid
          category="product"
          rows={rows}
          masterData={masterData}
          labMode={{ kind: "fixed", sectionId: 2, code: "FP" }}
          onChangeRow={onChangeRow}
          onAddRow={() => setRows((prev) => (prev.length >= MAX_RECEIVE_ROWS ? prev : [...prev, makeRow()]))}
          onDeleteRow={(idx) => setRows((prev) => prev.filter((_, i) => i !== idx))}
        />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

afterEach(cleanup);

const accept = () => screen.getByRole("button", { name: "Accept" }) as HTMLButtonElement;
const preview = () => screen.findByRole("table", { name: /paste preview/i });
const field = (label: string) => screen.getByLabelText(label) as HTMLInputElement;

describe("MultiSampleEntryGrid paste", () => {
  it("a block fills rows from the focused cell's column and adds rows as needed", async () => {
    const user = userEvent.setup();
    render(<Harness initialRows={[makeRow(), makeRow()]} />);

    await user.click(field("Sample 2 quantity"));
    await user.paste("5 g\tAnalyst Bob\tB-100\n6 g\tAnalyst Cy\tB-101\n7 g\tAnalyst Di\tB-102\n");
    await preview();
    await user.click(accept());

    expect(field("Sample 1 quantity").value).toBe("");
    expect(field("Sample 2 quantity").value).toBe("5 g");
    expect(field("Sample 2 batch number").value).toBe("B-100");
    expect(field("Sample 2 sampled by").value).toBe("Analyst Bob");
    expect(field("Sample 3 quantity").value).toBe("6 g");
    expect(field("Sample 4 quantity").value).toBe("7 g");
    expect(field("Sample 4 sampled by").value).toBe("Analyst Di");
  });

  it("starts at the focused column, not at the first one", async () => {
    const user = userEvent.setup();
    render(<Harness initialRows={[makeRow()]} />);

    await user.click(field("Sample 1 batch number"));
    await user.paste("B-9\tC-9\n");
    await preview();
    await user.click(accept());

    expect(field("Sample 1 batch number").value).toBe("B-9");
    expect(field("Sample 1 control number").value).toBe("C-9");
    expect(field("Sample 1 quantity").value).toBe("");
  });

  it("reports a date format problem for the cell, blocks Accept, and accepts the fixed paste", async () => {
    const user = userEvent.setup();
    render(<Harness initialRows={[makeRow(), makeRow()]} />);

    await user.click(field("Sample 1 batch number"));
    await user.paste("B-1\tC-1\t31/02/2026\t2027-01-31\nB-2\tC-2\tsoon\t01/02/2027\n");
    const table = await preview();

    expect(accept().disabled).toBe(true);
    expect(within(table).getAllByText(/date/i, { selector: "td *, td" }).length).toBeGreaterThanOrEqual(2);
    expect(screen.getByRole("alert").textContent).toMatch(/2 problems/);
    expect(field("Sample 1 batch number").value).toBe("");

    await user.click(screen.getByRole("button", { name: "Cancel" }));
    await waitFor(() => expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull());
    await user.click(field("Sample 1 batch number"));
    await user.paste("B-1\tC-1\t28/02/2026\t2027-01-31\n");
    await preview();
    expect(accept().disabled).toBe(false);
    await user.click(accept());
    expect(dateFieldValue("Sample 1 manufacturing date")).toBe("2026-02-28");
    expect(dateFieldValue("Sample 1 expiry date")).toBe("2027-01-31");
  });

  it("lists the choice columns it skipped as not pasted", async () => {
    const user = userEvent.setup();
    render(<Harness initialRows={[makeRow()]} />);

    // Tab lands on the first control of the grid, the Item choice.
    await user.tab();
    await user.paste("5 g\tAnalyst Bob\n");
    await preview();

    const notPasted = screen.getByRole("list", { name: /not pasted/i });
    expect(within(notPasted).getByText(/cause of testing/i)).toBeTruthy();
    expect(within(notPasted).getByText(/^item$/i)).toBeTruthy();
    await user.click(accept());
    expect(field("Sample 1 quantity").value).toBe("5 g");
  });

  it("Cancel leaves the grid untouched", async () => {
    const user = userEvent.setup();
    render(<Harness initialRows={[makeRow()]} />);

    await user.click(field("Sample 1 quantity"));
    await user.paste("5 g\tAnalyst Bob\n6 g\tAnalyst Cy\n");
    await preview();
    await user.click(screen.getByRole("button", { name: "Cancel" }));
    await waitFor(() => expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull());

    expect(field("Sample 1 quantity").value).toBe("");
    expect(screen.queryByLabelText("Sample 2 quantity")).toBeNull();
  });

  it("refuses a paste that would take the batch past 100 rows", async () => {
    const user = userEvent.setup();
    render(<Harness initialRows={[makeRow()]} />);

    const block = Array.from({ length: 101 }, (_, i) => `${i} g\tAnalyst`).join("\n");
    await user.click(field("Sample 1 quantity"));
    await user.paste(block);
    await screen.findByRole("alert");

    expect(screen.getByRole("alert").textContent).toMatch(/101 rows/);
    expect(accept().disabled).toBe(true);
    expect(screen.queryByLabelText("Sample 2 quantity")).toBeNull();
  });

  it("a single value pasted into a cell is a normal paste", async () => {
    const user = userEvent.setup();
    render(<Harness initialRows={[makeRow()]} />);

    await user.click(field("Sample 1 batch number"));
    await user.paste("B-77");

    expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull();
    expect(field("Sample 1 batch number").value).toBe("B-77");
  });
});
