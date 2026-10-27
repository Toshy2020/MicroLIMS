import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { ThemeProvider } from "@mui/material/styles";
import { lightTheme } from "../../theme";
import { UsersPage } from "./UsersPage";
import { UserService, UserRecord } from "./services/UserService";
import { RoleService } from "../roles/services/RoleService";

vi.mock("./services/UserService", () => ({
  UserService: {
    getAll: vi.fn(), create: vi.fn(), updateProfile: vi.fn(), changeRole: vi.fn(), setStatus: vi.fn(),
    unlock: vi.fn(), initiatePasswordReset: vi.fn(), adminPasswordRecovery: vi.fn(),
    forcePasswordChange: vi.fn(), hardDelete: vi.fn()
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
    { id: 4, name: "Analyst", description: null, type: "Analyst", isSystemRole: true, isActive: true }
  ]);
  vi.mocked(UserService.changeRole).mockResolvedValue({});
  vi.mocked(UserService.hardDelete).mockResolvedValue({});
  vi.mocked(UserService.unlock).mockResolvedValue({});
  vi.mocked(UserService.setStatus).mockResolvedValue({});
  vi.mocked(UserService.forcePasswordChange).mockResolvedValue({});
  vi.mocked(UserService.updateProfile).mockResolvedValue({});
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

  it("sends the reason with an account enable", async () => {
    vi.mocked(UserService.getAll).mockResolvedValue([{ ...target, isActive: false, isLocked: false }]);
    renderPage();
    const dialog = await openAction("Enable Account");
    const enable = within(dialog).getByRole("button", { name: "Enable Account" }) as HTMLButtonElement;
    expect(enable.disabled).toBe(true);

    fireEvent.change(within(dialog).getByLabelText(/Reason for Enabling/), { target: { value: "Returned from leave" } });
    fireEvent.click(enable);

    await waitFor(() => expect(UserService.setStatus).toHaveBeenCalledWith(2, true, "Returned from leave"));
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

  it("deletes a user only with a reason and a signature", async () => {
    renderPage();
    const dialog = await openAction("Permanently Delete User");
    const remove = within(dialog).getByRole("button", { name: "Permanently Delete" }) as HTMLButtonElement;
    expect(remove.disabled).toBe(true);

    fireEvent.change(within(dialog).getByLabelText(/Reason for Deletion/), { target: { value: "Created by mistake" } });
    fireEvent.click(remove);

    expect(UserService.hardDelete).not.toHaveBeenCalled();
    await screen.findByText("Electronic Signature");
    sign("my-password");

    await waitFor(() => expect(UserService.hardDelete).toHaveBeenCalledWith(2, "Created by mistake", "my-password"));
  });
});
