import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../contexts/AuthContext";
import { PERMISSIONS } from "../routes/routes";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { mfaService, type MfaStatus } from "../modules/authentication/services/mfaService";
import { notificationService } from "../modules/notifications/services/notificationService";
import { laboratorySectionService } from "../services/laboratorySectionService";
import { ProfilePage } from "./Profile";

vi.mock("sonner", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn()
  }
}));

vi.mock("../modules/authentication/services/authenticationService", () => ({
  authenticationService: {
    me: vi.fn()
  }
}));

vi.mock("../modules/authentication/services/mfaService", () => ({
  mfaService: {
    getStatus: vi.fn(),
    startEnrolment: vi.fn(),
    confirmEnrolment: vi.fn(),
    regenerateRecoveryCodes: vi.fn(),
    removeFactor: vi.fn(),
    forgetTrustedDevice: vi.fn()
  }
}));

vi.mock("../modules/notifications/services/notificationService", () => ({
  notificationService: {
    getPreferences: vi.fn().mockResolvedValue([]),
    putPreferences: vi.fn().mockResolvedValue([]),
    getMandatory: vi.fn().mockResolvedValue([]),
    putMandatory: vi.fn().mockResolvedValue([])
  }
}));

vi.mock("../services/laboratorySectionService", () => ({
  laboratorySectionService: {
    getMySections: vi.fn().mockResolvedValue([])
  }
}));

const sampleUser = {
  userId: 7,
  username: "a.hassan",
  fullName: "Amina Hassan",
  role: "Analyst" as const,
  roleName: "Senior Microbiologist",
  lastLoginAt: "2026-11-12T08:00:00Z",
  passwordChangedAt: "2026-11-01T08:00:00Z",
  mustChangePassword: false
};

const enrolledStatus: MfaStatus = {
  enrolled: true,
  type: "AuthenticatorApp",
  confirmedAt: "2026-11-10T09:30:00Z",
  recoveryCodesRemaining: 8,
  required: false,
  enrolmentDueAt: null,
  trustedDevices: [
    {
      id: 41,
      createdAt: "2026-11-12T07:00:00Z",
      expiresAt: "2026-11-12T19:00:00Z",
      createdIp: "10.20.30.40",
      userAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/130.0.0.0"
    }
  ]
};

function renderPage() {
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <ProfilePage />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.clearAllMocks();
});

