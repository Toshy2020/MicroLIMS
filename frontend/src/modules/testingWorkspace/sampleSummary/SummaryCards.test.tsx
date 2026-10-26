import { describe, it, expect, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { SampleIdentityCard } from "./SummaryCards";
import type { SampleSummary } from "../types/sampleSummaryTypes";

afterEach(cleanup);

const mockSummary = (over: Partial<SampleSummary> = {}): SampleSummary => ({
  sampleId: 101,
  referenceNumber: "RM2610001",
  category: "RawMaterial",
  displayName: "Sodium Chloride USP",
  productionStage: null,
  causeOfTesting: "Routine",
  batchNumber: "BN-999",
  grade: "USP",
  retentionQuantity: null,
  retentionQuantityUnit: null,
  controlNumber: "CN-999",
  status: "InTesting",
  statusDisplay: "In testing",
  receivedByName: "John Doe",
  receivedAt: "2026-10-26T10:00:00Z",
  sampledBy: "QC",
  sampleQuantity: "100 g",
  mfgDate: "2026-01-01T00:00:00Z",
  expDate: "2028-01-01T00:00:00Z",
  waterSamplingPointCode: null,
  waterSamplingPointLocation: null,
  storageCondition: null,
  storageTimeHours: null,
  reviewedByName: null,
  reviewedAt: null,
  approvedByName: null,
  approvedAt: null,
  approvalDecision: null,
  certificateRemarks: null,
  preparation: null,
  signatures: [],
  testOrders: [],
  timeline: [],
  certificate: { sample: { matrix: null, simple: null } } as never,
  sections: [],
  overallStatus: "InProgress",
  combinedCoaAvailable: false,
  allSectionsVisible: true,
  ...over
});

describe("SampleIdentityCard retention display", () => {
  it("renders read-only retention line when retentionQuantity and unit are set", () => {
    const summary = mockSummary({
      retentionQuantity: 50,
      retentionQuantityUnit: "g"
    });

    render(
      <PinnedLightTheme>
        <SampleIdentityCard summary={summary} />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Retention: 50 g")).toBeTruthy();
    expect(screen.getByText("Retention", { selector: "p" })).toBeTruthy();
  });

  it("does not render retention field when retentionQuantity is null", () => {
    const summary = mockSummary({
      retentionQuantity: null,
      retentionQuantityUnit: null
    });

    render(
      <PinnedLightTheme>
        <SampleIdentityCard summary={summary} />
      </PinnedLightTheme>
    );

    expect(screen.queryByText(/Retention:/)).toBeNull();
    expect(screen.queryByText("Retention", { selector: "p" })).toBeNull();
  });
});
