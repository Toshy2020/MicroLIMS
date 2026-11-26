import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { InvestigationPage } from "./InvestigationPage";
import {
  oosInvestigationService,
  type OosInvestigationDetailDto
} from "../../services/oosInvestigationService";
import { UserService } from "../users/services/UserService";
import { OosTrackingService } from "./services/OosTrackingService";

vi.mock("./services/OosTrackingService", () => ({
  OosTrackingService: {
    getDocuments: vi.fn(),
    uploadDocument: vi.fn(),
    downloadDocument: vi.fn(),
    supersedeDocument: vi.fn(),
    voidDocument: vi.fn()
  }
}));

vi.mock("../../services/oosInvestigationService", () => ({
  oosInvestigationService: {
    getById: vi.fn(),
    getFacts: vi.fn(),
    listCategories: vi.fn(),
    downloadPdf: vi.fn(),
    addAnalystStatement: vi.fn()
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
    vi.mocked(OosTrackingService.getDocuments).mockResolvedValue([]);
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

  it("files the attachments under the investigation number and lets only Oos.Manage upload", async () => {
    vi.mocked(oosInvestigationService.getFacts).mockResolvedValue({ version: 3, orders: [] });
    renderInvestigation(["Oos.View"], "Reviewer");

    expect(await screen.findByRole("heading", { name: "Attachments" })).toBeTruthy();
    expect(OosTrackingService.getDocuments).toHaveBeenCalledWith("OOS1126042", expect.anything());
    expect(screen.queryByLabelText(/^Upload file\. Accepted formats/)).toBeNull();

    cleanup();
    renderInvestigation(["Oos.View", "Oos.Manage"], "SectionHead");
    expect(await screen.findAllByLabelText(/^Upload file\. Accepted formats/)).toHaveLength(1);
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

  it("shows Address Request action to Section Head inside InvestigationPage", async () => {
    vi.mocked(oosInvestigationService.getFacts).mockResolvedValue({ version: 3, orders: [] });
    vi.mocked(oosInvestigationService.getById).mockResolvedValue({
      ...baseDetail,
      deviationRequests: [
        {
          id: 9,
          reason: "Escalate to deviation",
          requestedByUserId: 5,
          requestedByName: "Quinn QA",
          requestedAtUtc: "2026-11-21T10:00:00Z",
          status: "Open",
          addressedComment: null,
          addressedByUserId: null,
          addressedByName: null,
          addressedAtUtc: null,
          version: 1
        }
      ]
    });

    renderInvestigation(["Oos.View", "Oos.Manage"], "SectionHead");

    expect(await screen.findByText("Escalate to deviation")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Address Request" })).toBeTruthy();
  });

  it("shows inline refusal and keeps statement input when mutation fails without unhandled rejection", async () => {
    const user = userEvent.setup();
    vi.mocked(oosInvestigationService.getFacts).mockResolvedValue({ version: 3, orders: [] });
    vi.mocked(oosInvestigationService.addAnalystStatement).mockRejectedValue({
      response: { data: { message: "Investigation version conflict." } }
    });

    renderInvestigation(["Oos.View", "Oos.Contribute"], "Analyst");

    const input = (await screen.findByLabelText("Analyst statement")) as HTMLTextAreaElement;
    await user.type(input, "Dilution verified.");
    await user.click(screen.getByRole("button", { name: "Add Statement" }));

    await waitFor(() => {
      expect(screen.getByRole("alert").textContent).toContain("Investigation version conflict.");
    });
    expect(input.value).toBe("Dilution verified.");
  });
});
