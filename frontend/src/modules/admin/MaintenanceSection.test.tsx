import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { MaintenanceSection } from "./MaintenanceSection";
import { SettingsService, SystemSetting } from "./services/SettingsService";

vi.mock("./services/SettingsService", () => ({ SettingsService: { getAll: vi.fn(), update: vi.fn(), history: vi.fn() } }));
vi.mock("../../contexts/AuthContext", () => ({ useAuth: () => ({ fullName: "Ada Admin", username: "ada", role: "SystemAdministrator" }) }));
vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } }));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

const setting = (key: string, value: string, over: Partial<SystemSetting> = {}): SystemSetting => ({
  key, category: "System", description: "d", valueType: "String", value, defaultValue: value, source: "Default",
  unit: null, min: null, max: null, allowedValues: null, isGxpRelevant: false, requiresSignature: false,
  version: 0, lastChangedAt: null, lastChangedByUserId: null, lastChangedByName: null, ...over
});

const settings = (on = false): SystemSetting[] => [
  setting("System.MaintenanceMode", on ? "true" : "false", { valueType: "Boolean", isGxpRelevant: true, requiresSignature: true }),
  setting("System.MaintenanceMessage", "Back soon"),
  setting("System.AnnouncementBanner", ""),
  setting("System.AnnouncementSeverity", "info", { allowedValues: ["info", "warning"] })
];

const renderSection = (on = false, onChanged = vi.fn()) =>
  render(<PinnedLightTheme><MaintenanceSection settings={settings(on)} onChanged={onChanged} /></PinnedLightTheme>);

describe("MaintenanceSection", () => {
  it("turns maintenance on only with a reason and an electronic signature", async () => {
    vi.mocked(SettingsService.update).mockResolvedValue(setting("System.MaintenanceMode", "true"));
    const onChanged = vi.fn();
    renderSection(false, onChanged);

    fireEvent.click(screen.getByRole("button", { name: "Turn maintenance mode on" }));
    const next = screen.getByRole("button", { name: "Continue to sign" }) as HTMLButtonElement;
    expect(next.disabled).toBe(true);
    fireEvent.change(screen.getAllByLabelText(/Reason for change/).at(-1)!, { target: { value: "Database upgrade" } });
    fireEvent.click(next);

    fireEvent.change(await screen.findByLabelText(/^Password/), { target: { value: "AdminP@ss1" } });
    expect(SettingsService.update).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => expect(SettingsService.update).toHaveBeenCalledWith("System.MaintenanceMode", "true", "Database upgrade", "AdminP@ss1"));
    await waitFor(() => expect(onChanged).toHaveBeenCalled());
  });

  it("offers to turn maintenance off when it is on", () => {
    renderSection(true);
    expect(screen.getByRole("button", { name: "Turn maintenance mode off" })).toBeTruthy();
    expect(screen.getByText(/Maintenance mode is ON/)).toBeTruthy();
  });

  it("previews the banner and saves message and banner with a reason but no password", async () => {
    vi.mocked(SettingsService.update).mockResolvedValue(setting("System.AnnouncementBanner", "x"));
    renderSection();

    const save = screen.getByRole("button", { name: "Save message and banner" }) as HTMLButtonElement;
    expect(save.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText("Announcement banner"), { target: { value: "Planned downtime Friday" } });
    expect(screen.getByRole("status", { name: "Announcement" }).textContent).toContain("Planned downtime Friday");
    expect(save.disabled).toBe(true); // still needs a reason
    fireEvent.change(screen.getAllByLabelText(/Reason for change/)[0], { target: { value: "Announce window" } });
    fireEvent.click(save);

    await waitFor(() => expect(SettingsService.update).toHaveBeenCalledWith("System.AnnouncementBanner", "Planned downtime Friday", "Announce window"));
    expect(SettingsService.update).toHaveBeenCalledTimes(1);
  });
});
