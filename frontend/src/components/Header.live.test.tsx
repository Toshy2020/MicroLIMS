import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { BenchModeProvider } from "../theme/BenchModeContext";
import { ThemeModeProvider } from "../theme/ThemeModeContext";
import { AuthProvider } from "../contexts/AuthContext";
import { LiveProvider } from "../contexts/LiveContext";
import { createFakeLiveConnection } from "../contexts/testing/fakeLiveConnection";
import { apiClient } from "../services/apiClient";
import { Header } from "./Header";

vi.mock("../services/apiClient", () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn()
  }
}));

function notification(id: number) {
  return {
    id,
    type: "ReviewWaiting",
    message: `Notification #${id}`,
    timestamp: new Date(Date.UTC(2026, 10, 15, 12, 0, 0)).toISOString(),
    severity: "info",
    isRead: false,
    sampleId: 100 + id,
    testOrderId: null
  };
}

function renderHeader(connection: ReturnType<typeof createFakeLiveConnection>) {
  localStorage.setItem("microlims_token", "h.e30=.s");
  localStorage.setItem("microlims_username", "s.lab");
  localStorage.setItem("microlims_full_name", "Sam Lab");
  render(
    <PinnedLightTheme>
      <BenchModeProvider>
        <ThemeModeProvider>
          <AuthProvider>
            <LiveProvider connection={connection}>
              <MemoryRouter initialEntries={["/my-work"]}>
                <Header labCodes={[]} />
              </MemoryRouter>
            </LiveProvider>
          </AuthProvider>
        </ThemeModeProvider>
      </BenchModeProvider>
    </PinnedLightTheme>
  );
}

afterEach(() => {
  vi.useRealTimers();
  cleanup();
  localStorage.clear();
  vi.clearAllMocks();
});

describe("Header live updates", () => {
  it("bell badge updates on a notifications signal without waiting 60 s", async () => {
    vi.useFakeTimers();
    let items = [notification(1)];
    vi.mocked(apiClient.get).mockImplementation(async (url: string) => {
      if (url === "/messages/unread-count") return { data: { data: { unreadCount: 0 } } };
      if (url === "/dashboard/notifications") return { data: { data: items } };
      return { data: { data: null } };
    });
    const connection = createFakeLiveConnection();
    renderHeader(connection);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(5_000);
    });
    expect(screen.getByRole("button", { name: /Notifications, 1 unread/i })).toBeTruthy();

    items = [notification(1), notification(2)];
    await act(async () => {
      connection.emitSignal({ kind: "notifications", key: "user:1" });
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(screen.getByRole("button", { name: /Notifications, 2 unread/i })).toBeTruthy();
    expect(screen.queryByRole("status", { name: "Live updates paused" })).toBeNull();
  });

  it("shows a 'Live updates paused' indicator with an accessible name while offline", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: [] } });
    const connection = createFakeLiveConnection({ startFails: true });
    renderHeader(connection);

    const indicator = await screen.findByRole("status", { name: "Live updates paused" });
    expect(indicator.textContent).toContain("Live updates paused");
  });
});
