import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Link, MemoryRouter, Route, Routes } from "react-router-dom";
import { ThemeModeProvider } from "../theme/ThemeModeContext";
import { AuthProvider } from "../contexts/AuthContext";
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

    const banner = await screen.findByRole("status");
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
