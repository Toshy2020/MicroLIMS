import { useMemo, useState } from "react";
import { Tabs, Tab, Box } from "@mui/material";
import { PageHeader } from "../components/PageHeader";
import { brandColors } from "../theme";
import { QuickPeriodSelector } from "../modules/reports/components/QuickPeriodSelector";
import { ComingSoonPanel } from "../modules/reports/components/ComingSoonPanel";
import { RecordSearchTab } from "../modules/reports/RecordSearchTab";
import { QuickPeriod, computeQuickPeriodRange, toDateInputValue } from "../modules/reports/utils/dateRange";

const TABS = ["Overview", "Record Search", "Report Builder", "Trending & Analysis", "Saved Reports", "KPI / Performance"] as const;

// Rebuilt as the redesign's tabbed shell - Record Search is the only
// functional tab in this prompt, everything else is a placeholder (see
// ComingSoonPanel). The quick-period selector lives here, above the
// tabs, so its fromDate/toDate can be shared with Trending & Analysis
// once that tab is built.
export function ReportsPage() {
  const [tab, setTab] = useState(1); // Record Search active by default

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
          onChange={(_, v) => setTab(v)}
          variant="scrollable"
          scrollButtons="auto"
          TabIndicatorProps={{ style: { backgroundColor: brandColors.sectionTitle } }}
          sx={{ "& .Mui-selected": { color: `${brandColors.sectionTitle} !important`, fontWeight: 700 } }}
        >
          {TABS.map((label) => <Tab key={label} label={label} />)}
        </Tabs>
      </Box>

      {tab === 0 && <ComingSoonPanel title="Overview" />}
      {tab === 1 && <RecordSearchTab fromDate={range.fromDate} toDate={range.toDate} />}
      {tab === 2 && <ComingSoonPanel title="Report Builder" />}
      {tab === 3 && <ComingSoonPanel title="Trending & Analysis" />}
      {tab === 4 && <ComingSoonPanel title="Saved Reports" />}
      {tab === 5 && <ComingSoonPanel title="KPI / Performance" />}
    </>
  );
}
