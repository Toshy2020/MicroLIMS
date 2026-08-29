import { useEffect, useState } from "react";
import {
  Box,
  Grid,
  Paper,
  Typography,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Button,
  Chip,
  LinearProgress,
  useTheme
} from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import ThermostatOutlinedIcon from "@mui/icons-material/ThermostatOutlined";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import RateReviewOutlinedIcon from "@mui/icons-material/RateReviewOutlined";
import VerifiedUserOutlinedIcon from "@mui/icons-material/VerifiedUserOutlined";
import AccessTimeOutlinedIcon from "@mui/icons-material/AccessTimeOutlined";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import ArrowForwardOutlinedIcon from "@mui/icons-material/ArrowForwardOutlined";
import PeopleAltOutlinedIcon from "@mui/icons-material/PeopleAltOutlined";
import TrendingUpOutlinedIcon from "@mui/icons-material/TrendingUpOutlined";
import { useNavigate, Link } from "react-router-dom";
import { useAuth } from "../../contexts/AuthContext";
import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { DashboardService } from "./services/DashboardService";
import { SectionHeadDashboard, MonthlyTrendPoint, DistributionSlice } from "./types/dashboard";
import { SamplesTrendChart } from "./components/SamplesTrendChart";
import { TestOrderStatusDonut } from "./components/TestOrderStatusDonut";
import { brandColors } from "../../theme";

