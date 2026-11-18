import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { getGroupedMenu } from "../routes/menuConfig";
import { myWorkService } from "../modules/myWork/services/myWorkService";
import { AuthProvider } from "../contexts/AuthContext";
import { LiveProvider } from "../contexts/LiveContext";
import { createFakeLiveConnection } from "../contexts/testing/fakeLiveConnection";
import { Sidebar } from "./Sidebar";

vi.mock("../modules/myWork/services/myWorkService", () => ({
  myWorkService: {
    get: vi.fn(),
    summary: vi.fn(),
    take: vi.fn(),
    release: vi.fn()
  }
}));

function renderSidebar(collapsed = false) {
  const groups = getGroupedMenu({
    role: "Analyst",
    permissions: [],
    labCodes: []
  });

  return {
    groups,
    ...render(
      <PinnedLightTheme>
        <MemoryRouter initialEntries={["/my-work"]}>
          <Sidebar
            mobileOpen={false}
            onMobileClose={() => {}}
            collapsed={collapsed}
            onToggleCollapse={() => {}}
            groups={groups}
            activeTrail={null}
          />
        </MemoryRouter>
      </PinnedLightTheme>
    )
  };
}

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => {
  cleanup();
});

describe("Sidebar My Work badge", () => {
  it("shows My Work first with the summary count and accessible name", async () => {
    vi.mocked(myWorkService.summary).mockResolvedValue({
      total: 7,
      overdue: 2,
      now: 1,
      complete: true
    });

    const { groups } = renderSidebar(false);

    expect(groups[0]?.items[0]?.label).toBe("My Work");
    expect(groups[0]?.items[0]?.badge).toBe("myWork");

    const links = screen.getAllByRole("link");
    expect(links[0]?.getAttribute("href")).toBe("/my-work");

    await waitFor(() => {
      expect(screen.getByRole("link", { name: "My Work, 7 tasks, 2 overdue" })).toBeTruthy();
    });
    expect(screen.getByText("7")).toBeTruthy();
  });

  it("hides the badge when summary.complete is false", async () => {
    vi.mocked(myWorkService.summary).mockResolvedValue({
      total: 7,
      overdue: 2,
      now: 1,
      complete: false
    });

    renderSidebar(false);

    await waitFor(() => {
      expect(myWorkService.summary).toHaveBeenCalledTimes(1);
    });
    expect(screen.getByRole("link", { name: "My Work" })).toBeTruthy();
    expect(screen.queryByTestId("my-work-badge")).toBeNull();
  });

  it("exposes the count to screen readers when the sidebar is collapsed", async () => {
    vi.mocked(myWorkService.summary).mockResolvedValue({
      total: 7,
      overdue: 2,
      now: 1,
      complete: true
    });

    renderSidebar(true);

    await waitFor(() => {
      expect(screen.getByRole("link", { name: "My Work, 7 tasks, 2 overdue" })).toBeTruthy();
    });
  });

  it("refreshes the summary on window focus", async () => {
    vi.mocked(myWorkService.summary)
      .mockResolvedValueOnce({ total: 3, overdue: 0, now: 1, complete: true })
      .mockResolvedValueOnce({ total: 7, overdue: 2, now: 1, complete: true });

    renderSidebar(false);

    await waitFor(() => {
      expect(screen.getByRole("link", { name: "My Work, 3 tasks" })).toBeTruthy();
    });

    window.dispatchEvent(new Event("focus"));

    await waitFor(() => {
      expect(screen.getByRole("link", { name: "My Work, 7 tasks, 2 overdue" })).toBeTruthy();
    });
  });

  it("refreshes the summary when the live my-work signal arrives", async () => {
    vi.mocked(myWorkService.summary)
      .mockResolvedValueOnce({ total: 3, overdue: 0, now: 1, complete: true })
      .mockResolvedValue({ total: 9, overdue: 1, now: 1, complete: true });
    const connection = createFakeLiveConnection();
    localStorage.setItem("microlims_token", "h.e30=.s");
    const groups = getGroupedMenu({ role: "Analyst", permissions: [], labCodes: [] });

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <LiveProvider connection={connection}>
            <MemoryRouter initialEntries={["/my-work"]}>
              <Sidebar mobileOpen={false} onMobileClose={() => {}} collapsed={false} onToggleCollapse={() => {}} groups={groups} activeTrail={null} />
            </MemoryRouter>
          </LiveProvider>
        </AuthProvider>
      </PinnedLightTheme>
    );

    await waitFor(() => {
      expect(screen.getByRole("link", { name: "My Work, 3 tasks" })).toBeTruthy();
    });
    // The live connection must be up before a signal can be delivered.
    await waitFor(() => expect(connection.start).toHaveBeenCalled());
    await act(async () => {
      connection.emitSignal({ kind: "mywork", key: "user:1" });
    });
    await waitFor(() => {
      expect(screen.getByRole("link", { name: "My Work, 9 tasks, 1 overdue" })).toBeTruthy();
    });
    localStorage.clear();
  });
});
