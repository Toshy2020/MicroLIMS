import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../../theme";
import { NotApplicableTestsSection } from "./NotApplicableTestsSection";
import { NotApplicableTestService, NotApplicableTestDto } from "../services/NotApplicableTestService";
import { PERMISSIONS } from "../../../../routes/routes";

let mockPermissions: string[] = [];

vi.mock("../../../../contexts/AuthContext", () => ({
  useAuth: () => ({
    fullName: "Test User",
    username: "testuser",
    role: "SectionHead",
    permissions: mockPermissions,
  }),
  useHasPermission: (perm: string | string[]) => {
    if (Array.isArray(perm)) return perm.some((p) => mockPermissions.includes(p));
    return mockPermissions.includes(perm);
  },
}));

vi.mock("../services/NotApplicableTestService", () => ({
  NotApplicableTestService: {
    listForItem: vi.fn(),
    create: vi.fn(),
    headApprove: vi.fn(),
    finalApprove: vi.fn(),
    withdraw: vi.fn(),
  },
}));

describe("NotApplicableTestsSection", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockPermissions = [];
  });

  afterEach(() => {
    cleanup();
  });

  const dummyAssignedTests = [
    { testCode: "TEST-01", displayName: "Assay by HPLC" },
    { testCode: "TEST-02", displayName: "Related Substances" },
  ];
  const dummyGrades = ["USP", "BP"];

  it("Draft shows only the head action to a Section Head", async () => {
    mockPermissions = [PERMISSIONS.NOT_APPLICABLE_TESTS_MANAGE];
    const draftRecord: NotApplicableTestDto = {
      id: 101,
      itemId: 1,
      grade: "USP",
      testCode: "TEST-01",
      testDisplayName: "Assay by HPLC",
      justification: "Not required for USP grade",
      criticality: "Non-critical",
      immediateAction: "Skip assay",
      longTermAction: "Review monograph",
      reference: "USP-2026",
      status: "Draft",
      headSignatureId: null,
      finalSignatureId: null,
      createdByUserId: 1,
      createdAt: "2026-11-01T10:00:00Z",
      headSignedBy: null,
      headSignedAt: null,
      finalSignedBy: null,
      finalSignedAt: null,
    };

    vi.mocked(NotApplicableTestService.listForItem).mockResolvedValue([draftRecord]);

    render(
      <ThemeProvider theme={lightTheme}>
        <NotApplicableTestsSection itemId={1} assignedTests={dummyAssignedTests} grades={dummyGrades} />
      </ThemeProvider>
    );

    await waitFor(() => {
      expect(screen.getByText("Assay by HPLC")).toBeTruthy();
    });

    // Should show "Approve (Section Head)"
    expect(screen.getByText("Approve (Section Head)")).toBeTruthy();
    // Should NOT show "Approve (QC Manager)"
    expect(screen.queryByText("Approve (QC Manager)")).toBeNull();
  });

  it("HeadApproved shows the QC Manager action only to a QC Manager", async () => {
    mockPermissions = [PERMISSIONS.NOT_APPLICABLE_TESTS_FINAL_APPROVE];
    const headApprovedRecord: NotApplicableTestDto = {
      id: 102,
      itemId: 1,
      grade: null,
      testCode: "TEST-02",
      testDisplayName: "Related Substances",
      justification: "Not applicable to this material",
      criticality: "Critical",
      immediateAction: "Omit test",
      longTermAction: "Update master",
      reference: "REF-002",
      status: "HeadApproved",
      headSignatureId: 5,
      finalSignatureId: null,
      createdByUserId: 1,
      createdAt: "2026-11-01T10:00:00Z",
      headSignedBy: "Section Head User",
      headSignedAt: "2026-11-01T11:00:00Z",
      finalSignedBy: null,
      finalSignedAt: null,
    };

    vi.mocked(NotApplicableTestService.listForItem).mockResolvedValue([headApprovedRecord]);

    render(
      <ThemeProvider theme={lightTheme}>
        <NotApplicableTestsSection itemId={1} assignedTests={dummyAssignedTests} grades={dummyGrades} />
      </ThemeProvider>
    );

    await waitFor(() => {
      expect(screen.getByText("Related Substances")).toBeTruthy();
    });

    // Should show "Approve (QC Manager)"
    expect(screen.getByText("Approve (QC Manager)")).toBeTruthy();
    // Should NOT show "Approve (Section Head)"
    expect(screen.queryByText("Approve (Section Head)")).toBeNull();
  });

  it("Submit sends the fields", async () => {
    mockPermissions = [PERMISSIONS.NOT_APPLICABLE_TESTS_MANAGE];
    vi.mocked(NotApplicableTestService.listForItem).mockResolvedValue([]);
    vi.mocked(NotApplicableTestService.create).mockResolvedValue({
      id: 103,
      itemId: 1,
      grade: "BP",
      testCode: "TEST-01",
      testDisplayName: "Assay by HPLC",
      justification: "Specific to BP",
      criticality: "Non-critical",
      immediateAction: "Action now",
      longTermAction: "Action later",
      reference: "BP monograph",
      status: "Draft",
      headSignatureId: null,
      finalSignatureId: null,
      createdByUserId: 1,
      createdAt: "2026-11-01T10:00:00Z",
      headSignedBy: null,
      headSignedAt: null,
      finalSignedBy: null,
      finalSignedAt: null,
    });

    render(
      <ThemeProvider theme={lightTheme}>
        <NotApplicableTestsSection itemId={1} assignedTests={dummyAssignedTests} grades={dummyGrades} />
      </ThemeProvider>
    );

    // Click "Declare not applicable"
    const declareBtn = await screen.findByText("Declare not applicable");
    fireEvent.click(declareBtn);

    // Fill form fields
    const testSelect = screen.getByLabelText(/Test/i);
    fireEvent.change(testSelect, { target: { value: "TEST-01" } });

    const gradeSelect = screen.getByLabelText(/Grade/i);
    fireEvent.change(gradeSelect, { target: { value: "BP" } });

    fireEvent.change(screen.getByLabelText(/Justification/i), { target: { value: "Specific to BP" } });
    fireEvent.change(screen.getByLabelText(/Criticality/i), { target: { value: "Non-critical" } });
    fireEvent.change(screen.getByLabelText(/Immediate Action/i), { target: { value: "Action now" } });
    fireEvent.change(screen.getByLabelText(/Long-term Action/i), { target: { value: "Action later" } });
    fireEvent.change(screen.getByLabelText(/Reference/i), { target: { value: "BP monograph" } });

    // Click Submit
    const submitBtn = screen.getByRole("button", { name: /Submit/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(NotApplicableTestService.create).toHaveBeenCalledWith(1, {
        testCode: "TEST-01",
        grade: "BP",
        justification: "Specific to BP",
        criticality: "Non-critical",
        immediateAction: "Action now",
        longTermAction: "Action later",
        reference: "BP monograph",
      });
    });
  });

  it("Withdraw action opens SignatureDialog and calls withdraw with reason", async () => {
    mockPermissions = [PERMISSIONS.NOT_APPLICABLE_TESTS_MANAGE];
    const draftRecord: NotApplicableTestDto = {
      id: 104,
      itemId: 1,
      grade: null,
      testCode: "TEST-01",
      testDisplayName: "Assay by HPLC",
      justification: "Draft to withdraw",
      criticality: "Non-critical",
      immediateAction: "None",
      longTermAction: "None",
      reference: null,
      status: "Draft",
      headSignatureId: null,
      finalSignatureId: null,
      createdByUserId: 1,
      createdAt: "2026-11-01T10:00:00Z",
      headSignedBy: null,
      headSignedAt: null,
      finalSignedBy: null,
      finalSignedAt: null,
    };

    vi.mocked(NotApplicableTestService.listForItem).mockResolvedValue([draftRecord]);
    vi.mocked(NotApplicableTestService.withdraw).mockResolvedValue({
      ...draftRecord,
      status: "Withdrawn",
    });

    render(
      <ThemeProvider theme={lightTheme}>
        <NotApplicableTestsSection itemId={1} assignedTests={dummyAssignedTests} grades={dummyGrades} />
      </ThemeProvider>
    );

    await waitFor(() => {
      expect(screen.getByText("Withdraw")).toBeTruthy();
    });

    // Click Withdraw
    fireEvent.click(screen.getByText("Withdraw"));

    // SignatureDialog is shown
    expect(screen.getByText("Electronic Signature")).toBeTruthy();

    // Fill reason into comment field
    const commentInput = screen.getByLabelText(/Comment \(optional\)/i);
    fireEvent.change(commentInput, { target: { value: "Wrong specification declared" } });

    // Fill password
    const passwordInput = screen.getByLabelText(/^Password/i);
    fireEvent.change(passwordInput, { target: { value: "ValidPassword123!" } });

    // Click Sign
    const signBtn = screen.getByRole("button", { name: "Sign" });
    fireEvent.click(signBtn);

    await waitFor(() => {
      expect(NotApplicableTestService.withdraw).toHaveBeenCalledWith(
        104,
        "ValidPassword123!",
        "Wrong specification declared"
      );
    });
  });
});
