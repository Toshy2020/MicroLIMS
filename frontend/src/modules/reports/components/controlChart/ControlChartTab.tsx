import { useEffect, useMemo, useRef, useState } from "react";
import {
  Alert, Box, Button, Checkbox, Chip, Divider, FormControl, FormControlLabel, Grid, InputLabel, MenuItem, Paper,
  Select, Stack, Tab, Table, TableBody, TableCell, TableHead, TableRow, Tabs, TextField, Tooltip, Typography, useTheme
} from "@mui/material";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import ImageIcon from "@mui/icons-material/Image";
import PublishedWithChangesIcon from "@mui/icons-material/PublishedWithChanges";
import { toast } from "sonner";
import { DateField } from "../../../../components/DateField";
import { useHasPermission } from "../../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../../routes/routes";
import { ReportingService } from "../../services/ReportingService";
import { ControlChartService } from "../../services/ControlChartService";
import { LocationCascadeFilter, hasLocationHierarchy } from "../LocationCascadeFilter";
import { RecordDetailDialog } from "../RecordDetailDialog";
import { captureSvgAsPng } from "../../utils/chartExport";
import { downloadCsv } from "../../utils/exportTrend";
import { toDateInputValue } from "../../utils/dateRange";
import { ResultRecordItem, SampleCategory, TestCodeOption } from "../../types/reportingTypes";
import {
  AppliedLimit, CHART_KINDS, CapabilityBasis, ChartKind, ControlChartCriteria, ControlChartPoint, ControlChartResult,
  ControlChartSettings, DEFAULT_SETTINGS, LimitMethod, LimitReviewResult, LimitRounding, RULE_LABELS
} from "../../types/controlChartTypes";
import { SpcChart, SpcChartOptions, fmt } from "./SpcCharts";
import { ApplyCandidate, ApplyLimitsDialog } from "./ApplyLimitsDialog";

const CATEGORY_LABELS: Partial<Record<SampleCategory, string>> = {
  EnvironmentalMonitoring: "Environmental Monitoring",
  Water: "Water",
  AfterCleaning: "After Cleaning",
  FinishedProduct: "Finished Product",
  RawMaterial: "Raw Material",
  PackagingMaterial: "Packaging Material"
};

// Only these configurations carry alert/action limits the analysis can replace.
const LIMIT_CATEGORIES: SampleCategory[] = ["EnvironmentalMonitoring", "Water"];

const METHOD_LABELS: Record<LimitMethod, string> = {
  MovingRange: "Mean ± k·σ (σ from moving range, Minitab I-chart)",
  StandardDeviation: "Mean ± k × standard deviation",
  Percentile: "Percentiles of the data"
};

const ROUNDING_LABELS: Record<LimitRounding, string> = {
  None: "None",
  OneDecimal: "1 decimal",
  TwoDecimals: "2 decimals",
  UpToWholeNumber: "Up to whole number"
};

const BASIS_LABELS: Record<CapabilityBasis, string> = {
  SpecLimit: "Specification limit",
  ActionLimit: "Configured action limit",
  AlertLimit: "Configured alert limit",
  Custom: "Custom LSL / USL"
};

function defaultCriteria(): ControlChartCriteria {
  const to = new Date();
  const from = new Date(to);
  from.setFullYear(from.getFullYear() - 1);
  return {
    category: "EnvironmentalMonitoring",
    testCode: "",
    subjectName: "",
    fromDate: toDateInputValue(from),
    toDate: toDateInputValue(to),
    calculationFrom: "",
    calculationTo: "",
    approvedOnly: false,
    capabilityBasis: "SpecLimit",
    customLsl: "",
    customUsl: ""
  };
}

function methodSummary(s: ControlChartSettings): string {
  if (s.method === "Percentile") return `P${s.alertPercentile} / P${s.actionPercentile} of the results`;
  const sigma = s.method === "StandardDeviation" ? "SD" : `σ (MR span ${s.movingRangeLength})`;
  return `mean + ${s.alertK} / ${s.actionK} ${sigma}`;
}

function capabilityTone(v: number | null): "success" | "warning" | "error" | "default" {
  if (v == null) return "default";
  if (v >= 1.33) return "success";
  if (v >= 1) return "warning";
  return "error";
}

