import { useEffect, useState, useMemo } from "react";
import {
  Box, Paper, Typography, Grid, FormControl, InputLabel, Select, MenuItem,
  Button, Table, TableHead, TableRow, TableCell, TableBody, TableSortLabel,
  Stack, Chip, Tooltip, Alert, Divider, IconButton
} from "@mui/material";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import TuneIcon from "@mui/icons-material/Tune";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import {
  ResponsiveContainer, BarChart, Bar, XAxis, YAxis, Tooltip as RechartsTooltip,
  PieChart, Pie, Cell, LineChart, Line, CartesianGrid, Legend
} from "recharts";
import {
  AnalystComparisonRow,
  AnalystKpiFilters,
  AnalystPerformanceDashboardData,
  OverviewKpiCardData
} from "../types/reportingTypes";
import { AnalystKpiService } from "../services/AnalystKpiService";
import { WorkloadWeightsDialog } from "./WorkloadWeightsDialog";
import { useAuth } from "../../../contexts/AuthContext";
import { brandColors } from "../../../theme";

function KpiCard({ data }: { data: OverviewKpiCardData }) {
  const isUp = data.deltaDirection === "up";
  const isError = data.variant === "error";
  const isWarning = data.variant === "warning";

  let deltaColor = isUp ? brandColors.ok : brandColors.err;
  if (isError) deltaColor = brandColors.err;
  if (isWarning) deltaColor = brandColors.badgePM;

  const card = (
    <Paper sx={{ p: 2, height: "100%", display: "flex", flexDirection: "column", justifyContent: "space-between" }}>
      <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: "text.secondary" }}>
        {data.title}
      </Typography>
      <Typography sx={{ fontSize: 24, fontWeight: 800, color: isError ? brandColors.err : brandColors.sectionTitle, my: 0.5 }}>
        {data.value}
      </Typography>
      <Box sx={{ display: "flex", alignItems: "center", gap: 0.5, flexWrap: "wrap" }}>
        <Box sx={{ display: "flex", alignItems: "center", color: deltaColor, fontSize: 11, fontWeight: 700 }}>
          {isUp ? <ArrowUpwardIcon sx={{ fontSize: 13 }} /> : <ArrowDownwardIcon sx={{ fontSize: 13 }} />}
          {Math.abs(data.deltaPercent)}%
        </Box>
        <Typography sx={{ fontSize: 10.5, color: "text.secondary" }} noWrap>
          {data.comparisonLabel}
        </Typography>
      </Box>
    </Paper>
  );

  return data.tooltip ? (
    <Tooltip title={data.tooltip} arrow>
      {card}
    </Tooltip>
  ) : (
    card
  );
}

type Order = "asc" | "desc";

