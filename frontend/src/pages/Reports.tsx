import { useMemo, useState, useEffect } from "react";
import { useSearchParams } from "react-router-dom";
import { Tabs, Tab, Box } from "@mui/material";
import { PageHeader } from "../components/PageHeader";
import { brandColors } from "../theme";
import { QuickPeriodSelector } from "../modules/reports/components/QuickPeriodSelector";
import { OverviewTab } from "../modules/reports/components/OverviewTab";
import { RecordSearchTab } from "../modules/reports/RecordSearchTab";
import { TrendingTab } from "../modules/reports/components/TrendingTab";
import { AnalystKpiTab } from "../modules/reports/components/AnalystKpiTab";
import { QuickPeriod, computeQuickPeriodRange, toDateInputValue } from "../modules/reports/utils/dateRange";

const TABS = [
  "Overview",
  "Record Search",
  "Trending & Analysis",
  "KPI / Performance"
] as const;

const TAB_KEYS = ["overview", "search", "trending", "performance"] as const;

export function ReportsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const tabParam = searchParams.get("tab")?.toLowerCase();

  const initialTab = useMemo(() => {
    if (!tabParam) return 0; // Overview active by default
    const idx = TAB_KEYS.indexOf(tabParam as any);
    return idx >= 0 ? idx : 0;
  }, [tabParam]);

  const [tab, setTab] = useState(initialTab);

  useEffect(() => {
    if (tabParam) {
      const idx = TAB_KEYS.indexOf(tabParam as any);
      const safeTab = idx >= 0 ? idx : 0;
      if (safeTab !== tab) {
        setTab(safeTab);
      }
      if (idx < 0) {
        setSearchParams({ tab: TAB_KEYS[0] }, { replace: true });
      }
    }
  }, [tabParam, tab, setSearchParams]);

  const handleTabChange = (newTab: number) => {
    const safeTab = Math.max(0, Math.min(newTab, TAB_KEYS.length - 1));
    setTab(safeTab);
    setSearchParams({ tab: TAB_KEYS[safeTab] });
  };

  const [period, setPeriod] = useState<QuickPeriod>("30d");
  const defaultRange = useMemo(() => computeQuickPeriodRange("30d"), []);
  const [customFrom, setCustomFrom] = useState(toDateInputValue(new Date(Date.now() - 30 * 86400000)));
  const [customTo, setCustomTo] = useState(toDateInputValue(new Date()));

  // Cross-tab interaction state for Trending drill-down
  const [trendParams, setTrendParams] = useState<{ testCode?: string; subjectName?: string }>({});

  const range = useMemo(() => {
    if (period === "custom") {
      return { fromDate: customFrom ? `${customFrom}T00:00:00.000Z` : undefined, toDate: customTo ? `${customTo}T23:59:59.999Z` : undefined };
    }
    return computeQuickPeriodRange(period);
  }, [period, customFrom, customTo, defaultRange]);

  const handleAnalyzeTrend = (testCode: string, subjectName: string) => {
    setTrendParams({ testCode, subjectName });
    handleTabChange(2); // Switch to Trending & Analysis
  };

  return (
    <>
      <PageHeader title="Reports" subtitle="Search, trend, and export laboratory results." />

      <QuickPeriodSelector
        period={period}
        customFrom={customFrom}
        customTo={customTo}
        onPeriodChange={setPeriod}
        onCustomChange={(from, to) => { setCustomFrom(from); setCustomTo(to); }}
      />

      <Box sx={{ borderBottom: 1, borderColor: "divider", mb: 2 }}>
        <Tabs
          value={tab}
          onChange={(_, v) => handleTabChange(v)}
          variant="scrollable"
          scrollButtons="auto"
          TabIndicatorProps={{ style: { backgroundColor: brandColors.sectionTitle } }}
          sx={{ "& .Mui-selected": { color: `${brandColors.sectionTitle} !important`, fontWeight: 700 } }}
        >
          {TABS.map((label) => <Tab key={label} label={label} />)}
        </Tabs>
      </Box>

      {tab === 0 && (
        <OverviewTab
          fromDate={range.fromDate}
          toDate={range.toDate}
          onNavigateTab={handleTabChange}
        />
      )}

      {tab === 1 && (
        <RecordSearchTab
          fromDate={range.fromDate}
          toDate={range.toDate}
          onAnalyzeTrend={handleAnalyzeTrend}
        />
      )}

      {tab === 2 && (
        <TrendingTab
          initialTestCode={trendParams.testCode}
          initialSubjectName={trendParams.subjectName}
        />
      )}

      {tab === 3 && (
        <AnalystKpiTab />
      )}
    </>
  );
}
