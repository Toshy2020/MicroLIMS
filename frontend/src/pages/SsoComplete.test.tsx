import { StrictMode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { AuthProvider } from "../contexts/AuthContext";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { ssoService } from "../modules/authentication/services/ssoService";
import { SsoCompletePage } from "./SsoComplete";

vi.mock("../modules/authentication/services/ssoService", () => ({
  ssoService: {
    getMode: vi.fn(),
    startUrl: vi.fn(),
    exchange: vi.fn()
  }
}));

vi.mock("../modules/authentication/services/authenticationService", () => ({
  authenticationService: {
    me: vi.fn()
  }
}));

function tokenFor(role: string): string {
  const payload = btoa(
    JSON.stringify({
      "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": role,
      permission: "Samples.View"
    })
  );
  return `h.${payload}.s`;
}

function renderSsoComplete(strictMode = false) {
  const tree = (
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={["/sso-complete"]}>
          <Routes>
            <Route path="/sso-complete" element={<SsoCompletePage />} />
            <Route path="/login" element={<div>Login Page</div>} />
            <Route path="/set-signing-password" element={<div>Set Signing Password Page</div>} />
            <Route path="/" element={<div>Dashboard Home</div>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
  return render(strictMode ? <StrictMode>{tree}</StrictMode> : tree);
}

afterEach(() => {
  cleanup();
  localStorage.clear();
  window.history.replaceState(null, "", "/");
  vi.restoreAllMocks();
  vi.clearAllMocks();
});

describe("SsoCompletePage", () => {
  it("SsoComplete reads the code from the fragment, clears it from the address bar, exchanges it and signs in", async () => {
    window.history.replaceState(null, "", "/sso-complete#code=one-time-code-123");
    const replaceSpy = vi.spyOn(window.history, "replaceState");

    vi.mocked(ssoService.exchange).mockResolvedValue({
      token: tokenFor("Analyst"),
      refreshToken: "ref-1",
      role: "Analyst",
      roleName: "Analyst",
      permissions: ["Samples.View"],
      mustChangePassword: false,
      signingPasswordRequired: false
    });
    vi.mocked(authenticationService.me).mockResolvedValue({
      userId: 7,
      username: "dir.user",
      fullName: "Directory User",
      role: "Analyst",
      lastLoginAt: null,
      passwordChangedAt: null,
      mustChangePassword: false,
      signInMethod: "Directory",
      signingPasswordSet: true
    });

    renderSsoComplete(true);

    expect(screen.getByRole("status")).toBeTruthy();

    await waitFor(() => {
      expect(screen.getByText("Dashboard Home")).toBeTruthy();
    });
    expect(replaceSpy).toHaveBeenCalled();
    expect(window.location.hash).toBe("");
    expect(ssoService.exchange).toHaveBeenCalledTimes(1);
    expect(ssoService.exchange).toHaveBeenCalledWith("one-time-code-123");
  });

  it('SsoComplete with #error shows the generic message and "Back to sign in"', async () => {
    window.history.replaceState(null, "", "/sso-complete#error=signin_failed");
    const replaceSpy = vi.spyOn(window.history, "replaceState");

    renderSsoComplete();

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Company sign-in did not complete. Try again or contact your administrator.");
    const backLink = screen.getByRole("link", { name: "Back to sign in" });
    expect(backLink.getAttribute("href")).toBe("/login");
    expect(replaceSpy).toHaveBeenCalled();
    expect(window.location.hash).toBe("");
    expect(ssoService.exchange).not.toHaveBeenCalled();
  });

  it("SsoComplete sends a directory user without a signing password to /set-signing-password", async () => {
    window.history.replaceState(null, "", "/sso-complete#code=first-login-code");

    vi.mocked(ssoService.exchange).mockResolvedValue({
      token: tokenFor("Analyst"),
      refreshToken: "ref-2",
      role: "Analyst",
      roleName: "Analyst",
      permissions: ["Samples.View"],
      mustChangePassword: false,
      signingPasswordRequired: true
    });
    vi.mocked(authenticationService.me).mockResolvedValue({
      userId: 8,
      username: "new.dir.user",
      fullName: "New Directory User",
      role: "Analyst",
      lastLoginAt: null,
      passwordChangedAt: null,
      mustChangePassword: false,
      signInMethod: "Directory",
      signingPasswordSet: false
    });

    renderSsoComplete();

    expect(await screen.findByText("Set Signing Password Page")).toBeTruthy();
    expect(localStorage.getItem("microlims_signing_password_required")).toBe("true");
  });

  it("SsoComplete with no code shows the error state", async () => {
    window.history.replaceState(null, "", "/sso-complete");

    renderSsoComplete();

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Company sign-in did not complete. Try again or contact your administrator.");
    expect(screen.getByRole("link", { name: "Back to sign in" })).toBeTruthy();
    expect(ssoService.exchange).not.toHaveBeenCalled();
  });
});
