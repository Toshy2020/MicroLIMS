import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { AuthProvider } from "../contexts/AuthContext";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { mfaService } from "../modules/authentication/services/mfaService";
import { LoginPage } from "./Login";

vi.mock("../modules/authentication/services/authenticationService", () => ({
  authenticationService: {
    login: vi.fn(),
    me: vi.fn(),
    requestPasswordReset: vi.fn()
  }
}));

vi.mock("../modules/authentication/services/mfaService", () => ({
  mfaService: {
    verify: vi.fn(),
    getOptions: vi.fn()
  }
}));

function EnrolProbe() {
  const location = useLocation();
  const state = location.state as { mfaToken?: string } | null;
  return (
    <div
      data-testid="mfa-enrol-route"
      data-token={state?.mfaToken ?? ""}
      data-search={location.search}
    />
  );
}

function renderPage() {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={["/login"]}>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/dashboard" element={<div>Dashboard</div>} />
            <Route path="/mfa-enrol" element={<EnrolProbe />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

async function submitCredentials(
  user: ReturnType<typeof userEvent.setup>,
  username = "a.hassan",
  password = "Secret#1"
) {
  await user.type(screen.getByLabelText(/^Username/), username);
  await user.type(screen.getByLabelText(/^Password/), password);
  await user.click(screen.getByRole("button", { name: "Sign In" }));
}

afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.clearAllMocks();
  vi.useRealTimers();
});

