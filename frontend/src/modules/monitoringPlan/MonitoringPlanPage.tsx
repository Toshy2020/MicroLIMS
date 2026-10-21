import { useCallback, useEffect, useMemo, useState } from "react";
import { Alert, Box, Button, IconButton, MenuItem, TextField, Typography } from "@mui/material";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import { PageHeader } from "../../components/PageHeader";
import { useHasPermission } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { DuePanel } from "./components/DuePanel";
import { ManualEntryDialog } from "./components/ManualEntryDialog";
import { MonthGrid } from "./components/MonthGrid";
import { OccurrenceDialog } from "./components/OccurrenceDialog";
import { YearGrid } from "./components/YearGrid";
import { MonitoringPlanService } from "./services/MonitoringPlanService";
import { CATEGORY_LABEL, MONTH_NAMES, isoDate } from "./statusDisplay";
import type { DueResult, MonitoringCategory, OccurrenceSummary, YearSummary } from "./types";

// The annual monitoring plan as a calendar: the year shows twelve month
// tiles, a month shows its days. Everything shown comes from the backend.
export function MonitoringPlanPage() {
  const canManage = useHasPermission(PERMISSIONS.MONITORING_PLAN_MANAGE);
  const now = new Date();
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState<number | null>(null);
  const [category, setCategory] = useState<MonitoringCategory | "">("");
  const [summary, setSummary] = useState<YearSummary | null>(null);
  const [days, setDays] = useState<OccurrenceSummary[]>([]);
  const [due, setDue] = useState<DueResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [openId, setOpenId] = useState<number | null>(null);
  const [manualOpen, setManualOpen] = useState(false);

  const cat = category || undefined;

  const load = useCallback(async () => {
    setError(null);
    try {
      const [y, d] = await Promise.all([MonitoringPlanService.getYear(year, cat), MonitoringPlanService.getDue(cat)]);
      setSummary(y);
      setDue(d);
      if (month !== null) {
        const last = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
        setDays(await MonitoringPlanService.getCalendar(isoDate(year, month, 1), isoDate(year, month, last), cat));
      }
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not load the monitoring plan.");
    }
  }, [year, month, cat]);

  useEffect(() => { void load(); }, [load]);

  const today = due?.today ?? isoDate(now.getFullYear(), now.getMonth(), now.getDate());
  const monthDays = useMemo(() => days, [days]);

  const step = (delta: number) => {
    if (month === null) { setYear(year + delta); return; }
    const next = month + delta;
    if (next < 0) { setYear(year - 1); setMonth(11); }
    else if (next > 11) { setYear(year + 1); setMonth(0); }
    else setMonth(next);
  };

  return (
    <Box>
      <PageHeader title="Monitoring Plan" subtitle="Water, environmental monitoring and after-cleaning occurrences for the year.">
        {canManage && <Button variant="outlined" onClick={() => setManualOpen(true)}>Add After Cleaning</Button>}
      </PageHeader>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <DuePanel due={due} onPick={(o) => setOpenId(o.id)} />

      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 2, flexWrap: "wrap" }}>
        <IconButton onClick={() => step(-1)} aria-label="Previous"><ChevronLeftIcon /></IconButton>
        <Typography sx={{ fontWeight: 600, minWidth: 160, textAlign: "center" }}>
          {month === null ? year : `${MONTH_NAMES[month]} ${year}`}
        </Typography>
        <IconButton onClick={() => step(1)} aria-label="Next"><ChevronRightIcon /></IconButton>
        {month !== null && <Button onClick={() => setMonth(null)}>Back to year</Button>}
        <TextField
          select size="small" label="Area" value={category} sx={{ minWidth: 220, ml: "auto" }}
          onChange={(e) => setCategory(e.target.value as MonitoringCategory | "")}
        >
          <MenuItem value="">All areas</MenuItem>
          {(Object.keys(CATEGORY_LABEL) as MonitoringCategory[]).map((c) => (
            <MenuItem key={c} value={c}>{CATEGORY_LABEL[c]}</MenuItem>
          ))}
        </TextField>
      </Box>

      {month === null ? (
        <YearGrid summary={summary} onPick={setMonth} />
      ) : (
        <MonthGrid year={year} month={month} occurrences={monthDays} today={today} onPick={(o) => setOpenId(o.id)} />
      )}

      <OccurrenceDialog occurrenceId={openId} onClose={() => setOpenId(null)} onChanged={() => void load()} />
      <ManualEntryDialog open={manualOpen} onClose={() => setManualOpen(false)} onCreated={() => void load()} />
    </Box>
  );
}
