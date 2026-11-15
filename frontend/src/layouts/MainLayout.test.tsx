import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Link, MemoryRouter, Route, Routes } from "react-router-dom";
import { ThemeModeProvider } from "../theme/ThemeModeContext";
import { BenchModeProvider } from "../theme/BenchModeContext";
import { AuthProvider } from "../contexts/AuthContext";
import { notifyNetworkFailure } from "../services/connectionSignal";
import { draftStore } from "../services/drafts/draftStore";
import { makeDraft, resetDraftDb } from "../services/drafts/draftTestUtils";
import { OFFLINE_MESSAGE } from "../components/bench/ConnectionBanner";
import { mfaService } from "../modules/authentication/services/mfaService";
import { laboratorySectionService } from "../services/laboratorySectionService";
import { MainLayout } from "./MainLayout";

vi.mock("../services/apiClient", () => ({
  apiClient: {
    get: vi.fn((url: string) =>
      Promise.resolve({ data: { data: url.includes("/notifications") ? [] : { unreadCount: 0 } } })
    ),
    post: vi.fn().mockResolvedValue({})
  }
}));

vi.mock("../services/laboratorySectionService", () => ({
  laboratorySectionService: {
    getMySections: vi.fn().mockResolvedValue([])
  }
}));

vi.mock("../modules/authentication/services/mfaService", () => ({
  mfaService: {
    getStatus: vi.fn()
  }
}));

function seedSession() {
  const payload = btoa(
    JSON.stringify({
      "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "SectionHead",
      permission: "Samples.View"
    })
  );
  localStorage.setItem("microlims_token", `h.${payload}.s`);
  localStorage.setItem("microlims_username", "sec.head");
  localStorage.setItem("microlims_full_name", "Section Head");
}

function renderLayout() {
  seedSession();
  return render(
    <BenchModeProvider>
    <ThemeModeProvider>
      <AuthProvider>
        <MemoryRouter initialEntries={["/dashboard"]}>
          <Routes>
            <Route element={<MainLayout />}>
              <Route
                path="/dashboard"
                element={
                  <div>
                    Dashboard Outlet <Link to="/reports">Go Reports</Link>
                  </div>
                }
              />
              <Route path="/reports" element={<div>Reports Outlet</div>} />
            </Route>
            <Route path="/mfa-enrol" element={<div>MFA Enrolment Target</div>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </ThemeModeProvider>
    </BenchModeProvider>
  );
}

afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.clearAllMocks();
});

describe("MainLayout MFA grace banner", () => {
  it("grace banner shows the due date with time zone and links to enrolment", async () => {
    vi.mocked(mfaService.getStatus).mockResolvedValue({
      enrolled: false,
      type: null,
      confirmedAt: null,
      recoveryCodesRemaining: 0,
      required: true,
      enrolmentDueAt: "2026-11-20T14:30:00Z",
      trustedDevices: []
    });

    renderLayout();

    // The header also has a "Live updates paused" status while the hub is offline; pick the MFA banner.
    await waitFor(() =>
      expect(screen.getAllByRole("status").some((el) => /second factor/i.test(el.textContent ?? ""))).toBe(true)
    );
    const banner = screen.getAllByRole("status").find((el) => /second factor/i.test(el.textContent ?? ""));
    if (!banner) throw new Error("MFA grace banner not found");
    expect(banner.textContent).toMatch(/2026/);
    expect(banner.textContent).toMatch(/(UTC|GMT|[A-Z]{2,5})/);
    const link = screen.getByRole("link", { name: /Set up second factor/i });
    expect(link.getAttribute("href")).toBe("/mfa-enrol");
  });

  it("hides the grace banner when already enrolled and fetches status once per mount", async () => {
    const user = userEvent.setup();
    vi.mocked(mfaService.getStatus).mockResolvedValue({
      enrolled: true,
      type: "AuthenticatorApp",
      confirmedAt: "2026-11-10T10:00:00Z",
      recoveryCodesRemaining: 10,
      required: true,
      enrolmentDueAt: null,
      trustedDevices: []
    });

    renderLayout();
    await waitFor(() => expect(mfaService.getStatus).toHaveBeenCalledTimes(1));
    expect(screen.queryByRole("link", { name: /Set up second factor/i })).toBeNull();

    await user.click(screen.getByRole("link", { name: "Go Reports" }));
    expect(await screen.findByText("Reports Outlet")).toBeTruthy();
    expect(mfaService.getStatus).toHaveBeenCalledTimes(1);
    expect(laboratorySectionService.getMySections).toHaveBeenCalled();
  });

  it("shows nothing and keeps the layout working when status call fails", async () => {
    vi.mocked(mfaService.getStatus).mockRejectedValue(new Error("unavailable"));

    renderLayout();

    await waitFor(() => expect(mfaService.getStatus).toHaveBeenCalledTimes(1));
    expect(screen.getByText(/Dashboard Outlet/)).toBeTruthy();
    expect(screen.queryByRole("link", { name: /Set up second factor/i })).toBeNull();
  });
});

describe("MainLayout sidebar in bench mode", () => {
  it("starts expanded normally and closed (icon rail) in bench mode", async () => {
    vi.mocked(mfaService.getStatus).mockResolvedValue({
      enrolled: true, type: "AuthenticatorApp", confirmedAt: null,
      recoveryCodesRemaining: 0, required: false, enrolmentDueAt: null, trustedDevices: []
    });

    const normal = renderLayout();
    expect(screen.getAllByRole("button", { name: "Collapse sidebar" }).length).toBeGreaterThan(0);
    expect(screen.queryByRole("button", { name: "Expand sidebar" })).toBeNull();
    normal.unmount();

    localStorage.setItem("microlims_bench_mode", "on");
    renderLayout();
    expect(screen.getAllByRole("button", { name: "Expand sidebar" }).length).toBeGreaterThan(0);
    expect(screen.queryByRole("button", { name: "Collapse sidebar" })).toBeNull();
  });
});

describe("MainLayout bench drafts and connection", () => {
  const status = {
    enrolled: true, type: "AuthenticatorApp" as const, confirmedAt: null,
    recoveryCodesRemaining: 0, required: false, enrolmentDueAt: null, trustedDevices: []
  };

  it("shows the connection banner when a request fails with no answer from the server", async () => {
    vi.mocked(mfaService.getStatus).mockResolvedValue(status);
    renderLayout();
    expect(screen.queryByRole("alert")).toBeNull();

    act(() => notifyNetworkFailure());

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toBe(OFFLINE_MESSAGE);
  });

  it("Switch user warns with the real number of this user's unsaved entries and not those of another user", async () => {
    const user = userEvent.setup();
    vi.mocked(mfaService.getStatus).mockResolvedValue(status);
    await resetDraftDb();
    localStorage.setItem("microlims_bench_mode", "on");
    localStorage.setItem("microlims_user_id", "7");
    await draftStore.put(makeDraft({ userId: 7, recordKey: "a", values: {} }));
    await draftStore.put(makeDraft({ userId: 7, recordKey: "b", values: {} }));
    await draftStore.put(makeDraft({ userId: 8, recordKey: "c", values: {} }));
    renderLayout();

    await screen.findByRole("button", { name: "Unsaved entries on this tablet, 2" });
    await user.click(screen.getByRole("button", { name: "Switch user" }));

    expect(await screen.findByText(/You have 2 unsaved entries/)).toBeTruthy();
  });
});
