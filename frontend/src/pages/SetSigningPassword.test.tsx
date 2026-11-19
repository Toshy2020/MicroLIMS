import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { ThemeModeProvider } from "../theme/ThemeModeContext";
import { AuthProvider } from "../contexts/AuthContext";
import { MainLayout } from "../layouts/MainLayout";
import { SetSigningPasswordPage } from "./SetSigningPassword";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { mfaService } from "../modules/authentication/services/mfaService";

vi.mock("../services/apiClient", () => ({
  apiClient: {
    get: vi.fn((url: string) =>
      Promise.resolve({ data: { data: url.includes("/notifications") ? [] : { unreadCount: 0 } } })
    ),
    post: vi.fn().mockResolvedValue({})
  }
}));

vi.mock("../services/laboratorySectionService", () => ({
  laboratorySectionService: {
    getMySections: vi.fn().mockResolvedValue([])
  }
}));

vi.mock("../modules/authentication/services/mfaService", () => ({
  mfaService: {
    getStatus: vi.fn().mockResolvedValue({
      enrolled: false,
      type: null,
      confirmedAt: null,
      recoveryCodesRemaining: 0,
      required: false,
      enrolmentDueAt: null,
      trustedDevices: []
    })
  }
}));

vi.mock("../modules/authentication/services/authenticationService", () => ({
  authenticationService: {
    setSigningPassword: vi.fn(),
    me: vi.fn()
  }
}));

function seedDirectorySession(signingPasswordRequired = true) {
  const payload = btoa(
    JSON.stringify({
      "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "Analyst",
      permission: "Samples.View"
    })
  );
  localStorage.setItem("microlims_token", `h.${payload}.s`);
  localStorage.setItem("microlims_username", "dir.user");
  localStorage.setItem("microlims_full_name", "Directory User");
  localStorage.setItem("microlims_sign_in_method", "Directory");
  localStorage.setItem("microlims_signing_password_required", String(signingPasswordRequired));
}

function renderWithLayout(initialPath = "/dashboard") {
  return render(
    <ThemeModeProvider>
      <AuthProvider>
        <MemoryRouter initialEntries={[initialPath]}>
          <Routes>
            <Route element={<MainLayout />}>
              <Route path="/" element={<div>Home Dashboard</div>} />
              <Route path="/dashboard" element={<div>Dashboard Outlet</div>} />
              <Route path="/reports" element={<div>Reports Outlet</div>} />
              <Route path="/set-signing-password" element={<SetSigningPasswordPage />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </ThemeModeProvider>
  );
}

afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.clearAllMocks();
});

describe("SetSigningPasswordPage", () => {
  it("blocks navigation to other routes until signing password is saved", async () => {
    seedDirectorySession(true);
    renderWithLayout("/reports");

    expect(await screen.findByRole("heading", { name: "Choose your signing password" })).toBeTruthy();
    expect(screen.getByText(/used only for electronic signatures in MicroLIMS and must not be your company password/i)).toBeTruthy();
    expect(screen.queryByText("Reports Outlet")).toBeNull();
  });

  it("shows an inline mismatch error when confirmation does not match", async () => {
    const user = userEvent.setup();
    seedDirectorySession(true);
    renderWithLayout("/set-signing-password");

    await user.type(screen.getByLabelText(/^New Signing Password/), "ValidPass1!");
    await user.type(screen.getByLabelText(/^Confirm Signing Password/), "Different1!");
    await user.click(screen.getByRole("button", { name: "Save Signing Password" }));

    expect(screen.getByText("Signing passwords do not match.")).toBeTruthy();
    expect(screen.getByRole("alert").textContent).toContain("Signing password and confirmation do not match.");
    expect(authenticationService.setSigningPassword).not.toHaveBeenCalled();
  });

  it("shows backend policy errors inline when saving fails", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.setSigningPassword).mockRejectedValue({
      response: { data: { message: "This signing password was recently used." } }
    });
    seedDirectorySession(true);
    renderWithLayout("/set-signing-password");

    await user.type(screen.getByLabelText(/^New Signing Password/), "ValidPass1!");
    await user.type(screen.getByLabelText(/^Confirm Signing Password/), "ValidPass1!");
    await user.click(screen.getByRole("button", { name: "Save Signing Password" }));

    const alerts = await screen.findAllByRole("alert");
    expect(alerts.some((a) => a.textContent?.includes("This signing password was recently used."))).toBe(true);
    expect(localStorage.getItem("microlims_signing_password_required")).toBe("true");
  });

  it("calls the service with the typed password, clears the gate, and navigates to / on success", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.setSigningPassword).mockResolvedValue({});
    vi.mocked(mfaService.getStatus).mockResolvedValue({
      enrolled: false,
      type: null,
      confirmedAt: null,
      recoveryCodesRemaining: 0,
      required: false,
      enrolmentDueAt: null,
      trustedDevices: []
    });
    seedDirectorySession(true);
    renderWithLayout("/dashboard");

    await screen.findByRole("heading", { name: "Choose your signing password" });

    await user.type(screen.getByLabelText(/^New Signing Password/), "ValidPass1!");
    await user.type(screen.getByLabelText(/^Confirm Signing Password/), "ValidPass1!");
    await user.click(screen.getByRole("button", { name: "Save Signing Password" }));

    await waitFor(() => {
      expect(authenticationService.setSigningPassword).toHaveBeenCalledWith("ValidPass1!");
    });
    expect(localStorage.getItem("microlims_signing_password_required")).toBe("false");
    expect(await screen.findByText("Home Dashboard")).toBeTruthy();
  });
});
