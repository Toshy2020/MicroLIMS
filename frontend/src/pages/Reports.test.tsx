import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { ReportsPage } from "./Reports";

const state = vi.hoisted(() => ({ canViewKpi: true }));

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
vi.mock("../modules/reports/components/TrendingTab", async () => {
  const { createElement } = await import("react");
  return { TrendingTab: () => createElement("div", null, "trending tab") };
});
vi.mock("../modules/kpi/components/KpiPerformanceTab", async () => {
  const { createElement } = await import("react");
  return { KpiPerformanceTab: () => createElement("div", null, "kpi tab") };
});

afterEach(cleanup);

function renderAt(url: string) {
  render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={[url]}><ReportsPage /></MemoryRouter>
    </PinnedLightTheme>
  );
}

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
