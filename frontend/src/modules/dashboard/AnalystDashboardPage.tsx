import { useEffect, useState } from "react";
import { Grid, Box, Typography, Button } from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../../contexts/AuthContext";
import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
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

export function AnalystDashboardPage() {
  const { username, fullName } = useAuth();
  const navigate = useNavigate();
  const displayName = fullName ?? username ?? "Analyst";

  const { data: summary } = useDashboardSummary();
  const { data: tasks, loading: tasksLoading } = useMyTasks();
  const { data: todaysWork } = useTodaysWork();

  const [incubations, setIncubations] = useState<IncubationOverviewRow[]>([]);
  const [metrics, setMetrics] = useState<AnalystMetrics | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      DashboardService.getIncubationOverview(true).catch(() => []),
      DashboardService.getAnalystMetrics().catch(() => null)
    ]).then(([incData, metData]) => {
      setIncubations(incData);
      setMetrics(metData);
      setLoading(false);
    });
  }, []);

  if (!summary) return <LoadingSpinner />;

  return (
    <>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2, flexWrap: "wrap", gap: 1.5 }}>
        <PageHeader
          title={`Welcome back, ${displayName}`}
          subtitle="Here is your prioritized microbiological workspace for today."
        />
        <Button
          variant="contained"
          startIcon={<ScienceOutlinedIcon />}
          onClick={() => navigate("/testing-workspace")}
          sx={{ textTransform: "none", fontWeight: 600, borderRadius: 2 }}
        >
          Open Testing Workspace
        </Button>
      </Box>

      {/* Tier 1: KPI Work Summary Strip */}
      <AnalystWorkSummary
        tasks={tasks ?? []}
        readyToReadCount={summary.readyToReadCount}
        onSelectCategory={(cat) => {
          if (cat === "Overdue") navigate("/testing-workspace?scope=mine&urgency=overdue");
          else if (cat === "ReadyToRead") navigate("/testing-workspace?scope=mine&testStatus=ReadyToRead");
          else navigate("/testing-workspace?scope=mine");
        }}
      />

      {/* Tier 2: Action Required by Analyst */}
      <ActionRequiredPanel tasks={tasks} loading={tasksLoading} />

      {/* Tier 3: Attention Required & Incubation Monitoring */}
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid item xs={12} md={6}>
          <AttentionRequiredPanel />
        </Grid>
        <Grid item xs={12} md={6}>
          <IncubationObservationPanel rows={incubations} loading={loading} />
        </Grid>
      </Grid>

      {/* Tier 4: My Active Work Table */}
      <Box sx={{ mb: 2 }}>
        {todaysWork ? <TodaysWorkTable items={todaysWork} /> : <LoadingSpinner />}
      </Box>

      {/* Tier 5: Daily Throughput & Operational Metrics */}
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid item xs={12} md={6}>
          <CompletedTodayPanel metrics={metrics} />
        </Grid>
        <Grid item xs={12} md={6}>
          <AnalystPerformancePanel metrics={metrics} />
        </Grid>
      </Grid>
    </>
  );
}
