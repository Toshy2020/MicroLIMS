import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { ItemSpecificationsSection } from "./ItemSpecificationsSection";
import type { TestDefinitionSummary } from "../specWizard/SpecificationWizard";
import type { Item } from "../services/ItemService";
import {
  SpecificationService,
  type SpecificationDto
} from "../../specifications/services/SpecificationService";
import {
  laboratorySectionService,
  type LaboratorySection
} from "../../../../services/laboratorySectionService";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { masterDataUsageService } from "../../../../services/masterDataUsageService";

vi.mock("../../specifications/services/SpecificationService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../specifications/services/SpecificationService")>();
  return {
    ...actual,
    SpecificationService: {
      ...actual.SpecificationService,
      getForItem: vi.fn(),
      update: vi.fn(),
      remove: vi.fn(),
      reinstate: vi.fn(),
      validate: vi.fn()
    }
  };
});

vi.mock("../../../../services/masterDataUsageService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/masterDataUsageService")>();
  return { ...actual, masterDataUsageService: { ...actual.masterDataUsageService, getUsage: vi.fn() } };
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

// Network boundary only: the change-control lookups. The prompt, the gate hook and the section are real.
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

const testItem: Item = {
  id: 77,
  version: 1,
  name: "Paracetamol Tablets",
  code: "FP-PARA",
  category: "FinishedProduct",
  sopNumber: "SOP-PARA",
  isActive: true,
  assignedTests: [{ testCode: "PH", displayName: "pH" }],
  specifications: []
};

const testDefinitions: TestDefinitionSummary[] = [
  { id: 1, code: "PH", displayName: "pH", workflowType: "Measurement", sectionId: 1 }
];

const activeSpec: SpecificationDto = {
  id: 100,
  version: 2,
  itemId: 77,
  testCode: "PH",
  parameterName: "pH",
  displayOrder: 0,
  limitType: "Range",
  lowerLimit: 5,
  upperLimit: 7,
  lowerInclusive: true,
  upperInclusive: true,
  unit: "pH",
  referenceStandard: null,
  resultBasis: null,
  isRetired: false,
  canEdit: true
};

const retiredSpec: SpecificationDto = { ...activeSpec, id: 101, isRetired: true };

function gateRefusal(code: string, message: string) {
  return { response: { status: 403, data: { success: false, message, errorDetails: [{ code, message }] } } };
}

function mountSection() {
  render(
    <PinnedLightTheme>
      <AuthProvider>
        <ItemSpecificationsSection item={testItem} />
      </AuthProvider>
    </PinnedLightTheme>
  );
}

describe("ItemSpecificationsSection change-control gate", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([fpSection]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([fpSection]);
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue(testDefinitions);
    vi.mocked(masterDataUsageService.getUsage).mockResolvedValue({ type: "Specification", id: 100, openWorkCount: 0, historicalCount: 0, lines: [] });
    lookup.getSectionSetting.mockResolvedValue({ sectionId: 1, changeControlRequired: false, version: 1 });
    lookup.listApproved.mockResolvedValue([]);
  });

  afterEach(cleanup);

  async function openReinstate(user: ReturnType<typeof userEvent.setup>) {
    mountSection();
    await user.click(await screen.findByRole("button", { name: "Reinstate" }));
    await user.type(screen.getByLabelText(/Reason for change/i), "Reinstated per QAR-2026-14 review");
  }

  it("reinstate refused by the gate opens the prompt and the retry sends the change control with the same arguments", async () => {
    vi.mocked(SpecificationService.getForItem).mockResolvedValue([retiredSpec]);
    vi.mocked(SpecificationService.reinstate)
      .mockRejectedValueOnce(gateRefusal("ChangeControl.GateRequired", "This laboratory requires an approved change control for this change."))
      .mockResolvedValueOnce({ ...activeSpec, id: 101 });
    const user = userEvent.setup();
    await openReinstate(user);

    await user.click(screen.getByRole("button", { name: "Reinstate" }));

    const prompt = await screen.findByRole("dialog", { name: "Change control needed" });
    expect(within(prompt).getByRole("alert").textContent).toContain("requires an approved change control");
    expect(lookup.listApproved).toHaveBeenCalledWith(1, expect.any(AbortSignal));
    expect(SpecificationService.reinstate).toHaveBeenCalledTimes(1);

    await user.type(within(prompt).getByRole("combobox", { name: "Approved change control" }), "cc-2026-0003");
    await user.click(within(prompt).getByRole("button", { name: "Retry with this change control" }));

    await waitFor(() => expect(SpecificationService.reinstate).toHaveBeenCalledTimes(2));
    expect(vi.mocked(SpecificationService.reinstate).mock.calls[0]).toEqual([101, "Reinstated per QAR-2026-14 review"]);
    expect(vi.mocked(SpecificationService.reinstate).mock.calls[1]).toEqual([101, "Reinstated per QAR-2026-14 review", "CC-2026-0003"]);
  });

  it("a refusal that is not the gate's does not open the prompt", async () => {
    vi.mocked(SpecificationService.getForItem).mockResolvedValue([retiredSpec]);
    vi.mocked(SpecificationService.reinstate).mockRejectedValueOnce({
      response: { status: 400, data: { message: "Something else.", errorDetails: [{ code: "Specification.Other", message: "Something else." }] } }
    });
    const user = userEvent.setup();
    await openReinstate(user);

    await user.click(screen.getByRole("button", { name: "Reinstate" }));

    expect(await screen.findByText("Something else.")).toBeTruthy();
    expect(screen.queryByRole("dialog", { name: "Change control needed" })).toBeNull();
  });

  it("delete asks for the change control before the first save when the laboratory setting is on", async () => {
    lookup.getSectionSetting.mockResolvedValue({ sectionId: 1, changeControlRequired: true, version: 3 });
    vi.mocked(SpecificationService.getForItem).mockResolvedValue([activeSpec]);
    vi.mocked(SpecificationService.remove).mockResolvedValue({ outcome: "deleted", id: 100 });
    const user = userEvent.setup();
    mountSection();
    await user.click(await screen.findByTitle("Delete Specification"));
    await user.type(screen.getByLabelText(/Reason for change/i), "Duplicate spec replaced by USP");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    const prompt = await screen.findByRole("dialog", { name: "Change control needed" });
    expect(SpecificationService.remove).not.toHaveBeenCalled();
    expect(lookup.getSectionSetting).toHaveBeenCalledWith(1, expect.anything());

    await user.type(within(prompt).getByRole("combobox", { name: "Approved change control" }), "CC-7");
    await user.click(within(prompt).getByRole("button", { name: "Save with this change control" }));

    await waitFor(() => expect(SpecificationService.remove).toHaveBeenCalledTimes(1));
    expect(SpecificationService.remove).toHaveBeenCalledWith(100, "Duplicate spec replaced by USP", "CC-7");
  });
});
