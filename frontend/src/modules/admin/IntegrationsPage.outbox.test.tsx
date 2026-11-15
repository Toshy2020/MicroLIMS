import { AxiosError, AxiosHeaders } from "axios";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import {
  type EmailOutboxSummaryDto,
  notificationService
} from "../notifications/services/notificationService";
import { IntegrationsPage } from "./IntegrationsPage";
import { IntegrationsService } from "./services/IntegrationsService";

vi.mock("./services/IntegrationsService", () => ({
  IntegrationsService: {
    agents: vi.fn(),
    smtp: vi.fn(),
    storage: vi.fn(),
    rotateAgentKey: vi.fn(),
    revokeAgent: vi.fn(),
    sendTestEmail: vi.fn(),
    probeStorage: vi.fn()
  }
}));

vi.mock("../notifications/services/notificationService", () => ({
  notificationService: {
    getEmailOutboxSummary: vi.fn()
  }
}));

function mockBaseIntegrations() {
  vi.mocked(IntegrationsService.agents).mockResolvedValue([]);
  vi.mocked(IntegrationsService.smtp).mockResolvedValue({
    isConfigured: true,
    host: "smtp.example.org",
    port: 587,
    fromAddress: "noreply@example.org",
    enableSsl: true,
    authenticationConfigured: true,
    password: "••••",
    lastSendOutcome: null,
    lastSendAtUtc: null,
    lastSendError: null
  });
  vi.mocked(IntegrationsService.storage).mockResolvedValue({
    provider: "Local",
    location: "C:\\storage",
    freeBytes: 1024,
    totalBytes: 2048
  });
}

function renderIntegrations(permissions: string[]) {
  localStorage.clear();
  localStorage.setItem("microlims_permissions", JSON.stringify(permissions));
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <IntegrationsPage />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.clearAllMocks();
});

describe("IntegrationsPage Email delivery outbox card", () => {
  it("hides the Email delivery card without Users.Manage permission", async () => {
    mockBaseIntegrations();
    renderIntegrations([PERMISSIONS.MASTER_DATA_MANAGE]);

    await screen.findByRole("heading", { name: "Email (SMTP)" });
    expect(screen.queryByRole("heading", { name: "Email delivery" })).toBeNull();
    expect(notificationService.getEmailOutboxSummary).not.toHaveBeenCalled();
  });

  it("renders waiting/failed counts, last sent timestamp, and users without email with loading state", async () => {
    mockBaseIntegrations();
    let resolveOutbox!: (summary: EmailOutboxSummaryDto) => void;
    vi.mocked(notificationService.getEmailOutboxSummary).mockImplementation(
      () => new Promise<EmailOutboxSummaryDto>((resolve) => {
        resolveOutbox = resolve;
      })
    );

    renderIntegrations([PERMISSIONS.USERS_MANAGE]);

    expect(await screen.findByText("Loading email delivery status...")).toBeTruthy();

    resolveOutbox({
      waiting: 4,
      failed: 2,
      lastSentAt: "2026-11-15T03:30:00Z",
      usersWithoutEmail: [
        { id: 9, username: "k.tariq", fullName: "Karim Tariq", roleName: "Analyst" }
      ]
    });

    const summaryTable = await screen.findByRole("table", { name: "Email delivery summary" });
    expect(within(summaryTable).getByText("4")).toBeTruthy();
    expect(within(summaryTable).getByText("2")).toBeTruthy();
    expect(within(summaryTable).queryByText("Never")).toBeNull();

    const usersTable = screen.getByRole("table", { name: "Users without an email address" });
    expect(within(usersTable).getByText("Karim Tariq")).toBeTruthy();
    expect(within(usersTable).getByText("k.tariq")).toBeTruthy();
    expect(within(usersTable).getByText("Analyst")).toBeTruthy();
  });

  it("shows empty users message and Never when no emails have been sent", async () => {
    mockBaseIntegrations();
    vi.mocked(notificationService.getEmailOutboxSummary).mockResolvedValue({
      waiting: 0,
      failed: 0,
      lastSentAt: null,
      usersWithoutEmail: []
    });

    renderIntegrations([PERMISSIONS.USERS_MANAGE]);

    const summaryTable = await screen.findByRole("table", { name: "Email delivery summary" });
    expect(within(summaryTable).getByText("Never")).toBeTruthy();
    expect(screen.getByText("All active users have an email address.")).toBeTruthy();
  });

  it("renders error state and retries loading outbox summary", async () => {
    const user = userEvent.setup();
    mockBaseIntegrations();
    vi.mocked(notificationService.getEmailOutboxSummary)
      .mockRejectedValueOnce(
        new AxiosError("Request failed", "ERR_BAD_REQUEST", undefined, undefined, {
          data: { message: "Outbox query timed out" },
          status: 500,
          statusText: "Server Error",
          headers: {},
          config: { headers: new AxiosHeaders() }
        })
      )
      .mockResolvedValueOnce({
        waiting: 1,
        failed: 0,
        lastSentAt: null,
        usersWithoutEmail: []
      });

    renderIntegrations([PERMISSIONS.USERS_MANAGE]);

    const card = (await screen.findByRole("heading", { name: "Email delivery" })).closest("section") as HTMLElement;
    const alert = await within(card).findByRole("alert");
    expect(alert.textContent).toContain("Outbox query timed out");

    await user.click(within(card).getByRole("button", { name: "Retry" }));
    expect(await within(card).findByText("All active users have an email address.")).toBeTruthy();
    expect(notificationService.getEmailOutboxSummary).toHaveBeenCalledTimes(2);
  });
});
