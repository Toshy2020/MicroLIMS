import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosResponse } from "axios";
import { MemoryRouter } from "react-router-dom";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../theme";
import { AuthProvider } from "../../contexts/AuthContext";
import { OverrideDialog, type OverrideCandidate } from "./OverrideDialog";
import { competencyService } from "../../services/competencyService";

vi.mock("../../services/competencyService", () => ({
  competencyService: {
    grantOverride: vi.fn()
  }
}));

const candidates: OverrideCandidate[] = [
  { userId: 3, fullName: "Mona Ali", reason: "Mona Ali is not qualified for TAMC." },
  { userId: 4, fullName: "Omar Saleh", reason: "Omar Saleh has not acknowledged SOP-01 revision 2." }
];

function renderDialog(onGranted = vi.fn(), onClose = vi.fn()) {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter>
          <OverrideDialog
            open
            testOrderId={501}
            testLabel="TAMC — Total Aerobic Microbial Count"
            candidates={candidates}
            onClose={onClose}
            onGranted={onGranted}
          />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

function makeServerError(status: number, message: string): AxiosError {
  return new AxiosError("Error", String(status), undefined, undefined, {
    status,
    statusText: "Error",
    headers: {},
    config: {} as never,
    data: { message }
  } as AxiosResponse);
}

describe("OverrideDialog", () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem("microlims_full_name", "Hana Head");
    localStorage.setItem("microlims_role", "SectionHead");
    vi.mocked(competencyService.grantOverride).mockResolvedValue(99);
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("sends selected analyst, reason, and password via SignatureDialog", async () => {
    const user = userEvent.setup();
    const onGranted = vi.fn();
    renderDialog(onGranted);

    await user.click(screen.getByRole("combobox", { name: "Analyst" }));
    await user.click(within(screen.getByRole("listbox")).getByRole("option", { name: /Omar Saleh/i }));

    await user.type(screen.getByRole("textbox", { name: /Reason for override/i }), "Urgent batch release under supervision");
    await user.click(screen.getByRole("button", { name: "Continue to Sign" }));

    const passwordInput = await screen.findByLabelText(/Password/i);
    await user.type(passwordInput, "Secret123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() =>
      expect(competencyService.grantOverride).toHaveBeenCalledWith(501, {
        analystUserId: 4,
        reason: "Urgent batch release under supervision",
        password: "Secret123!"
      })
    );
    expect(onGranted).toHaveBeenCalledWith(99);
  });

  it("requires an override reason before advancing to signature", async () => {
    const user = userEvent.setup();
    renderDialog();

    await user.click(screen.getByRole("button", { name: "Continue to Sign" }));

    expect(await screen.findByText("Override reason is required.")).toBeTruthy();
    expect(competencyService.grantOverride).not.toHaveBeenCalled();
  });

  it.each([
    ["not needed", 400, "A competency override is not needed."],
    ["already active", 400, "This test order already has an active competency override."],
    ["self-override", 400, "You cannot override your own competency."],
    ["closed order", 400, "This test order is not open for a competency override."],
    ["wrong password", 401, "Signature password verification failed."]
  ])("shows server refusal (%s) inline and keeps the dialog open", async (_label, status, serverMessage) => {
    const user = userEvent.setup();
    const onGranted = vi.fn();
    const onClose = vi.fn();
    vi.mocked(competencyService.grantOverride).mockRejectedValueOnce(makeServerError(status, serverMessage));

    renderDialog(onGranted, onClose);
    await user.type(screen.getByRole("textbox", { name: /Reason for override/i }), "Supervised execution");
    await user.click(screen.getByRole("button", { name: "Continue to Sign" }));

    await user.type(await screen.findByLabelText(/Password/i), "Secret123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    const alertMsg = await screen.findByText(serverMessage);
    expect(alertMsg.closest('[role="alert"]')).toBeTruthy();
    expect(screen.getByRole("button", { name: "Sign" })).toBeTruthy();
    expect(onGranted).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
  });
});
