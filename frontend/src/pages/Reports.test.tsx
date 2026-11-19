import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { resetUiStateCache } from "../hooks/useUiState";
import { uiStateService } from "../services/uiStateService";
import { ReportsPage } from "./Reports";

const state = vi.hoisted(() => ({ canViewKpi: true }));

vi.mock("../services/uiStateService", () => ({
  uiStateService: { getAll: vi.fn(), set: vi.fn(), remove: vi.fn() }
}));
vi.mock("../contexts/AuthContext", () => ({ useHasPermission: () => state.canViewKpi }));
vi.mock("../modules/reports/components/OverviewTab", async () => {
  const { createElement } = await import("react");
  // Mirrors the real prop: the KPI shortcut is only offered when canViewKpi is true (defaults to false).
  return {
    OverviewTab: ({ canViewKpi = false }: { canViewKpi?: boolean }) =>
      createElement("div", null, "overview tab", canViewKpi ? createElement("button", null, "KPI shortcut") : null)
  };
});
vi.mock("../modules/reports/RecordSearchTab", async () => {
  const { createElement } = await import("react");
  return { RecordSearchTab: () => createElement("div", null, "search tab") };
});
vi.mock("../modules/reports/MediaGptReportTab", async () => {
  const { createElement } = await import("react");
  return { MediaGptReportTab: () => createElement("div", null, "media tab") };
});
vi.mock("../modules/reports/ReferenceStrainReportTab", async () => {
  const { createElement } = await import("react");
  return { ReferenceStrainReportTab: () => createElement("div", null, "strain tab") };
});
vi.mock("../modules/reports/components/controlChart/ControlChartTab", async () => {
  const { createElement } = await import("react");
  return { ControlChartTab: () => createElement("div", null, "control chart tab"), presetControlChart: vi.fn() };
});
vi.mock("../modules/kpi/components/KpiPerformanceTab", async () => {
  const { createElement } = await import("react");
  return { KpiPerformanceTab: () => createElement("div", null, "kpi tab") };
});

beforeEach(() => {
  resetUiStateCache();
  vi.mocked(uiStateService.getAll).mockResolvedValue({});
  vi.mocked(uiStateService.set).mockResolvedValue(undefined);
});

afterEach(() => {
  cleanup();
  resetUiStateCache();
});

function renderAt(url: string) {
  render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={[url]}><ReportsPage /></MemoryRouter>
    </PinnedLightTheme>
  );
}

describe("ReportsPage trending link", () => {
  it("sends the retired ?tab=trending link to the merged control charts tab", () => {
    state.canViewKpi = true;
    renderAt("/reports?tab=trending");
    expect(screen.getByText("control chart tab")).toBeTruthy();
    expect(screen.queryByRole("tab", { name: "Trending & Analysis" })).toBeNull();
  });
});

describe("ReportsPage KPI tab", () => {
  it("offers and mounts the KPI / Performance tab to a holder of Kpi.View", () => {
    state.canViewKpi = true;
    renderAt("/reports?tab=performance");
    expect(screen.getByRole("tab", { name: "KPI / Performance" })).toBeTruthy();
    expect(screen.getByText("kpi tab")).toBeTruthy();
  });

  it("hides the tab and falls back to Overview without Kpi.View", () => {
    state.canViewKpi = false;
    renderAt("/reports?tab=performance");
    expect(screen.queryByRole("tab", { name: "KPI / Performance" })).toBeNull();
    expect(screen.queryByText("kpi tab")).toBeNull();
    expect(screen.getByText("overview tab")).toBeTruthy();
  });

  it("offers the Overview KPI shortcut only to a holder of Kpi.View", () => {
    state.canViewKpi = true;
    renderAt("/reports?tab=overview");
    expect(screen.getByRole("button", { name: "KPI shortcut" })).toBeTruthy();
    cleanup();
    state.canViewKpi = false;
    renderAt("/reports?tab=overview");
    expect(screen.queryByRole("button", { name: "KPI shortcut" })).toBeNull();
  });

  it("hides the page-level period selector on the KPI tab, which owns its filters, and keeps it elsewhere", () => {
    state.canViewKpi = true;
    renderAt("/reports?tab=performance");
    expect(screen.queryByText("Last 7 Days")).toBeNull();
    cleanup();
    renderAt("/reports?tab=overview");
    expect(screen.getByText("Last 7 Days")).toBeTruthy();
  });
});

describe("ReportsPage remembered tab", () => {
  it("switches tabs and remembers the choice", async () => {
    const user = userEvent.setup();
    state.canViewKpi = true;
    renderAt("/reports");

    expect(screen.getByText("overview tab")).toBeTruthy();
    await user.click(screen.getByRole("tab", { name: "Record Search" }));
    expect(screen.getByText("search tab")).toBeTruthy();
    await waitFor(() => expect(uiStateService.set).toHaveBeenCalledWith("nav.tab.reports", "search"), { timeout: 2000 });
  });

  it("opens the last tab used when the address names none", async () => {
    state.canViewKpi = true;
    vi.mocked(uiStateService.getAll).mockResolvedValue({ "nav.tab.reports": "media-gpt" });
    renderAt("/reports");

    await waitFor(() => expect(screen.getByText("media tab")).toBeTruthy());
  });

  it("ignores a stored KPI tab once the user lost Kpi.View", async () => {
    state.canViewKpi = false;
    vi.mocked(uiStateService.getAll).mockResolvedValue({ "nav.tab.reports": "performance" });
    renderAt("/reports");

    await waitFor(() => expect(uiStateService.getAll).toHaveBeenCalled());
    await new Promise((r) => setTimeout(r, 50));
    expect(screen.getByText("overview tab")).toBeTruthy();
    expect(screen.queryByText("kpi tab")).toBeNull();
  });
});
