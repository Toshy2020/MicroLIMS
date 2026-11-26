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

// Network boundaries only: the item, specification, section and change-control services.
vi.mock("../services/ItemService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../services/ItemService")>();
  return { ...actual, ItemService: { ...actual.ItemService, getAll: vi.fn() } };
});
vi.mock("../../specifications/services/SpecificationService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../specifications/services/SpecificationService")>();
  return {
    ...actual,
    SpecificationService: { ...actual.SpecificationService, getForItem: vi.fn(), previewCopy: vi.fn(), copySpecifications: vi.fn() }
  };
});
vi.mock("../../../../services/laboratorySectionService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/laboratorySectionService")>();
  return {
    ...actual,
    getSections: vi.fn(),
    getMySections: vi.fn(),
    laboratorySectionService: { ...actual.laboratorySectionService, getSections: vi.fn(), getMySections: vi.fn() }
  };
});
vi.mock("../../../../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/masterDataOptions")>();
  return { ...actual, masterDataOptions: { ...actual.masterDataOptions, getTestDefinitions: vi.fn() } };
});
const lookup = vi.hoisted(() => ({ listApproved: vi.fn(), getSectionSetting: vi.fn() }));
vi.mock("../../../../services/changeControlLookupService", () => ({ changeControlLookupService: lookup }));

const fpSection: LaboratorySection = {
  sectionId: 1,
  sectionCode: "FP",
  sectionName: "Finished Product",
  departmentId: 1,
  departmentCode: "QC",
  departmentName: "Quality Control"
};

const targetItem: Item = {
  id: 7,
  version: 2,
  name: "Oral Tablet A",
  code: "FP-A",
  category: "FinishedProduct",
  sopNumber: "SOP-1",
  isActive: true,
  assignedTests: [{ testCode: "PH", displayName: "pH" }],
  specifications: []
};
const sourceItem: Item = { ...targetItem, id: 8, name: "Oral Tablet B", code: "FP-B", assignedTests: [], specifications: [] };
const testDefinitions: TestDefinitionSummary[] = [{ id: 1, code: "PH", displayName: "pH", workflowType: "Measurement", sectionId: 1 }];
const phSpec: SpecificationDto = {
  id: 101, version: 4, itemId: 8, testCode: "PH", parameterName: "pH", displayOrder: 0, limitType: "Range",
  lowerLimit: 5, upperLimit: 7, lowerInclusive: true, upperInclusive: false, unit: "pH", canEdit: true, sectionName: "Finished Product"
};
const previewRows: CopySpecificationsPreviewRow[] = [
  { sourceSpecificationId: 101, testCode: "PH", parameterName: "pH", status: "Ok", message: null }
];

function gateRefusal(code: string, message: string) {
  return { response: { status: 403, data: { success: false, message, errorDetails: [{ code, message }] } } };
}

async function openAndSave(user: ReturnType<typeof userEvent.setup>) {
  render(
    <PinnedLightTheme>
      <AuthProvider>
        <ItemSpecificationsSection item={targetItem} />
      </AuthProvider>
    </PinnedLightTheme>
  );
  await user.click(await screen.findByRole("button", { name: "Copy from another item" }));
  const copyDialog = screen.getByRole("dialog", { name: /Copy/ });
  await user.click(await within(copyDialog).findByRole("combobox", { name: "Copy from item" }));
  await user.click(await screen.findByRole("option", { name: "Oral Tablet B (FP-B)" }));
  await within(copyDialog).findByRole("checkbox", { name: "Copy pH" });
  await user.click(within(copyDialog).getByRole("button", { name: "Save" }));
}

describe("CopySpecificationsDialog change-control gate", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([fpSection]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([fpSection]);
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue(testDefinitions);
    vi.mocked(ItemService.getAll).mockResolvedValue([targetItem, sourceItem]);
    vi.mocked(SpecificationService.getForItem).mockImplementation(async (itemId: number) => (itemId === 8 ? [phSpec] : []));
    vi.mocked(SpecificationService.previewCopy).mockResolvedValue(previewRows);
    lookup.getSectionSetting.mockResolvedValue({ sectionId: 1, changeControlRequired: false, version: 1 });
    lookup.listApproved.mockResolvedValue([]);
  });

  afterEach(cleanup);

  it("a gate refusal opens the prompt and Retry repeats the same copy request with the change control", async () => {
    vi.mocked(SpecificationService.copySpecifications)
      .mockRejectedValueOnce(gateRefusal("ChangeControl.GateRequired", "This laboratory requires an approved change control for this change."))
      .mockResolvedValueOnce(1);
    const user = userEvent.setup();
    await openAndSave(user);

    const prompt = await screen.findByRole("dialog", { name: "Change control needed" });
    expect(within(prompt).getByRole("alert").textContent).toContain("requires an approved change control");
    expect(lookup.listApproved).toHaveBeenCalledWith(null, expect.any(AbortSignal));
    expect(SpecificationService.copySpecifications).toHaveBeenCalledTimes(1);

    await user.type(within(prompt).getByRole("combobox", { name: "Approved change control" }), "cc-2026-0009");
    await user.click(within(prompt).getByRole("button", { name: "Retry with this change control" }));

    await waitFor(() => expect(SpecificationService.copySpecifications).toHaveBeenCalledTimes(2));
    const request = { sourceItemId: 8, targetItemId: 7, specificationIds: [101] };
    expect(vi.mocked(SpecificationService.copySpecifications).mock.calls[0]).toEqual([request]);
    expect(vi.mocked(SpecificationService.copySpecifications).mock.calls[1]).toEqual([request, "CC-2026-0009"]);
  });

  it("cancelling the prompt copies nothing more and shows the cancel text", async () => {
    vi.mocked(SpecificationService.copySpecifications).mockRejectedValueOnce(
      gateRefusal("ChangeControl.GateRequired", "This laboratory requires an approved change control for this change.")
    );
    const user = userEvent.setup();
    await openAndSave(user);
    const prompt = await screen.findByRole("dialog", { name: "Change control needed" });

    await user.click(within(prompt).getByRole("button", { name: "Cancel" }));

    expect(await screen.findByText("Not saved: no change control was chosen.")).toBeTruthy();
    expect(SpecificationService.copySpecifications).toHaveBeenCalledTimes(1);
  });
});