function useDebounced<T>(value: T, ms: number): T {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms);
    return () => clearTimeout(t);
  }, [value, ms]);
  return v;
}

export function ControlChartTab() {
  const theme = useTheme();
  const canManageLimits = useHasPermission(PERMISSIONS.MASTER_DATA_MANAGE);

  const [testCodes, setTestCodes] = useState<TestCodeOption[]>([]);
  const [subjectNames, setSubjectNames] = useState<string[]>([]);
  const [categories, setCategories] = useState<SampleCategory[]>([]);
  const [optionsFailed, setOptionsFailed] = useState(false);

  const [criteria, setCriteria] = useState<ControlChartCriteria>(defaultCriteria);
  const [settings, setSettings] = useState<ControlChartSettings>(DEFAULT_SETTINGS);
  const [view, setView] = useState<"chart" | "review">("chart");
  const [chartKind, setChartKind] = useState<ChartKind>("imr");
  const [options, setOptions] = useState<SpcChartOptions>({
    showLowerLimits: false, showConfiguredLimits: true, showRuleNumbers: true, showValueLabels: true
  });

  const [chart, setChart] = useState<ControlChartResult | null>(null);
  const [review, setReview] = useState<LimitReviewResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [applyCandidates, setApplyCandidates] = useState<ApplyCandidate[] | null>(null);
  const [applyFromChart, setApplyFromChart] = useState(false);
  const [drillDown, setDrillDown] = useState<ResultRecordItem | null>(null);
  const chartRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    ReportingService.getFilterOptions()
      .then((opts) => {
        setTestCodes(opts?.testCodes ?? []);
        setSubjectNames(opts?.subjectNames ?? []);
        setCategories((opts?.categories ?? []) as SampleCategory[]);
        setCriteria((c) => (c.testCode || !opts?.testCodes?.length ? c : { ...c, testCode: opts.testCodes[0].testCode }));
      })
      .catch(() => setOptionsFailed(true));
  }, []);

  const query = useDebounced({ criteria, settings, view }, 350);

  useEffect(() => {
    const { criteria: c, settings: s, view: v } = query;
    if (!c.testCode || (v === "chart" && !c.subjectName)) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    const request = v === "chart"
      ? ControlChartService.getChart(c, s).then((r) => { if (!cancelled) setChart(r); })
      : ControlChartService.getLimitReview(c, s).then((r) => { if (!cancelled) { setReview(r); setSelected(new Set()); } });
    request
      .catch((e: any) => { if (!cancelled) setError(e?.response?.data?.message ?? "The analysis could not be loaded."); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [query, reloadKey]);

  const set = <K extends keyof ControlChartCriteria>(k: K, v: ControlChartCriteria[K]) => setCriteria((c) => ({ ...c, [k]: v }));
  const setS = <K extends keyof ControlChartSettings>(k: K, v: ControlChartSettings[K]) => setSettings((s) => ({ ...s, [k]: v }));
  const numberField = (k: "alertK" | "actionK" | "alertPercentile" | "actionPercentile" | "minimumPoints", label: string) => (
    <TextField size="small" type="number" label={label} value={settings[k]}
      onChange={(e) => setS(k, e.target.value === "" ? 0 : Number(e.target.value))}
      slotProps={{ htmlInput: { step: "any", "aria-label": label } }} sx={{ flex: 1 }} />
  );

  const categoryOptions = useMemo(
    () => Array.from(new Set<SampleCategory>([...LIMIT_CATEGORIES, ...categories])).filter((c) => c !== "GPT"),
    [categories]
  );
  const canApplyCategory = LIMIT_CATEGORIES.includes(criteria.category);

  const handlePointClick = async (p: ControlChartPoint) => {
    try {
      setDrillDown(await ReportingService.getResultById(p.recordId));
    } catch {
      toast.error("The source record could not be loaded.");
    }
  };

  const exportPng = async () => {
    const svg = chartRef.current?.querySelector<SVGSVGElement>(".recharts-wrapper > svg.recharts-surface");
    if (!svg) return;
    try {
      const url = await captureSvgAsPng(svg);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${criteria.testCode}_${criteria.subjectName}_${chartKind}.png`.replace(/\s+/g, "_");
      a.click();
    } catch (e: any) {
      toast.error(e?.message ?? "Could not capture the chart.");
    }
  };

  const exportPointsCsv = () => {
    if (!chart) return;
    const head = ["Date", "Reference", "Reported", "Plotted value", "Moving range", "Result level", "Failed tests", "In calculation"];
    const lines = chart.points.map((p) => [
      p.date.slice(0, 10), p.referenceNumber, p.reportedValue, p.value, p.movingRange ?? "", p.resultLevel, p.rules.join(" "), p.inCalculation ? "Yes" : "No"
    ]);
    downloadCsv([head, ...lines].map((r) => r.map((v) => `"${String(v).replace(/"/g, '""')}"`).join(",")).join("\n"),
      `control_chart_${criteria.testCode}_${criteria.subjectName}.csv`.replace(/\s+/g, "_"));
  };

  const exportReviewCsv = () => {
    if (!review) return;
    const head = ["Location", "Configuration", "n", "Mean", "SD", "Sigma used", "Proposed alert", "Proposed action", "Current alert", "Current action", "Cpk", "Flagged points"];
    const lines = review.rows.flatMap((r) => (r.limitTargets.length ? r.limitTargets : [null]).map((t) => [
      r.subjectName, t?.testType ?? "", r.count, fmt(r.mean), fmt(r.standardDeviation), fmt(r.sigmaUsed),
      fmt(r.proposedAlert), fmt(r.proposedAction), t?.alertLimit ?? fmt(r.currentAlert), t?.actionLimit ?? fmt(r.currentAction),
      fmt(r.cpk), r.flaggedPoints
    ]));
    downloadCsv([head, ...lines].map((r) => r.map((v) => `"${String(v).replace(/"/g, '""')}"`).join(",")).join("\n"),
      `limit_review_${criteria.testCode}_${criteria.category}.csv`);
  };

  // ---- Apply ----------------------------------------------------------

  // From the single chart the limits are estimated over the calculation
  // window, so that window is what the server must recompute over.
  const applyPeriod = applyFromChart
    ? { fromDate: criteria.calculationFrom || criteria.fromDate, toDate: criteria.calculationTo || criteria.toDate }
    : { fromDate: criteria.fromDate, toDate: criteria.toDate };

  const basisSummary = `${methodSummary(settings)}, rounding ${ROUNDING_LABELS[settings.rounding].toLowerCase()}, ` +
    `results ${applyPeriod.fromDate || "start"} to ${applyPeriod.toDate || "now"}${criteria.approvedOnly ? " (approved only)" : ""}`;

  const openApplyFromChart = () => {
    if (!chart) return;
    const s = chart.statistics;
    setApplyFromChart(true);
    setApplyCandidates(chart.limitTargets.map((t) => ({
      target: t, subjectName: chart.subjectName, proposedAlert: s.limits.alertUpper, proposedAction: s.limits.actionUpper,
      count: s.count, belowMinimumPoints: s.belowMinimumPoints
    })));
  };

  const reviewCandidates: (ApplyCandidate & { key: string })[] = useMemo(
    () => (review?.rows ?? []).flatMap((r) => r.limitTargets.map((t) => ({
      key: `${t.kind}:${t.configurationId}`, target: t, subjectName: r.subjectName, proposedAlert: r.proposedAlert,
      proposedAction: r.proposedAction, count: r.count, belowMinimumPoints: r.belowMinimumPoints
    }))),
    [review]
  );

  const openApplyFromReview = () => {
    setApplyFromChart(false);
    setApplyCandidates(reviewCandidates.filter((c) => selected.has(c.key)));
  };

  const submitApply = (items: Parameters<typeof ControlChartService.applyLimits>[2], reason: string, password: string) =>
    ControlChartService.applyLimits(
      { testCode: criteria.testCode, category: criteria.category, approvedOnly: criteria.approvedOnly, ...applyPeriod },
      settings, items, reason, password);

  const onApplied = (applied: AppliedLimit[]) => {
    setApplyCandidates(null);
    toast.success(`Limits updated for ${applied.map((a) => a.location).join(", ")}.`);
    setReloadKey((k) => k + 1);
  };

  // ---- Render ---------------------------------------------------------

  const stats = chart?.statistics;
  const cap = stats?.capability;
  const flaggedPoints = chart?.points.filter((p) => p.rules.length > 0) ?? [];
  const chartTitle = CHART_KINDS.find((k) => k.value === chartKind);

  const statRow = (label: string, value: string, hint?: string) => (
    <Stack direction="row" sx={{ py: 0.4, justifyContent: "space-between" }}>
      <Tooltip title={hint ?? ""} placement="left"><Typography sx={{ fontSize: 13, color: "text.secondary" }}>{label}</Typography></Tooltip>
      <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{value}</Typography>
    </Stack>
  );

  return (
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", lg: "300px 1fr" }, gap: 2, alignItems: "start", pb: 4 }}>
      {/* ---------------- Criteria ---------------- */}
      <Paper sx={{ p: 2.5 }}>
        <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main, mb: 2 }}>Analysis criteria</Typography>
        {optionsFailed && <Alert severity="error" sx={{ mb: 2 }}>The test and location lists could not be loaded. Reload the page to try again.</Alert>}
        <Stack spacing={2}>
          <FormControl fullWidth size="small">
            <InputLabel>Sample category</InputLabel>
            <Select label="Sample category" value={criteria.category}
              onChange={(e) => setCriteria((c) => ({ ...c, category: e.target.value as SampleCategory, subjectName: "" }))}>
              {categoryOptions.map((c) => <MenuItem key={c} value={c}>{CATEGORY_LABELS[c] ?? c}</MenuItem>)}
            </Select>
          </FormControl>
          <FormControl fullWidth size="small">
            <InputLabel>Test</InputLabel>
            <Select label="Test" value={criteria.testCode} onChange={(e) => set("testCode", e.target.value)}>
              {testCodes.map((t) => <MenuItem key={t.testCode} value={t.testCode}>{t.testDisplayName || t.testCode}</MenuItem>)}
            </Select>
          </FormControl>
          {view === "chart" && (hasLocationHierarchy(criteria.category) ? (
            <LocationCascadeFilter category={criteria.category} subjectName={criteria.subjectName}
              onSubjectNameChange={(name) => set("subjectName", name)} />
          ) : (
            <FormControl fullWidth size="small">
              <InputLabel>Product / subject</InputLabel>
              <Select label="Product / subject" value={criteria.subjectName} onChange={(e) => set("subjectName", e.target.value)}>
                {subjectNames.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
              </Select>
            </FormControl>
          ))}
          <Stack direction="row" spacing={1}>
            <DateField label="From" size="small" value={criteria.fromDate} onChange={(v) => set("fromDate", v)} />
            <DateField label="To" size="small" value={criteria.toDate} onChange={(v) => set("toDate", v)} />
          </Stack>
          <FormControlLabel control={<Checkbox size="small" checked={criteria.approvedOnly} onChange={(e) => set("approvedOnly", e.target.checked)} />}
            label={<Typography sx={{ fontSize: 13 }}>Approved results only</Typography>} />

          <Divider />
          <Typography sx={{ fontSize: 13, fontWeight: 700 }}>Limit calculation</Typography>
          <FormControl fullWidth size="small">
            <InputLabel>Method</InputLabel>
            <Select label="Method" value={settings.method} onChange={(e) => setS("method", e.target.value as LimitMethod)}>
              {(Object.keys(METHOD_LABELS) as LimitMethod[]).map((m) => <MenuItem key={m} value={m}>{METHOD_LABELS[m]}</MenuItem>)}
            </Select>
          </FormControl>
          {settings.method === "Percentile" ? (
            <Stack direction="row" spacing={1}>{numberField("alertPercentile", "Alert percentile")}{numberField("actionPercentile", "Action percentile")}</Stack>
          ) : (
            <Stack direction="row" spacing={1}>{numberField("alertK", "Alert k")}{numberField("actionK", "Action k")}</Stack>
          )}
          <Stack direction="row" spacing={1}>
            <FormControl size="small" sx={{ flex: 1 }}>
              <InputLabel>MR span</InputLabel>
              <Select label="MR span" value={settings.movingRangeLength} onChange={(e) => setS("movingRangeLength", Number(e.target.value))}>
                {[2, 3, 4, 5, 6, 7, 8, 9, 10].map((n) => <MenuItem key={n} value={n}>{n}{n === 2 ? " (consecutive)" : ""}</MenuItem>)}
              </Select>
            </FormControl>
            {numberField("minimumPoints", "Min. points")}
          </Stack>
          <FormControl fullWidth size="small">
            <InputLabel>Rounding</InputLabel>
            <Select label="Rounding" value={settings.rounding} onChange={(e) => setS("rounding", e.target.value as LimitRounding)}>
              {(Object.keys(ROUNDING_LABELS) as LimitRounding[]).map((r) => <MenuItem key={r} value={r}>{ROUNDING_LABELS[r]}</MenuItem>)}
            </Select>
          </FormControl>
          {view === "chart" && (
            <>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Estimate limits from (blank = whole period)</Typography>
              <Stack direction="row" spacing={1}>
                <DateField label="From" size="small" value={criteria.calculationFrom} onChange={(v) => set("calculationFrom", v)} />
                <DateField label="To" size="small" value={criteria.calculationTo} onChange={(v) => set("calculationTo", v)} />
              </Stack>
            </>
          )}

          <Divider />
          <Typography sx={{ fontSize: 13, fontWeight: 700 }}>Tests that flag a point</Typography>
          <Box>
            {Object.entries(RULE_LABELS).map(([n, label]) => {
              const rule = Number(n);
              return (
                <FormControlLabel key={n} sx={{ display: "flex", mr: 0 }}
                  control={<Checkbox size="small" checked={settings.rules.includes(rule)}
                    onChange={(e) => setS("rules", e.target.checked ? [...settings.rules, rule].sort() : settings.rules.filter((r) => r !== rule))} />}
                  label={<Typography sx={{ fontSize: 12 }}>{n}: {label}</Typography>} />
              );
            })}
          </Box>

          {view === "chart" && (
            <>
              <Divider />
              <Typography sx={{ fontSize: 13, fontWeight: 700 }}>Process capability against</Typography>
              <FormControl fullWidth size="small">
                <InputLabel>Capability limits</InputLabel>
                <Select label="Capability limits" value={criteria.capabilityBasis} onChange={(e) => set("capabilityBasis", e.target.value as CapabilityBasis)}>
                  {(Object.keys(BASIS_LABELS) as CapabilityBasis[]).map((b) => <MenuItem key={b} value={b}>{BASIS_LABELS[b]}</MenuItem>)}
                </Select>
              </FormControl>
              {criteria.capabilityBasis === "Custom" && (
                <Stack direction="row" spacing={1}>
                  <TextField size="small" type="number" label="LSL" value={criteria.customLsl} onChange={(e) => set("customLsl", e.target.value)} />
                  <TextField size="small" type="number" label="USL" value={criteria.customUsl} onChange={(e) => set("customUsl", e.target.value)} />
                </Stack>
              )}
            </>
          )}
        </Stack>
      </Paper>

      {/* ---------------- Results ---------------- */}
      <Box sx={{ minWidth: 0 }}>
        <Paper sx={{ px: 2, mb: 2 }}>
          <Tabs value={view} onChange={(_, v) => setView(v)}>
            <Tab value="chart" label="Control chart & capability" />
            <Tab value="review" label="Limit review (all locations)" />
          </Tabs>
        </Paper>

        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        {!criteria.testCode && <Alert severity="info">Choose a test to analyse.</Alert>}
        {view === "chart" && criteria.testCode && !criteria.subjectName && <Alert severity="info">Choose a location or product to chart.</Alert>}

        {view === "chart" && chart && criteria.subjectName && (
          <Stack spacing={2}>
            <Paper sx={{ p: 2 }}>
              <Stack direction={{ xs: "column", md: "row" }} spacing={1.5} sx={{ mb: 1.5, alignItems: { md: "center" }, justifyContent: "space-between" }}>
                <Box>
                  <Typography sx={{ fontSize: 15, fontWeight: 700 }}>{chart.testDisplayName} · {chart.subjectName}</Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{chartTitle?.description}</Typography>
                </Box>
                <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                  <FormControl size="small" sx={{ minWidth: 220 }}>
                    <InputLabel>Chart type</InputLabel>
                    <Select label="Chart type" value={chartKind} onChange={(e) => setChartKind(e.target.value as ChartKind)}>
                      {CHART_KINDS.map((k) => <MenuItem key={k.value} value={k.value}>{k.label}</MenuItem>)}
                    </Select>
                  </FormControl>
                  <Tooltip title="Download chart (PNG)"><span><Button size="small" onClick={exportPng}><ImageIcon fontSize="small" /></Button></span></Tooltip>
                  <Tooltip title="Download points (CSV)"><span><Button size="small" onClick={exportPointsCsv}><FileDownloadIcon fontSize="small" /></Button></span></Tooltip>
                </Stack>
              </Stack>
              <Stack direction="row" sx={{ mb: 1, flexWrap: "wrap" }}>
                {([
                  ["showConfiguredLimits", "Configured limits"],
                  ["showLowerLimits", "Lower limits (−2SL, LCL)"],
                  ["showRuleNumbers", "Test numbers"],
                  ["showValueLabels", "Value labels"]
                ] as [keyof SpcChartOptions, string][]).map(([k, label]) => (
                  <FormControlLabel key={k} label={<Typography sx={{ fontSize: 12 }}>{label}</Typography>}
                    control={<Checkbox size="small" checked={options[k]} onChange={(e) => setOptions((o) => ({ ...o, [k]: e.target.checked }))} />} />
                ))}
              </Stack>
              {stats?.belowMinimumPoints && (
                <Alert severity="warning" sx={{ mb: 1 }}>
                  Only {stats.count} result(s) in the calculation window - fewer than the minimum of {settings.minimumPoints} for a statistically based limit.
                </Alert>
              )}
              {chart.imputedPointCount > 0 && (
                <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
                  {chart.imputedPointCount} result(s) below the detection limit are plotted at half the limit.
                </Typography>
              )}
              <Box ref={chartRef} sx={{ opacity: loading ? 0.5 : 1 }}>
                {chartKind === "imr" ? (
                  <>
                    <SpcChart kind="individuals" result={chart} options={options} height={340} onPointClick={handlePointClick} />
                    <SpcChart kind="movingRange" result={chart} options={options} height={220} onPointClick={handlePointClick} />
                  </>
                ) : (
                  <SpcChart kind={chartKind} result={chart} options={options} height={400} onPointClick={handlePointClick} />
                )}
              </Box>
            </Paper>

            <Grid container spacing={2}>
              <Grid size={{ xs: 12, md: 4 }}>
                <Paper sx={{ p: 2, height: "100%" }}>
                  <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1 }}>Statistics</Typography>
                  {statRow("Results (n)", String(stats?.count ?? 0))}
                  {statRow("Mean", fmt(stats?.mean))}
                  {statRow("Median", fmt(stats?.median))}
                  {statRow("Min / Max", `${fmt(stats?.min)} / ${fmt(stats?.max)}`)}
                  {statRow("Standard deviation", fmt(stats?.standardDeviation), "Overall sample SD (n−1)")}
                  {statRow("σ within (MR̄/d2)", fmt(stats?.sigmaWithin), "Short-term σ from the average moving range")}
                  {statRow("MR̄", fmt(stats?.movingRangeMean))}
                </Paper>
              </Grid>
              <Grid size={{ xs: 12, md: 4 }}>
                <Paper sx={{ p: 2, height: "100%" }}>
                  <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1 }}>Proposed limits · {methodSummary(settings)}</Typography>
                  {statRow("Alert", fmt(stats?.limits.alertUpper))}
                  {statRow("Action", fmt(stats?.limits.actionUpper))}
                  {statRow("Current alert / action", `${fmt(chart.currentLimits.alert)} / ${fmt(chart.currentLimits.action)}`)}
                  {statRow("Specification", chart.currentLimits.specUpper != null ? fmt(chart.currentLimits.specUpper) : "—")}
                  {statRow("Points flagged", String(flaggedPoints.length))}
                  {canApplyCategory && (
                    <Box sx={{ mt: 1.5 }}>
                      {chart.limitTargets.length === 0 ? (
                        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>No active configuration matches this location and test.</Typography>
                      ) : (
                        <Tooltip title={canManageLimits ? "" : "Needs the MasterData.Manage permission"}>
                          <span>
                            <Button fullWidth variant="contained" size="small" startIcon={<PublishedWithChangesIcon />}
                              disabled={!canManageLimits || stats?.limits.actionUpper == null} onClick={openApplyFromChart}>
                              Apply to configuration
                            </Button>
                          </span>
                        </Tooltip>
                      )}
                    </Box>
                  )}
                </Paper>
              </Grid>
              <Grid size={{ xs: 12, md: 4 }}>
                <Paper sx={{ p: 2, height: "100%" }}>
                  <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1 }}>
                    Process capability · {cap?.lsl != null ? `LSL ${fmt(cap.lsl)} ` : ""}{cap?.usl != null ? `USL ${fmt(cap.usl)}` : "no limit"}
                  </Typography>
                  <Stack direction="row" spacing={1} sx={{ mb: 1 }}>
                    {(["cp", "cpk", "pp", "ppk"] as const).map((k) => (
                      <Chip key={k} size="small" variant="outlined" color={capabilityTone(cap?.[k] ?? null)}
                        label={`${k === "cp" ? "Cp" : k === "cpk" ? "Cpk" : k === "pp" ? "Pp" : "Ppk"} ${fmt(cap?.[k] ?? null)}`} />
                    ))}
                  </Stack>
                  {statRow("Cpu / Cpl", `${fmt(cap?.cpu)} / ${fmt(cap?.cpl)}`)}
                  {statRow("Observed outside limits", cap?.observedPercentOutside != null ? `${fmt(cap.observedPercentOutside)} %` : "—")}
                  {statRow("Expected outside (normal)", cap?.expectedPercentOutside != null ? `${fmt(cap.expectedPercentOutside, 3)} %` : "—")}
                  <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 1 }}>
                    Cp/Cpk use σ within, Pp/Ppk the overall SD. With only an upper limit Cpk = Cpu. Indices assume roughly normal data -
                    microbial counts are usually skewed, so read them with the capability histogram.
                  </Typography>
                </Paper>
              </Grid>
            </Grid>

            {flaggedPoints.length > 0 && (
              <Paper sx={{ p: 2 }}>
                <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1 }}>Flagged points</Typography>
                <Table size="small">
                  <TableHead><TableRow><TableCell>Date</TableCell><TableCell>Reference</TableCell><TableCell align="right">Result</TableCell><TableCell>Failed tests</TableCell></TableRow></TableHead>
                  <TableBody>
                    {flaggedPoints.map((p) => (
                      <TableRow key={p.recordId} hover sx={{ cursor: "pointer" }} onClick={() => handlePointClick(p)}>
                        <TableCell>{new Date(p.date).toLocaleDateString("en-GB")}</TableCell>
                        <TableCell>{p.referenceNumber}</TableCell>
                        <TableCell align="right">{p.reportedValue}</TableCell>
                        <TableCell>{p.rules.map((r) => `${r}: ${RULE_LABELS[r]}`).join("; ")}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Paper>
            )}
          </Stack>
        )}

        {view === "review" && review && criteria.testCode && (
          <Paper sx={{ p: 2 }}>
            <Stack direction={{ xs: "column", md: "row" }} spacing={1} sx={{ mb: 1.5, justifyContent: "space-between" }}>
              <Box>
                <Typography sx={{ fontSize: 15, fontWeight: 700 }}>{review.testDisplayName} · {CATEGORY_LABELS[review.category] ?? review.category}</Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Proposed limits: {methodSummary(settings)}, rounding {ROUNDING_LABELS[settings.rounding].toLowerCase()}</Typography>
              </Box>
              <Stack direction="row" spacing={1}>
                <Button size="small" startIcon={<FileDownloadIcon />} onClick={exportReviewCsv}>CSV</Button>
                {canApplyCategory && (
                  <Tooltip title={canManageLimits ? "" : "Needs the MasterData.Manage permission"}>
                    <span>
                      <Button size="small" variant="contained" startIcon={<PublishedWithChangesIcon />}
                        disabled={!canManageLimits || selected.size === 0} onClick={openApplyFromReview}>
                        Apply selected ({selected.size})
                      </Button>
                    </span>
                  </Tooltip>
                )}
              </Stack>
            </Stack>
            <Box sx={{ overflowX: "auto", opacity: loading ? 0.5 : 1 }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    {canApplyCategory && <TableCell padding="checkbox" />}
                    <TableCell>Location</TableCell>
                    <TableCell>Configuration</TableCell>
                    <TableCell align="right">n</TableCell>
                    <TableCell align="right">Mean</TableCell>
                    <TableCell align="right">SD</TableCell>
                    <TableCell align="right">σ used</TableCell>
                    <TableCell align="right" sx={{ bgcolor: theme.custom.status.inconclusive.bg }}>New alert</TableCell>
                    <TableCell align="right" sx={{ bgcolor: theme.custom.status.inconclusive.bg }}>New action</TableCell>
                    <TableCell align="right">Current alert</TableCell>
                    <TableCell align="right">Current action</TableCell>
                    <TableCell align="right">Δ alert</TableCell>
                    <TableCell align="right">Δ action</TableCell>
                    <TableCell align="right">Cpk</TableCell>
                    <TableCell align="right">Flagged</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {review.rows.flatMap((r) => {
                    const lines = r.limitTargets.length ? r.limitTargets : [null];
                    return lines.map((t) => {
                      const key = t ? `${t.kind}:${t.configurationId}` : `none:${r.subjectName}`;
                      const delta = (a: number | null, b: number | null) => (a != null && b != null ? fmt(a - b) : "—");
                      const selectable = !!t && r.proposedAction != null && r.proposedAlert != null;
                      return (
                        <TableRow key={key} hover>
                          {canApplyCategory && (
                            <TableCell padding="checkbox">
                              <Checkbox size="small" disabled={!selectable || !canManageLimits} checked={selected.has(key)}
                                slotProps={{ input: { "aria-label": `Select ${r.subjectName}` } }}
                                onChange={(e) => setSelected((s) => {
                                  const next = new Set(s);
                                  if (e.target.checked) next.add(key); else next.delete(key);
                                  return next;
                                })} />
                            </TableCell>
                          )}
                          <TableCell>
                            <Button size="small" sx={{ p: 0, minWidth: 0, textTransform: "none", fontWeight: 600 }}
                              onClick={() => { setCriteria((c) => ({ ...c, subjectName: r.subjectName })); setView("chart"); }}>
                              {r.subjectName}
                            </Button>
                            {t?.detail && <Typography sx={{ fontSize: 11, color: "text.secondary" }}>{t.detail}</Typography>}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12 }}>{t ? (t.testType ?? t.testCode) : "Not configured"}</TableCell>
                          <TableCell align="right" sx={{ color: r.belowMinimumPoints ? "error.main" : undefined, fontWeight: r.belowMinimumPoints ? 700 : undefined }}>{r.count}</TableCell>
                          <TableCell align="right">{fmt(r.mean)}</TableCell>
                          <TableCell align="right">{fmt(r.standardDeviation)}</TableCell>
                          <TableCell align="right">{fmt(r.sigmaUsed)}</TableCell>
                          <TableCell align="right" sx={{ fontWeight: 700 }}>{fmt(r.proposedAlert)}</TableCell>
                          <TableCell align="right" sx={{ fontWeight: 700 }}>{fmt(r.proposedAction)}</TableCell>
                          <TableCell align="right">{t?.alertLimit || fmt(r.currentAlert)}</TableCell>
                          <TableCell align="right">{t?.actionLimit || fmt(r.currentAction)}</TableCell>
                          <TableCell align="right">{delta(r.proposedAlert, r.currentAlert)}</TableCell>
                          <TableCell align="right">{delta(r.proposedAction, r.currentAction)}</TableCell>
                          <TableCell align="right"><Chip size="small" variant="outlined" color={capabilityTone(r.cpk)} label={fmt(r.cpk)} /></TableCell>
                          <TableCell align="right" sx={{ color: r.flaggedPoints ? "error.main" : undefined }}>{r.flaggedPoints}</TableCell>
                        </TableRow>
                      );
                    });
                  })}
                </TableBody>
              </Table>
            </Box>
            <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 1 }}>
              Red n = fewer results than the minimum. Cpk is against the specification limit. Select locations and apply to replace their configured alert and action limits under your electronic signature.
            </Typography>
          </Paper>
        )}
      </Box>

      <ApplyLimitsDialog
        open={applyCandidates !== null}
        candidates={applyCandidates ?? []}
        basisSummary={basisSummary}
        onCancel={() => setApplyCandidates(null)}
        onSubmit={submitApply}
        onApplied={onApplied}
      />
      <RecordDetailDialog open={!!drillDown} record={drillDown} onClose={() => setDrillDown(null)} />
    </Box>
  );
}
