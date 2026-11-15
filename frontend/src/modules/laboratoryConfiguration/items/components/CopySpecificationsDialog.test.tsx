import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { ItemSpecificationsSection } from "./ItemSpecificationsSection";
import type { TestDefinitionSummary } from "../specWizard/specHelpers";
import { Item, ItemService } from "../services/ItemService";
import {
  SpecificationService,
  type CopySpecificationsPreviewRow,
  type SpecificationDto
} from "../../specifications/services/SpecificationService";
import { laboratorySectionService, type LaboratorySection } from "../../../../services/laboratorySectionService";
import { masterDataOptions } from "../../../../services/masterDataOptions";

// Only the HTTP service layer is mocked; the real section, the real copy
// dialog, the real hooks and the real AuthProvider run (CLAUDE.md Rule 4.5).
vi.mock("../services/ItemService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../services/ItemService")>();
  return { ...actual, ItemService: { ...actual.ItemService, getAll: vi.fn() } };
});

vi.mock("../../specifications/services/SpecificationService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../specifications/services/SpecificationService")>();
  return {
    ...actual,
    SpecificationService: {
      ...actual.SpecificationService,
      getForItem: vi.fn(),
      previewCopy: vi.fn(),
      copySpecifications: vi.fn()
    }
  };
});

vi.mock("../../../../services/laboratorySectionService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/laboratorySectionService")>();
  return {
    ...actual,
    getSections: vi.fn(),
    getMySections: vi.fn(),
    laboratorySectionService: {
      ...actual.laboratorySectionService,
      getSections: vi.fn(),
      getMySections: vi.fn()
    }
  };
});

vi.mock("../../../../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/masterDataOptions")>();
  return { ...actual, masterDataOptions: { ...actual.masterDataOptions, getTestDefinitions: vi.fn() } };
});

const fpSection: LaboratorySection = {
  sectionId: 1,
  sectionCode: "FP",
  sectionName: "Finished Product",
  departmentId: 1,
  departmentCode: "QC",
  departmentName: "Quality Control"
};

const TARGET_ID = 7;
const SOURCE_ID = 8;
const EMPTY_SOURCE_ID = 9;

const targetItem: Item = {
  id: TARGET_ID,
  version: 2,
  name: "Oral Tablet A",
  code: "FP-A",
  category: "FinishedProduct",
  sopNumber: "SOP-1",
  isActive: true,
  assignedTests: [
    { testCode: "PH", displayName: "pH" },
    { testCode: "ASSAY", displayName: "Assay" },
    { testCode: "DISS", displayName: "Dissolution" },
    { testCode: "STAGE", displayName: "Stage Check" }
  ],
  specifications: []
};
const sourceItem: Item = { ...targetItem, id: SOURCE_ID, name: "Oral Tablet B", code: "FP-B", assignedTests: [], specifications: [] };
const emptySourceItem: Item = { ...targetItem, id: EMPTY_SOURCE_ID, name: "Empty Item", code: "FP-E", assignedTests: [], specifications: [] };

const testDefinitions: TestDefinitionSummary[] = [
  { id: 1, code: "PH", displayName: "pH", workflowType: "Measurement", sectionId: 1 },
  { id: 2, code: "ASSAY", displayName: "Assay", workflowType: "Measurement", sectionId: 1 },
  { id: 3, code: "DISS", displayName: "Dissolution", workflowType: "Dissolution", sectionId: 1 },
  { id: 4, code: "STAGE", displayName: "Stage Check", workflowType: "Measurement", sectionId: 1 }
];

