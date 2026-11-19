import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../contexts/AuthContext";
import { LiveProvider } from "../../contexts/LiveContext";
import { createFakeLiveConnection } from "../../contexts/testing/fakeLiveConnection";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
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

describe("MessagesPage Arabic", () => {
  afterEach(async () => {
    cleanup();
    localStorage.clear();
    await resetLanguage();
  });

  it("shows the title, empty states and new message button in Arabic", async () => {
    localStorage.setItem("microlims_token", "h.e30=.s");
    vi.mocked(messageService.getConversations).mockResolvedValue([]);
    const connection = createFakeLiveConnection();

    await renderInArabic(
      <AuthProvider>
        <LiveProvider connection={connection}>
          <MemoryRouter>
            <MessagesPage />
          </MemoryRouter>
        </LiveProvider>
      </AuthProvider>,
      ["messages"]
    );

    expect(await screen.findByText("لا توجد محادثات بعد. ابدأ رسالة جديدة!")).toBeTruthy();
    expect(screen.getByText("اختر محادثة")).toBeTruthy();
    expect(screen.getByText("الرسائل المباشرة والجماعية")).toBeTruthy();
    expect(screen.getByRole("button", { name: "رسالة جديدة" })).toBeTruthy();
  });
});
