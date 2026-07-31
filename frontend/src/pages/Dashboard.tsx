import { useEffect, useState } from "react";
import { Grid, Paper, Typography, Box, Stack, Alert, Select, MenuItem, Button } from "@mui/material";
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer,
  PieChart, Pie, Cell
} from "recharts";
import { useAuth } from "../contexts/AuthContext";
import { apiClient } from "../services/apiClient";
import { LoadingSpinner } from "../components/LoadingSpinner";
import { PageHeader } from "../components/PageHeader";
import { SectionTitle } from "../components/SectionTitle";
import { brandColors, chartPalette } from "../theme";

interface DashboardSummary {
  pendingTests: number; delayedTests: number; samplesToday: number; reviewerQueue: number; approvalQueue: number;
}
interface KpiDeltas {
  samplesThisMonth: number; samplesDeltaPercent: number; testsThisMonth: number; testsDeltaPercent: number;
  totalSamples: number; totalTests: number;
}
interface MonthlyTrendPoint { month: string; samplesLodged: number; testsLodged: number }
interface DistributionSlice { category?: string; status?: string; count: number; percent: number }

const summaryWidgets: { key: keyof DashboardSummary; label: string }[] = [
  { key: "pendingTests", label: "Pending Tests" },
  { key: "delayedTests", label: "Delayed Tests" },
  { key: "reviewerQueue", label: "Reviewer Queue" },
  { key: "approvalQueue", label: "Approval Queue" }
];

