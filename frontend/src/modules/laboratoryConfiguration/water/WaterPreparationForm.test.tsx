import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { WaterPreparationForm, isTestCodeInLab } from "./WaterPreparationForm";
import { getAllowedCategories } from "../../receivingTesting/ReceivingTestingWorkspacePage";
import type { TestDefinitionOption } from "../../../hooks/useTestDefinitions";

const mockPoints = [
  { id: 101, code: "WP-01", location: "Building A", assignedTestCodes: ["TAMC-Water"] },
  { id: 102, code: "WP-02", location: "Building B", assignedTestCodes: ["WATER_CHEM"] },
  { id: 103, code: "WP-03", location: "Building C", assignedTestCodes: ["TAMC-Water", "WATER_CHEM"] }
];

const mockTestDefs: TestDefinitionOption[] = [
  {
    id: 1,
    code: "TAMC-Water",
    displayName: "TAMC for Water",
    isActive: true,
    workflowType: "CountTest",
    sectionId: 1,
    section: { id: 1, name: "Microbiology", code: "MICRO" }
  },
  {
    id: 2,
    code: "WATER_CHEM",
    displayName: "Water Chemistry",
    isActive: true,
    workflowType: "QuantitativeTest",
    sectionId: 2,
    section: { id: 2, name: "Physicochemical", code: "FP" }
  }
];

const prepServiceMock = vi.hoisted(() => ({
  getSamplingPointsForDepartment: vi.fn(),
  prepare: vi.fn()
}));

vi.mock("./services/WaterPreparationService", () => ({
  WaterPreparationService: prepServiceMock
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
  prepServiceMock.getSamplingPointsForDepartment.mockReset().mockResolvedValue(mockPoints);
  prepServiceMock.prepare.mockReset().mockResolvedValue({});
});

afterEach(cleanup);

describe("getAllowedCategories", () => {
  it("gives microbiology all six categories", () => {
    expect(getAllowedCategories({ sectionId: 1, code: "MICRO", name: "Microbiology" })).toEqual([
      "product", "rm", "pm", "water", "em", "ac"
    ]);
  });

  it("gives rmpm area rm, pm, and water", () => {
    expect(getAllowedCategories({ sectionId: 2, code: "FP", name: "Physchem", area: "rmpm" })).toEqual([
      "rm", "pm", "water"
    ]);
  });

  it("gives fp area product and ac", () => {
    expect(getAllowedCategories({ sectionId: 2, code: "FP", name: "Physchem", area: "fp" })).toEqual([
      "product", "ac"
    ]);
  });
});

describe("isTestCodeInLab", () => {
  it("filters correctly by explicit labSectionId", () => {
    expect(isTestCodeInLab("WATER_CHEM", 2, mockTestDefs)).toBe(true);
    expect(isTestCodeInLab("TAMC-Water", 2, mockTestDefs)).toBe(false);
    expect(isTestCodeInLab("UNKNOWN", 2, mockTestDefs)).toBe(false);
  });

  it("treats null labSectionId as MICRO lab", () => {
    expect(isTestCodeInLab("TAMC-Water", null, mockTestDefs)).toBe(true);
    expect(isTestCodeInLab("WATER_CHEM", null, mockTestDefs)).toBe(false);
  });

  it("treats undefined labSectionId as MICRO lab", () => {
    expect(isTestCodeInLab("TAMC-Water", undefined, mockTestDefs)).toBe(true);
    expect(isTestCodeInLab("WATER_CHEM", undefined, mockTestDefs)).toBe(false);
  });

  it("falls back to current behaviour (true) if no test has section code", () => {
    const unsectionedTests: TestDefinitionOption[] = [
      { id: 10, code: "TEST_X", displayName: "X", isActive: true, workflowType: "CountTest" }
    ];
    expect(isTestCodeInLab("TEST_X", null, unsectionedTests)).toBe(true);
  });
});

describe("WaterPreparationForm filtering", () => {
  it("filters points and tests for FP lab (labSectionId: 2)", async () => {
    render(
      <PinnedLightTheme>
        <WaterPreparationForm
          sampleId={1}
          waterDepartmentId={10}
          labSectionId={2}
          onComplete={vi.fn()}
        />
      </PinnedLightTheme>
    );

    // WP-01 only has MICRO test so it must not appear
    await waitFor(() => {
      expect(screen.queryByText("WP-01")).toBeNull();
    });

    // WP-02 has FP test so it appears with WATER_CHEM
    expect(screen.getByText("WP-02")).toBeDefined();
    expect(screen.getAllByText("WATER_CHEM").length).toBe(2);

    // WP-03 has both tests; it appears but TAMC-Water is excluded
    expect(screen.getByText("WP-03")).toBeDefined();
    expect(screen.queryByText("TAMC-Water")).toBeNull();
  });

  it("filters points and tests for MICRO lab when labSectionId is null", async () => {
    render(
      <PinnedLightTheme>
        <WaterPreparationForm
          sampleId={1}
          waterDepartmentId={10}
          labSectionId={null}
          onComplete={vi.fn()}
        />
      </PinnedLightTheme>
    );

    await waitFor(() => {
      expect(screen.getByText("WP-01")).toBeDefined();
    });

    // WP-01 appears with TAMC-Water
    expect(screen.getByText("WP-01")).toBeDefined();
    expect(screen.getAllByText("TAMC-Water").length).toBeGreaterThan(0);

    // WP-02 only has FP test so it must not appear
    expect(screen.queryByText("WP-02")).toBeNull();

    // WP-03 appears but WATER_CHEM is excluded
    expect(screen.getByText("WP-03")).toBeDefined();
    expect(screen.queryByText("WATER_CHEM")).toBeNull();
  });

  it("selects a point, enters storage condition, and submits preparation", async () => {
    const onComplete = vi.fn();
    render(
      <PinnedLightTheme>
        <WaterPreparationForm
          sampleId={123}
          waterDepartmentId={10}
          labSectionId={2}
          onComplete={onComplete}
        />
      </PinnedLightTheme>
    );

    await screen.findByText("WP-02");

    // Select storage condition: RoomTemperature
    fireEvent.mouseDown(screen.getByLabelText("Storage condition"));
    fireEvent.click(await screen.findByRole("option", { name: "Room Temperature" }));

    // Click card WP-02 to select it
    fireEvent.click(screen.getByText("WP-02"));

    // Click confirm button
    const confirmButton = screen.getByRole("button", { name: /Start Testing/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(prepServiceMock.prepare).toHaveBeenCalledWith(
        123,
        [102],
        "RoomTemperature",
        null,
        []
      );
      expect(onComplete).toHaveBeenCalled();
    });
  });
});
