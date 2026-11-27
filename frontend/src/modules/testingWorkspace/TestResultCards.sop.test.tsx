import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { TestResultCard } from "./TestResultCards";
import type { TestOrderSummaryDetail } from "./types/sampleSummaryTypes";

vi.mock("../../services/apiClient", () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn()
  }
}));

const baseTest: TestOrderSummaryDetail = {
  testOrderId: 101,
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
  analysis: null
};

afterEach(async () => {
  cleanup();
  await resetLanguage();
});

describe("TestResultCard SOP display", () => {
  it("renders captured SOP method, effective time in lab zone, and newer revision notice", () => {
    const test: TestOrderSummaryDetail = {
      ...baseTest,
      sopCapture: {
        status: "Captured",
        documentMasterId: 10,
        documentCode: "SOP-MIC-001",
        title: "Bioburden Assay",
        revisionId: 12,
        revisionNumber: "03",
        effectiveDateUtc: "2026-10-01T08:30:00Z",
        capturedAtUtc: "2026-10-02T09:00:00Z"
      },
      newerSopRevision: {
        revisionNumber: "04",
        effectiveDateUtc: "2026-10-05T10:00:00Z"
      }
    };

    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <TestResultCard test={test} showSop labTimeZoneId="UTC" />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    expect(screen.getByText((_, el) => el?.tagName === "P" && (el.textContent ?? "").includes("SOP-MIC-001 rev. 03"))).toBeTruthy();
    expect(screen.getByText(/Bioburden Assay/)).toBeTruthy();
    expect(screen.getByText(/Effective/)).toBeTruthy();
    expect(screen.getByRole("status").textContent).toContain("rev. 04");
  });

  it("renders unresolved SOP reason and not-recorded fallback when showSop is true", () => {
    const unresolved: TestOrderSummaryDetail = {
      ...baseTest,
      testOrderId: 102,
      sopCapture: {
        status: "NotFound",
        documentMasterId: null,
        documentCode: "SOP-MISSING",
        title: null,
        revisionId: null,
        revisionNumber: null,
        effectiveDateUtc: null,
        capturedAtUtc: "2026-10-02T09:00:00Z"
      }
    };

    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <TestResultCard test={unresolved} showSop />
          <TestResultCard test={baseTest} showSop />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    expect(screen.getByText("SOP not recorded: SOP not found in Document Control")).toBeTruthy();
    expect(screen.getByText("SOP not recorded")).toBeTruthy();
  });

  it("renders SOP details in Arabic", async () => {
    const test: TestOrderSummaryDetail = {
      ...baseTest,
      sopCapture: {
        status: "NoEffectiveRevision",
        documentMasterId: 10,
        documentCode: "SOP-MIC-001",
        title: null,
        revisionId: null,
        revisionNumber: null,
        effectiveDateUtc: null,
        capturedAtUtc: "2026-10-02T09:00:00Z"
      }
    };

    await renderInArabic(
      <PinnedLightTheme>
        <MemoryRouter>
          <TestResultCard test={test} showSop />
        </MemoryRouter>
      </PinnedLightTheme>,
      ["testingWorkspace", "common"]
    );

    expect(screen.getByText(/لا توجد مراجعة سارية/)).toBeTruthy();
  });
});
