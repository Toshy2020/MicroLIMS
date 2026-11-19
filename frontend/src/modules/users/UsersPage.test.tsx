import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { lightTheme } from "../../theme";
import { UsersPage } from "./UsersPage";
import { UserService, UserRecord } from "./services/UserService";
import { RoleService } from "../roles/services/RoleService";

vi.mock("./services/UserService", () => ({
  UserService: {
    getAll: vi.fn(), create: vi.fn(), updateProfile: vi.fn(), changeRole: vi.fn(), setStatus: vi.fn(),
    unlock: vi.fn(), initiatePasswordReset: vi.fn(), adminPasswordRecovery: vi.fn(),
    forcePasswordChange: vi.fn(), resetMfa: vi.fn(), assignHardwareToken: vi.fn(),
    getSessions: vi.fn(), revokeSessions: vi.fn(), getTrustedDevices: vi.fn(), revokeTrustedDevice: vi.fn(),
    relinkDirectory: vi.fn()
  }
}));
vi.mock("../roles/services/RoleService", () => ({ RoleService: { getAll: vi.fn() } }));
vi.mock("../../contexts/AuthContext", () => ({
  useAuth: () => ({ userId: 1, fullName: "Admin One", username: "admin", role: "SystemAdministrator" })
}));
vi.mock("./dialogs/UserSectionsDialog", () => ({ UserSectionsDialog: () => null }));
const toastError = vi.fn();
vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: (...a: unknown[]) => toastError(...a) } }));

const target: UserRecord = {
  id: 2, fullName: "Jane Analyst", username: "janalyst", email: "jane@example.com", roleId: 4,
  role: { id: 4, name: "Analyst", type: "Analyst" }, isActive: true, isLocked: true, lockedUntil: null,
  mustChangePassword: false, createdAt: "2026-01-01T00:00:00Z", lastLoginAt: null, passwordChangedAt: null
};

beforeEach(() => {
  vi.mocked(UserService.getAll).mockResolvedValue([target]);
  vi.mocked(RoleService.getAll).mockResolvedValue([
    { id: 3, name: "Reviewer", description: null, type: "Reviewer", isSystemRole: true, isActive: true },
    { id: 4, name: "Analyst", description: null, type: "Analyst", isSystemRole: true, isActive: true },
    { id: 1, name: "System Administrator", description: null, type: "SystemAdministrator", isSystemRole: true, isActive: true }
  ]);
  vi.mocked(UserService.changeRole).mockResolvedValue({});
  vi.mocked(UserService.unlock).mockResolvedValue({});
  vi.mocked(UserService.setStatus).mockResolvedValue({});
  vi.mocked(UserService.forcePasswordChange).mockResolvedValue({});
  vi.mocked(UserService.updateProfile).mockResolvedValue({});
  vi.mocked(UserService.resetMfa).mockResolvedValue();
  vi.mocked(UserService.assignHardwareToken).mockResolvedValue();
});
afterEach(() => { cleanup(); vi.clearAllMocks(); });

function renderPage() {
  return render(<ThemeProvider theme={lightTheme}><UsersPage /></ThemeProvider>);
}

// The signature dialog is the topmost one; the create form also has a Password field behind it.
function sign(password: string) {
  const dialogs = screen.getAllByRole("dialog");
  const top = dialogs[dialogs.length - 1];
  fireEvent.change(within(top).getByLabelText(/^Password/), { target: { value: password } });
  fireEvent.click(within(top).getByRole("button", { name: "Sign" }));
}

async function openAction(label: string) {
  fireEvent.click(await screen.findByRole("button", { name: label }));
  return await screen.findByRole("dialog");
}

