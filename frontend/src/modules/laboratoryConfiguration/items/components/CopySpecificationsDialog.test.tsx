import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { ItemSpecificationsSection } from "./ItemSpecificationsSection";
import type { TestDefinitionSummary } from "./SpecificationParameterDialog";
import { Item, ItemService } from "../services/ItemService";
import {
  SpecificationService,
  type CreateSpecificationPayload,
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
      create: vi.fn(),
      update: vi.fn(),
      remove: vi.fn(),
      getForItem: vi.fn()
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
const RAW_ID = 9;

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
    { testCode: "MICRO", displayName: "Microbial count" }
  ],
  specifications: []
};
const sourceItem: Item = { ...targetItem, id: SOURCE_ID, name: "Oral Tablet B", code: "FP-B", assignedTests: [], specifications: [] };
const rawItem: Item = { ...targetItem, id: RAW_ID, name: "Starch", code: "RM-C", category: "RawMaterial", assignedTests: [], specifications: [] };

const testDefinitions: TestDefinitionSummary[] = [
  { id: 1, code: "PH", displayName: "pH", workflowType: "Measurement", sectionId: 1 },
  { id: 2, code: "ASSAY", displayName: "Assay", workflowType: "Measurement", sectionId: 1 },
  { id: 3, code: "DISS", displayName: "Dissolution", workflowType: "Dissolution", sectionId: 1 },
  { id: 4, code: "MICRO", displayName: "Microbial count", workflowType: "CountTest", sectionId: 2 }
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
const notAssignedSpec: SpecificationDto = {
  id: 104,
  version: 1,
  itemId: SOURCE_ID,
  testCode: "HEAVY",
  parameterName: "Heavy metals",
  displayOrder: 0,
  limitType: "NotMoreThan",
  upperLimit: 10,
  unit: "ppm"
};
const microSpec: SpecificationDto = {
  id: 105,
  version: 1,
  itemId: SOURCE_ID,
  testCode: "MICRO",
  parameterName: "Microbial count",
  displayOrder: 0,
  limitType: "CountTiered",
  alertLimit: "50",
  actionLimit: "80",
  specLimit: "100"
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
const sourceSpecs: SpecificationDto[] = [phSpec, assaySpec, dissSpec, notAssignedSpec, microSpec, ph2Spec];
const targetSpecs: SpecificationDto[] = [{ ...assaySpec, id: 900, itemId: TARGET_ID }];

const NULLS = {
  referenceStandard: null,
  target: null,
  tolerance: null,
  toleranceMode: null,
  expectedResultText: null,
  expectedState: null,
  sampleQuantity: null,
  sampleQuantityUnit: null,
  alertLimit: null,
  actionLimit: null,
  specLimit: null,
  dilutionFactor: null,
  resultBasis: null,
  labelClaim: null,
  labelClaimUnit: null,
  dosageForm: null,
  hplcMethodAnalyteId: null,
  hplcMethodImpurityId: null,
  relatedSubstanceRow: null,
  icpTestElementId: null,
  productionStageRole: null,
  grade: null
};

const phPayload: CreateSpecificationPayload = {
  ...NULLS,
  itemId: TARGET_ID,
  testCode: "PH",
  parameterName: "pH",
  displayOrder: 0,
  limitType: "Range",
  lowerLimit: 5,
  upperLimit: 7,
  lowerInclusive: true,
  upperInclusive: false,
  unit: "pH"
};
const dissPayload: CreateSpecificationPayload = {
  ...NULLS,
  itemId: TARGET_ID,
  testCode: "DISS",
  parameterName: "Dissolution",
  displayOrder: 0,
  limitType: "DissolutionQ",
  lowerLimit: 80,
  upperLimit: null,
  labelClaim: 500,
  labelClaimUnit: "mg",
  specLimit: "Q = 80 %",
  unit: null
};
const ph2Payload: CreateSpecificationPayload = {
  ...NULLS,
  itemId: TARGET_ID,
  testCode: "PH",
  parameterName: "pH (1 % solution)",
  displayOrder: 1,
  limitType: "Range",
  lowerLimit: 4.5,
  upperLimit: 6.5,
  lowerInclusive: true,
  upperInclusive: true,
  unit: null
};

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
    vi.mocked(ItemService.getAll).mockResolvedValue([targetItem, sourceItem, rawItem]);
    vi.mocked(SpecificationService.getForItem).mockImplementation(async (itemId: number) => {
      if (itemId === SOURCE_ID) return sourceSpecs;
      if (itemId === TARGET_ID) return targetSpecs;
      return [];
    });
    vi.mocked(SpecificationService.create).mockImplementation(async (payload) => ({ ...payload, id: 5000 }) as SpecificationDto);
  });

  afterEach(cleanup);

  it("lists only the eligible parameters and counts what was skipped and why", async () => {
    const user = userEvent.setup();
    mount();
    await openAndPick(user, "Oral Tablet B (FP-B)");

    const ui = dialogUi();
    const boxes = await ui.findAllByRole("checkbox");
    expect(boxes.map((b) => b.getAttribute("aria-label"))).toEqual(["Copy pH", "Copy Dissolution", "Copy pH – pH (1 % solution)"]);
    expect(boxes.every((b) => (b as HTMLInputElement).checked)).toBe(true);
    // The limits read as they do in the table.
    expect(ui.getByText("NLT 5 — NMT 7 pH")).toBeTruthy();
    expect(ui.getByText("Q = 80 %")).toBeTruthy();
    expect(ui.getByText("1 parameter is not assigned to this item")).toBeTruthy();
    expect(ui.getByText("1 parameter already has limits here")).toBeTruthy();
    expect(ui.getByText("1 parameter belongs to another laboratory")).toBeTruthy();
    expect(ui.getByRole("button", { name: "Copy 3 parameters" })).toBeTruthy();
    expect(SpecificationService.create).not.toHaveBeenCalled();
  });

  it("shows how many assigned tests already have limits", async () => {
    mount();
    expect(await screen.findByText(/Limits set for 1 of\s+4 assigned tests/)).toBeTruthy();
  });

  it("creates one specification per ticked row in list order, then closes and refreshes", async () => {
    const user = userEvent.setup();
    const onSpecsChanged = vi.fn();
    mount({ onSpecsChanged });
    await openAndPick(user, "Oral Tablet B (FP-B)");

    await user.click(await dialogUi().findByRole("checkbox", { name: "Copy Dissolution" }));
    await user.click(dialogUi().getByRole("button", { name: "Copy 2 parameters" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(vi.mocked(SpecificationService.create).mock.calls).toEqual([[phPayload], [ph2Payload]]);
    expect(onSpecsChanged).toHaveBeenCalledTimes(1);
    // Once for the section's own load, once more for the refresh after the copy.
    expect(vi.mocked(SpecificationService.getForItem).mock.calls.filter(([id]) => id === TARGET_ID).length).toBeGreaterThanOrEqual(3);
    expect(await screen.findByText("Copied 2 specification parameters to Oral Tablet A.")).toBeTruthy();
  });

  it("stops at a refused row, keeps the created ones, and sends only the rest on retry", async () => {
    const user = userEvent.setup();
    const onSpecsChanged = vi.fn();
    vi.mocked(SpecificationService.create)
      .mockResolvedValueOnce({ ...phSpec, id: 5001 })
      .mockRejectedValueOnce({ response: { data: { message: "Q must not exceed 100." } } })
      .mockResolvedValue({ ...phSpec, id: 5002 });
    mount({ onSpecsChanged });
    await openAndPick(user, "Oral Tablet B (FP-B)");

    await user.click(await dialogUi().findByRole("button", { name: "Copy 3 parameters" }));

    const ui = dialogUi();
    expect(await ui.findByText(/Copying stopped at "Dissolution" \(DISS\): Q must not exceed 100\./)).toBeTruthy();
    expect(screen.getByRole("dialog")).toBeTruthy();
    expect(vi.mocked(SpecificationService.create).mock.calls).toEqual([[phPayload], [dissPayload]]);
    expect(ui.getByText("Copied")).toBeTruthy();
    expect((ui.getByRole("checkbox", { name: "Copy pH" }) as HTMLInputElement).disabled).toBe(true);
    // The saved row is shown without waiting for the dialog to close.
    await waitFor(() => expect(onSpecsChanged).toHaveBeenCalledTimes(1));

    await user.click(ui.getByRole("button", { name: "Copy 2 parameters" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    // pH is not sent again; the order continues from the first attempt.
    expect(vi.mocked(SpecificationService.create).mock.calls).toEqual([[phPayload], [dissPayload], [dissPayload], [ph2Payload]]);
    expect(await screen.findByText("Copied 3 specification parameters to Oral Tablet A.")).toBeTruthy();
  });

  it("sends nothing when the dialog is cancelled", async () => {
    const user = userEvent.setup();
    mount();
    await openAndPick(user, "Oral Tablet B (FP-B)");
    await dialogUi().findAllByRole("checkbox");

    await user.click(dialogUi().getByRole("button", { name: "Cancel" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(SpecificationService.create).not.toHaveBeenCalled();
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

  it("offers nothing from an item of another category and says why", async () => {
    const user = userEvent.setup();
    mount();
    await openAndPick(user, "Starch (RM-C)");

    expect(await dialogUi().findByText(/is a different kind of item from Oral Tablet A/)).toBeTruthy();
    expect(dialogUi().queryAllByRole("checkbox")).toHaveLength(0);
    expect((dialogUi().getByRole("button", { name: "Copy 0 parameters" }) as HTMLButtonElement).disabled).toBe(true);
    expect(SpecificationService.create).not.toHaveBeenCalled();
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
