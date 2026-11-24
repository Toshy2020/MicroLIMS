import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosResponse } from "axios";
import { MemoryRouter } from "react-router-dom";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../theme";
import { AuthProvider } from "../../contexts/AuthContext";
import { WithdrawDialog } from "./WithdrawDialog";
import { competencyService } from "../../services/competencyService";

vi.mock("../../services/competencyService", () => ({
  competencyService: {
    withdraw: vi.fn()
  }
}));

function renderDialog(onWithdrawn = vi.fn(), onClose = vi.fn()) {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter>
          <WithdrawDialog
            open
            qualificationId={55}
            analystName="Mona Ali"
            testLabel="TAMC"
            onClose={onClose}
            onWithdrawn={onWithdrawn}
          />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

describe("WithdrawDialog", () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem("microlims_full_name", "Hana Head");
    localStorage.setItem("microlims_role", "SectionHead");
    vi.mocked(competencyService.withdraw).mockResolvedValue({
      id: 55,
      version: 2,
      userId: 101,
      testDefinitionId: 10,
      basis: "ObservedPractice",
      evidence: "Initial",
      evidenceSampleId: null,
      qualifiedOn: "2026-11-01",
      expiresOn: "2027-11-01",
      signature: {
        userFullName: "Hana Head",
        username: "hana",
        roleName: "Section Head",
        meaningOfSignature: "AnalystQualified",
        signedAt: "2026-11-01T10:00:00Z",
        comment: "Initial"
      },
      withdrawnOn: "2026-11-24T12:00:00Z",
      withdrawnReason: "Retraining required",
      withdrawalSignature: {
        userFullName: "Hana Head",
        username: "hana",
        roleName: "Section Head",
        meaningOfSignature: "AnalystQualificationWithdrawn",
        signedAt: "2026-11-24T12:00:00Z",
        comment: "Retraining required"
      },
      attachments: []
    });
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("requires a withdrawal reason before Sign is enabled and submits reason + password", async () => {
    const user = userEvent.setup();
    const onWithdrawn = vi.fn();
    renderDialog(onWithdrawn);

    const signButton = screen.getByRole("button", { name: "Sign" }) as HTMLButtonElement;
    expect(signButton.disabled).toBe(true);

    await user.type(screen.getByLabelText(/Password/i), "Secret123!");
    expect(signButton.disabled).toBe(true);

    await user.type(screen.getByLabelText(/Reason for withdrawal/i), "Retraining required");
    expect(signButton.disabled).toBe(false);

    await user.click(signButton);

    await waitFor(() =>
      expect(competencyService.withdraw).toHaveBeenCalledWith(55, {
        reason: "Retraining required",
        password: "Secret123!"
      })
    );
    expect(onWithdrawn).toHaveBeenCalled();
  });

  it("shows wrong password and server errors in the dialog", async () => {
    const user = userEvent.setup();
    vi.mocked(competencyService.withdraw)
      .mockRejectedValueOnce(
        new AxiosError("Unauthorized", "401", undefined, undefined, {
          status: 401,
          statusText: "Unauthorized",
          headers: {},
          config: {} as never,
          data: { message: "Invalid signing password." }
        } as AxiosResponse)
      )
      .mockRejectedValueOnce(
        new AxiosError("Bad Request", "400", undefined, undefined, {
          status: 400,
          statusText: "Bad Request",
          headers: {},
          config: {} as never,
          data: { message: "Qualification is already withdrawn." }
        } as AxiosResponse)
      );

    renderDialog();
    await user.type(screen.getByLabelText(/Reason for withdrawal/i), "Retraining");
    await user.type(screen.getByLabelText(/Password/i), "wrong");
    await user.click(screen.getByRole("button", { name: "Sign" }));
    expect(await screen.findByText("Invalid signing password.")).toBeTruthy();

    await user.type(screen.getByLabelText(/Password/i), "Secret123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));
    expect(await screen.findByText("Qualification is already withdrawn.")).toBeTruthy();
  });
});
