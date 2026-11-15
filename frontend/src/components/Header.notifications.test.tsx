import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { BenchModeProvider } from "../theme/BenchModeContext";
import { ThemeModeProvider } from "../theme/ThemeModeContext";
import { AuthProvider } from "../contexts/AuthContext";
import { apiClient } from "../services/apiClient";
import { Header } from "./Header";

vi.mock("../services/apiClient", () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn()
  }
}));

function LocationEcho() {
  const loc = useLocation();
  return <div data-testid="location-echo">{`${loc.pathname}${loc.search}`}</div>;
}

function makeBellNotifications(count: number) {
  return Array.from({ length: count }, (_, i) => ({
    id: i + 1,
    type: "ReviewWaiting",
    message: `Notification #${i + 1}`,
    timestamp: new Date(Date.UTC(2026, 10, 15, 12, 0 - i, 0)).toISOString(),
    severity: "info",
    isRead: false,
    sampleId: 100 + i,
    testOrderId: null
  }));
}

function renderHeader() {
  localStorage.setItem("microlims_token", "h.e30=.s");
  localStorage.setItem("microlims_username", "s.lab");
  localStorage.setItem("microlims_full_name", "Sam Lab");

  return render(
    <PinnedLightTheme>
      <BenchModeProvider>
        <ThemeModeProvider>
          <AuthProvider>
            <MemoryRouter initialEntries={["/my-work"]}>
              <Header labCodes={[]} />
              <Routes>
                <Route path="*" element={<LocationEcho />} />
              </Routes>
            </MemoryRouter>
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

describe("Header notification bell", () => {
  it("shows at most 10 newest notifications and links to /notifications via See all", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

    const fifteenItems = makeBellNotifications(15);
    vi.mocked(apiClient.get).mockImplementation(async (url: string) => {
      if (url === "/messages/unread-count") {
        return { data: { data: { unreadCount: 0 } } };
      }
      if (url === "/dashboard/notifications") {
        return { data: { data: fifteenItems } };
      }
      return { data: { data: null } };
    });

    renderHeader();

    // Header delays its initial /dashboard/notifications load by 5 seconds.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(5_000);
    });

    const bellButton = await screen.findByRole("button", { name: /Notifications, 15 unread/i });
    await user.click(bellButton);

    const menu = await screen.findByRole("menu");
    const items = within(menu).getAllByRole("menuitem").filter((el) => el.textContent !== "See all");
    expect(items).toHaveLength(10);
    expect(within(menu).getByText("Notification #1")).toBeTruthy();
    expect(within(menu).getByText("Notification #10")).toBeTruthy();
    expect(within(menu).queryByText("Notification #11")).toBeNull();

    const seeAllLink = within(menu).getByRole("menuitem", { name: "See all" });
    expect(seeAllLink.getAttribute("href")).toBe("/notifications");

    // Reachable from the keyboard like the other entries: Up from the first entry wraps to it.
    within(menu).getAllByRole("menuitem")[0].focus();
    await user.keyboard("{ArrowUp}");
    expect(document.activeElement).toBe(seeAllLink);

    await user.click(seeAllLink);

    await waitFor(() => {
      expect(screen.getByTestId("location-echo").textContent).toBe("/notifications");
    });
  });

  it("marks a notification read via /dashboard/notifications/{id}/read and navigates to its target", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

    vi.mocked(apiClient.get).mockImplementation(async (url: string) => {
      if (url === "/messages/unread-count") {
        return { data: { data: { unreadCount: 0 } } };
      }
      if (url === "/dashboard/notifications") {
        return {
          data: {
            data: [
              {
                id: 55,
                type: "MediaExpiry",
                message: "Media lot M-55 expiring",
                timestamp: "2026-11-15T10:00:00Z",
                severity: "warning",
                isRead: false,
                sampleId: null,
                testOrderId: null
              }
            ]
          }
        };
      }
      return { data: { data: null } };
    });
    vi.mocked(apiClient.post).mockResolvedValue({ data: { data: {} } });

    renderHeader();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(5_000);
    });

    await user.click(await screen.findByRole("button", { name: /Notifications, 1 unread/i }));
    const menu = await screen.findByRole("menu");
    await user.click(within(menu).getByRole("menuitem", { name: /Media lot M-55 expiring/i }));

    await waitFor(() => {
      expect(apiClient.post).toHaveBeenCalledWith("/dashboard/notifications/55/read");
      expect(screen.getByTestId("location-echo").textContent).toBe("/laboratory-configuration/media");
    });
  });
});
