import {
  Box, Button, Grid, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography, useTheme
} from "@mui/material";
import {
  CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip as RechartsTooltip, XAxis, YAxis
} from "recharts";
import type { KpiEntry, KpiSeriesPoint } from "../types/kpiTypes";
import { entryView, formatExtras, formatKpiValue, scaleKpiValue } from "../utils/kpiFormat";
import { KpiChips } from "./KpiTile";

const secondary = { fontSize: 12, color: "text.secondary" } as const;

// Median and P90 per stage; the server sends them in stage order.
function StageBars({ points, unit }: { points: KpiSeriesPoint[]; unit: string }) {
  const theme = useTheme();
  const max = Math.max(...points.map((p) => p.secondary ?? p.value ?? 0), 0.0001);
  return (
    <Stack spacing={1.25}>
      {points.map((p) => (
        <Box key={p.label}>
          <Typography sx={{ fontSize: 12.5, fontWeight: 600 }}>{p.label}</Typography>
          <Box sx={{ position: "relative", height: 10, borderRadius: 1, bgcolor: "action.hover" }}>
            <Box sx={{ position: "absolute", inset: 0, width: `${((p.secondary ?? 0) / max) * 100}%`, borderRadius: 1, bgcolor: theme.custom.chartSequential[1] }} />
            <Box sx={{ position: "absolute", inset: 0, width: `${((p.value ?? 0) / max) * 100}%`, borderRadius: 1, bgcolor: theme.custom.chartSequential[3] }} />
          </Box>
          <Typography sx={secondary}>
            median {formatKpiValue(unit, p.value)} · P90 {formatKpiValue(unit, p.secondary)} · n = {p.n}
          </Typography>
        </Box>
      ))}
    </Stack>
  );
}

const BUCKETS = [
  { label: "within50", header: "Within 50% of target" },
  { label: "50to100", header: "50-100% of target" },
  { label: "over100", header: "Over target" }
];

// A missing bucket prints a dash: the page does not invent a zero count.
function AgingTable({ points }: { points: KpiSeriesPoint[] }) {
  const stages = Array.from(new Set(points.map((p) => p.group ?? "All")));
  const cell = (stage: string, bucket: string) => {
    const v = points.find((p) => (p.group ?? "All") === stage && p.label === bucket)?.value;
    return v == null ? "—" : Math.round(v);
  };
  return (
    <Table size="small">
      <TableHead>
        <TableRow>
          <TableCell>Stage</TableCell>
          {BUCKETS.map((b) => <TableCell key={b.label} align="right">{b.header}</TableCell>)}
        </TableRow>
      </TableHead>
      <TableBody>
        {stages.map((stage) => (
          <TableRow key={stage}>
            <TableCell>{stage}</TableCell>
            {BUCKETS.map((b) => <TableCell key={b.label} align="right">{cell(stage, b.label)}</TableCell>)}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

// By test code, never by person.
function ReturnsList({ points }: { points: KpiSeriesPoint[] }) {
  return (
    <Table size="small">
      <TableHead>
        <TableRow><TableCell>Test</TableCell><TableCell align="right">Returns</TableCell></TableRow>
      </TableHead>
      <TableBody>
        {points.map((p) => (
          <TableRow key={p.label}>
            <TableCell>{p.label}</TableCell>
            <TableCell align="right">{p.value == null ? "—" : Math.round(p.value)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

// The chart is decoration for the sighted; the same figures are one click away in the records drawer.
function TrendLine({ points, unit }: { points: KpiSeriesPoint[]; unit: string }) {
  const theme = useTheme();
  if (points.length < 2) return <Typography sx={secondary}>Not enough points for a trend yet.</Typography>;
  const data = points.map((p) => ({ label: p.label, value: p.value == null ? null : scaleKpiValue(unit, p.value) }));
  return (
    <Box sx={{ height: 150 }} aria-hidden>
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={data}>
          <CartesianGrid vertical={false} stroke={theme.palette.divider} />
          <XAxis dataKey="label" tick={{ fontSize: 12, fill: theme.palette.text.secondary }} />
          <YAxis width={40} tick={{ fontSize: 12, fill: theme.palette.text.secondary }} />
          <RechartsTooltip />
          <Line type="monotone" dataKey="value" stroke={theme.custom.status.info.text} connectNulls={false} />
        </LineChart>
      </ResponsiveContainer>
    </Box>
  );
}

function SeriesBlock({ entry }: { entry: KpiEntry }) {
  const points = entry.result?.series ?? [];
  if (points.length === 0) return null;
  switch (entry.key) {
    case "stage-tat": return <StageBars points={points} unit={entry.unit} />;
    case "backlog-aging": return <AgingTable points={points} />;
    case "returns-by-test": return <ReturnsList points={points} />;
    default: return <TrendLine points={points} unit={entry.unit} />;
  }
}

function Panel({ entry, onOpenRecords }: { entry: KpiEntry; onOpenRecords(key: string): void }) {
  const theme = useTheme();
  const view = entryView(entry);
  return (
    <Paper sx={{ p: 2, height: "100%" }}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
        <Typography component="h3" sx={{ fontSize: 14, fontWeight: 700 }}>{entry.name}</Typography>
        {view.kind !== "error" && (
          <Button size="small" onClick={() => onOpenRecords(entry.key)} aria-label={`Show records for ${entry.name}`}>
            Records
          </Button>
        )}
      </Stack>
      {view.kind === "error" && (
        <Typography role="alert" sx={{ fontSize: 13, color: theme.custom.status.detected.text }}>
          Could not be calculated: {view.message}
        </Typography>
      )}
      {view.kind === "noData" && (
        <>
          <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 1 }}>No data in this period</Typography>
          <KpiChips result={view.result} />
        </>
      )}
      {view.kind === "value" && (
        <Stack spacing={1}>
          {formatExtras(entry).map((x) => (
            <Typography key={x.label} sx={secondary}>{x.label} {x.text}</Typography>
          ))}
          {view.muted && <Typography sx={secondary}>too few records to judge</Typography>}
          {/* A count panel with no series (for example no returns in the period) must still say something. */}
          {view.result.series.length === 0 && view.text !== "" && (
            <>
              <Typography sx={{ fontSize: 18, fontWeight: 700 }}>{view.text}</Typography>
              {view.result.value === 0 && <Typography sx={secondary}>None in this period</Typography>}
            </>
          )}
          <SeriesBlock entry={entry} />
          <KpiChips result={view.result} />
        </Stack>
      )}
    </Paper>
  );
}

interface KpiSectionProps {
  title: string;
  entries: KpiEntry[];
  onOpenRecords(key: string): void;
}

export function KpiSection({ title, entries, onOpenRecords }: KpiSectionProps) {
  if (entries.length === 0) return null;
  return (
    <Box component="section" aria-label={title} sx={{ mb: 3 }}>
      <Typography component="h2" sx={{ fontSize: 16, fontWeight: 700, mb: 1.5 }}>{title}</Typography>
      <Grid container spacing={2}>
        {entries.map((e) => (
          <Grid key={e.key} size={{ xs: 12, md: 6 }}>
            <Panel entry={e} onOpenRecords={onOpenRecords} />
          </Grid>
        ))}
      </Grid>
    </Box>
  );
}
