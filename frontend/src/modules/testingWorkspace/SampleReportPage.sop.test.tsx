import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { SampleReportPage } from "./SampleReportPage";

const mockGetSummary = vi.fn();
const mockGetArchived = vi.fn();

vi.mock("./services/SampleSummaryService", () => ({
  SampleSummaryService: { getSummary: (...args: unknown[]) => mockGetSummary(...args) }
}));
vi.mock("./services/ArchivedRecordsService", () => ({
  ArchivedRecordsService: { getForEntity: (...args: unknown[]) => mockGetArchived(...args) }
}));

const summary = {
  sampleId: 1,
  itemId: 42,
  labTimeZoneId: "UTC",
  referenceNumber: "FP1126009",
  category: "FinishedProduct",
  displayName: "Product X",
  status: "Approved",
  statusDisplay: "Approved",
  receivedAt: "2026-10-26T10:00:07Z",
  mfgDate: "2026-01-01T00:00:00Z",
  expDate: "2028-10-01T00:00:00Z",
  reviewedByName: "Rana Reviewer",
  reviewedAt: "2026-11-17T09:41:07Z",
  approvedByName: "Sami Head",
  approvedAt: "2026-11-17T10:41:07Z",
  signatures: [],
  testOrders: [
    {
      testOrderId: 201,
      testCode: "TAMC",
      testDisplayName: "Total Aerobic Microbial Count",
      specificationText: "< 100 CFU/g",
      sectionName: "Microbiology",
      status: "Reviewed",
      currentStep: "Reviewed",
      isSuperseded: false,
      outsourcedToLab: null,
      cancelledAtStep: null,
      cancelledAtStage: null,
      locations: [],
      incubations: [],
      results: [
        {
          rawValue: "10",
          interpretedValue: "10 CFU/g",
          type: "Numeric",
          enteredByName: "Amina Analyst",
          enteredAt: "2026-11-24T10:00:00Z"
        }
      ],
      countTestReadings: [],
      pathogenObservations: [],
      biochemicalResults: [],
      workflowHistory: [],
      analysis: null,
      sopCapture: {
        status: "Captured",
        documentMasterId: 10,
        documentCode: "SOP-RPT-01",
        title: "Microbial Limit Test",
        revisionId: 11,
        revisionNumber: "02",
        effectiveDateUtc: "2026-10-01T08:00:00Z",
        capturedAtUtc: "2026-10-02T09:00:00Z"
      },
      newerSopRevision: null
    },
    {
      testOrderId: 202,
      testCode: "TYMC",
      testDisplayName: "Total Yeasts and Moulds",
      specificationText: "< 10 CFU/g",
      sectionName: "Microbiology",
      status: "Reviewed",
      currentStep: "Reviewed",
      isSuperseded: false,
      outsourcedToLab: null,
      cancelledAtStep: null,
      cancelledAtStage: null,
      locations: [],
      incubations: [],
      results: [
        {
          rawValue: "0",
          interpretedValue: "0 CFU/g",
          type: "Numeric",
          enteredByName: "Amina Analyst",
          enteredAt: "2026-11-24T10:00:00Z"
        }
      ],
      countTestReadings: [],
      pathogenObservations: [],
      biochemicalResults: [],
      workflowHistory: [],
      analysis: null,
      sopCapture: null,
      newerSopRevision: null
    }
  ],
  timeline: [],
  certificate: { sample: { matrix: null, simple: null, methodDocuments: [] }, sections: [] }
};

beforeEach(() => {
  mockGetSummary.mockResolvedValue(summary);
  mockGetArchived.mockResolvedValue([]);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("SampleReportPage SOP display", () => {
  it("renders captured SOP and pre-capture not-recorded text on test result cards", async () => {
    render(
      <PinnedLightTheme>
        <MemoryRouter initialEntries={["/samples/1/report"]}>
          <Routes>
            <Route path="/samples/:id/report" element={<SampleReportPage />} />
          </Routes>
        </MemoryRouter>
      </PinnedLightTheme>
    );

    await screen.findAllByText("FP1126009");
    expect(screen.getByText((_, el) => el?.tagName === "P" && (el.textContent ?? "").includes("SOP-RPT-01 rev. 02"))).toBeTruthy();
    expect(screen.getByText(/Microbial Limit Test/)).toBeTruthy();
    expect(screen.getByText("SOP not recorded")).toBeTruthy();
  });
});
