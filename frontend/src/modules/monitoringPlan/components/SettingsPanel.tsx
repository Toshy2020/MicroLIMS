import { useCallback, useEffect, useState } from "react";
import {
  Alert, Box, Button, Checkbox, FormControlLabel, IconButton, Paper, Stack, TextField, Typography
} from "@mui/material";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import { Trans } from "react-i18next";
import { DateField } from "../../../components/DateField";
import { Ltr } from "../../../components/Ltr";
import { useHasPermission } from "../../../contexts/AuthContext";
import { useTranslation } from "../../../i18n";
import { PERMISSIONS } from "../../../routes/routes";
import { serverMessage } from "../../../utils/errorMessage";
import { MonitoringSettingsApi } from "../services/MonitoringPlanService";
import type { LabCalendarDate, MonitoringSettings } from "../types";

// bit0 = Monday ... bit6 = Sunday, shown in the lab's Sunday-first order.
const WEEKDAYS: { weekdayIndex: number; bit: number }[] = [
  { weekdayIndex: 0, bit: 6 },
  { weekdayIndex: 1, bit: 0 },
  { weekdayIndex: 2, bit: 1 },
  { weekdayIndex: 3, bit: 2 },
  { weekdayIndex: 4, bit: 3 },
  { weekdayIndex: 5, bit: 4 },
  { weekdayIndex: 6, bit: 5 }
];

const NUMBER_FIELDS: { key: keyof MonitoringSettings; labelKey: string }[] = [
  { key: "waterEarlyToleranceDays", labelKey: "settings.fields.waterEarlyToleranceDays" },
  { key: "waterLateToleranceDays", labelKey: "settings.fields.waterLateToleranceDays" },
  { key: "emEarlyToleranceDays", labelKey: "settings.fields.emEarlyToleranceDays" },
  { key: "emLateToleranceDays", labelKey: "settings.fields.emLateToleranceDays" },
  { key: "acEarlyToleranceDays", labelKey: "settings.fields.acEarlyToleranceDays" },
  { key: "acLateToleranceDays", labelKey: "settings.fields.acLateToleranceDays" },
  { key: "warningDayOfMonth", labelKey: "settings.fields.warningDayOfMonth" },
  { key: "graceDays", labelKey: "settings.fields.graceDays" },
  { key: "deviationDueDays", labelKey: "settings.fields.deviationDueDays" },
  { key: "followUpWindowDays", labelKey: "settings.fields.followUpWindowDays" },
  { key: "monthlyMinGapDays", labelKey: "settings.fields.monthlyMinGapDays" },
  { key: "monthlyMaxGapDays", labelKey: "settings.fields.monthlyMaxGapDays" },
  { key: "weeklyMinGapDays", labelKey: "settings.fields.weeklyMinGapDays" },
  { key: "weeklyMaxGapDays", labelKey: "settings.fields.weeklyMaxGapDays" }
];

// Plan settings and the lab's non-working dates (public holidays and shutdowns).
export function SettingsPanel({ year }: { year: number }) {
  const { t } = useTranslation("monitoring");
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
    } catch (e: unknown) {
      setMessage({ text: serverMessage(e) ?? t("errors.loadSettings"), ok: false });
    }
  }, [year, t]);

  useEffect(() => { void load(); }, [load]);

  const run = async (action: () => Promise<unknown>, done: string) => {
    setMessage(null);
    try {
      await action();
      setMessage({ text: done, ok: true });
      await load();
    } catch (e: unknown) {
      setMessage({ text: serverMessage(e) ?? t("errors.saveSettings"), ok: false });
    }
  };

  if (!settings) return message ? <Alert severity="error">{message.text}</Alert> : null;
  const set = (patch: Partial<MonitoringSettings>) => setSettings({ ...settings, ...patch });

  return (
    <Stack spacing={2}>
      {message && <Alert severity={message.ok ? "success" : "error"}>{message.text}</Alert>}
      <Paper variant="outlined" sx={{ p: 2 }}>
        <Typography sx={{ fontWeight: 600, mb: 1 }}>{t("settings.workingDays")}</Typography>
        <Box>
          {WEEKDAYS.map((d) => (
            <FormControlLabel
              key={d.weekdayIndex}
              disabled={!canManage}
              control={
                <Checkbox
                  checked={(settings.workingWeekdays & (1 << d.bit)) !== 0}
                  onChange={(e) => set({ workingWeekdays: e.target.checked ? settings.workingWeekdays | (1 << d.bit) : settings.workingWeekdays & ~(1 << d.bit) })}
                />
              }
              label={t(`weekdays.${d.weekdayIndex}`)}
            />
          ))}
        </Box>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(3, 1fr)" }, gap: 2, mt: 1 }}>
          {NUMBER_FIELDS.map((f) => (
            <TextField
              key={f.key} size="small" type="number" label={t(f.labelKey)} disabled={!canManage}
              value={settings[f.key] as number}
              onChange={(e) => set({ [f.key]: Number(e.target.value) } as Partial<MonitoringSettings>)}
            />
          ))}
        </Box>
        {canManage && (
          <Button sx={{ mt: 2 }} variant="contained" onClick={() => run(() => MonitoringSettingsApi.save(settings), t("settings.saved"))}>
            {t("settings.save")}
          </Button>
        )}
      </Paper>

      <Paper variant="outlined" sx={{ p: 2 }}>
        <Typography sx={{ fontWeight: 600, mb: 1 }}>
          <Trans i18nKey="settings.nonWorkingDates" ns="monitoring" values={{ year }} components={{ ltr: <Ltr>{null}</Ltr> }} />
        </Typography>
        {dates.length === 0 && <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{t("settings.noneEntered")}</Typography>}
        {dates.map((d) => (
          <Box key={d.id} sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <Typography sx={{ fontSize: 14, flex: 1 }}><Ltr>{d.date}</Ltr> · {d.description}</Typography>
            {canManage && (
              <IconButton size="small" aria-label={t("settings.removeDateAria", { date: d.date })} onClick={() => run(() => MonitoringSettingsApi.removeDate(d.id), t("settings.dateRemoved"))}>
                <DeleteOutlineIcon fontSize="small" />
              </IconButton>
            )}
          </Box>
        ))}
        {canManage && (
          <Box sx={{ display: "flex", gap: 1, mt: 1.5, flexWrap: "wrap" }}>
            <DateField label={t("settings.date")} value={newDate} onChange={(v) => setNewDate(v)} />
            <TextField size="small" label={t("settings.description")} value={newDescription} onChange={(e) => setNewDescription(e.target.value)} />
            <Button
              variant="outlined" disabled={!newDate || !newDescription.trim()}
              onClick={() => run(async () => { await MonitoringSettingsApi.addDate(newDate, newDescription.trim()); setNewDate(""); setNewDescription(""); }, t("settings.dateAdded"))}
            >
              {t("settings.addDate")}
            </Button>
          </Box>
        )}
      </Paper>
    </Stack>
  );
}
