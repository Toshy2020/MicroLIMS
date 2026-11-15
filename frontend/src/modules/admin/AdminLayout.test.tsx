import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { resetUiStateCache } from "../../hooks/useUiState";
import { uiStateService } from "../../services/uiStateService";
import { AdminIndexRedirect, AdminLayout } from "./AdminLayout";

const auth = { permissions: [] as string[] };
vi.mock("../../services/uiStateService", () => ({
  uiStateService: { getAll: vi.fn(), set: vi.fn(), remove: vi.fn() }
}));
vi.mock("../../contexts/AuthContext", () => ({ useAuth: () => auth }));

beforeEach(() => {
  resetUiStateCache();
  vi.mocked(uiStateService.getAll).mockResolvedValue({});
  vi.mocked(uiStateService.set).mockResolvedValue(undefined);
});

afterEach(() => {
  cleanup();
  resetUiStateCache();
  vi.clearAllMocks();
});

function renderAt(url: string) {
  render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route path="/admin" element={<AdminLayout />}>
            <Route index element={<AdminIndexRedirect />} />
            <Route path="users" element={<div>users page</div>} />
            <Route path="audit" element={<div>audit page</div>} />
            <Route path="organization" element={<div>organization page</div>} />
          </Route>
          <Route path="/dashboard" element={<div>dashboard</div>} />
        </Routes>
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

describe("AdminLayout", () => {
  it("shows only the tabs the user holds the permission for", () => {
    auth.permissions = ["Audit.View", "System.ViewErrorLog"];
    renderAt("/admin/audit");
    expect(screen.getAllByRole("tab").map((t) => t.textContent)).toEqual(["Audit Trail", "Error Log", "Overview"]);
    expect(screen.getByText("audit page")).toBeTruthy();
  });

  it("shows Settings to System.ConfigureSettings and marks the current tab selected", () => {
    auth.permissions = ["Users.Manage", "System.ConfigureSettings"];
    renderAt("/admin/users");
    expect(screen.getAllByRole("tab").map((t) => t.textContent)).toEqual(["Users", "Company sign-in", "Settings", "Integrations"]);
    expect(screen.getByRole("tab", { name: "Users" }).getAttribute("aria-selected")).toBe("true");
  });

  it("shows Organization to Documents.ConfigManage holders only", () => {
    auth.permissions = ["Documents.ConfigManage"];
    renderAt("/admin/organization");
    expect(screen.getAllByRole("tab").map((t) => t.textContent)).toEqual(["Organization"]);

    cleanup();
    auth.permissions = ["Users.Manage", "Documents.Reassign"]; // the Section Head's code does not open the admin tab
    renderAt("/admin/users");
    expect(screen.getAllByRole("tab").map((t) => t.textContent)).toEqual(["Users", "Company sign-in"]);
  });

  it("/admin lands on the first permitted tab", async () => {
    auth.permissions = ["Audit.View"];
    renderAt("/admin");
    expect(await screen.findByText("audit page")).toBeTruthy();
  });

  it("/admin lands on the tab used last time when the user may still open it", async () => {
    auth.permissions = ["Users.Manage", "Audit.View"];
    vi.mocked(uiStateService.getAll).mockResolvedValue({ "nav.tab.admin": "/admin/audit" });
    renderAt("/admin");
    expect(await screen.findByText("audit page")).toBeTruthy();
  });

  it("/admin ignores a remembered tab the user no longer has permission for", async () => {
    auth.permissions = ["Users.Manage"];
    vi.mocked(uiStateService.getAll).mockResolvedValue({ "nav.tab.admin": "/admin/audit" });
    renderAt("/admin");
    expect(await screen.findByText("users page")).toBeTruthy();
  });

  it("switching tabs works and remembers the tab", async () => {
    const user = userEvent.setup();
    auth.permissions = ["Users.Manage", "Audit.View"];
    renderAt("/admin/users");
    await user.click(screen.getByRole("tab", { name: "Audit Trail" }));
    expect(screen.getByText("audit page")).toBeTruthy();
    await waitFor(() => expect(uiStateService.set).toHaveBeenCalledWith("nav.tab.admin", "/admin/audit"), { timeout: 2000 });
  });

  it("/admin sends a user with no admin permission to the dashboard", async () => {
    auth.permissions = [];
    renderAt("/admin");
    expect(await screen.findByText("dashboard")).toBeTruthy();
  });
});
