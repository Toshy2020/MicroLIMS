import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { ItemAssignedTestsSection } from "./ItemAssignedTestsSection";
import { ItemService, type Item } from "../services/ItemService";
import { masterDataOptions } from "../../../../services/masterDataOptions";

// Only the HTTP service layer is mocked; the real section, test picker and
// useTestDefinitions hook run (CLAUDE.md Rule 4.5).
vi.mock("../services/ItemService", () => ({
  ItemService: { update: vi.fn() }
}));

vi.mock("../../../../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/masterDataOptions")>();
  return {
    ...actual,
    masterDataOptions: { ...actual.masterDataOptions, getTestDefinitions: vi.fn(), createTestDefinition: vi.fn() }
  };
});

const item: Item = {
  id: 7,
  version: 3,
  name: "Zinc Oxide",
  code: "RM-ZINC",
  category: "RawMaterial",
  sopNumber: "SOP-12",
  isActive: true,
  assignedTests: [{ testCode: "ASSAY", displayName: "Assay by titration" }],
  specifications: [],
  minSampleQuantity: 5,
  minRetestSampleQuantity: 2,
  minSampleQuantityUnit: "g"
};

const testDefinitions = [
  { id: 1, code: "ASSAY", displayName: "Assay by titration", isActive: true, workflowType: "Titration" },
  { id: 2, code: "LOD", displayName: "Loss on drying", isActive: true, workflowType: "Gravimetric" }
];

function renderSection(props?: Partial<React.ComponentProps<typeof ItemAssignedTestsSection>>) {
  const onSaved = vi.fn();
  render(
    <PinnedLightTheme>
      <ItemAssignedTestsSection item={item} onSaved={onSaved} {...props} />
    </PinnedLightTheme>
  );
  return { onSaved };
}

describe("ItemAssignedTestsSection", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue(testDefinitions);
    vi.mocked(ItemService.update).mockResolvedValue(undefined);
  });

  afterEach(cleanup);

  it("lists the assigned tests and offers no edit control to a read-only viewer", () => {
    renderSection({ readOnly: true });

    const list = screen.getByRole("list", { name: "Assigned tests" });
    expect(within(list).getByText("Assay by titration")).toBeTruthy();
    expect(within(list).getByText("ASSAY")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Edit tests" })).toBeNull();
  });

  it("adds a test in place and saves the whole item with its row version", async () => {
    const user = userEvent.setup();
    const { onSaved } = renderSection();

    await user.click(screen.getByRole("button", { name: "Edit tests" }));
    await user.click(screen.getByRole("combobox", { name: "Assigned Tests" }));
    await user.click(await screen.findByRole("option", { name: "LOD: Loss on drying" }));
    await user.click(screen.getByRole("button", { name: "Save tests" }));

    await waitFor(() => expect(ItemService.update).toHaveBeenCalledTimes(1));
    expect(ItemService.update).toHaveBeenCalledWith(
      7,
      {
        name: "Zinc Oxide",
        code: "RM-ZINC",
        category: "RawMaterial",
        sopNumber: "SOP-12",
        assignedTests: [
          { testCode: "ASSAY", displayName: "Assay by titration" },
          { testCode: "LOD", displayName: "LOD" }
        ],
        minSampleQuantity: 5,
        minRetestSampleQuantity: 2,
        minSampleQuantityUnit: "g"
      },
      3
    );
    await waitFor(() => expect(onSaved).toHaveBeenCalledTimes(1));
    // Back to the read view.
    expect(screen.getByRole("button", { name: "Edit tests" })).toBeTruthy();
  });

  it("refuses to save with no test left and sends nothing", async () => {
    const user = userEvent.setup();
    const { onSaved } = renderSection();

    await user.click(screen.getByRole("button", { name: "Edit tests" }));
    // The picker shows the assigned test as a removable tag once the Test Master has loaded.
    await screen.findByRole("button", { name: "ASSAY" });
    await user.click(screen.getByRole("combobox", { name: "Assigned Tests" }));
    await user.keyboard("{Backspace}");
    await user.click(screen.getByRole("button", { name: "Save tests" }));

    expect(await screen.findByText("At least one assigned test is required.")).toBeTruthy();
    expect(ItemService.update).not.toHaveBeenCalled();
    expect(onSaved).not.toHaveBeenCalled();
  });

  it("shows the server's refusal and stays in edit mode", async () => {
    vi.mocked(ItemService.update).mockRejectedValue({
      response: { data: { message: "The record was changed by another user." } }
    });
    const user = userEvent.setup();
    const { onSaved } = renderSection();

    await user.click(screen.getByRole("button", { name: "Edit tests" }));
    await user.click(screen.getByRole("button", { name: "Save tests" }));

    expect(await screen.findByText("The record was changed by another user.")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Save tests" })).toBeTruthy();
    expect(onSaved).not.toHaveBeenCalled();
  });

  it("cancel leaves the item untouched", async () => {
    const user = userEvent.setup();
    renderSection();

    await user.click(screen.getByRole("button", { name: "Edit tests" }));
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    expect(ItemService.update).not.toHaveBeenCalled();
    expect(screen.getByRole("list", { name: "Assigned tests" })).toBeTruthy();
  });
});
