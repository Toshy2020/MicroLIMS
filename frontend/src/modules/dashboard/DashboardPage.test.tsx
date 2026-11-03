import { afterEach, describe, it, expect, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { DashboardPage } from "./DashboardPage";

const auth = vi.hoisted(() => ({ role: "SectionHead", permissions: [] as string[] }));
vi.mock("../../contexts/AuthContext", () => ({
  useAuth: () => auth,
  useHasPermission: (code: string | string[]) => [code].flat().some((c) => auth.permissions.includes(c)),
}));
vi.mock("../../hooks/useMyLabs", () => ({ useMyLabs: () => ({ codes: [], physchemAreas: [], loading: false }) }));
vi.mock("./AnalystDashboardPage", () => ({ AnalystDashboardPage: () => null }));
vi.mock("./ReviewerDashboardPage", () => ({ ReviewerDashboardPage: () => null }));
vi.mock("./AdminDashboardPage", () => ({ AdminDashboardPage: () => null }));
vi.mock("./SectionHeadDashboardPage", () => ({ SectionHeadDashboardPage: () => <div>Section head dashboard</div> }));

function renderDashboard(role: string, permissions: string[]) {
  auth.role = role;
  auth.permissions = permissions;
  render(
    <MemoryRouter initialEntries={["/dashboard"]}>
      <Routes>
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/laboratory-configuration/items" element={<div>Items page</div>} />
      </Routes>
    </MemoryRouter>
  );
}

afterEach(cleanup);

describe("DashboardPage", () => {
  it("sends a user who can give final approval but reads no dashboard to the Items page, whatever the role name", () => {
    renderDashboard("CustomRole", ["tests.not-applicable.final-approve"]);
    expect(screen.getByText("Items page")).toBeTruthy();
    expect(screen.queryByText("Section head dashboard")).toBeNull();
  });

  it("keeps the dashboard for a QC Manager role that lost the final-approve permission (no redirect loop)", () => {
    renderDashboard("QcManager", []);
    expect(screen.getByText("Section head dashboard")).toBeTruthy();
    expect(screen.queryByText("Items page")).toBeNull();
  });

  it("keeps the dashboard for a user who holds a dashboard permission as well", () => {
    renderDashboard("SectionHead", ["tests.not-applicable.final-approve", "Dashboards.LabOverview"]);
    expect(screen.getByText("Section head dashboard")).toBeTruthy();
  });
});