const phSpec: SpecificationDto = {
  id: 101,
  version: 4,
  itemId: SOURCE_ID,
  testCode: "PH",
  parameterName: "pH",
  displayOrder: 0,
  limitType: "Range",
  lowerLimit: 5,
  upperLimit: 7,
  lowerInclusive: true,
  upperInclusive: false,
  unit: "pH",
  canEdit: true,
  sectionName: "Finished Product"
};
const assaySpec: SpecificationDto = {
  id: 102,
  version: 1,
  itemId: SOURCE_ID,
  testCode: "ASSAY",
  parameterName: "Assay",
  displayOrder: 0,
  limitType: "NotLessThan",
  lowerLimit: 98,
  unit: "%"
};
const dissSpec: SpecificationDto = {
  id: 103,
  version: 1,
  itemId: SOURCE_ID,
  testCode: "DISS",
  parameterName: "Dissolution",
  displayOrder: 0,
  limitType: "DissolutionQ",
  lowerLimit: 80,
  labelClaim: 500,
  labelClaimUnit: "mg",
  specLimit: "Q = 80 %"
};
const stageInvalidSpec: SpecificationDto = {
  id: 104,
  version: 1,
  itemId: SOURCE_ID,
  testCode: "STAGE",
  parameterName: "Stage Check",
  displayOrder: 0,
  limitType: "NotMoreThan",
  upperLimit: 10,
  unit: "ppm"
};
const ph2Spec: SpecificationDto = {
  id: 106,
  version: 1,
  itemId: SOURCE_ID,
  testCode: "PH",
  parameterName: "pH (1 % solution)",
  displayOrder: 1,
  limitType: "Range",
  lowerLimit: 4.5,
  upperLimit: 6.5,
  lowerInclusive: true,
  upperInclusive: true
};

const sourceSpecs: SpecificationDto[] = [phSpec, assaySpec, dissSpec, stageInvalidSpec, ph2Spec];
const targetSpecs: SpecificationDto[] = [{ ...assaySpec, id: 900, itemId: TARGET_ID }];

const defaultPreviewRows: CopySpecificationsPreviewRow[] = [
  { sourceSpecificationId: 101, testCode: "PH", parameterName: "pH", status: "Ok", message: null },
  {
    sourceSpecificationId: 102,
    testCode: "ASSAY",
    parameterName: "Assay",
    status: "AlreadyPresent",
    message: "A matching specification already exists on the target item."
  },
  { sourceSpecificationId: 103, testCode: "DISS", parameterName: "Dissolution", status: "Ok", message: null },
  {
    sourceSpecificationId: 104,
    testCode: "STAGE",
    parameterName: "Stage Check",
    status: "Invalid",
    message: "Production stage Finished is not valid for raw materials."
  },
  { sourceSpecificationId: 106, testCode: "PH", parameterName: "pH (1 % solution)", status: "Ok", message: null }
];

