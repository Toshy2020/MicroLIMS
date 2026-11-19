import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { UserRecord, UserService } from "../services/UserService";
import { ResetMfaDialog } from "./ResetMfaDialog";

vi.mock("../services/UserService", () => ({
  UserService: {
    resetMfa: vi.fn()
  }
}));

const toastSuccess = vi.fn();
const toastError = vi.fn();
vi.mock("sonner", () => ({
  toast: {
    success: (...args: unknown[]) => toastSuccess(...args),
    error: (...args: unknown[]) => toastError(...args)
  }
}));

const targetUser: UserRecord = {
  id: 2,
  fullName: "Jane Analyst",
  username: "janalyst",
  email: "jane@example.com",
  roleId: 4,
  role: { id: 4, name: "Analyst", type: "Analyst" },
  isActive: true,
  isLocked: false,
  lockedUntil: null,
  mustChangePassword: false,
  createdAt: "2026-01-01T00:00:00Z",
  lastLoginAt: null,
  passwordChangedAt: null,
  mfaState: "Enrolled"
};

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("ResetMfaDialog", () => {
  it("Reset second factor requires a reason; Cancel is focused first; calls the service with the reason", async () => {
    const user = userEvent.setup();
    vi.mocked(UserService.resetMfa).mockResolvedValue();
    const onReset = vi.fn();
    const onClose = vi.fn();

    render(
      <PinnedLightTheme>
        <ResetMfaDialog open user={targetUser} onClose={onClose} onReset={onReset} />
      </PinnedLightTheme>
    );

    const cancelButton = await screen.findByRole("button", { name: "Cancel" });
    await waitFor(() => expect(document.activeElement).toBe(cancelButton));

    const confirmButton = screen.getByRole("button", { name: "Reset second factor" }) as HTMLButtonElement;
    expect(confirmButton.disabled).toBe(true);

    const reasonField = screen.getByLabelText(/Reason/);
    await user.type(reasonField, "   ");
    expect(confirmButton.disabled).toBe(true);
    expect(UserService.resetMfa).not.toHaveBeenCalled();

    await user.clear(reasonField);
    await user.type(reasonField, "Lost authenticator phone");
    expect(confirmButton.disabled).toBe(false);
    await user.click(confirmButton);

    await waitFor(() => expect(UserService.resetMfa).toHaveBeenCalledWith(2, "Lost authenticator phone"));
    expect(toastSuccess).toHaveBeenCalled();
    expect(onReset).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });

  it("server refusal is shown as a toast and the dialog stays open", async () => {
    const user = userEvent.setup();
    vi.mocked(UserService.resetMfa).mockRejectedValue({
      response: { data: { message: "Cannot reset MFA for your own account." } }
    });
    const onReset = vi.fn();
    const onClose = vi.fn();

    render(
      <PinnedLightTheme>
        <ResetMfaDialog open user={targetUser} onClose={onClose} onReset={onReset} />
      </PinnedLightTheme>
    );

    await user.type(await screen.findByLabelText(/Reason/), "Locked out of device");
    await user.click(screen.getByRole("button", { name: "Reset second factor" }));

    await waitFor(() => expect(toastError).toHaveBeenCalledWith("Cannot reset MFA for your own account."));
    expect(screen.getByRole("alertdialog")).toBeTruthy();
    expect(onClose).not.toHaveBeenCalled();
    expect(onReset).not.toHaveBeenCalled();
  });
});
