import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { SettingsPage } from "./SettingsPage";
import { SettingsService, SystemSetting } from "./services/SettingsService";
import { IntegrationsService } from "./services/IntegrationsService";

vi.mock("./services/SettingsService", () => ({ SettingsService: { getAll: vi.fn(), update: vi.fn(), history: vi.fn() } }));
vi.mock("./services/IntegrationsService", () => ({ IntegrationsService: { sendTestAlert: vi.fn() } }));
vi.mock("../../contexts/AuthContext", () => ({ useAuth: () => ({ fullName: "Ada Admin", username: "ada", role: "SystemAdministrator" }) }));
vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

const base: SystemSetting = {
  key: "security.lockout.maxFailedAttempts", category: "Security", description: "Failed sign-ins before the account is locked.",
  valueType: "Integer", value: "5", defaultValue: "5", source: "Default", unit: "attempts", min: 3, max: 20, allowedValues: null,
  isGxpRelevant: true, requiresSignature: true, version: 0, lastChangedAt: null, lastChangedByUserId: null, lastChangedByName: null
};
const plain: SystemSetting = {
  ...base, key: "retention.sessionTokens.retentionDays", category: "Retention", description: "Token retention.",
  value: "30", defaultValue: "30", unit: "days", min: 1, max: 365, isGxpRelevant: false, requiresSignature: false
};

function mockOverview() {
  vi.mocked(SettingsService.getAll).mockResolvedValue({
    settings: [base, plain],
    fixedItems: [{ key: "secrets", label: "Secrets", value: "********", source: "Environment", note: "Never stored.", editLocation: null }]
  });
}

describe("SettingsPage", () => {
  it("lists settings with value, source and the masked secrets row", async () => {
    mockOverview();
    render(<PinnedLightTheme><SettingsPage /></PinnedLightTheme>);
    expect(await screen.findByText("security.lockout.maxFailedAttempts")).toBeTruthy();
    expect(screen.getByText(/5 attempts/)).toBeTruthy();
    expect(screen.getByText("********")).toBeTruthy();
    expect(screen.getByText("Signature")).toBeTruthy();
  });

  it("requires a reason and saves a non-GxP key without a password", async () => {
    mockOverview();
    vi.mocked(SettingsService.update).mockResolvedValue({ ...plain, value: "60", version: 1 });
    render(<PinnedLightTheme><SettingsPage /></PinnedLightTheme>);
    fireEvent.click(await screen.findByLabelText("Edit retention.sessionTokens.retentionDays"));
    const save = screen.getByRole("button", { name: "Save" }) as HTMLButtonElement;
    expect(save.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText(/^Value/), { target: { value: "60" } });
    fireEvent.change(screen.getByLabelText(/Reason for change/), { target: { value: "Align with policy" } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    await waitFor(() => expect(SettingsService.update).toHaveBeenCalledWith("retention.sessionTokens.retentionDays", "60", "Align with policy", undefined));
  });

  it("asks for the signature on a GxP-relevant key and sends the password", async () => {
    mockOverview();
    vi.mocked(SettingsService.update).mockResolvedValue({ ...base, value: "6", version: 1 });
    render(<PinnedLightTheme><SettingsPage /></PinnedLightTheme>);
    fireEvent.click(await screen.findByLabelText("Edit security.lockout.maxFailedAttempts"));
    fireEvent.change(screen.getByLabelText(/^Value/), { target: { value: "6" } });
    fireEvent.change(screen.getByLabelText(/Reason for change/), { target: { value: "Tighten lockout" } });
    fireEvent.click(screen.getByRole("button", { name: "Continue to sign" }));
    expect(SettingsService.update).not.toHaveBeenCalled();
    fireEvent.change(await screen.findByLabelText(/Password/), { target: { value: "Secret#1" } });
    fireEvent.click(screen.getByRole("button", { name: /Sign/ }));
    await waitFor(() => expect(SettingsService.update).toHaveBeenCalledWith("security.lockout.maxFailedAttempts", "6", "Tighten lockout", "Secret#1"));
  });

  it("shows the history of a setting", async () => {
    mockOverview();
    vi.mocked(SettingsService.history).mockResolvedValue([
      { version: 1, value: "6", previousValue: "5", changedByUserId: 1, changedByName: "Ada Admin", changedAt: "2026-10-27T10:00:00Z", reason: "Tighten lockout", electronicSignatureId: 9 }
    ]);
    render(<PinnedLightTheme><SettingsPage /></PinnedLightTheme>);
    fireEvent.click(await screen.findByLabelText("History of security.lockout.maxFailedAttempts"));
    expect(await screen.findByText("Reason: Tighten lockout")).toBeTruthy();
    expect(screen.getByText(/\(e-signed\)/)).toBeTruthy();
  });

  it("says so when the settings cannot be loaded", async () => {
    vi.mocked(SettingsService.getAll).mockRejectedValue(new Error("403"));
    render(<PinnedLightTheme><SettingsPage /></PinnedLightTheme>);
    expect(await screen.findByText(/Could not load the settings/)).toBeTruthy();
  });

  const recipients: SystemSetting = {
    ...plain, key: "Alerts.CriticalRecipients", category: "Alerts", description: "Who is emailed.", valueType: "EmailList",
    value: "a@example.test,b@example.test", defaultValue: "", unit: "addresses", min: null, max: 20
  };

  it("edits the recipient list one per line, allows clearing it, and sends the text as typed", async () => {
    vi.mocked(SettingsService.getAll).mockResolvedValue({ settings: [recipients], fixedItems: [] });
    vi.mocked(SettingsService.update).mockResolvedValue({ ...recipients, value: "" });
    render(<PinnedLightTheme><SettingsPage /></PinnedLightTheme>);
    expect(await screen.findByText("a@example.test, b@example.test")).toBeTruthy();
    fireEvent.click(screen.getByLabelText("Edit Alerts.CriticalRecipients"));
    const value = screen.getByLabelText(/^Value/) as HTMLTextAreaElement;
    expect(value.value).toBe("a@example.test\nb@example.test");
    fireEvent.change(value, { target: { value: "" } });
    fireEvent.change(screen.getByLabelText(/Reason for change/), { target: { value: "Stand down" } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    await waitFor(() => expect(SettingsService.update).toHaveBeenCalledWith("Alerts.CriticalRecipients", "", "Stand down", undefined));
  });

  it("sends a test alert with a reason and shows each recipient's outcome", async () => {
    vi.mocked(SettingsService.getAll).mockResolvedValue({ settings: [recipients], fixedItems: [] });
    vi.mocked(IntegrationsService.sendTestAlert).mockResolvedValue({
      recipientCount: 2, sent: 1,
      outcomes: [{ recipient: "a@example.test", outcome: "Sent", error: null }, { recipient: "b@example.test", outcome: "Failed", error: "mailbox unavailable" }]
    });
    render(<PinnedLightTheme><SettingsPage /></PinnedLightTheme>);
    fireEvent.click(await screen.findByRole("button", { name: "Send test alert" }));
    const send = screen.getAllByRole("button", { name: "Send test alert" }).slice(-1)[0] as HTMLButtonElement;
    expect(send.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText(/Reason/), { target: { value: "Drill" } });
    fireEvent.click(send);
    await waitFor(() => expect(IntegrationsService.sendTestAlert).toHaveBeenCalledWith("Drill"));
    expect(await screen.findByText("b@example.test: Failed - mailbox unavailable")).toBeTruthy();
  });
});
