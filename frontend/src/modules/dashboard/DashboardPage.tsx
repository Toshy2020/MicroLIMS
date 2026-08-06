import { useState } from "react";
import { Grid, Paper, Typography, Box, Stack, Button } from "@mui/material";
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

function MonthlyStatCard({ value, label, deltaPercent }: { value: number; label: string; deltaPercent: number }) {
  return (
    <Paper sx={{ p: 2 }}>
      <Typography sx={{ fontSize: 22, fontWeight: 700, color: brandColors.sectionTitle }}>{value}</Typography>
      <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{label}</Typography>
      <Typography sx={{ fontSize: 11, color: deltaPercent >= 0 ? brandColors.ok : brandColors.err }}>
        {deltaPercent >= 0 ? "+" : ""}{deltaPercent}% vs last month
      </Typography>
    </Paper>
  );
}

// Role-aware Dashboard: everyone gets the KPI strip, Quick Links,
// Today's Laboratory Work, Alerts, Incubation Overview, and the two
// charts. Analysts additionally get My Tasks (above Today's Laboratory
// Work) and Media Expiry (beside Incubation Overview) - gated here by
// role rather than a route guard, since it's two extra panels layered
// onto one shared page, not a different route.
export function DashboardPage() {
  const { role, username } = useAuth();
  const isAnalyst = role === "Analyst";

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
        <PageHeader title={`Welcome back, ${username}`} subtitle="Here's what's happening in your laboratory today." />
        <Button variant="contained">Generate Report</Button>
      </Box>

      <KpiStrip summary={summary} />
      <QuickLinksBar
        preparationQueue={summary.preparationQueue}
        reviewerQueue={summary.reviewerQueue}
        approvalQueue={summary.approvalQueue}
      />

      {isAnalyst && (
        <Grid container spacing={2} sx={{ mb: 1 }}>
          <Grid item xs={12}>
            <MyTasksPanel />
          </Grid>
        </Grid>
      )}

      <Grid container spacing={2} sx={{ mb: 1 }}>
        <Grid item xs={12} md={8}>
          {todaysWork ? <TodaysWorkTable items={todaysWork} /> : <LoadingSpinner />}
        </Grid>
        <Grid item xs={12} md={4}>
          <AlertsPanel />
        </Grid>
      </Grid>

      <Grid container spacing={2} sx={{ mb: 1 }}>
        <Grid item xs={12} md={isAnalyst ? 6 : 12}>
          {incubationOverview ? <IncubationOverview rows={incubationOverview} /> : <LoadingSpinner />}
        </Grid>
        {isAnalyst && (
          <Grid item xs={12} md={6}>
            <MediaExpiryPanel />
          </Grid>
        )}
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
