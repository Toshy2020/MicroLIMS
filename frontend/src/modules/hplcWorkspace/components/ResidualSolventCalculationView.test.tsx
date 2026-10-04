import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import {
  ResidualSolventCalculationView,
  type ResidualSolventCalculationData
} from "./ResidualSolventCalculationView";

afterEach(cleanup);

const mockRsCalc: ResidualSolventCalculationData = {
  analyte: "Methanol",
  hplcMethodAnalyteId: 101,
  quantity: "ResidualSolventPpm",
  basis: "Mean",
  standardConcentrationUgPerMl: 50,
  sampleSolutionVolumeMl: 10,
  standardMeanResponse: 24500,
  replicates: [
    {
      replicateNo: 1,
      sampleWeightMg: 100.2,
      response: 12250,
      ppm: 2495.01,
      ppmDisplay: "2495 ppm"
    },
    {
      replicateNo: 2,
      sampleWeightMg: 100.5,
      response: 12300,
      ppm: 2497.51,
      ppmDisplay: "2498 ppm"
    }
  ],
  reportedValue: 2496,
  notDetected: false,
  runCode: "GC-2026-001",
  sstCode: "SST-001",
  hplcMethodId: 12
};

describe("ResidualSolventCalculationView", () => {
  it("renders calibration parameters, formula, and replicate data", () => {
    render(
      <PinnedLightTheme>
        <ResidualSolventCalculationView
          calc={mockRsCalc}
          specLimit="NMT 3000 ppm"
          status="Passed"
          reportsSection={<div data-testid="test-reports">Evidence Reports Mock</div>}
        />
      </PinnedLightTheme>
    );

    // Title and metadata
    expect(screen.getByText(/Residual Solvent Review · Methanol/i)).toBeTruthy();
    expect(screen.getAllByText(/GC-2026-001/).length).toBeGreaterThan(0);
    expect(screen.getByText(/SST-001/)).toBeTruthy();

    // Standard calibration & formula
    expect(screen.getByText("50 µg/mL")).toBeTruthy();
    expect(screen.getByText("10 mL")).toBeTruthy();
    expect(screen.getByText("24500")).toBeTruthy();
    expect(
      screen.getByText(/Formula: ppm = \(C_std × V \/ W\) × \(r_u \/ r̄_std\)/i)
    ).toBeTruthy();

    // Replicate rows
    expect(screen.getByText("#1")).toBeTruthy();
    expect(screen.getByText("100.2")).toBeTruthy();
    expect(screen.getByText("12250")).toBeTruthy();
    expect(screen.getByText("2495 ppm")).toBeTruthy();

    expect(screen.getByText("#2")).toBeTruthy();
    expect(screen.getByText("100.5")).toBeTruthy();
    expect(screen.getByText("12300")).toBeTruthy();
    expect(screen.getByText("2498 ppm")).toBeTruthy();

    // Reported result, spec limit, and reports slot
    expect(screen.getByText(/2496 ppm/)).toBeTruthy();
    expect(screen.getByText(/\(Spec: NMT 3000 ppm\)/i)).toBeTruthy();
    expect(screen.getByTestId("test-reports")).toBeTruthy();
  });

  it("displays 'Not detected' when notDetected flag is true", () => {
    const notDetectedCalc: ResidualSolventCalculationData = {
      ...mockRsCalc,
      notDetected: true,
      reportedValue: 0,
      replicates: [
        {
          replicateNo: 1,
          sampleWeightMg: 100.1,
          response: 0,
          ppm: 0,
          ppmDisplay: "Not detected"
        }
      ]
    };

    render(
      <PinnedLightTheme>
        <ResidualSolventCalculationView calc={notDetectedCalc} />
      </PinnedLightTheme>
    );

    // Both replicate row and reported result say "Not detected"
    const notDetectedElements = screen.getAllByText("Not detected");
    expect(notDetectedElements.length).toBe(2);
  });
});