function mount(options: { readOnly?: boolean; onSpecsChanged?: () => void } = {}) {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <ItemSpecificationsSection item={targetItem} readOnly={options.readOnly} onSpecsChanged={options.onSpecsChanged} />
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const copyButton = () => screen.getByRole("button", { name: "Copy from another item" });
const dialogUi = () => within(screen.getByRole("dialog"));

async function openAndPick(user: ReturnType<typeof userEvent.setup>, optionName: string) {
  await user.click(await screen.findByRole("button", { name: "Copy from another item" }));
  await user.click(await dialogUi().findByRole("combobox", { name: "Copy from item" }));
  await user.click(await screen.findByRole("option", { name: optionName }));
}

describe("CopySpecificationsDialog (through ItemSpecificationsSection)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([fpSection]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([fpSection]);
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue(testDefinitions);
    vi.mocked(ItemService.getAll).mockResolvedValue([targetItem, sourceItem, emptySourceItem]);
    vi.mocked(SpecificationService.getForItem).mockImplementation(async (itemId: number) => {
      if (itemId === SOURCE_ID) return sourceSpecs;
      if (itemId === TARGET_ID) return targetSpecs;
      return [];
    });
    vi.mocked(SpecificationService.previewCopy).mockImplementation(async (req) => {
      if (req.sourceItemId === EMPTY_SOURCE_ID) return [];
      if (req.specificationIds.length === 0) return defaultPreviewRows;
      const wanted = new Set(req.specificationIds);
      return defaultPreviewRows.filter((r) => wanted.has(r.sourceSpecificationId));
    });
    vi.mocked(SpecificationService.copySpecifications).mockImplementation(async (req) => req.specificationIds.length);
  });

  afterEach(cleanup);

  it("choose source, tick tests, preview with statuses, Save disabled while any ticked row is Invalid", async () => {
    const user = userEvent.setup();
    mount();
    await openAndPick(user, "Oral Tablet B (FP-B)");

    const ui = dialogUi();
    await ui.findByRole("checkbox", { name: "Copy pH" });

    expect(SpecificationService.previewCopy).toHaveBeenCalledWith({
      sourceItemId: SOURCE_ID,
      targetItemId: TARGET_ID,
      specificationIds: []
    });

    // Statuses are shown as text, not color alone, with the server message.
    expect(ui.getAllByText("Ok")).toHaveLength(3);
    expect(ui.getByText("Already present")).toBeTruthy();
    expect(ui.getByText("A matching specification already exists on the target item.")).toBeTruthy();
    expect(ui.getByText("Invalid")).toBeTruthy();
    expect(ui.getByText("Production stage Finished is not valid for raw materials.")).toBeTruthy();

    // The limits read as they do in the table.
    expect(ui.getByText("NLT 5 — NMT 7 pH")).toBeTruthy();
    expect(ui.getByText("Q = 80 %")).toBeTruthy();

    // AlreadyPresent is unticked and disabled; Ok rows are ticked; Invalid starts unticked.
    const assayBox = ui.getByRole("checkbox", { name: "Copy Assay" }) as HTMLInputElement;
    const stageBox = ui.getByRole("checkbox", { name: "Copy Stage Check" }) as HTMLInputElement;
    const phBox = ui.getByRole("checkbox", { name: "Copy pH" }) as HTMLInputElement;
    expect(assayBox.checked).toBe(false);
    expect(assayBox.disabled).toBe(true);
    expect(phBox.checked).toBe(true);
    expect(stageBox.checked).toBe(false);

    const saveBtn = ui.getByRole("button", { name: "Save" }) as HTMLButtonElement;
    expect(saveBtn.disabled).toBe(false);

    // Ticking the Invalid row disables Save.
    await user.click(stageBox);
    expect(stageBox.checked).toBe(true);
    expect(saveBtn.disabled).toBe(true);

    // Explicit Preview calls previewCopy for the ticked specificationIds.
    await user.click(ui.getByRole("button", { name: "Preview" }));
    await waitFor(() =>
      expect(SpecificationService.previewCopy).toHaveBeenLastCalledWith({
        sourceItemId: SOURCE_ID,
        targetItemId: TARGET_ID,
        specificationIds: [101, 103, 104, 106]
      })
    );
    expect(saveBtn.disabled).toBe(true);

    // Unticking the Invalid row re-enables Save.
    await user.click(stageBox);
    expect(stageBox.checked).toBe(false);
    expect(saveBtn.disabled).toBe(false);
    expect(SpecificationService.copySpecifications).not.toHaveBeenCalled();
  });

  it("shows how many assigned tests already have limits", async () => {
    mount();
    expect(await screen.findByText(/Limits set for 1 of\s+4 assigned tests/)).toBeTruthy();
  });

  it("Save sends { sourceItemId, targetItemId, specificationIds }, shows created count, closes and refreshes", async () => {
    const user = userEvent.setup();
    const onSpecsChanged = vi.fn();
    vi.mocked(SpecificationService.copySpecifications).mockResolvedValueOnce(2);
    mount({ onSpecsChanged });
    await openAndPick(user, "Oral Tablet B (FP-B)");

    await user.click(await dialogUi().findByRole("checkbox", { name: "Copy Dissolution" }));
    await user.click(dialogUi().getByRole("button", { name: "Save" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(SpecificationService.copySpecifications).toHaveBeenCalledWith({
      sourceItemId: SOURCE_ID,
      targetItemId: TARGET_ID,
      specificationIds: [101, 106]
    });
    expect(onSpecsChanged).toHaveBeenCalledTimes(1);
    // Once for the section's initial load, once more for the refresh after the copy.
    expect(vi.mocked(SpecificationService.getForItem).mock.calls.filter(([id]) => id === TARGET_ID).length).toBeGreaterThanOrEqual(2);
    expect(await screen.findByText("Copied 2 specification parameters to Oral Tablet A.")).toBeTruthy();
  });

  it("keeps the dialog open and shows the server message when copy fails, allowing retry", async () => {
    const user = userEvent.setup();
    const onSpecsChanged = vi.fn();
    vi.mocked(SpecificationService.copySpecifications)
      .mockRejectedValueOnce({ response: { data: { message: "Target item belongs to a section you cannot configure." } } })
      .mockResolvedValueOnce(3);
    mount({ onSpecsChanged });
    await openAndPick(user, "Oral Tablet B (FP-B)");

    await user.click(await dialogUi().findByRole("button", { name: "Save" }));

    const ui = dialogUi();
    expect(await ui.findByText("Target item belongs to a section you cannot configure.")).toBeTruthy();
    expect(screen.getByRole("dialog")).toBeTruthy();
    expect(onSpecsChanged).not.toHaveBeenCalled();

    await user.click(ui.getByRole("button", { name: "Save" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(onSpecsChanged).toHaveBeenCalledTimes(1);
    expect(await screen.findByText("Copied 3 specification parameters to Oral Tablet A.")).toBeTruthy();
  });

  it("sends nothing when the dialog is cancelled", async () => {
    const user = userEvent.setup();
    mount();
    await openAndPick(user, "Oral Tablet B (FP-B)");
    await dialogUi().findAllByRole("checkbox");

    await user.click(dialogUi().getByRole("button", { name: "Cancel" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(SpecificationService.copySpecifications).not.toHaveBeenCalled();
  });

  it("reports a failed item list and loads it on Retry", async () => {
    const user = userEvent.setup();
    vi.mocked(ItemService.getAll).mockRejectedValueOnce({ response: { data: { message: "Items are unavailable." } } });
    mount();
    await user.click(await screen.findByRole("button", { name: "Copy from another item" }));

    expect(await dialogUi().findByText(/Items could not be loaded: Items are unavailable\./)).toBeTruthy();
    expect(dialogUi().queryByRole("combobox")).toBeNull();

    await user.click(dialogUi().getByRole("button", { name: "Retry" }));
    await user.click(await dialogUi().findByRole("combobox", { name: "Copy from item" }));
    expect(await screen.findByRole("option", { name: "Oral Tablet B (FP-B)" })).toBeTruthy();
    expect(ItemService.getAll).toHaveBeenCalledTimes(2);
  });

  it("reports a failed preview and loads it on Retry", async () => {
    const user = userEvent.setup();
    vi.mocked(SpecificationService.previewCopy).mockRejectedValueOnce({
      response: { data: { message: "Preview failed." } }
    });
    mount();
    await openAndPick(user, "Oral Tablet B (FP-B)");

    expect(await dialogUi().findByText(/The preview for Oral Tablet B could not be loaded: Preview failed\./)).toBeTruthy();

    await user.click(dialogUi().getByRole("button", { name: "Retry" }));
    expect(await dialogUi().findByRole("checkbox", { name: "Copy pH" })).toBeTruthy();
  });

  it("shows an empty message when the source item has no active specifications", async () => {
    const user = userEvent.setup();
    mount();
    await openAndPick(user, "Empty Item (FP-E)");

    expect(await dialogUi().findByText(/Nothing on Empty Item can be copied to Oral Tablet A\./)).toBeTruthy();
    expect(dialogUi().queryAllByRole("checkbox")).toHaveLength(0);
    expect((dialogUi().getByRole("button", { name: "Save" }) as HTMLButtonElement).disabled).toBe(true);
    expect(SpecificationService.copySpecifications).not.toHaveBeenCalled();
  });

  it("does not offer the target item itself as a source", async () => {
    const user = userEvent.setup();
    mount();
    await user.click(await screen.findByRole("button", { name: "Copy from another item" }));
    await user.click(await dialogUi().findByRole("combobox", { name: "Copy from item" }));

    expect(await screen.findByRole("option", { name: "Oral Tablet B (FP-B)" })).toBeTruthy();
    expect(screen.queryByRole("option", { name: "Oral Tablet A (FP-A)" })).toBeNull();
  });

  it("has no entry point for a read-only viewer", async () => {
    mount({ readOnly: true });
    await screen.findByText(/Limits set for/);
    expect(screen.queryByRole("button", { name: "Copy from another item" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Add Specification Parameter" })).toBeNull();
  });

  it("has the entry point next to Add Specification Parameter for an editor", async () => {
    mount();
    expect(await screen.findByRole("button", { name: "Add Specification Parameter" })).toBeTruthy();
    expect(copyButton()).toBeTruthy();
  });
});
