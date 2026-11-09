import { describe, expect, it, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { TestCodePickerMulti } from "./TestCodePickerMulti";
import type { TestDefinitionOption } from "../hooks/useTestDefinitions";

const mockOptions: TestDefinitionOption[] = [
  {
    id: 1,
    code: "TAMC",
    displayName: "Total Aerobic Microbial Count",
    isActive: true,
    workflowType: "CountTest",
    section: { id: 1, name: "Microbiology", code: "MICRO" }
  },
  {
    id: 2,
    code: "TYMC",
    displayName: "Total Yeast and Mold Count",
    isActive: true,
    workflowType: "CountTest",
    section: { id: 1, name: "Microbiology", code: "MICRO" }
  },
  {
    id: 3,
    code: "WATER_CHEM",
    displayName: "Water Chemical Analysis",
    isActive: true,
    workflowType: "Range",
    section: { id: 2, name: "Physicochemical", code: "FP" }
  }
];

vi.mock("../hooks/useTestDefinitions", () => ({
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

describe("TestCodePickerMulti", () => {
  it("renders locked chip without delete icon and with owning lab tooltip for codes outside editableCodes", () => {
    const onChange = vi.fn();
    render(
      <TestCodePickerMulti
        value={["TAMC", "WATER_CHEM"]}
        onChange={onChange}
        editableCodes={new Set(["TAMC"])}
      />
    );

    // Both chips are rendered
    expect(screen.getByText("TAMC")).toBeDefined();
    expect(screen.getByText("WATER_CHEM")).toBeDefined();

    // TAMC is editable: its chip has a delete icon (MuiChip-deleteIcon)
    const tamcChip = screen.getByText("TAMC").closest(".MuiChip-root");
    expect(tamcChip?.querySelector(".MuiChip-deleteIcon")).not.toBeNull();

    // WATER_CHEM is locked: its chip does NOT have a delete icon
    const waterChip = screen.getByText("WATER_CHEM").closest(".MuiChip-root");
    expect(waterChip?.querySelector(".MuiChip-deleteIcon")).toBeNull();
  });

  it("offers only editable codes in the dropdown options", () => {
    const onChange = vi.fn();
    render(
      <TestCodePickerMulti
        value={[]}
        onChange={onChange}
        editableCodes={new Set(["TAMC"])}
      />
    );

    const input = screen.getByPlaceholderText("e.g. TAMC, PATHOGEN_ECOLI");
    fireEvent.mouseDown(input);

    expect(screen.getByText("TAMC: Total Aerobic Microbial Count")).toBeDefined();
    expect(screen.queryByText("WATER_CHEM: Water Chemical Analysis")).toBeNull();
  });

  it("preserves locked codes from other labs on change", () => {
    const onChange = vi.fn();
    const { rerender } = render(
      <TestCodePickerMulti
        value={["WATER_CHEM"]}
        onChange={onChange}
        editableCodes={new Set(["TAMC"])}
      />
    );

    const input = screen.getByPlaceholderText("e.g. TAMC, PATHOGEN_ECOLI");
    fireEvent.mouseDown(input);

    // Pick TAMC
    const option = screen.getByText("TAMC: Total Aerobic Microbial Count");
    fireEvent.click(option);

    // Should include both the selected TAMC and preserved locked WATER_CHEM
    expect(onChange).toHaveBeenCalledWith(expect.arrayContaining(["WATER_CHEM", "TAMC"]));
  });

  it("behaves normally without editableCodes prop (backward compatibility)", () => {
    const onChange = vi.fn();
    render(
      <TestCodePickerMulti
        value={["TAMC", "WATER_CHEM"]}
        onChange={onChange}
      />
    );

    // Both chips have delete icons when editableCodes is omitted
    const tamcChip = screen.getByText("TAMC").closest(".MuiChip-root");
    const waterChip = screen.getByText("WATER_CHEM").closest(".MuiChip-root");
    expect(tamcChip?.querySelector(".MuiChip-deleteIcon")).not.toBeNull();
    expect(waterChip?.querySelector(".MuiChip-deleteIcon")).not.toBeNull();
  });
});
