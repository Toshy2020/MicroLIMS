import { useState } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { BenchModeProvider } from "../../../theme/BenchModeContext";
import { ThemeModeProvider } from "../../../theme/ThemeModeContext";
import { AuthProvider } from "../../../contexts/AuthContext";
import { MainLayout } from "../../../layouts/MainLayout";
import { MyWorkPage } from "../../myWork/MyWorkPage";
import { resetUiStateCache } from "../../../hooks/useUiState";
import { PERMISSIONS } from "../../../routes/routes";
import { HelpMenu } from "../HelpMenu";
import { HelpPanelProvider, type HelpPanelServices } from "../HelpPanelContext";
import { HelpPanelSlot } from "../HelpPanelSlot";
import { TOUR_STEPS, tourStateKey, type RoleType } from "../tour/tourSteps";
import { WhatsNewDialog } from "./WhatsNewDialog";
import { RELEASE_NOTES, WHATS_NEW_STATE_KEY, type ReleaseNote } from "./releaseNotes";

const uiStateMock = vi.hoisted(() => ({
  getAll: vi.fn(),
  set: vi.fn(),
  remove: vi.fn()
}));
vi.mock("../../../services/uiStateService", () => ({
  uiStateService: uiStateMock
}));

const mfaMock = vi.hoisted(() => ({
  getStatus: vi.fn()
}));
vi.mock("../../authentication/services/mfaService", () => ({
  mfaService: mfaMock
}));

const labSectionMock = vi.hoisted(() => ({
  getMySections: vi.fn()
}));
vi.mock("../../../services/laboratorySectionService", () => ({
  laboratorySectionService: labSectionMock
}));

const myWorkMock = vi.hoisted(() => ({
  get: vi.fn(),
  summary: vi.fn(),
  take: vi.fn(),
  release: vi.fn()
}));
vi.mock("../../myWork/services/myWorkService", () => ({
  myWorkService: myWorkMock
}));

const versionServiceMock = vi.hoisted(() => ({
  get: vi.fn()
}));
vi.mock("../../../services/systemVersionService", () => ({
  systemVersionService: versionServiceMock
}));

vi.mock("../../../services/apiClient", () => ({
  apiClient: {
    get: vi.fn((url: string) =>
      Promise.resolve({ data: { data: url.includes("/notifications") ? [] : { unreadCount: 0 } } })
    ),
    post: vi.fn().mockResolvedValue({}),
    put: vi.fn().mockResolvedValue({}),
    delete: vi.fn().mockResolvedValue({})
  }
}));

const ROLE_PERMISSIONS: Record<RoleType, string[]> = {
  Analyst: [
    PERMISSIONS.TEST_WORKFLOW_EXECUTE,
    PERMISSIONS.SAMPLES_RECEIVE_OWN_LAB,
    PERMISSIONS.MEDIA_PREPARE,
    PERMISSIONS.MONITORING_PLAN_VIEW
  ],
  Reviewer: [
    PERMISSIONS.SAMPLES_REVIEW,
    PERMISSIONS.SAMPLES_RECEIVE_OWN_LAB,
    PERMISSIONS.DASHBOARDS_REVIEW,
    PERMISSIONS.MONITORING_PLAN_VIEW
  ],
  SectionHead: [
    PERMISSIONS.SAMPLES_REVIEW,
    PERMISSIONS.SAMPLES_APPROVE,
    PERMISSIONS.SAMPLES_RECEIVE,
    PERMISSIONS.SAMPLES_TRACK_ALL,
    PERMISSIONS.OOS_VIEW,
    PERMISSIONS.OOS_MANAGE,
    PERMISSIONS.ITEMS_MANAGE,
    PERMISSIONS.MASTER_DATA_MANAGE,
    PERMISSIONS.AUDIT_VIEW,
    PERMISSIONS.DASHBOARDS_LAB_OVERVIEW,
    PERMISSIONS.MONITORING_PLAN_VIEW
  ],
  QcManager: [
    PERMISSIONS.NOT_APPLICABLE_TESTS_FINAL_APPROVE,
    PERMISSIONS.MONITORING_PLAN_VIEW,
    PERMISSIONS.TABLE_VIEWS_SHARE,
    PERMISSIONS.DATA_EXPORT
  ],
  Qa: [
    PERMISSIONS.AUDIT_VIEW,
    PERMISSIONS.AUDIT_REVIEW,
    PERMISSIONS.ACCESS_REVIEW,
    PERMISSIONS.ACCESS_REVIEW_SIGN,
    PERMISSIONS.SAMPLES_TRACK_ALL,
    PERMISSIONS.SECTIONS_VIEW_ALL,
    PERMISSIONS.MONITORING_PLAN_VIEW,
    PERMISSIONS.DASHBOARDS_LAB_OVERVIEW
  ],
  SystemAdministrator: [
    PERMISSIONS.USERS_MANAGE,
    PERMISSIONS.ROLES_MANAGE,
    PERMISSIONS.SYSTEM_CONFIGURE_SETTINGS,
    PERMISSIONS.SYSTEM_VIEW_ERROR_LOG,
    PERMISSIONS.SYSTEM_MANAGE_JOBS,
    PERMISSIONS.AUDIT_VIEW,
    PERMISSIONS.ACCESS_REVIEW,
    PERMISSIONS.ITEMS_MANAGE,
    PERMISSIONS.MASTER_DATA_MANAGE,
    PERMISSIONS.DOCUMENTS_CONFIG_MANAGE,
    PERMISSIONS.SAMPLES_TRACK_ALL,
    PERMISSIONS.MONITORING_PLAN_VIEW
  ]
};

