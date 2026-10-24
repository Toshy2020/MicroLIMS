import { cloneElement, useMemo } from "react";
import { Box, Paper, Typography, useTheme } from "@mui/material";
import {
  Area, Bar, CartesianGrid, Cell, ComposedChart, Legend, Line, ReferenceLine, ResponsiveContainer,
  Tooltip as RechartsTooltip, XAxis, YAxis
} from "recharts";
import { ChartKind, ControlChartPoint, ControlChartResult, MonthlyBox } from "../../types/controlChartTypes";

// Draws the server-computed control chart, capability and distribution
// results. No statistic is derived here - every limit, σ, moving range,
// rule flag, bin and quartile arrives in ControlChartResult.

export interface SpcChartOptions {
  showLowerLimits: boolean;
  showConfiguredLimits: boolean;
  showRuleNumbers: boolean;
  showValueLabels: boolean;
}

interface SpcChartProps {
  kind: Exclude<ChartKind, "imr">;
  result: ControlChartResult;
  options: SpcChartOptions;
  height?: number;
  // A fixed pixel width renders without ResponsiveContainer, synchronously -
  // used to capture charts off-screen for a report.
  width?: number;
  onPointClick?: (point: ControlChartPoint) => void;
}

export function fmt(v: number | null | undefined, digits = 2): string {
  if (v == null || Number.isNaN(v)) return "—";
  return String(Number(v.toFixed(digits)));
}

function dateLabel(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "2-digit" });
}

function useSpcColors() {
  const theme = useTheme();
  const s = theme.custom.status;
  return {
    result: theme.custom.chartPalette[0],
    mean: s.notDetected.text,
    action: s.detected.text,
    alert: theme.custom.chartPalette[6] ?? s.info.text,
    configuredAlert: s.inconclusive.text,
    configuredAction: s.purple.text,
    spec: s.detected.text,
    flagged: s.detected.text,
    muted: theme.palette.text.secondary,
    grid: theme.palette.divider,
    paper: theme.palette.background.paper,
    level: {
      WithinLimit: theme.custom.chartPalette[0],
      AlertLevel: s.inconclusive.text,
      ActionLevel: s.action.text,
      OutOfSpecification: s.detected.text,
      NotApplicable: s.pending.text
    } as Record<string, string>
  };
}

type Colors = ReturnType<typeof useSpcColors>;

interface Row extends ControlChartPoint {
  label: string;
  index: number;
  flagged: boolean;
}

function limitLine(y: number | null | undefined, color: string, text: string, opts: { dashed?: boolean; show?: boolean; showLabel: boolean }) {
  if (y == null || opts.show === false) return null;
  return (
    <ReferenceLine
      key={text}
      y={y}
      stroke={color}
      strokeWidth={1.5}
      strokeDasharray={opts.dashed ? "6 4" : undefined}
      ifOverflow="extendDomain"
      label={opts.showLabel ? { value: `${text}=${fmt(y)}`, position: "right", fontSize: 11, fill: color } : undefined}
    />
  );
}

function PointTooltip({ active, payload, unit, mrMode }: any) {
  if (!active || !payload?.length) return null;
  const p = payload[0].payload as Row;
  return (
    <Paper sx={{ p: 1.25, fontSize: 12 }}>
      <Typography sx={{ fontWeight: 700, fontSize: 12 }}>{dateLabel(p.date)} · {p.referenceNumber}</Typography>
      <Typography sx={{ fontSize: 12 }}>
        Result: <strong>{p.reportedValue}</strong> {unit ?? ""}{p.isImputed ? ` (plotted at ${fmt(p.value)})` : ""}
      </Typography>
      {mrMode && <Typography sx={{ fontSize: 12 }}>Moving range: <strong>{fmt(p.movingRange)}</strong></Typography>}
      <Typography sx={{ fontSize: 12 }}>Level: {p.resultLevel}</Typography>
      {!p.inCalculation && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Outside the limit calculation window</Typography>}
      {p.rules.length > 0 && (
        <Typography sx={{ fontSize: 12, color: "error.main", fontWeight: 600 }}>Failed test(s): {p.rules.join(", ")}</Typography>
      )}
    </Paper>
  );
}

