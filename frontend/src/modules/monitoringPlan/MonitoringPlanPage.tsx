import { useCallback, useEffect, useRef, useState } from "react";
import {
  Alert,
  Box,
  Button,
  FormControlLabel,
  IconButton,
  LinearProgress,
  MenuItem,
  Stack,
  Switch,
  Tab,
  Tabs,
  TextField,
  Typography
} from "@mui/material";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import { PageHeader } from "../../components/PageHeader";
import { useHasPermission } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { AddEventDialog } from "./components/AddEventDialog";
import { CoverageTable } from "./components/CoverageTable";
import { DeviationsPanel } from "./components/DeviationsPanel";
import { DuePanel } from "./components/DuePanel";
import { EquipmentPlanPanel } from "./components/EquipmentPlanPanel";
import { EventDialog } from "./components/EventDialog";
import { ManualEntryDialog } from "./components/ManualEntryDialog";
import { MonthGrid } from "./components/MonthGrid";
import { OccurrenceDialog } from "./components/OccurrenceDialog";
import { PlanRevisionsPanel } from "./components/PlanRevisionsPanel";
import { SettingsPanel } from "./components/SettingsPanel";
import { SpacingTable } from "./components/SpacingTable";
import { YearGrid } from "./components/YearGrid";
import { EquipmentPlanService } from "./services/EquipmentPlanService";
import { MonitoringPlanService } from "./services/MonitoringPlanService";
import { MONTH_NAMES, isoDate } from "./statusDisplay";
import type {
  CalendarArea,
  CalendarEventResponse,
  CoverageReport,
  DueResult,
  EquipmentMonthSummary,
  EquipmentTaskItem,
  MonitoringCategory,
  OccurrenceSummary,
  SpacingReport,
  YearSummary
} from "./types";

