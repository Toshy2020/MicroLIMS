import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../theme";
import { AuthProvider } from "../../contexts/AuthContext";
import { QualifyDialog } from "./QualifyDialog";
import { competencyService } from "../../services/competencyService";

vi.mock("../../services/competencyService", () => ({
  competencyService: {
    qualify: vi.fn()
  }
}));

function renderDialog(onQualified = vi.fn(), onClose = vi.fn()) {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter>
          <QualifyDialog
            open
            userId={101}
            analystName="Mona Ali"
            testDefinitionId={10}
            testLabel="TAMC — Total Aerobic Microbial Count"
            onClose={onClose}
            onQualified={onQualified}
          />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

describe("QualifyDialog", () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem("microlims_full_name", "Hana Head");
    localStorage.setItem("microlims_role", "SectionHead");
    vi.mocked(competencyService.qualify).mockResolvedValue({
      id: 55,
      version: 1,
      userId: 101,
      testDefinitionId: 10,
      basis: "ProficiencySample",
      evidence: "Passed blind PT panel #4",
      evidenceSampleId: null,
      qualifiedOn: "2026-11-20",
      expiresOn: "2027-11-20",
      signature: {
        userFullName: "Hana Head",
        username: "hana",
        roleName: "Section Head",
        meaningOfSignature: "AnalystQualified",
        signedAt: "2026-11-20T10:00:00Z",
        comment: "Passed blind PT panel #4"
      },
      withdrawnOn: null,
      withdrawnReason: null,
      withdrawalSignature: null,
      attachments: []
    });
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("sends basis, evidence, qualifiedOn date, and password via SignatureDialog", async () => {
    const user = userEvent.setup();
    const onQualified = vi.fn();
    renderDialog(onQualified);

    await user.click(screen.getByRole("combobox", { name: "Qualification Basis" }));
    await user.click(within(screen.getByRole("listbox")).getByRole("option", { name: "Proficiency Sample" }));

    await user.type(screen.getByRole("textbox", { name: /^Evidence/i }), "Passed blind PT panel #4");
    const dateInput = screen.getByLabelText(/Qualified On/i);
    await user.clear(dateInput);
    await user.type(dateInput, "2026-11-20");

    await user.click(screen.getByRole("button", { name: "Continue to Sign" }));

    const passwordInput = await screen.findByLabelText(/Password/i);
    await user.type(passwordInput, "Secret123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() =>
      expect(competencyService.qualify).toHaveBeenCalledWith(
        expect.objectContaining({
          userId: 101,
          testDefinitionId: 10,
          basis: "ProficiencySample",
          evidence: "Passed blind PT panel #4",
          qualifiedOn: "2026-11-20",
          password: "Secret123!"
        })
      )
    );
    expect(onQualified).toHaveBeenCalled();
  });
});
