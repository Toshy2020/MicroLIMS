import { AxiosError, AxiosHeaders } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { ItemSpecificationsSection } from "./ItemSpecificationsSection";
import {
  SpecificationParameterDialog,
  type TestDefinitionSummary
} from "./SpecificationParameterDialog";
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

vi.mock("../../specifications/services/SpecificationService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../specifications/services/SpecificationService")>();
  return {
    ...actual,
    SpecificationService: {
      ...actual.SpecificationService,
      getForItem: vi.fn(),
      update: vi.fn(),
      remove: vi.fn(),
      reinstate: vi.fn()
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
  return {
    ...actual,
    masterDataOptions: {
      ...actual.masterDataOptions,
      getTestDefinitions: vi.fn()
    }
  };
});

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

const retiredSpec: SpecificationDto = {
  ...activeSpec,
  id: 101,
  isRetired: true
};

function makeReasonRequiredError(): AxiosError {
  return new AxiosError(
    "Reason required",
    "ERR_BAD_REQUEST",
    undefined,
    undefined,
    {
      data: {
        success: false,
        message: "A reason of at least 10 characters is required when active laboratory work exists.",
        fieldErrors: [{ field: "reason", message: "Reason must be at least 10 characters." }]
      },
      status: 400,
      statusText: "Bad Request",
      headers: {},
      config: { headers: new AxiosHeaders() }
    }
  );
}

describe("ItemSpecificationsSection retirement & change reason", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([fpSection]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([fpSection]);
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue(testDefinitions);
  });

  afterEach(cleanup);

  it("opens ChangeReasonDialog on delete and shows retired vs deleted outcomes", async () => {
    vi.mocked(SpecificationService.getForItem).mockResolvedValue([activeSpec]);
    vi.mocked(SpecificationService.remove).mockResolvedValueOnce({ outcome: "retired", id: 100 });

    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <AuthProvider>
          <ItemSpecificationsSection item={testItem} />
        </AuthProvider>
      </PinnedLightTheme>
    );

    const deleteBtn = await screen.findByTitle("Delete Specification");
    await user.click(deleteBtn);

    const confirmBtn = screen.getByRole("button", { name: "Confirm" }) as HTMLButtonElement;
    expect(confirmBtn.disabled).toBe(true);

    await user.type(screen.getByLabelText(/Reason for change/i), "Duplicate spec replaced by USP");
    expect(confirmBtn.disabled).toBe(false);
    await user.click(confirmBtn);

    await waitFor(() =>
      expect(SpecificationService.remove).toHaveBeenCalledWith(100, "Duplicate spec replaced by USP")
    );
    expect(await screen.findByText(/was retired because results are recorded against it/i)).toBeTruthy();

    vi.mocked(SpecificationService.remove).mockResolvedValueOnce({ outcome: "deleted", id: 100 });
    await user.click(await screen.findByTitle("Delete Specification"));
    await user.type(screen.getByLabelText(/Reason for change/i), "Unused spec added by mistake");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() =>
      expect(SpecificationService.remove).toHaveBeenCalledWith(100, "Unused spec added by mistake")
    );
    expect(await screen.findByText(/Specification "pH" was deleted\./i)).toBeTruthy();
  });

  it("shows Retired mark and opens ChangeReasonDialog to reinstate a retired specification", async () => {
    vi.mocked(SpecificationService.getForItem).mockResolvedValue([retiredSpec]);
    vi.mocked(SpecificationService.reinstate).mockResolvedValue({ ...activeSpec, id: 101 });

    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <AuthProvider>
          <ItemSpecificationsSection item={testItem} />
        </AuthProvider>
      </PinnedLightTheme>
    );

    expect(await screen.findByText("Retired")).toBeTruthy();
    const reinstateBtn = screen.getByRole("button", { name: "Reinstate" });
    await user.click(reinstateBtn);

    await user.type(screen.getByLabelText(/Reason for change/i), "Reinstated per QAR-2026-14 review");
    await user.click(screen.getByRole("button", { name: "Reinstate" }));

    await waitFor(() =>
      expect(SpecificationService.reinstate).toHaveBeenCalledWith(101, "Reinstated per QAR-2026-14 review")
    );
  });

  it("retries specification update with ChangeReasonDialog when server returns field 'reason'", async () => {
    vi.mocked(SpecificationService.update)
      .mockRejectedValueOnce(makeReasonRequiredError())
      .mockResolvedValueOnce({ ...activeSpec, upperLimit: 7.5 });

    const onClose = vi.fn();
    const onSuccess = vi.fn();
    const user = userEvent.setup();

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <SpecificationParameterDialog
            open
            onClose={onClose}
            item={testItem}
            existingSpecs={[activeSpec]}
            editingSpec={activeSpec}
            workflowTypeByCode={{ PH: "Measurement" }}
            testDefinitionByCode={{ PH: testDefinitions[0] }}
            onSuccess={onSuccess}
          />
        </AuthProvider>
      </PinnedLightTheme>
    );

    const saveBtn = await screen.findByRole("button", { name: "Save Changes" });
    await waitFor(() => expect(laboratorySectionService.getMySections).toHaveBeenCalled());
    await user.click(saveBtn);

    const reasonInput = await screen.findByLabelText(/Reason for change/i);
    await user.type(reasonInput, "Updated pharmacopeia upper limit");
    const dialogs = screen.getAllByRole("dialog");
    await user.click(within(dialogs[dialogs.length - 1]).getByRole("button", { name: "Save Changes" }));

    await waitFor(() => expect(SpecificationService.update).toHaveBeenCalledTimes(2));
    expect(vi.mocked(SpecificationService.update).mock.calls[1][3]).toBe("Updated pharmacopeia upper limit");
    await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1));
  });
});
