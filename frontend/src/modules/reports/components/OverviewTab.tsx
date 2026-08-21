import { useEffect, useState } from "react";
import { Grid, Paper, Typography, Box, Stack, Button, Table, TableHead, TableRow, TableCell, TableBody, Chip, Tooltip, useTheme } from "@mui/material";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import SearchIcon from "@mui/icons-material/Search";
import PostAddIcon from "@mui/icons-material/PostAdd";
import ShowChartIcon from "@mui/icons-material/ShowChart";
import FolderSharedIcon from "@mui/icons-material/FolderShared";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import HourglassEmptyIcon from "@mui/icons-material/HourglassEmpty";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";
import { ResponsiveContainer, PieChart, Pie, Cell, BarChart, Bar, XAxis, YAxis, Tooltip as RechartsTooltip, CartesianGrid } from "recharts";
import { OverviewDashboardData, OverviewKpiCardData } from "../types/reportingTypes";
import { OverviewService } from "../services/OverviewService";
import { brandColors } from "../../../theme";

interface OverviewTabProps {
  fromDate?: string;
  toDate?: string;
  onNavigateTab: (tabIndex: number) => void;
}

function KpiCard({ data }: { data: OverviewKpiCardData }) {
  const isUp = data.deltaDirection === "up";
  const isError = data.variant === "error";
  const isWarning = data.variant === "warning";

  let deltaColor = isUp ? brandColors.ok : brandColors.err;
  if (isError) deltaColor = brandColors.err;
  if (isWarning) deltaColor = brandColors.badgePM;

  const content = (
    <Paper sx={{ p: 2, height: "100%", display: "flex", flexDirection: "column", justifyContent: "space-between" }}>
      <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: "text.secondary" }}>
        {data.title}
      </Typography>
      <Typography sx={{ fontSize: 26, fontWeight: 800, color: isError ? brandColors.err : brandColors.sectionTitle, my: 0.5 }}>
        {data.value}
      </Typography>
      <Box sx={{ display: "flex", alignItems: "center", gap: 0.5, flexWrap: "wrap" }}>
        <Box sx={{ display: "flex", alignItems: "center", color: deltaColor, fontSize: 11, fontWeight: 700 }}>
          {isUp ? <ArrowUpwardIcon sx={{ fontSize: 13 }} /> : <ArrowDownwardIcon sx={{ fontSize: 13 }} />}
          {Math.abs(data.deltaPercent)}%
        </Box>
        <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
          {data.comparisonLabel}
        </Typography>
      </Box>
    </Paper>
  );

  return data.tooltip ? (
    <Tooltip title={data.tooltip} arrow>
      {content}
    </Tooltip>
  ) : (
    content
  );
}