describe("LoginPage", () => {
  it("shows what the system is beside the sign-in form, with no remember option", () => {
    renderPage();
    expect(screen.getByRole("heading", { level: 1, name: "Sign in" })).toBeTruthy();
    expect(screen.getByText("Pharmaceutical Quality Control")).toBeTruthy();
    expect(screen.getByText("FDA 21 CFR Part 11")).toBeTruthy();
    expect(screen.getByText("EU GMP Annex 11")).toBeTruthy();
    expect(screen.getByText("Authorised users only. Every sign-in attempt is recorded.")).toBeTruthy();
    expect(screen.queryByRole("checkbox")).toBeNull();
  });

  it("signs in with the entered credentials", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.login).mockResolvedValue({
      token: "t",
      refreshToken: "r",
      role: "Analyst",
      roleName: "Analyst",
      permissions: [],
      mustChangePassword: false
    });
    vi.mocked(authenticationService.me).mockResolvedValue({
      userId: 7,
      username: "a.hassan",
      fullName: "A Hassan",
      role: "Analyst",
      roleName: "Analyst",
      lastLoginAt: null,
      passwordChangedAt: null,
      mustChangePassword: false
    });
    renderPage();

    await submitCredentials(user);

    await waitFor(() => expect(screen.getByText("Dashboard")).toBeTruthy());
    expect(authenticationService.login).toHaveBeenCalledWith("a.hassan", "Secret#1");
    expect(localStorage.getItem("microlims_token")).toBe("t");
    expect(localStorage.getItem("microlims_refresh_token")).toBe("r");
  });

  it("offers both recovery routes on the help screen", async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByRole("button", { name: "Need help signing in?" }));

    expect(screen.getByRole("heading", { level: 1, name: "Need help signing in?" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Send Reset Link" })).toBeTruthy();
    expect(screen.getByRole("link", { name: "Have a recovery code from your administrator?" })).toBeTruthy();
  });

  it("shows the code step when login returns mfaRequired, and does not store tokens", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.login).mockResolvedValue({
      mfaRequired: true,
      mfaToken: "challenge-token-1"
    });
    vi.mocked(mfaService.getOptions).mockResolvedValue({ trustedDeviceHours: 12 });
    renderPage();

    await submitCredentials(user);

    await waitFor(() =>
      expect(screen.getByRole("heading", { level: 1, name: "Enter the 6-digit code" })).toBeTruthy()
    );
    expect(screen.getByLabelText("6-digit code")).toBeTruthy();
    expect(localStorage.getItem("microlims_token")).toBeNull();
    expect(localStorage.getItem("microlims_refresh_token")).toBeNull();
    expect(localStorage.getItem("challenge-token-1")).toBeNull();
  });

  it("submits the code and signs in", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.login).mockResolvedValue({
      mfaRequired: true,
      mfaToken: "challenge-token-1"
    });
    vi.mocked(mfaService.getOptions).mockResolvedValue({ trustedDeviceHours: 12 });
    vi.mocked(mfaService.verify).mockResolvedValue({
      token: "mfa-access-token",
      refreshToken: "mfa-refresh-token",
      role: "SectionHead",
      roleName: "Section Head",
      permissions: ["Samples.View"],
      mustChangePassword: true
    });
    vi.mocked(authenticationService.me).mockResolvedValue({
      userId: 12,
      username: "a.hassan",
      fullName: "A Hassan",
      role: "SectionHead",
      roleName: "Section Head",
      lastLoginAt: null,
      passwordChangedAt: null,
      mustChangePassword: true
    });
    renderPage();

    await submitCredentials(user);
    await waitFor(() => expect(screen.getByLabelText("6-digit code")).toBeTruthy());

    await user.type(screen.getByLabelText("6-digit code"), "654321");
    await user.click(screen.getByRole("button", { name: "Verify" }));

    await waitFor(() => expect(screen.getByText("Dashboard")).toBeTruthy());
    expect(mfaService.verify).toHaveBeenCalledWith({
      mfaToken: "challenge-token-1",
      code: "654321",
      trustDevice: false
    });
    expect(localStorage.getItem("microlims_token")).toBe("mfa-access-token");
    expect(localStorage.getItem("microlims_refresh_token")).toBe("mfa-refresh-token");
    expect(localStorage.getItem("microlims_must_change_password")).toBe("true");
    expect(localStorage.getItem("microlims_role_name")).toBe("Section Head");
  });

  it("sends the stored device token with the password", async () => {
    const user = userEvent.setup();
    localStorage.setItem("microlims_device_token", "trusted-dev-123");
    vi.mocked(authenticationService.login).mockResolvedValue({
      token: "t-trusted",
      refreshToken: "r-trusted",
      role: "Analyst",
      roleName: "Analyst",
      permissions: [],
      mustChangePassword: false
    });
    vi.mocked(authenticationService.me).mockResolvedValue({
      userId: 7,
      username: "a.hassan",
      fullName: "A Hassan",
      role: "Analyst",
      lastLoginAt: null,
      passwordChangedAt: null,
      mustChangePassword: false
    });
    renderPage();

    await submitCredentials(user);

    await waitFor(() => expect(screen.getByText("Dashboard")).toBeTruthy());
    expect(authenticationService.login).toHaveBeenCalledWith("a.hassan", "Secret#1", "trusted-dev-123");
  });

  it("clears the stored device token when the server asks for a code anyway", async () => {
    const user = userEvent.setup();
    localStorage.setItem("microlims_device_token", "stale-dev-999");
    vi.mocked(authenticationService.login).mockResolvedValue({
      mfaRequired: true,
      mfaToken: "challenge-after-stale"
    });
    vi.mocked(mfaService.getOptions).mockResolvedValue({ trustedDeviceHours: 12 });
    renderPage();

    await submitCredentials(user);

    await waitFor(() =>
      expect(screen.getByRole("heading", { level: 1, name: "Enter the 6-digit code" })).toBeTruthy()
    );
    expect(localStorage.getItem("microlims_device_token")).toBeNull();
  });

  it('shows "The code is not correct." in an alert and keeps focus in the code field', async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.login).mockResolvedValue({
      mfaRequired: true,
      mfaToken: "challenge-token-1"
    });
    vi.mocked(mfaService.getOptions).mockResolvedValue({ trustedDeviceHours: 12 });
    vi.mocked(mfaService.verify).mockRejectedValue({
      response: { status: 401, data: { message: "The code is not correct." } }
    });
    renderPage();

    await submitCredentials(user);
    await waitFor(() => expect(screen.getByLabelText("6-digit code")).toBeTruthy());

    await user.type(screen.getByLabelText("6-digit code"), "111111");
    await user.click(screen.getByRole("button", { name: "Verify" }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("The code is not correct.");
    await waitFor(() => expect(document.activeElement).toBe(screen.getByLabelText("6-digit code")));
  });

  it("switches to the recovery code field and submits it", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.login).mockResolvedValue({
      mfaRequired: true,
      mfaToken: "challenge-token-1"
    });
    vi.mocked(mfaService.getOptions).mockResolvedValue({ trustedDeviceHours: 12 });
    vi.mocked(mfaService.verify).mockResolvedValue({
      token: "rec-token",
      refreshToken: "rec-refresh",
      role: "Analyst",
      permissions: [],
      mustChangePassword: false
    });
    vi.mocked(authenticationService.me).mockResolvedValue({
      userId: 7,
      username: "a.hassan",
      fullName: "A Hassan",
      role: "Analyst",
      lastLoginAt: null,
      passwordChangedAt: null,
      mustChangePassword: false
    });
    renderPage();

    await submitCredentials(user);
    await waitFor(() => expect(screen.getByRole("button", { name: "Use a recovery code instead" })).toBeTruthy());

    await user.click(screen.getByRole("button", { name: "Use a recovery code instead" }));
    const recoveryField = screen.getByLabelText("Recovery code");
    expect(recoveryField).toBeTruthy();

    await user.type(recoveryField, "ABCD-EFGH");
    await user.click(screen.getByRole("button", { name: "Verify" }));

    await waitFor(() => expect(screen.getByText("Dashboard")).toBeTruthy());
    expect(mfaService.verify).toHaveBeenCalledWith({
      mfaToken: "challenge-token-1",
      recoveryCode: "ABCD-EFGH",
      trustDevice: false
    });
  });

  it('stores the device token when "Trust this computer" is ticked, and sends false when unticked', async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.login).mockResolvedValue({
      mfaRequired: true,
      mfaToken: "challenge-token-1"
    });
    vi.mocked(mfaService.getOptions).mockResolvedValue({ trustedDeviceHours: 8 });
    vi.mocked(mfaService.verify).mockResolvedValue({
      token: "t-ok",
      refreshToken: "r-ok",
      role: "Analyst",
      permissions: [],
      mustChangePassword: false,
      deviceToken: "new-device-tok"
    });
    vi.mocked(authenticationService.me).mockResolvedValue({
      userId: 7,
      username: "a.hassan",
      fullName: "A Hassan",
      role: "Analyst",
      lastLoginAt: null,
      passwordChangedAt: null,
      mustChangePassword: false
    });
    renderPage();

    await submitCredentials(user);
    const checkbox = await screen.findByRole("checkbox", { name: /Trust this computer for 8 hours/i });
    expect((checkbox as HTMLInputElement).checked).toBe(false);

    await user.click(checkbox);
    await user.type(screen.getByLabelText("6-digit code"), "123456");
    await user.click(screen.getByRole("button", { name: "Verify" }));

    await waitFor(() => expect(screen.getByText("Dashboard")).toBeTruthy());
    expect(mfaService.verify).toHaveBeenCalledWith({
      mfaToken: "challenge-token-1",
      code: "123456",
      trustDevice: true
    });
    expect(localStorage.getItem("microlims_device_token")).toBe("new-device-tok");
  });

  it("hides the trust checkbox when the server says trusted hours is 0", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.login).mockResolvedValue({
      mfaRequired: true,
      mfaToken: "challenge-token-1"
    });
    vi.mocked(mfaService.getOptions).mockResolvedValue({ trustedDeviceHours: 0 });
    renderPage();

    await submitCredentials(user);

    await waitFor(() =>
      expect(screen.getByRole("heading", { level: 1, name: "Enter the 6-digit code" })).toBeTruthy()
    );
    await waitFor(() => expect(mfaService.getOptions).toHaveBeenCalled());
    expect(screen.queryByRole("checkbox", { name: /Trust this computer/i })).toBeNull();
  });

  it("hides the trust checkbox when fetching MFA options fails", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.login).mockResolvedValue({
      mfaRequired: true,
      mfaToken: "challenge-token-1"
    });
    vi.mocked(mfaService.getOptions).mockRejectedValue(new Error("network"));
    renderPage();

    await submitCredentials(user);

    await waitFor(() =>
      expect(screen.getByRole("heading", { level: 1, name: "Enter the 6-digit code" })).toBeTruthy()
    );
    await waitFor(() => expect(mfaService.getOptions).toHaveBeenCalled());
    expect(screen.queryByRole("checkbox", { name: /Trust this computer/i })).toBeNull();
  });

  it("returns to the password step when the challenge has expired", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    vi.mocked(authenticationService.login).mockResolvedValue({
      mfaRequired: true,
      mfaToken: "challenge-token-1"
    });
    vi.mocked(mfaService.getOptions).mockResolvedValue({ trustedDeviceHours: 12 });
    renderPage();

    await submitCredentials(user);
    await waitFor(() =>
      expect(screen.getByRole("heading", { level: 1, name: "Enter the 6-digit code" })).toBeTruthy()
    );

    act(() => {
      vi.advanceTimersByTime(5 * 60 * 1000);
    });

    await waitFor(() =>
      expect(screen.getByRole("heading", { level: 1, name: "Sign in" })).toBeTruthy()
    );
    expect(screen.getByRole("alert").textContent).toContain("Your sign-in took too long. Enter your password again.");
  });

  it("returns to the password step after 5 refused codes", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.login).mockResolvedValue({
      mfaRequired: true,
      mfaToken: "challenge-token-1"
    });
    vi.mocked(mfaService.getOptions).mockResolvedValue({ trustedDeviceHours: 12 });
    vi.mocked(mfaService.verify).mockRejectedValue({
      response: { status: 401, data: { message: "The code is not correct." } }
    });
    renderPage();

    await submitCredentials(user);
    await waitFor(() => expect(screen.getByLabelText("6-digit code")).toBeTruthy());

    for (let attempt = 0; attempt < 5; attempt++) {
      const codeInput = screen.getByLabelText("6-digit code") as HTMLInputElement;
      await user.clear(codeInput);
      await user.type(codeInput, "000000");
      await user.click(screen.getByRole("button", { name: "Verify" }));
    }

    await waitFor(() =>
      expect(screen.getByRole("heading", { level: 1, name: "Sign in" })).toBeTruthy()
    );
    expect(screen.getByRole("alert").textContent).toMatch(/Sign in again/i);
  });

  it("warns how many recovery codes remain after using one", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.login).mockResolvedValue({
      mfaRequired: true,
      mfaToken: "challenge-token-1"
    });
    vi.mocked(mfaService.getOptions).mockResolvedValue({ trustedDeviceHours: 12 });
    vi.mocked(mfaService.verify).mockResolvedValue({
      token: "rec-token",
      refreshToken: "rec-refresh",
      role: "Analyst",
      permissions: [],
      mustChangePassword: false,
      recoveryCodesRemaining: 3
    });
    vi.mocked(authenticationService.me).mockResolvedValue({
      userId: 7,
      username: "a.hassan",
      fullName: "A Hassan",
      role: "Analyst",
      lastLoginAt: null,
      passwordChangedAt: null,
      mustChangePassword: false
    });
    renderPage();

    await submitCredentials(user);
    await waitFor(() => expect(screen.getByRole("button", { name: "Use a recovery code instead" })).toBeTruthy());

    await user.click(screen.getByRole("button", { name: "Use a recovery code instead" }));
    await user.type(screen.getByLabelText("Recovery code"), "REC-1234");
    await user.click(screen.getByRole("button", { name: "Verify" }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("3");
    expect(alert.textContent).toMatch(/recovery code/i);

    await user.click(screen.getByRole("button", { name: "Continue" }));
    await waitFor(() => expect(screen.getByText("Dashboard")).toBeTruthy());
  });

  it("navigates to /mfa-enrol with the token in router state when enrolment is required", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.login).mockResolvedValue({
      mfaEnrolmentRequired: true,
      mfaToken: "enrol-state-token"
    });
    renderPage();

    await submitCredentials(user);

    const enrolProbe = await screen.findByTestId("mfa-enrol-route");
    expect(enrolProbe.getAttribute("data-token")).toBe("enrol-state-token");
    expect(enrolProbe.getAttribute("data-search")).toBe("");
    expect(localStorage.getItem("microlims_token")).toBeNull();
  });

  it("shows recovery codes once when a hardware token is confirmed at sign-in", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.login).mockResolvedValue({
      mfaRequired: true,
      mfaToken: "hw-challenge"
    });
    vi.mocked(mfaService.getOptions).mockResolvedValue({ trustedDeviceHours: 0 });
    vi.mocked(mfaService.verify).mockResolvedValue({
      token: "hw-access",
      refreshToken: "hw-refresh",
      role: "Analyst",
      permissions: [],
      mustChangePassword: false,
      recoveryCodes: ["AAAA-1111", "BBBB-2222"]
    });
    vi.mocked(authenticationService.me).mockResolvedValue({
      userId: 7,
      username: "a.hassan",
      fullName: "A Hassan",
      role: "Analyst",
      lastLoginAt: null,
      passwordChangedAt: null,
      mustChangePassword: false
    });
    renderPage();

    await submitCredentials(user);
    await waitFor(() => expect(screen.getByLabelText("6-digit code")).toBeTruthy());

    await user.type(screen.getByLabelText("6-digit code"), "222333");
    await user.click(screen.getByRole("button", { name: "Verify" }));

    await waitFor(() => expect(screen.getByText("AAAA-1111")).toBeTruthy());
    expect(screen.getByText("BBBB-2222")).toBeTruthy();

    const continueBtn = screen.getByRole("button", { name: "Continue" }) as HTMLButtonElement;
    expect(continueBtn.disabled).toBe(true);
    expect(localStorage.getItem("microlims_token")).toBeNull();

    await user.click(screen.getByRole("checkbox", { name: "I have saved these codes" }));
    expect(continueBtn.disabled).toBe(false);
    await user.click(continueBtn);

    await waitFor(() => expect(screen.getByText("Dashboard")).toBeTruthy());
    expect(localStorage.getItem("microlims_token")).toBe("hw-access");
  });

  it("returns to the password step and discards the challenge token when switching account", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.login).mockResolvedValue({
      mfaRequired: true,
      mfaToken: "discard-me"
    });
    vi.mocked(mfaService.getOptions).mockResolvedValue({ trustedDeviceHours: 12 });
    renderPage();

    await submitCredentials(user);
    await waitFor(() => expect(screen.getByRole("button", { name: "Use a different account" })).toBeTruthy());

    await user.click(screen.getByRole("button", { name: "Use a different account" }));

    await waitFor(() => expect(screen.getByRole("heading", { level: 1, name: "Sign in" })).toBeTruthy());
    expect((screen.getByLabelText(/^Password/) as HTMLInputElement).value).toBe("");
  });
});