describe("UsersPage reasons and signatures", () => {
  it("will not create a user without a reason", { timeout: 20000 }, async () => {
    renderPage();
    await screen.findByText("Jane Analyst");
    fireEvent.change(screen.getByLabelText(/^Full Name/), { target: { value: "New Person" } });
    fireEvent.change(screen.getByLabelText(/^Username/), { target: { value: "newperson" } });
    fireEvent.change(screen.getByLabelText(/^Password/), { target: { value: "Passw0rd!" } });
    fireEvent.mouseDown(screen.getAllByRole("combobox")[0]);
    fireEvent.click(await screen.findByRole("option", { name: "Reviewer" }));

    fireEvent.submit(screen.getByRole("button", { name: "Create User" }).closest("form")!);

    expect(UserService.create).not.toHaveBeenCalled();
    expect(toastError).toHaveBeenCalled();
  });

  it("requires the reason to unlock", async () => {
    renderPage();
    const dialog = await openAction("Unlock Account");
    const unlock = within(dialog).getByRole("button", { name: "Unlock Account" }) as HTMLButtonElement;
    expect(unlock.disabled).toBe(true);

    fireEvent.change(within(dialog).getByLabelText(/Reason for Unlock/), { target: { value: "Identity verified" } });
    fireEvent.click(unlock);

    await waitFor(() => expect(UserService.unlock).toHaveBeenCalledWith(2, "Identity verified"));
  });

  it("requires the reason for a profile edit and sends it", async () => {
    renderPage();
    const dialog = await openAction("Edit Profile");
    const save = within(dialog).getByRole("button", { name: "Save Profile" }) as HTMLButtonElement;
    expect(save.disabled).toBe(true);

    fireEvent.change(within(dialog).getByLabelText(/Reason for Change/), { target: { value: "Name corrected" } });
    fireEvent.click(save);

    await waitFor(() => expect(UserService.updateProfile).toHaveBeenCalledWith(
      2, "Jane Analyst", "janalyst", "jane@example.com", null, "Name corrected", undefined));
  });

  it("requires the reason to force a password change", async () => {
    renderPage();
    const dialog = await openAction("Force Password Change at Next Login");
    const force = within(dialog).getByRole("button", { name: "Force Password Change" }) as HTMLButtonElement;
    expect(force.disabled).toBe(true);

    fireEvent.change(within(dialog).getByLabelText(/^Reason/), { target: { value: "Policy" } });
    fireEvent.click(force);

    await waitFor(() => expect(UserService.forcePasswordChange).toHaveBeenCalledWith(2, "Policy"));
  });

  it("changes a role only after the reason and the signature password", async () => {
    renderPage();
    const dialog = await openAction("Change Role");
    const change = within(dialog).getByRole("button", { name: "Change Role" }) as HTMLButtonElement;
    expect(change.disabled).toBe(true);

    fireEvent.change(within(dialog).getByLabelText(/Reason for Change/), { target: { value: "Promotion" } });
    fireEvent.click(change);

    // Nothing is sent until the administrator signs.
    expect(UserService.changeRole).not.toHaveBeenCalled();
    const signature = await screen.findByText("Electronic Signature");
    expect(signature).toBeTruthy();
    sign("my-password");

    await waitFor(() => expect(UserService.changeRole).toHaveBeenCalledWith(2, 4, "Promotion", "my-password"));
  });

  it("keeps the signature dialog open with the server's message when the password is refused", async () => {
    vi.mocked(UserService.changeRole).mockRejectedValue({ response: { data: { message: "Password verification failed. The signature was not applied." } } });
    renderPage();
    const dialog = await openAction("Change Role");
    fireEvent.change(within(dialog).getByLabelText(/Reason for Change/), { target: { value: "Promotion" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Change Role" }));
    await screen.findByText("Electronic Signature");
    sign("wrong");

    expect(await screen.findByText("Password verification failed. The signature was not applied.")).toBeTruthy();
    expect(screen.getByText("Electronic Signature")).toBeTruthy();
  });

  it("loads the active list by default and has no delete action", async () => {
    renderPage();
    await screen.findByText("Jane Analyst");
    expect(UserService.getAll).toHaveBeenCalledWith("active");
    expect(screen.queryByRole("button", { name: /Permanently Delete/ })).toBeNull();
  });

  it("shows inactive users only on the Inactive tab, with reactivation as the sole action", async () => {
    const inactive: UserRecord = { ...target, id: 3, fullName: "Old Timer", username: "oldtimer", isActive: false, isLocked: false };
    vi.mocked(UserService.getAll).mockImplementation(async (status) => (status === "inactive" ? [inactive] : [target]));
    renderPage();
    await screen.findByText("Jane Analyst");
    expect(screen.queryByText("Old Timer")).toBeNull();

    fireEvent.click(screen.getByRole("tab", { name: "Inactive" }));
    await screen.findByText("Old Timer");
    expect(screen.queryByText("Jane Analyst")).toBeNull();
    expect(UserService.getAll).toHaveBeenCalledWith("inactive");
    expect(screen.queryByRole("button", { name: "Change Role" })).toBeNull();

    const dialog = await openAction("Reactivate Account");
    fireEvent.change(within(dialog).getByLabelText(/Reason for Enabling/), { target: { value: "Returned from leave" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Reactivate Account" }));
    await waitFor(() => expect(UserService.setStatus).toHaveBeenCalledWith(3, true, "Returned from leave"));
  });

  it("Users table shows the second-factor state as text for each state", async () => {
    vi.mocked(UserService.getAll).mockResolvedValue([
      { ...target, id: 10, username: "u_notreq", fullName: "User NotReq", mfaState: "NotRequired" },
      { ...target, id: 11, username: "u_enrolled", fullName: "User Enrolled", mfaState: "Enrolled" },
      { ...target, id: 12, username: "u_due", fullName: "User Due", mfaState: "DueByDate" },
      { ...target, id: 13, username: "u_overdue", fullName: "User Overdue", mfaState: "Overdue" },
      { ...target, id: 14, username: "u_pending", fullName: "User Pending", mfaState: "TokenPendingConfirmation" },
      { ...target, id: 15, username: "u_unknown", fullName: "User Unknown" }
    ]);

    renderPage();

    expect(await screen.findByText("Second factor")).toBeTruthy();
    expect(screen.getByText("Not required")).toBeTruthy();
    expect(screen.getByText("Set up")).toBeTruthy();
    expect(screen.getByText("Due — not set up yet")).toBeTruthy();
    expect(screen.getByText("Overdue")).toBeTruthy();
    expect(screen.getByText("Token awaiting first use")).toBeTruthy();
    const unknownRow = screen.getByText("User Unknown").closest("tr")!;
    expect(within(unknownRow).getByText("—")).toBeTruthy();
  });

  it("Reset is not offered on the signed-in administrator's own row", async () => {
    const user = userEvent.setup();
    vi.mocked(UserService.getAll).mockResolvedValue([
      { ...target, id: 1, username: "admin", fullName: "Admin One", mfaState: "Enrolled" },
      { ...target, id: 2, username: "janalyst", fullName: "Jane Analyst", mfaState: "Enrolled" }
    ]);
    vi.mocked(UserService.resetMfa).mockResolvedValue();

    renderPage();
    await screen.findByText("Jane Analyst");

    expect(screen.queryByRole("button", { name: /Reset second factor for admin/i })).toBeNull();
    expect(screen.queryByRole("button", { name: /Assign hardware token for admin/i })).toBeNull();

    const otherReset = screen.getByRole("button", { name: "Reset second factor for janalyst" });
    expect(screen.getByRole("button", { name: "Assign hardware token for janalyst" })).toBeTruthy();

    await user.click(otherReset);
    const dialog = await screen.findByRole("alertdialog");
    await user.type(within(dialog).getByLabelText(/Reason/), "Replaced phone");
    await user.click(within(dialog).getByRole("button", { name: "Reset second factor" }));
    await waitFor(() => expect(UserService.resetMfa).toHaveBeenCalledWith(2, "Replaced phone"));
  });

  it("user form: choosing \"Company login\" requires a company username; \"Emergency account\" is disabled with its reason for non-administrators", async () => {
    const user = userEvent.setup();
    vi.mocked(UserService.create).mockResolvedValue({});
    renderPage();
    await screen.findByText("Jane Analyst");

    await user.type(screen.getByLabelText(/^Full Name/), "New Person");
    await user.type(screen.getByLabelText(/^Username/), "newperson");
    await user.type(screen.getByLabelText(/^Reason/), "New starter");
    await user.click(screen.getAllByRole("combobox")[0]);
    await user.click(await screen.findByRole("option", { name: "Analyst" }));

    // Non-administrator: the checkbox is disabled and says why.
    const emergency = screen.getByRole("checkbox", { name: "Emergency account" }) as HTMLInputElement;
    expect(emergency.disabled).toBe(true);
    expect(screen.getByText("Only available when the role is System Administrator.")).toBeTruthy();

    // Company login needs no password but needs a company username.
    await user.click(screen.getByRole("combobox", { name: /Sign-in method/ }));
    await user.click(await screen.findByRole("option", { name: "Company login" }));
    expect(screen.queryByLabelText(/^Password/)).toBeNull();
    expect((screen.getByLabelText(/^Company username/) as HTMLInputElement).required).toBe(true);
    await user.click(screen.getByRole("button", { name: "Create User" }));
    expect(UserService.create).not.toHaveBeenCalled();

    await user.type(screen.getByLabelText(/^Company username/), "new.person");
    await user.click(screen.getByRole("button", { name: "Create User" }));
    await waitFor(() => expect(UserService.create).toHaveBeenCalledWith(
      "New Person", "newperson", "", 4, "New starter", "", "",
      { signInMethod: "Directory", directoryUsername: "new.person", isEmergencyAccount: false }));
  });

  it("user form: Emergency account is enabled when the selected role is System Administrator", async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText("Jane Analyst");
    await user.click(screen.getAllByRole("combobox")[0]);
    await user.click(await screen.findByRole("option", { name: "System Administrator" }));
    const emergency = screen.getByRole("checkbox", { name: "Emergency account" }) as HTMLInputElement;
    expect(emergency.disabled).toBe(false);
    await user.click(emergency);
    expect(emergency.checked).toBe(true);
  });

  it("Users table shows the sign-in method and link state as text", async () => {
    vi.mocked(UserService.getAll).mockResolvedValue([
      { ...target, id: 20, username: "u_local", fullName: "User Local", signInMethod: "Local" },
      { ...target, id: 21, username: "u_nl", fullName: "User NotLinked", signInMethod: "Directory", linkState: "NotLinked" },
      { ...target, id: 22, username: "u_ns", fullName: "User NeverSigned", signInMethod: "Directory", linkState: "LinkedNeverSignedIn" },
      { ...target, id: 23, username: "u_l", fullName: "User Linked", signInMethod: "Directory", linkState: "Linked", isEmergencyAccount: true },
      { ...target, id: 24, username: "u_ref", fullName: "User Refused", signInMethod: "Directory", linkState: "SignInRefused" }
    ]);
    renderPage();
    await screen.findByText("User Linked");
    const row = (name: string) => screen.getByText(name).closest("tr")!;
    expect(within(row("User Local")).getByText("Laboratory login")).toBeTruthy();
    expect(within(row("User NotLinked")).getByText("Not linked")).toBeTruthy();
    expect(within(row("User NeverSigned")).getByText("Linked, never signed in")).toBeTruthy();
    expect(within(row("User Linked")).getByText("Linked")).toBeTruthy();
    expect(within(row("User Linked")).getByText("Emergency account")).toBeTruthy();
    expect(within(row("User Refused")).getByText("Sign-in refused")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Re-link company account for u_local" })).toBeNull();
  });

  it("Re-link requires a reason", async () => {
    const user = userEvent.setup();
    vi.mocked(UserService.relinkDirectory).mockResolvedValue();
    vi.mocked(UserService.getAll).mockResolvedValue([
      { ...target, id: 21, username: "u_nl", fullName: "User Linked", signInMethod: "Directory", linkState: "Linked" }
    ]);
    renderPage();
    await user.click(await screen.findByRole("button", { name: "Re-link company account for u_nl" }));
    const dialog = await screen.findByRole("dialog");

    expect(document.activeElement).toBe(within(dialog).getByRole("button", { name: "Cancel" }));
    const relink = within(dialog).getByRole("button", { name: "Re-link" }) as HTMLButtonElement;
    expect(relink.disabled).toBe(true);
    expect(UserService.relinkDirectory).not.toHaveBeenCalled();

    await user.type(within(dialog).getByLabelText(/^Reason/), "Company username changed");
    await user.click(relink);
    await waitFor(() => expect(UserService.relinkDirectory).toHaveBeenCalledWith(21, "Company username changed"));
  });
});
