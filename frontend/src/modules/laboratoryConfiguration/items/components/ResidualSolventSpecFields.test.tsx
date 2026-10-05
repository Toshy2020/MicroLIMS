import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent } from "@testing-library/react";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { ResidualSolventSpecFields } from "./ResidualSolventSpecFields";
import type { HplcMethodAnalyteResponse } from "../../masterDataSimple/services/HplcMethodService";

afterEach(cleanup);

const mockAnalytes: HplcMethodAnalyteResponse[] = [
  {
    id: 1,
    displayOrder: 1,
    name: "Methanol",
    standardEntryId: 101,
    standardEntryCode: "STD-METH",
    standardConcentrationUgPerMl: 50,
    theoreticalWeightStdMg: 0,
    theoreticalWeightTestMg: 0,
    standardInjections: 1
  },
  {
    id: 2,
    displayOrder: 2,
    name: "Acetone",
    standardEntryId: 102,
    standardEntryCode: "STD-ACT",
    standardConcentrationUgPerMl: 100,
    theoreticalWeightStdMg: 0,
    theoreticalWeightTestMg: 0,
    standardInjections: 1
  }
];

describe("ResidualSolventSpecFields", () => {
  it("renders solvent select and fixed ppm basis", () => {
    render(
      <PinnedLightTheme>
        <ResidualSolventSpecFields
          methodAnalytes={mockAnalytes}
          loadingMethodAnalytes={false}
          selectedAnalyteId={1}
          onAnalyteChange={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Residual Solvent (GC) Specification")).toBeTruthy();
    expect(screen.getByDisplayValue("ppm")).toBeTruthy();
    expect(screen.getByText("Fixed basis for residual solvents")).toBeTruthy();
  });

  it("calls onAnalyteChange when solvent selection changes", async () => {
    const onAnalyteChange = vi.fn();
    render(
      <PinnedLightTheme>
        <ResidualSolventSpecFields
          methodAnalytes={mockAnalytes}
          loadingMethodAnalytes={false}
          selectedAnalyteId=""
          onAnalyteChange={onAnalyteChange}
        />
      </PinnedLightTheme>
    );

    // Click the select to open options
    const combobox = screen.getByRole("combobox");
    fireEvent.mouseDown(combobox);

    const option = await screen.findByRole("option", { name: /Acetone/i });
    fireEvent.click(option);

    expect(onAnalyteChange).toHaveBeenCalledWith(2, mockAnalytes[1]);
  });
});