// Minitab convention: a failing point is a red square with its test number.
function makeDot(c: Colors, opts: SpcChartOptions, onClick?: (p: ControlChartPoint) => void, valueKey: "value" | "movingRange" = "value") {
  return (props: any) => {
    const { cx, cy, payload } = props;
    if (cx == null || cy == null || payload?.[valueKey] == null) return <g key={props.key} />;
    const flagged = valueKey === "value" && payload.rules.length > 0;
    const faded = !payload.inCalculation;
    return (
      <g key={props.key} style={{ cursor: onClick ? "pointer" : undefined }} onClick={() => onClick?.(payload)}>
        {flagged ? (
          <rect x={cx - 4.5} y={cy - 4.5} width={9} height={9} fill={c.flagged} stroke={c.paper} strokeWidth={1} />
        ) : (
          <circle cx={cx} cy={cy} r={3.5} fill={faded ? c.paper : c.result} stroke={c.result} strokeWidth={1.5} />
        )}
        {flagged && opts.showRuleNumbers && (
          <text x={cx} y={cy - 9} textAnchor="middle" fontSize={10} fontWeight={700} fill={c.flagged}>
            {payload.rules.join(",")}
          </text>
        )}
      </g>
    );
  };
}

function ChartFrame({ children, height, width }: { children: React.ReactElement<{ width?: number; height?: number }>; height: number; width?: number }) {
  if (width) return <Box sx={{ width, height }}>{cloneElement(children, { width, height })}</Box>;
  return (
    <Box sx={{ width: "100%", height }}>
      <ResponsiveContainer width="100%" height="100%">{children}</ResponsiveContainer>
    </Box>
  );
}

