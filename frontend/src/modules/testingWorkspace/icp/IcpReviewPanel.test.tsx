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
  element: "Pb",
  icpMethodElementId: 5,
  quantity: "IcpMgPerKg",
  amountUnit: "Gram",
  unitAmount: 1.0,
  conversionFactor: 1.0,
  labelClaim: null,
  labelClaimUnit: null,
  standardLevelsMgPerL: [0.01, 0.05, 0.1, 0.5, 1.0],
  replicates: [
    {
      replicateNo: 1,
      sampleAmount: 0.5012,
      volumeMl: 50.0,
      dilutionFactor: 1.0,
      solutionMgPerL: 0.0012,
      contentPerAmount: 0.1197,
      belowLoq: true
    },
    {
      replicateNo: 2,
      sampleAmount: 0.4998,
      volumeMl: 50.0,
      dilutionFactor: 1.0,
      solutionMgPerL: 0.0013,
      contentPerAmount: 0.1301,
      belowLoq: false
    }
  ],
  meanContentPerAmount: 0.1301,
  reportedValue: 0.13,
  display: "0.13 µg/g",
  runCode: "ICP-2026-004",
  calibrationCode: "CAL-Pb-002",
  calibrationConfirmedAt: "2026-10-04T10:30:00Z",
  correlationR: 0.999876,
  ccv: [
    {
      measuredMgPerL: 0.102,
      recoveryPercent: 102.0,
      passed: true,
      enteredAt: "2026-10-04T11:00:00Z"
    }
  ],
  icpMethodId: 3,
  icpRunId: 104,
  icpRunSampleId: 201
};

describe("IcpReviewPanel", () => {
  it("renders replicate rows, <LOQ flag, and quantity label for IcpMgPerKg", async () => {
    vi.spyOn(IcpReviewApi, "getTestOrderEvidence").mockResolvedValueOnce([
      {
        id: 99,
        icpRunId: 104,
        context: "Sample",
        kind: "SampleReport",
        fileName: "Pb-run-report.pdf",
        contentType: "application/pdf",
        uploadedByUserId: 1,
        uploadedByUserName: "Analyst Alice",
        uploadedAt: "2026-10-04T11:15:00Z",
        isCurrent: true
      }
    ]);

    const param: ParameterResultDetail = {
      ...baseParameter,
      calculationJson: JSON.stringify(mockIcpCalc)
    };

    render(
      <PinnedLightTheme>
        <IcpReviewPanel parameter={param} testOrderId={123} />
      </PinnedLightTheme>
    );

    // Title & Quantity Label
    expect(screen.getByText(/ICP Review · Pb/i)).toBeTruthy();
    expect(screen.getAllByText("Content (µg/g)").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("Unit: g")).toBeTruthy();

    // Run & Calibration Metadata
    expect(screen.getAllByText("ICP-2026-004").length).toBeGreaterThan(0);
    expect(screen.getAllByText(/CAL-Pb-002/).length).toBeGreaterThan(0);
    expect(screen.getByText("0.999876")).toBeTruthy();

    // CCV Reading
    expect(screen.getByText("102.0%")).toBeTruthy();
    expect(screen.getByText("Pass")).toBeTruthy();

    // Formula text
    expect(
      screen.getByText(/Formula: content = C × V × DF \/ W/i)
    ).toBeTruthy();

    // Replicate rows
    expect(screen.getByText("#1")).toBeTruthy();
    expect(screen.getByText("0.5012")).toBeTruthy();
    expect(screen.getAllByText("50").length).toBeGreaterThan(0);
    expect(screen.getByText("0.0012")).toBeTruthy();
    expect(screen.getByText("0.1197")).toBeTruthy();
    expect(screen.getByText("<LOQ")).toBeTruthy();

    expect(screen.getByText("#2")).toBeTruthy();
    expect(screen.getByText("0.4998")).toBeTruthy();
    expect(screen.getByText("0.0013")).toBeTruthy();
    expect(screen.getByText("0.1301")).toBeTruthy();

    // Mean content
    expect(screen.getByText(/Mean content:/i)).toBeTruthy();

    // Reported display and spec
    expect(screen.getByText("0.125 µg/g")).toBeTruthy();
    expect(screen.getByText(/\(Spec: NMT 0.5 µg\/g\)/i)).toBeTruthy();

    // Evidence file
    expect(await screen.findByText("Pb-run-report.pdf")).toBeTruthy();
  });

  it("renders quantity labels for IcpMgPerUnit and IcpPercentLabelClaim", () => {
    vi.spyOn(IcpReviewApi, "getTestOrderEvidence").mockResolvedValue([]);

    // Test IcpMgPerUnit
    const mgPerUnitCalc = {
      ...mockIcpCalc,
      quantity: "IcpMgPerUnit",
      amountUnit: "Gram"
    };
    const { unmount } = render(
      <PinnedLightTheme>
        <IcpReviewPanel
          parameter={{ ...baseParameter, calculationJson: JSON.stringify(mgPerUnitCalc) }}
          testOrderId={123}
        />
      </PinnedLightTheme>
    );
    expect(screen.getByText("Amount per unit")).toBeTruthy();
    unmount();

    // Test IcpPercentLabelClaim
    const percentCalc = {
      ...mockIcpCalc,
      quantity: "IcpPercentLabelClaim",
      amountUnit: "Milliliter"
    };
    render(
      <PinnedLightTheme>
        <IcpReviewPanel
          parameter={{ ...baseParameter, calculationJson: JSON.stringify(percentCalc) }}
          testOrderId={123}
        />
      </PinnedLightTheme>
    );
    expect(screen.getByText("% of label claim")).toBeTruthy();
    expect(screen.getByText("Unit: mL")).toBeTruthy();
  });

  it("handles bad JSON defensively and shows the stored display only", async () => {
    vi.spyOn(IcpReviewApi, "getTestOrderEvidence").mockResolvedValue([]);

    const badJsonParam: ParameterResultDetail = {
      ...baseParameter,
      parameterName: "Arsenic (As)",
      reportedDisplay: "0.045 µg/g",
      specLimit: "NMT 0.2 µg/g",
      comparisonStatus: "WithinLimits",
      calculationJson: "{invalid-json: broken payload"
    };

    render(
      <PinnedLightTheme>
        <IcpReviewPanel parameter={badJsonParam} testOrderId={456} />
      </PinnedLightTheme>
    );

    // Shows fallback header with parameter name
    expect(screen.getByText(/ICP Review · Arsenic \(As\)/i)).toBeTruthy();

    // Shows stored reported display
    expect(screen.getAllByText("0.045 µg/g").length).toBeGreaterThan(0);
    expect(screen.getByText(/\(Spec: NMT 0.2 µg\/g\)/i)).toBeTruthy();

    // Shows warning/info alert about payload
    expect(
      screen.getByText(/Detailed calculation payload was not found or is in an invalid format/i)
    ).toBeTruthy();

    // Does NOT render replicate table headers
    expect(screen.queryByText(/Sample Amount/i)).toBeNull();
  });
});
