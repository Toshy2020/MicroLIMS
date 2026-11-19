import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { ThemeProvider, type Theme } from "@mui/material/styles";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { BenchModeProvider } from "../../../theme/BenchModeContext";
import { ThemeModeProvider } from "../../../theme/ThemeModeContext";
import { AuthProvider } from "../../../contexts/AuthContext";
import { MainLayout } from "../../../layouts/MainLayout";
import { MyWorkPage } from "../../myWork/MyWorkPage";
import { resetUiStateCache } from "../../../hooks/useUiState";
import { PERMISSIONS } from "../../../routes/routes";
import { Tour } from "./Tour";
import { TOUR_STEPS, tourStateKey, type RoleType, type TourStep } from "./tourSteps";
import { WHATS_NEW_STATE_KEY } from "../whatsNew/releaseNotes";

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

function renderAppLayout(options: SeedOptions = {}, initialPath = "/my-work") {
  seedSession(options);
  return render(
    <BenchModeProvider>
      <ThemeModeProvider>
        <PinnedLightTheme>
          <AuthProvider>
            <MemoryRouter initialEntries={[initialPath]}>
              <Routes>
                <Route element={<MainLayout />}>
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

const SAMPLE_STEPS: TourStep[] = [
  { target: "alpha", title: "Alpha area", body: "Overview of the alpha area." },
  { target: "beta", title: "Beta area", body: "Overview of the beta area." },
  { target: "gamma", title: "Gamma area", body: "Overview of the gamma area." }
];

function renderStandaloneTour(steps: TourStep[], onFinish = vi.fn()) {
  return {
    onFinish,
    ...render(
      <PinnedLightTheme>
        <MemoryRouter>
          <main id="main-content" tabIndex={-1}>
            <button type="button" data-tour="alpha">Alpha target</button>
            <button type="button" data-tour="beta">Beta target</button>
            <button type="button" data-tour="gamma">Gamma target</button>
          </main>
          <Tour steps={steps} onFinish={onFinish} />
        </MemoryRouter>
      </PinnedLightTheme>
    )
  };
}

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  resetUiStateCache();
  uiStateMock.getAll.mockResolvedValue({});
  uiStateMock.set.mockResolvedValue(undefined);
  uiStateMock.remove.mockResolvedValue(undefined);
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
    groups: [
      {
        key: "assigned",
        title: "Assigned to you",
        count: 1,
        lockedCount: 0,
        hasMore: false,
        items: [
          {
            key: "test-1",
            groupKey: "assigned",
            identifier: "SMP-001",
            title: "TAMC",
            context: "Product A",
            urgency: "Now",
            dueAtUtc: undefined,
            locked: false,
            lockReason: undefined,
            actions: [],
            target: { type: "test-step", sampleId: 1, testOrderId: 10 }
          }
        ]
      }
    ]
  });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  vi.unstubAllGlobals();
  localStorage.clear();
  sessionStorage.clear();
  resetUiStateCache();
});

