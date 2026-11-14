import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosResponse } from "axios";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { toast } from "sonner";
import { renderWithTheme } from "../../components/bench/benchTestUtils";
import { AuthProvider } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { draftStore } from "../../services/drafts/draftStore";
import { makeDraft, rawStoredDrafts, resetDraftDb, signInForTest } from "../../services/drafts/draftTestUtils";
import { masterDataOptions } from "../../services/masterDataOptions";
import { EquipmentConfigurationService } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { labelService } from "../../services/labelService";
import { SolutionPreparationService } from "./services/SolutionPreparationService";
import type { SolutionPreparationResponse } from "./types";
import { PreparationWizardPage } from "./PreparationWizardPage";
import { PreparationRecordPage } from "./PreparationRecordPage";

vi.mock("../../services/masterDataOptions", () => ({
  masterDataOptions: {
    getEquipment: vi.fn()
  }
}));

vi.mock("../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: {
    getAutoclavePrograms: vi.fn()
  }
}));

vi.mock("./services/SolutionPreparationService", () => ({
  SolutionPreparationService: {
    getById: vi.fn(),
    getLotOptions: vi.fn(),
    getSourcePreparations: vi.fn(),
    save: vi.fn(),
    complete: vi.fn()
  }
}));

vi.mock("../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "Analyst", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

const draftPreparation: SolutionPreparationResponse = {
  id: 12,
  version: 1,
  sectionId: 2,
  sectionName: "Physicochemical",
  code: null,
  solutionMasterId: 5,
  solutionMasterName: "Phosphate Buffer pH 6.8",
  type: "MobilePhase",
  hplcMethodId: 3,
  hplcMethodAbbreviation: "AMX-01",
  status: "InProgress",
  effectiveStatus: "InProgress",
  startedByUserId: 1,
  startedByUserName: "Analyst",
  startedAt: "2026-11-13T07:00:00Z",
  preparedByUserId: null,
  preparedByUserName: null,
  preparedAt: null,
  expiresAt: null,
  finalVolumeMl: 1000,
  measuredPh: null,
  approvalStatus: null,
  releasedByUserId: null,
  releasedByUserName: null,
  releasedAt: null,
  releaseComment: null,
  recipeSnapshotJson: JSON.stringify({
    name: "Phosphate Buffer pH 6.8",
    type: "MobilePhase",
    phTarget: null,
    phTolerance: null,
    releaseRequired: false,
    sterilizationRequired: false,
    shelfLifeValue: 7,
    shelfLifeUnit: "Days",
    storageCondition: "Room temp",
    instructions: null
  }),
  components: [
    {
      id: 101,
      order: 1,
      kind: "Material",
      materialMasterEntryId: 9,
      entryCode: "KH2PO4",
      entryName: "Potassium Dihydrogen Phosphate",
      recipeQuantity: 6.8,
      recipeUnit: "Gram",
      materialId: 77,
      lotBatchNumber: "KH-2601",
      lotUnit: "Gram",
      quantityUsed: 6.8
    }
  ],
  statusHistory: []
};

const completedPreparation: SolutionPreparationResponse = {
  ...draftPreparation,
  version: 2,
  code: "SP-2026-0012",
  status: "Prepared",
  effectiveStatus: "Prepared",
  preparedByUserId: 1,
  preparedByUserName: "Analyst",
  preparedAt: "2026-11-13T08:00:00Z",
  expiresAt: "2026-11-20T08:00:00Z"
};

function renderWizard(bench = false) {
  return renderWithTheme(
    <AuthProvider>
      <MemoryRouter initialEntries={["/preparation/12/edit"]}>
        <Routes>
          <Route path="/preparation/:id/edit" element={<PreparationWizardPage />} />
          <Route path="/preparation/:id" element={<PreparationRecordPage />} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
    { bench }
  );
}

async function completeWizard(user: ReturnType<typeof userEvent.setup>) {
  const continueBtn = await screen.findByRole("button", { name: "Continue to Review" });
  await user.click(continueBtn);

  const signStepBtn = await screen.findByRole("button", { name: "Sign & Complete Preparation" });
  await user.click(signStepBtn);

  const passwordInput = await screen.findByLabelText(/Password/i);
  await user.type(passwordInput, "secret123");
  await user.click(screen.getByRole("button", { name: "Sign" }));
}

describe("PreparationWizardPage label printing offer", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    localStorage.clear();
    await resetDraftDb();
    signInForTest(7);
    seedPermissions([PERMISSIONS.SOLUTIONS_PREPARE]);
    vi.spyOn(toast, "success").mockImplementation(() => "");
    vi.mocked(masterDataOptions.getEquipment).mockResolvedValue([
      { id: 41, code: "AC-01", name: "Autoclave Alpha" }
    ] as never);
    vi.mocked(EquipmentConfigurationService.getAutoclavePrograms).mockResolvedValue([]);

    vi.mocked(SolutionPreparationService.getById).mockImplementation(async () =>
      vi.mocked(SolutionPreparationService.complete).mock.calls.length > 0
        ? completedPreparation
        : draftPreparation
    );
    vi.mocked(SolutionPreparationService.getLotOptions).mockResolvedValue([
      {
        materialId: 77,
        batchNumber: "KH-2601",
        expiryDate: "2028-01-01",
        quantityRemaining: 500,
        unit: "Gram",
        usable: true,
        reason: null
      }
    ]);
    vi.mocked(SolutionPreparationService.getSourcePreparations).mockResolvedValue([]);
    vi.mocked(SolutionPreparationService.save).mockResolvedValue(draftPreparation);
    vi.mocked(SolutionPreparationService.complete).mockResolvedValue(completedPreparation);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it("offers label printing after solution preparation is created", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      {
        entityType: "SolutionPreparation",
        entityId: 12,
        canPrint: true,
        blockedReason: null,
        printCount: 0,
        defaultCopies: 1
      }
    ]);

    renderWizard();
    await completeWizard(user);

    const dialog = await screen.findByRole("dialog", { name: "Print labels" });
    expect(await within(dialog).findByText("SP-2026-0012")).toBeTruthy();
  }, 15000);

  it("does not open the print dialog when printing is unavailable, while keeping the success flow", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      {
        entityType: "SolutionPreparation",
        entityId: 12,
        canPrint: false,
        blockedReason: "Label printing is disabled.",
        printCount: 0,
        defaultCopies: 1
      }
    ]);

    renderWizard();
    await completeWizard(user);

    await waitFor(() => {
      expect(toast.success).toHaveBeenCalledWith("Preparation completed successfully. Code: SP-2026-0012");
    });
    expect(await screen.findByRole("heading", { name: "SP-2026-0012" })).toBeTruthy();
    expect(screen.queryByRole("dialog", { name: "Print labels" })).toBeNull();
  }, 15000);

  it("solution payload equals SavePreparationRequest", async () => {
    const user = userEvent.setup();
    renderWizard();

    const finalVolInput = await screen.findByLabelText(/Final Volume/i);
    await user.clear(finalVolInput);
    await user.type(finalVolInput, "1000");

    const phInput = screen.getByLabelText(/Measured pH/i);
    await user.type(phInput, "6.8");

    await user.click(screen.getByRole("button", { name: "Save Draft" }));

    await waitFor(() => expect(SolutionPreparationService.save).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SolutionPreparationService.save).mock.calls[0]).toEqual([
      12,
      {
        components: [
          {
            componentId: 101,
            materialId: 77,
            quantityUsed: 6.8
          }
        ],
        finalVolumeMl: 1000,
        measuredPh: 6.8
      }
    ]);
  });

  it('numeric fields (volume, pH, temperature, cycle time, quantity used) use NumericEntryField kind="decimal"; cycle number kind="count"', async () => {
    vi.mocked(SolutionPreparationService.getById).mockResolvedValue({
      ...draftPreparation,
      recipeSnapshotJson: JSON.stringify({
        ...JSON.parse(draftPreparation.recipeSnapshotJson),
        sterilizationRequired: true
      })
    });
    renderWizard(true);

    const finalVol = await screen.findByLabelText(/Final Volume/i);
    const measuredPh = screen.getByLabelText(/Measured pH/i);
    const qtyUsed = screen.getByLabelText(/Quantity Used/i);
    const temp = screen.getByLabelText(/Temperature/i);
    const cycleTime = screen.getByLabelText(/Cycle Time/i);
    const cycleNumber = screen.getByLabelText(/Cycle Number/i);

    for (const field of [finalVol, measuredPh, qtyUsed, temp, cycleTime]) {
      expect(field.hasAttribute("data-numeric-entry")).toBe(true);
      expect(field.getAttribute("inputmode")).toBe("decimal");
    }
    expect(cycleNumber.hasAttribute("data-numeric-entry")).toBe(true);
    expect(cycleNumber.getAttribute("inputmode")).toBe("numeric");
  });

  it("sections stack one per screen height in bench mode with a step indicator", async () => {
    vi.mocked(SolutionPreparationService.getById).mockResolvedValue({
      ...draftPreparation,
      recipeSnapshotJson: JSON.stringify({
        ...JSON.parse(draftPreparation.recipeSnapshotJson),
        sterilizationRequired: true
      })
    });
    renderWizard(true);

    const stepper = await screen.findByTestId("lots-step-indicator");
    expect(stepper).toBeTruthy();
    const sections = document.querySelectorAll("[data-bench-section]");
    expect(sections.length).toBe(3);
  });

  it("lot and equipment pickers accept a scan when B1's ScanField is present", async () => {
    const user = userEvent.setup();
    vi.mocked(SolutionPreparationService.getById).mockResolvedValue({
      ...draftPreparation,
      recipeSnapshotJson: JSON.stringify({
        ...JSON.parse(draftPreparation.recipeSnapshotJson),
        sterilizationRequired: true
      })
    });
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce({
      outcome: "Found",
      layoutCorrected: false,
      normalizedValue: "AC-01",
      match: {
        entityType: "EquipmentAsset",
        id: 801,
        linkedEquipmentId: 41,
        code: "AC-01",
        displayName: "Autoclave Alpha",
        status: "Active",
        usable: true,
        blockedReason: null,
        navigationTarget: "/equipment/41"
      },
      candidates: [],
      hint: null
    });

    renderWizard();
    const scanAutoclave = await screen.findByLabelText("Scan autoclave");
    await user.type(scanAutoclave, "EQ:AC-01{Enter}");

    await waitFor(() => {
      expect(screen.getByRole("combobox", { name: "Autoclave" }).textContent).toContain("AC-01");
    });
  });

  it("solution wizard draft restores to the same step and excludes signature inputs", async () => {
    const user = userEvent.setup();
    await draftStore.put(
      makeDraft({
        userId: 7,
        formKey: "solution-preparation",
        recordKey: "12",
        recordVersion: 1,
        values: {
          activeStep: 2,
          finalVolumeText: "950",
          measuredPhText: "6.9",
          password: "secret",
          comment: "signed-comment"
        }
      })
    );

    renderWizard(true);
    const restoreBtn = await screen.findByRole("button", { name: "Restore entry" });
    await user.click(restoreBtn);

    expect(await screen.findByRole("button", { name: "Sign & Complete Preparation" })).toBeTruthy();

    // Go back to step 1 and verify values restored and no password/comment stored on future draft writes
    await user.click(screen.getByRole("button", { name: "Back to Edit" }));
    expect((await screen.findByLabelText(/Final Volume/i) as HTMLInputElement).value).toBe("950");

    await user.type(screen.getByLabelText(/Measured pH/i), "1");
    await new Promise((r) => setTimeout(r, 500));
    const stored = (await rawStoredDrafts()) as Array<{ values: Record<string, unknown> }>;
    expect(stored.length).toBe(1);
    expect("password" in stored[0].values).toBe(false);
    expect("comment" in stored[0].values).toBe(false);
  });

  it("server validation errors appear on the matching field", async () => {
    const user = userEvent.setup();
    vi.mocked(SolutionPreparationService.save).mockRejectedValueOnce(
      new AxiosError("Validation failed", "ERR_BAD_REQUEST", undefined, undefined, {
        status: 400,
        statusText: "Bad Request",
        data: {
          message: "Validation failed",
          errors: {
            finalVolumeMl: ["Final volume must be positive."],
            measuredPh: ["Measured pH is out of range."]
          }
        },
        headers: {},
        config: {}
      } as AxiosResponse)
    );

    renderWizard();
    await screen.findByLabelText(/Final Volume/i);
    await user.click(screen.getByRole("button", { name: "Save Draft" }));

    expect(await screen.findByText("Final volume must be positive.")).toBeTruthy();
    expect(screen.getByText("Measured pH is out of range.")).toBeTruthy();
  });

  it("network error keeps values and draft; 409 keeps draft as rejected without retrying", async () => {
    const user = userEvent.setup();
    renderWizard(true);

    const phInput = await screen.findByLabelText(/Measured pH/i);
    await user.type(phInput, "6.8");
    await new Promise((r) => setTimeout(r, 500));

    vi.mocked(SolutionPreparationService.save).mockRejectedValueOnce(new AxiosError("Network Error", "ERR_NETWORK"));
    await user.click(screen.getByRole("button", { name: "Save Draft" }));
    expect(await screen.findByRole("button", { name: "Try saving again" })).toBeTruthy();
    expect((screen.getByLabelText(/Measured pH/i) as HTMLInputElement).value).toBe("6.8");
    expect(SolutionPreparationService.save).toHaveBeenCalledTimes(1);

    vi.mocked(SolutionPreparationService.save).mockRejectedValueOnce(
      new AxiosError("Conflict", "ERR_BAD_REQUEST", undefined, undefined, {
        status: 409,
        statusText: "Conflict",
        data: { message: "Preparation version conflict." },
        headers: {},
        config: {}
      } as AxiosResponse)
    );
    await user.click(screen.getByRole("button", { name: "Save Draft" }));
    await waitFor(() => expect(screen.getByText("Preparation version conflict.")).toBeTruthy());
    expect(SolutionPreparationService.save).toHaveBeenCalledTimes(2);
    const after409 = (await rawStoredDrafts()) as Array<{ state: string }>;
    expect(after409[0]?.state).toBe("rejected");
  });
});