export function AnalystKpiTab() {
  const { role, userId } = useAuth();
  const isAnalyst = role === "Analyst";

  const [filters, setFilters] = useState<AnalystKpiFilters>({
    dateRange: "30d",
    analystId: isAnalyst ? userId || 101 : "All",
    category: "All",
    location: "All",
    testCode: "All"
  });

  const [data, setData] = useState<AnalystPerformanceDashboardData | null>(null);
  const [loading, setLoading] = useState(true);
  const [selectedAnalystId, setSelectedAnalystId] = useState<number | null>(null);
  const [weightsDialogOpen, setWeightsDialogOpen] = useState(false);

  // Sorting for comparison table
  const [orderBy, setOrderBy] = useState<keyof AnalystComparisonRow>("completed");
  const [order, setOrder] = useState<Order>("desc");

  const loadData = () => {
    setLoading(true);
    AnalystKpiService.getDashboardData(filters, role || "SectionHead", userId || 101)
      .then((res) => {
        setData(res);
        if (!selectedAnalystId && res.analystComparison.length > 0) {
          setSelectedAnalystId(res.analystComparison[0].analystId);
        }
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters.dateRange, filters.analystId, filters.category]);

  const handleRequestSort = (property: keyof AnalystComparisonRow) => {
    const isAsc = orderBy === property && order === "asc";
    setOrder(isAsc ? "desc" : "asc");
    setOrderBy(property);
  };

  const sortedRows = useMemo(() => {
    if (!data) return [];
    return [...data.analystComparison].sort((a, b) => {
      const aVal = a[orderBy];
      const bVal = b[orderBy];
      if (aVal < bVal) return order === "asc" ? -1 : 1;
      if (aVal > bVal) return order === "asc" ? 1 : -1;
      return 0;
    });
  }, [data, order, orderBy]);

  const selectedAnalystDetail = useMemo(() => {
    if (!data || !selectedAnalystId) return null;
    const detail = data.selectedAnalystDetail;
    if (detail && detail.analystId === selectedAnalystId) return detail;
    const row = data.analystComparison.find((r) => r.analystId === selectedAnalystId);
    if (!row) return detail;
    return {
      analystId: row.analystId,
      analystName: row.analystName,
      username: row.username,
      role: "Analyst",
      workload: {
        assignedTests: row.assigned,
        completedTests: row.completed,
        pendingTests: row.pending,
        overdueTests: row.overdue,
        configuredWorkloadUnits: row.workloadUnits
      },
      timeliness: {
        avgTestingTatDays: row.avgTestingTatDays,
        medianTestingTatDays: Math.max(1.0, Number((row.avgTestingTatDays - 0.2).toFixed(1))),
        onTimePercent: row.onTimePercent,
        overdueCount: row.overdue
      },
      quality: {
        reviewReturns: row.reviewReturns,
        documentationCorrections: row.docCorrections,
        calculationCorrections: 0,
        missingMandatoryDataCount: 0,
        firstTimeReviewAcceptanceRate: Number((100 - (row.reviewReturns / row.completed) * 100).toFixed(1)),
        executionRelatedDeviations: 0
      },
      compliance: {
        trainingStatus: "Current / Qualified",
        competencyStatus: "Current (Annual Evaluation Passed)",
        sopComplianceIndex: "99.1%",
        lateEntriesCount: row.overdue
      },
      dataCoverage: {
        totalEvaluatedRecords: row.completed,
        recordsWithCompleteTimestamps: Math.round(row.completed * 0.96),
        coveragePercent: 96.0
      }
    };
  }, [data, selectedAnalystId]);

  if (!data) return null;

  return (
    <Box sx={{ pb: 4 }}>
      {/* Top Filter & Access Control Bar */}
      <Paper sx={{ p: 2, mb: 2.5 }}>
        <Grid container spacing={2} alignItems="center">
          <Grid item xs={12} sm={6} md={2.5}>
            <FormControl fullWidth size="small">
              <InputLabel>Date Range</InputLabel>
              <Select
                label="Date Range"
                value={filters.dateRange}
                onChange={(e) => setFilters((f) => ({ ...f, dateRange: e.target.value as any }))}
              >
                <MenuItem value="7d">Last 7 Days</MenuItem>
                <MenuItem value="30d">Last 30 Days</MenuItem>
                <MenuItem value="3m">Last 3 Months</MenuItem>
                <MenuItem value="6m">Last 6 Months</MenuItem>
                <MenuItem value="12m">Last 12 Months</MenuItem>
              </Select>
            </FormControl>
          </Grid>

          <Grid item xs={12} sm={6} md={2.5}>
            <FormControl fullWidth size="small">
              <InputLabel>Analyst</InputLabel>
              <Select
                label="Analyst"
                value={filters.analystId}
                disabled={isAnalyst}
                onChange={(e) => {
                  setFilters((f) => ({ ...f, analystId: e.target.value as any }));
                  if (e.target.value !== "All") setSelectedAnalystId(Number(e.target.value));
                }}
              >
                {!isAnalyst && <MenuItem value="All">All Analysts (Section View)</MenuItem>}
                <MenuItem value={101}>Amal Hamdy</MenuItem>
                <MenuItem value={102}>Ahmed Ali</MenuItem>
                <MenuItem value={103}>Sara Mohamed</MenuItem>
                <MenuItem value={104}>Khaled Omar</MenuItem>
                <MenuItem value={105}>Nour Ibrahim</MenuItem>
                <MenuItem value={106}>Youssef Tarek</MenuItem>
              </Select>
            </FormControl>
          </Grid>

          <Grid item xs={12} sm={6} md={2}>
            <FormControl fullWidth size="small">
              <InputLabel>Category</InputLabel>
              <Select
                label="Category"
                value={filters.category}
                onChange={(e) => setFilters((f) => ({ ...f, category: e.target.value as any }))}
              >
                <MenuItem value="All">All Categories</MenuItem>
                <MenuItem value="FinishedProduct">Finished Product</MenuItem>
                <MenuItem value="RawMaterial">Raw Material</MenuItem>
                <MenuItem value="Water">Water</MenuItem>
                <MenuItem value="EnvironmentalMonitoring">EM</MenuItem>
              </Select>
            </FormControl>
          </Grid>

          <Grid item xs={12} sm={6} md={5}>
            <Stack direction="row" spacing={1} justifyContent="flex-end">
              <Button
                size="small"
                variant="outlined"
                startIcon={<TuneIcon />}
                onClick={() => setWeightsDialogOpen(true)}
              >
                Workload Weights
              </Button>
              <Button
                size="small"
                variant="outlined"
                startIcon={<PictureAsPdfIcon />}
                onClick={() => window.print()}
              >
                Export KPI Report
              </Button>
            </Stack>
          </Grid>
        </Grid>
      </Paper>

      {/* Data Coverage & GMP Quality Signal Banner */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2, flexWrap: "wrap", gap: 1 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <CheckCircleOutlineIcon sx={{ color: brandColors.ok, fontSize: 18 }} />
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            <strong>Data Availability: {data.dataCoveragePercent}%</strong> · {data.dataCoverageNote}
          </Typography>
        </Box>
        <Chip
          size="small"
          icon={<InfoOutlinedIcon />}
          label="Designed to support GMP & ALCOA++ requirements"
          sx={{ fontSize: 11, height: 22 }}
        />
      </Box>

      {/* Top 6 Analyst Performance KPI Cards */}
      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        <Grid item xs={12} sm={6} md={2}>
          <KpiCard data={data.testsAssigned} />
        </Grid>
        <Grid item xs={12} sm={6} md={2}>
          <KpiCard data={data.testsCompleted} />
        </Grid>
        <Grid item xs={12} sm={6} md={2}>
          <KpiCard data={data.completionRate} />
        </Grid>
        <Grid item xs={12} sm={6} md={2}>
          <KpiCard data={data.onTimeCompletion} />
        </Grid>
        <Grid item xs={12} sm={6} md={2}>
          <KpiCard data={data.averageTestingTat} />
        </Grid>
        <Grid item xs={12} sm={6} md={2}>
          <KpiCard data={data.pendingOverdue} />
        </Grid>
      </Grid>

      {/* Visualizations Row */}
      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        {/* Tests Completed by Month */}
        <Grid item xs={12} md={5}>
          <Paper sx={{ p: 2.5, height: 320 }}>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: brandColors.pageTitle, mb: 1 }}>
              Tests Completed by Month
            </Typography>
            <Box sx={{ height: 250 }}>
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={data.completedByMonth} margin={{ top: 10, right: 10, left: -20, bottom: 5 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                  <XAxis dataKey="month" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} />
                  <RechartsTooltip />
                  <Legend verticalAlign="top" height={32} wrapperStyle={{ fontSize: 11 }} />
                  <Bar dataKey="year2025" name="2025" fill="#94a3b8" radius={[3, 3, 0, 0]} barSize={12} />
                  <Bar dataKey="year2026" name="2026" fill="#7b2d8e" radius={[3, 3, 0, 0]} barSize={12} />
                </BarChart>
              </ResponsiveContainer>
            </Box>
          </Paper>
        </Grid>

        {/* Tests by Category Donut */}
        <Grid item xs={12} md={4}>
          <Paper sx={{ p: 2.5, height: 320, display: "flex", flexDirection: "column" }}>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: brandColors.pageTitle, mb: 1 }}>
              Tests by Category
            </Typography>
            <Box sx={{ display: "flex", alignItems: "center", flex: 1 }}>
              <Box sx={{ width: "50%", height: 200, position: "relative" }}>
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={data.testsByCategory}
                      dataKey="count"
                      nameKey="category"
                      innerRadius={45}
                      outerRadius={70}
                      paddingAngle={2}
                    >
                      {data.testsByCategory.map((e, i) => (
                        <Cell key={i} fill={e.color} />
                      ))}
                    </Pie>
                    <RechartsTooltip />
                  </PieChart>
                </ResponsiveContainer>
                <Box sx={{ position: "absolute", top: "50%", left: "50%", transform: "translate(-50%, -50%)", textAlign: "center" }}>
                  <Typography sx={{ fontSize: 15, fontWeight: 800, color: brandColors.sectionTitle }}>1,284</Typography>
                  <Typography sx={{ fontSize: 9, color: "text.secondary" }}>Total</Typography>
                </Box>
              </Box>
              <Box sx={{ width: "50%", pl: 1 }}>
                <Stack spacing={0.5}>
                  {data.testsByCategory.map((item, idx) => (
                    <Box key={idx} sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", fontSize: 11 }}>
                      <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                        <Box sx={{ width: 6, height: 6, borderRadius: "50%", bgcolor: item.color }} />
                        <Typography sx={{ fontSize: 11, color: "text.secondary" }} noWrap>{item.category}</Typography>
                      </Box>
                      <Typography sx={{ fontSize: 11, fontWeight: 600 }}>{item.percentage}%</Typography>
                    </Box>
                  ))}
                </Stack>
              </Box>
            </Box>
          </Paper>
        </Grid>

        {/* Average TAT Trend */}
        <Grid item xs={12} md={3}>
          <Paper sx={{ p: 2.5, height: 320 }}>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: brandColors.pageTitle, mb: 1 }}>
              Average Testing TAT (Days)
            </Typography>
            <Box sx={{ height: 250 }}>
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={data.tatTrendByMonth} margin={{ top: 10, right: 10, left: -25, bottom: 5 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                  <XAxis dataKey="month" tick={{ fontSize: 11 }} />
                  <YAxis domain={[0, 4]} tick={{ fontSize: 11 }} />
                  <RechartsTooltip formatter={(val: number) => [`${val} days`, "Testing TAT"]} />
                  <Line type="monotone" dataKey="tatDays" stroke="#0891b2" strokeWidth={2.5} dot={{ r: 4 }} />
                </LineChart>
              </ResponsiveContainer>
            </Box>
          </Paper>
        </Grid>
      </Grid>

      {/* Workflow Queue & TAT Separation Cards */}
      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 2 }}>
            <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.pageTitle, mb: 1.5 }}>
              Workflow Bottleneck (Records in Queue)
            </Typography>
            <Grid container spacing={1}>
              <Grid item xs={4}>
                <Box sx={{ p: 1.5, bgcolor: "#f8fafc", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Testing Queue</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: brandColors.sectionTitle }}>
                    {data.workflowBottleneck.testingQueueCount}
                  </Typography>
                  <Typography sx={{ fontSize: 10, color: brandColors.ok }}>↑ 5.1% vs prev</Typography>
                </Box>
              </Grid>
              <Grid item xs={4}>
                <Box sx={{ p: 1.5, bgcolor: "#f5f3ff", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Review Queue</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: "#5b21b6" }}>
                    {data.workflowBottleneck.reviewQueueCount}
                  </Typography>
                  <Typography sx={{ fontSize: 10, color: brandColors.ok }}>↓ 12.5% vs prev</Typography>
                </Box>
              </Grid>
              <Grid item xs={4}>
                <Box sx={{ p: 1.5, bgcolor: "#f0fdf4", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Approval Queue</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: "#166534" }}>
                    {data.workflowBottleneck.approvalQueueCount}
                  </Typography>
                  <Typography sx={{ fontSize: 10, color: brandColors.ok }}>↓ 25.0% vs prev</Typography>
                </Box>
              </Grid>
            </Grid>
          </Paper>
        </Grid>

        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 2 }}>
            <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.pageTitle, mb: 1.5 }}>
              TAT Stage Summary (Days) — Segregated by Responsibility
            </Typography>
            <Grid container spacing={1}>
              <Grid item xs={3}>
                <Box sx={{ p: 1.5, bgcolor: "#eff6ff", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 10.5, color: "text.secondary" }}>Testing (Analyst)</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: "#2563eb" }}>
                    {data.tatSummary.testingTatDays} d
                  </Typography>
                  <Typography sx={{ fontSize: 10, color: "text.secondary" }}>Assignment → Entry</Typography>
                </Box>
              </Grid>
              <Grid item xs={3}>
                <Box sx={{ p: 1.5, bgcolor: "#f8fafc", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 10.5, color: "text.secondary" }}>Review Stage</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800 }}>
                    {data.tatSummary.reviewTatDays} d
                  </Typography>
                  <Typography sx={{ fontSize: 10, color: "text.secondary" }}>Entry → Review</Typography>
                </Box>
              </Grid>
              <Grid item xs={3}>
                <Box sx={{ p: 1.5, bgcolor: "#f8fafc", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 10.5, color: "text.secondary" }}>Approval Stage</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800 }}>
                    {data.tatSummary.approvalTatDays} d
                  </Typography>
                  <Typography sx={{ fontSize: 10, color: "text.secondary" }}>Review → Release</Typography>
                </Box>
              </Grid>
              <Grid item xs={3}>
                <Box sx={{ p: 1.5, bgcolor: "#faf5ff", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 10.5, color: "text.secondary" }}>Total Lifecycle</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: brandColors.sectionTitle }}>
                    {data.tatSummary.totalTatDays} d
                  </Typography>
                  <Typography sx={{ fontSize: 10, color: "text.secondary" }}>Full turnaround</Typography>
                </Box>
              </Grid>
            </Grid>
          </Paper>
        </Grid>
      </Grid>

      {/* Analyst Comparison Table */}
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
          <Box>
            <Typography sx={{ fontSize: 15, fontWeight: 700, color: brandColors.pageTitle }}>
              Analyst Performance Comparison
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
              Normalized with Configured Workload Units (Operational Metric). Click any row to view full detail.
            </Typography>
          </Box>
          <Chip size="small" label={`${sortedRows.length} Analysts`} sx={{ bgcolor: brandColors.causeBadgeBg, color: brandColors.causeBadgeText, fontWeight: 700 }} />
        </Box>

        <Table size="small" sx={{ "& th": { fontWeight: 700, fontSize: 11.5 }, "& td": { fontSize: 12 } }}>
          <TableHead>
            <TableRow>
              <TableCell>Analyst</TableCell>
              <TableCell align="right">
                <TableSortLabel active={orderBy === "assigned"} direction={orderBy === "assigned" ? order : "asc"} onClick={() => handleRequestSort("assigned")}>
                  Assigned
                </TableSortLabel>
              </TableCell>
              <TableCell align="right">
                <TableSortLabel active={orderBy === "completed"} direction={orderBy === "completed" ? order : "asc"} onClick={() => handleRequestSort("completed")}>
                  Completed
                </TableSortLabel>
              </TableCell>
              <TableCell align="right">
                <Tooltip title="Configured Workload Units: Normalized metric based on test complexity weights">
                  <TableSortLabel active={orderBy === "workloadUnits"} direction={orderBy === "workloadUnits" ? order : "asc"} onClick={() => handleRequestSort("workloadUnits")}>
                    Workload Units
                  </TableSortLabel>
                </Tooltip>
              </TableCell>
              <TableCell align="right">
                <TableSortLabel active={orderBy === "completionRatePercent"} direction={orderBy === "completionRatePercent" ? order : "asc"} onClick={() => handleRequestSort("completionRatePercent")}>
                  Completion %
                </TableSortLabel>
              </TableCell>
              <TableCell align="right">
                <TableSortLabel active={orderBy === "onTimePercent"} direction={orderBy === "onTimePercent" ? order : "asc"} onClick={() => handleRequestSort("onTimePercent")}>
                  On-Time %
                </TableSortLabel>
              </TableCell>
              <TableCell align="right">
                <TableSortLabel active={orderBy === "avgTestingTatDays"} direction={orderBy === "avgTestingTatDays" ? order : "asc"} onClick={() => handleRequestSort("avgTestingTatDays")}>
                  Avg Testing TAT
                </TableSortLabel>
              </TableCell>
              <TableCell align="right">
                <TableSortLabel active={orderBy === "reviewReturns"} direction={orderBy === "reviewReturns" ? order : "asc"} onClick={() => handleRequestSort("reviewReturns")}>
                  Review Returns
                </TableSortLabel>
              </TableCell>
              <TableCell align="right">
                <TableSortLabel active={orderBy === "docCorrections"} direction={orderBy === "docCorrections" ? order : "asc"} onClick={() => handleRequestSort("docCorrections")}>
                  Doc Corrections
                </TableSortLabel>
              </TableCell>
              <TableCell align="right">Pending</TableCell>
              <TableCell align="right">Overdue</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {sortedRows.map((row) => {
              const isSelected = selectedAnalystId === row.analystId;
              return (
                <TableRow
                  key={row.analystId}
                  hover
                  selected={isSelected}
                  sx={{ cursor: "pointer", bgcolor: isSelected ? "#f5f3ff" : "inherit" }}
                  onClick={() => setSelectedAnalystId(row.analystId)}
                >
                  <TableCell sx={{ fontWeight: 700, color: brandColors.sectionTitle }}>
                    {row.analystName} <Typography component="span" sx={{ fontSize: 11, color: "text.secondary" }}>({row.username})</Typography>
                  </TableCell>
                  <TableCell align="right">{row.assigned}</TableCell>
                  <TableCell align="right" sx={{ fontWeight: 600 }}>{row.completed}</TableCell>
                  <TableCell align="right">
                    <Chip size="small" label={`${row.workloadUnits} WU`} sx={{ fontSize: 11, height: 20, bgcolor: "#ede9fe", color: "#6d28d9", fontWeight: 700 }} />
                  </TableCell>
                  <TableCell align="right">{row.completionRatePercent}%</TableCell>
                  <TableCell align="right" sx={{ color: row.onTimePercent >= 95 ? brandColors.ok : brandColors.badgePM, fontWeight: 600 }}>
                    {row.onTimePercent}%
                  </TableCell>
                  <TableCell align="right">{row.avgTestingTatDays} d</TableCell>
                  <TableCell align="right" sx={{ color: row.reviewReturns > 0 ? brandColors.badgePM : "inherit" }}>
                    {row.reviewReturns}
                  </TableCell>
                  <TableCell align="right" sx={{ color: row.docCorrections > 0 ? "#ea580c" : "inherit" }}>
                    {row.docCorrections}
                  </TableCell>
                  <TableCell align="right">{row.pending}</TableCell>
                  <TableCell align="right" sx={{ color: row.overdue > 0 ? brandColors.err : "inherit", fontWeight: row.overdue > 0 ? 700 : 400 }}>
                    {row.overdue}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </Paper>

      {/* Detailed Analyst Drill-Down (4 Panels) */}
      {selectedAnalystDetail && (
        <Paper sx={{ p: 3, border: "1px solid #e2e8f0" }}>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2, pb: 1.5, borderBottom: 1, borderColor: "divider" }}>
            <Box>
              <Typography sx={{ fontSize: 16, fontWeight: 800, color: brandColors.sectionTitle }}>
                Analyst Performance Detail: {selectedAnalystDetail.analystName}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                Username: <strong>@{selectedAnalystDetail.username}</strong> · Role: {selectedAnalystDetail.role}
              </Typography>
            </Box>
            <Chip
              size="small"
              label={`Data Coverage: ${selectedAnalystDetail.dataCoverage.coveragePercent}% (${selectedAnalystDetail.dataCoverage.recordsWithCompleteTimestamps}/${selectedAnalystDetail.dataCoverage.totalEvaluatedRecords} records)`}
              color="success"
              variant="outlined"
            />
          </Box>

          <Grid container spacing={2}>
            {/* Panel A: Workload */}
            <Grid item xs={12} sm={6} md={3}>
              <Box sx={{ p: 2, bgcolor: "#f8fafc", borderRadius: 1.5, height: "100%", border: "1px solid #e2e8f0" }}>
                <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.pageTitle, mb: 1.5 }}>
                  A. Workload Execution
                </Typography>
                <Stack spacing={1}>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Assigned Tests:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{selectedAnalystDetail.workload.assignedTests}</Typography>
                  </Box>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Completed Tests:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{selectedAnalystDetail.workload.completedTests}</Typography>
                  </Box>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Active Pending:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{selectedAnalystDetail.workload.pendingTests}</Typography>
                  </Box>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Configured Workload Units:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 700, color: brandColors.sectionTitle }}>
                      {selectedAnalystDetail.workload.configuredWorkloadUnits} WU
                    </Typography>
                  </Box>
                </Stack>
              </Box>
            </Grid>

            {/* Panel B: Timeliness */}
            <Grid item xs={12} sm={6} md={3}>
              <Box sx={{ p: 2, bgcolor: "#f8fafc", borderRadius: 1.5, height: "100%", border: "1px solid #e2e8f0" }}>
                <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.pageTitle, mb: 1.5 }}>
                  B. Timeliness & TAT
                </Typography>
                <Stack spacing={1}>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Average Testing TAT:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{selectedAnalystDetail.timeliness.avgTestingTatDays} Days</Typography>
                  </Box>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Median Testing TAT:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{selectedAnalystDetail.timeliness.medianTestingTatDays} Days</Typography>
                  </Box>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>On-Time Completion:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 700, color: brandColors.ok }}>
                      {selectedAnalystDetail.timeliness.onTimePercent}%
                    </Typography>
                  </Box>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Overdue Count:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 600, color: selectedAnalystDetail.timeliness.overdueCount > 0 ? brandColors.err : "inherit" }}>
                      {selectedAnalystDetail.timeliness.overdueCount}
                    </Typography>
                  </Box>
                </Stack>
              </Box>
            </Grid>

            {/* Panel C: Documentation & Review Quality */}
            <Grid item xs={12} sm={6} md={3}>
              <Box sx={{ p: 2, bgcolor: "#f8fafc", borderRadius: 1.5, height: "100%", border: "1px solid #e2e8f0" }}>
                <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.pageTitle, mb: 1.5 }}>
                  C. Documentation Quality
                </Typography>
                <Stack spacing={1}>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Review Returns:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{selectedAnalystDetail.quality.reviewReturns}</Typography>
                  </Box>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Doc Corrections:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{selectedAnalystDetail.quality.documentationCorrections}</Typography>
                  </Box>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>First-Time Review Rate:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 700, color: brandColors.ok }}>
                      {selectedAnalystDetail.quality.firstTimeReviewAcceptanceRate}%
                    </Typography>
                  </Box>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Execution Deviations:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{selectedAnalystDetail.quality.executionRelatedDeviations}</Typography>
                  </Box>
                </Stack>
              </Box>
            </Grid>

            {/* Panel D: Compliance */}
            <Grid item xs={12} sm={6} md={3}>
              <Box sx={{ p: 2, bgcolor: "#f8fafc", borderRadius: 1.5, height: "100%", border: "1px solid #e2e8f0" }}>
                <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.pageTitle, mb: 1.5 }}>
                  D. Compliance & Training
                </Typography>
                <Stack spacing={1}>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Training Status:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 600, color: brandColors.ok }}>
                      {selectedAnalystDetail.compliance.trainingStatus}
                    </Typography>
                  </Box>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Competency:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{selectedAnalystDetail.compliance.competencyStatus}</Typography>
                  </Box>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>SOP Index:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{selectedAnalystDetail.compliance.sopComplianceIndex}</Typography>
                  </Box>
                  <Box sx={{ display: "flex", justifyContent: "space-between", fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Late Entries:</Typography>
                    <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{selectedAnalystDetail.compliance.lateEntriesCount}</Typography>
                  </Box>
                </Stack>
              </Box>
            </Grid>
          </Grid>
        </Paper>
      )}

      {/* Workload Weights Dialog */}
      <WorkloadWeightsDialog
        open={weightsDialogOpen}
        onClose={() => setWeightsDialogOpen(false)}
        onUpdated={loadData}
      />
    </Box>
  );
}
