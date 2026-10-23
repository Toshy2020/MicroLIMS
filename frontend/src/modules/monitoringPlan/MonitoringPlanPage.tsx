import { useCallback, useEffect, useRef, useState } from "react";
import { Alert, Box, Button, FormControlLabel, IconButton, LinearProgress, MenuItem, Switch, Tab, Tabs, TextField, Typography } from "@mui/material";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import { PageHeader } from "../../components/PageHeader";
import { useHasPermission } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { CoverageTable } from "./components/CoverageTable";
import { DeviationsPanel } from "./components/DeviationsPanel";
import { DuePanel } from "./components/DuePanel";
import { ManualEntryDialog } from "./components/ManualEntryDialog";
import { MonthGrid } from "./components/MonthGrid";
import { PlanRevisionsPanel } from "./components/PlanRevisionsPanel";
import { OccurrenceDialog } from "./components/OccurrenceDialog";
import { SettingsPanel } from "./components/SettingsPanel";
import { SpacingTable } from "./components/SpacingTable";
import { YearGrid } from "./components/YearGrid";
import { MonitoringPlanService } from "./services/MonitoringPlanService";
import { CATEGORY_LABEL, MONTH_NAMES, isoDate } from "./statusDisplay";
import type { CoverageReport, DueResult, MonitoringCategory, OccurrenceSummary, SpacingReport, YearSummary } from "./types";

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
  const [tab, setTab] = useState<"calendar" | "coverage" | "spacing" | "deviations" | "plan" | "settings">("calendar");
  const [coverage, setCoverage] = useState<CoverageReport | null>(null);
  const [spacing, setSpacing] = useState<SpacingReport | null>(null);
  const [flaggedOnly, setFlaggedOnly] = useState(true);
  const [loading, setLoading] = useState(false);
  const requestId = useRef(0);

  const cat = category || undefined;

  // Only the latest request may touch state, so a slow answer for a month or
  // tab already left cannot overwrite the one on screen.
  const load = useCallback(async () => {
    if (tab === "settings" || tab === "plan" || tab === "deviations") return;
    const mine = ++requestId.current;
    const latest = () => mine === requestId.current;
    setError(null);
    setLoading(true);
    try {
      if (tab === "coverage") {
        const report = await MonitoringPlanService.getCoverage(year, cat);
        if (latest()) setCoverage(report);
        return;
      }
      if (tab === "spacing") {
        const report = await MonitoringPlanService.getSpacing(year, cat, flaggedOnly);
        if (latest()) setSpacing(report);
        return;
      }
      const [y, d] = await Promise.all([MonitoringPlanService.getYear(year, cat), MonitoringPlanService.getDue(cat)]);
      let calendarDays: OccurrenceSummary[] | null = null;
      if (month !== null) {
        const last = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
        calendarDays = await MonitoringPlanService.getCalendar(isoDate(year, month, 1), isoDate(year, month, last), cat);
      }
      if (!latest()) return;
      setSummary(y);
      setDue(d);
      if (calendarDays) setDays(calendarDays);
    } catch (e: any) {
      if (!latest()) return;
      setError(e?.response?.data?.message ?? "Could not load the monitoring plan.");
      if (tab === "coverage") setCoverage(null);
      else if (tab === "spacing") setSpacing(null);
      else { setSummary(null); setDue(null); setDays([]); }
    } finally {
      if (latest()) setLoading(false);
    }
  }, [year, month, cat, tab, flaggedOnly]);

  useEffect(() => { void load(); }, [load]);

  const today = due?.today ?? isoDate(now.getFullYear(), now.getMonth(), now.getDate());

  const step = (delta: number) => {
    if (tab !== "calendar" || month === null) { setYear(year + delta); return; }
    const next = month + delta;
    if (next < 0) { setYear(year - 1); setMonth(11); }
    else if (next > 11) { setYear(year + 1); setMonth(0); }
    else setMonth(next);
  };

  const goToday = () => {
    setYear(now.getFullYear());
    if (tab === "calendar" && month !== null) setMonth(now.getMonth());
  };
  const showsData = tab === "calendar" || tab === "coverage" || tab === "spacing";

  return (
    <Box>
      <PageHeader title="Monitoring Plan" subtitle="Water, environmental monitoring and after-cleaning occurrences for the year.">
        {canManage && <Button variant="outlined" onClick={() => setManualOpen(true)}>Add After Cleaning</Button>}
      </PageHeader>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}>
        <Tab value="calendar" label="Calendar" />
        <Tab value="coverage" label="Coverage" />
        <Tab value="spacing" label="Spacing" />
        <Tab value="deviations" label="Deviations" />
        <Tab value="plan" label="Plan" />
        <Tab value="settings" label="Settings" />
      </Tabs>
      {tab === "calendar" && <DuePanel due={due} onPick={(o) => setOpenId(o.id)} />}

      {tab !== "deviations" && <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 2, flexWrap: "wrap" }}>
        <IconButton onClick={() => step(-1)} aria-label="Previous"><ChevronLeftIcon /></IconButton>
        <Typography sx={{ fontWeight: 600, minWidth: 160, textAlign: "center" }}>
          {tab !== "calendar" || month === null ? year : `${MONTH_NAMES[month]} ${year}`}
        </Typography>
        <IconButton onClick={() => step(1)} aria-label="Next"><ChevronRightIcon /></IconButton>
        {showsData && <Button variant="outlined" onClick={goToday}>Today</Button>}
        {tab === "calendar" && month !== null && <Button onClick={() => setMonth(null)}>Back to year</Button>}
        {tab === "spacing" && (
          <FormControlLabel control={<Switch checked={flaggedOnly} onChange={(e) => setFlaggedOnly(e.target.checked)} />} label="Only flagged gaps" />
        )}
        {showsData && <TextField
          select size="small" label="Area" value={category} sx={{ minWidth: 220, ml: "auto" }}
          onChange={(e) => setCategory(e.target.value as MonitoringCategory | "")}
        >
          <MenuItem value="">All areas</MenuItem>
          {(Object.keys(CATEGORY_LABEL) as MonitoringCategory[]).map((c) => (
            <MenuItem key={c} value={c}>{CATEGORY_LABEL[c]}</MenuItem>
          ))}
        </TextField>}
      </Box>}
      {loading && showsData && <LinearProgress aria-label="Loading" sx={{ mb: 1 }} />}

      <Box aria-busy={loading && showsData} sx={{ opacity: loading && showsData ? 0.55 : 1, transition: "opacity 120ms" }}>
      {tab === "deviations" ? (
        <DeviationsPanel onPick={setOpenId} />
      ) : tab === "plan" ? (
        <PlanRevisionsPanel year={year} onApproved={() => void load()} />
      ) : tab === "settings" ? (
        <SettingsPanel year={year} />
      ) : tab === "coverage" ? (
        <CoverageTable report={coverage} />
      ) : tab === "spacing" ? (
        <SpacingTable report={spacing} />
      ) : month === null ? (
        <YearGrid summary={summary} today={today} onPick={setMonth} />
      ) : (
        <MonthGrid year={year} month={month} occurrences={days} today={today} onPick={(o) => setOpenId(o.id)} />
      )}
      </Box>

      <OccurrenceDialog occurrenceId={openId} onClose={() => setOpenId(null)} onChanged={() => void load()} />
      <ManualEntryDialog open={manualOpen} onClose={() => setManualOpen(false)} onCreated={() => void load()} />
    </Box>
  );
}
