import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../theme";
import { AuthProvider } from "../../contexts/AuthContext";
import { MyQualificationsPage } from "./MyQualificationsPage";
import { competencyService } from "../../services/competencyService";
import { trainingAssignmentService } from "../documentControl/services/trainingAssignmentService";

vi.mock("../../services/competencyService", () => ({
  competencyService: {
    getMyMatrix: vi.fn()
  }
}));

vi.mock("../documentControl/services/trainingAssignmentService", () => ({
  trainingAssignmentService: {
    getMyAssignments: vi.fn()
  }
}));

function renderPage() {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter>
          <MyQualificationsPage />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

describe("MyQualificationsPage", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.mocked(competencyService.getMyMatrix).mockResolvedValue({
      sectionId: 1,
      page: 1,
      pageSize: 50,
      totalCount: 1,
      tests: [
        {
          testDefinitionId: 10,
          code: "TAMC",
          displayName: "Total Aerobic Microbial Count",
          sop: { documentCode: "SOP-QC-014", revisionNumber: "3" }
        }
      ],
      analysts: [
        {
          userId: 101,
          analystName: "Mona Ali",
          cells: [{ userId: 101, testDefinitionId: 10, status: "ExpiringSoon", expiresOn: "2026-12-15" }]
        }
      ]
    });
    vi.mocked(trainingAssignmentService.getMyAssignments).mockResolvedValue([
      {
        id: 77,
        documentMasterId: 5,
        microLimsDocumentId: "DOC-005",
        companyDocumentCode: "SOP-QC-014",
        documentTitle: "Microbial Enumeration Procedure",
        documentRevisionId: 9,
        revisionNumber: "3",
        assignedUserId: 101,
        assignedUserName: "Mona Ali",
        assignmentType: "Reading",
        status: "Assigned",
        assignedDateUtc: "2026-11-01T08:00:00Z",
        dueDateUtc: "2026-12-01T08:00:00Z",
        createdByUserId: 1,
        createdByUserName: "Hana Head",
        createdAtUtc: "2026-11-01T08:00:00Z",
        isOverdue: false,
        daysRemainingOrOverdue: 10
      }
    ]);
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("shows own qualifications with expiry and SOPs to read", async () => {
    renderPage();

    expect(await screen.findByText("TAMC")).toBeTruthy();
    expect(screen.getByText("Expiring Soon")).toBeTruthy();
    expect(screen.getByText("2026-12-15")).toBeTruthy();
    expect(screen.getByText(/Microbial Enumeration Procedure/)).toBeTruthy();
    expect(screen.getByRole("link", { name: "Open My Reading List" })).toBeTruthy();
  });
});