describe("ProfilePage Security section", () => {
  it("loading, empty (no trusted computers) and error states render", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.me).mockResolvedValue(sampleUser);
    let resolvePendingStatus: (value: MfaStatus) => void;
    const pendingPromise = new Promise<MfaStatus>((resolve) => {
      resolvePendingStatus = resolve;
    });
    vi.mocked(mfaService.getStatus)
      .mockReturnValueOnce(pendingPromise)
      .mockRejectedValueOnce(new Error("boom"))
      .mockResolvedValueOnce({ ...enrolledStatus, trustedDevices: [] });

    renderPage();
    expect(await screen.findByText("Security")).toBeTruthy();

    resolvePendingStatus!({ ...enrolledStatus, trustedDevices: [] });
    await waitFor(() => expect(screen.getByText("No trusted computers")).toBeTruthy());

    cleanup();
    renderPage();
    expect(await screen.findByRole("button", { name: "Retry" })).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Retry" }));
    await waitFor(() => expect(screen.getByText("No trusted computers")).toBeTruthy());
  });

  it("Profile shows status, remaining recovery codes and trusted computers", async () => {
    vi.mocked(authenticationService.me).mockResolvedValue(sampleUser);
    vi.mocked(mfaService.getStatus).mockResolvedValue(enrolledStatus);

    renderPage();

    expect(await screen.findByText("Authenticator app")).toBeTruthy();
    expect(screen.getByText("8 remaining")).toBeTruthy();
    expect(screen.getByText(/10\.20\.30\.40/)).toBeTruthy();
    expect(screen.getByText(/Chrome/)).toBeTruthy();
    expect(screen.getByRole("button", { name: /Forget/i })).toBeTruthy();
  });

  it('Remove is disabled with the reason "Your role requires a second factor" when required', async () => {
    vi.mocked(authenticationService.me).mockResolvedValue(sampleUser);
    vi.mocked(mfaService.getStatus).mockResolvedValue({
      ...enrolledStatus,
      required: true
    });

    renderPage();

    const removeBtn = (await screen.findByRole("button", { name: "Remove" })) as HTMLButtonElement;
    expect(removeBtn.disabled).toBe(true);
    expect(screen.getByText("Your role requires a second factor")).toBeTruthy();
  });

  it("allows removing second factor when not required and keeps Cancel focused first", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.me).mockResolvedValue(sampleUser);
    vi.mocked(mfaService.getStatus)
      .mockResolvedValueOnce(enrolledStatus)
      .mockResolvedValueOnce({
        enrolled: false,
        type: null,
        confirmedAt: null,
        recoveryCodesRemaining: 0,
        required: false,
        enrolmentDueAt: null,
        trustedDevices: []
      });
    vi.mocked(mfaService.removeFactor).mockResolvedValue(undefined);

    renderPage();

    await user.click(await screen.findByRole("button", { name: "Remove" }));
    const cancelBtn = await screen.findByRole("button", { name: "Cancel" });
    expect(document.activeElement).toBe(cancelBtn);

    await user.type(screen.getByLabelText("Password"), "MyPass#1");
    await user.type(screen.getByLabelText("6-digit code"), "123456");
    await user.click(screen.getByRole("button", { name: "Remove second factor" }));

    await waitFor(() => expect(mfaService.removeFactor).toHaveBeenCalledWith("MyPass#1", "123456"));
    await waitFor(() => expect(screen.getByRole("button", { name: "Set up" })).toBeTruthy());
  });

  it("Regenerate asks for password and code, then shows the new codes once", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.me).mockResolvedValue(sampleUser);
    vi.mocked(mfaService.getStatus).mockResolvedValue(enrolledStatus);
    vi.mocked(mfaService.regenerateRecoveryCodes)
      .mockRejectedValueOnce({ response: { status: 400, data: { message: "The code is not correct." } } })
      .mockResolvedValueOnce({ recoveryCodes: ["NEW-1111", "NEW-2222"] });

    renderPage();

    await user.click(await screen.findByRole("button", { name: "Regenerate recovery codes" }));
    await user.type(screen.getByLabelText("Password"), "MyPass#1");
    await user.type(screen.getByLabelText("6-digit code"), "000000");
    await user.click(screen.getByRole("button", { name: "Regenerate" }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("The code is not correct.");
    expect(screen.getByLabelText("Password")).toBeTruthy();

    await user.clear(screen.getByLabelText("6-digit code"));
    await user.type(screen.getByLabelText("6-digit code"), "654321");
    await user.click(screen.getByRole("button", { name: "Regenerate" }));

    await waitFor(() => expect(screen.getByText("NEW-1111")).toBeTruthy());
    expect(screen.getByText("NEW-2222")).toBeTruthy();
    await user.click(screen.getByRole("checkbox", { name: "I have saved these codes" }));
    await user.click(screen.getByRole("button", { name: "Continue" }));

    await waitFor(() => expect(mfaService.getStatus).toHaveBeenCalledTimes(2));
  });

  it("Forget removes a trusted computer after confirmation", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.me).mockResolvedValue(sampleUser);
    vi.mocked(mfaService.getStatus)
      .mockResolvedValueOnce(enrolledStatus)
      .mockResolvedValueOnce({ ...enrolledStatus, trustedDevices: [] });
    vi.mocked(mfaService.forgetTrustedDevice).mockResolvedValue(undefined);

    renderPage();

    const forgetBtn = await screen.findByRole("button", { name: /Forget Chrome on Windows \(10\.20\.30\.40\)/i });
    await user.click(forgetBtn);

    const confirmBtn = await screen.findByRole("button", { name: "Forget" });
    await user.click(confirmBtn);

    await waitFor(() => expect(mfaService.forgetTrustedDevice).toHaveBeenCalledWith(41));
    await waitFor(() => expect(screen.getByText("No trusted computers")).toBeTruthy());
  });

  it("sets up second factor from Profile dialog and reloads status", async () => {
    const user = userEvent.setup();
    vi.mocked(authenticationService.me).mockResolvedValue(sampleUser);
    vi.mocked(mfaService.getStatus)
      .mockResolvedValueOnce({
        enrolled: false,
        type: null,
        confirmedAt: null,
        recoveryCodesRemaining: 0,
        required: false,
        enrolmentDueAt: null,
        trustedDevices: []
      })
      .mockResolvedValueOnce({ ...enrolledStatus, trustedDevices: [] });
    vi.mocked(mfaService.startEnrolment).mockResolvedValue({
      manualKey: "JBSWY3DPEHPK3PXP",
      otpAuthUri: "otpauth://totp/MicroLIMS:sam?secret=JBSWY3DPEHPK3PXP",
      qrSvg: '<svg xmlns="http://www.w3.org/2000/svg"><rect/></svg>'
    });
    vi.mocked(mfaService.confirmEnrolment).mockResolvedValue({
      recoveryCodes: ["SET-1111"]
    });

    renderPage();

    await user.click(await screen.findByRole("button", { name: "Set up" }));
    await user.type(screen.getByLabelText("Password"), "Pass#123");
    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(await screen.findByAltText("QR code to add MicroLIMS to your authenticator app")).toBeTruthy();
    await user.type(screen.getByLabelText("6-digit code"), "123456");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => expect(screen.getByText("SET-1111")).toBeTruthy());
    await user.click(screen.getByRole("checkbox", { name: "I have saved these codes" }));
    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(await screen.findByText("Authenticator app")).toBeTruthy();
    expect(mfaService.getStatus).toHaveBeenCalledTimes(2);
  });

  it("shows the server's refusal as visible text when an assigned hardware token blocks self-enrolment", async () => {
    const user = userEvent.setup();
    const refusal =
      "A hardware token has been assigned to you; activate it, or ask an administrator to reset your two-factor setup, before setting up an authenticator app.";
    vi.mocked(authenticationService.me).mockResolvedValue(sampleUser);
    vi.mocked(mfaService.getStatus).mockResolvedValue({
      enrolled: false,
      type: null,
      confirmedAt: null,
      recoveryCodesRemaining: 0,
      required: false,
      enrolmentDueAt: null,
      trustedDevices: []
    });
    vi.mocked(mfaService.startEnrolment).mockRejectedValue({
      response: { status: 400, data: { message: refusal } }
    });

    renderPage();

    await user.click(await screen.findByRole("button", { name: "Set up" }));
    await user.type(screen.getByLabelText("Password"), "Pass#123");
    await user.click(screen.getByRole("button", { name: "Continue" }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain(refusal);
    expect(screen.queryByAltText("QR code to add MicroLIMS to your authenticator app")).toBeNull();
  });
});

