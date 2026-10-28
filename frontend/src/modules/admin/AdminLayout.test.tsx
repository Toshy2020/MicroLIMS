import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AdminIndexRedirect, AdminLayout } from "./AdminLayout";

const auth = { permissions: [] as string[] };
vi.mock("../../contexts/AuthContext", () => ({ useAuth: () => auth }));

afterEach(() => cleanup());

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
    expect(screen.getAllByRole("tab").map((t) => t.textContent)).toEqual(["Users", "Settings", "Integrations"]);
    expect(screen.getByRole("tab", { name: "Users" }).getAttribute("aria-selected")).toBe("true");
  });

  it("shows Organization to Documents.ConfigManage holders only", () => {
    auth.permissions = ["Documents.ConfigManage"];
    renderAt("/admin/organization");
    expect(screen.getAllByRole("tab").map((t) => t.textContent)).toEqual(["Organization"]);

    cleanup();
    auth.permissions = ["Users.Manage", "Documents.Reassign"]; // the Section Head's code does not open the admin tab
    renderAt("/admin/users");
    expect(screen.getAllByRole("tab").map((t) => t.textContent)).toEqual(["Users"]);
  });

  it("/admin lands on the first permitted tab", () => {
    auth.permissions = ["Audit.View"];
    renderAt("/admin");
    expect(screen.getByText("audit page")).toBeTruthy();
  });

  it("/admin sends a user with no admin permission to the dashboard", () => {
    auth.permissions = [];
    renderAt("/admin");
    expect(screen.getByText("dashboard")).toBeTruthy();
  });
});
