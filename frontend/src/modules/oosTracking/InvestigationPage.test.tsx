import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { InvestigationPage } from "./InvestigationPage";
import {
  oosInvestigationService,
  type OosInvestigationDetailDto
} from "../../services/oosInvestigationService";
import { UserService } from "../users/services/UserService";

vi.mock("../../services/oosInvestigationService", () => ({
  oosInvestigationService: {
    getById: vi.fn(),
    getFacts: vi.fn(),
    listCategories: vi.fn(),
    downloadPdf: vi.fn()
  }
}));

vi.mock("../users/services/UserService", () => ({
  UserService: {
    getEligibleAnalysts: vi.fn()
  }
}));

const baseDetail: OosInvestigationDetailDto = {
  id: 42,
  number: "OOS1126042",
  kind: "Oos",
  status: "PhaseOne",
  sampleId: 7,
  sampleReferenceNumber: "S-2026-0007",
  sampleCategory: "FinishedProduct",
  sampleDisplayName: "Paracetamol 500mg",
  batchNumber: "B-42",
  sectionId: 1,
  sectionName: "Microbiology",
  openedAtUtc: "2026-11-20T09:00:00Z",
  openedByUserId: 2,
  openedByName: "Rita Reviewer",
  phaseOneDueDate: "2026-11-24",
  dueDate: "2026-12-01",
  isOverdue: false,
  phaseOneAssessment: null,
  phaseOneCauseCategoryId: null,
  phaseOneCauseCategoryName: null,
  phaseOneCauseDescription: null,
  phaseOneSignedAtUtc: null,
  phaseOneSignedByUserId: null,
  phaseOneSignedByName: null,
  conclusion: null,
  rootCauseCategoryId: null,
  rootCauseCategoryName: null,
  rootCauseDescription: null,
  conclusionSummary: null,
  concludedAtUtc: null,
  concludedByUserId: null,
  concludedByName: null,
  version: 3,
  checklistItems: [
    { phase: 1, key: "k1", order: 1, text: "Verify pipette calibration" }
  ],
  results: [
    {
      id: 1,
      testOrderId: 55,
      testCode: "TAMC",
      testDisplayName: "Total Aerobic Microbial Count",
      outcomeAtOpening: "Fails",
      resultSummaryAtOpening: "520 CFU/g",
      addedAtUtc: "2026-11-20T09:00:00Z",
      isInvalidated: false,
      invalidatedAtUtc: null
    }
  ],
  entries: [],
  retestPlans: [],
  reopenings: [],
  deviationRequests: [],
  timeline: []
};

function renderInvestigation(permissions: string[], role = "Reviewer") {
  localStorage.setItem("microlims_username", "viewer");
  localStorage.setItem("microlims_role", role);
  localStorage.setItem("microlims_full_name", "Vera Viewer");
  localStorage.setItem("microlims_user_id", "9");
  localStorage.setItem("microlims_permissions", JSON.stringify(permissions));

  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={["/oos-tracking/42"]}>
          <Routes>
            <Route path="/oos-tracking/:id" element={<InvestigationPage />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

describe("InvestigationPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(oosInvestigationService.getById).mockResolvedValue(baseDetail);
    vi.mocked(oosInvestigationService.listCategories).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 100
    });
    vi.mocked(UserService.getEligibleAnalysts).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("shows PartialOutageBanner when facts fail to load", async () => {
    vi.mocked(oosInvestigationService.getFacts).mockRejectedValue(new Error("network error"));
    renderInvestigation(["Oos.View"]);

    expect(await screen.findByText("OOS1126042")).toBeTruthy();
    expect(await screen.findByText(/Some data could not be loaded: Laboratory Facts/i)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Retry" })).toBeTruthy();
  });

  it("is read-only when user only has Oos.View", async () => {
    vi.mocked(oosInvestigationService.getFacts).mockResolvedValue({ version: 3, orders: [] });
    renderInvestigation(["Oos.View"], "Reviewer");

    expect(await screen.findByText("OOS1126042")).toBeTruthy();
    expect(screen.getByText(/Read-only view/i)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Add Statement" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Save Answer" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Sign Phase I" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Request Deviation" })).toBeNull();
  });
});
