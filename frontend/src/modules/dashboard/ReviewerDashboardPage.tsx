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
  IconButton,
  Tooltip,
  useTheme
} from "@mui/material";
import RateReviewOutlinedIcon from "@mui/icons-material/RateReviewOutlined";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import AccessTimeOutlinedIcon from "@mui/icons-material/AccessTimeOutlined";
import UndoOutlinedIcon from "@mui/icons-material/UndoOutlined";
import ArrowForwardOutlinedIcon from "@mui/icons-material/ArrowForwardOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import HistoryOutlinedIcon from "@mui/icons-material/HistoryOutlined";
import { Link } from "react-router-dom";
import { useAuth } from "../../contexts/AuthContext";
import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { DashboardService } from "./services/DashboardService";
import { ReviewerDashboard, ReviewerQueueItem } from "./types/dashboard";
import { brandColors } from "../../theme";

function formatAge(minutes: number): string {
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  const remainingMins = minutes % 60;
  if (hours < 24) return `${hours}h ${remainingMins}m`;
  const days = Math.floor(hours / 24);
  return `${days}d ${hours % 24}h`;
}

export function ReviewerDashboardPage() {
  const theme = useTheme();
  const { username, fullName } = useAuth();
  const displayName = fullName ?? username ?? "Reviewer";

  const [data, setData] = useState<ReviewerDashboard | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    DashboardService.getReviewerDashboard()
      .then((res) => {
        setData(res);
        setLoading(false);
      })
      .catch((err) => {
        console.error("Failed to load reviewer dashboard:", err);
        setLoading(false);
      });
  }, []);

  if (loading || !data) return <LoadingSpinner />;

  return (
    <>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2, flexWrap: "wrap", gap: 1.5 }}>
        <PageHeader
          title={`Reviewer Command Center — ${displayName}`}
          subtitle="What results are waiting for your scientific review today?"
        />
        <Button
          component={Link}
          to="/testing-workspace"
          variant="contained"
          startIcon={<ScienceOutlinedIcon />}
          sx={{ textTransform: "none", fontWeight: 600, borderRadius: 2 }}
        >
          Open Testing Workspace
        </Button>
      </Box>

      {/* Tier 1: Summary Cards */}
      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        <Grid item xs={12} sm={6} md={2.4}>
          <Paper
            component={Link}
            to="/testing-workspace?testStatus=ResultEntered"
            sx={{
              p: 2,
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
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Pending Review
              </Typography>
              <RateReviewOutlinedIcon sx={{ color: theme.palette.primary.main, fontSize: 20 }} />
            </Box>
            <Typography sx={{ fontSize: 28, fontWeight: 800, color: theme.palette.primary.main, my: 0.5 }}>
              {data.pendingReviewCount}
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
              Awaiting scientific verification
            </Typography>
          </Paper>
        </Grid>

        <Grid item xs={12} sm={6} md={2.4}>
          <Paper
            component={Link}
            to="/testing-workspace?testStatus=ResultEntered&urgency=overdue"
            sx={{
              p: 2,
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
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Overdue Review
              </Typography>
              <AccessTimeOutlinedIcon sx={{ color: brandColors.err, fontSize: 20 }} />
            </Box>
            <Typography sx={{ fontSize: 28, fontWeight: 800, color: brandColors.err, my: 0.5 }}>
              {data.overdueReviewCount}
            </Typography>
            <Typography sx={{ fontSize: 11, color: brandColors.err }}>
              Waiting &gt;24 hours
            </Typography>
          </Paper>
        </Grid>

        <Grid item xs={12} sm={6} md={2.4}>
          <Paper
            component={Link}
            to="/testing-workspace?testStatus=ResultEntered"
            sx={{
              p: 2,
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
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Due Today
              </Typography>
              <AccessTimeOutlinedIcon sx={{ color: brandColors.warn, fontSize: 20 }} />
            </Box>
            <Typography sx={{ fontSize: 28, fontWeight: 800, color: brandColors.warn, my: 0.5 }}>
              {data.dueTodayCount}
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
              Results submitted today
            </Typography>
          </Paper>
        </Grid>

        <Grid item xs={12} sm={6} md={2.4}>
          <Paper
            component={Link}
            to="/testing-workspace?testStatus=RetestRequested"
            sx={{
              p: 2,
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
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Retests / Actions
              </Typography>
              <UndoOutlinedIcon sx={{ color: brandColors.info, fontSize: 20 }} />
            </Box>
            <Typography sx={{ fontSize: 28, fontWeight: 800, color: brandColors.info, my: 0.5 }}>
              {data.returnedCount}
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
              Retest or action required
            </Typography>
          </Paper>
        </Grid>

        <Grid item xs={12} sm={6} md={2.4}>
          <Paper
            sx={{
              p: 2,
              borderLeft: `4px solid ${brandColors.ok}`,
              bgcolor: "background.paper"
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Completed Today
              </Typography>
              <CheckCircleOutlineIcon sx={{ color: brandColors.ok, fontSize: 20 }} />
            </Box>
            <Typography sx={{ fontSize: 28, fontWeight: 800, color: brandColors.ok, my: 0.5 }}>
              {data.completedTodayCount}
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
              Reviewed by you today
            </Typography>
          </Paper>
        </Grid>
      </Grid>

      {/* Tier 2: Attention Items (if any) */}
      {data.attentionItems.length > 0 && (
        <Paper sx={{ p: 2, mb: 2.5, borderLeft: `4px solid ${brandColors.err}`, bgcolor: "background.paper" }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1.5 }}>
            <WarningAmberOutlinedIcon sx={{ color: brandColors.err }} />
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: brandColors.err }}>
              Attention Required ({data.attentionItems.length})
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
                    gap: 1,
                    borderColor: "divider",
                    "&:hover": { bgcolor: "action.hover" }
                  }}
                >
                  <Box>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                      <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
                        {item.referenceNumber} — {item.subjectName}
                      </Typography>
                      <Chip label={item.testCode} size="small" sx={{ fontSize: 11, height: 20 }} />
                    </Box>
                    <Typography sx={{ fontSize: 12, color: brandColors.err, mt: 0.25 }}>
                      {item.reason}
                    </Typography>
                  </Box>
                  <Button
                    component={Link}
                    to={`/testing-workspace?sampleId=${item.sampleId}&testOrderId=${item.testOrderId}`}
                    variant="outlined"
                    color="error"
                    size="small"
                    endIcon={<ArrowForwardOutlinedIcon />}
                    sx={{ textTransform: "none", fontSize: 12, fontWeight: 600, flexShrink: 0 }}
                  >
                    Review Now
                  </Button>
                </Paper>
              </Grid>
            ))}
          </Grid>
        </Paper>
      )}

      {/* Tier 3: Central Review Queue Table */}
      <Grid container spacing={2.5}>
        <Grid item xs={12} lg={8.5}>
          <Paper sx={{ p: 2, mb: 2 }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
              <Box>
                <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main }}>
                  Central Review Queue ({data.reviewQueue.length})
                </Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  Results submitted by laboratory analysts requiring independent scientific verification.
                </Typography>
              </Box>
              <Button
                component={Link}
                to="/testing-workspace?testStatus=ResultEntered"
                variant="text"
                size="small"
                sx={{ textTransform: "none", fontWeight: 600 }}
              >
                View Full Workspace →
              </Button>
            </Box>

            {data.reviewQueue.length === 0 ? (
              <Box sx={{ py: 6, textAlign: "center" }}>
                <CheckCircleOutlineIcon sx={{ color: brandColors.ok, fontSize: 48, mb: 1 }} />
                <Typography sx={{ fontSize: 14, fontWeight: 600 }}>Review Queue is Clear</Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  No completed results are currently waiting for scientific review.
                </Typography>
              </Box>
            ) : (
              <Box sx={{ overflowX: "auto" }}>
                <Table size="small">
                  <TableHead>
                    <TableRow sx={{ bgcolor: "background.default" }}>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Sample / Ref</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Item / Location</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Test</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Analyst</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Reported Result</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Age</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Priority</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>Action</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.reviewQueue.map((row) => (
                      <TableRow
                        key={row.testOrderId}
                        hover
                      >
                        <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>
                          <Typography
                            component={Link}
                            to={`/testing-workspace?sampleId=${row.sampleId}&testOrderId=${row.testOrderId}`}
                            sx={{
                              fontSize: 12,
                              fontWeight: 700,
                              color: "text.primary",
                              textDecoration: "none",
                              "&:hover": { color: "primary.main", textDecoration: "underline" }
                            }}
                          >
                            {row.referenceNumber}
                          </Typography>
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          {row.subjectName}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          <Chip label={row.testCode} size="small" sx={{ fontSize: 11, height: 22, fontWeight: 600 }} />
                        </TableCell>
                        <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                          {row.analystName ?? "—"}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          <Typography sx={{ fontSize: 12, fontWeight: 600 }}>
                            {row.reportedValue ?? "Entered"} {row.unit ?? ""}
                          </Typography>
                          {row.resultLevel && (
                            <Chip
                              label={row.resultLevel}
                              size="small"
                              sx={{
                                fontSize: 10,
                                height: 18,
                                bgcolor:
                                  row.resultLevel === "OutOfSpecification"
                                    ? brandColors.err + "22"
                                    : row.resultLevel === "ActionLimit"
                                    ? brandColors.warn + "22"
                                    : brandColors.ok + "22",
                                color:
                                  row.resultLevel === "OutOfSpecification"
                                    ? brandColors.err
                                    : row.resultLevel === "ActionLimit"
                                    ? brandColors.warn
                                    : brandColors.ok,
                                fontWeight: 700
                              }}
                            />
                          )}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          <Typography
                            sx={{
                              fontSize: 12,
                              fontWeight: row.ageMinutes > 1440 ? 700 : 400,
                              color: row.ageMinutes > 1440 ? brandColors.err : "text.primary"
                            }}
                          >
                            {formatAge(row.ageMinutes)}
                          </Typography>
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          <Chip
                            label={row.priority}
                            size="small"
                            sx={{
                              fontSize: 10,
                              height: 20,
                              fontWeight: 700,
                              bgcolor:
                                row.priority === "High"
                                  ? brandColors.err + "22"
                                  : row.priority === "Medium"
                                  ? brandColors.warn + "22"
                                  : "action.selected",
                              color:
                                row.priority === "High"
                                  ? brandColors.err
                                  : row.priority === "Medium"
                                  ? brandColors.warn
                                  : "text.secondary"
                            }}
                          />
                        </TableCell>
                        <TableCell sx={{ textAlign: "right" }}>
                          <Button
                            component={Link}
                            to={`/testing-workspace?sampleId=${row.sampleId}&testOrderId=${row.testOrderId}`}
                            variant="contained"
                            size="small"
                            startIcon={<RateReviewOutlinedIcon />}
                            sx={{ textTransform: "none", fontSize: 11, fontWeight: 700, py: 0.3 }}
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

        {/* Tier 4: Recently Reviewed Panel */}
        <Grid item xs={12} lg={3.5}>
          <Paper sx={{ p: 2 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1.5 }}>
              <HistoryOutlinedIcon sx={{ color: theme.palette.primary.main }} />
              <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
                Recently Reviewed
              </Typography>
            </Box>
            <Typography sx={{ fontSize: 11, color: "text.secondary", mb: 2 }}>
              Historical scientific review audit trail.
            </Typography>

            {data.recentlyReviewed.length === 0 ? (
              <Typography sx={{ fontSize: 12, color: "text.secondary", py: 3, textAlign: "center" }}>
                No recent review history.
              </Typography>
            ) : (
              <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
                {data.recentlyReviewed.map((rec, idx) => (
                  <Paper
                    key={idx}
                    component={Link}
                    to={`/testing-workspace?sampleId=${rec.sampleId}&testOrderId=${rec.testOrderId}`}
                    variant="outlined"
                    sx={{
                      p: 1.5,
                      cursor: "pointer",
                      display: "block",
                      textDecoration: "none",
                      color: "inherit",
                      "&:hover": { bgcolor: "action.hover" }
                    }}
                  >
                    <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                      <Box>
                        <Typography sx={{ fontSize: 12, fontWeight: 700 }}>
                          {rec.referenceNumber}
                        </Typography>
                        <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                          {rec.subjectName}
                        </Typography>
                      </Box>
                      <Chip
                        label={rec.status}
                        size="small"
                        sx={{ fontSize: 10, height: 18, fontWeight: 700 }}
                      />
                    </Box>
                    <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 1 }}>
                      <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                        Tests: {rec.testCode}
                      </Typography>
                      <Typography sx={{ fontSize: 10, color: "text.secondary" }}>
                        {new Date(rec.reviewedAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                      </Typography>
                    </Box>
                  </Paper>
                ))}
              </Box>
            )}
          </Paper>
        </Grid>
      </Grid>
    </>
  );
}
