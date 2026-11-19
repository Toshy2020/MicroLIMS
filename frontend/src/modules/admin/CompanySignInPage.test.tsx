import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { CompanySignInPage } from "./CompanySignInPage";
import { SsoAdminService, type SsoAdminOverview } from "./services/SsoAdminService";
import { SettingsService, type SystemSetting, type SystemSettingsOverview } from "./services/SettingsService";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";

vi.mock("./services/SsoAdminService", () => ({
  SsoAdminService: { getOverview: vi.fn(), testConnection: vi.fn(), previewLinks: vi.fn(), importLinks: vi.fn() }
}));
vi.mock("./services/SettingsService", () => ({
  SettingsService: { getAll: vi.fn(), update: vi.fn(), history: vi.fn() }
}));
vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const TESTED_AT = "2026-11-12T08:30:00Z";

const overview = (over: Partial<SsoAdminOverview> = {}): SsoAdminOverview => ({
  mode: "Off",
  configuration: { Authority: "https://login.example.test", ClientId: "microlims", UsePkce: true, Scope: null },
  lastTestPassedAt: TESTED_AT,
  blockers: [],
  unlinkedUsers: [],
  ...over
});

const modeSetting = (over: Partial<SystemSetting> = {}): SystemSetting => ({
  key: "security.sso.mode", category: "Security", description: "Company sign-in mode", valueType: "String", value: "Off",
  defaultValue: "Off", source: "Default", unit: null, min: null, max: null, allowedValues: ["Off", "OpenIdConnect", "ActiveDirectory"],
  isGxpRelevant: true, requiresSignature: false, version: 0, lastChangedAt: null, lastChangedByUserId: null, lastChangedByName: null,
  ...over
});

const settings = (s: SystemSetting = modeSetting()): SystemSettingsOverview => ({ settings: [s], fixedItems: [] });

beforeEach(() => {
  localStorage.setItem("microlims_username", "admin");
  localStorage.setItem("microlims_full_name", "Ada Admin");
  vi.mocked(SsoAdminService.getOverview).mockResolvedValue(overview());
  vi.mocked(SettingsService.getAll).mockResolvedValue(settings());
});
afterEach(() => { cleanup(); localStorage.clear(); vi.clearAllMocks(); });

const renderPage = () => render(<PinnedLightTheme><AuthProvider><CompanySignInPage /></AuthProvider></PinnedLightTheme>);

