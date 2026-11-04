import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { StandardEntryForm } from "./StandardEntryForm";
import type { HplcSstAnalyteDto, SaveSstAnalyteInput } from "../types";
import type { HplcMethodAnalyteResponse } from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";

afterEach(cleanup);

const mockAnalyte: HplcSstAnalyteDto = {
  id: 1,
  hplcMethodAnalyteId: 10,
  analyteName: "Test Analyte",
  standardEntryId: 5,
  injections: [
    { injectionNo: 1, response: 1000, internalStandardResponse: 500 },
    { injectionNo: 2, response: 1010, internalStandardResponse: 505 }
  ],
  passed: true
};

const mockMethodAnalyte: HplcMethodAnalyteResponse = {
  id: 10,
  displayOrder: 1,
  name: "Test Analyte",
  standardEntryId: 5,
  standardEntryCode: "STD-01",
  standardInjections: 2,
  theoreticalWeightStdMg: 50,
  theoreticalWeightTestMg: 50
};

const mockFormValue: SaveSstAnalyteInput = {
  hplcSstAnalyteId: 1,
  standardMaterialId: 12,
  standardWeightMg: 50.2,
  responses: [1000, 1010],
  internalStandardResponses: [500, 505]
};

describe("StandardEntryForm", () => {
  it("renders the IS inputs when hasInternalStandard is true", () => {
    render(
      <PinnedLightTheme>
        <StandardEntryForm
          analyte={mockAnalyte}
          methodAnalyte={mockMethodAnalyte}
          standardLots={[]}
          formValue={mockFormValue}
          onChange={vi.fn()}
          hasInternalStandard={true}
          internalStandardName="n-Hexane (IS)"
        />
      </PinnedLightTheme>
    );

    expect(screen.getAllByLabelText(/Analyte area/i).length).toBe(2);
    expect(screen.getAllByLabelText(/IS area/i).length).toBe(2);
    expect(screen.getByLabelText("Injection 1 IS area")).toBeTruthy();
    expect(screen.getByLabelText("Injection 2 IS area")).toBeTruthy();
  });

  it("hides the IS inputs when hasInternalStandard is false", () => {
    render(
      <PinnedLightTheme>
        <StandardEntryForm
          analyte={mockAnalyte}
          methodAnalyte={mockMethodAnalyte}
          standardLots={[]}
          formValue={mockFormValue}
          onChange={vi.fn()}
          hasInternalStandard={false}
          internalStandardName={null}
        />
      </PinnedLightTheme>
    );

    expect(screen.queryByLabelText(/IS area/i)).toBeNull();
  });
});

describe("StandardEntryForm - standard weight", () => {
  const renderForm = (standardIsWeighed?: boolean) =>
    render(
      <PinnedLightTheme>
        <StandardEntryForm
          analyte={mockAnalyte}
          methodAnalyte={mockMethodAnalyte}
          standardLots={[]}
          formValue={{ ...mockFormValue, standardWeightMg: 0 }}
          onChange={vi.fn()}
          showMissing
          standardIsWeighed={standardIsWeighed}
        />
      </PinnedLightTheme>
    );

  it("requires the actual standard weight by default (assay)", () => {
    renderForm();
    expect(screen.getByLabelText(/Actual Std Weight/i).hasAttribute("required")).toBe(true);
    expect(screen.getByLabelText(/Actual Std Weight/i).getAttribute("aria-invalid")).toBe("true");
  });

  it("does not demand a weight when the standard is not weighed (related substances)", () => {
    renderForm(false);
    expect(screen.getByLabelText(/Actual Std Weight/i).hasAttribute("required")).toBe(false);
    expect(screen.getByLabelText(/Actual Std Weight/i).getAttribute("aria-invalid")).toBe("false");
  });
});
