import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { UvReviewPanel, isUvCalculation } from "./UvReviewPanel";
import type { ParameterResultDetail } from "../types/sampleSummaryTypes";

afterEach(() => {
  cleanup();
});

const baseParameter: ParameterResultDetail = {
  id: 1,
  specificationId: 10,
  parameterName: "Riboflavin 5-phosphate",
  reportedValue: 78.63,
  reportedDisplay: "78.63 %",
  unit: "%",
  specLimit: "73.0 - 79.0 %",
  resultBasis: "PercentDriedBasis",
  comparisonStatus: "WithinLimits",
  overRange: false,
  belowLoq: false,
  calculationJson: null,
  stageReached: 1,
  readings: []
};

describe("UvReviewPanel", () => {
  it("shows mode label, volume, A1%, wavelength, as-is value and moisture", () => {
    const calc = {
      engineVersion: "uv-1",
      quantity: "Uv",
      mode: "SpecificAbsorbanceAssay",
      wavelengthNm: 444,
      totalVolumeMl: 5000,
      specificAbsorbance: 328,
      standard: null,
      referenceAbsorbance: null,
      resultBasis: "PercentDriedBasis",
      asIsValue: 74.70,
      moisturePercent: 5.0,
      replicates: [
        { replicateNo: 1, weightG: 0.1, absorbance: 0.49, value: 74.70 }
      ],
      reportedValue: 78.63
    };

    const param: ParameterResultDetail = {
      ...baseParameter,
      calculationJson: JSON.stringify(calc)
    };

    const { container } = render(
      <PinnedLightTheme>
        <UvReviewPanel parameter={param} testOrderId={101} />
      </PinnedLightTheme>
    );

    // Mode label
    expect(screen.getAllByText(/Specific Absorbance Assay/i).length).toBeGreaterThan(0);
    // Volume
    expect(screen.getByText(/5000/)).toBeTruthy();
    // A1%
    expect(screen.getByText(/328/)).toBeTruthy();
    // Wavelength
    expect(screen.getByText(/444 nm/i)).toBeTruthy();
    // As-is value
    expect(screen.getAllByText(/74\.70/).length).toBeGreaterThan(0);
    // Moisture
    expect(screen.getByText(/5(\.0)?\s*%/)).toBeTruthy();

    // No text contains "undefined"
    expect(container.textContent).not.toContain("undefined");
  });

  it("shows the standard lot for StandardComparison", () => {
    const calc = {
      engineVersion: "uv-1",
      quantity: "Uv",
      mode: "StandardComparison",
      wavelengthNm: 275,
      totalVolumeMl: 100,
      specificAbsorbance: null,
      standard: {
        lotId: 10,
        code: "STD-LOT-999",
        purityPercent: 99.8,
        moisturePercent: 0.1,
        weightG: 0.1,
        absorbance: 0.5,
        volumeMl: 100
      },
      referenceAbsorbance: null,
      resultBasis: "PercentAsIs",
      asIsValue: 99.8,
      moisturePercent: null,
      replicates: [
        { replicateNo: 1, weightG: 0.1, absorbance: 0.5, value: 99.8 }
      ],
      reportedValue: 99.8
    };

    const param: ParameterResultDetail = {
      ...baseParameter,
      parameterName: "Standard Comparison Assay",
      calculationJson: JSON.stringify(calc)
    };

    const { container } = render(
      <PinnedLightTheme>
        <UvReviewPanel parameter={param} testOrderId={102} />
      </PinnedLightTheme>
    );

    expect(screen.getAllByText(/Standard Comparison/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/STD-LOT-999/)).toBeTruthy();
    expect(container.textContent).not.toContain("undefined");
  });

  it("shows the reference for AbsorbanceLimit", () => {
    const calc = {
      engineVersion: "uv-1",
      quantity: "Uv",
      mode: "AbsorbanceLimit",
      wavelengthNm: 700,
      totalVolumeMl: null,
      specificAbsorbance: null,
      standard: null,
      referenceAbsorbance: 0.150,
      resultBasis: null,
      asIsValue: 0.120,
      moisturePercent: null,
      replicates: [
        { replicateNo: 1, weightG: null, absorbance: 0.120, value: 0.120 }
      ],
      reportedValue: 0.120
    };

    const param: ParameterResultDetail = {
      ...baseParameter,
      parameterName: "Inorganic phosphate",
      unit: "AU",
      reportedDisplay: "0.120 AU",
      calculationJson: JSON.stringify(calc)
    };

    const { container } = render(
      <PinnedLightTheme>
        <UvReviewPanel parameter={param} testOrderId={103} />
      </PinnedLightTheme>
    );

    expect(screen.getByText(/Absorbance Limit/i)).toBeTruthy();
    expect(screen.getByText(/0\.150/)).toBeTruthy();
    expect(container.textContent).not.toContain("undefined");
  });

  it("shows neither Conforms nor Does not conform and hides As-is Value in limit mode", () => {
    const calc = {
      engineVersion: "uv-1",
      quantity: "Uv",
      mode: "AbsorbanceLimit",
      wavelengthNm: 700,
      totalVolumeMl: null,
      specificAbsorbance: null,
      standard: null,
      referenceAbsorbance: 0.150,
      resultBasis: null,
      asIsValue: 0.120,
      moisturePercent: null,
      replicates: [
        { replicateNo: 1, weightG: null, absorbance: 0.120, value: 0.120 }
      ],
      reportedValue: 0.120
    };

    const param: ParameterResultDetail = {
      ...baseParameter,
      parameterName: "Inorganic phosphate",
      unit: "AU",
      reportedDisplay: "0.120 AU",
      calculationJson: JSON.stringify(calc)
    };

    const { container } = render(
      <PinnedLightTheme>
        <UvReviewPanel parameter={param} testOrderId={103} />
      </PinnedLightTheme>
    );

    expect(container.textContent).not.toContain("Conforms");
    expect(container.textContent).not.toContain("Does not conform");
    expect(screen.queryByText(/As-is Value/i)).toBeNull();
  });

  it("shows '—' for a null reference in limit mode", () => {
    const calc = {
      engineVersion: "uv-1",
      quantity: "Uv",
      mode: "AbsorbanceLimit",
      wavelengthNm: 700,
      totalVolumeMl: null,
      specificAbsorbance: null,
      standard: null,
      referenceAbsorbance: null,
      resultBasis: null,
      asIsValue: 0.120,
      moisturePercent: null,
      replicates: [
        { replicateNo: 1, weightG: null, absorbance: 0.120, value: 0.120 }
      ],
      reportedValue: 0.120
    };

    const param: ParameterResultDetail = {
      ...baseParameter,
      parameterName: "Inorganic phosphate",
      unit: "AU",
      reportedDisplay: "0.120 AU",
      calculationJson: JSON.stringify(calc)
    };

    render(
      <PinnedLightTheme>
        <UvReviewPanel parameter={param} testOrderId={104} />
      </PinnedLightTheme>
    );

    expect(screen.getByText(/Reference Absorbance/i)).toBeTruthy();
    expect(screen.getByText("—")).toBeTruthy();
  });

  it("isUvCalculation returns false on malformed JSON instead of string matching", () => {
    expect(isUvCalculation('{"quantity": "Uv", malformed json')).toBe(false);
  });
});
