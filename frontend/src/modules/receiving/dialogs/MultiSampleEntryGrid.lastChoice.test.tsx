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
  items: [{ id: 10, name: "Paracetamol 500mg" }],
  waterPoints: [],
  waterDepartments: [],
  departments: [],
  machines: [],
  causes: [{ id: 1, name: "Routine" }],
  samplers: [{ id: 1, name: "Analyst Alice" }],
  productionStages: [{ id: 1, name: "Bulk" }]
};

// Mirrors NewSampleDialog: the "from your last entry" flag on row 1 is cleared the
// moment the user edits that field, which used to remount the input mid-typing.
function Harness() {
  const [rows, setRows] = useState<ReceiveRowItem[]>([makeRow({ sampledBy: "Previous Sampler" })]);
  const [sampledByDefault, setSampledByDefault] = useState(true);
  return (
    <PinnedLightTheme>
      <MemoryRouter>
        <MultiSampleEntryGrid
          category="product"
          rows={rows}
          masterData={masterData}
          labMode={{ kind: "fixed", sectionId: 2, code: "FP" }}
          onChangeRow={(index, field, value) => {
            if (field === "sampledBy") setSampledByDefault(false);
            setRows((prev) => prev.map((r, i) => (i === index ? { ...r, [field]: value } : r)));
          }}
          onAddRow={() => undefined}
          onDeleteRow={() => undefined}
          onSave={() => undefined}
          firstRowDefaults={{ sampledBy: sampledByDefault }}
        />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

afterEach(cleanup);

describe("MultiSampleEntryGrid - pre-filled Sampled by cell", () => {
  it("keeps every typed character and Tab moves to the next column", async () => {
    const user = userEvent.setup();
    render(<Harness />);

    const sampledBy = screen.getByLabelText("Sample 1 sampled by") as HTMLInputElement;
    await user.click(sampledBy);
    await user.clear(sampledBy);
    await user.keyboard("QA");
    const after = screen.getByLabelText("Sample 1 sampled by") as HTMLInputElement;
    expect(after.value).toBe("QA");
    expect(document.activeElement).toBe(after);

    await user.tab();
    const batch = screen.getByLabelText("Sample 1 batch number") as HTMLInputElement;
    expect(document.activeElement).toBe(batch);
    await user.keyboard("B123");
    expect(batch.value).toBe("B123");
    expect((screen.getByLabelText("Sample 1 sampled by") as HTMLInputElement).value).toBe("QA");
  });
});