export function SpcChart({ kind, result, options, height = 360, width, onPointClick }: SpcChartProps) {
  const c = useSpcColors();
  const rows: Row[] = useMemo(
    () => result.points.map((p, index) => ({ ...p, index, label: dateLabel(p.date), flagged: p.rules.length > 0 })),
    [result.points]
  );
  const stats = result.statistics;
  const L = stats.limits;
  const showLabel = options.showValueLabels;
  const margin = { top: 16, right: showLabel ? 92 : 24, left: 8, bottom: 8 };
  const yLabel = { value: result.unit ?? "", angle: -90, position: "insideLeft" as const, fontSize: 12, fill: c.muted };
  const axisTick = { fontSize: 11, fill: c.muted };

  if (rows.length === 0) {
    return (
      <Box sx={{ height, display: "flex", alignItems: "center", justifyContent: "center", color: "text.secondary", fontSize: 13 }}>
        No numeric results for {result.testDisplayName} at {result.subjectName} in this period.
      </Box>
    );
  }

  const configuredLines = options.showConfiguredLimits
    ? [
        <Line key="cfgAlert" type="stepAfter" dataKey="alertLimit" name="Configured alert" stroke={c.configuredAlert} strokeDasharray="6 4" strokeWidth={1.5} dot={false} isAnimationActive={false} connectNulls />,
        <Line key="cfgAction" type="stepAfter" dataKey="actionLimit" name="Configured action" stroke={c.configuredAction} strokeDasharray="6 4" strokeWidth={1.5} dot={false} isAnimationActive={false} connectNulls />,
        <Line key="cfgSpec" type="stepAfter" dataKey="specLimit" name="Specification" stroke={c.spec} strokeDasharray="2 3" strokeWidth={1.5} dot={false} isAnimationActive={false} connectNulls />
      ]
    : [];

  const calcLimitLines = (sigmaLabels: boolean) => [
    limitLine(stats.mean, c.mean, sigmaLabels ? "X̄" : "Mean", { showLabel }),
    limitLine(L.actionUpper, c.action, sigmaLabels ? "UCL" : "Action", { showLabel }),
    limitLine(L.alertUpper, c.alert, sigmaLabels ? "+2SL" : "Alert", { showLabel }),
    limitLine(L.alertLower, c.alert, "-2SL", { showLabel, show: options.showLowerLimits }),
    limitLine(L.actionLower, c.action, "LCL", { showLabel, show: options.showLowerLimits })
  ];

  const tooltip = <RechartsTooltip content={<PointTooltip unit={result.unit} mrMode={kind === "movingRange"} />} />;
  const legend = <Legend verticalAlign="top" height={28} wrapperStyle={{ fontSize: 12 }} />;

  switch (kind) {
    case "individuals":
      return (
        <ChartFrame height={height} width={width}>
          <ComposedChart data={rows} margin={margin}>
            <CartesianGrid vertical={false} stroke={c.grid} />
            <XAxis dataKey="label" tick={axisTick} minTickGap={16} />
            <YAxis tick={axisTick} label={yLabel} />
            {tooltip}
            {legend}
            {calcLimitLines(true)}
            {configuredLines}
            <Line type="linear" dataKey="value" name="Result" stroke={c.result} strokeWidth={1.5}
              dot={makeDot(c, options, onPointClick)} activeDot={false} isAnimationActive={false} />
          </ComposedChart>
        </ChartFrame>
      );

    case "movingRange":
      return (
        <ChartFrame height={height} width={width}>
          <ComposedChart data={rows} margin={margin}>
            <CartesianGrid vertical={false} stroke={c.grid} />
            <XAxis dataKey="label" tick={axisTick} minTickGap={16} />
            <YAxis tick={axisTick} label={{ ...yLabel, value: "Moving range" }} />
            {tooltip}
            {legend}
            {limitLine(stats.movingRangeMean, c.mean, "MR̄", { showLabel })}
            {limitLine(stats.movingRangeUcl, c.action, "UCL", { showLabel })}
            {limitLine(stats.movingRangeLcl && stats.movingRangeLcl > 0 ? stats.movingRangeLcl : null, c.action, "LCL", { showLabel })}
            <Line type="linear" dataKey="movingRange" name="Moving range" stroke={c.result} strokeWidth={1.5}
              dot={makeDot(c, options, onPointClick, "movingRange")} activeDot={false} isAnimationActive={false} />
          </ComposedChart>
        </ChartFrame>
      );

    case "run":
    case "scatter":
    case "area":
      return (
        <ChartFrame height={height} width={width}>
          <ComposedChart data={rows} margin={margin}>
            <CartesianGrid vertical={false} stroke={c.grid} />
            <XAxis dataKey="label" tick={axisTick} minTickGap={16} />
            <YAxis tick={axisTick} label={yLabel} domain={[0, "auto"]} />
            {tooltip}
            {legend}
            {limitLine(stats.mean, c.mean, "Mean", { showLabel })}
            {configuredLines}
            {kind === "area" ? (
              <Area type="monotone" dataKey="value" name="Result" stroke={c.result} fill={c.result} fillOpacity={0.18}
                dot={makeDot(c, options, onPointClick)} isAnimationActive={false} />
            ) : (
              <Line type={kind === "run" ? "monotone" : "linear"} dataKey="value" name="Result"
                stroke={kind === "scatter" ? "none" : c.result} strokeWidth={kind === "scatter" ? 0 : 2}
                dot={makeDot(c, options, onPointClick)} activeDot={false} isAnimationActive={false} />
            )}
          </ComposedChart>
        </ChartFrame>
      );

    case "bar":
      return (
        <ChartFrame height={height} width={width}>
          <ComposedChart data={rows} margin={margin}>
            <CartesianGrid vertical={false} stroke={c.grid} />
            <XAxis dataKey="label" tick={axisTick} minTickGap={16} />
            <YAxis tick={axisTick} label={yLabel} domain={[0, "auto"]} />
            {tooltip}
            {legend}
            {limitLine(stats.mean, c.mean, "Mean", { showLabel })}
            {configuredLines}
            <Bar dataKey="value" name="Result" isAnimationActive={false} onClick={(d: any) => onPointClick?.(d?.payload ?? d)}>
              {rows.map((r) => (
                <Cell key={r.recordId} fill={r.flagged ? c.flagged : c.level[r.resultLevel] ?? c.result} cursor={onPointClick ? "pointer" : undefined} />
              ))}
            </Bar>
          </ComposedChart>
        </ChartFrame>
      );

    case "histogram":
      return <HistogramChart result={result} colors={c} height={height} width={width} showLabel={showLabel} />;

    case "boxplot":
      return <MonthlyBoxPlot result={result} colors={c} height={height} width={width} options={options} />;

    case "monthly":
      return <MonthlySummaryChart result={result} colors={c} height={height} width={width} options={options} />;
  }
}

// ---- Capability histogram ------------------------------------------------

