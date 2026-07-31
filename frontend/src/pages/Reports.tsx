import { useEffect, useState } from "react";
import { Button, Stack, Paper, Typography, Box, Grid } from "@mui/material";
import { apiClient } from "../services/apiClient";
import { PageHeader } from "../components/PageHeader";
import { SectionTitle } from "../components/SectionTitle";
import { useAuth } from "../contexts/AuthContext";
import { brandColors } from "../theme";

interface AnalystKpi { userId: number; username: string; completedTests: number; pendingTests: number; averageTurnaroundHours: number }
interface CompletionStats { totalTestOrders: number; approved: number; rejected: number; pending: number; approvalRatePercent: number }
interface DelayTracking { delayedCount: number; averageDelayHours: number }

// Interactive tables + one-click PDF, per spec section 6, styled per
// the provided card design. KPI section (Analyst KPIs, completion
// stats, delay tracking) is Section Head/Administrator only.
export function ReportsPage() {
  const { role } = useAuth();
  const canSeeKpis = role === "SectionHead" || role === "SystemAdministrator";

  const [analystKpis, setAnalystKpis] = useState<AnalystKpi[] | null>(null);
  const [completionStats, setCompletionStats] = useState<CompletionStats | null>(null);
  const [delayTracking, setDelayTracking] = useState<DelayTracking | null>(null);

  useEffect(() => {
    if (!canSeeKpis) return;
    apiClient.get("/kpi/analysts").then((res) => setAnalystKpis(res.data.data));
    apiClient.get("/kpi/completion-stats").then((res) => setCompletionStats(res.data.data));
    apiClient.get("/kpi/delay-tracking").then((res) => setDelayTracking(res.data.data));
  }, [canSeeKpis]);

  const download = async (type: "product" | "water" | "em" | "aftercleaning") => {
    const res = await apiClient.get(`/reports/${type}`, { responseType: "blob" });
    const url = window.URL.createObjectURL(res.data);
    const a = document.createElement("a");
    a.href = url;
    a.download = `${type}-report.pdf`;
    a.click();
  };

  return (
    <>
      <PageHeader title="Reports" subtitle="Generate and download laboratory reports." />
      <SectionTitle>Download Reports</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap">
          <Button variant="contained" onClick={() => download("product")}>Product Report</Button>
          <Button variant="contained" onClick={() => download("water")}>Water Report</Button>
          <Button variant="contained" onClick={() => download("em")}>EM Report</Button>
          <Button variant="contained" onClick={() => download("aftercleaning")}>After Cleaning Report</Button>
        </Stack>
      </Paper>

      {canSeeKpis && (
        <>
          <SectionTitle>Completion Statistics</SectionTitle>
          {completionStats && (
            <Grid container spacing={2} sx={{ mb: 3 }}>
              {[
                { label: "Total Test Orders", value: completionStats.totalTestOrders },
                { label: "Approved", value: completionStats.approved },
                { label: "Rejected", value: completionStats.rejected },
                { label: "Pending", value: completionStats.pending },
                { label: "Approval Rate", value: `${completionStats.approvalRatePercent}%` }
              ].map((c) => (
                <Grid item xs={6} sm={2.4} key={c.label}>
                  <Paper sx={{ p: 2 }}>
                    <Typography sx={{ fontSize: 24, fontWeight: 700, color: brandColors.sectionTitle }}>{c.value}</Typography>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{c.label}</Typography>
                  </Paper>
                </Grid>
              ))}
            </Grid>
          )}

          <SectionTitle>Delay Tracking</SectionTitle>
          {delayTracking && (
            <Paper sx={{ p: 2.5, mb: 3 }}>
              <Typography variant="body2" color="text.secondary">
                {delayTracking.delayedCount} test order(s) currently delayed, averaging {delayTracking.averageDelayHours} hours over the 24-hour threshold.
              </Typography>
            </Paper>
          )}

          <SectionTitle>Analyst KPIs</SectionTitle>
          {!analystKpis || analystKpis.length === 0 ? (
            <Typography sx={{ color: "#9ca3af", fontSize: 13 }}>No analyst activity yet.</Typography>
          ) : (
            <Stack spacing={1}>
              {analystKpis.map((k) => (
                <Paper key={k.userId} sx={{ p: 2 }}>
                  <Stack direction="row" spacing={4}>
                    <Box><Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Analyst</Typography><Typography sx={{ fontWeight: 700 }}>{k.username}</Typography></Box>
                    <Box><Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Completed</Typography><Typography>{k.completedTests}</Typography></Box>
                    <Box><Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Pending</Typography><Typography>{k.pendingTests}</Typography></Box>
                    <Box><Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Avg Turnaround</Typography><Typography>{k.averageTurnaroundHours}h</Typography></Box>
                  </Stack>
                </Paper>
              ))}
            </Stack>
          )}
        </>
      )}
    </>
  );
}
