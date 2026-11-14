import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { AuthProvider } from "../contexts/AuthContext";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { mfaService } from "../modules/authentication/services/mfaService";
import { MfaEnrolmentPage } from "./MfaEnrolment";

const toastError = vi.fn();
vi.mock("sonner", () => ({
  toast: {
    success: vi.fn(),
    error: (...args: unknown[]) => toastError(...args)
  }
}));

vi.mock("../modules/authentication/services/authenticationService", () => ({
  authenticationService: {
    me: vi.fn()
  }
}));

vi.mock("../modules/authentication/services/mfaService", () => ({
  mfaService: {
    startEnrolmentWithToken: vi.fn(),
    confirmEnrolmentWithToken: vi.fn(),
    startEnrolment: vi.fn(),
    confirmEnrolment: vi.fn()
  }
}));

const sampleStart = {
  manualKey: "JBSWY3DPEHPK3PXP",
  otpAuthUri: "otpauth://totp/MicroLIMS:sam?secret=JBSWY3DPEHPK3PXP",
  qrSvg: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="10" height="10"/></svg>'
};

function renderEnrol(initialState?: { mfaToken?: string }) {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={[{ pathname: "/mfa-enrol", state: initialState }]}>
          <Routes>
            <Route path="/mfa-enrol" element={<MfaEnrolmentPage />} />
            <Route path="/login" element={<div>Login Page</div>} />
            <Route path="/my-work" element={<div>My Work</div>} />
            <Route path="/profile" element={<div>Profile</div>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.clearAllMocks();
});

describe("MfaEnrolmentPage", () => {
  it("forced enrolment without a token redirects to login", async () => {
    renderEnrol();

    await waitFor(() => expect(screen.getByText("Login Page")).toBeTruthy());
  });

  it("the QR is rendered as an img data URI and the page contains no injected svg element", async () => {
    vi.mocked(mfaService.startEnrolmentWithToken).mockResolvedValue(sampleStart);
    const { container } = renderEnrol({ mfaToken: "enrol-tok-1" });

    const img = (await screen.findByAltText(
      "QR code to add MicroLIMS to your authenticator app"
    )) as HTMLImageElement;
    expect(img.src.startsWith("data:image/svg+xml")).toBe(true);
    expect(container.querySelector("svg")).toBeNull();
  });

  it("forced enrolment signs the user in after the codes step and tokens are stored only after the codes are acknowledged", async () => {
    const user = userEvent.setup();
    vi.mocked(mfaService.startEnrolmentWithToken).mockResolvedValue(sampleStart);
    vi.mocked(mfaService.confirmEnrolmentWithToken).mockResolvedValue({
      recoveryCodes: ["REC-1111", "REC-2222"],
      login: {
        token: "enrol-access-tok",
        refreshToken: "enrol-refresh-tok",
        role: "QcManager",
        roleName: "QC Manager",
        permissions: ["Samples.View"],
        mustChangePassword: false
      }
    });
    vi.mocked(authenticationService.me).mockResolvedValue({
      userId: 9,
      username: "qc.lead",
      fullName: "QC Lead",
      role: "QcManager",
      roleName: "QC Manager",
      lastLoginAt: null,
      passwordChangedAt: null,
      mustChangePassword: false
    });

    renderEnrol({ mfaToken: "enrol-tok-1" });

    await user.type(await screen.findByLabelText("6-digit code"), "654321");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => expect(screen.getByText("REC-1111")).toBeTruthy());
    expect(localStorage.getItem("microlims_token")).toBeNull();
    expect(localStorage.getItem("microlims_refresh_token")).toBeNull();

    await user.click(screen.getByRole("checkbox", { name: "I have saved these codes" }));
    await user.click(screen.getByRole("button", { name: "Continue" }));

    await waitFor(() => expect(screen.getByText("My Work")).toBeTruthy());
    expect(localStorage.getItem("microlims_token")).toBe("enrol-access-tok");
    expect(localStorage.getItem("microlims_refresh_token")).toBe("enrol-refresh-tok");
  });

  it("tokens are stored only after the codes are acknowledged", async () => {
    const user = userEvent.setup();
    vi.mocked(mfaService.startEnrolmentWithToken).mockResolvedValue(sampleStart);
    vi.mocked(mfaService.confirmEnrolmentWithToken).mockResolvedValue({
      recoveryCodes: ["REC-9999"],
      login: {
        token: "delayed-token",
        refreshToken: "delayed-refresh",
        role: "Analyst",
        permissions: [],
        mustChangePassword: false
      }
    });
    vi.mocked(authenticationService.me).mockResolvedValue({
      userId: 3,
      username: "analyst",
      fullName: "Lab Analyst",
      role: "Analyst",
      lastLoginAt: null,
      passwordChangedAt: null,
      mustChangePassword: false
    });

    renderEnrol({ mfaToken: "enrol-tok-2" });

    await user.type(await screen.findByLabelText("6-digit code"), "123456");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => expect(screen.getByText("REC-9999")).toBeTruthy());
    expect(localStorage.getItem("microlims_token")).toBeNull();
  });

  it("collects password before starting enrolment when signed in", async () => {
    const user = userEvent.setup();
    localStorage.setItem("microlims_token", "existing-jwt");
    localStorage.setItem("microlims_role", "Analyst");
    vi.mocked(mfaService.startEnrolment).mockResolvedValue(sampleStart);
    vi.mocked(mfaService.confirmEnrolment).mockResolvedValue({
      recoveryCodes: ["VOL-1111"]
    });

    renderEnrol();

    const passwordInput = await screen.findByLabelText("Password");
    await user.type(passwordInput, "CurrentPass#1");
    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(await screen.findByAltText("QR code to add MicroLIMS to your authenticator app")).toBeTruthy();
    expect(mfaService.startEnrolment).toHaveBeenCalledWith("CurrentPass#1");

    await user.type(screen.getByLabelText("6-digit code"), "112233");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => expect(screen.getByText("VOL-1111")).toBeTruthy());
    await user.click(screen.getByRole("checkbox", { name: "I have saved these codes" }));
    await user.click(screen.getByRole("button", { name: "Continue" }));

    await waitFor(() => expect(screen.getByText("Profile")).toBeTruthy());
  });

  it("shows an error and returns to login when the final sign-in fails", async () => {
    const user = userEvent.setup();
    vi.mocked(mfaService.startEnrolmentWithToken).mockResolvedValue(sampleStart);
    vi.mocked(mfaService.confirmEnrolmentWithToken).mockResolvedValue({
      recoveryCodes: ["REC-1111"],
      login: {
        token: "enrol-access-tok",
        refreshToken: "enrol-refresh-tok",
        role: "QcManager",
        permissions: [],
        mustChangePassword: false
      }
    });
    vi.mocked(authenticationService.me).mockRejectedValue(new Error("network failure"));

    renderEnrol({ mfaToken: "enrol-tok-err" });

    await user.type(await screen.findByLabelText("6-digit code"), "654321");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => expect(screen.getByText("REC-1111")).toBeTruthy());
    await user.click(screen.getByRole("checkbox", { name: "I have saved these codes" }));
    await user.click(screen.getByRole("button", { name: "Continue" }));

    await waitFor(() => expect(screen.getByText("Login Page")).toBeTruthy());
    expect(toastError).toHaveBeenCalled();
  });
});