function HistogramChart({ result, colors: c, height, width, showLabel }: { result: ControlChartResult; colors: Colors; height: number; width?: number; showLabel: boolean }) {
  const bins = result.statistics.histogram;
  const cap = result.statistics.capability;
  const data = bins.map((b) => ({ ...b, mid: (b.from + b.to) / 2, range: `${fmt(b.from)} – ${fmt(b.to)}` }));
  if (data.length === 0) return null;
  const binWidth = bins[0].to - bins[0].from;
  const lo = Math.min(bins[0].from, cap.lsl ?? Infinity);
  const hi = Math.max(bins[bins.length - 1].to, cap.usl ?? -Infinity);
  const ref = (x: number | null, color: string, text: string, dashed = false) =>
    x == null ? null : (
      <ReferenceLine key={text} x={x} stroke={color} strokeWidth={1.75} strokeDasharray={dashed ? "6 4" : undefined}
        label={showLabel ? { value: `${text}=${fmt(x)}`, position: "top", fontSize: 11, fill: color } : undefined} />
    );

  return (
    <ChartFrame height={height} width={width}>
        <ComposedChart data={data} margin={{ top: 24, right: 24, left: 8, bottom: 8 }} barCategoryGap={1}>
          <CartesianGrid vertical={false} stroke={c.grid} />
          <XAxis dataKey="mid" type="number" domain={[lo - binWidth / 2, hi + binWidth / 2]} tick={{ fontSize: 11, fill: c.muted }}
            tickFormatter={(v) => fmt(v, 1)} label={{ value: result.unit ?? "", position: "insideBottom", offset: -2, fontSize: 12, fill: c.muted }} />
          <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: c.muted }} label={{ value: "Frequency", angle: -90, position: "insideLeft", fontSize: 12, fill: c.muted }} />
          <RechartsTooltip
            content={({ active, payload }: any) => {
              if (!active || !payload?.length) return null;
              const b = payload[0].payload;
              return (
                <Paper sx={{ p: 1.25, fontSize: 12 }}>
                  <Typography sx={{ fontSize: 12, fontWeight: 700 }}>{b.range}</Typography>
                  <Typography sx={{ fontSize: 12 }}>Results: <strong>{b.count}</strong></Typography>
                  {b.expectedCount != null && <Typography sx={{ fontSize: 12 }}>Normal fit: {fmt(b.expectedCount, 1)}</Typography>}
                </Paper>
              );
            }}
          />
          <Legend verticalAlign="top" height={28} wrapperStyle={{ fontSize: 12 }} />
          <Bar dataKey="count" name="Results" fill={c.result} fillOpacity={0.75} isAnimationActive={false} />
          <Line type="monotone" dataKey="expectedCount" name="Normal curve" stroke={c.mean} strokeWidth={2} dot={false} isAnimationActive={false} />
          {ref(cap.lsl, c.spec, "LSL")}
          {ref(cap.usl, c.spec, "USL")}
          {ref(result.statistics.mean, c.mean, "Mean", true)}
        </ComposedChart>
    </ChartFrame>
  );
}

// ---- Box plot by month -----------------------------------------------------