export function OverviewTab({ fromDate, toDate, onNavigateTab }: OverviewTabProps) {
  const theme = useTheme();
  const [data, setData] = useState<OverviewDashboardData | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    OverviewService.getOverviewData(fromDate, toDate)
      .then(setData)
      .finally(() => setLoading(false));
  }, [fromDate, toDate]);

  if (!data) return null;

  return (
    <Box sx={{ pb: 3 }}>
      {/* Top 6 KPI Cards */}
      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        <Grid item xs={12} sm={6} md={2}>
          <KpiCard data={data.totalTests} />
        </Grid>
        <Grid item xs={12} sm={6} md={2}>
          <KpiCard data={data.approvedResults} />
        </Grid>
        <Grid item xs={12} sm={6} md={2}>
          <KpiCard data={data.pendingReview} />
        </Grid>
        <Grid item xs={12} sm={6} md={2}>
          <KpiCard data={data.pendingApproval} />
        </Grid>
        <Grid item xs={12} sm={6} md={2}>
          <KpiCard data={data.outOfSpec} />
        </Grid>
        <Grid item xs={12} sm={6} md={2}>
          <KpiCard data={data.alertActionLevel} />
        </Grid>
      </Grid>

      {/* Middle Row: 3 Visualizations */}
      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        {/* Results by Category */}
        <Grid item xs={12} md={4}>
          <Paper sx={{ p: 2.5, height: 340, display: "flex", flexDirection: "column" }}>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main, mb: 1 }}>
              Results by Category
            </Typography>
            <Box sx={{ display: "flex", alignItems: "center", flex: 1 }}>
              <Box sx={{ width: "50%", height: 220, position: "relative" }}>
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={data.categoryDistribution}
                      dataKey="count"
                      nameKey="label"
                      innerRadius={55}
                      outerRadius={80}
                      paddingAngle={2}
                    >
                      {data.categoryDistribution.map((entry, idx) => (
                        <Cell key={idx} fill={entry.color} />
                      ))}
                    </Pie>
                    <RechartsTooltip formatter={(val: number, name: string) => [`${val} tests`, name]} />
                  </PieChart>
                </ResponsiveContainer>
                <Box sx={{
                  position: "absolute", top: "50%", left: "50%", transform: "translate(-50%, -50%)",
                  textAlign: "center", pointerEvents: "none"
                }}>
                  <Typography sx={{ fontSize: 16, fontWeight: 800, color: theme.palette.primary.main, lineHeight: 1 }}>
                    1,284
                  </Typography>
                  <Typography sx={{ fontSize: 10, color: "text.secondary" }}>Total</Typography>
                </Box>
              </Box>

              <Box sx={{ width: "50%", pl: 1 }}>
                <Stack spacing={0.75}>
                  {data.categoryDistribution.map((item, idx) => (
                    <Box key={idx} sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", fontSize: 11.5 }}>
                      <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, minWidth: 0 }}>
                        <Box sx={{ width: 8, height: 8, borderRadius: "50%", bgcolor: item.color, flexShrink: 0 }} />
                        <Typography sx={{ fontSize: 11.5, color: "text.secondary" }} noWrap>{item.label}</Typography>
                      </Box>
                      <Typography sx={{ fontSize: 11.5, fontWeight: 600 }}>
                        {item.percentage}% ({item.count})
                      </Typography>
                    </Box>
                  ))}
                </Stack>
              </Box>
            </Box>
          </Paper>
        </Grid>

        {/* Results by Test */}
        <Grid item xs={12} md={4}>
          <Paper sx={{ p: 2.5, height: 340, display: "flex", flexDirection: "column" }}>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main, mb: 1 }}>
              Results by Test
            </Typography>
            <Box sx={{ flex: 1, height: 250 }}>
              <ResponsiveContainer width="100%" height="100%">
                <BarChart
                  data={data.testDistribution}
                  layout="vertical"
                  margin={{ top: 5, right: 30, left: 40, bottom: 5 }}
                >
                  <CartesianGrid strokeDasharray="3 3" stroke={theme.palette.divider} />
                  <XAxis type="number" hide />
                  <YAxis type="category" dataKey="testName" tick={{ fontSize: 11, fill: theme.palette.text.secondary }} width={80} />
                  <RechartsTooltip formatter={(val: number) => [`${val} tests`, "Volume"]} />
                  <Bar dataKey="count" fill={theme.palette.primary.main} radius={[0, 4, 4, 0]} barSize={14} />
                </BarChart>
              </ResponsiveContainer>
            </Box>
          </Paper>
        </Grid>

        {/* Results by Location / Point */}
        <Grid item xs={12} md={4}>
          <Paper sx={{ p: 2.5, height: 340, display: "flex", flexDirection: "column" }}>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main, mb: 1 }}>
              Results by Location / Point
            </Typography>
            <Box sx={{ display: "flex", alignItems: "center", flex: 1 }}>
              <Box sx={{ width: "50%", height: 220, position: "relative" }}>
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={data.locationDistribution}
                      dataKey="count"
                      nameKey="location"
                      innerRadius={55}
                      outerRadius={80}
                      paddingAngle={2}
                    >
                      {data.locationDistribution.map((entry, idx) => (
                        <Cell key={idx} fill={entry.color} />
                      ))}
                    </Pie>
                    <RechartsTooltip formatter={(val: number, name: string) => [`${val} tests`, name]} />
                  </PieChart>
                </ResponsiveContainer>
                <Box sx={{
                  position: "absolute", top: "50%", left: "50%", transform: "translate(-50%, -50%)",
                  textAlign: "center", pointerEvents: "none"
                }}>
                  <Typography sx={{ fontSize: 16, fontWeight: 800, color: theme.palette.primary.main, lineHeight: 1 }}>
                    1,284
                  </Typography>
                  <Typography sx={{ fontSize: 10, color: "text.secondary" }}>Total</Typography>
                </Box>
              </Box>

              <Box sx={{ width: "50%", pl: 1 }}>
                <Stack spacing={1}>
                  {data.locationDistribution.map((item, idx) => (
                    <Box key={idx} sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", fontSize: 11.5 }}>
                      <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, minWidth: 0 }}>
                        <Box sx={{ width: 8, height: 8, borderRadius: "50%", bgcolor: item.color, flexShrink: 0 }} />
                        <Typography sx={{ fontSize: 11.5, color: "text.secondary" }} noWrap>{item.location}</Typography>
                      </Box>
                      <Typography sx={{ fontSize: 11.5, fontWeight: 600 }}>
                        {item.percentage}% ({item.count})
                      </Typography>
                    </Box>
                  ))}
                </Stack>
              </Box>
            </Box>
          </Paper>
        </Grid>
      </Grid>

      {/* Bottom Row: Recent Reports, Quality Signals, Quick Actions */}
      <Grid container spacing={2}>
        {/* Recent Reports */}
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 2.5, height: "100%" }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
              <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main }}>
                Recent Reports
              </Typography>
              <Button size="small" onClick={() => onNavigateTab(4)}>View All</Button>
            </Box>
            <Table size="small" sx={{ "& th": { fontWeight: 700, fontSize: 11.5 }, "& td": { fontSize: 12 } }}>
              <TableHead>
                <TableRow>
                  <TableCell>Report Name</TableCell>
                  <TableCell>Type</TableCell>
                  <TableCell>Date Generated</TableCell>
                  <TableCell>Generated By</TableCell>
                  <TableCell>Status</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.recentReports.map((r) => (
                  <TableRow key={r.id} hover sx={{ cursor: "pointer" }} onClick={() => onNavigateTab(4)}>
                    <TableCell sx={{ fontWeight: 600, color: theme.palette.primary.main }}>{r.name}</TableCell>
                    <TableCell>{r.type}</TableCell>
                    <TableCell sx={{ color: "text.secondary" }}>{r.dateGenerated}</TableCell>
                    <TableCell>{r.generatedBy}</TableCell>
                    <TableCell>
                      <Chip size="small" label={r.status} color="success" sx={{ fontSize: 10, height: 20, fontWeight: 700 }} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Paper>
        </Grid>

        {/* Quality Signals */}
        <Grid item xs={12} md={3}>
          <Paper sx={{ p: 2.5, height: "100%" }}>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main, mb: 1.5 }}>
              Quality Signals
            </Typography>
            <Stack spacing={1.5}>
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", p: 1, bgcolor: theme.custom.status.detected.bg, borderRadius: 1 }}>
                <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                  <ErrorOutlineIcon sx={{ color: brandColors.err, fontSize: 18 }} />
                  <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: theme.custom.status.detected.text }}>Out of Spec</Typography>
                </Box>
                <Typography sx={{ fontSize: 16, fontWeight: 800, color: brandColors.err }}>
                  {data.qualitySignals.outOfSpecCount}
                </Typography>
              </Box>

              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", p: 1, bgcolor: theme.custom.status.inconclusive.bg, borderRadius: 1 }}>
                <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                  <WarningAmberIcon sx={{ color: brandColors.badgePM, fontSize: 18 }} />
                  <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: theme.custom.status.inconclusive.text }}>Alert / Action Level</Typography>
                </Box>
                <Typography sx={{ fontSize: 16, fontWeight: 800, color: brandColors.badgePM }}>
                  {data.qualitySignals.alertActionCount}
                </Typography>
              </Box>

              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", p: 1, bgcolor: theme.custom.status.purple.bg, borderRadius: 1 }}>
                <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                  <HourglassEmptyIcon sx={{ color: theme.palette.primary.main, fontSize: 18 }} />
                  <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: theme.custom.status.purple.text }}>Pending Review</Typography>
                </Box>
                <Typography sx={{ fontSize: 16, fontWeight: 800, color: theme.palette.primary.main }}>
                  {data.qualitySignals.pendingReviewCount}
                </Typography>
              </Box>

              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", p: 1, bgcolor: theme.custom.status.notDetected.bg, borderRadius: 1 }}>
                <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                  <VerifiedUserIcon sx={{ color: brandColors.ok, fontSize: 18 }} />
                  <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: theme.custom.status.notDetected.text }}>Pending Approval</Typography>
                </Box>
                <Typography sx={{ fontSize: 16, fontWeight: 800, color: brandColors.ok }}>
                  {data.qualitySignals.pendingApprovalCount}
                </Typography>
              </Box>
            </Stack>
          </Paper>
        </Grid>

        {/* Quick Actions */}
        <Grid item xs={12} md={3}>
          <Paper sx={{ p: 2.5, height: "100%" }}>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main, mb: 1.5 }}>
              Quick Actions
            </Typography>
            <Stack spacing={1.25}>
              <Button
                variant="outlined"
                fullWidth
                startIcon={<SearchIcon />}
                onClick={() => onNavigateTab(1)}
                sx={{ justifyContent: "flex-start", py: 1 }}
              >
                Search Records
              </Button>
              <Button
                variant="outlined"
                fullWidth
                startIcon={<PostAddIcon />}
                onClick={() => onNavigateTab(2)}
                sx={{ justifyContent: "flex-start", py: 1 }}
              >
                Build Report
              </Button>
              <Button
                variant="outlined"
                fullWidth
                startIcon={<ShowChartIcon />}
                onClick={() => onNavigateTab(3)}
                sx={{ justifyContent: "flex-start", py: 1 }}
              >
                Trending & Analysis
              </Button>
              <Button
                variant="outlined"
                fullWidth
                startIcon={<FolderSharedIcon />}
                onClick={() => onNavigateTab(4)}
                sx={{ justifyContent: "flex-start", py: 1 }}
              >
                Saved Reports
              </Button>
            </Stack>
          </Paper>
        </Grid>
      </Grid>
    </Box>
  );
}
