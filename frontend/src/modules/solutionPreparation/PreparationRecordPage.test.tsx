import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { lightTheme } from "../../theme";
import { AuthProvider } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { labelService } from "../../services/labelService";
import { SolutionPreparationService } from "./services/SolutionPreparationService";
import type { SolutionPreparationResponse } from "./types";
import { PreparationRecordPage } from "./PreparationRecordPage";

vi.mock("./services/SolutionPreparationService", () => ({
  SolutionPreparationService: {
    getById: vi.fn(),
    cancel: vi.fn(),
    discard: vi.fn()
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

const preparedRecord: SolutionPreparationResponse = {
  id: 12,
  version: 2,
  sectionId: 2,
  sectionName: "Physicochemical",
  code: "SP-2026-0012",
  solutionMasterId: 5,
  solutionMasterName: "Phosphate Buffer pH 6.8",
  type: "MobilePhase",
  hplcMethodId: 3,
  hplcMethodAbbreviation: "AMX-01",
  status: "Prepared",
  effectiveStatus: "Prepared",
  startedByUserId: 1,
  startedByUserName: "Analyst",
  startedAt: "2026-11-13T07:00:00Z",
  preparedByUserId: 1,
  preparedByUserName: "Analyst",
  preparedAt: "2026-11-13T08:00:00Z",
  expiresAt: "2026-11-20T08:00:00Z",
  finalVolumeMl: 1000,
  measuredPh: 6.8,
  approvalStatus: "Approved",
  releasedByUserId: null,
  releasedByUserName: null,
  releasedAt: null,
  releaseComment: null,
  recipeSnapshotJson: JSON.stringify({
    name: "Phosphate Buffer pH 6.8",
    type: "MobilePhase",
    phTarget: 6.8,
    phTolerance: 0.1,
    releaseRequired: false,
    sterilizationRequired: false,
    shelfLifeValue: 7,
    shelfLifeUnit: "Days",
    storageCondition: "Room temp",
    instructions: null
  }),
  components: [],
  statusHistory: []
};

describe("PreparationRecordPage label printing", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.SOLUTIONS_PREPARE, PERMISSIONS.LABELS_REPRINT]);

    vi.mocked(SolutionPreparationService.getById).mockResolvedValue(preparedRecord);
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
          <MemoryRouter initialEntries={["/preparation/12"]}>
            <Routes>
              <Route path="/preparation/:id" element={<PreparationRecordPage />} />
            </Routes>
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
    expect(await within(dialog).findByText("SP-2026-0012")).toBeTruthy();
  });
});
