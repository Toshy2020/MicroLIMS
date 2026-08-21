import { useState } from "react";
import { Grid, Paper, Typography, Box, Stack, Button, useTheme } from "@mui/material";
import { useAuth } from "../../contexts/AuthContext";
import { useApi } from "../../hooks/useApi";
import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { brandColors } from "../../theme";
import { KpiDeltas, MonthlyTrendPoint, DistributionSlice } from "./types/dashboard";
import { useDashboardSummary } from "./hooks/useDashboardSummary";
import { useTodaysWork } from "./hooks/useTodaysWork";
import { useIncubationOverview } from "./hooks/useIncubationOverview";
import { KpiStrip } from "./components/KpiStrip";
import { QuickLinksBar } from "./components/QuickLinksBar";
import { TodaysWorkTable } from "./components/TodaysWorkTable";
import { AlertsPanel } from "./components/AlertsPanel";
import { IncubationOverview } from "./components/IncubationOverview";
import { MyTasksPanel } from "./components/MyTasksPanel";
import { MediaExpiryPanel } from "./components/MediaExpiryPanel";
import { SamplesTrendChart } from "./components/SamplesTrendChart";
import { TestOrderStatusDonut } from "./components/TestOrderStatusDonut";
import { AnalystDashboardPage } from "./AnalystDashboardPage";

function MonthlyStatCard({ value, label, deltaPercent }: { value: number; label: string; deltaPercent: number }) {
  const theme = useTheme();
  return (
    <Paper sx={{ p: 2 }}>
      <Typography sx={{ fontSize: 22, fontWeight: 700, color: theme.palette.primary.main }}>{value}</Typography>
      <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{label}</Typography>
      <Typography sx={{ fontSize: 11, color: deltaPercent >= 0 ? brandColors.ok : brandColors.err }}>
        {deltaPercent >= 0 ? "+" : ""}{deltaPercent}% vs last month
      </Typography>
    </Paper>
  );
}

export function DashboardPage() {
  const { role, username, fullName } = useAuth();

  // Analysts receive the action-oriented workspace dashboard
  if (role === "Analyst") {
    return <AnalystDashboardPage />;
  }

  const { data: summary } = useDashboardSummary();
  const { data: todaysWork } = useTodaysWork();
  const { data: incubationOverview } = useIncubationOverview();
  const { data: kpis } = useApi<KpiDeltas>("/dashboard/kpi-deltas");
  const [months, setMonths] = useState(6);
  const { data: trend } = useApi<MonthlyTrendPoint[]>(`/dashboard/monthly-trend?months=${months}`);
  const { data: statusDist } = useApi<DistributionSlice[]>("/dashboard/status-distribution");

  if (!summary) return <LoadingSpinner />;

  return (
    <>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
        <PageHeader title={`Welcome back, ${fullName ?? username}`} subtitle="Here's what's happening in your laboratory today." />
        <Button variant="contained">Generate Report</Button>
      </Box>

      <KpiStrip summary={summary} />
      <QuickLinksBar
        preparationQueue={summary.preparationQueue}
        reviewerQueue={summary.reviewerQueue}
        approvalQueue={summary.approvalQueue}
      />

      <Grid container spacing={2} sx={{ mb: 1 }}>
        <Grid item xs={12} md={8}>
          {todaysWork ? <TodaysWorkTable items={todaysWork} /> : <LoadingSpinner />}
        </Grid>
        <Grid item xs={12} md={4}>
          <AlertsPanel />
        </Grid>
      </Grid>

      <Grid container spacing={2} sx={{ mb: 1 }}>
        <Grid item xs={12}>
          {incubationOverview ? <IncubationOverview rows={incubationOverview} /> : <LoadingSpinner />}
        </Grid>
      </Grid>

      <Grid container spacing={2} sx={{ mb: 1 }}>
        <Grid item xs={12} md={7}>
          <SamplesTrendChart trend={trend} months={months} onMonthsChange={setMonths} />
        </Grid>
        <Grid item xs={12} md={2}>
          <Stack spacing={2}>
            {kpis && (
              <>
                <MonthlyStatCard value={kpis.samplesThisMonth} label="Total Samples" deltaPercent={kpis.samplesDeltaPercent} />
                <MonthlyStatCard value={kpis.testsThisMonth} label="Total Test Requests" deltaPercent={kpis.testsDeltaPercent} />
              </>
            )}
          </Stack>
        </Grid>
        <Grid item xs={12} md={3}>
          <TestOrderStatusDonut statusDist={statusDist} />
        </Grid>
      </Grid>
    </>
  );
}
