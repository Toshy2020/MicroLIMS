import { describe, expect, it, vi, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { AcPartCard } from "./AcPartCard";
import type { MachinePart, PartConfig } from "../acConfigTypes";
import type { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";

const mockOptions: TestDefinitionOption[] = [
  {
    id: 1,
    code: "SWAB_TAMC",
    displayName: "Swab TAMC",
    isActive: true,
    workflowType: "CountTest",
    section: { id: 1, name: "Microbiology", code: "MICRO" }
  },
  {
    id: 2,
    code: "RINSE_HPLC",
    displayName: "Rinse HPLC MACO",
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
    codes: ["SWAB_TAMC"],
    editableCodes: new Set(["SWAB_TAMC"]),
    loading: false,
    reload: vi.fn()
  })
}));

afterEach(cleanup);

describe("AcPartCard", () => {
  const part: MachinePart = {
    id: 10,
    name: "Filling needles",
    machineId: 1,
    version: 1
  };

  const configs: PartConfig[] = [
    {
      id: 1,
      machinePartId: 10,
      testType: "SwabCount",
      testCode: "SWAB_TAMC",
      alertLimit: "10",
      actionLimit: "50",
      specLimit: "100",
      isPathogenTest: false,
      unit: "CFU/swab",
      version: 1
    },
    {
      id: 2,
      machinePartId: 10,
      testType: "RinseCount",
      testCode: "RINSE_HPLC",
      alertLimit: "1",
      actionLimit: "5",
      specLimit: "10",
      isPathogenTest: false,
      unit: "µg/swab",
      version: 1
    }
  ];

  it("renders other labs' config rows as read-only with a lab-name caption and no edit or delete buttons", () => {
    const onEditCount = vi.fn();
    const onDeleteCount = vi.fn();

    render(
      <PinnedLightTheme>
        <AcPartCard
          part={part}
          configs={configs}
          testName={(c) => c}
          onEditPart={vi.fn()}
          onDeletePart={vi.fn()}
          onAddCount={vi.fn()}
          onEditCount={onEditCount}
          onDeleteCount={onDeleteCount}
          onEditPathogens={vi.fn()}
          editableCodes={new Set(["SWAB_TAMC"])}
        />
      </PinnedLightTheme>
    );

    // SWAB_TAMC is editable: has Edit and Remove buttons
    expect(screen.getByLabelText("Edit SwabCount SWAB_TAMC")).toBeDefined();
    expect(screen.getByLabelText("Remove SwabCount SWAB_TAMC")).toBeDefined();

    // RINSE_HPLC is locked: does NOT have Edit or Remove buttons
    expect(screen.queryByLabelText("Edit RinseCount RINSE_HPLC")).toBeNull();
    expect(screen.queryByLabelText("Remove RinseCount RINSE_HPLC")).toBeNull();

    // Shows owning lab caption "Physicochemical"
    expect(screen.getByText("Physicochemical")).toBeDefined();
  });
});
