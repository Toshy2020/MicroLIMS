import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { toast } from "sonner";
import { lightTheme } from "../../theme";
import { AuthProvider } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { labelService } from "../../services/labelService";
import { SolutionPreparationService } from "./services/SolutionPreparationService";
import type { SolutionPreparationResponse } from "./types";
import { PreparationWizardPage } from "./PreparationWizardPage";
import { PreparationRecordPage } from "./PreparationRecordPage";

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

function renderWizard() {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter initialEntries={["/preparation/12/edit"]}>
          <Routes>
            <Route path="/preparation/:id/edit" element={<PreparationWizardPage />} />
            <Route path="/preparation/:id" element={<PreparationRecordPage />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
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
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.SOLUTIONS_PREPARE]);
    vi.spyOn(toast, "success").mockImplementation(() => "");

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
});
