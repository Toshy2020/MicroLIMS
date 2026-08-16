import { useMemo, useState, useEffect } from "react";
import { useSearchParams } from "react-router-dom";
import { Tabs, Tab, Box } from "@mui/material";
import { PageHeader } from "../components/PageHeader";
import { brandColors } from "../theme";
import { QuickPeriodSelector } from "../modules/reports/components/QuickPeriodSelector";
import { OverviewTab } from "../modules/reports/components/OverviewTab";
import { RecordSearchTab } from "../modules/reports/RecordSearchTab";
import { ReportBuilderTab } from "../modules/reports/components/ReportBuilderTab";
import { TrendingTab } from "../modules/reports/components/TrendingTab";
import { SavedReportsTab } from "../modules/reports/components/SavedReportsTab";
import { AnalystKpiTab } from "../modules/reports/components/AnalystKpiTab";
import { QuickPeriod, computeQuickPeriodRange, toDateInputValue } from "../modules/reports/utils/dateRange";
import { ResultRecordItem, SavedReportConfiguration } from "../modules/reports/types/reportingTypes";

const TABS = [
  "Overview",
  "Record Search",
  "Report Builder",
  "Trending & Analysis",
  "Saved Reports",
  "KPI / Performance"
] as const;

const TAB_KEYS = ["overview", "search", "builder", "trending", "saved", "performance"] as const;

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
      if (idx >= 0 && idx !== tab) {
        setTab(idx);
      }
    }
  }, [tabParam, tab]);

  const handleTabChange = (newTab: number) => {
    setTab(newTab);
    setSearchParams({ tab: TAB_KEYS[newTab] });
  };

  const [period, setPeriod] = useState<QuickPeriod>("30d");
  const defaultRange = useMemo(() => computeQuickPeriodRange("30d"), []);
  const [customFrom, setCustomFrom] = useState(toDateInputValue(new Date(Date.now() - 30 * 86400000)));
  const [customTo, setCustomTo] = useState(toDateInputValue(new Date()));

  // Cross-tab interaction states
  const [builderRecords, setBuilderRecords] = useState<ResultRecordItem[] | undefined>(undefined);
  const [trendParams, setTrendParams] = useState<{ testCode?: string; subjectName?: string }>({});

  const range = useMemo(() => {
    if (period === "custom") {
      return { fromDate: customFrom ? `${customFrom}T00:00:00.000Z` : undefined, toDate: customTo ? `${customTo}T23:59:59.999Z` : undefined };
    }
    return computeQuickPeriodRange(period);
  }, [period, customFrom, customTo, defaultRange]);

  const handleBuildReport = (selectedRows: ResultRecordItem[]) => {
    setBuilderRecords(selectedRows);
    handleTabChange(2); // Switch to Report Builder
  };

  const handleAnalyzeTrend = (testCode: string, subjectName: string) => {
    setTrendParams({ testCode, subjectName });
    handleTabChange(3); // Switch to Trending & Analysis
  };

  const handleRunConfiguration = (config: SavedReportConfiguration) => {
    handleTabChange(2); // Switch to Report Builder with loaded config
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
          onBuildReport={handleBuildReport}
          onAnalyzeTrend={handleAnalyzeTrend}
        />
      )}

      {tab === 2 && (
        <ReportBuilderTab
          preloadedRecords={builderRecords}
        />
      )}

      {tab === 3 && (
        <TrendingTab
          initialTestCode={trendParams.testCode}
          initialSubjectName={trendParams.subjectName}
        />
      )}

      {tab === 4 && (
        <SavedReportsTab
          onNewReport={() => handleTabChange(2)}
          onRunConfiguration={handleRunConfiguration}
        />
      )}

      {tab === 5 && (
        <AnalystKpiTab />
      )}
    </>
  );
}