// Dashboard content restyled after the reference design (KPI cards +
// trend chart + donut breakdowns), kept in one homogeneous purple
// palette instead of the reference's mixed colors, and still living
// inside our established topbar+subnav shell.
export function DashboardPage() {
  const { role, username } = useAuth();
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [kpis, setKpis] = useState<KpiDeltas | null>(null);
  const [trend, setTrend] = useState<MonthlyTrendPoint[] | null>(null);
  const [months, setMonths] = useState(6);
  const [categoryDist, setCategoryDist] = useState<DistributionSlice[] | null>(null);
  const [statusDist, setStatusDist] = useState<DistributionSlice[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadAll = () => {
    setError(null);
    apiClient.get("/dashboard").then((r) => setSummary(r.data.data)).catch(() => setError("Could not load summary."));
    apiClient.get("/dashboard/kpi-deltas").then((r) => setKpis(r.data.data)).catch(() => {});
    apiClient.get(`/dashboard/monthly-trend?months=${months}`).then((r) => setTrend(r.data.data)).catch(() => {});
    apiClient.get("/dashboard/category-distribution").then((r) => setCategoryDist(r.data.data)).catch(() => {});
    apiClient.get("/dashboard/status-distribution").then((r) => setStatusDist(r.data.data)).catch(() => {});
  };

  useEffect(() => { loadAll(); /* eslint-disable-next-line */ }, [months]);

  if (!summary) return error ? <Alert severity="error">{error}</Alert> : <LoadingSpinner />;

  return (
    <>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
        <PageHeader title="Dashboard" subtitle={`Hello ${username}, here are your performance stats.`} />
        <Button variant="contained">Generate Report</Button>
      </Box>

      <SectionTitle>Your Stats</SectionTitle>
      {kpis && (
        <Grid container spacing={2} sx={{ mb: 1 }}>
          <Grid item xs={12} sm={6} md={3}>
            <Paper sx={{ p: 2.5 }}>
              <Typography sx={{ fontSize: 28, fontWeight: 700, color: brandColors.sectionTitle }}>{kpis.samplesThisMonth}</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>Total samples lodged</Typography>
              <Typography sx={{ fontSize: 12, color: kpis.samplesDeltaPercent >= 0 ? brandColors.ok : brandColors.err }}>
                {kpis.samplesDeltaPercent >= 0 ? "+" : ""}{kpis.samplesDeltaPercent}% since last month
              </Typography>
            </Paper>
          </Grid>
          <Grid item xs={12} sm={6} md={3}>
            <Paper sx={{ p: 2.5 }}>
              <Typography sx={{ fontSize: 28, fontWeight: 700, color: brandColors.sectionTitle }}>{kpis.testsThisMonth}</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>Total test requests lodged</Typography>
              <Typography sx={{ fontSize: 12, color: kpis.testsDeltaPercent >= 0 ? brandColors.ok : brandColors.err }}>
                {kpis.testsDeltaPercent >= 0 ? "+" : ""}{kpis.testsDeltaPercent}% since last month
              </Typography>
            </Paper>
          </Grid>
          <Grid item xs={12} sm={6} md={3}>
            <Paper sx={{ p: 2.5 }}>
              <Typography sx={{ fontSize: 28, fontWeight: 700, color: brandColors.sectionTitle }}>{kpis.totalSamples}</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>Overall samples lodged</Typography>
            </Paper>
          </Grid>
          <Grid item xs={12} sm={6} md={3}>
            <Paper sx={{ p: 2.5 }}>
              <Typography sx={{ fontSize: 28, fontWeight: 700, color: brandColors.sectionTitle }}>{kpis.totalTests}</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>Overall test requests lodged</Typography>
            </Paper>
          </Grid>
        </Grid>
      )}

      <Grid container spacing={2} sx={{ mb: 1 }}>
        <Grid item xs={12} md={7}>
          <Paper sx={{ p: 2.5, height: 320 }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
              <Typography sx={{ fontWeight: 600 }}>Samples &amp; test requests lodged</Typography>
              <Select size="small" value={months} onChange={(e) => setMonths(Number(e.target.value))}>
                <MenuItem value={3}>Last 3 months</MenuItem>
                <MenuItem value={6}>Last 6 months</MenuItem>
                <MenuItem value={12}>Last 12 months</MenuItem>
              </Select>
            </Box>
            {trend && (
              <ResponsiveContainer width="100%" height="85%">
                <BarChart data={trend}>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} />
                  <XAxis dataKey="month" fontSize={12} />
                  <YAxis fontSize={12} />
                  <Tooltip />
                  <Legend />
                  <Bar dataKey="samplesLodged" name="Total samples lodged" fill={chartPalette[1]} radius={[4, 4, 0, 0]} />
                  <Bar dataKey="testsLodged" name="Total tests requests lodged" fill={chartPalette[0]} radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            )}
          </Paper>
        </Grid>

        <Grid item xs={12} md={5}>
          <Paper sx={{ p: 2.5, height: 320 }}>
            <Typography sx={{ fontWeight: 600, mb: 1 }}>Test order status</Typography>
            {statusDist && statusDist.length > 0 ? (
              <ResponsiveContainer width="100%" height="85%">
                <PieChart>
                  <Pie data={statusDist} dataKey="count" nameKey="status" innerRadius={55} outerRadius={85} paddingAngle={2}>
                    {statusDist.map((_, i) => <Cell key={i} fill={chartPalette[i % chartPalette.length]} />)}
                  </Pie>
                  <Tooltip formatter={(value: number, name: string) => [`${value}`, name]} />
                  <Legend />
                </PieChart>
              </ResponsiveContainer>
            ) : (
              <Typography color="text.secondary" sx={{ mt: 4 }}>No data yet.</Typography>
            )}
          </Paper>
        </Grid>
      </Grid>

      <Grid container spacing={2} sx={{ mb: 1 }}>
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 2.5, height: 300 }}>
            <Typography sx={{ fontWeight: 600, mb: 1 }}>Sample categories processed</Typography>
            {categoryDist && categoryDist.length > 0 ? (
              <ResponsiveContainer width="100%" height="85%">
                <PieChart>
                  <Pie data={categoryDist} dataKey="count" nameKey="category" innerRadius={50} outerRadius={80} paddingAngle={2}>
                    {categoryDist.map((_, i) => <Cell key={i} fill={chartPalette[i % chartPalette.length]} />)}
                  </Pie>
                  <Tooltip />
                  <Legend />
                </PieChart>
              </ResponsiveContainer>
            ) : (
              <Typography color="text.secondary" sx={{ mt: 4 }}>No data yet.</Typography>
            )}
          </Paper>
        </Grid>

        <Grid item xs={12} md={6}>
          <SectionTitle>Queue Summary</SectionTitle>
          <Grid container spacing={2}>
            {summaryWidgets.map((w) => (
              <Grid item xs={6} key={w.key}>
                <Paper sx={{ p: 2, borderTop: `3px solid ${chartPalette[1]}` }}>
                  <Typography sx={{ fontSize: 24, fontWeight: 700, color: brandColors.sectionTitle }}>{summary[w.key]}</Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{w.label}</Typography>
                </Paper>
              </Grid>
            ))}
          </Grid>
        </Grid>
      </Grid>
    </>
  );
}
