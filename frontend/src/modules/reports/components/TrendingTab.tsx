import { useEffect, useRef, useState } from "react";
import {
  Box, Paper, Typography, TextField, FormControl, InputLabel, Select, MenuItem,
  Button, Grid, Stack, Chip, Divider, IconButton, Tooltip, useTheme, Menu, Alert
} from "@mui/material";
import TableChartIcon from "@mui/icons-material/TableChart";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import CompareArrowsIcon from "@mui/icons-material/CompareArrows";
import LocationOnIcon from "@mui/icons-material/LocationOn";
import {
  ResponsiveContainer, ComposedChart, Line, XAxis, YAxis, Tooltip as RechartsTooltip,
  CartesianGrid, Legend, Dot, BarChart, Bar
} from "recharts";
import {
  NumericTrendPoint,
  QualitativeEventItem,
  TrendAnalysisResult,
  TrendingCriteria,
  SampleCategory,
  ResultRecordItem
} from "../types/reportingTypes";
import { TrendingService, resolveDateRange } from "../services/TrendingService";
import { ReportingService } from "../services/ReportingService";
import { LocationCascadeFilter, hasLocationHierarchy } from "./LocationCascadeFilter";
import { TrendingDataDialog } from "./TrendingDataDialog";
import { CompareDialog } from "./CompareDialog";
import { RecordDetailDialog } from "./RecordDetailDialog";
import { captureSvgAsPng } from "../utils/chartExport";
import { buildTrendCsv, downloadCsv, exportTrendPdfTable, exportTrendPdfWithChart, TrendExportContext } from "../utils/exportTrend";

interface TrendingTabProps {
  initialTestCode?: string;
  initialSubjectName?: string;
}

