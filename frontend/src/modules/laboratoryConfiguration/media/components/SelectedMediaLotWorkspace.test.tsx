import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { MemoryRouter } from "react-router-dom";
import { lightTheme } from "../../../../theme";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../../routes/routes";
import { apiClient } from "../../../../services/apiClient";
import { labelService } from "../../../../services/labelService";
import { SamplePreparationService } from "../../../testPreparation/services/SamplePreparationService";
import { SelectedMediaLotWorkspace } from "./SelectedMediaLotWorkspace";

vi.mock("../../../../services/apiClient", () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn()
  },
  ifMatch: vi.fn((version: number) => ({ headers: { "If-Match": String(version) } }))
}));

vi.mock("../../../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

vi.mock("../../../testPreparation/services/SamplePreparationService", () => ({
  SamplePreparationService: {
    getWhereUsed: vi.fn()
  }
}));

function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "SectionHead", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

const sampleLot = {
  id: 88,
  materialId: 301,
  material: {
    id: 301,
    materialName: "Tryptic Soy Agar Powder",
    batchNumber: "OX-2601"
  },
  lotNumber: "ML-2026-0088",
  manufacturerLot: "OX-2601",
  manufacturerName: "Oxoid",
  totalWeight: 40,
  totalVolume: "1000 mL",
  autoclaveEquipmentId: 41,
  autoclaveProgram: "STD-121",
  loadType: "Liquids",
  temperature: 121,
  cycleTime: 15,
  cycleNumber: 1,
  ph: 7.3,
  expiryDate: "2027-06-30",
  preparedAt: "2026-11-13T08:00:00Z",
  preparedByUserId: 1,
  status: "Prepared",
  approvalStatus: "PendingReview",
  approvedByUserId: null,
  approvedAt: null,
  isReleasedForUse: false
};

describe("SelectedMediaLotWorkspace label printing", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.MEDIA_PREPARE, PERMISSIONS.LABELS_REPRINT]);

    vi.mocked(apiClient.get).mockResolvedValue({
      data: {
        data: {
          id: 88,
          lotNumber: "ML-2026-0088",
          materialName: "Tryptic Soy Agar Powder",
          manufacturerLot: "OX-2601",
          manufacturerName: "Oxoid",
          totalWeight: 40,
          totalVolume: "1000 mL",
          autoclaveEquipmentName: "Autoclave Alpha",
          autoclaveProgram: "STD-121",
          loadType: "Liquids",
          temperature: 121,
          cycleTime: 15,
          cycleNumber: 1,
          ph: 7.3,
          preparedByName: "Microbiologist",
          preparedAt: "2026-11-13T08:00:00Z",
          expiryDate: "2027-06-30",
          status: "Prepared",
          approvalStatus: "PendingReview",
          isReleasedForUse: false,
          additives: [],
          signatures: [],
          timeline: []
        }
      }
    });
    vi.mocked(SamplePreparationService.getWhereUsed).mockResolvedValue([]);
    vi.mocked(labelService.getStatus).mockResolvedValue([
      {
        entityType: "MediaLot",
        entityId: 88,
        canPrint: true,
        blockedReason: null,
        printCount: 0,
        defaultCopies: 1
      }
    ]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("shows the print label button with the record's code", async () => {
    const user = userEvent.setup();

    render(
      <ThemeProvider theme={lightTheme}>
        <AuthProvider>
          <MemoryRouter>
            <SelectedMediaLotWorkspace
              lot={sampleLot}
              awaitingApprovalIds={new Set()}
              onClose={vi.fn()}
              onViewRecord={vi.fn()}
              onViewAuditHistory={vi.fn()}
              onOpenEvaluation={vi.fn()}
              onRequestReleaseDecision={vi.fn()}
              evaluationsList={[]}
            />
          </MemoryRouter>
        </AuthProvider>
      </ThemeProvider>
    );

    const printBtn = await screen.findByRole("button", { name: "Print label" });
    await waitFor(() => {
      expect((printBtn as HTMLButtonElement).disabled).toBe(false);
    });

    await user.click(printBtn);

    const dialog = await screen.findByRole("dialog", { name: "Print labels" });
    expect(await within(dialog).findByText("ML-2026-0088")).toBeTruthy();
  });
});