interface SeedOptions {
  role?: RoleType;
  mustChangePassword?: boolean;
  signingPasswordRequired?: boolean;
  userId?: number;
}

function seedSession(options: SeedOptions = {}) {
  const role = options.role ?? "Analyst";
  const permissions = ROLE_PERMISSIONS[role];
  const payload = btoa(
    JSON.stringify({
      "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": role,
      permission: permissions
    })
  );
  localStorage.setItem("microlims_token", `h.${payload}.s`);
  localStorage.setItem("microlims_username", "test.user");
  localStorage.setItem("microlims_full_name", "Test User");
  localStorage.setItem("microlims_role", role);
  localStorage.setItem("microlims_permissions", JSON.stringify(permissions));
  localStorage.setItem("microlims_user_id", String(options.userId ?? 7));
  localStorage.setItem("microlims_must_change_password", String(Boolean(options.mustChangePassword)));
  localStorage.setItem("microlims_signing_password_required", String(Boolean(options.signingPasswordRequired)));
}

function renderAppLayout(
  options: SeedOptions = {},
  whatsNewNotes?: readonly ReleaseNote[]
) {
  seedSession(options);
  return render(
    <BenchModeProvider>
      <ThemeModeProvider>
        <PinnedLightTheme>
          <AuthProvider>
            <MemoryRouter initialEntries={["/my-work"]}>
              <Routes>
                <Route element={<MainLayout whatsNewNotes={whatsNewNotes} />}>
                  <Route path="/my-work" element={<MyWorkPage />} />
                  <Route path="/dashboard" element={<div>Dashboard Content</div>} />
                  <Route path="/change-password" element={<div>Change Password Page</div>} />
                  <Route path="/set-signing-password" element={<div>Set Signing Password Page</div>} />
                </Route>
              </Routes>
            </MemoryRouter>
          </AuthProvider>
        </PinnedLightTheme>
      </ThemeModeProvider>
    </BenchModeProvider>
  );
}

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  resetUiStateCache();
  uiStateMock.getAll.mockResolvedValue({});
  uiStateMock.set.mockResolvedValue(undefined);
  uiStateMock.remove.mockResolvedValue(undefined);
  versionServiceMock.get.mockResolvedValue({ version: "0.1.0", environment: "Test" });
  mfaMock.getStatus.mockResolvedValue({
    enrolled: true,
    type: "AuthenticatorApp",
    confirmedAt: "2026-11-10T10:00:00Z",
    recoveryCodesRemaining: 10,
    required: false,
    enrolmentDueAt: null,
    trustedDevices: []
  });
  labSectionMock.getMySections.mockResolvedValue([
    { sectionCode: "MICRO", physchemAreas: [] },
    { sectionCode: "FP", physchemAreas: ["fp", "rmpm"] }
  ]);
  myWorkMock.summary.mockResolvedValue({
    total: 1,
    overdue: 0,
    now: 1,
    complete: true
  });
  myWorkMock.get.mockResolvedValue({
    timeZoneId: "UTC",
    serverTimeUtc: "2026-11-14T10:00:00Z",
    unavailableSources: [],
    groups: []
  });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  localStorage.clear();
  sessionStorage.clear();
  resetUiStateCache();
});

