import { useEffect, useState } from "react";
import { Alert, Grid, Box, Button } from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { Link } from "react-router-dom";
import { useAuth } from "../../contexts/AuthContext";
import { PageHeader } from "../../components/PageHeader";
import { TableSkeleton } from "../../components/TableSkeleton";
import RefreshIcon from "@mui/icons-material/Refresh";
import { DashboardStateGate } from "./components/DashboardStateGate";
import { useDashboardSummary } from "./hooks/useDashboardSummary";
import { useMyTasks } from "./hooks/useMyTasks";
import { useTodaysWork } from "./hooks/useTodaysWork";
import { DashboardService } from "./services/DashboardService";
import { IncubationOverviewRow, AnalystMetrics } from "./types/dashboard";
import { AnalystWorkSummary } from "./components/AnalystWorkSummary";
import { ActionRequiredPanel } from "./components/ActionRequiredPanel";
import { AttentionRequiredPanel } from "./components/AttentionRequiredPanel";
import { IncubationObservationPanel } from "./components/IncubationObservationPanel";
import { TodaysWorkTable } from "./components/TodaysWorkTable";
import { CompletedTodayPanel } from "./components/CompletedTodayPanel";
import { AnalystPerformancePanel } from "./components/AnalystPerformancePanel";
import { LAB_LABELS, useDashboardLab } from "./DashboardLabContext";

export function AnalystDashboardPage() {
  const { username, fullName } = useAuth();
  const displayName = fullName ?? username ?? "Analyst";
  const lab = useDashboardLab();

  // useApi already tracks the error; this page used to destructure only
  // `data` and gate on `!summary`, so a failed request left the screen as a
  // spinner forever.
  const { data: summary, loading: summaryLoading, error: summaryError, reload: reloadSummary } = useDashboardSummary(lab.code);
  const { data: tasks, loading: tasksLoading, reload: reloadTasks } = useMyTasks(lab.code);
  const { data: todaysWork, reload: reloadTodaysWork } = useTodaysWork(lab.code);

  const [incubations, setIncubations] = useState<IncubationOverviewRow[]>([]);
  const [metrics, setMetrics] = useState<AnalystMetrics | null>(null);
  const [loading, setLoading] = useState(true);
  const [partialFailure, setPartialFailure] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  const reload = () => {
    setReloadKey((k) => k + 1);
    reloadSummary();
    reloadTasks();
    reloadTodaysWork();
  };

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    // These two are supplementary, so a failure degrades their panels rather
    // than blanking the dashboard - but it is NOT silent. Swallowing them into
    // [] and null made a dead endpoint look like "nothing is incubating",
    // which on a GMP board is worse than an error.
    // Incubations are Microbiology work; a Physicochemical view skips them.
    Promise.allSettled([
      lab.isPhyschem ? Promise.resolve([] as IncubationOverviewRow[]) : DashboardService.getIncubationOverview(true, lab.code),
      DashboardService.getAnalystMetrics(lab.code)
    ]).then(([incResult, metResult]) => {
      if (cancelled) return;
      setIncubations(incResult.status === "fulfilled" ? incResult.value : []);
      setMetrics(metResult.status === "fulfilled" ? metResult.value : null);
      setPartialFailure(incResult.status === "rejected" || metResult.status === "rejected");
      setLoading(false);
    });

    return () => { cancelled = true; };
  }, [reloadKey, lab.code, lab.isPhyschem]);

  if (!summary) {
    return (
      <DashboardStateGate loading={summaryLoading} error={summaryError} hasData={false} onRetry={reload}>
        {null}
      </DashboardStateGate>
    );
  }

  return (
    <>
      <PageHeader
        title={`Welcome back, ${displayName}`}
        subtitle={lab.code ? `Your prioritized work in the ${LAB_LABELS[lab.code]} for today.` : "Your prioritized work for today."}
      >
        <Button
          variant="outlined"
          onClick={reload}
          disabled={loading}
          startIcon={<RefreshIcon />}
        >
          Refresh
        </Button>
        <Button
          component={Link}
          to={lab.workspace()}
          variant="contained"
          startIcon={<ScienceOutlinedIcon />}
        >
          Open Testing Workspace
        </Button>
      </PageHeader>

      {partialFailure && (
        <Alert
          severity="warning"
          sx={{ mb: 2, borderRadius: 2 }}
          action={
            <Button size="small" color="inherit" onClick={reload} sx={{ textTransform: "none", fontWeight: 600 }}>
              Retry
            </Button>
          }
        >
          {lab.isPhyschem
            ? "Some panels could not be loaded, so the throughput figures below may be incomplete."
            : "Some panels could not be loaded, so the incubation and throughput figures below may be incomplete."}
        </Alert>
      )}

      {/* Tier 1: KPI Work Summary Strip */}
      {/* No onSelectCategory: each tile always gets a `to`, so its Link
          branch wins and the onClick branch was unreachable. The tiles
          navigate to these same routes via CATEGORY_ROUTES, as real links. */}
      <AnalystWorkSummary tasks={tasks ?? []} readyToReadCount={summary.readyToReadCount} />

      {/* Tier 2: Action Required by Analyst */}
      <ActionRequiredPanel tasks={tasks} loading={tasksLoading} />

      {/* Tier 3: Attention Required & (Microbiology) Incubation Monitoring */}
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid
          size={{
            xs: 12,
            md: lab.isPhyschem ? 12 : 6
          }}>
          <AttentionRequiredPanel />
        </Grid>
        {!lab.isPhyschem && (
          <Grid
            size={{
              xs: 12,
              md: 6
            }}>
            <IncubationObservationPanel rows={incubations} loading={loading} />
          </Grid>
        )}
      </Grid>

      {/* Tier 4: My Active Work Table */}
      <Box sx={{ mb: 2 }}>
        {todaysWork ? <TodaysWorkTable items={todaysWork} /> : <TableSkeleton rows={4} cols={5} />}
      </Box>

      {/* Tier 5: Daily Throughput & Operational Metrics */}
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid
          size={{
            xs: 12,
            md: 6
          }}>
          <CompletedTodayPanel metrics={metrics} />
        </Grid>
        <Grid
          size={{
            xs: 12,
            md: 6
          }}>
          <AnalystPerformancePanel metrics={metrics} />
        </Grid>
      </Grid>
    </>
  );
}