describe("First-login tour", () => {
  it("starts for a user without the key; not for one with it; not while password change is pending", async () => {
    // 1. Starts when the key is absent
    uiStateMock.getAll.mockResolvedValueOnce({});
    const first = renderAppLayout({ role: "Analyst", mustChangePassword: false });

    const firstStepTitle = TOUR_STEPS.Analyst[0]?.title ?? "";
    expect(firstStepTitle.length).toBeGreaterThan(0);
    const dialog = await screen.findByRole("dialog", { name: firstStepTitle });
    expect(dialog.getAttribute("aria-modal")).toBe("false");
    expect(within(dialog).getByText(`Step 1 of ${TOUR_STEPS.Analyst.length}`)).toBeTruthy();
    first.unmount();
    resetUiStateCache();

    // 2. Does NOT start when the key is already present
    uiStateMock.getAll.mockResolvedValueOnce({
      [tourStateKey("Analyst")]: "done",
      [WHATS_NEW_STATE_KEY]: __APP_VERSION__
    });
    const second = renderAppLayout({ role: "Analyst", mustChangePassword: false });
    await waitFor(() => expect(uiStateMock.getAll).toHaveBeenCalledTimes(2));
    expect(screen.queryByRole("dialog", { name: firstStepTitle })).toBeNull();
    second.unmount();
    resetUiStateCache();

    // 3. Does NOT start while password change is pending
    uiStateMock.getAll.mockResolvedValueOnce({});
    const third = renderAppLayout({ role: "Analyst", mustChangePassword: true });
    expect(await screen.findByText("Change Password Page")).toBeTruthy();
    // A tour that never ran must not leave a stored "skipped" behind.
    await new Promise((r) => setTimeout(r, 50));
    expect(uiStateMock.set).not.toHaveBeenCalled();
    expect(screen.queryByRole("dialog", { name: firstStepTitle })).toBeNull();
    third.unmount();
    resetUiStateCache();

    // 4. Does NOT start while forced MFA enrolment is pending
    uiStateMock.getAll.mockResolvedValueOnce({});
    mfaMock.getStatus.mockResolvedValueOnce({
      enrolled: false,
      type: null,
      confirmedAt: null,
      recoveryCodesRemaining: 0,
      required: true,
      enrolmentDueAt: null,
      trustedDevices: []
    });
    const fourth = renderAppLayout({ role: "Analyst", mustChangePassword: false });
    await waitFor(() => expect(mfaMock.getStatus).toHaveBeenCalled());
    await waitFor(() => expect(screen.queryByRole("dialog", { name: firstStepTitle })).toBeNull());
    fourth.unmount();
  });

  it("Next/Back/Skip work by keyboard; focus moves into each step and returns to the page at the end", async () => {
    const user = userEvent.setup();
    const onFinish = vi.fn();

    // Render with focus starting on Alpha target before the tour mounts
    const { rerender } = render(
      <PinnedLightTheme>
        <MemoryRouter>
          <main id="main-content" tabIndex={-1}>
            <button type="button" data-tour="alpha">Alpha target</button>
            <button type="button" data-tour="beta">Beta target</button>
            <button type="button" data-tour="gamma">Gamma target</button>
          </main>
        </MemoryRouter>
      </PinnedLightTheme>
    );
    const originButton = screen.getByRole("button", { name: "Alpha target" });
    originButton.focus();
    expect(document.activeElement).toBe(originButton);

    rerender(
      <PinnedLightTheme>
        <MemoryRouter>
          <main id="main-content" tabIndex={-1}>
            <button type="button" data-tour="alpha">Alpha target</button>
            <button type="button" data-tour="beta">Beta target</button>
            <button type="button" data-tour="gamma">Gamma target</button>
          </main>
          <Tour steps={SAMPLE_STEPS} onFinish={onFinish} />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    // Step 1 of 3
    const step1 = await screen.findByRole("dialog", { name: "Alpha area" });
    expect(within(step1).getByText("Step 1 of 3")).toBeTruthy();
    expect(originButton.getAttribute("data-tour-active")).toBe("true");
    expect(originButton.style.outline).not.toBe("");
    await waitFor(() => expect(step1.contains(document.activeElement)).toBe(true));

    // Focus is trapped inside the dialog on Tab
    await user.tab();
    expect(step1.contains(document.activeElement)).toBe(true);
    await user.tab();
    expect(step1.contains(document.activeElement)).toBe(true);

    // Activate Next by keyboard
    within(step1).getByRole("button", { name: "Next" }).focus();
    await user.keyboard("{Enter}");

    // Step 2 of 3: focus moves into Step 2
    const step2 = await screen.findByRole("dialog", { name: "Beta area" });
    expect(within(step2).getByText("Step 2 of 3")).toBeTruthy();
    await waitFor(() => expect(step2.contains(document.activeElement)).toBe(true));
    expect(screen.getByRole("button", { name: "Beta target" }).getAttribute("data-tour-active")).toBe("true");
    expect(originButton.getAttribute("data-tour-active")).toBeNull();

    // Activate Back by keyboard
    within(step2).getByRole("button", { name: "Back" }).focus();
    await user.keyboard("{Enter}");

    const step1Again = await screen.findByRole("dialog", { name: "Alpha area" });
    expect(within(step1Again).getByText("Step 1 of 3")).toBeTruthy();
    await waitFor(() => expect(step1Again.contains(document.activeElement)).toBe(true));

    // Advance by keyboard to Step 2 and Step 3, then Finish by keyboard
    within(step1Again).getByRole("button", { name: "Next" }).focus();
    await user.keyboard("{Enter}");
    const step2Again = await screen.findByRole("dialog", { name: "Beta area" });
    within(step2Again).getByRole("button", { name: "Next" }).focus();
    await user.keyboard("{Enter}");

    const step3 = await screen.findByRole("dialog", { name: "Gamma area" });
    expect(within(step3).getByText("Step 3 of 3")).toBeTruthy();
    await waitFor(() => expect(step3.contains(document.activeElement)).toBe(true));

    within(step3).getByRole("button", { name: "Finish" }).focus();
    await user.keyboard("{Enter}");

    expect(onFinish).toHaveBeenCalledWith("done");
    expect(document.activeElement).toBe(originButton);
  });

  it("finishing stores \"done\", skipping stores \"skipped\"", async () => {
    const user = userEvent.setup();

    // 1. Finishing stores "done"
    uiStateMock.getAll.mockResolvedValueOnce({});
    const first = renderAppLayout({ role: "Analyst" });

    const steps = TOUR_STEPS.Analyst;
    for (let i = 0; i < steps.length - 1; i += 1) {
      const dialog = await screen.findByRole("dialog", { name: steps[i].title });
      expect(within(dialog).getByText(`Step ${i + 1} of ${steps.length}`)).toBeTruthy();
      await user.click(within(dialog).getByRole("button", { name: "Next" }));
    }

    const lastDialog = await screen.findByRole("dialog", { name: steps[steps.length - 1].title });
    await user.click(within(lastDialog).getByRole("button", { name: "Finish" }));

    await waitFor(() => expect(uiStateMock.set).toHaveBeenCalledWith(tourStateKey("Analyst"), "done"));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    first.unmount();
    resetUiStateCache();
    uiStateMock.set.mockClear();

    // 2. Skipping stores "skipped"
    uiStateMock.getAll.mockResolvedValueOnce({});
    const second = renderAppLayout({ role: "Analyst" });

    const firstDialog = await screen.findByRole("dialog", { name: steps[0].title });
    await user.click(within(firstDialog).getByRole("button", { name: "Skip" }));

    await waitFor(() => expect(uiStateMock.set).toHaveBeenCalledWith(tourStateKey("Analyst"), "skipped"));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    second.unmount();
  });

  it("Replay from Help starts it again without clearing the stored value until it ends", async () => {
    const user = userEvent.setup();
    uiStateMock.getAll.mockResolvedValueOnce({
      [tourStateKey("Analyst")]: "done",
      [WHATS_NEW_STATE_KEY]: __APP_VERSION__
    });

    renderAppLayout({ role: "Analyst" });
    await waitFor(() => expect(uiStateMock.getAll).toHaveBeenCalledTimes(1));
    expect(screen.queryByRole("dialog")).toBeNull();

    // Open Help menu and click Replay tour
    await user.click(screen.getByRole("button", { name: "Help" }));
    await user.click(await screen.findByRole("menuitem", { name: "Replay tour" }));

    const steps = TOUR_STEPS.Analyst;
    const dialog = await screen.findByRole("dialog", { name: steps[0].title });
    expect(within(dialog).getByText(`Step 1 of ${steps.length}`)).toBeTruthy();

    // Stored value was NOT cleared or overwritten when starting the replay
    expect(uiStateMock.remove).not.toHaveBeenCalled();
    expect(uiStateMock.set).not.toHaveBeenCalled();

    // Skip the replayed tour -> now stores "skipped" and returns focus to Help button
    await user.click(within(dialog).getByRole("button", { name: "Skip" }));
    await waitFor(() => expect(uiStateMock.set).toHaveBeenCalledWith(tourStateKey("Analyst"), "skipped"));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Help" }));
  });

  it("missing target is skipped; all missing -> ends, nothing rendered", async () => {
    const user = userEvent.setup();

    // 1. One missing target and one hidden target are skipped
    const onFinishPartial = vi.fn();
    const partialSteps: TourStep[] = [
      { target: "alpha", title: "Step Alpha", body: "Alpha body" },
      { target: "missing-one", title: "Step Missing", body: "Missing body" },
      { target: "hidden-one", title: "Step Hidden", body: "Hidden body" },
      { target: "gamma", title: "Step Gamma", body: "Gamma body" }
    ];

    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <main id="main-content" tabIndex={-1}>
            <button type="button" data-tour="alpha">Alpha target</button>
            <button type="button" data-tour="hidden-one" style={{ display: "none" }}>Hidden target</button>
            <button type="button" data-tour="gamma">Gamma target</button>
          </main>
          <Tour steps={partialSteps} onFinish={onFinishPartial} />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const dialog1 = await screen.findByRole("dialog", { name: "Step Alpha" });
    expect(within(dialog1).getByText("Step 1 of 2")).toBeTruthy();
    await user.click(within(dialog1).getByRole("button", { name: "Next" }));

    const dialog2 = await screen.findByRole("dialog", { name: "Step Gamma" });
    expect(within(dialog2).getByText("Step 2 of 2")).toBeTruthy();
    await user.click(within(dialog2).getByRole("button", { name: "Finish" }));
    expect(onFinishPartial).toHaveBeenCalledWith("done");

    cleanup();

    // 2. All targets missing -> ends silently as "skipped", nothing rendered
    const onFinishNone = vi.fn();
    const missingSteps: TourStep[] = [
      { target: "does-not-exist-1", title: "Ghost 1", body: "Body 1" },
      { target: "does-not-exist-2", title: "Ghost 2", body: "Body 2" }
    ];

    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <main id="main-content" tabIndex={-1}>
            <button type="button">Plain button</button>
          </main>
          <Tour steps={missingSteps} onFinish={onFinishNone} />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    expect(screen.queryByRole("dialog")).toBeNull();
    await waitFor(() => expect(onFinishNone).toHaveBeenCalledWith("skipped"));
  });

  it("each role has 5 or 6 steps and every target exists in the app for that role", async () => {
    const roles: RoleType[] = [
      "Analyst",
      "Reviewer",
      "SectionHead",
      "QcManager",
      "Qa",
      "SystemAdministrator"
    ];

    for (const role of roles) {
      const steps = TOUR_STEPS[role];
      expect(steps.length).toBeGreaterThanOrEqual(5);
      expect(steps.length).toBeLessThanOrEqual(6);

      for (const step of steps) {
        expect(step.title.trim().length).toBeGreaterThan(0);
        expect(step.body.trim().length).toBeGreaterThan(0);
        expect(step.title).not.toMatch(/\d+\s*(cfu|%|°C|hours?|days?)/i);
        expect(step.body).not.toMatch(/\d+\s*(cfu|%|°C|hours?|days?)/i);
      }

      uiStateMock.getAll.mockResolvedValueOnce({
        [tourStateKey(role)]: "done",
        [WHATS_NEW_STATE_KEY]: __APP_VERSION__
      });
      const view = renderAppLayout({ role });

      await screen.findByRole("heading", { name: "My Work" });

      for (const step of steps) {
        const el = view.container.querySelector<HTMLElement>(`[data-tour="${step.target}"]`);
        expect(el, `Expected target [data-tour="${step.target}"] to exist for role ${role}`).not.toBeNull();
      }

      view.unmount();
      resetUiStateCache();
    }
  });

  it("Escape skips and returns focus to the page", async () => {
    const user = userEvent.setup();
    const { onFinish } = renderStandaloneTour(SAMPLE_STEPS);

    const dialog = await screen.findByRole("dialog", { name: "Alpha area" });
    expect(dialog).toBeTruthy();

    await user.keyboard("{Escape}");

    expect(onFinish).toHaveBeenCalledWith("skipped");
    const main = document.getElementById("main-content");
    expect(document.activeElement === main || main?.contains(document.activeElement)).toBe(true);
  });

  it("respects prefers-reduced-motion when scrolling to a target", async () => {
    const scrollIntoView = vi.fn();
    Object.defineProperty(HTMLElement.prototype, "scrollIntoView", { configurable: true, writable: true, value: scrollIntoView });
    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: query.includes("prefers-reduced-motion"),
      media: query,
      onchange: null,
      addEventListener: () => {},
      removeEventListener: () => {},
      addListener: () => {},
      removeListener: () => {},
      dispatchEvent: () => false
    }));
    try {
      renderStandaloneTour(SAMPLE_STEPS);
      await screen.findByRole("dialog", { name: "Alpha area" });
      expect(scrollIntoView).toHaveBeenCalledWith({ block: "nearest", behavior: "auto" });
    } finally {
      Reflect.deleteProperty(HTMLElement.prototype, "scrollIntoView");
    }
  });

  it("uses bottom-end placement in RTL so the popover stays on screen", async () => {
    const onFinish = vi.fn();
    render(
      <PinnedLightTheme>
        <ThemeProvider theme={(outer: Theme): Theme => ({ ...outer, direction: "rtl" })}>
          <MemoryRouter>
            <main id="main-content" tabIndex={-1}>
              <button type="button" data-tour="alpha">Alpha target</button>
            </main>
            <Tour steps={SAMPLE_STEPS} onFinish={onFinish} />
          </MemoryRouter>
        </ThemeProvider>
      </PinnedLightTheme>
    );

    const dialog = await screen.findByRole("dialog", { name: "Alpha area" });
    const popper = dialog.closest("[data-popper-placement]");
    await waitFor(() => {
      expect(popper?.getAttribute("data-popper-placement")).toBe("bottom-end");
    });
  });
});
