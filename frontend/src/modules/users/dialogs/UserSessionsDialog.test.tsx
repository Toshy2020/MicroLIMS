import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { lightTheme } from "../../../theme";
import { UserSessionsDialog } from "./UserSessionsDialog";
import { UserService, UserRecord } from "../services/UserService";

vi.mock("../services/UserService", () => ({
  UserService: {
    getSessions: vi.fn(),
    revokeSessions: vi.fn(),
    getTrustedDevices: vi.fn(),
    revokeTrustedDevice: vi.fn()
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

const user = { id: 7, username: "janalyst" } as UserRecord;
const session = {
  id: 1, createdAt: "2026-01-01T00:00:00Z", createdIp: "10.0.0.5", userAgent: "TestBrowser/1.0",
  lastUsedAt: null, expiresAt: "2026-01-08T00:00:00Z"
};

afterEach(() => { cleanup(); vi.clearAllMocks(); });

function renderDialog(onRevoked = vi.fn()) {
  render(<ThemeProvider theme={lightTheme}><UserSessionsDialog open onClose={vi.fn()} user={user} onRevoked={onRevoked} /></ThemeProvider>);
  return onRevoked;
}

describe("UserSessionsDialog", () => {
  it("lists sessions with IP and user agent", async () => {
    vi.mocked(UserService.getSessions).mockResolvedValue([session]);
    vi.mocked(UserService.getTrustedDevices).mockResolvedValue([]);
    renderDialog();
    expect(await screen.findByText("10.0.0.5")).toBeTruthy();
    expect(screen.getByText("TestBrowser/1.0")).toBeTruthy();
  });

  it("requires a reason before revoking, then revokes and reloads", async () => {
    vi.mocked(UserService.getSessions).mockResolvedValueOnce([session]).mockResolvedValueOnce([]);
    vi.mocked(UserService.getTrustedDevices).mockResolvedValue([]);
    vi.mocked(UserService.revokeSessions).mockResolvedValue({ revokedCount: 1 });
    const onRevoked = renderDialog();
    await screen.findByText("10.0.0.5");
    const button = screen.getByRole("button", { name: "Revoke all sessions" }) as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText(/Reason/), { target: { value: "Lost laptop" } });
    expect(button.disabled).toBe(false);
    fireEvent.click(button);
    await waitFor(() => expect(UserService.revokeSessions).toHaveBeenCalledWith(7, "Lost laptop"));
    await waitFor(() => expect(onRevoked).toHaveBeenCalledWith("Revoked 1 session(s) for janalyst."));
    expect(await screen.findByText("No active sessions.")).toBeTruthy();
  });

  it("Sessions dialog shows \"Second factor: Yes/No\" per session and lists trusted computers with Revoke", async () => {
    const userActor = userEvent.setup();
    vi.mocked(UserService.getSessions).mockResolvedValue([
      { ...session, id: 1, createdIp: "10.0.0.5", mfaVerified: true },
      { ...session, id: 2, createdIp: "10.0.0.6", mfaVerified: false }
    ]);
    vi.mocked(UserService.getTrustedDevices)
      .mockResolvedValueOnce([
        {
          id: 42,
          createdAt: "2026-01-01T10:00:00Z",
          expiresAt: "2026-01-02T10:00:00Z",
          createdIp: "192.168.1.25",
          userAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0"
        }
      ])
      .mockResolvedValueOnce([]);
    vi.mocked(UserService.revokeTrustedDevice).mockResolvedValue();

    renderDialog();

    expect(await screen.findByText("10.0.0.5")).toBeTruthy();
    expect(screen.getByText("Second factor: Yes")).toBeTruthy();
    expect(screen.getByText("Second factor: No")).toBeTruthy();
    expect(await screen.findByText("Chrome on Windows")).toBeTruthy();
    expect(screen.getByText(/192\.168\.1\.25/)).toBeTruthy();

    await userActor.click(screen.getByRole("button", { name: /Revoke Chrome on Windows/i }));
    const confirmDialog = await screen.findByRole("alertdialog");
    await userActor.click(within(confirmDialog).getByRole("button", { name: "Revoke" }));

    await waitFor(() => expect(UserService.revokeTrustedDevice).toHaveBeenCalledWith(7, 42));
    expect(await screen.findByText("No trusted computers")).toBeTruthy();
  });

  it("a failed trusted-computers load still shows the sessions", async () => {
    vi.mocked(UserService.getSessions).mockResolvedValue([{ ...session, mfaVerified: true }]);
    vi.mocked(UserService.getTrustedDevices).mockRejectedValue(new Error("trusted devices failed"));

    renderDialog();

    expect(await screen.findByText("10.0.0.5")).toBeTruthy();
    expect(screen.getByText("Second factor: Yes")).toBeTruthy();
    expect(await screen.findByText(/Some data could not be loaded: trusted computers/i)).toBeTruthy();
  });
});
