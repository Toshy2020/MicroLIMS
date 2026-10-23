import { useCallback, useEffect, useState } from "react";
import {
  Alert, Box, Button, Checkbox, FormControlLabel, IconButton, Paper, Stack, TextField, Typography
} from "@mui/material";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import { useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { MonitoringSettingsApi } from "../services/MonitoringPlanService";
import type { LabCalendarDate, MonitoringSettings } from "../types";

// bit0 = Monday ... bit6 = Sunday, shown in the lab's Sunday-first order.
const WEEKDAYS: { label: string; bit: number }[] = [
  { label: "Sun", bit: 6 }, { label: "Mon", bit: 0 }, { label: "Tue", bit: 1 }, { label: "Wed", bit: 2 },
  { label: "Thu", bit: 3 }, { label: "Fri", bit: 4 }, { label: "Sat", bit: 5 }
];

const NUMBER_FIELDS: { key: keyof MonitoringSettings; label: string }[] = [
  { key: "waterEarlyToleranceDays", label: "Water: days early allowed" },
  { key: "waterLateToleranceDays", label: "Water: days late allowed" },
  { key: "emEarlyToleranceDays", label: "EM: days early allowed" },
  { key: "emLateToleranceDays", label: "EM: days late allowed" },
  { key: "acEarlyToleranceDays", label: "After Cleaning: days early allowed" },
  { key: "acLateToleranceDays", label: "After Cleaning: days late allowed" },
  { key: "warningDayOfMonth", label: "Warn from day of month" },
  { key: "graceDays", label: "Grace days before Missed" },
  { key: "deviationDueDays", label: "Days allowed to close a deviation" },
  { key: "followUpWindowDays", label: "Working days for follow-up sampling" },
  { key: "monthlyMinGapDays", label: "Monthly: shortest gap (days)" },
  { key: "monthlyMaxGapDays", label: "Monthly: longest gap (days)" },
  { key: "weeklyMinGapDays", label: "Weekly: shortest gap (days)" },
  { key: "weeklyMaxGapDays", label: "Weekly: longest gap (days)" }
];

// Plan settings and the lab's non-working dates (public holidays and shutdowns).
export function SettingsPanel({ year }: { year: number }) {
  const canManage = useHasPermission(PERMISSIONS.MONITORING_PLAN_MANAGE);
  const [settings, setSettings] = useState<MonitoringSettings | null>(null);
  const [dates, setDates] = useState<LabCalendarDate[]>([]);
  const [newDate, setNewDate] = useState("");
  const [newDescription, setNewDescription] = useState("");
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = useCallback(async () => {
    try {
      const [s, d] = await Promise.all([MonitoringSettingsApi.get(), MonitoringSettingsApi.getDates(year)]);
      setSettings(s);
      setDates(d);
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not load the settings.", ok: false });
    }
  }, [year]);

  useEffect(() => { void load(); }, [load]);

  const run = async (action: () => Promise<unknown>, done: string) => {
    setMessage(null);
    try {
      await action();
      setMessage({ text: done, ok: true });
      await load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "The change was not saved.", ok: false });
    }
  };

  if (!settings) return message ? <Alert severity="error">{message.text}</Alert> : null;
  const set = (patch: Partial<MonitoringSettings>) => setSettings({ ...settings, ...patch });

  return (
    <Stack spacing={2}>
      {message && <Alert severity={message.ok ? "success" : "error"}>{message.text}</Alert>}
      <Paper variant="outlined" sx={{ p: 2 }}>
        <Typography sx={{ fontWeight: 600, mb: 1 }}>Working days</Typography>
        <Box>
          {WEEKDAYS.map((d) => (
            <FormControlLabel
              key={d.label}
              disabled={!canManage}
              control={
                <Checkbox
                  checked={(settings.workingWeekdays & (1 << d.bit)) !== 0}
                  onChange={(e) => set({ workingWeekdays: e.target.checked ? settings.workingWeekdays | (1 << d.bit) : settings.workingWeekdays & ~(1 << d.bit) })}
                />
              }
              label={d.label}
            />
          ))}
        </Box>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(3, 1fr)" }, gap: 2, mt: 1 }}>
          {NUMBER_FIELDS.map((f) => (
            <TextField
              key={f.key} size="small" type="number" label={f.label} disabled={!canManage}
              value={settings[f.key] as number}
              onChange={(e) => set({ [f.key]: Number(e.target.value) } as Partial<MonitoringSettings>)}
            />
          ))}
        </Box>
        {canManage && (
          <Button sx={{ mt: 2 }} variant="contained" onClick={() => run(() => MonitoringSettingsApi.save(settings), "Settings saved.")}>
            Save settings
          </Button>
        )}
      </Paper>

      <Paper variant="outlined" sx={{ p: 2 }}>
        <Typography sx={{ fontWeight: 600, mb: 1 }}>Non-working dates in {year}</Typography>
        {dates.length === 0 && <Typography sx={{ fontSize: 13, color: "text.secondary" }}>None entered.</Typography>}
        {dates.map((d) => (
          <Box key={d.id} sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <Typography sx={{ fontSize: 14, flex: 1 }}>{d.date} · {d.description}</Typography>
            {canManage && (
              <IconButton size="small" aria-label={`Remove ${d.date}`} onClick={() => run(() => MonitoringSettingsApi.removeDate(d.id), "Date removed.")}>
                <DeleteOutlineIcon fontSize="small" />
              </IconButton>
            )}
          </Box>
        ))}
        {canManage && (
          <Box sx={{ display: "flex", gap: 1, mt: 1.5, flexWrap: "wrap" }}>
            <TextField size="small" type="date" label="Date" slotProps={{ inputLabel: { shrink: true } }} value={newDate} onChange={(e) => setNewDate(e.target.value)} />
            <TextField size="small" label="Description" value={newDescription} onChange={(e) => setNewDescription(e.target.value)} />
            <Button
              variant="outlined" disabled={!newDate || !newDescription.trim()}
              onClick={() => run(async () => { await MonitoringSettingsApi.addDate(newDate, newDescription.trim()); setNewDate(""); setNewDescription(""); }, "Date added.")}
            >
              Add date
            </Button>
          </Box>
        )}
      </Paper>
    </Stack>
  );
}