export function SectionHeadDashboardPage() {
  const theme = useTheme();
  const navigate = useNavigate();
  const { username, fullName } = useAuth();
  const displayName = fullName ?? username ?? "Section Head";

  const [data, setData] = useState<SectionHeadDashboard | null>(null);
  const [months, setMonths] = useState(6);
  const [trend, setTrend] = useState<MonthlyTrendPoint[] | null>(null);
  const [statusDist, setStatusDist] = useState<DistributionSlice[] | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      DashboardService.getSectionHeadDashboard().catch(() => null),
      DashboardService.getMonthlyTrend(months).catch(() => null),
      DashboardService.getStatusDistribution().catch(() => null)
    ]).then(([dashData, trendData, distData]) => {
      setData(dashData);
      setTrend(trendData);
      setStatusDist(distData);
      setLoading(false);
    });
  }, [months]);

  if (loading || !data) return <LoadingSpinner />;

  const totalBottleneck =
    data.testingBottleneck +
    data.incubationBottleneck +
    data.readyToReadBottleneck +
    data.reviewBottleneck +
    data.approvalBottleneck || 1;

  return (
    <>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2, flexWrap: "wrap", gap: 1.5 }}>
        <PageHeader
          title={`Section Head Command Center — ${displayName}`}
          subtitle="Laboratory-wide operational overview, workflow bottlenecks, and intervention tracking."
        />
        <Box sx={{ display: "flex", gap: 1.5, flexWrap: "wrap" }}>
          <Button
            component={Link}
            to="/receiving-testing"
            variant="contained"
            startIcon={<ScienceOutlinedIcon />}
            sx={{ textTransform: "none", fontWeight: 600, borderRadius: 2 }}
          >
            Receiving & Testing Workspace
          </Button>
        </Box>
      </Box>

      {/* Tier 1: Laboratory Overview KPI Strip */}
      <Grid container spacing={1.5} sx={{ mb: 2.5 }}>
        <Grid item xs={6} sm={4} md={1.71}>
          <Paper
            component={Link}
            to="/testing-workspace?status=Active"
            sx={{
              p: 1.75,
              cursor: "pointer",
              display: "block",
              textDecoration: "none",
              color: "inherit",
              borderLeft: `4px solid ${theme.palette.primary.main}`,
              transition: "transform 0.15s, box-shadow 0.15s",
              "&:hover": { transform: "translateY(-2px)", boxShadow: 3 }
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Active Tests
              </Typography>
              <ScienceOutlinedIcon sx={{ color: theme.palette.primary.main, fontSize: 18 }} />
            </Box>
            <Typography sx={{ fontSize: 24, fontWeight: 800, color: theme.palette.primary.main, my: 0.25 }}>
              {data.activeTests}
            </Typography>
            <Typography sx={{ fontSize: 10, color: "text.secondary" }}>In progress</Typography>
          </Paper>
        </Grid>

        <Grid item xs={6} sm={4} md={1.71}>
          <Paper
            component={Link}
            to="/testing-workspace?view=kanban"
            sx={{
              p: 1.75,
              cursor: "pointer",
              display: "block",
              textDecoration: "none",
              color: "inherit",
              borderLeft: `4px solid ${brandColors.info}`,
              transition: "transform 0.15s, box-shadow 0.15s",
              "&:hover": { transform: "translateY(-2px)", boxShadow: 3 }
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Incubating
              </Typography>
              <ThermostatOutlinedIcon sx={{ color: brandColors.info, fontSize: 18 }} />
            </Box>
            <Typography sx={{ fontSize: 24, fontWeight: 800, color: brandColors.info, my: 0.25 }}>
              {data.incubating}
            </Typography>
            <Typography sx={{ fontSize: 10, color: "text.secondary" }}>Active chambers</Typography>
          </Paper>
        </Grid>

        <Grid item xs={6} sm={4} md={1.71}>
          <Paper
            component={Link}
            to="/testing-workspace?testStatus=ReadyToRead"
            sx={{
              p: 1.75,
              cursor: "pointer",
              display: "block",
              textDecoration: "none",
              color: "inherit",
              borderLeft: `4px solid ${brandColors.ok}`,
              transition: "transform 0.15s, box-shadow 0.15s",
              "&:hover": { transform: "translateY(-2px)", boxShadow: 3 }
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Ready to Read
              </Typography>
              <VisibilityOutlinedIcon sx={{ color: brandColors.ok, fontSize: 18 }} />
            </Box>
            <Typography sx={{ fontSize: 24, fontWeight: 800, color: brandColors.ok, my: 0.25 }}>
              {data.readyToRead}
            </Typography>
            <Typography sx={{ fontSize: 10, color: "text.secondary" }}>Readings pending</Typography>
          </Paper>
        </Grid>

        <Grid item xs={6} sm={4} md={1.71}>
          <Paper
            component={Link}
            to="/testing-workspace?testStatus=ResultEntered"
            sx={{
              p: 1.75,
              cursor: "pointer",
              display: "block",
              textDecoration: "none",
              color: "inherit",
              borderLeft: `4px solid ${brandColors.warn}`,
              transition: "transform 0.15s, box-shadow 0.15s",
              "&:hover": { transform: "translateY(-2px)", boxShadow: 3 }
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Review Queue
              </Typography>
              <RateReviewOutlinedIcon sx={{ color: brandColors.warn, fontSize: 18 }} />
            </Box>
            <Typography sx={{ fontSize: 24, fontWeight: 800, color: brandColors.warn, my: 0.25 }}>
              {data.pendingReview}
            </Typography>
            <Typography sx={{ fontSize: 10, color: "text.secondary" }}>Awaiting review</Typography>
          </Paper>
        </Grid>

        <Grid item xs={6} sm={4} md={1.71}>
          <Paper
            component={Link}
            to="/testing-workspace?testStatus=Reviewed"
            sx={{
              p: 1.75,
              cursor: "pointer",
              display: "block",
              textDecoration: "none",
              color: "inherit",
              borderLeft: `4px solid ${brandColors.ok}`,
              transition: "transform 0.15s, box-shadow 0.15s",
              "&:hover": { transform: "translateY(-2px)", boxShadow: 3 }
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Approval Queue
              </Typography>
              <VerifiedUserOutlinedIcon sx={{ color: brandColors.ok, fontSize: 18 }} />
            </Box>
            <Typography sx={{ fontSize: 24, fontWeight: 800, color: brandColors.ok, my: 0.25 }}>
              {data.pendingApproval}
            </Typography>
            <Typography sx={{ fontSize: 10, color: "text.secondary" }}>Awaiting release</Typography>
          </Paper>
        </Grid>

        <Grid item xs={6} sm={4} md={1.71}>
          <Paper
            component={Link}
            to="/testing-workspace?urgency=overdue"
            sx={{
              p: 1.75,
              cursor: "pointer",
              display: "block",
              textDecoration: "none",
              color: "inherit",
              borderLeft: `4px solid ${brandColors.err}`,
              transition: "transform 0.15s, box-shadow 0.15s",
              "&:hover": { transform: "translateY(-2px)", boxShadow: 3 }
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Overdue
              </Typography>
              <AccessTimeOutlinedIcon sx={{ color: brandColors.err, fontSize: 18 }} />
            </Box>
            <Typography sx={{ fontSize: 24, fontWeight: 800, color: brandColors.err, my: 0.25 }}>
              {data.overdue}
            </Typography>
            <Typography sx={{ fontSize: 10, color: brandColors.err }}>&gt;24h delay</Typography>
          </Paper>
        </Grid>

        <Grid item xs={12} sm={4} md={1.71}>
          <Paper
            onClick={() => {
              const el = document.getElementById("attention-section");
              if (el) el.scrollIntoView({ behavior: "smooth" });
            }}
            sx={{
              p: 1.75,
              cursor: "pointer",
              borderLeft: `4px solid ${data.attentionCount > 0 ? brandColors.err : brandColors.ok}`,
              transition: "transform 0.15s, box-shadow 0.15s",
              "&:hover": { transform: "translateY(-2px)", boxShadow: 3 }
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Attention Items
              </Typography>
              <WarningAmberOutlinedIcon sx={{ color: data.attentionCount > 0 ? brandColors.err : brandColors.ok, fontSize: 18 }} />
            </Box>
            <Typography sx={{ fontSize: 24, fontWeight: 800, color: data.attentionCount > 0 ? brandColors.err : brandColors.ok, my: 0.25 }}>
              {data.attentionCount}
            </Typography>
            <Typography sx={{ fontSize: 10, color: "text.secondary" }}>Action required</Typography>
          </Paper>
        </Grid>
      </Grid>

      {/* Tier 2: Workflow Bottlenecks Pipeline */}
      <Paper sx={{ p: 2, mb: 2.5 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
          <Box>
            <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
              Laboratory Workflow Pipeline & Bottlenecks
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              Click any stage to inspect the active test orders in that stage.
            </Typography>
          </Box>
        </Box>

        <Grid container spacing={1.5}>
          {[
            { label: "1. Testing / Preparation", count: data.testingBottleneck, color: theme.palette.primary.main, link: "/testing-workspace?status=Active" },
            { label: "2. Incubation", count: data.incubationBottleneck, color: brandColors.info, link: "/testing-workspace" },
            { label: "3. Ready to Read", count: data.readyToReadBottleneck, color: brandColors.ok, link: "/testing-workspace?testStatus=ReadyToRead" },
            { label: "4. Scientific Review", count: data.reviewBottleneck, color: brandColors.warn, link: "/testing-workspace?testStatus=ResultEntered" },
            { label: "5. Final Approval", count: data.approvalBottleneck, color: brandColors.ok, link: "/testing-workspace?testStatus=Reviewed" }
          ].map((stage, idx) => (
            <Grid item xs={12} sm={6} md={2.4} key={idx}>
              <Paper
                component={Link}
                to={stage.link}
                variant="outlined"
                sx={{
                  p: 1.5,
                  cursor: "pointer",
                  display: "block",
                  textDecoration: "none",
                  color: "inherit",
                  borderColor: "divider",
                  transition: "background-color 0.15s, border-color 0.15s",
                  "&:hover": { bgcolor: "action.hover", borderColor: stage.color }
                }}
              >
                <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary" }}>
                  {stage.label}
                </Typography>
                <Typography sx={{ fontSize: 22, fontWeight: 800, color: stage.color, my: 0.5 }}>
                  {stage.count}
                </Typography>
                <LinearProgress
                  variant="determinate"
                  value={Math.min(100, Math.round((stage.count / totalBottleneck) * 100))}
                  sx={{
                    height: 6,
                    borderRadius: 3,
                    bgcolor: "action.selected",
                    "& .MuiLinearProgress-bar": { bgcolor: stage.color }
                  }}
                />
              </Paper>
            </Grid>
          ))}
        </Grid>
      </Paper>

      {/* Tier 3: Attention Required (Actionable Section) */}
      <Box id="attention-section" sx={{ mb: 2.5 }}>
        {data.attentionItems.length > 0 ? (
          <Paper sx={{ p: 2, borderLeft: `4px solid ${brandColors.err}` }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1.5 }}>
              <WarningAmberOutlinedIcon sx={{ color: brandColors.err }} />
              <Typography sx={{ fontSize: 15, fontWeight: 700, color: brandColors.err }}>
                Laboratory Attention Required ({data.attentionItems.length})
              </Typography>
            </Box>
            <Grid container spacing={1.5}>
              {data.attentionItems.map((item, idx) => (
                <Grid item xs={12} md={6} key={idx}>
                  <Paper
                    variant="outlined"
                    sx={{
                      p: 1.5,
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center",
                      gap: 1.5,
                      borderColor: "divider",
                      "&:hover": { bgcolor: "action.hover" }
                    }}
                  >
                    <Box>
                      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                        <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
                          {item.referenceNumber} — {item.subjectName}
                        </Typography>
                        <Chip label={item.testCode} size="small" sx={{ fontSize: 10, height: 20 }} />
                      </Box>
                      <Typography sx={{ fontSize: 12, color: brandColors.err, mt: 0.25 }}>
                        {item.reason}
                      </Typography>
                    </Box>
                    <Button
                      component={Link}
                      to={item.testOrderId ? `/testing-workspace?sampleId=${item.sampleId}&testOrderId=${item.testOrderId}` : `/testing-workspace?sampleId=${item.sampleId}`}
                      variant="outlined"
                      color="error"
                      size="small"
                      endIcon={<ArrowForwardOutlinedIcon />}
                      sx={{ textTransform: "none", fontSize: 12, fontWeight: 600, flexShrink: 0 }}
                    >
                      Intervene
                    </Button>
                  </Paper>
                </Grid>
              ))}
            </Grid>
          </Paper>
        ) : (
          <Paper sx={{ p: 2, display: "flex", alignItems: "center", gap: 1.5, borderLeft: `4px solid ${brandColors.ok}` }}>
            <VerifiedUserOutlinedIcon sx={{ color: brandColors.ok }} />
            <Box>
              <Typography sx={{ fontSize: 14, fontWeight: 700, color: brandColors.ok }}>
                All Laboratory Workflows Running Within Normal SLA
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                No overdue test orders, delayed reviews, or unresolved critical exceptions currently require section head intervention.
              </Typography>
            </Box>
          </Paper>
        )}
      </Box>

      {/* Tier 4: Review Queue & Approval Queue Summaries */}
      <Grid container spacing={2.5} sx={{ mb: 2.5 }}>
        {/* Review Queue Summary */}
        <Grid item xs={12} lg={6}>
          <Paper sx={{ p: 2, height: "100%", display: "flex", flexDirection: "column" }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
              <Box>
                <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
                  Review Queue ({data.reviewQueueCount})
                </Typography>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                  Overdue: {data.reviewQueueOverdueCount} | Oldest: {data.reviewQueueOldestHours}h
                </Typography>
              </Box>
              <Button
                component={Link}
                to="/testing-workspace?testStatus=ResultEntered"
                variant="text"
                size="small"
                sx={{ textTransform: "none", fontWeight: 600, fontSize: 12 }}
              >
                View Full Review Queue →
              </Button>
            </Box>

            {data.reviewQueueItems.length === 0 ? (
              <Typography sx={{ fontSize: 12, color: "text.secondary", py: 4, textAlign: "center" }}>
                No results awaiting scientific review.
              </Typography>
            ) : (
              <Box sx={{ overflowX: "auto", flex: 1 }}>
                <Table size="small">
                  <TableHead>
                    <TableRow sx={{ bgcolor: "background.default" }}>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Sample / Ref</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Test</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Analyst</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Age</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11, textAlign: "right" }}>Action</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.reviewQueueItems.slice(0, 5).map((row) => (
                      <TableRow
                        key={row.testOrderId}
                        hover
                      >
                        <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>
                          <Typography
                            component={Link}
                            to={`/testing-workspace?sampleId=${row.sampleId}&testOrderId=${row.testOrderId}`}
                            sx={{
                              fontSize: 11,
                              fontWeight: 700,
                              color: "text.primary",
                              textDecoration: "none",
                              "&:hover": { color: "primary.main", textDecoration: "underline" }
                            }}
                          >
                            {row.referenceNumber}
                          </Typography>
                        </TableCell>
                        <TableCell sx={{ fontSize: 11 }}><Chip label={row.testCode} size="small" sx={{ fontSize: 10, height: 18 }} /></TableCell>
                        <TableCell sx={{ fontSize: 11, color: "text.secondary" }}>{row.analystName ?? "—"}</TableCell>
                        <TableCell sx={{ fontSize: 11, color: row.ageHours >= 24 ? brandColors.err : "text.primary" }}>{row.ageHours}h</TableCell>
                        <TableCell sx={{ textAlign: "right" }}>
                          <Button
                            component={Link}
                            to={`/testing-workspace?sampleId=${row.sampleId}&testOrderId=${row.testOrderId}`}
                            variant="outlined"
                            size="small"
                            sx={{ textTransform: "none", fontSize: 10, py: 0.2, fontWeight: 600 }}
                          >
                            Review
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
            )}
          </Paper>
        </Grid>

        {/* Approval Queue Summary */}
        <Grid item xs={12} lg={6}>
          <Paper sx={{ p: 2, height: "100%", display: "flex", flexDirection: "column" }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
              <Box>
                <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
                  Approval Queue ({data.approvalQueueCount})
                </Typography>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                  Overdue: {data.approvalQueueOverdueCount} | Oldest: {data.approvalQueueOldestHours}h
                </Typography>
              </Box>
              <Button
                component={Link}
                to="/testing-workspace?testStatus=Reviewed"
                variant="text"
                size="small"
                sx={{ textTransform: "none", fontWeight: 600, fontSize: 12 }}
              >
                View Full Approval Queue →
              </Button>
            </Box>

            {data.approvalQueueItems.length === 0 ? (
              <Typography sx={{ fontSize: 12, color: "text.secondary", py: 4, textAlign: "center" }}>
                No samples awaiting final authorization.
              </Typography>
            ) : (
              <Box sx={{ overflowX: "auto", flex: 1 }}>
                <Table size="small">
                  <TableHead>
                    <TableRow sx={{ bgcolor: "background.default" }}>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Sample / Ref</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Test</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Reviewer</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Age</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 11, textAlign: "right" }}>Action</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.approvalQueueItems.slice(0, 5).map((row) => (
                      <TableRow
                        key={row.testOrderId}
                        hover
                      >
                        <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>
                          <Typography
                            component={Link}
                            to={`/testing-workspace?sampleId=${row.sampleId}&testOrderId=${row.testOrderId}`}
                            sx={{
                              fontSize: 11,
                              fontWeight: 700,
                              color: "text.primary",
                              textDecoration: "none",
                              "&:hover": { color: "primary.main", textDecoration: "underline" }
                            }}
                          >
                            {row.referenceNumber}
                          </Typography>
                        </TableCell>
                        <TableCell sx={{ fontSize: 11 }}><Chip label={row.testCode} size="small" sx={{ fontSize: 10, height: 18 }} /></TableCell>
                        <TableCell sx={{ fontSize: 11, color: "text.secondary" }}>{row.reviewerName ?? "—"}</TableCell>
                        <TableCell sx={{ fontSize: 11, color: row.ageHours >= 24 ? brandColors.err : "text.primary" }}>{row.ageHours}h</TableCell>
                        <TableCell sx={{ textAlign: "right" }}>
                          <Button
                            component={Link}
                            to={`/testing-workspace?sampleId=${row.sampleId}&testOrderId=${row.testOrderId}`}
                            variant="outlined"
                            size="small"
                            color="success"
                            sx={{ textTransform: "none", fontSize: 10, py: 0.2, fontWeight: 600 }}
                          >
                            Approve
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
            )}
          </Paper>
        </Grid>
      </Grid>

      {/* Tier 5: Laboratory Workload & Incubation Status */}
      <Grid container spacing={2.5} sx={{ mb: 2.5 }}>
        {/* Analyst Workload Allocation */}
        <Grid item xs={12} lg={7}>
          <Paper sx={{ p: 2 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
              <PeopleAltOutlinedIcon sx={{ color: theme.palette.primary.main }} />
              <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
                Laboratory Analyst Workload Allocation
              </Typography>
            </Box>
            <Typography sx={{ fontSize: 11, color: "text.secondary", mb: 2 }}>
              Active test assignment and daily operational capacity across laboratory staff. Click an analyst to filter workspace.
            </Typography>

            {data.analystWorkloads.length === 0 ? (
              <Typography sx={{ fontSize: 12, color: "text.secondary", py: 3, textAlign: "center" }}>
                No active laboratory analysts found.
              </Typography>
            ) : (
              <Box sx={{ overflowX: "auto" }}>
                <Table size="small">
                  <TableHead>
                    <TableRow sx={{ bgcolor: "background.default" }}>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Analyst</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Active Assigned Tests</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Overdue Tests</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Completed Today</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>Workspace</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.analystWorkloads.map((a) => (
                      <TableRow
                        key={a.analystId}
                        hover
                      >
                        <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>
                          <Typography
                            component={Link}
                            to={`/testing-workspace?analystId=${a.analystId}`}
                            sx={{
                              fontSize: 12,
                              fontWeight: 700,
                              color: "text.primary",
                              textDecoration: "none",
                              "&:hover": { color: "primary.main", textDecoration: "underline" }
                            }}
                          >
                            {a.analystName}
                          </Typography>
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          <Chip
                            label={`${a.activeCount} tests`}
                            size="small"
                            sx={{ fontSize: 11, fontWeight: 600, bgcolor: "action.selected" }}
                          />
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          {a.overdueCount > 0 ? (
                            <Chip
                              component={Link}
                              to={`/testing-workspace?analystId=${a.analystId}&urgency=overdue`}
                              label={`${a.overdueCount} overdue`}
                              size="small"
                              clickable
                              sx={{ fontSize: 11, fontWeight: 700, bgcolor: brandColors.err + "22", color: brandColors.err }}
                            />
                          ) : (
                            <Typography sx={{ fontSize: 12, color: brandColors.ok, fontWeight: 600 }}>0</Typography>
                          )}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12, fontWeight: 600, color: brandColors.ok }}>
                          {a.completedTodayCount}
                        </TableCell>
                        <TableCell sx={{ textAlign: "right" }}>
                          <Button
                            component={Link}
                            to={`/testing-workspace?analystId=${a.analystId}`}
                            variant="text"
                            size="small"
                            sx={{ textTransform: "none", fontSize: 11, fontWeight: 600 }}
                          >
                            Filter Tests →
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
            )}
          </Paper>
        </Grid>

        {/* Incubation Summary */}
        <Grid item xs={12} lg={5}>
          <Paper sx={{ p: 2 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
              <ThermostatOutlinedIcon sx={{ color: theme.palette.primary.main }} />
              <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
                Laboratory Incubation Status
              </Typography>
            </Box>
            <Typography sx={{ fontSize: 11, color: "text.secondary", mb: 2 }}>
              Active microbiological test incubations grouped by test type.
            </Typography>

            {data.incubationSummary.length === 0 ? (
              <Typography sx={{ fontSize: 12, color: "text.secondary", py: 3, textAlign: "center" }}>
                No active test incubations.
              </Typography>
            ) : (
              <Box sx={{ overflowX: "auto" }}>
                <Table size="small">
                  <TableHead>
                    <TableRow sx={{ bgcolor: "background.default" }}>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Test Type</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Ready to Read</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Still Incubating</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>Action</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.incubationSummary.map((inc) => (
                      <TableRow
                        key={inc.testCode}
                        hover
                      >
                        <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>
                          <Chip label={inc.testCode} size="small" sx={{ fontSize: 11, fontWeight: 600 }} />
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          {inc.readyToRead > 0 ? (
                            <Chip
                              label={`${inc.readyToRead} ready`}
                              size="small"
                              sx={{ fontSize: 11, fontWeight: 700, bgcolor: brandColors.ok + "22", color: brandColors.ok }}
                            />
                          ) : (
                            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>0</Typography>
                          )}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                          {inc.incubating}
                        </TableCell>
                        <TableCell sx={{ textAlign: "right" }}>
                          <Button
                            component={Link}
                            to={`/testing-workspace?testStatus=${inc.readyToRead > 0 ? "ReadyToRead" : ""}`}
                            variant="text"
                            size="small"
                            sx={{ textTransform: "none", fontSize: 11, fontWeight: 600 }}
                          >
                            Open →
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
            )}
          </Paper>
        </Grid>
      </Grid>

      {/* Tier 6: Laboratory Trends & Distribution */}
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid item xs={12} md={8}>
          <SamplesTrendChart trend={trend} months={months} onMonthsChange={setMonths} />
        </Grid>
        <Grid item xs={12} md={4}>
          <TestOrderStatusDonut statusDist={statusDist} />
        </Grid>
      </Grid>
    </>
  );
}
