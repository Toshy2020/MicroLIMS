import { useEffect, useState } from "react";
import {
  Box, Paper, Typography, TextField, FormControl, InputLabel, Select, MenuItem,
  Button, Grid, Stack, Chip, Divider, IconButton, Tooltip
} from "@mui/material";
import TableChartIcon from "@mui/icons-material/TableChart";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import BookmarkBorderIcon from "@mui/icons-material/BookmarkBorder";
import CompareArrowsIcon from "@mui/icons-material/CompareArrows";
import LocationOnIcon from "@mui/icons-material/LocationOn";
import {
  ResponsiveContainer, ComposedChart, Line, XAxis, YAxis, Tooltip as RechartsTooltip,
  CartesianGrid, Legend, Dot, BarChart, Bar
} from "recharts";
import {
  NumericTrendPoint,
  TrendAnalysisResult,
  TrendingCriteria,
  SampleCategory,
  ResultRecordItem
} from "../types/reportingTypes";
import { TrendingService } from "../services/TrendingService";
import { TrendingDataDialog } from "./TrendingDataDialog";
import { CompareDialog } from "./CompareDialog";
import { RecordDetailDialog } from "./RecordDetailDialog";
import { brandColors } from "../../../theme";

interface TrendingTabProps {
  initialTestCode?: string;
  initialSubjectName?: string;
}

