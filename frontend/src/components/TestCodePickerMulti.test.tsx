import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { TestCodePickerMulti } from "./TestCodePickerMulti";
import type { TestDefinitionOption } from "../hooks/useTestDefinitions";
import { masterDataOptions } from "../services/masterDataOptions";

// Only the HTTP service layer is mocked; the real picker and
// useTestDefinitions hook run (CLAUDE.md Rule 4.5).
vi.mock("../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../services/masterDataOptions")>();
  return {
    ...actual,
    masterDataOptions: { ...actual.masterDataOptions, getTestDefinitions: vi.fn(), createTestDefinition: vi.fn() }
  };
});

const NO_SUCH_TEST = "No such test. Add it in Test Master first.";
const PLACEHOLDER = "e.g. TAMC, PATHOGEN_ECOLI";

const testDefinitions: TestDefinitionOption[] = [
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
  },
  {
    id: 4,
    code: "OLD_TEST",
    displayName: "Retired Test",
    isActive: false,
    workflowType: "Range",
    section: { id: 2, name: "Physicochemical", code: "FP" }
  }
];

function renderPicker(props?: Partial<React.ComponentProps<typeof TestCodePickerMulti>>) {
  const onChange = vi.fn();
  render(
    <PinnedLightTheme>
      <TestCodePickerMulti value={[]} onChange={onChange} {...props} />
    </PinnedLightTheme>
  );
  return { onChange, user: userEvent.setup() };
}

describe("TestCodePickerMulti", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue(testDefinitions);
  });

  afterEach(cleanup);

  it("renders locked chip without delete icon for codes outside editableCodes", async () => {
    renderPicker({ value: ["TAMC", "WATER_CHEM"], editableCodes: new Set(["TAMC"]) });

    const tamcChip = (await screen.findByText("TAMC")).closest(".MuiChip-root");
    const waterChip = (await screen.findByText("WATER_CHEM")).closest(".MuiChip-root");

    // TAMC is editable: its chip has a delete icon
    expect(tamcChip?.querySelector(".MuiChip-deleteIcon")).not.toBeNull();
    // WATER_CHEM is locked: its chip does NOT have a delete icon
    expect(waterChip?.querySelector(".MuiChip-deleteIcon")).toBeNull();
  });

  it("shows the owning laboratory as a tooltip on a locked chip", async () => {
    const { user } = renderPicker({ value: ["WATER_CHEM"], editableCodes: new Set(["TAMC"]) });

    await user.hover(await screen.findByText("WATER_CHEM"));

    expect(await screen.findByRole("tooltip")).toHaveProperty("textContent", "Physicochemical");
  });

  it("offers only editable codes in the dropdown options", async () => {
    const { user } = renderPicker({ editableCodes: new Set(["TAMC"]) });

    await user.click(screen.getByPlaceholderText(PLACEHOLDER));

    expect(await screen.findByRole("option", { name: "TAMC: Total Aerobic Microbial Count" })).toBeTruthy();
    expect(screen.queryByRole("option", { name: "WATER_CHEM: Water Chemical Analysis" })).toBeNull();
  });

  it("preserves locked codes from other labs on change", async () => {
    const { onChange, user } = renderPicker({ value: ["WATER_CHEM"], editableCodes: new Set(["TAMC"]) });

    await screen.findByText("WATER_CHEM");
    await user.click(screen.getByRole("combobox"));
    await user.click(await screen.findByRole("option", { name: "TAMC: Total Aerobic Microbial Count" }));

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenCalledWith(["WATER_CHEM", "TAMC"]);
  });

  it("keeps a locked code when an editable chip is removed", async () => {
    const { onChange, user } = renderPicker({ value: ["TAMC", "WATER_CHEM"], editableCodes: new Set(["TAMC"]) });

    const tamcChip = (await screen.findByText("TAMC")).closest(".MuiChip-root");
    await user.click(tamcChip?.querySelector(".MuiChip-deleteIcon") as Element);

    expect(onChange).toHaveBeenCalledWith(["WATER_CHEM"]);
  });

  it("behaves normally without editableCodes prop (backward compatibility)", async () => {
    renderPicker({ value: ["TAMC", "WATER_CHEM"] });

    const tamcChip = (await screen.findByText("TAMC")).closest(".MuiChip-root");
    const waterChip = (await screen.findByText("WATER_CHEM")).closest(".MuiChip-root");
    expect(tamcChip?.querySelector(".MuiChip-deleteIcon")).not.toBeNull();
    expect(waterChip?.querySelector(".MuiChip-deleteIcon")).not.toBeNull();
  });

  it("calls onChange with the chosen code added to the previously selected codes", async () => {
    const { onChange, user } = renderPicker({ value: ["TAMC"] });

    await screen.findByText("TAMC");
    await user.click(screen.getByRole("combobox"));
    await user.click(await screen.findByRole("option", { name: "TYMC: Total Yeast and Mold Count" }));

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenCalledWith(["TAMC", "TYMC"]);
  });

  it("shows an already-assigned frozen test as selected but does not offer it for new selection", async () => {
    const { user } = renderPicker({ value: ["OLD_TEST"] });

    expect(await screen.findByText("OLD_TEST")).toBeTruthy();
    await user.click(screen.getByRole("combobox"));

    expect(await screen.findByRole("option", { name: "TAMC: Total Aerobic Microbial Count" })).toBeTruthy();
    expect(screen.queryByRole("option", { name: "OLD_TEST: Retired Test" })).toBeNull();
  });

  it("says no such test exists, with no add option, when the typed code matches nothing", async () => {
    const { user } = renderPicker();

    await user.click(screen.getByPlaceholderText(PLACEHOLDER));
    await screen.findByRole("option", { name: "TAMC: Total Aerobic Microbial Count" });
    await user.type(screen.getByPlaceholderText(PLACEHOLDER), "TYPO_CODE");

    expect(await screen.findByText(NO_SUCH_TEST)).toBeTruthy();
    expect(screen.queryAllByRole("option")).toHaveLength(0);
    expect(screen.queryByText(/to Test Master/)).toBeNull();
  });

  it("never creates a test or emits the typed text when an unknown code is submitted with Enter", async () => {
    const { onChange, user } = renderPicker({ value: ["TAMC"] });

    await screen.findByText("TAMC");
    await user.click(screen.getByRole("combobox"));
    await screen.findByRole("option", { name: "TYMC: Total Yeast and Mold Count" });
    await user.type(screen.getByRole("combobox"), "TYPO_CODE{Enter}");

    expect(masterDataOptions.createTestDefinition).not.toHaveBeenCalled();
    expect(onChange).not.toHaveBeenCalled();
  });
});
