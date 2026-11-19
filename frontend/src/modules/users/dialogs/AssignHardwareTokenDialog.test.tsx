import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { UserRecord, UserService } from "../services/UserService";
import { AssignHardwareTokenDialog } from "./AssignHardwareTokenDialog";

vi.mock("../services/UserService", () => ({
  UserService: {
    assignHardwareToken: vi.fn()
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
  mfaState: "DueByDate"
};

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("AssignHardwareTokenDialog", () => {
  it("Assign hardware token validates serial, seed and step, and never shows the seed after submit", async () => {
    const user = userEvent.setup();
    vi.mocked(UserService.assignHardwareToken).mockResolvedValue();
    const onAssigned = vi.fn();
    const onClose = vi.fn();
    const secretHexSeed = "3132333435363738393031323334353637383930";

    const { container } = render(
      <PinnedLightTheme>
        <AssignHardwareTokenDialog open user={targetUser} onClose={onClose} onAssigned={onAssigned} />
      </PinnedLightTheme>
    );

    const submitButton = await screen.findByRole("button", { name: "Assign hardware token" });
    await user.click(submitButton);
    expect(await screen.findByText(/Enter the token serial number/i)).toBeTruthy();
    expect(UserService.assignHardwareToken).not.toHaveBeenCalled();

    await user.type(screen.getByLabelText(/Serial number/i), "HW-9001");
    await user.type(screen.getByLabelText(/^Seed/i), "INVALID0189!");
    await user.type(screen.getByLabelText(/^Reason/i), "Issued cleanroom token");
    await user.click(submitButton);
    expect(await screen.findByText(/Base32 seeds may contain/i)).toBeTruthy();
    expect(UserService.assignHardwareToken).not.toHaveBeenCalled();

    // Switch to Hex and supply odd length first to validate hex check.
    await user.click(screen.getByRole("radio", { name: "Hex" }));
    const seedInput = screen.getByLabelText(/^Seed/i) as HTMLInputElement;
    expect(seedInput.type).toBe("password");
    expect(seedInput.getAttribute("autocomplete")).toBe("off");
    await user.clear(seedInput);
    await user.type(seedInput, "abc");
    await user.click(submitButton);
    expect(await screen.findByText(/even number of hexadecimal/i)).toBeTruthy();
    expect(UserService.assignHardwareToken).not.toHaveBeenCalled();

    // Select 60-second step and submit a valid hex seed.
    await user.click(screen.getByRole("combobox", { name: /Time step/i }));
    await user.click(await screen.findByRole("option", { name: "60 seconds" }));
    await user.clear(seedInput);
    await user.type(seedInput, secretHexSeed);
    await user.click(submitButton);

    await waitFor(() =>
      expect(UserService.assignHardwareToken).toHaveBeenCalledWith(2, {
        serialNumber: "HW-9001",
        seed: secretHexSeed,
        seedFormat: "hex",
        stepSeconds: 60,
        reason: "Issued cleanroom token"
      })
    );
    expect(seedInput.value).toBe("");
    expect(container.innerHTML).not.toContain(secretHexSeed);
    expect(document.body.textContent).not.toContain(secretHexSeed);
    expect(onAssigned).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });

  it("the seed is cleared after a failed submit and never appears in the document", async () => {
    const user = userEvent.setup();
    const secretBase32Seed = "JBSWY3DPEHPK3PXP";
    vi.mocked(UserService.assignHardwareToken).mockRejectedValue({
      response: { data: { message: "Two-factor authentication is already active for this user." } }
    });
    const onAssigned = vi.fn();
    const onClose = vi.fn();

    const { container } = render(
      <PinnedLightTheme>
        <AssignHardwareTokenDialog open user={targetUser} onClose={onClose} onAssigned={onAssigned} />
      </PinnedLightTheme>
    );

    await user.type(await screen.findByLabelText(/Serial number/i), "HW-9002");
    const seedInput = screen.getByLabelText(/^Seed/i) as HTMLInputElement;
    await user.type(seedInput, secretBase32Seed);
    await user.type(screen.getByLabelText(/^Reason/i), "Replacement token");
    await user.click(screen.getByRole("button", { name: "Assign hardware token" }));

    await waitFor(() =>
      expect(toastError).toHaveBeenCalledWith("Two-factor authentication is already active for this user.")
    );
    expect(screen.getByRole("dialog")).toBeTruthy();
    expect(seedInput.value).toBe("");
    expect(container.innerHTML).not.toContain(secretBase32Seed);
    expect(document.body.textContent).not.toContain(secretBase32Seed);
    expect(onClose).not.toHaveBeenCalled();
    expect(onAssigned).not.toHaveBeenCalled();
  });
});
