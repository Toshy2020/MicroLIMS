import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { IcpReviewPanel } from "./IcpReviewPanel";
import { IcpReviewApi } from "./icpReviewApi";
import type { ParameterResultDetail } from "../types/sampleSummaryTypes";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

const baseParameter: ParameterResultDetail = {
  id: 42,
  specificationId: 10,
  parameterName: "Lead (Pb)",
  reportedValue: 0.125,
  reportedDisplay: "0.125 µg/g",
  unit: "µg/g",
  specLimit: "NMT 0.5 µg/g",
  resultBasis: "MgPerKg",
  comparisonStatus: "WithinLimits",
  overRange: false,
  belowLoq: false,
  calculationJson: null,
  stageReached: 1,
  readings: []
};

const mockIcpCalc = {
  element: "Zn",
  icpTestElementId: 5,
  quantity: "IcpAssayPercent",
  mode: "Assay",
  sampleForm: "Solid",
  targetPpm: 10,
  theoreticalAmount: 0.5,
  specificGravity: null,
  actualWeightG: 0.55,
  resultPpm: 9.5,
  reportedValue: 86.4,
  display: "86.4 %",
  runCode: "ICP RUN 01/102026",
  calibrationCode: "ICP CAL 01/102026",
  calibrationConfirmedAt: "2026-10-04T10:30:00Z",
  correlationR: 0.999876,
  icpRunId: 104,
  icpRunSampleId: 201
};

describe("IcpReviewPanel", () => {
  it("shows the assay calculation inputs, the solid formula and the stored result", async () => {
    vi.spyOn(IcpReviewApi, "getTestOrderEvidence").mockResolvedValueOnce([
      {
        id: 99,
        icpRunId: 104,
        context: "Sample",
        kind: "SampleReport",
        fileName: "Zn-run-report.pdf",
        contentType: "application/pdf",
        uploadedByUserId: 1,
        uploadedByUserName: "Analyst Alice",
        uploadedAt: "2026-10-04T11:15:00Z",
        isCurrent: true
      }
    ]);

    const param: ParameterResultDetail = {
      ...baseParameter,
      parameterName: "Zn",
      reportedDisplay: "86.4 %",
      unit: "%",
      specLimit: "90-110",
      resultBasis: null,
      comparisonStatus: "OutOfSpecification",
      calculationJson: JSON.stringify(mockIcpCalc)
    };

    render(
      <PinnedLightTheme>
        <IcpReviewPanel parameter={param} testOrderId={123} />
      </PinnedLightTheme>
    );

    expect(screen.getByText(/ICP Review · Zn/i)).toBeTruthy();
    expect(screen.getAllByText("Assay (%)").length).toBeGreaterThanOrEqual(1);

    // Run & calibration metadata
    expect(screen.getAllByText("ICP RUN 01/102026").length).toBeGreaterThan(0);
    expect(screen.getAllByText(/ICP CAL 01\/102026/).length).toBeGreaterThan(0);
    expect(screen.getByText("0.999876")).toBeTruthy();

    // Calculation inputs
    expect(screen.getByText("9.5 ppm")).toBeTruthy();
    expect(screen.getByText("10 ppm")).toBeTruthy();
    expect(screen.getByText("Th.Wt")).toBeTruthy();
    expect(screen.getByText("0.5 g")).toBeTruthy();
    expect(screen.getByText("Act.Wt")).toBeTruthy();
    expect(screen.getByText("0.55 g")).toBeTruthy();
    expect(screen.queryByText("Sp.gr")).toBeNull();
    expect(screen.getByText("Formula: Assay % = result × Th.Wt × 100 / (target × Act.Wt)")).toBeTruthy();

    // Reported display and spec
    expect(screen.getByText("86.4 %")).toBeTruthy();
    expect(screen.getByText(/\(Spec: 90-110\)/i)).toBeTruthy();

    // Evidence file
    expect(await screen.findByText("Zn-run-report.pdf")).toBeTruthy();
  });

  it("shows volume and specific gravity for a liquid assay", () => {
    vi.spyOn(IcpReviewApi, "getTestOrderEvidence").mockResolvedValue([]);
    const liquid = { ...mockIcpCalc, sampleForm: "Liquid", theoreticalAmount: 10, specificGravity: 1.2, actualWeightG: 12 };

    render(
      <PinnedLightTheme>
        <IcpReviewPanel parameter={{ ...baseParameter, calculationJson: JSON.stringify(liquid) }} testOrderId={123} />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Th.Vol")).toBeTruthy();
    expect(screen.getByText("10 mL")).toBeTruthy();
    expect(screen.getByText("Sp.gr")).toBeTruthy();
    expect(screen.getByText("1.2")).toBeTruthy();
    expect(screen.getByText("Formula: Assay % = result × Th.Vol × Sp.gr × 100 / (target × Act.Wt)")).toBeTruthy();
  });

  it("shows only the ppm result and the impurity rule for an impurity test", () => {
    vi.spyOn(IcpReviewApi, "getTestOrderEvidence").mockResolvedValue([]);
    const impurity = {
      ...mockIcpCalc,
      element: "Pb",
      quantity: "IcpImpurityPpm",
      mode: "Impurity",
      sampleForm: null,
      targetPpm: null,
      theoreticalAmount: null,
      actualWeightG: null,
      resultPpm: 0.125,
      display: "0.125 ppm"
    };

    render(
      <PinnedLightTheme>
        <IcpReviewPanel
          parameter={{ ...baseParameter, calculationJson: JSON.stringify(impurity) }}
          testOrderId={123}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Impurity (ppm)")).toBeTruthy();
    expect(screen.getByText("0.125 ppm")).toBeTruthy();
    expect(screen.queryByText("Target")).toBeNull();
    expect(screen.queryByText("Act.Wt")).toBeNull();
    expect(screen.getByText(/judged against the item's not-more-than limit/i)).toBeTruthy();
  });

  it("handles bad JSON defensively and shows the stored display only", async () => {
    vi.spyOn(IcpReviewApi, "getTestOrderEvidence").mockResolvedValue([]);

    const badJsonParam: ParameterResultDetail = {
      ...baseParameter,
      parameterName: "Arsenic (As)",
      reportedDisplay: "0.045 ppm",
      specLimit: "NMT 0.2",
      comparisonStatus: "WithinLimits",
      calculationJson: "{invalid-json: broken payload"
    };

    render(
      <PinnedLightTheme>
        <IcpReviewPanel parameter={badJsonParam} testOrderId={456} />
      </PinnedLightTheme>
    );

    expect(screen.getByText(/ICP Review · Arsenic \(As\)/i)).toBeTruthy();
    expect(screen.getAllByText("0.045 ppm").length).toBeGreaterThan(0);
    expect(screen.getByText(/\(Spec: NMT 0.2\)/i)).toBeTruthy();
    expect(
      screen.getByText(/Detailed calculation payload was not found or is in an invalid format/i)
    ).toBeTruthy();
    expect(screen.queryByText(/Instrument result/i)).toBeNull();
  });
});
