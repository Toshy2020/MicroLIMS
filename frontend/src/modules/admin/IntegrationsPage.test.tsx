import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { IntegrationsPage } from "./IntegrationsPage";
import { IntegrationsService } from "./services/IntegrationsService";

const auth = { permissions: ["System.ConfigureSettings"] as string[] };
vi.mock("../../contexts/AuthContext", () => ({ useAuth: () => auth }));
vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock("./services/IntegrationsService", () => ({
  IntegrationsService: {
    agents: vi.fn(), smtp: vi.fn(), storage: vi.fn(), sendTestEmail: vi.fn(), probeStorage: vi.fn(),
    sendTestAlert: vi.fn(), rotateAgentKey: vi.fn(), revokeAgent: vi.fn()
  }
}));

afterEach(() => { cleanup(); vi.clearAllMocks(); auth.permissions = ["System.ConfigureSettings"]; });

function mockStatus() {
  vi.mocked(IntegrationsService.agents).mockResolvedValue([
    { id: 1, code: "AGT-HPLC1", name: "Lab PC", equipmentId: 4, equipmentCode: "HPLC-01", keyPrefix: "abcd1234", status: "Active",
      lastSeenAt: "2026-10-28T08:00:00Z", createdAt: "2026-10-01T08:00:00Z", revokedAt: null, revokeReason: null },
    { id: 2, code: "AGT-HPLC2", name: "Old PC", equipmentId: 5, equipmentCode: "HPLC-02", keyPrefix: "zzzz9999", status: "Revoked",
      lastSeenAt: null, createdAt: "2026-09-01T08:00:00Z", revokedAt: "2026-10-02T08:00:00Z", revokeReason: "retired" }
  ]);
  vi.mocked(IntegrationsService.smtp).mockResolvedValue({
    isConfigured: true, host: "smtp.example.test", port: 587, fromAddress: "no-reply@example.test", enableSsl: true,
    authenticationConfigured: true, password: "********", lastSendAtUtc: null, lastSendOutcome: null, lastSendError: null
  });
  vi.mocked(IntegrationsService.storage).mockResolvedValue({ provider: "Local", location: "/data/storage", freeBytes: 5 * 1024 ** 3, totalBytes: 10 * 1024 ** 3 });
}

const renderPage = () => render(<PinnedLightTheme><IntegrationsPage /></PinnedLightTheme>);

describe("IntegrationsPage", () => {
  it("shows agents, masked SMTP password and storage free space", async () => {
    mockStatus();
    renderPage();
    expect(await screen.findByText("AGT-HPLC1")).toBeTruthy();
    expect(screen.getByText("Revoked")).toBeTruthy();
    expect(screen.getByText("smtp.example.test")).toBeTruthy();
    expect(screen.getByText("********")).toBeTruthy();
    expect(screen.getByText("/data/storage")).toBeTruthy();
    expect(screen.getByText("5.0 GB of 10.0 GB")).toBeTruthy();
  });

  it("offers rotate and revoke only to holders of the master data permission, and only for active agents", async () => {
    mockStatus();
    renderPage();
    await screen.findByText("AGT-HPLC1");
    expect(screen.queryByRole("button", { name: "Rotate key of AGT-HPLC1" })).toBeNull();
    cleanup();

    auth.permissions = ["System.ConfigureSettings", "MasterData.Manage"];
    renderPage();
    await screen.findByText("AGT-HPLC1");
    expect(screen.getByRole("button", { name: "Rotate key of AGT-HPLC1" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Rotate key of AGT-HPLC2" })).toBeNull();
  });

  it("sends a test email only with an address and a reason, and shows the outcome", async () => {
    mockStatus();
    vi.mocked(IntegrationsService.sendTestEmail).mockResolvedValue({
      recipientCount: 1, sent: 1, outcomes: [{ recipient: "ops@example.test", outcome: "Sent", error: null }]
    });
    renderPage();
    fireEvent.click(await screen.findByRole("button", { name: "Send test email" }));
    const send = screen.getAllByRole("button", { name: "Send test email" }).slice(-1)[0] as HTMLButtonElement;
    expect(send.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText(/Send to/), { target: { value: "ops@example.test" } });
    fireEvent.change(screen.getByLabelText(/Reason/), { target: { value: "Relay change" } });
    fireEvent.click(send);
    await waitFor(() => expect(IntegrationsService.sendTestEmail).toHaveBeenCalledWith("ops@example.test", "Relay change"));
    expect(await screen.findByText("ops@example.test: Sent")).toBeTruthy();
  });

  it("shows the server's message when the rate limit refuses a probe", async () => {
    mockStatus();
    vi.mocked(IntegrationsService.probeStorage).mockRejectedValue(
      Object.assign(new Error("x"), { isAxiosError: true, response: { data: { message: "Try again in 40 s." } } }));
    renderPage();
    fireEvent.click(await screen.findByRole("button", { name: "Run storage probe" }));
    fireEvent.change(screen.getByLabelText(/Reason/), { target: { value: "Check" } });
    fireEvent.click(screen.getByRole("button", { name: "Run probe" }));
    expect(await screen.findByText("Try again in 40 s.")).toBeTruthy();
  });

  it("reports each step of a successful probe", async () => {
    mockStatus();
    vi.mocked(IntegrationsService.probeStorage).mockResolvedValue({
      succeeded: true, warning: null,
      steps: [{ step: "Write", succeeded: true, elapsedMs: 3, error: null }, { step: "Read back", succeeded: true, elapsedMs: 2, error: null }, { step: "Delete", succeeded: true, elapsedMs: 1, error: null }]
    });
    renderPage();
    fireEvent.click(await screen.findByRole("button", { name: "Run storage probe" }));
    fireEvent.change(screen.getByLabelText(/Reason/), { target: { value: "Check" } });
    fireEvent.click(screen.getByRole("button", { name: "Run probe" }));
    expect(await screen.findByText("Write, read and delete all succeeded.")).toBeTruthy();
  });
});