export function TrendingTab({ initialTestCode, initialSubjectName }: TrendingTabProps) {
  const [criteria, setCriteria] = useState<TrendingCriteria>({
    testCode: initialTestCode || "TAMC",
    subjectName: initialSubjectName || "Osteocare Liquid",
    category: "FinishedProduct",
    location: "All",
    dateRange: "12m",
    frequency: "Monthly",
    compareWith: "None",
    showMode: "All"
  });

  const [analysis, setAnalysis] = useState<TrendAnalysisResult | null>(null);
  const [loading, setLoading] = useState(false);

  // Dialog states
  const [dataDialogOpen, setDataDialogOpen] = useState(false);
  const [compareDialogOpen, setCompareDialogOpen] = useState(false);
  const [compareMode, setCompareMode] = useState<"products" | "locations">("products");
  const [drillDownRecord, setDrillDownRecord] = useState<ResultRecordItem | null>(null);
  const [savedSuccess, setSavedSuccess] = useState(false);

  const fetchAnalysis = () => {
    setLoading(true);
    TrendingService.getAnalysis(criteria)
      .then(setAnalysis)
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchAnalysis();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [criteria.testCode, criteria.subjectName, criteria.dateRange]);

  const handlePointClick = (point: NumericTrendPoint) => {
    // Generate full record details for drilldown dialog
    setDrillDownRecord({
      id: point.recordId,
      sampleId: 100 + point.recordId,
      testOrderId: 500 + point.recordId,
      sourceTable: "TestOrders",
      sourceId: 500 + point.recordId,
      round: 1,
      referenceNumber: point.referenceNumber,
      category: (criteria.category || "FinishedProduct") as SampleCategory,
      subjectName: criteria.subjectName,
      subjectDetail: "Point Drill-down",
      batchNumber: `B-${point.label.replace(" ", "")}`,
      controlNumber: "C-2026",
      testCode: criteria.testCode,
      testDisplayName: analysis?.testDisplayName || criteria.testCode,
      resultKind: analysis?.isNumeric ? "Quantitative" : "Qualitative",
      numericValue: point.value,
      reportedValue: point.reportedValue,
      unit: analysis?.unit ?? "CFU/mL",
      isBelowDetectionLimit: point.value != null && point.value < 1,
      detectionLimit: 1,
      alertLimit: point.alertLevel ? `${point.alertLevel}` : null,
      actionLimit: point.actionLevel ? `${point.actionLevel}` : null,
      specLimit: point.upperLimit ? `NMT ${point.upperLimit}` : null,
      resultLevel: point.resultLevel,
      resultEnteredAt: point.date,
      resultEnteredByUserId: 101,
      resultEnteredByName: "Amal Hamdy",
      sampleStatus: "Approved",
      approvalStatus: "Approved",
      approvedByUserId: 201,
      approvedByName: "Amal Hamdy",
      approvedAt: point.date
    });
  };

  return (
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", lg: "280px 1fr 240px" }, gap: 2, alignItems: "start", pb: 4 }}>
      {/* LEFT COLUMN: Analysis Criteria */}
      <Paper sx={{ p: 2.5 }}>
        <Typography sx={{ fontSize: 15, fontWeight: 700, color: brandColors.sectionTitle, mb: 2 }}>
          Analysis Criteria
        </Typography>

        <Stack spacing={2}>
          <FormControl fullWidth size="small">
            <InputLabel>Parameter / Test</InputLabel>
            <Select
              label="Parameter / Test"
              value={criteria.testCode}
              onChange={(e) => setCriteria((c) => ({ ...c, testCode: e.target.value }))}
            >
              <MenuItem value="TAMC">TAMC (Total Aerobic Count)</MenuItem>
              <MenuItem value="TYMC">TYMC (Total Yeast & Mold)</MenuItem>
              <MenuItem value="WATER_TAMC">Water TAMC (Purified Water)</MenuItem>
              <MenuItem value="PATHOGEN_ECOLI">E. coli (Pathogen Screening)</MenuItem>
            </Select>
          </FormControl>

          <FormControl fullWidth size="small">
            <InputLabel>Product / Item</InputLabel>
            <Select
              label="Product / Item"
              value={criteria.subjectName}
              onChange={(e) => setCriteria((c) => ({ ...c, subjectName: e.target.value }))}
            >
              <MenuItem value="Osteocare Liquid">Osteocare Liquid</MenuItem>
              <MenuItem value="Feroglobin Syrup">Feroglobin Syrup</MenuItem>
              <MenuItem value="Honey">Honey (Raw Material)</MenuItem>
              <MenuItem value="Purified Water">Purified Water (Loop 1)</MenuItem>
              <MenuItem value="Point W-01">Point W-01 (Utilities)</MenuItem>
            </Select>
          </FormControl>

          <FormControl fullWidth size="small">
            <InputLabel>Category</InputLabel>
            <Select
              label="Category"
              value={criteria.category ?? ""}
              onChange={(e) => setCriteria((c) => ({ ...c, category: (e.target.value || "") as any }))}
            >
              <MenuItem value="FinishedProduct">Finished Product</MenuItem>
              <MenuItem value="RawMaterial">Raw Material</MenuItem>
              <MenuItem value="Water">Water</MenuItem>
              <MenuItem value="EnvironmentalMonitoring">Environmental Monitoring</MenuItem>
            </Select>
          </FormControl>

          <FormControl fullWidth size="small">
            <InputLabel>Location / Point</InputLabel>
            <Select
              label="Location / Point"
              value={criteria.location ?? "All"}
              onChange={(e) => setCriteria((c) => ({ ...c, location: e.target.value }))}
            >
              <MenuItem value="All">All Locations</MenuItem>
              <MenuItem value="Production">Production</MenuItem>
              <MenuItem value="Warehouse">Warehouse</MenuItem>
              <MenuItem value="QC Lab">QC Lab</MenuItem>
            </Select>
          </FormControl>

          <FormControl fullWidth size="small">
            <InputLabel>Date Range</InputLabel>
            <Select
              label="Date Range"
              value={criteria.dateRange}
              onChange={(e) => setCriteria((c) => ({ ...c, dateRange: e.target.value as any }))}
            >
              <MenuItem value="30d">Last 30 Days</MenuItem>
              <MenuItem value="3m">Last 3 Months</MenuItem>
              <MenuItem value="6m">Last 6 Months</MenuItem>
              <MenuItem value="12m">Last 12 Months</MenuItem>
            </Select>
          </FormControl>

          <FormControl fullWidth size="small">
            <InputLabel>Frequency</InputLabel>
            <Select
              label="Frequency"
              value={criteria.frequency}
              onChange={(e) => setCriteria((c) => ({ ...c, frequency: e.target.value as any }))}
            >
              <MenuItem value="Monthly">Monthly</MenuItem>
              <MenuItem value="Weekly">Weekly</MenuItem>
              <MenuItem value="Quarterly">Quarterly</MenuItem>
            </Select>
          </FormControl>

          <FormControl fullWidth size="small">
            <InputLabel>Compare With</InputLabel>
            <Select
              label="Compare With"
              value={criteria.compareWith}
              onChange={(e) => setCriteria((c) => ({ ...c, compareWith: e.target.value as any }))}
            >
              <MenuItem value="None">None</MenuItem>
              <MenuItem value="Previous Period">Previous Period</MenuItem>
              <MenuItem value="Product Comparison">Product Comparison</MenuItem>
              <MenuItem value="Location Comparison">Location Comparison</MenuItem>
            </Select>
          </FormControl>

          <Button variant="contained" fullWidth onClick={fetchAnalysis}>
            Apply Criteria
          </Button>
        </Stack>
      </Paper>

      {/* CENTER COLUMN: Trend Chart & Statistics */}
      <Stack spacing={2}>
        {/* Trend Chart Card */}
        <Paper sx={{ p: 2.5 }}>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5, flexWrap: "wrap", gap: 1 }}>
            <Box>
              <Typography sx={{ fontSize: 16, fontWeight: 700, color: brandColors.sectionTitle }}>
                Trend Chart
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600, color: "#1e293b" }}>
                {analysis?.testDisplayName || criteria.testCode} — {analysis?.subjectName || criteria.subjectName} ({criteria.dateRange === "12m" ? "Last 12 Months" : criteria.dateRange})
              </Typography>
            </Box>
            <Chip
              size="small"
              label={analysis?.isNumeric ? `Numeric (${analysis?.unit ?? "CFU/mL"})` : "Qualitative (Categorical)"}
              sx={{ bgcolor: "#f1f5f9", fontWeight: 700, fontSize: 11 }}
            />
          </Box>

          <Box sx={{ height: 320, width: "100%", mt: 2 }}>
            <ResponsiveContainer width="100%" height="100%">
              {analysis?.isNumeric ? (
                <ComposedChart
                  data={analysis.numericPoints ?? []}
                  margin={{ top: 10, right: 30, left: 10, bottom: 25 }}
                >
                  <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                  <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                  <YAxis
                    tick={{ fontSize: 11 }}
                    domain={[0, 120]}
                    label={{ value: analysis.unit ?? "CFU/mL", angle: -90, position: "insideLeft", fontSize: 11 }}
                  />
                  <RechartsTooltip
                    content={({ payload, label }) => {
                      if (!payload || payload.length === 0) return null;
                      const pt = payload[0].payload as NumericTrendPoint;
                      return (
                        <Paper sx={{ p: 1.5, fontSize: 12, boxShadow: "0 4px 12px rgba(0,0,0,0.1)" }}>
                          <Typography sx={{ fontWeight: 700, color: brandColors.sectionTitle }}>{label}</Typography>
                          <Typography>Ref: <strong>{pt.referenceNumber}</strong></Typography>
                          <Typography>Result: <strong>{pt.reportedValue} {analysis.unit}</strong></Typography>
                          <Typography>Level: <strong>{pt.resultLevel}</strong></Typography>
                          <Typography sx={{ fontSize: 10, color: "text.secondary", mt: 0.5 }}>Click point to view source record</Typography>
                        </Paper>
                      );
                    }}
                  />
                  <Legend verticalAlign="top" height={36} wrapperStyle={{ fontSize: 12 }} />

                  {/* Limit & Baseline Reference Lines */}
                  <Line type="monotone" dataKey="upperLimit" name="Upper Limit (USL)" stroke="#dc2626" strokeDasharray="5 5" strokeWidth={1.5} dot={false} />
                  <Line type="monotone" dataKey="actionLevel" name="Action Level" stroke="#ea580c" strokeDasharray="3 3" strokeWidth={1.5} dot={false} />
                  <Line type="monotone" dataKey="alertLevel" name="Alert Level" stroke="#f59e0b" strokeDasharray="2 2" strokeWidth={1.5} dot={false} />
                  <Line type="monotone" dataKey="mean" name="Mean" stroke="#7b2d8e" strokeDasharray="4 4" strokeWidth={1.5} dot={false} />

                  {/* Main Result Line & Interactive Points */}
                  <Line
                    type="monotone"
                    dataKey="value"
                    name="Results"
                    stroke="#2563eb"
                    strokeWidth={2}
                    activeDot={{ r: 7, onClick: (_, event: any) => {
                      if (event?.payload) handlePointClick(event.payload);
                    } }}
                    dot={(props: any) => {
                      const { cx, cy, payload } = props;
                      if (cx == null || cy == null) return <></>;
                      const isAlert = payload.resultLevel === "AlertLevel";
                      const isAction = payload.resultLevel === "ActionLevel";
                      const isOos = payload.resultLevel === "OutOfSpecification";
                      let fill = "#2563eb";
                      if (isOos) fill = "#dc2626";
                      else if (isAction) fill = "#ea580c";
                      else if (isAlert) fill = "#f59e0b";

                      return (
                        <circle
                          cx={cx}
                          cy={cy}
                          r={isAlert || isAction || isOos ? 6 : 4.5}
                          fill={fill}
                          stroke="#fff"
                          strokeWidth={1.5}
                          style={{ cursor: "pointer" }}
                          onClick={() => handlePointClick(payload)}
                        />
                      );
                    }}
                  />
                </ComposedChart>
              ) : (
                <BarChart
                  data={analysis?.qualitativePoints ?? []}
                  margin={{ top: 10, right: 30, left: 10, bottom: 25 }}
                >
                  <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                  <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} />
                  <RechartsTooltip />
                  <Legend verticalAlign="top" height={36} wrapperStyle={{ fontSize: 12 }} />
                  <Bar dataKey="absentCount" name="Conform / Absent" fill="#16a34a" stackId="a" />
                  <Bar dataKey="detectedCount" name="Detected / Non-Conform" fill="#dc2626" stackId="a" />
                </BarChart>
              )}
            </ResponsiveContainer>
          </Box>
        </Paper>

        {/* Statistics Summary (10 Cards for Numeric / Categorical for Qualitative) */}
        <Paper sx={{ p: 2.5 }}>
          <Typography sx={{ fontSize: 14, fontWeight: 700, color: brandColors.pageTitle, mb: 2 }}>
            Statistics Summary
          </Typography>

          {analysis?.isNumeric && analysis.numericStats ? (
            <Grid container spacing={1.5}>
              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: "#f8fafc", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>No. of Results</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: brandColors.sectionTitle }}>
                    {analysis.numericStats.numberOfResults}
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: "#f8fafc", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Minimum</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800 }}>
                    {analysis.numericStats.minimum}
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: "#f8fafc", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Maximum</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800 }}>
                    {analysis.numericStats.maximum}
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: "#f8fafc", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Mean</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: brandColors.sectionTitle }}>
                    {analysis.numericStats.mean}
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: "#f8fafc", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Median</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800 }}>
                    {analysis.numericStats.median}
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: "#f8fafc", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Std. Deviation</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800 }}>
                    {analysis.numericStats.standardDeviation}
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: "#f0fdf4", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>% Within Spec</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: brandColors.ok }}>
                    {analysis.numericStats.percentWithinSpec}%
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: "#fffbeb", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>% Alert Level</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: brandColors.badgePM }}>
                    {analysis.numericStats.percentAlertLevel}%
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: "#fff7ed", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>% Action Level</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: "#ea580c" }}>
                    {analysis.numericStats.percentActionLevel}%
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: analysis.numericStats.outOfSpecCount > 0 ? "#fef2f2" : "#f8fafc", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Out of Spec</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: analysis.numericStats.outOfSpecCount > 0 ? brandColors.err : "inherit" }}>
                    {analysis.numericStats.outOfSpecCount}
                  </Typography>
                </Box>
              </Grid>
            </Grid>
          ) : (
            <Grid container spacing={2}>
              <Grid item xs={6} sm={3}>
                <Box sx={{ p: 1.5, bgcolor: "#f8fafc", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Total Tests</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800 }}>{analysis?.qualitativeStats?.numberOfResults ?? 0}</Typography>
                </Box>
              </Grid>
              <Grid item xs={6} sm={3}>
                <Box sx={{ p: 1.5, bgcolor: "#f0fdf4", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>% Conform</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: brandColors.ok }}>{analysis?.qualitativeStats?.percentConform ?? 100}%</Typography>
                </Box>
              </Grid>
              <Grid item xs={6} sm={3}>
                <Box sx={{ p: 1.5, bgcolor: "#f0fdf4", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Absent Count</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: brandColors.ok }}>{analysis?.qualitativeStats?.absentCount ?? 0}</Typography>
                </Box>
              </Grid>
              <Grid item xs={6} sm={3}>
                <Box sx={{ p: 1.5, bgcolor: "#fef2f2", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Detected Count</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: brandColors.err }}>{analysis?.qualitativeStats?.detectedCount ?? 0}</Typography>
                </Box>
              </Grid>
            </Grid>
          )}
        </Paper>
      </Stack>

      {/* RIGHT COLUMN: Analysis Actions & Compare */}
      <Stack spacing={2}>
        <Paper sx={{ p: 2.5 }}>
          <Typography sx={{ fontSize: 14, fontWeight: 700, color: brandColors.sectionTitle, mb: 1.5 }}>
            Analysis Actions
          </Typography>
          <Stack spacing={1.25}>
            <Button
              variant="outlined"
              fullWidth
              startIcon={<TableChartIcon />}
              onClick={() => setDataDialogOpen(true)}
            >
              View Data Table
            </Button>
            <Button
              variant="outlined"
              fullWidth
              startIcon={<FileDownloadIcon />}
              onClick={() => setDataDialogOpen(true)}
            >
              Export Data (Excel)
            </Button>
            <Button
              variant="contained"
              fullWidth
              startIcon={<BookmarkBorderIcon />}
              onClick={() => {
                setSavedSuccess(true);
                setTimeout(() => setSavedSuccess(false), 3000);
              }}
            >
              {savedSuccess ? "Saved to Workspace" : "Save Analysis"}
            </Button>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5 }}>
          <Typography sx={{ fontSize: 14, fontWeight: 700, color: brandColors.sectionTitle, mb: 1.5 }}>
            Quick Compare
          </Typography>
          <Stack spacing={1.25}>
            <Button
              variant="outlined"
              fullWidth
              startIcon={<CompareArrowsIcon />}
              onClick={() => {
                setCompareMode("products");
                setCompareDialogOpen(true);
              }}
            >
              Compare Products
            </Button>
            <Button
              variant="outlined"
              fullWidth
              startIcon={<LocationOnIcon />}
              onClick={() => {
                setCompareMode("locations");
                setCompareDialogOpen(true);
              }}
            >
              Compare Locations
            </Button>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2, bgcolor: "#f8fafc" }}>
          <Typography sx={{ fontSize: 11, fontWeight: 700, color: brandColors.sectionTitle, mb: 0.5, textTransform: "uppercase" }}>
            Drill Down
          </Typography>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            Click on any individual data point in the chart to inspect its underlying laboratory record in read-only mode.
          </Typography>
        </Paper>
      </Stack>

      {/* Modals */}
      <TrendingDataDialog
        open={dataDialogOpen}
        onClose={() => setDataDialogOpen(false)}
        isNumeric={analysis?.isNumeric ?? true}
        testName={analysis?.testDisplayName || criteria.testCode}
        subjectName={analysis?.subjectName || criteria.subjectName}
        unit={analysis?.unit ?? null}
        numericPoints={analysis?.numericPoints}
        qualitativePoints={analysis?.qualitativePoints}
        onSelectRecord={(recId) => {
          const pt = analysis?.numericPoints?.find((p) => p.recordId === recId);
          if (pt) handlePointClick(pt);
          setDataDialogOpen(false);
        }}
      />

      <CompareDialog
        open={compareDialogOpen}
        onClose={() => setCompareDialogOpen(false)}
        initialMode={compareMode}
      />

      <RecordDetailDialog
        open={!!drillDownRecord}
        onClose={() => setDrillDownRecord(null)}
        record={drillDownRecord}
      />
    </Box>
  );
}
