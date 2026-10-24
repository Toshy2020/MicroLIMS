import { useMemo, useState, useEffect } from "react";
import { useSearchParams } from "react-router-dom";
import { Tabs, Tab, Box } from "@mui/material";
import { PageHeader } from "../components/PageHeader";
import { useHasPermission } from "../contexts/AuthContext";
import { PERMISSIONS } from "../routes/routes";
import { QuickPeriodSelector } from "../modules/reports/components/QuickPeriodSelector";
import { OverviewTab } from "../modules/reports/components/OverviewTab";
import { RecordSearchTab } from "../modules/reports/RecordSearchTab";
import { MediaGptReportTab } from "../modules/reports/MediaGptReportTab";
import { ReferenceStrainReportTab } from "../modules/reports/ReferenceStrainReportTab";
import { ControlChartTab, presetControlChart } from "../modules/reports/components/controlChart/ControlChartTab";
import { KpiPerformanceTab } from "../modules/kpi/components/KpiPerformanceTab";
import { QuickPeriod, computeQuickPeriodRange, toDateInputValue } from "../modules/reports/utils/dateRange";
import { SampleCategory } from "../modules/reports/types/reportingTypes";

const TABS = [
  "Overview",
  "Record Search",
  "Media / GPT",
  "Reference Strains",
  "Trending & Control Charts",
  "KPI / Performance"
] as const;

const TAB_KEYS = ["overview", "search", "media-gpt", "reference-strains", "control-charts", "performance"] as const;

// The KPI tab is the last entry. It owns its filters, so the page-level period selector is hidden there.
const PERFORMANCE_TAB = 5;
const CONTROL_CHARTS_TAB = 4;

export function ReportsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  // Trending was merged into Control Charts; old links still land there.
  const rawTab = searchParams.get("tab")?.toLowerCase();
  const tabParam = rawTab === "trending" ? "control-charts" : rawTab;

  // Without Kpi.View the KPI tab is dropped from both lists, so a link to
  // ?tab=performance falls back to Overview. The server enforces the permission too.
  const canViewKpi = useHasPermission(PERMISSIONS.KPI_VIEW);
  const tabs: readonly string[] = canViewKpi ? TABS : TABS.slice(0, PERFORMANCE_TAB);
  const tabKeys = useMemo<readonly string[]>(
    () => (canViewKpi ? TAB_KEYS : TAB_KEYS.slice(0, PERFORMANCE_TAB)),
    [canViewKpi]
  );

  const initialTab = useMemo(() => {
    if (!tabParam) return 0; // Overview active by default
    const idx = tabKeys.indexOf(tabParam);
    return idx >= 0 ? idx : 0;
  }, [tabParam, tabKeys]);

  const [tab, setTab] = useState(initialTab);

  useEffect(() => {
    if (tabParam) {
      const idx = tabKeys.indexOf(tabParam);
      const safeTab = idx >= 0 ? idx : 0;
      if (safeTab !== tab) {
        setTab(safeTab);
      }
      if (idx < 0) {
        setSearchParams({ tab: tabKeys[0] }, { replace: true });
      }
    }
  }, [tabParam, tab, tabKeys, setSearchParams]);

  const handleTabChange = (newTab: number) => {
    const safeTab = Math.max(0, Math.min(newTab, tabKeys.length - 1));
    setTab(safeTab);
    setSearchParams({ tab: tabKeys[safeTab] });
  };

  const [period, setPeriod] = useState<QuickPeriod>("30d");
  const defaultRange = useMemo(() => computeQuickPeriodRange("30d"), []);
  const [customFrom, setCustomFrom] = useState(toDateInputValue(new Date(Date.now() - 30 * 86400000)));
  const [customTo, setCustomTo] = useState(toDateInputValue(new Date()));

  const range = useMemo(() => {
    if (period === "custom") {
      return { fromDate: customFrom ? `${customFrom}T00:00:00.000Z` : undefined, toDate: customTo ? `${customTo}T23:59:59.999Z` : undefined };
    }
    return computeQuickPeriodRange(period);
  }, [period, customFrom, customTo, defaultRange]);

  const handleAnalyzeTrend = (testCode: string, subjectName: string, category: SampleCategory) => {
    presetControlChart({ category, testCode, subjectName });
    handleTabChange(CONTROL_CHARTS_TAB);
  };

  return (
    <>
      <PageHeader title="Reports" subtitle="Search, trend, and export laboratory results." />

      {tab !== PERFORMANCE_TAB && (
        <QuickPeriodSelector
          period={period}
          customFrom={customFrom}
          customTo={customTo}
          onPeriodChange={setPeriod}
          onCustomChange={(from, to) => { setCustomFrom(from); setCustomTo(to); }}
        />
      )}

      <Box sx={{ borderBottom: 1, borderColor: "divider", mb: 2 }}>
        <Tabs
          value={tab}
          onChange={(_, v) => handleTabChange(v)}
          variant="scrollable"
          scrollButtons="auto"
          sx={{ "& .Mui-selected": { fontWeight: 700 } }}
        >
          {tabs.map((label) => <Tab key={label} label={label} />)}
        </Tabs>
      </Box>

      {tab === 0 && (
        <OverviewTab
          fromDate={range.fromDate}
          toDate={range.toDate}
          onNavigateTab={handleTabChange}
          canViewKpi={canViewKpi}
        />
      )}

      {tab === 1 && (
        <RecordSearchTab
          fromDate={range.fromDate}
          toDate={range.toDate}
          onAnalyzeTrend={handleAnalyzeTrend}
          onNavigateTab={handleTabChange}
        />
      )}

      {tab === 2 && (
        <MediaGptReportTab
          fromDate={range.fromDate}
          toDate={range.toDate}
        />
      )}

      {tab === 3 && (
        <ReferenceStrainReportTab />
      )}

      {tab === CONTROL_CHARTS_TAB && (
        <ControlChartTab />
      )}

      {tab === PERFORMANCE_TAB && canViewKpi && (
        <KpiPerformanceTab />
      )}
    </>
  );
}