describe("What's new automatic prompt and dialog", () => {
  it("is shown once for a newer version and not shown again after closing", async () => {
    const user = userEvent.setup();

    // 1. Wait until UI state loads before showing, then show when whatsNew.lastSeen is absent/older.
    let resolveUiState: (value: Record<string, string>) => void = () => {};
    uiStateMock.getAll.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveUiState = resolve;
        })
    );

    const first = renderAppLayout({ role: "Analyst" });
    expect(screen.queryByRole("dialog", { name: "What's new" })).toBeNull();

    resolveUiState({
      [tourStateKey("Analyst")]: "done",
      [WHATS_NEW_STATE_KEY]: "0.0.9"
    });

    const dialog = await screen.findByRole("dialog", { name: "What's new" });
    expect(within(dialog).getByText(`Version ${__APP_VERSION__}`)).toBeTruthy();

    await user.click(within(dialog).getByRole("button", { name: "Close" }));
    await waitFor(() =>
      expect(uiStateMock.set).toHaveBeenCalledWith(WHATS_NEW_STATE_KEY, __APP_VERSION__)
    );
    await waitFor(() => expect(screen.queryByRole("dialog", { name: "What's new" })).toBeNull());

    first.unmount();
    resetUiStateCache();
    uiStateMock.set.mockClear();

    // 2. Subsequent login with current version stored does not show the dialog again.
    uiStateMock.getAll.mockResolvedValueOnce({
      [tourStateKey("Analyst")]: "done",
      [WHATS_NEW_STATE_KEY]: __APP_VERSION__
    });

    const second = renderAppLayout({ role: "Analyst" });
    await waitFor(() => expect(uiStateMock.getAll).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(mfaMock.getStatus).toHaveBeenCalledTimes(2));
    expect(screen.queryByRole("dialog", { name: "What's new" })).toBeNull();
    expect(uiStateMock.set).not.toHaveBeenCalled();
    second.unmount();
  });

  it("shows only items for the signed-in user's role", async () => {
    uiStateMock.getAll.mockResolvedValueOnce({ [tourStateKey("Qa")]: "done" });
    renderAppLayout({ role: "Qa" });

    const dialog = await screen.findByRole("dialog", { name: "What's new" });
    const expectedQaItems = RELEASE_NOTES[0].items.filter(
      (item) => item.roles === "all" || item.roles.includes("Qa")
    );
    const hiddenAnalystItems = RELEASE_NOTES[0].items.filter(
      (item) => item.roles !== "all" && !item.roles.includes("Qa")
    );
    expect(expectedQaItems.length).toBeGreaterThan(0);
    expect(hiddenAnalystItems.length).toBeGreaterThan(0);

    for (const item of expectedQaItems) {
      expect(within(dialog).getByText(item.text)).toBeTruthy();
    }
    for (const item of hiddenAnalystItems) {
      expect(within(dialog).queryByText(item.text)).toBeNull();
    }
  });

  it("does not show the dialog when the newest note has no items for the role, but still stores the version", async () => {
    const analystOnlyNotes: ReleaseNote[] = [
      {
        version: __APP_VERSION__,
        date: "2026-11-14",
        items: [{ roles: ["Analyst"], text: "Analyst-only update." }]
      }
    ];
    uiStateMock.getAll.mockResolvedValueOnce({ [tourStateKey("Qa")]: "done" });

    renderAppLayout({ role: "Qa" }, analystOnlyNotes);

    await waitFor(() =>
      expect(uiStateMock.set).toHaveBeenCalledWith(WHATS_NEW_STATE_KEY, __APP_VERSION__)
    );
    expect(screen.queryByRole("dialog", { name: "What's new" })).toBeNull();
  });

  it("does not show or store anything when the first-login tour is due or running, nor when blocked", async () => {
    const user = userEvent.setup();

    // 1. Tour key absent -> tour runs, What's new does not show even after tour is skipped in the same session.
    uiStateMock.getAll.mockResolvedValueOnce({});
    const tourSession = renderAppLayout({ role: "Analyst" });

    const tourDialog = await screen.findByRole("dialog", { name: TOUR_STEPS.Analyst[0].title });
    expect(screen.queryByRole("dialog", { name: "What's new" })).toBeNull();
    expect(uiStateMock.set).not.toHaveBeenCalled();

    await user.click(within(tourDialog).getByRole("button", { name: "Skip" }));
    await waitFor(() => expect(uiStateMock.set).toHaveBeenCalledWith(tourStateKey("Analyst"), "skipped"));
    expect(uiStateMock.set).not.toHaveBeenCalledWith(WHATS_NEW_STATE_KEY, expect.anything());
    expect(screen.queryByRole("dialog", { name: "What's new" })).toBeNull();
    tourSession.unmount();
    resetUiStateCache();
    uiStateMock.set.mockClear();

    // 2. Blocked while password change is pending.
    uiStateMock.getAll.mockResolvedValueOnce({ [tourStateKey("Analyst")]: "done" });
    const pwSession = renderAppLayout({ role: "Analyst", mustChangePassword: true });
    expect(await screen.findByText("Change Password Page")).toBeTruthy();
    expect(screen.queryByRole("dialog", { name: "What's new" })).toBeNull();
    expect(uiStateMock.set).not.toHaveBeenCalled();
    pwSession.unmount();
    resetUiStateCache();

    // 3. Blocked while forced MFA enrolment is pending.
    uiStateMock.getAll.mockResolvedValueOnce({ [tourStateKey("Analyst")]: "done" });
    mfaMock.getStatus.mockResolvedValueOnce({
      enrolled: false,
      type: null,
      confirmedAt: null,
      recoveryCodesRemaining: 0,
      required: true,
      enrolmentDueAt: null,
      trustedDevices: []
    });
    const mfaSession = renderAppLayout({ role: "Analyst" });
    await waitFor(() => expect(mfaMock.getStatus).toHaveBeenCalled());
    await waitFor(() => expect(uiStateMock.getAll).toHaveBeenCalled());
    expect(screen.queryByRole("dialog", { name: "What's new" })).toBeNull();
    expect(uiStateMock.set).not.toHaveBeenCalled();
    mfaSession.unmount();
  });

  it("Help -> What's new lists all versions filtered by role and opens linked guide chapters", async () => {
    const user = userEvent.setup();
    const multiVersionNotes: ReleaseNote[] = [
      {
        version: "0.2.0",
        date: "2026-12-01",
        items: [
          { roles: "all", text: "Added batch trend summary.", guide: { chapterId: "34-reports-kpis" } },
          { roles: ["Qa"], text: "QA-only audit note." }
        ]
      },
      {
        version: "0.1.0",
        date: "2026-11-14",
        items: [
          { roles: "all", text: "Launched My Work queue.", guide: { chapterId: "45-my-work" } }
        ]
      }
    ];
    const helpServices: HelpPanelServices = {
      loadChapter: vi.fn(async (id: string) => `## 34. Chapter ${id}\n\nDetails for ${id}.`)
    };

    seedSession({ role: "Analyst" });
    render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter initialEntries={["/my-work"]}>
            <HelpPanelProvider whatsNewNotes={multiVersionNotes}>
              <HelpMenu />
              <HelpPanelSlot services={helpServices} />
            </HelpPanelProvider>
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );

    await user.click(screen.getByRole("button", { name: "Help" }));
    await user.click(await screen.findByRole("menuitem", { name: "What's new" }));

    const dialog = await screen.findByRole("dialog", { name: "What's new" });
    expect(within(dialog).getByText("Version 0.2.0")).toBeTruthy();
    expect(within(dialog).getByText("Version 0.1.0")).toBeTruthy();
    expect(within(dialog).getByText("Added batch trend summary.")).toBeTruthy();
    expect(within(dialog).getByText("Launched My Work queue.")).toBeTruthy();
    expect(within(dialog).queryByText("QA-only audit note.")).toBeNull();

    await user.click(within(dialog).getByRole("button", { name: "Read guide: Added batch trend summary." }));

    await waitFor(() => expect(screen.queryByRole("dialog", { name: "What's new" })).toBeNull());
    const panel = await screen.findByRole("region", { name: "Help" });
    expect(await within(panel).findByRole("heading", { name: "34. Chapter 34-reports-kpis" })).toBeTruthy();
    expect(helpServices.loadChapter).toHaveBeenCalledWith("34-reports-kpis");
  });

  it("uses span typography inside DialogTitle (no nested heading) and returns focus to the opener on close", async () => {
    const user = userEvent.setup();

    function OpenerHost() {
      const [open, setOpen] = useState(false);
      return (
        <PinnedLightTheme>
          <MemoryRouter>
            <HelpPanelProvider>
              <button type="button" onClick={() => setOpen(true)}>
                Open release notes
              </button>
              <WhatsNewDialog open={open} onClose={() => setOpen(false)} role="Analyst" />
            </HelpPanelProvider>
          </MemoryRouter>
        </PinnedLightTheme>
      );
    }

    render(<OpenerHost />);
    const opener = screen.getByRole("button", { name: "Open release notes" });
    await user.click(opener);

    const dialog = await screen.findByRole("dialog", { name: "What's new" });
    const titleSpan = within(dialog).getByText("What's new");
    expect(titleSpan.tagName).toBe("SPAN");
    expect(dialog.querySelectorAll("h1 h1, h1 h2, h1 h3, h2 h1, h2 h2, h2 h3, h2 h4, h2 h5, h2 h6")).toHaveLength(0);

    await user.click(within(dialog).getByRole("button", { name: "Close" }));
    await waitFor(() => expect(screen.queryByRole("dialog", { name: "What's new" })).toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(opener));
  });
});
