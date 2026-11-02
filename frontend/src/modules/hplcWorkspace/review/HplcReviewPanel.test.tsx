import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { HplcReviewPanel, hplcPanelResults } from "./HplcReviewPanel";
import { formatAnalysisConditions } from "../../testingWorkspace/utils/moistureRetentionHelpers";
import type { ParameterResultDetail } from "../../testingWorkspace/types/sampleSummaryTypes";

vi.mock("./HplcReportsSection", () => ({ HplcReportsSection: () => null }));

const rs = JSON.stringify({
  quantity: "RelatedSubstance", analyte: "Principal", runCode: "R1", sstCode: "S1", referenceSolutionPercent: 0.1, disregardLimitPercent: 0.05,
  replicates: [{ replicateNo: 1, referenceArea: 10000, peaks: [
    { label: "D", relativeRetention: 0.8, area: 12000, correctionFactor: 0.7, percent: 0.084, disregarded: false },
    { label: "Unspecified", relativeRetention: null, area: 3000, correctionFactor: 1, percent: 0.03, disregarded: true }
  ] }]
});
const isAssay = JSON.stringify({
  quantity: "AssayPercent", basis: "Mean", analyte: "Principal", runCode: "R2", sstCode: "S2", thWtStdMg: 50, thWtTestMg: 50, standardWeightMg: 50,
  standardPurityPercent: 99.8, standardMoisturePercent: 0.1, standardMeanRatio: 2, internalStandardName: "p-hydroxybenzoic acid", asIsValue: 98.0003, moisturePercent: 0.5,
  replicates: [{ replicateNo: 1, actualWeightMg: 50, response: 9829.5, internalStandardResponse: 5000, ratio: 1.9659, assayPercent: 98, assayPercentDisplay: "98.0 %" }]
});
const param = (calculationJson: string | null) =>
  ({ id: Math.random(), calculationJson, parameterName: "P", reportedDisplay: "1", comparisonStatus: "WithinLimits" }) as unknown as ParameterResultDetail;
const show = (json: string) =>
  render(
    <PinnedLightTheme>
      <HplcReviewPanel parameter={param(json)} testOrderId={1} />
    </PinnedLightTheme>
  );

describe("HplcReviewPanel", () => {
  afterEach(cleanup);

  it("shows related substances peaks, not the assay layout", () => {
    show(rs);
    expect(screen.getByText("Related Substances Review · Principal")).toBeTruthy();
    expect(screen.getByText("Reference: 0.1%")).toBeTruthy();
    expect(screen.getByText("0.03 (disregarded)")).toBeTruthy();
    expect(screen.queryByText(/undefined/)).toBeNull();
    expect(screen.queryByText(/Theoretical Weights/)).toBeNull();
  });

  it("gives all related substances rows one panel and keeps other results", () => {
    const [a, b, other] = [param(rs), param(rs), param(isAssay)];
    expect(hplcPanelResults([a, b, other])).toEqual([a, other]);
  });

  it("shows the ratio, internal standard and content basis of an internal standard assay", () => {
    show(isAssay);
    expect(screen.getByText("Standard Mean Ratio")).toBeTruthy();
    expect(screen.getByText("p-hydroxybenzoic acid")).toBeTruthy();
    expect(screen.getByText("1.9659")).toBeTruthy();
    expect(screen.getByText("As is: 98.00 % · Moisture: 0.5 %")).toBeTruthy();
  });

  it("reads a stored moisture condition in words", () => {
    expect(formatAnalysisConditions(JSON.stringify({ moisture: { percent: 0.5, role: "LossOnDrying", testCode: "LOD-UI" } })))
      .toBe("Loss on drying 0.5 % (from LOD-UI)");
    expect(formatAnalysisConditions(JSON.stringify({ temp: "25" }))).toBe("temp: 25");
    expect(formatAnalysisConditions(null)).toBeNull();
  });
});
