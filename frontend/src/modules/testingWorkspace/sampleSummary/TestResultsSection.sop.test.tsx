import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, expect, it } from "vitest";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import type { TestOrderSummaryDetail } from "../types/sampleSummaryTypes";
import { TestResultsSection } from "./TestResultsSection";

afterEach(cleanup);

function order(testOrderId: number, testCode: string): TestOrderSummaryDetail {
  return {
    testOrderId, testCode, testDisplayName: testCode, specificationText: null, status: "InProgress",
    currentStep: "Running", isSuperseded: false, cancelledAtStep: null, cancelledAtStage: null,
    incubations: [], results: [], countTestReadings: [], pathogenObservations: [], biochemicalResults: [],
    workflowHistory: [], locations: []
  };
}

it("shows each test's stored SOP in review and displays unresolved reasons separately", async () => {
  const date = "2026-10-01T00:00:00Z";
  const user = userEvent.setup();
  render(
    <PinnedLightTheme>
      <MemoryRouter>
        <TestResultsSection overallStatus="UnderReview" testOrders={[
          {
            ...order(1, "Assay"),
            sopCapture: {
              status: "Captured", documentMasterId: 1, documentCode: "SOP-ASSAY", title: "Method for assay",
              revisionId: 2, revisionNumber: "02", effectiveDateUtc: date, capturedAtUtc: date
            },
            newerSopRevision: { revisionNumber: "03", effectiveDateUtc: date }
          },
          {
            ...order(2, "Identity"),
            sopCapture: {
              status: "NoEffectiveRevision", documentMasterId: null, documentCode: null, title: null,
              revisionId: null, revisionNumber: null, effectiveDateUtc: null, capturedAtUtc: date
            }
          }
        ]} />
      </MemoryRouter>
    </PinnedLightTheme>
  );

  expect(screen.getByText(/SOP-ASSAY rev\. 02/)).toBeTruthy();
  expect(screen.getByText(/A newer revision \(rev\. 03\)/)).toBeTruthy();
  await user.click(screen.getByRole("button", { name: /Identity/ }));
  expect(screen.getByText("SOP not recorded: No effective revision")).toBeTruthy();
});
