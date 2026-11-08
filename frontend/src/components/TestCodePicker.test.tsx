import { describe, expect, it, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent } from "@testing-library/react";
import { TestCodePicker } from "./TestCodePicker";
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

describe("TestCodePicker", () => {
  it("offers only editable codes when editableCodes is provided", () => {
    const onChange = vi.fn();
    render(
      <TestCodePicker
        value=""
        onChange={onChange}
        editableCodes={new Set(["TAMC"])}
      />
    );

    const input = screen.getByPlaceholderText("e.g. TAMC, PATHOGEN_ECOLI");
    fireEvent.mouseDown(input);

    expect(screen.getByText("TAMC: Total Aerobic Microbial Count")).toBeDefined();
    expect(screen.queryByText("WATER_CHEM: Water Chemical Analysis")).toBeNull();
  });

  it("offers all active codes when editableCodes is omitted", () => {
    const onChange = vi.fn();
    render(
      <TestCodePicker
        value=""
        onChange={onChange}
      />
    );

    const input = screen.getByPlaceholderText("e.g. TAMC, PATHOGEN_ECOLI");
    fireEvent.mouseDown(input);

    expect(screen.getByText("TAMC: Total Aerobic Microbial Count")).toBeDefined();
    expect(screen.getByText("WATER_CHEM: Water Chemical Analysis")).toBeDefined();
  });
});
