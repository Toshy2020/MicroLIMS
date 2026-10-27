import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { SettingsPage } from "./SettingsPage";
import { SettingsService, SystemSetting } from "./services/SettingsService";

vi.mock("./services/SettingsService", () => ({ SettingsService: { getAll: vi.fn(), update: vi.fn(), history: vi.fn() } }));
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
});
