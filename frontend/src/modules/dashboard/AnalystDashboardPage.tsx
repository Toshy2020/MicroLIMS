import { useEffect, useState } from "react";
import { Alert, Grid, Button, Paper, Stack, Typography } from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { Link } from "react-router-dom";
import { useAuth } from "../../contexts/AuthContext";
import { PageHeader } from "../../components/PageHeader";
import RefreshIcon from "@mui/icons-material/Refresh";
import { DashboardStateGate } from "./components/DashboardStateGate";
import { useDashboardSummary } from "./hooks/useDashboardSummary";
import { DashboardService } from "./services/DashboardService";
import { myWorkService } from "../myWork/services/myWorkService";
import type { MyWorkSummary } from "../myWork/myWorkTypes";
import { IncubationOverviewRow, AnalystMetrics } from "./types/dashboard";
import { AnalystWorkSummary } from "./components/AnalystWorkSummary";
import { IncubationObservationPanel } from "./components/IncubationObservationPanel";
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

  const [workSummary, setWorkSummary] = useState<MyWorkSummary | null>(null);
  const [incubations, setIncubations] = useState<IncubationOverviewRow[]>([]);
  const [metrics, setMetrics] = useState<AnalystMetrics | null>(null);
  const [loading, setLoading] = useState(true);
  const [partialFailure, setPartialFailure] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  const reload = () => {
    setReloadKey((k) => k + 1);
    reloadSummary();
  };

  useEffect(() => {
    const controller = new AbortController();
    let cancelled = false;
    setLoading(true);

    // These are supplementary, so a failure degrades their panels rather
    // than blanking the dashboard - but it is NOT silent. Swallowing them into
    // [] and null made a dead endpoint look like "nothing is incubating",
    // which on a GMP board is worse than an error.
    // Incubations are Microbiology work; a Physicochemical view skips them.
    Promise.allSettled([
      lab.isPhyschem ? Promise.resolve([] as IncubationOverviewRow[]) : DashboardService.getIncubationOverview(true, lab.code),
      DashboardService.getAnalystMetrics(lab.code),
      myWorkService.summary(controller.signal)
    ]).then(([incResult, metResult, workResult]) => {
      if (cancelled || controller.signal.aborted) return;
      setIncubations(incResult.status === "fulfilled" ? incResult.value : []);
      setMetrics(metResult.status === "fulfilled" ? metResult.value : null);
      setWorkSummary(workResult.status === "fulfilled" ? workResult.value : null);
      setPartialFailure(incResult.status === "rejected" || metResult.status === "rejected");
      setLoading(false);
    });

    return () => {
      cancelled = true;
      controller.abort();
    };
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
      <AnalystWorkSummary
        overdueCount={workSummary?.complete ? workSummary.overdue : null}
        dueNowCount={workSummary?.complete ? workSummary.now : null}
        readyToReadCount={summary.readyToReadCount}
      />

      {/* Tier 2: Link to My Work queue */}
      <Paper variant="outlined" sx={{ p: 2, mb: 2, borderRadius: 2 }}>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={1.5}
          sx={{ alignItems: { sm: "center" }, justifyContent: "space-between" }}
        >
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Your prioritized tests, reviews, and tasks live in My Work.
          </Typography>
          <Button
            component={Link}
            to="/my-work"
            variant="contained"
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            {workSummary?.complete && workSummary.total !== null ? `Open My Work (${workSummary.total})` : "Open My Work"}
          </Button>
        </Stack>
      </Paper>

      {/* Tier 3: (Microbiology) Incubation Monitoring */}
      {!lab.isPhyschem && (
        <Grid container spacing={2} sx={{ mb: 2 }}>
          <Grid size={{ xs: 12 }}>
            <IncubationObservationPanel rows={incubations} loading={loading} />
          </Grid>
        </Grid>
      )}

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
