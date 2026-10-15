import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { OverviewService } from "../services/OverviewService";
import type { OverviewDashboardData } from "../types/reportingTypes";
import { OverviewTab } from "./OverviewTab";

vi.mock("../services/OverviewService", () => ({
  OverviewService: { getOverviewData: vi.fn() }
}));
vi.mock("recharts", async () => {
  const { createElement, Fragment } = await import("react");
  const Pass = ({ children }: { children?: unknown }) => createElement(Fragment, null, children as never);
  const Nil = () => null;
  return {
    ResponsiveContainer: Pass,
    PieChart: Pass,
    Pie: Nil,
    Cell: Nil,
    BarChart: Pass,
    Bar: Nil,
    XAxis: Nil,
    YAxis: Nil,
    CartesianGrid: Nil,
    Tooltip: Nil,
    AreaChart: Pass,
    Area: Nil,
    LabelList: Nil
  };
});

afterEach(cleanup);

const dummyData: OverviewDashboardData = {
  totalTests: { title: "Total Tests", value: 10, deltaPercent: 0, deltaDirection: "neutral", comparisonLabel: "vs previous" },
  approvedResults: { title: "Approved", value: 5, deltaPercent: 0, deltaDirection: "neutral", comparisonLabel: "vs previous" },
  pendingReview: { title: "Pending Review", value: 2, deltaPercent: 0, deltaDirection: "neutral", comparisonLabel: "vs previous" },
  pendingApproval: { title: "Pending Approval", value: 3, deltaPercent: 0, deltaDirection: "neutral", comparisonLabel: "vs previous" },
  outOfSpec: { title: "OOS", value: 0, deltaPercent: 0, deltaDirection: "neutral", comparisonLabel: "vs previous" },
  alertActionLevel: { title: "Alert/Action", value: 0, deltaPercent: 0, deltaDirection: "neutral", comparisonLabel: "vs previous" },
  categoryDistribution: [],
  testDistribution: [],
  locationDistribution: [],
  recentResults: [],
  qualitySignals: { outOfSpecCount: 0, alertActionCount: 0, pendingReviewCount: 2, pendingApprovalCount: 3 }
};

describe("OverviewTab", () => {
  it("hides the KPI shortcut button when canViewKpi is not passed (defaults to false)", async () => {
    vi.mocked(OverviewService.getOverviewData).mockResolvedValue(dummyData);
    render(
      <PinnedLightTheme>
        <OverviewTab onNavigateTab={vi.fn()} />
      </PinnedLightTheme>
    );
    expect(await screen.findByText("Quick Actions")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "KPI / Performance" })).toBeNull();
  });

  it("shows the KPI shortcut button and navigates to tab 5 when canViewKpi is true", async () => {
    vi.mocked(OverviewService.getOverviewData).mockResolvedValue(dummyData);
    const onNavigateTab = vi.fn();
    render(
      <PinnedLightTheme>
        <OverviewTab onNavigateTab={onNavigateTab} canViewKpi={true} />
      </PinnedLightTheme>
    );
    const button = await screen.findByRole("button", { name: "KPI / Performance" });
    expect(button).toBeTruthy();
    await userEvent.click(button);
    expect(onNavigateTab).toHaveBeenCalledWith(5);
  });
});