// The annual monitoring plan as a calendar: the year shows twelve month
// tiles, a month shows its days. Includes equipment calibration, maintenance
// and ad-hoc calendar events. Everything shown comes from the backend.
export function MonitoringPlanPage() {
  const canManage = useHasPermission(PERMISSIONS.MONITORING_PLAN_MANAGE);
  const now = new Date();
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState<number | null>(null);
  const [category, setCategory] = useState<CalendarArea | "">("");

  const [summary, setSummary] = useState<YearSummary | null>(null);
  const [equipmentSummary, setEquipmentSummary] = useState<EquipmentMonthSummary[] | null>(null);
  const [days, setDays] = useState<OccurrenceSummary[]>([]);
  const [equipmentTasks, setEquipmentTasks] = useState<EquipmentTaskItem[]>([]);
  const [dueEquipmentTasks, setDueEquipmentTasks] = useState<EquipmentTaskItem[]>([]);
  const [calendarEvents, setCalendarEvents] = useState<CalendarEventResponse[]>([]);

  const [due, setDue] = useState<DueResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [openId, setOpenId] = useState<number | null>(null);
  const [manualOpen, setManualOpen] = useState(false);

  const [addEventOpen, setAddEventOpen] = useState(false);
  const [addEventDate, setAddEventDate] = useState<string | null>(null);
  const [addEventMonth, setAddEventMonth] = useState<string | null>(null);

  const [selectedEvent, setSelectedEvent] = useState<CalendarEventResponse | null>(null);
  const [eventDialogOpen, setEventDialogOpen] = useState(false);

  const [tab, setTab] = useState<"calendar" | "coverage" | "spacing" | "deviations" | "plan" | "settings">("calendar");
  const [coverage, setCoverage] = useState<CoverageReport | null>(null);
  const [spacing, setSpacing] = useState<SpacingReport | null>(null);
  const [flaggedOnly, setFlaggedOnly] = useState(true);
  const [loading, setLoading] = useState(false);
  const requestId = useRef(0);

  const monitoringCategory: MonitoringCategory | undefined =
    category === "Water" || category === "EnvironmentalMonitoring" || category === "AfterCleaning"
      ? category
      : undefined;

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
        const report = await MonitoringPlanService.getCoverage(year, monitoringCategory);
        if (latest()) setCoverage(report);
        return;
      }
      if (tab === "spacing") {
        const report = await MonitoringPlanService.getSpacing(year, monitoringCategory, flaggedOnly);
        if (latest()) setSpacing(report);
        return;
      }

      // Calendar tab loading
      const yearStart = isoDate(year, 0, 1);
      const yearEnd = isoDate(year, 11, 31);
      const isEquipmentOnly = category === "Calibration" || category === "PreventiveMaintenance";
      const isEventsOnly = category === "Events";
      const isMonitoringOnly = Boolean(monitoringCategory);

      // 1. Monitoring occurrences and year summary
      let monYear: YearSummary | null = null;
      let monDue: DueResult | null = null;
      let monDays: OccurrenceSummary[] = [];

      // 2. Equipment summaries, tasks and events
      let eqYear: EquipmentMonthSummary[] | null = null;
      let eqMonthTasks: EquipmentTaskItem[] = [];
      let eqDueTasks: EquipmentTaskItem[] = [];
      let evList: CalendarEventResponse[] = [];

      const promises: Promise<unknown>[] = [];

      if (!isEquipmentOnly && !isEventsOnly) {
        promises.push(
          MonitoringPlanService.getYear(year, monitoringCategory).then((res) => { monYear = res; }),
          MonitoringPlanService.getDue(monitoringCategory).then((res) => { monDue = res; })
        );
        if (month !== null) {
          const last = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
          promises.push(
            MonitoringPlanService.getCalendar(isoDate(year, month, 1), isoDate(year, month, last), monitoringCategory).then((res) => { monDays = res; })
          );
        }
      }

      if (!isMonitoringOnly) {
        promises.push(
          EquipmentPlanService.getYear(year).then((res) => { eqYear = res; }),
          EquipmentPlanService.getCalendar(yearStart, yearEnd).then((res) => {
            if (category === "Calibration") {
              eqDueTasks = res.tasks.filter((t) => t.type === "Calibration");
            } else if (category === "PreventiveMaintenance") {
              eqDueTasks = res.tasks.filter((t) => t.type === "PreventiveMaintenance");
            } else if (category === "Events") {
              eqDueTasks = [];
            } else {
              eqDueTasks = res.tasks;
            }
          })
        );

        if (month !== null) {
          const last = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
          const from = isoDate(year, month, 1);
          const to = isoDate(year, month, last);

          if (category === "Events") {
            promises.push(
              EquipmentPlanService.getEvents(from, to).then((res) => {
                evList = res.filter((e) => e.status === "Active");
              })
            );
          } else {
            promises.push(
              EquipmentPlanService.getCalendar(from, to).then((res) => {
                if (category === "Calibration") {
                  eqMonthTasks = res.tasks.filter((t) => t.type === "Calibration");
                  evList = [];
                } else if (category === "PreventiveMaintenance") {
                  eqMonthTasks = res.tasks.filter((t) => t.type === "PreventiveMaintenance");
                  evList = [];
                } else {
                  eqMonthTasks = res.tasks;
                  evList = res.events;
                }
              })
            );
          }
        }
      }

      await Promise.all(promises);

      if (!latest()) return;

      setSummary(monYear);
      setDue(monDue);
      setDays(monDays);
      setEquipmentSummary(eqYear);
      setEquipmentTasks(eqMonthTasks);
      setDueEquipmentTasks(eqDueTasks);
      setCalendarEvents(evList);
    } catch (e: any) {
      if (!latest()) return;
      setError(e?.response?.data?.message ?? "Could not load the monitoring plan.");
      if (tab === "coverage") setCoverage(null);
      else if (tab === "spacing") setSpacing(null);
      else {
        setSummary(null);
        setDue(null);
        setDays([]);
        setEquipmentSummary(null);
        setEquipmentTasks([]);
        setDueEquipmentTasks([]);
        setCalendarEvents([]);
      }
    } finally {
      if (latest()) setLoading(false);
    }
  }, [year, month, category, tab, flaggedOnly, monitoringCategory]);

  useEffect(() => {
    void load();
  }, [load]);

  const today = due?.today ?? isoDate(now.getFullYear(), now.getMonth(), now.getDate());

  const step = (delta: number) => {
    if (tab !== "calendar" || month === null) {
      setYear(year + delta);
      return;
    }
    const next = month + delta;
    if (next < 0) {
      setYear(year - 1);
      setMonth(11);
    } else if (next > 11) {
      setYear(year + 1);
      setMonth(0);
    } else {
      setMonth(next);
    }
  };

  const goToday = () => {
    setYear(now.getFullYear());
    if (tab === "calendar" && month !== null) setMonth(now.getMonth());
  };

  const showsData = tab === "calendar" || tab === "coverage" || tab === "spacing";

  return (
    <Box>
      <PageHeader
        title="Monitoring Plan"
        subtitle="Water, environmental monitoring and after-cleaning occurrences, plus equipment calibration, maintenance and calendar events."
      >
        {canManage && (
          <Button variant="outlined" onClick={() => setManualOpen(true)}>
            Add After Cleaning
          </Button>
        )}
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

      {tab === "calendar" && (
        <DuePanel
          due={due ?? { today, warningDayOfMonth: 20, showWarning: false, items: [] }}
          equipmentTasks={dueEquipmentTasks}
          onPick={(o) => setOpenId(o.id)}
        />
      )}

      {tab !== "deviations" && (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 2, flexWrap: "wrap" }}>
          <IconButton onClick={() => step(-1)} aria-label="Previous">
            <ChevronLeftIcon />
          </IconButton>
          <Typography sx={{ fontWeight: 600, minWidth: 160, textAlign: "center" }}>
            {tab !== "calendar" || month === null ? year : `${MONTH_NAMES[month]} ${year}`}
          </Typography>
          <IconButton onClick={() => step(1)} aria-label="Next">
            <ChevronRightIcon />
          </IconButton>
          {showsData && <Button variant="outlined" onClick={goToday}>Today</Button>}
          {tab === "calendar" && month !== null && (
            <Button onClick={() => setMonth(null)}>Back to year</Button>
          )}
          {tab === "spacing" && (
            <FormControlLabel
              control={<Switch checked={flaggedOnly} onChange={(e) => setFlaggedOnly(e.target.checked)} />}
              label="Only flagged gaps"
            />
          )}
          {showsData && (
            <TextField
              select
              size="small"
              label="Area"
              value={category}
              sx={{ minWidth: 220, ml: "auto" }}
              onChange={(e) => setCategory(e.target.value as CalendarArea | "")}
            >
              <MenuItem value="">All areas</MenuItem>
              <MenuItem value="Water">Water</MenuItem>
              <MenuItem value="EnvironmentalMonitoring">Environmental Monitoring</MenuItem>
              <MenuItem value="AfterCleaning">After Cleaning</MenuItem>
              <MenuItem value="Calibration">Calibration</MenuItem>
              <MenuItem value="PreventiveMaintenance">Preventive maintenance</MenuItem>
              <MenuItem value="Events">Events</MenuItem>
            </TextField>
          )}
        </Box>
      )}

      {loading && showsData && <LinearProgress aria-label="Loading" sx={{ mb: 1 }} />}

      <Box
        aria-busy={loading && showsData}
        sx={{ opacity: loading && showsData ? 0.55 : 1, transition: "opacity 120ms" }}
      >
        {tab === "deviations" ? (
          <DeviationsPanel onPick={setOpenId} />
        ) : tab === "plan" ? (
          <Stack spacing={4}>
            <PlanRevisionsPanel year={year} onApproved={() => void load()} />
            <EquipmentPlanPanel year={year} onApproved={() => void load()} />
          </Stack>
        ) : tab === "settings" ? (
          <SettingsPanel year={year} />
        ) : tab === "coverage" ? (
          <CoverageTable report={coverage} />
        ) : tab === "spacing" ? (
          <SpacingTable report={spacing} />
        ) : month === null ? (
          <YearGrid
            summary={summary}
            equipmentSummary={equipmentSummary}
            year={year}
            today={today}
            onPick={setMonth}
            onAddMonthEvent={(m) => {
              setAddEventMonth(m);
              setAddEventDate(null);
              setAddEventOpen(true);
            }}
          />
        ) : (
          <MonthGrid
            year={year}
            month={month}
            occurrences={days}
            tasks={equipmentTasks}
            events={calendarEvents}
            today={today}
            onPick={(o) => setOpenId(o.id)}
            onPickEvent={(ev) => {
              setSelectedEvent(ev);
              setEventDialogOpen(true);
            }}
            onAddEvent={(d) => {
              setAddEventDate(d);
              setAddEventMonth(null);
              setAddEventOpen(true);
            }}
          />
        )}
      </Box>

      <OccurrenceDialog occurrenceId={openId} onClose={() => setOpenId(null)} onChanged={() => void load()} />
      <ManualEntryDialog open={manualOpen} onClose={() => setManualOpen(false)} onCreated={() => void load()} />

      <AddEventDialog
        open={addEventOpen}
        onClose={() => setAddEventOpen(false)}
        onCreated={() => void load()}
        initialDate={addEventDate}
        initialMonth={addEventMonth}
      />

      <EventDialog
        event={selectedEvent}
        open={eventDialogOpen}
        onClose={() => {
          setEventDialogOpen(false);
          setSelectedEvent(null);
        }}
        onChanged={() => void load()}
      />
    </Box>
  );
}
