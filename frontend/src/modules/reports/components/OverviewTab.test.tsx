import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { OverviewService } from "../services/OverviewService";
import type { OverviewDashboardData } from "../types/reportingTypes";
import { OverviewTab } from "./OverviewTab";
import { renderInArabic, resetLanguage } from "../../../i18n/testUtils";

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

  it("renders Arabic headings and quick actions when language is Arabic", async () => {
    await renderInArabic(<OverviewTab onNavigateTab={vi.fn()} canViewKpi={true} />, ["common", "reports"]);

    try {
      expect(document.documentElement.dir).toBe("rtl");
      expect(await screen.findByText("بيانات قاعدة البيانات الحية")).toBeTruthy();
      expect(screen.getByText("إشارات الجودة")).toBeTruthy();
      expect(screen.getByRole("button", { name: "بحث السجلات" })).toBeTruthy();
    } finally {
      await resetLanguage();
    }
  });

  it("shows the KPI cards in Arabic with Latin digits and the counts kept in order", async () => {
    vi.mocked(OverviewService.getOverviewData).mockResolvedValue({
      ...dummyData,
      totalTests: { ...dummyData.totalTests, value: "2", caption: { key: "liveRecords" } },
      approvedResults: { ...dummyData.approvedResults, value: "2", caption: { key: "approved", approved: 2, total: 2 } },
      pendingReview: { ...dummyData.pendingReview, caption: { key: "reviewQueue" } },
      outOfSpec: { ...dummyData.outOfSpec, caption: { key: "qualitySignal" } }
    });
    await renderInArabic(<OverviewTab onNavigateTab={vi.fn()} />, ["common", "reports"]);

    try {
      expect(await screen.findByText("إجمالي الاختبارات")).toBeTruthy();
      expect(screen.getByText("النتائج المعتمدة")).toBeTruthy();
      expect(screen.getAllByText("خارج المواصفات").length).toBeGreaterThan(0);
      expect(screen.getByText("سجلات قاعدة البيانات الحية")).toBeTruthy();
      const caption = screen.getByText((_, el) => el?.tagName === "P" && el.textContent === "2 من 2 معتمدة");
      expect(caption.querySelectorAll("bdi[dir='ltr']").length).toBe(2);
      expect(document.body.textContent).not.toMatch(/[٠-٩]/);
    } finally {
      await resetLanguage();
    }
  });
});
