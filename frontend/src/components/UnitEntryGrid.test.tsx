import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { UnitEntryGrid, type UnitEntryGridColumn } from "./UnitEntryGrid";

const COLUMNS: UnitEntryGridColumn[] = [
  { key: "gross", label: "Gross", unit: "mg" },
  { key: "tare", label: "Tare", unit: "mg", readOnly: true },
  { key: "net", label: "Net", unit: "mg" }
];

function ControlledUnitEntryGrid({ initial }: { initial?: Record<string, string>[] }) {
  const [values, setValues] = useState<Record<string, string>[]>(
    () => initial ?? [{ gross: "10", net: "5" }, { gross: "", net: "" }, { gross: "", net: "" }]
  );
  return (
    <PinnedLightTheme>
      <MemoryRouter>
        <UnitEntryGrid
          rowCount={3}
          columns={COLUMNS}
          values={values}
          onChange={setValues}
        />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

describe("UnitEntryGrid", () => {
  it("UnitEntryGrid keeps its existing Enter/Up/Down behaviour and paste, and gains Left/Right and Ctrl+D", async () => {
    const user = userEvent.setup();
    render(<ControlledUnitEntryGrid />);

    const u1Gross = screen.getByLabelText("Unit 1 Gross") as HTMLInputElement;
    const u2Gross = screen.getByLabelText("Unit 2 Gross") as HTMLInputElement;
    const u3Gross = screen.getByLabelText("Unit 3 Gross") as HTMLInputElement;
    const u1Net = screen.getByLabelText("Unit 1 Net") as HTMLInputElement;
    const u2Net = screen.getByLabelText("Unit 2 Net") as HTMLInputElement;

    // Enter / Down / Up
    await user.click(u1Gross);
    await user.keyboard("{Enter}");
    expect(document.activeElement).toBe(u2Gross);

    await user.keyboard("{ArrowDown}");
    expect(document.activeElement).toBe(u3Gross);

    await user.keyboard("{ArrowUp}");
    expect(document.activeElement).toBe(u2Gross);

    // Ctrl+D copies from row above
    await user.keyboard("{Control>}d{/Control}");
    expect(u2Gross.value).toBe("10");

    // Ctrl+D on row 0 is ignored
    await user.click(u1Gross);
    await user.keyboard("{Control>}d{/Control}");
    expect(u1Gross.value).toBe("10");

    // Left / Right skips readOnly Tare column
    // Caret at end of "10" in u1Gross -> ArrowRight moves to u1Net
    await user.keyboard("{ArrowRight}");
    expect(document.activeElement).toBe(u1Net);

    // Caret at start of u1Net -> ArrowLeft skips Tare and moves back to u1Gross
    u1Net.setSelectionRange(0, 0);
    await user.keyboard("{ArrowLeft}");
    expect(document.activeElement).toBe(u1Gross);

    // Paste multi-cell block skips readOnly column
    await user.click(u2Gross);
    await user.paste("20\t999\t12\n30\t888\t15");
    expect(u2Gross.value).toBe("20");
    expect(u2Net.value).toBe("12");
    expect(u3Gross.value).toBe("30");
    expect((screen.getByLabelText("Unit 3 Net") as HTMLInputElement).value).toBe("15");
    // readOnly Tare column is untouched
    expect((screen.getByLabelText("Unit 2 Tare") as HTMLInputElement).value).toBe("");
  });

  it("Enter does not submit a surrounding form", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <form onSubmit={(e) => { e.preventDefault(); onSubmit(); }}>
            <UnitEntryGrid
              rowCount={2}
              columns={[{ key: "v", label: "Value" }]}
              values={[{ v: "" }, { v: "" }]}
              onChange={() => {}}
            />
          </form>
        </MemoryRouter>
      </PinnedLightTheme>
    );

    await user.click(screen.getByLabelText("Unit 1 Value"));
    await user.keyboard("{Enter}");
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
