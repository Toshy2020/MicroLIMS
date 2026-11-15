import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { LiveProvider } from "../../contexts/LiveContext";
import { createFakeLiveConnection } from "../../contexts/testing/fakeLiveConnection";
import { MessagesPage } from "./MessagesPage";
import { messageService } from "./services/messageService";

vi.mock("../../services/apiClient", () => ({
  apiClient: { get: vi.fn(), post: vi.fn() }
}));

vi.mock("./services/messageService", () => ({
  messageService: {
    getConversations: vi.fn(),
    getMessages: vi.fn(),
    markAsRead: vi.fn(),
    sendMessage: vi.fn(),
    getUserDirectory: vi.fn()
  }
}));

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  localStorage.clear();
  vi.clearAllMocks();
});

function renderPage(connection: ReturnType<typeof createFakeLiveConnection>) {
  localStorage.setItem("microlims_token", "h.e30=.s");
  render(
    <PinnedLightTheme>
      <AuthProvider>
        <LiveProvider connection={connection}>
          <MemoryRouter>
            <MessagesPage />
          </MemoryRouter>
        </LiveProvider>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

describe("MessagesPage live refresh", () => {
  it("chat list still refreshes every 10 s when the connection is down", async () => {
    vi.useFakeTimers();
    vi.mocked(messageService.getConversations).mockResolvedValue([]);
    renderPage(createFakeLiveConnection({ startFails: true }));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(messageService.getConversations).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(10_000);
    });
    expect(messageService.getConversations).toHaveBeenCalledTimes(2);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(10_000);
    });
    expect(messageService.getConversations).toHaveBeenCalledTimes(3);
  });

  it("refreshes the chat list on a messages signal when connected, without polling", async () => {
    vi.useFakeTimers();
    vi.mocked(messageService.getConversations).mockResolvedValue([]);
    const connection = createFakeLiveConnection();
    renderPage(connection);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(messageService.getConversations).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("button", { name: /New Message/i })).toBeTruthy();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(30_000);
    });
    expect(messageService.getConversations).toHaveBeenCalledTimes(1);

    await act(async () => {
      connection.emitSignal({ kind: "messages", key: "user:1" });
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(messageService.getConversations).toHaveBeenCalledTimes(2);
  });
});
