import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AfterCleaningPreparationForm } from "./AfterCleaningPreparationForm";
import type { TestDefinitionOption } from "../../../hooks/useTestDefinitions";

const mockParts = [
  { id: 1, name: "Hopper" },
  { id: 2, name: "Nozzle" }
];

const mockPartConfigs: Record<number, { id: number; testType: string; testCode: string }[]> = {
  1: [
    { id: 11, testType: "Swab", testCode: "TAMC-Swab" },
    { id: 12, testType: "Rinse", testCode: "HPLC-Rinse" }
  ],
  2: [
    { id: 21, testType: "Swab", testCode: "TAMC-Swab" }
  ]
};

const mockTestDefs: TestDefinitionOption[] = [
  {
    id: 1,
    code: "TAMC-Swab",
    displayName: "TAMC Swab",
    isActive: true,
    workflowType: "CountTest",
    sectionId: 1,
    section: { id: 1, name: "Microbiology", code: "MICRO" }
  },
  {
    id: 2,
    code: "HPLC-Rinse",
    displayName: "HPLC Rinse",
    isActive: true,
    workflowType: "QuantitativeTest",
    sectionId: 2,
    section: { id: 2, name: "Physicochemical", code: "FP" }
  }
];

const acServiceMock = vi.hoisted(() => ({
  getPartsForMachine: vi.fn(),
  getPartConfigurations: vi.fn(),
  prepare: vi.fn()
}));

vi.mock("./services/AfterCleaningPreparationService", () => ({
  AfterCleaningPreparationService: acServiceMock
}));

vi.mock("../../../hooks/useTestDefinitions", () => ({
  useTestDefinitions: () => ({
    options: mockTestDefs,
    loading: false,
    reload: vi.fn(),
    addNew: vi.fn()
  })
}));

vi.mock("../../monitoringPlan/components/MonitoringChecklist", () => ({
  MonitoringChecklist: () => <div data-testid="monitoring-checklist" />
}));

beforeEach(() => {
  acServiceMock.getPartsForMachine.mockReset().mockResolvedValue(mockParts);
  acServiceMock.getPartConfigurations.mockReset().mockImplementation((partId: number) =>
    Promise.resolve(mockPartConfigs[partId] ?? [])
  );
  acServiceMock.prepare.mockReset().mockResolvedValue({});
});

afterEach(cleanup);

describe("AfterCleaningPreparationForm filtering", () => {
  it("offers only FP configurations when labSectionId is 2", async () => {
    const onComplete = vi.fn();
    render(
      <PinnedLightTheme>
        <AfterCleaningPreparationForm
          sampleId={456}
          machineId={10}
          labSectionId={2}
          onComplete={onComplete}
        />
      </PinnedLightTheme>
    );

    await screen.findByText("Hopper");

    // Hopper has HPLC-Rinse; TAMC-Swab is not offered
    expect(screen.getByText("HPLC-Rinse")).toBeDefined();
    expect(screen.queryByText("TAMC-Swab")).toBeNull();

    // Nozzle has only TAMC-Swab, so for FP lab it has 0 allowed configs (disabled)
    const nozzleCard = screen.getByText("Nozzle").closest(".MuiCard-root");
    expect(nozzleCard).toBeDefined();

    // Select Hopper
    fireEvent.click(screen.getByText("Hopper"));

    // Confirm preparation
    const confirmButton = screen.getByRole("button", { name: /Start Testing/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      // Must only submit config 12 (HPLC-Rinse), never config 11 (TAMC-Swab)
      expect(acServiceMock.prepare).toHaveBeenCalledWith(456, [12], []);
      expect(onComplete).toHaveBeenCalled();
    });
  });

  it("offers only MICRO configurations when labSectionId is null", async () => {
    const onComplete = vi.fn();
    render(
      <PinnedLightTheme>
        <AfterCleaningPreparationForm
          sampleId={789}
          machineId={10}
          labSectionId={null}
          onComplete={onComplete}
        />
      </PinnedLightTheme>
    );

    await screen.findByText("Hopper");

    // TAMC-Swab is offered; HPLC-Rinse is not offered
    expect(screen.getAllByText("TAMC-Swab").length).toBeGreaterThan(0);
    expect(screen.queryByText("HPLC-Rinse")).toBeNull();

    // Select Hopper and Nozzle
    fireEvent.click(screen.getByText("Hopper"));
    fireEvent.click(screen.getByText("Nozzle"));

    // Confirm preparation
    const confirmButton = screen.getByRole("button", { name: /Start Testing/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      // Must submit configs 11 and 21 (TAMC-Swab), not 12 (HPLC-Rinse)
      expect(acServiceMock.prepare).toHaveBeenCalledWith(789, [11, 21], []);
      expect(onComplete).toHaveBeenCalled();
    });
  });
});