export function TrendingTab({ initialTestCode, initialSubjectName }: TrendingTabProps) {
  const theme = useTheme();
  const [filterOptions, setFilterOptions] = useState<{
    testCodes: { testCode: string; testDisplayName: string }[];
    subjectNames: string[];
    categories: SampleCategory[];
  }>({ testCodes: [], subjectNames: [], categories: [] });

  const [criteria, setCriteria] = useState<TrendingCriteria>({
    testCode: initialTestCode || "TAMC",
    subjectName: initialSubjectName || "Osteocare Liquid",
    category: "FinishedProduct",
    dateRange: "12m",
    compareWith: "None",
    showMode: "All"
  });

  const [analysis, setAnalysis] = useState<(TrendAnalysisResult & { qualitativeEvents?: QualitativeEventItem[] }) | null>(null);
  const [loading, setLoading] = useState(false);

  // Dialog states
  const [dataDialogOpen, setDataDialogOpen] = useState(false);
  const [compareDialogOpen, setCompareDialogOpen] = useState(false);
  const [compareMode, setCompareMode] = useState<"products" | "locations">("products");
  const [drillDownRecord, setDrillDownRecord] = useState<ResultRecordItem | null>(null);

  // Export menu
  const [exportMenuAnchor, setExportMenuAnchor] = useState<HTMLElement | null>(null);
  const [exportError, setExportError] = useState<string | null>(null);
  const chartContainerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    ReportingService.getFilterOptions()
      .then((opts) => {
        if (opts) {
          setFilterOptions({
            testCodes: opts.testCodes || [],
            subjectNames: opts.subjectNames || [],
            categories: (opts.categories || []) as SampleCategory[]
          });
          if (opts.testCodes?.length > 0 && !initialTestCode) {
            const firstTest = opts.testCodes[0].testCode;
            const firstSubject = opts.subjectNames?.[0] || criteria.subjectName;
            setCriteria((c) => ({
              ...c,
              testCode: firstTest,
              subjectName: firstSubject
            }));
          }
        }
      })
      .catch(() => {});
  }, [initialTestCode]);

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

  const handlePointClick = async (point: NumericTrendPoint) => {
    if (!point.recordId) return;
    try {
      const record = await ReportingService.getResultById(point.recordId);
      setDrillDownRecord(record);
    } catch {
      // Fallback only on network error
      setDrillDownRecord({
        id: point.recordId,
        sampleId: 0,
        testOrderId: 0,
        sourceTable: "ResultRecord",
        sourceId: point.recordId,
        round: 1,
        referenceNumber: point.referenceNumber,
        category: (criteria.category || "FinishedProduct") as SampleCategory,
        subjectName: criteria.subjectName,
        subjectDetail: null,
        batchNumber: null,
        controlNumber: null,
        testCode: criteria.testCode,
        testDisplayName: analysis?.testDisplayName || criteria.testCode,
        resultKind: "Quantitative",
        numericValue: point.value,
        reportedValue: point.reportedValue,
        unit: analysis?.unit ?? null,
        isBelowDetectionLimit: point.value == null,
        detectionLimit: null,
        alertLimit: point.alertLevel ? `${point.alertLevel}` : null,
        actionLimit: point.actionLevel ? `${point.actionLevel}` : null,
        specLimit: point.upperLimit ? `${point.upperLimit}` : null,
        resultLevel: point.resultLevel,
        resultEnteredAt: point.date,
        resultEnteredByUserId: 0,
        resultEnteredByName: "—",
        sampleStatus: "Approved",
        approvalStatus: "Approved",
        approvedByUserId: null,
        approvedByName: null,
        approvedAt: null
      });
    }
  };

  const exportContext = (): TrendExportContext => ({
    testName: analysis?.testDisplayName || criteria.testCode,
    subjectName: analysis?.subjectName || criteria.subjectName,
    unit: analysis?.unit ?? null,
    isNumeric: analysis?.isNumeric ?? true,
    numericPoints: analysis?.numericPoints ?? [],
    qualitativePoints: analysis?.qualitativePoints ?? []
  });

  const handleExportCsv = () => {
    setExportError(null);
    const ctx = exportContext();
    downloadCsv(buildTrendCsv(ctx), `trend_${ctx.testName.toLowerCase().replace(/\s+/g, "_")}_${new Date().toISOString().slice(0, 10)}.csv`);
    setExportMenuAnchor(null);
  };

  const handleExportPdfTable = () => {
    setExportError(null);
    exportTrendPdfTable(exportContext());
    setExportMenuAnchor(null);
  };

  const handleExportPdfWithChart = async () => {
    setExportError(null);
    setExportMenuAnchor(null);
    try {
      const svg = chartContainerRef.current?.querySelector("svg");
      if (!svg) throw new Error("No chart is currently rendered to capture.");
      const imageDataUrl = await captureSvgAsPng(svg);
      exportTrendPdfWithChart(exportContext(), imageDataUrl);
    } catch (e: any) {
      setExportError(e?.message || "Could not capture the chart image for export.");
    }
  };

  return (
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", lg: "280px 1fr 240px" }, gap: 2, alignItems: "start", pb: 4 }}>
      {/* LEFT COLUMN: Analysis Criteria */}
      <Paper sx={{ p: 2.5 }}>
        <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main, mb: 2 }}>
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
              {filterOptions.testCodes.length > 0 ? (
                filterOptions.testCodes.map((t) => (
                  <MenuItem key={t.testCode} value={t.testCode}>
                    {t.testDisplayName || t.testCode}
                  </MenuItem>
                ))
              ) : (
                [
                  <MenuItem key="TAMC" value="TAMC">TAMC (Total Aerobic Count)</MenuItem>,
                  <MenuItem key="TYMC" value="TYMC">TYMC (Total Yeast & Mold)</MenuItem>,
                  <MenuItem key="WATER_TAMC" value="WATER_TAMC">Water TAMC (Purified Water)</MenuItem>,
                  <MenuItem key="PATHOGEN_ECOLI" value="PATHOGEN_ECOLI">E. coli (Pathogen Screening)</MenuItem>
                ]
              )}
            </Select>
          </FormControl>

          {hasLocationHierarchy(criteria.category) ? (
            <LocationCascadeFilter
              category={criteria.category}
              subjectName={criteria.subjectName}
              onSubjectNameChange={(name) => setCriteria((c) => ({ ...c, subjectName: name }))}
            />
          ) : (
            <FormControl fullWidth size="small">
              <InputLabel>Product / Item</InputLabel>
              <Select
                label="Product / Item"
                value={criteria.subjectName}
                onChange={(e) => setCriteria((c) => ({ ...c, subjectName: e.target.value }))}
              >
                {filterOptions.subjectNames.length > 0 ? (
                  filterOptions.subjectNames.map((s) => (
                    <MenuItem key={s} value={s}>
                      {s}
                    </MenuItem>
                  ))
                ) : (
                  [
                    <MenuItem key="Osteocare Liquid" value="Osteocare Liquid">Osteocare Liquid</MenuItem>,
                    <MenuItem key="Feroglobin Syrup" value="Feroglobin Syrup">Feroglobin Syrup</MenuItem>,
                    <MenuItem key="Honey" value="Honey">Honey (Raw Material)</MenuItem>,
                    <MenuItem key="Purified Water" value="Purified Water">Purified Water (Loop 1)</MenuItem>,
                    <MenuItem key="Point W-01" value="Point W-01">Point W-01 (Utilities)</MenuItem>
                  ]
                )}
              </Select>
            </FormControl>
          )}

          <FormControl fullWidth size="small">
            <InputLabel>Category</InputLabel>
            <Select
              label="Category"
              value={criteria.category ?? ""}
              onChange={(e) => setCriteria((c) => ({ ...c, category: (e.target.value || "") as any }))}
            >
              {filterOptions.categories.length > 0 ? (
                filterOptions.categories.map((cat) => (
                  <MenuItem key={cat} value={cat}>
                    {cat}
                  </MenuItem>
                ))
              ) : (
                [
                  <MenuItem key="FinishedProduct" value="FinishedProduct">Finished Product</MenuItem>,
                  <MenuItem key="RawMaterial" value="RawMaterial">Raw Material</MenuItem>,
                  <MenuItem key="Water" value="Water">Water</MenuItem>,
                  <MenuItem key="EnvironmentalMonitoring" value="EnvironmentalMonitoring">Environmental Monitoring</MenuItem>
                ]
              )}
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
            <InputLabel>Compare With</InputLabel>
            <Select
              label="Compare With"
              value={criteria.compareWith}
              onChange={(e) => {
                const value = e.target.value as typeof criteria.compareWith;
                setCriteria((c) => ({ ...c, compareWith: value }));
                // "Previous Period" is a chart-overlay concept, out of this
                // batch's scope - only the two table-based comparisons open
                // the Quick Compare dialog, same as its own two buttons do.
                if (value === "Product Comparison") {
                  setCompareMode("products");
                  setCompareDialogOpen(true);
                } else if (value === "Location Comparison") {
                  setCompareMode("locations");
                  setCompareDialogOpen(true);
                }
              }}
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
              <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main }}>
                Trend Chart
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                {analysis?.testDisplayName || criteria.testCode} — {analysis?.subjectName || criteria.subjectName} ({criteria.dateRange === "12m" ? "Last 12 Months" : criteria.dateRange})
              </Typography>
            </Box>
            <Chip
              size="small"
              label={
                analysis?.isNumeric
                  ? `Live Numeric Trend (${analysis?.unit ?? "CFU/g"})`
                  : "Qualitative Detection Event Log"
              }
              sx={{
                bgcolor: analysis?.isNumeric ? theme.custom.status.notDetected.bg : theme.custom.status.detected.bg,
                color: analysis?.isNumeric ? theme.custom.status.notDetected.text : theme.custom.status.detected.text,
                fontWeight: 700,
                fontSize: 11
              }}
            />
          </Box>

          <Box ref={chartContainerRef} sx={{ minHeight: 320, width: "100%", mt: 2 }}>
            {analysis?.isNumeric ? (
              (!analysis.numericPoints || analysis.numericPoints.length === 0) ? (
                <Box sx={{ height: 320, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", bgcolor: "background.default", borderRadius: 1.5, border: 1, borderColor: "divider" }}>
                  <Typography sx={{ fontSize: 14, fontWeight: 600, color: "text.secondary" }}>
                    No matching records found
                  </Typography>
                  <Typography sx={{ fontSize: 12, color: "text.disabled", mt: 0.5 }}>
                    No numeric results found for {criteria.testCode} on {criteria.subjectName || "the selected parameter"}.
                  </Typography>
                </Box>
              ) : (
                <ResponsiveContainer width="100%" height={320}>
                  <ComposedChart
                    data={analysis.numericPoints ?? []}
                    margin={{ top: 10, right: 30, left: 10, bottom: 25 }}
                  >
                    <CartesianGrid strokeDasharray="3 3" stroke={theme.palette.divider} />
                    <XAxis dataKey="label" tick={{ fontSize: 11, fill: theme.palette.text.secondary }} />
                    <YAxis
                      tick={{ fontSize: 11, fill: theme.palette.text.secondary }}
                      domain={[0, "auto"]}
                      label={{ value: analysis.unit ?? "CFU/mL", angle: -90, position: "insideLeft", fontSize: 11, fill: theme.palette.text.secondary }}
                    />
                    <RechartsTooltip
                      content={({ payload, label }) => {
                        if (!payload || payload.length === 0) return null;
                        const pt = payload[0].payload as NumericTrendPoint;
                        return (
                          <Paper sx={{ p: 1.5, fontSize: 12, boxShadow: "0 4px 12px rgba(0,0,0,0.1)" }}>
                            <Typography sx={{ fontWeight: 700, color: theme.palette.primary.main }}>{label}</Typography>
                            <Typography>Ref: <strong>{pt.referenceNumber}</strong></Typography>
                            <Typography>Result: <strong>{pt.reportedValue} {analysis.unit}</strong></Typography>
                            <Typography>Level: <strong>{pt.resultLevel}</strong></Typography>
                            <Typography sx={{ fontSize: 10, color: "text.secondary", mt: 0.5 }}>Click point to view source record</Typography>
                          </Paper>
                        );
                      }}
                    />
                    <Legend verticalAlign="top" height={36} wrapperStyle={{ fontSize: 12 }} />

                    <Line type="monotone" dataKey="upperLimit" name="Upper Limit (USL)" stroke={theme.custom.status.detected.text} strokeDasharray="5 5" strokeWidth={1.5} dot={false} />
                    <Line type="monotone" dataKey="actionLevel" name="Action Level" stroke={theme.custom.status.action.text} strokeDasharray="3 3" strokeWidth={1.5} dot={false} />
                    <Line type="monotone" dataKey="alertLevel" name="Alert Level" stroke={theme.custom.status.inconclusive.text} strokeDasharray="2 2" strokeWidth={1.5} dot={false} />
                    <Line type="monotone" dataKey="mean" name="Mean" stroke={theme.palette.primary.main} strokeDasharray="4 4" strokeWidth={1.5} dot={false} />

                    <Line
                      type="monotone"
                      dataKey="value"
                      name="Results"
                      stroke={theme.custom.status.info.text}
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
                        let fill = theme.custom.status.info.text;
                        if (isOos) fill = theme.custom.status.detected.text;
                        else if (isAction) fill = theme.custom.status.action.text;
                        else if (isAlert) fill = theme.custom.status.inconclusive.text;

                        return (
                          <circle
                            cx={cx}
                            cy={cy}
                            r={isAlert || isAction || isOos ? 6 : 4.5}
                            fill={fill}
                            stroke={theme.palette.background.paper}
                            strokeWidth={1.5}
                            style={{ cursor: "pointer" }}
                            onClick={() => handlePointClick(payload)}
                          />
                        );
                      }}
                    />
                  </ComposedChart>
                </ResponsiveContainer>
              )
            ) : (
              <Box sx={{ width: "100%" }}>
                {(!analysis?.qualitativeEvents || analysis.qualitativeEvents.length === 0) ? (
                  <Box sx={{ p: 4, textAlign: "center", bgcolor: theme.custom.status.notDetected.bg, borderRadius: 1.5, border: 1, borderColor: "divider" }}>
                    <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.custom.status.notDetected.text }}>
                      No Pathogen Detection Events
                    </Typography>
                    <Typography sx={{ fontSize: 13, color: "text.secondary", mt: 1 }}>
                      Zero detections recorded for {analysis?.testDisplayName || criteria.testCode} in the selected period. All samples conformed (Absent / Negative).
                    </Typography>
                  </Box>
                ) : (
                  <Box sx={{ border: 1, borderColor: "divider", borderRadius: 1.5, overflow: "hidden" }}>
                    <Box sx={{ p: 1.5, bgcolor: theme.custom.status.detected.bg, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                      <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.custom.status.detected.text }}>
                        ⚠️ Pathogen Detection Events ({analysis.qualitativeEvents.length})
                      </Typography>
                      <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                        Click row to view complete result record
                      </Typography>
                    </Box>
                    <Box sx={{ overflowX: "auto" }}>
                      <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 12 }}>
                        <thead>
                          <tr style={{ background: theme.palette.background.default, textAlign: "left", borderBottom: `1px solid ${theme.palette.divider}` }}>
                            <th style={{ padding: "8px 12px", fontWeight: 600 }}>Reference</th>
                            <th style={{ padding: "8px 12px", fontWeight: 600 }}>Item / Location</th>
                            <th style={{ padding: "8px 12px", fontWeight: 600 }}>Pathogen</th>
                            <th style={{ padding: "8px 12px", fontWeight: 600 }}>Result</th>
                            <th style={{ padding: "8px 12px", fontWeight: 600 }}>Date Entered</th>
                            <th style={{ padding: "8px 12px", fontWeight: 600 }}>Entered By</th>
                            <th style={{ padding: "8px 12px", fontWeight: 600 }}>Status</th>
                          </tr>
                        </thead>
                        <tbody>
                          {analysis.qualitativeEvents.map((ev) => (
                            <tr
                              key={ev.id}
                              style={{ borderBottom: `1px solid ${theme.palette.divider}`, cursor: "pointer" }}
                              onClick={async () => {
                                try {
                                  const r = await ReportingService.getResultById(ev.id);
                                  setDrillDownRecord(r);
                                } catch {
                                  // Fallback
                                }
                              }}
                            >
                              <td style={{ padding: "8px 12px", fontWeight: 600, color: theme.palette.primary.main }}>{ev.referenceNumber}</td>
                              <td style={{ padding: "8px 12px" }}>{ev.subjectName} {ev.subjectDetail ? `(${ev.subjectDetail})` : ""}</td>
                              <td style={{ padding: "8px 12px" }}>{ev.testDisplayName}</td>
                              <td style={{ padding: "8px 12px", color: theme.custom.status.detected.text, fontWeight: 700 }}>{ev.reportedValue}</td>
                              <td style={{ padding: "8px 12px", color: theme.palette.text.secondary }}>
                                {new Date(ev.resultEnteredAt).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" })}
                              </td>
                              <td style={{ padding: "8px 12px" }}>{ev.resultEnteredByName}</td>
                              <td style={{ padding: "8px 12px" }}>
                                <Chip size="small" label={ev.approvalStatus} color={ev.approvalStatus === "Approved" ? "success" : "warning"} sx={{ fontSize: 10, height: 20 }} />
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </Box>
                  </Box>
                )}
              </Box>
            )}
          </Box>
        </Paper>

        {/* Statistics Summary (10 Cards for Numeric / Categorical for Qualitative) */}
        <Paper sx={{ p: 2.5 }}>
          <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main, mb: 2 }}>
            Statistics Summary
          </Typography>

          {analysis?.isNumeric && analysis.numericStats ? (
            <Grid container spacing={1.5}>
              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>No. of Results</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: theme.palette.primary.main }}>
                    {analysis.numericStats.numberOfResults}
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Minimum</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800 }}>
                    {analysis.numericStats.minimum}
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Maximum</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800 }}>
                    {analysis.numericStats.maximum}
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Mean</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: theme.palette.primary.main }}>
                    {analysis.numericStats.mean}
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Median</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800 }}>
                    {analysis.numericStats.median}
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Std. Deviation</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800 }}>
                    {analysis.numericStats.standardDeviation}
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: theme.custom.status.notDetected.bg, borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>% Within Spec</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: theme.custom.status.notDetected.text }}>
                    {analysis.numericStats.percentWithinSpec}%
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: theme.custom.status.inconclusive.bg, borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>% Alert Level</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: theme.custom.status.inconclusive.text }}>
                    {analysis.numericStats.percentAlertLevel}%
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: theme.custom.status.action.bg, borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>% Action Level</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: theme.custom.status.action.text }}>
                    {analysis.numericStats.percentActionLevel}%
                  </Typography>
                </Box>
              </Grid>

              <Grid item xs={6} sm={4} md={2.4}>
                <Box sx={{ p: 1.5, bgcolor: analysis.numericStats.outOfSpecCount > 0 ? theme.custom.status.detected.bg : "background.default", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Out of Spec</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: analysis.numericStats.outOfSpecCount > 0 ? theme.custom.status.detected.text : "text.primary" }}>
                    {analysis.numericStats.outOfSpecCount}
                  </Typography>
                </Box>
              </Grid>
            </Grid>
          ) : (
            <Grid container spacing={2}>
              <Grid item xs={6} sm={3}>
                <Box sx={{ p: 1.5, bgcolor: (analysis?.qualitativeEvents?.length ?? 0) > 0 ? theme.custom.status.detected.bg : theme.custom.status.notDetected.bg, borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Detection Events</Typography>
                  <Typography sx={{ fontSize: 18, fontWeight: 800, color: (analysis?.qualitativeEvents?.length ?? 0) > 0 ? theme.custom.status.detected.text : theme.custom.status.notDetected.text }}>
                    {analysis?.qualitativeEvents?.length ?? 0}
                  </Typography>
                </Box>
              </Grid>
              <Grid item xs={6} sm={3}>
                <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Pathogen Parameter</Typography>
                  <Typography sx={{ fontSize: 15, fontWeight: 700 }} noWrap>
                    {analysis?.testDisplayName || criteria.testCode}
                  </Typography>
                </Box>
              </Grid>
              <Grid item xs={6} sm={3}>
                <Box sx={{ p: 1.5, bgcolor: theme.custom.status.notDetected.bg, borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Surveillance Result</Typography>
                  <Typography sx={{ fontSize: 14, fontWeight: 800, color: (analysis?.qualitativeEvents?.length ?? 0) === 0 ? theme.custom.status.notDetected.text : theme.custom.status.detected.text }}>
                    {(analysis?.qualitativeEvents?.length ?? 0) === 0 ? "Conformant / Negative" : "Action Required"}
                  </Typography>
                </Box>
              </Grid>
              <Grid item xs={6} sm={3}>
                <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1, textAlign: "center" }}>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Period Range</Typography>
                  <Typography sx={{ fontSize: 15, fontWeight: 700 }}>
                    {criteria.dateRange.toUpperCase()}
                  </Typography>
                </Box>
              </Grid>
            </Grid>
          )}
        </Paper>
      </Stack>

      {/* RIGHT COLUMN: Analysis Actions & Compare */}
      <Stack spacing={2}>
        <Paper sx={{ p: 2.5 }}>
          <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main, mb: 1.5 }}>
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
              onClick={(e) => setExportMenuAnchor(e.currentTarget)}
            >
              Export
            </Button>
            <Menu anchorEl={exportMenuAnchor} open={!!exportMenuAnchor} onClose={() => setExportMenuAnchor(null)}>
              <MenuItem onClick={handleExportCsv}>CSV (Data Table)</MenuItem>
              <MenuItem onClick={handleExportPdfTable}>PDF (Data Table)</MenuItem>
              <MenuItem onClick={handleExportPdfWithChart}>PDF (Data Table + Chart)</MenuItem>
            </Menu>
            {exportError && <Alert severity="error" sx={{ fontSize: 12 }}>{exportError}</Alert>}
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5 }}>
          <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main, mb: 1.5 }}>
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

        <Paper sx={{ p: 2, bgcolor: "background.default" }}>
          <Typography sx={{ fontSize: 11, fontWeight: 700, color: theme.palette.primary.main, mb: 0.5, textTransform: "uppercase" }}>
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
        testCode={criteria.testCode}
        category={criteria.category || "FinishedProduct"}
        fromDate={resolveDateRange(criteria).fromDate}
        toDate={resolveDateRange(criteria).toDate}
      />

      <RecordDetailDialog
        open={!!drillDownRecord}
        onClose={() => setDrillDownRecord(null)}
        record={drillDownRecord}
      />
    </Box>
  );
}
