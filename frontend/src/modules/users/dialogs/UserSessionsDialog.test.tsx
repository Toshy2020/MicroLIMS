import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ThemeProvider } from "@mui/material/styles";
import { lightTheme } from "../../../theme";
import { UserSessionsDialog } from "./UserSessionsDialog";
import { UserService, UserRecord } from "../services/UserService";

vi.mock("../services/UserService", () => ({
  UserService: { getSessions: vi.fn(), revokeSessions: vi.fn() }
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
    renderDialog();
    expect(await screen.findByText("10.0.0.5")).toBeTruthy();
    expect(screen.getByText("TestBrowser/1.0")).toBeTruthy();
  });

  it("requires a reason before revoking, then revokes and reloads", async () => {
    vi.mocked(UserService.getSessions).mockResolvedValueOnce([session]).mockResolvedValueOnce([]);
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
});
