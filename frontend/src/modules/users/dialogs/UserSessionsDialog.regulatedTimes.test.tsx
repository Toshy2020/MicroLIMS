import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { ThemeProvider } from "@mui/material/styles";
import { lightTheme } from "../../../theme";
import { UserSessionsDialog } from "./UserSessionsDialog";
import { UserService, UserRecord } from "../services/UserService";

// Network boundary only.
vi.mock("../services/UserService", () => ({
  UserService: {
    getSessions: vi.fn(),
    revokeSessions: vi.fn(),
    getTrustedDevices: vi.fn(),
    revokeTrustedDevice: vi.fn()
  }
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("user sessions show time zones", () => {
  it("shows the zone beside the sign-in and expiry times", async () => {
    vi.mocked(UserService.getSessions).mockResolvedValue([
      {
        id: 1,
        createdAt: "2026-11-17T09:41:07Z",
        createdIp: "10.0.0.5",
        userAgent: "TestBrowser/1.0",
        lastUsedAt: null,
        expiresAt: "2026-11-24T09:41:07Z"
      }
    ]);
    vi.mocked(UserService.getTrustedDevices).mockResolvedValue([]);
    render(
      <ThemeProvider theme={lightTheme}>
        <UserSessionsDialog open onClose={vi.fn()} user={{ id: 7, username: "janalyst" } as UserRecord} onRevoked={vi.fn()} />
      </ThemeProvider>
    );
    await screen.findByText("10.0.0.5");
    const zoned = /^\d{2} Nov 2026, \d{2}:\d{2} [A-Za-z][A-Za-z0-9+:-]*$/;
    expect(screen.getAllByText(zoned)).toHaveLength(2);
  });
});
