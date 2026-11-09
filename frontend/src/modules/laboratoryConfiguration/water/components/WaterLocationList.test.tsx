import { describe, expect, it, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { WaterLocationList } from "./WaterLocationList";
import type { SamplingPoint, PointHealth, SamplingConfig } from "../waterConfigTypes";
import type { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";

const mockOptions: TestDefinitionOption[] = [
  {
    id: 1,
    code: "TAMC-Water",
    displayName: "TAMC for Water",
    isActive: true,
    workflowType: "CountTest",
    section: { id: 1, name: "Microbiology", code: "MICRO" }
  },
  {
    id: 2,
    code: "WATER_CHEM",
    displayName: "Water Chemistry",
    isActive: true,
    workflowType: "CountTest",
    section: { id: 2, name: "Physicochemical", code: "FP" }
  }
];

vi.mock("../../../../hooks/useTestDefinitions", () => ({
  useTestDefinitions: () => ({
    options: mockOptions,
    activeOptions: mockOptions.filter((o) => o.isActive),
    addNew: vi.fn(),
    update: vi.fn(),
    setActive: vi.fn(),
    reload: vi.fn()
  })
}));

vi.mock("../../../../hooks/useEditableTestCodes", () => ({
  useEditableTestCodes: () => ({
    codes: ["TAMC-Water"],
    editableCodes: new Set(["TAMC-Water"]),
    loading: false,
    reload: vi.fn()
  })
}));

afterEach(cleanup);

describe("WaterLocationList", () => {
  const points: SamplingPoint[] = [
    {
      id: 101,
      code: "PW-01",
      location: "Building A",
      waterDepartmentId: 1,
      testingFrequency: "Weekly",
      assignedTestCodes: ["TAMC-Water", "WATER_CHEM"],
      version: 1
    },
    {
      id: 102,
      code: "PW-02",
      location: "Building B",
      waterDepartmentId: 1,
      testingFrequency: "Weekly",
      assignedTestCodes: ["TAMC-Water"],
      version: 1
    }
  ];

  const configsByPoint: Record<number, SamplingConfig[]> = {
    101: [
      { id: 1, waterSamplingPointId: 101, testCode: "TAMC-Water", alertLimit: "10", actionLimit: "100", specLimit: "500", unit: "CFU/mL" },
      { id: 2, waterSamplingPointId: 101, testCode: "WATER_CHEM", alertLimit: "5", actionLimit: "10", specLimit: "20", unit: "ppm" }
    ],
    102: [
      { id: 3, waterSamplingPointId: 102, testCode: "TAMC-Water", alertLimit: "10", actionLimit: "100", specLimit: "500", unit: "CFU/mL" }
    ]
  };

  const healthByPoint: Record<number, PointHealth> = {
    101: { noTests: false, missingLimitCodes: [] },
    102: { noTests: false, missingLimitCodes: [] }
  };

  it("disables delete location when the point has locked codes from another lab", () => {
    const onDelete = vi.fn();
    render(
      <PinnedLightTheme>
        <WaterLocationList
          points={points}
          configsByPoint={configsByPoint}
          healthByPoint={healthByPoint}
          isCountTest={() => true}
          testName={(c) => c}
          onEdit={vi.fn()}
          onDelete={onDelete}
          onAdd={vi.fn()}
          addLabel="Add location"
          editableCodes={new Set(["TAMC-Water"])}
        />
      </PinnedLightTheme>
    );

    // PW-01 has WATER_CHEM which is outside editableCodes -> delete button disabled
    const deletePw01 = screen.getByLabelText("Delete PW-01");
    expect((deletePw01 as HTMLButtonElement).disabled).toBe(true);

    // PW-02 has only TAMC-Water which is in editableCodes -> delete button enabled
    const deletePw02 = screen.getByLabelText("Delete PW-02");
    expect((deletePw02 as HTMLButtonElement).disabled).toBe(false);
  });

  it("renders locked count-test limit rows as read-only with owning lab caption", () => {
    const onEdit = vi.fn();
    render(
      <PinnedLightTheme>
        <WaterLocationList
          points={[points[0]]}
          configsByPoint={configsByPoint}
          healthByPoint={healthByPoint}
          isCountTest={() => true}
          testName={(c) => c}
          onEdit={onEdit}
          onDelete={vi.fn()}
          onAdd={vi.fn()}
          addLabel="Add location"
          editableCodes={new Set(["TAMC-Water"])}
        />
      </PinnedLightTheme>
    );

    // Expand limits for PW-01
    const expandBtn = screen.getByLabelText("Show limits for PW-01");
    fireEvent.click(expandBtn);

    // TAMC-Water is editable -> has "Edit limits" link
    expect(screen.getByText("Edit limits")).toBeDefined();

    // WATER_CHEM is locked -> shows owning lab caption "Physicochemical", no edit link for it
    expect(screen.getByText("Physicochemical")).toBeDefined();
  });
});
