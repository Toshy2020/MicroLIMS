import { describe, expect, it, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { PathogenPicker } from "./PathogenPicker";
import type { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";

const mockOptions: TestDefinitionOption[] = [
  {
    id: 1,
    code: "ECOLI",
    displayName: "Escherichia coli",
    isActive: true,
    workflowType: "Observation",
    section: { id: 1, name: "Microbiology", code: "MICRO" }
  },
  {
    id: 2,
    code: "SALM",
    displayName: "Salmonella",
    isActive: true,
    workflowType: "Observation",
    section: { id: 1, name: "Microbiology", code: "MICRO" }
  },
  {
    id: 3,
    code: "LOCKED_PATHO",
    displayName: "Locked Pathogen",
    isActive: true,
    workflowType: "Observation",
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

afterEach(cleanup);

describe("PathogenPicker", () => {
  it("offers only editable pathogen codes when editableCodes is provided", () => {
    const onChange = vi.fn();
    render(
      <PathogenPicker
        value={[]}
        onChange={onChange}
        editableCodes={new Set(["ECOLI"])}
      />
    );

    const input = screen.getByLabelText("Pathogen tests");
    fireEvent.mouseDown(input);

    expect(screen.getByText("Escherichia coli (ECOLI)")).toBeDefined();
    expect(screen.queryByText("Salmonella (SALM)")).toBeNull();
  });

  it("renders locked chip without delete icon for codes outside editableCodes", () => {
    const onChange = vi.fn();
    render(
      <PathogenPicker
        value={["ECOLI", "LOCKED_PATHO"]}
        onChange={onChange}
        editableCodes={new Set(["ECOLI"])}
      />
    );

    const ecoliChip = screen.getByText("Escherichia coli (ECOLI)").closest(".MuiChip-root");
    const lockedChip = screen.getByText("Locked Pathogen (LOCKED_PATHO)").closest(".MuiChip-root");

    expect(ecoliChip?.querySelector(".MuiChip-deleteIcon")).not.toBeNull();
    expect(lockedChip?.querySelector(".MuiChip-deleteIcon")).toBeNull();
  });
});
