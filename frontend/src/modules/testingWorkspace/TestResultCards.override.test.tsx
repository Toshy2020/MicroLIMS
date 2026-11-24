import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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
  analysis: null,
  competencyOverrides: [
    {
      analystName: "Amina Analyst",
      grantedByFullName: "Hana Head",
      grantedAtUtc: "2026-11-24T09:00:00Z",
      meaning: "Competency override",
      reason: "Supervised execution",
      endedAtUtc: "2026-11-24T11:00:00Z"
    }
  ]
};

afterEach(async () => {
  cleanup();
  await resetLanguage();
});

describe("TestResultCard competency override display", () => {
  it("renders override manifestation and ended timestamp using i18n keys in English", async () => {
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <TestResultCard test={baseTest} />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    await user.click(screen.getByRole("button", { name: /TAMC/i }));

    expect(screen.getByText("Competency override · Analyst: Amina Analyst")).toBeTruthy();
    expect(screen.getByText("Hana Head")).toBeTruthy();
    expect(screen.getByText("Supervised execution")).toBeTruthy();
    expect(screen.getByText(/^Ended: /)).toBeTruthy();
  });

  it("renders translated analyst and ended labels in Arabic", async () => {
    const user = userEvent.setup();
    await renderInArabic(
      <PinnedLightTheme>
        <MemoryRouter>
          <TestResultCard test={baseTest} />
        </MemoryRouter>
      </PinnedLightTheme>,
      ["testingWorkspace"]
    );

    await user.click(screen.getByRole("button", { name: /TAMC/i }));

    expect(screen.getByText("Competency override · المحلل: Amina Analyst")).toBeTruthy();
    expect(screen.getByText(/^انتهى: /)).toBeTruthy();
  });
});
