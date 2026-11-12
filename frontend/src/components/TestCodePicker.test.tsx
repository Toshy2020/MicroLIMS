import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { TestCodePicker } from "./TestCodePicker";
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
    code: "WATER_CHEM",
    displayName: "Water Chemical Analysis",
    isActive: true,
    workflowType: "Range",
    section: { id: 2, name: "Physicochemical", code: "FP" }
  },
  {
    id: 3,
    code: "OLD_TEST",
    displayName: "Retired Test",
    isActive: false,
    workflowType: "Range",
    section: { id: 2, name: "Physicochemical", code: "FP" }
  }
];

function renderPicker(props?: Partial<React.ComponentProps<typeof TestCodePicker>>) {
  const onChange = vi.fn();
  render(
    <PinnedLightTheme>
      <TestCodePicker value="" onChange={onChange} {...props} />
    </PinnedLightTheme>
  );
  return { onChange, user: userEvent.setup() };
}

describe("TestCodePicker", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue(testDefinitions);
  });

  afterEach(cleanup);

  it("offers only editable codes when editableCodes is provided", async () => {
    const { user } = renderPicker({ editableCodes: new Set(["TAMC"]) });

    await user.click(screen.getByPlaceholderText(PLACEHOLDER));

    expect(await screen.findByRole("option", { name: "TAMC: Total Aerobic Microbial Count" })).toBeTruthy();
    expect(screen.queryByRole("option", { name: "WATER_CHEM: Water Chemical Analysis" })).toBeNull();
  });

  it("offers all active codes when editableCodes is omitted, but not frozen ones", async () => {
    const { user } = renderPicker();

    await user.click(screen.getByPlaceholderText(PLACEHOLDER));

    expect(await screen.findByRole("option", { name: "TAMC: Total Aerobic Microbial Count" })).toBeTruthy();
    expect(screen.getByRole("option", { name: "WATER_CHEM: Water Chemical Analysis" })).toBeTruthy();
    expect(screen.queryByRole("option", { name: "OLD_TEST: Retired Test" })).toBeNull();
  });

  it("calls onChange with the code of the chosen existing test", async () => {
    const { onChange, user } = renderPicker();

    await user.click(screen.getByPlaceholderText(PLACEHOLDER));
    await user.click(await screen.findByRole("option", { name: "WATER_CHEM: Water Chemical Analysis" }));

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenCalledWith("WATER_CHEM");
  });

  it("shows an already-assigned frozen test as selected but does not offer it for new selection", async () => {
    const { user } = renderPicker({ value: "OLD_TEST" });

    const input = await screen.findByDisplayValue("OLD_TEST: Retired Test");
    await user.click(input);

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
    const { onChange, user } = renderPicker();

    await user.click(screen.getByPlaceholderText(PLACEHOLDER));
    await screen.findByRole("option", { name: "TAMC: Total Aerobic Microbial Count" });
    await user.type(screen.getByPlaceholderText(PLACEHOLDER), "TYPO_CODE{Enter}");

    expect(masterDataOptions.createTestDefinition).not.toHaveBeenCalled();
    expect(onChange).not.toHaveBeenCalled();
  });
});