function MonthlyBoxPlot({ result, colors: c, height, width, options }: { result: ControlChartResult; colors: Colors; height: number; width?: number; options: SpcChartOptions }) {
  // The bar spans min..max of the month; the shape maps quartiles into it
  // with the same linear scale, so whiskers, box and outliers line up.
  const data = result.monthly.map((m) => ({ ...m, span: [m.box.min, m.box.max] as [number, number] }));
  const showLabel = options.showValueLabels;

  const shape = (props: any) => {
    const { x, y, width, height: h, payload } = props as { x: number; y: number; width: number; height: number; payload: MonthlyBox };
    const b = payload.box;
    const range = b.max - b.min;
    const py = (v: number) => (range === 0 ? y : y + (h * (b.max - v)) / range);
    const cx = x + width / 2;
    const bw = Math.min(width * 0.6, 42);
    return (
      <g>
        <line x1={cx} x2={cx} y1={py(b.upperWhisker)} y2={py(b.q3)} stroke={c.result} strokeWidth={1.5} />
        <line x1={cx} x2={cx} y1={py(b.q1)} y2={py(b.lowerWhisker)} stroke={c.result} strokeWidth={1.5} />
        <line x1={cx - bw / 4} x2={cx + bw / 4} y1={py(b.upperWhisker)} y2={py(b.upperWhisker)} stroke={c.result} strokeWidth={1.5} />
        <line x1={cx - bw / 4} x2={cx + bw / 4} y1={py(b.lowerWhisker)} y2={py(b.lowerWhisker)} stroke={c.result} strokeWidth={1.5} />
        <rect x={cx - bw / 2} y={py(b.q3)} width={bw} height={Math.max(1, py(b.q1) - py(b.q3))} fill={c.result} fillOpacity={0.2} stroke={c.result} strokeWidth={1.5} />
        <line x1={cx - bw / 2} x2={cx + bw / 2} y1={py(b.median)} y2={py(b.median)} stroke={c.mean} strokeWidth={2.5} />
        {b.outliers.map((o, i) => (
          <circle key={i} cx={cx} cy={py(o)} r={3.5} fill={c.paper} stroke={c.flagged} strokeWidth={1.5} />
        ))}
      </g>
    );
  };

  return (
    <ChartFrame height={height} width={width}>
        <ComposedChart data={data} margin={{ top: 16, right: showLabel ? 92 : 24, left: 8, bottom: 8 }}>
          <CartesianGrid vertical={false} stroke={c.grid} />
          <XAxis dataKey="month" tick={{ fontSize: 11, fill: c.muted }} />
          <YAxis tick={{ fontSize: 11, fill: c.muted }} domain={[0, "auto"]} label={{ value: result.unit ?? "", angle: -90, position: "insideLeft", fontSize: 12, fill: c.muted }} />
          <RechartsTooltip
            content={({ active, payload }: any) => {
              if (!active || !payload?.length) return null;
              const m = payload[0].payload as MonthlyBox;
              return (
                <Paper sx={{ p: 1.25, fontSize: 12 }}>
                  <Typography sx={{ fontSize: 12, fontWeight: 700 }}>{m.month} · n={m.count}</Typography>
                  <Typography sx={{ fontSize: 12 }}>Median {fmt(m.box.median)} · Q1 {fmt(m.box.q1)} · Q3 {fmt(m.box.q3)}</Typography>
                  <Typography sx={{ fontSize: 12 }}>Min {fmt(m.box.min)} · Max {fmt(m.box.max)} · Mean {fmt(m.mean)}</Typography>
                  {m.box.outliers.length > 0 && <Typography sx={{ fontSize: 12, color: "error.main" }}>Outliers: {m.box.outliers.map((o) => fmt(o)).join(", ")}</Typography>}
                </Paper>
              );
            }}
          />
          {options.showConfiguredLimits && limitLine(result.currentLimits.alert, c.configuredAlert, "Alert", { dashed: true, showLabel })}
          {options.showConfiguredLimits && limitLine(result.currentLimits.action, c.configuredAction, "Action", { dashed: true, showLabel })}
          <Bar dataKey="span" name="Monthly distribution" shape={shape} isAnimationActive={false} />
        </ComposedChart>
    </ChartFrame>
  );
}

// ---- Monthly summary -------------------------------------------------------

function MonthlySummaryChart({ result, colors: c, height, width, options }: { result: ControlChartResult; colors: Colors; height: number; width?: number; options: SpcChartOptions }) {
  const data = result.monthly.map((m) => ({ month: m.month, mean: m.mean, max: m.box.max, median: m.box.median, count: m.count }));
  const showLabel = options.showValueLabels;
  return (
    <ChartFrame height={height} width={width}>
        <ComposedChart data={data} margin={{ top: 16, right: showLabel ? 92 : 24, left: 8, bottom: 8 }}>
          <CartesianGrid vertical={false} stroke={c.grid} />
          <XAxis dataKey="month" tick={{ fontSize: 11, fill: c.muted }} />
          <YAxis tick={{ fontSize: 11, fill: c.muted }} domain={[0, "auto"]} label={{ value: result.unit ?? "", angle: -90, position: "insideLeft", fontSize: 12, fill: c.muted }} />
          <RechartsTooltip formatter={(v: any) => fmt(Number(v))} />
          <Legend verticalAlign="top" height={28} wrapperStyle={{ fontSize: 12 }} />
          <Bar dataKey="mean" name="Monthly mean" fill={c.result} fillOpacity={0.75} isAnimationActive={false} />
          <Line type="monotone" dataKey="max" name="Monthly maximum" stroke={c.action} strokeWidth={2} isAnimationActive={false} />
          <Line type="monotone" dataKey="median" name="Monthly median" stroke={c.mean} strokeWidth={1.5} strokeDasharray="4 3" isAnimationActive={false} />
          {options.showConfiguredLimits && limitLine(result.currentLimits.alert, c.configuredAlert, "Alert", { dashed: true, showLabel })}
          {options.showConfiguredLimits && limitLine(result.currentLimits.action, c.configuredAction, "Action", { dashed: true, showLabel })}
        </ComposedChart>
    </ChartFrame>
  );
}