describe("ProfilePage Notifications section", () => {
  function renderProfileWithAuth(permissions: string[]) {
    localStorage.clear();
    localStorage.setItem("microlims_permissions", JSON.stringify(permissions));
    return render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter>
            <ProfilePage />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );
  }

  it("renders notification preferences and gates mandatory panel on Notifications.Configure", async () => {
    vi.mocked(authenticationService.me).mockResolvedValue(sampleUser);
    vi.mocked(mfaService.getStatus).mockResolvedValue(enrolledStatus);
    vi.mocked(notificationService.getPreferences).mockResolvedValue([
      {
        type: "sample.approved",
        title: "Sample approved",
        emailAllowed: true,
        emailMode: "Immediate",
        mandatory: false,
        mandatoryReason: null
      }
    ]);

    renderProfileWithAuth([]);
    expect(await screen.findByRole("heading", { name: "Notifications" })).toBeTruthy();
    expect(await screen.findByText("Sample approved")).toBeTruthy();
    expect(screen.queryByRole("heading", { name: "Section mandatory notifications" })).toBeNull();

    cleanup();
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([
      {
        sectionId: 11,
        sectionCode: "MICRO",
        sectionName: "Microbiology",
        departmentId: 1,
        departmentCode: "QC",
        departmentName: "Quality Control"
      }
    ]);
    vi.mocked(notificationService.getMandatory).mockResolvedValue([]);

    renderProfileWithAuth([PERMISSIONS.NOTIFICATIONS_CONFIGURE]);
    expect(await screen.findByRole("heading", { name: "Section mandatory notifications" })).toBeTruthy();
  });
});
