import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../theme";
import { AuthProvider } from "../../contexts/AuthContext";
import { QualificationMatrixPage } from "./QualificationMatrixPage";
import { competencyService, type CompetencyMatrixDto } from "../../services/competencyService";
import { laboratorySectionService } from "../../services/laboratorySectionService";

vi.mock("../../services/competencyService", () => ({
  competencyService: {
    getMatrix: vi.fn(),
    getSettings: vi.fn(),
    getImpact: vi.fn(),
    setGate: vi.fn(),
    qualify: vi.fn(),
    withdraw: vi.fn(),
    getMyMatrix: vi.fn()
  }
}));

vi.mock("../../services/laboratorySectionService", () => ({
  laboratorySectionService: {
    getMySections: vi.fn(),
    getSections: vi.fn()
  },
  getMySections: vi.fn(),
  getSections: vi.fn()
}));

const sampleMatrix: CompetencyMatrixDto = {
  sectionId: 1,
  page: 1,
  pageSize: 25,
  totalCount: 2,
  tests: [
    {
      testDefinitionId: 10,
      code: "TAMC",
      displayName: "Total Aerobic Microbial Count",
      sop: { documentCode: "SOP-QC-014", revisionNumber: "3" }
    },
    {
      testDefinitionId: 11,
      code: "TYMC",
      displayName: "Total Yeast and Mold Count",
      sop: null
    }
  ],
  analysts: [
    {
      userId: 101,
      analystName: "Mona Ali",
      cells: [
        { userId: 101, testDefinitionId: 10, status: "Qualified", expiresOn: "2027-05-01", qualificationId: 101 },
        { userId: 101, testDefinitionId: 11, status: "ExpiringSoon", expiresOn: "2026-12-01", qualificationId: 102 }
      ]
    },
    {
      userId: 102,
      analystName: "Omar Saleh",
      cells: [
        { userId: 102, testDefinitionId: 10, status: "Expired", expiresOn: "2026-01-15", qualificationId: 103 },
        { userId: 102, testDefinitionId: 11, status: "Withdrawn", expiresOn: null, qualificationId: 104 }
      ]
    }
  ]
};

function renderPage() {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter>
          <QualificationMatrixPage />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

describe("QualificationMatrixPage", () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem("microlims_permissions", JSON.stringify(["Competency.View", "Competency.Manage"]));
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([
      {
        sectionId: 1,
        sectionCode: "MICRO",
        sectionName: "Microbiology",
        departmentId: 1,
        departmentCode: "QC",
        departmentName: "Quality Control"
      }
    ]);
    vi.mocked(competencyService.getMatrix).mockResolvedValue(sampleMatrix);
    vi.mocked(competencyService.getSettings).mockResolvedValue({
      sectionId: 1,
      gateEnabled: false,
      reminderDaysBeforeExpiry: 30,
      changedAt: null,
      changedByName: null,
      version: 0
    });
    vi.mocked(competencyService.getImpact).mockResolvedValue({
      sectionId: 1,
      analysts: [],
      totalRefusedOrders: 0
    });
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("renders all matrix cell states as text and passes analyst/test filters to getMatrix", async () => {
    const user = userEvent.setup();
    renderPage();

    expect(await screen.findByText("Mona Ali")).toBeTruthy();
    expect(screen.getByText("Omar Saleh")).toBeTruthy();
    expect(screen.getByText("Qualified")).toBeTruthy();
    expect(screen.getByText("Expiring Soon")).toBeTruthy();
    expect(screen.getByText("Expired")).toBeTruthy();
    expect(screen.getByText("Withdrawn")).toBeTruthy();
    expect(screen.getByText("SOP SOP-QC-014 rev 3")).toBeTruthy();

    await user.type(screen.getByLabelText("Filter by analyst"), "Mona");
    await waitFor(() =>
      expect(competencyService.getMatrix).toHaveBeenCalledWith(
        1,
        expect.objectContaining({ analystSearch: "Mona" }),
        expect.any(AbortSignal)
      )
    );

    await user.type(screen.getByLabelText("Filter by test"), "TAMC");
    await waitFor(() =>
      expect(competencyService.getMatrix).toHaveBeenCalledWith(
        1,
        expect.objectContaining({ testSearch: "TAMC" }),
        expect.any(AbortSignal)
      )
    );
  });

  it("shows pagination when totalCount exceeds 25 rows", async () => {
    vi.mocked(competencyService.getMatrix).mockResolvedValue({
      ...sampleMatrix,
      totalCount: 30
    });
    renderPage();

    expect(await screen.findByText("Mona Ali")).toBeTruthy();
    expect(screen.getByRole("button", { name: /next page/i })).toBeTruthy();
  });

  it("renders translated status filter options and passes selected status to getMatrix", async () => {
    const user = userEvent.setup();
    renderPage();

    expect(await screen.findByText("Mona Ali")).toBeTruthy();
    await user.click(screen.getByRole("combobox", { name: "Status" }));
    await user.click(screen.getByRole("option", { name: "Expiring Soon" }));

    await waitFor(() =>
      expect(competencyService.getMatrix).toHaveBeenCalledWith(
        1,
        expect.objectContaining({ status: "ExpiringSoon" }),
        expect.any(AbortSignal)
      )
    );
  });

  it("opens WithdrawDialog from a matrix cell and posts to withdraw with the cell qualificationId", async () => {
    const user = userEvent.setup();
    vi.mocked(competencyService.withdraw).mockResolvedValue({
      id: 101,
      version: 2,
      userId: 101,
      testDefinitionId: 10,
      basis: "ObservedPractice",
      evidence: "Initial",
      evidenceSampleId: null,
      qualifiedOn: "2026-11-01",
      expiresOn: "2027-05-01",
      signature: {
        userFullName: "Hana Head",
        username: "hana",
        roleName: "Section Head",
        meaningOfSignature: "AnalystQualified",
        signedAt: "2026-11-01T10:00:00Z",
        comment: "Initial"
      },
      withdrawnOn: "2026-11-24T12:00:00Z",
      withdrawnReason: "Retraining",
      withdrawalSignature: null,
      attachments: []
    });

    renderPage();

    const withdrawBtn = await screen.findByRole("button", {
      name: "Withdraw qualification for Mona Ali on TAMC — Total Aerobic Microbial Count"
    });
    await user.click(withdrawBtn);

    await user.type(await screen.findByLabelText(/Reason for withdrawal/i), "Retraining");
    await user.type(screen.getByLabelText(/Password/i), "Secret123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() =>
      expect(competencyService.withdraw).toHaveBeenCalledWith(101, {
        reason: "Retraining",
        password: "Secret123!"
      })
    );
  });
});
