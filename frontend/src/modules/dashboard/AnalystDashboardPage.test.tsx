import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { apiClient } from "../../services/apiClient";
import { DashboardLabProvider, makeDashboardLab } from "./DashboardLabContext";
import { DashboardService } from "./services/DashboardService";
import { myWorkService } from "../myWork/services/myWorkService";
import { AnalystDashboardPage } from "./AnalystDashboardPage";

vi.mock("../../services/apiClient", () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn()
  }
}));

vi.mock("./services/DashboardService", () => ({
  DashboardService: {
    getIncubationOverview: vi.fn(),
    getAnalystMetrics: vi.fn()
  }
}));

vi.mock("../myWork/services/myWorkService", () => ({
  myWorkService: {
    get: vi.fn(),
    summary: vi.fn(),
    take: vi.fn(),
    release: vi.fn()
  }
}));

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.setItem("microlims_token", "tok");
  localStorage.setItem("microlims_username", "analyst1");
  localStorage.setItem("microlims_full_name", "A. Analyst");
  localStorage.setItem("microlims_role", "Analyst");
  vi.mocked(apiClient.get).mockResolvedValue({
    data: {
      data: {
        readyToReadCount: 4,
        pendingTests: 5,
        incubatingCount: 2,
        completedToday: 3
      }
    }
  });
  vi.mocked(DashboardService.getIncubationOverview).mockResolvedValue([]);
  vi.mocked(DashboardService.getAnalystMetrics).mockResolvedValue({
    testsCompletedToday: 3,
    mediaLotsPreparedToday: 1,
    activeAssignedOrders: 5,
    onTimeReadingRate: 95,
    trailing7DayVolume: 12
  });
  vi.mocked(myWorkService.summary).mockResolvedValue({
    total: 7,
    overdue: 2,
    now: 3,
    complete: true
  });
});

afterEach(() => {
  cleanup();
  localStorage.clear();
});

describe("AnalystDashboardPage", () => {
  it("shows Open My Work (n) and keeps its KPI panels", async () => {
    render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter initialEntries={["/dashboard"]}>
            <DashboardLabProvider value={makeDashboardLab("MICRO", [])}>
              <AnalystDashboardPage />
            </DashboardLabProvider>
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );

    await waitFor(() => {
      const link = screen.getByRole("link", { name: "Open My Work (7)" });
      expect(link.getAttribute("href")).toBe("/my-work");
    });

    // Keeps KPI panels
    expect(screen.getByText("Overdue")).toBeTruthy();
    expect(screen.getByText("Due now")).toBeTruthy();
    expect(screen.getByText("Ready to read")).toBeTruthy();
    expect(screen.getByText("Completed Today")).toBeTruthy();
    expect(screen.getByText("My Operational Performance")).toBeTruthy();
    expect(screen.getByText("Incubation & Observation")).toBeTruthy();

    // Legacy work/attention panels are gone
    expect(screen.queryByText("Today's Laboratory Work")).toBeNull();
    expect(screen.queryByText("Attention Required")).toBeNull();
  });
});
