import { afterEach, beforeEach, describe, it, expect, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { resetUiStateCache } from "../../hooks/useUiState";
import { DashboardPage } from "./DashboardPage";

const auth = vi.hoisted(() => ({ role: "SectionHead", permissions: [] as string[] }));
vi.mock("../../contexts/AuthContext", () => ({
  useAuth: () => auth,
  useHasPermission: (code: string | string[]) => [code].flat().some((c) => auth.permissions.includes(c)),
}));
const labs = vi.hoisted(() => ({ codes: [] as string[] }));
vi.mock("../../hooks/useMyLabs", () => ({ useMyLabs: () => ({ codes: labs.codes, physchemAreas: [], loading: false }) }));
const ui = vi.hoisted(() => ({ getAll: vi.fn(), set: vi.fn(), remove: vi.fn() }));
vi.mock("../../services/uiStateService", () => ({ uiStateService: ui }));
vi.mock("./AnalystDashboardPage", () => ({ AnalystDashboardPage: () => null }));
vi.mock("./ReviewerDashboardPage", () => ({ ReviewerDashboardPage: () => null }));
vi.mock("./AdminDashboardPage", () => ({ AdminDashboardPage: () => null }));
vi.mock("./SectionHeadDashboardPage", () => ({ SectionHeadDashboardPage: () => <div>Section head dashboard</div> }));

function renderDashboard(role: string, permissions: string[], url = "/dashboard") {
  auth.role = role;
  auth.permissions = permissions;
  render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/laboratory-configuration/items" element={<div>Items page</div>} />
      </Routes>
    </MemoryRouter>
  );
}

beforeEach(() => {
  labs.codes = [];
  localStorage.clear();
  resetUiStateCache();
  ui.getAll.mockResolvedValue({});
  ui.set.mockResolvedValue(undefined);
});

afterEach(() => {
  cleanup();
  resetUiStateCache();
});

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

describe("DashboardPage laboratory tabs", () => {
  const selected = () => screen.getByRole("tab", { selected: true }).textContent;

  it("opens the laboratory named in ?lab= and switches between laboratories", async () => {
    const user = userEvent.setup();
    labs.codes = ["MICRO", "FP"];
    renderDashboard("SectionHead", [], "/dashboard?lab=FP");

    expect(selected()).toBe("Physicochemical Laboratory");
    await user.click(screen.getByRole("tab", { name: "Microbiology Laboratory" }));
    expect(selected()).toBe("Microbiology Laboratory");
    await waitFor(() => expect(ui.set).toHaveBeenCalledWith("nav.lastLab", "MICRO"), { timeout: 2000 });
  });

  it("opens the last laboratory when the address names none", async () => {
    labs.codes = ["MICRO", "FP"];
    ui.getAll.mockResolvedValue({ "nav.lastLab": "FP" });
    renderDashboard("SectionHead", []);

    await waitFor(() => expect(selected()).toBe("Physicochemical Laboratory"));
  });
});