describe("CompanySignInPage", () => {
  it("page shows mode, read-only configuration, last test time with time zone, and each blocker as text", async () => {
    vi.mocked(SsoAdminService.getOverview).mockResolvedValue(overview({
      blockers: ["No emergency account has a second factor.", "2 company-login users are not linked."]
    }));
    renderPage();

    expect(await screen.findByText("Mode: Off")).toBeTruthy();
    const config = screen.getByRole("table", { name: "Company sign-in configuration" });
    expect(within(config).getByText("Authority")).toBeTruthy();
    expect(within(config).getByText("https://login.example.test")).toBeTruthy();
    expect(within(config).getByText("Yes")).toBeTruthy();
    expect(within(config).getByText("Not set")).toBeTruthy();
    // Read-only: no field to edit a value.
    expect(within(config).queryByRole("textbox")).toBeNull();

    const expectedTime = formatDateTimeWithTimeZone(TESTED_AT);
    expect(expectedTime).toMatch(/(UTC|GMT[+-]?\d*|[A-Z]{2,5})$/);
    expect(screen.getByText((_, el) => el?.tagName === "P" && (el.textContent ?? "").includes(expectedTime))).toBeTruthy();

    const list = screen.getByRole("list", { name: "Readiness blockers" });
    expect(within(list).getByText("No emergency account has a second factor.")).toBeTruthy();
    expect(within(list).getByText("2 company-login users are not linked.")).toBeTruthy();
  });

  it("Test connection shows success, or the failure reason in an alert", async () => {
    const user = userEvent.setup();
    vi.mocked(SsoAdminService.testConnection)
      .mockResolvedValueOnce({ passed: true, testedAt: "2026-11-13T09:00:00Z" })
      .mockRejectedValueOnce({ response: { data: { message: "Directory server refused the bind." } } });
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Test connection" }));
    const ok = await screen.findByRole("alert");
    expect(ok.textContent).toContain("Connection test passed at");
    expect(ok.textContent).toContain(formatDateTimeWithTimeZone("2026-11-13T09:00:00Z"));

    await user.click(screen.getByRole("button", { name: "Test connection" }));
    await waitFor(() => expect(screen.getByRole("alert").textContent).toBe("Directory server refused the bind."));
  });

  it("switch-on control is disabled with the blockers listed while any blocker remains", async () => {
    vi.mocked(SsoAdminService.getOverview).mockResolvedValue(overview({ blockers: ["Test the connection first."] }));
    renderPage();

    const button = (await screen.findByRole("button", { name: "Switch on company sign-in" })) as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    expect(screen.getByText("Test the connection first.")).toBeTruthy();
    expect(screen.getByText(/until every blocker above is resolved/)).toBeTruthy();
  });

  it("switch-on control saves the sign-in mode setting with a reason once no blocker remains", async () => {
    const user = userEvent.setup();
    vi.mocked(SettingsService.update).mockResolvedValue(modeSetting({ value: "OpenIdConnect" }));
    renderPage();

    const button = (await screen.findByRole("button", { name: "Switch on company sign-in" })) as HTMLButtonElement;
    expect(button.disabled).toBe(false);
    await user.click(button);
    const dialog = await screen.findByRole("dialog");
    const confirm = within(dialog).getByRole("button", { name: "Switch on" }) as HTMLButtonElement;
    expect(confirm.disabled).toBe(true);
    await user.type(within(dialog).getByLabelText(/Reason for change/), "Go live");
    await user.click(confirm);

    await waitFor(() => expect(SettingsService.update).toHaveBeenCalledWith("security.sso.mode", "OpenIdConnect", "Go live", undefined));
  });

  it("unlinked users report lists users and is replaced by an empty state when none", async () => {
    vi.mocked(SsoAdminService.getOverview).mockResolvedValueOnce(overview({
      unlinkedUsers: [{ id: 5, username: "jdoe", fullName: "Jane Doe", role: "SystemAdministrator" }]
    }));
    const first = renderPage();
    const table = await screen.findByRole("table", { name: "Unlinked users" });
    expect(within(table).getByText("jdoe")).toBeTruthy();
    expect(within(table).getByText("Jane Doe")).toBeTruthy();
    expect(within(table).getByText("System Administrator")).toBeTruthy();
    first.unmount();

    renderPage();
    expect(await screen.findByText("No unlinked users")).toBeTruthy();
    expect(screen.queryByRole("table", { name: "Unlinked users" })).toBeNull();
  });

  it("shows a loading state, then an error with retry when the status cannot be loaded", async () => {
    const user = userEvent.setup();
    let reject: (e: unknown) => void = () => {};
    vi.mocked(SsoAdminService.getOverview).mockReturnValueOnce(new Promise((_, r) => { reject = r; }));
    renderPage();
    expect(screen.getByRole("status", { name: "Loading company sign-in status" })).toBeTruthy();

    reject({ response: { data: { message: "Directory settings are unreadable." } } });
    expect(await screen.findByText("Directory settings are unreadable.")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByText("Mode: Off")).toBeTruthy();
    expect(SsoAdminService.getOverview).toHaveBeenCalledTimes(2);
  });

  it("partial outage: names the setting that failed to load and keeps switch-on gated with its reason", async () => {
    vi.mocked(SettingsService.getAll).mockRejectedValue(new Error("down"));
    renderPage();

    expect(await screen.findByText("Some data could not be loaded: sign-in mode setting.")).toBeTruthy();
    const button = screen.getByRole("button", { name: "Switch on company sign-in" }) as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    expect(screen.getByText("The sign-in mode setting could not be loaded.")).toBeTruthy();
    expect(screen.getByText("Mode: Off")).toBeTruthy();
  });
});
