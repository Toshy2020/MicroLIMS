import { type FormEvent, useCallback, useEffect, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import axios from "axios";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormHelperText,
  Link as MuiLink,
  Paper,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography
} from "@mui/material";
import { toast } from "sonner";
import { LabPage } from "../../../components/lab";
import { LoadErrorAlert } from "../../../components/LoadErrorAlert";
import { SectionTitle } from "../../../components/SectionTitle";
import { APP_ROUTES } from "../../../routes/routes";
import { getErrorMessage } from "../../../utils/errorMessage";
import { errorsByField, errorsByRow } from "../../../utils/fieldErrors";
import {
  type DayOfWeekName,
  type UpdateWorkingHoursDayRequest,
  type WorkingHoursDayDto,
  type WorkingHoursDto,
  workingHoursService
} from "./services/workingHoursService";

interface DraftDayRow {
  dayOfWeek: DayOfWeekName;
  isWorkingDay: boolean;
  startTime: string;
  endTime: string;
}

interface RowFieldErrors {
  dayOfWeek?: string;
  startMinute?: string;
  endMinute?: string;
}

function minutesToTime(minutes: number): string {
  const clamped = Math.max(0, Math.min(1439, Math.trunc(minutes)));
  const hh = String(Math.floor(clamped / 60)).padStart(2, "0");
  const mm = String(clamped % 60).padStart(2, "0");
  return `${hh}:${mm}`;
}

function timeToMinutes(value: string): number | null {
  const match = /^(\d{2}):(\d{2})$/.exec(value.trim());
  if (!match) return null;
  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59) return null;
  return hours * 60 + minutes;
}

function toDraftRows(days: WorkingHoursDayDto[]): DraftDayRow[] {
  return days.map((day) => ({
    dayOfWeek: day.dayOfWeek,
    isWorkingDay: day.isWorkingDay,
    startTime: minutesToTime(day.startMinute),
    endTime: minutesToTime(day.endMinute)
  }));
}

export function WorkingHoursPage() {
  const [data, setData] = useState<WorkingHoursDto | null>(null);
  const [rows, setRows] = useState<DraftDayRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [rowErrors, setRowErrors] = useState<Record<number, RowFieldErrors>>({});
  const [reloadToken, setReloadToken] = useState(0);

  const fetchSchedule = useCallback((signal: AbortSignal) => {
    setLoading(true);
    setLoadError(null);

    workingHoursService
      .get(signal)
      .then((res) => {
        if (signal.aborted) return;
        setData(res);
        setRows(toDraftRows(res.days));
        setRowErrors({});
        setSaveError(null);
        setLoading(false);
      })
      .catch((err: unknown) => {
        if (signal.aborted || axios.isCancel(err)) return;
        const message = getErrorMessage(err, "Could not load working hours.");
        setLoadError(message);
        setLoading(false);
        toast.error(message);
      });
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    fetchSchedule(controller.signal);
    return () => controller.abort();
  }, [fetchSchedule, reloadToken]);

  const clearRowError = (index: number) => {
    setRowErrors((prev) => {
      if (!(index in prev)) return prev;
      const next = { ...prev };
      delete next[index];
      return next;
    });
    if (saveError) setSaveError(null);
  };

  const updateRow = (index: number, patch: Partial<DraftDayRow>) => {
    setRows((prev) => prev.map((row, idx) => (idx === index ? { ...row, ...patch } : row)));
    clearRowError(index);
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (saving || rows.length === 0) return;

    const localErrors: Record<number, RowFieldErrors> = {};
    const payload: UpdateWorkingHoursDayRequest[] = [];

    for (let i = 0; i < rows.length; i++) {
      const row = rows[i];
      const parsedStart = timeToMinutes(row.startTime);
      const parsedEnd = timeToMinutes(row.endTime);

      if (row.isWorkingDay) {
        if (parsedStart === null) {
          localErrors[i] = { ...localErrors[i], startMinute: "Start time is required." };
        }
        if (parsedEnd === null) {
          localErrors[i] = { ...localErrors[i], endMinute: "End time is required." };
        }
      }

      payload.push({
        dayOfWeek: row.dayOfWeek,
        isWorkingDay: row.isWorkingDay,
        startMinute: parsedStart ?? 0,
        endMinute: parsedEnd ?? 0
      });
    }

    if (Object.keys(localErrors).length > 0) {
      setRowErrors(localErrors);
      setSaveError("Enter valid start and end times for every working day.");
      return;
    }

    setSaving(true);
    setSaveError(null);
    setRowErrors({});

    try {
      const updated = await workingHoursService.update(payload);
      setData(updated);
      setRows(toDraftRows(updated.days));
      toast.success("Working hours saved.");
    } catch (err: unknown) {
      const message = getErrorMessage(err, "Could not save working hours.");
      const byRow = errorsByRow(err);
      const top = errorsByField(err);
      const nextRowErrors: Record<number, RowFieldErrors> = {};

      for (const [rowKey, fields] of Object.entries(byRow)) {
        const idx = Number(rowKey);
        if (!Number.isNaN(idx)) {
          nextRowErrors[idx] = {
            dayOfWeek: fields.dayOfWeek,
            startMinute: fields.startMinute,
            endMinute: fields.endMinute
          };
        }
      }

      setRowErrors(nextRowErrors);
      const topMessages = Object.values(top).filter((m) => m.trim().length > 0);
      setSaveError(topMessages.length > 0 ? topMessages.join(" ") : message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <LabPage
      title="Working Hours"
      subtitle="Define the weekly working schedule used to calculate escalation thresholds in laboratory time."
    >
      {loading && (
        <Paper sx={{ p: 2.5 }}>
          <Stack direction="row" spacing={1.5} sx={{ alignItems: "center" }} role="status" aria-live="polite">
            <CircularProgress size={20} />
            <Typography variant="body2" color="text.secondary">
              Loading working hours...
            </Typography>
          </Stack>
        </Paper>
      )}

      {!loading && loadError && (
        <LoadErrorAlert message={loadError} onRetry={() => setReloadToken((t) => t + 1)} />
      )}

      {!loading && !loadError && data && (
        <Stack spacing={2}>
          <Paper sx={{ p: 2.5 }}>
            <Stack spacing={1}>
              <Typography variant="body2">
                <strong>Laboratory time zone:</strong>{" "}
                <Box component="span" sx={{ fontVariantNumeric: "tabular-nums" }}>
                  {data.timeZoneId}
                  {data.timeZoneDisplayName && data.timeZoneDisplayName !== data.timeZoneId
                    ? ` (${data.timeZoneDisplayName})`
                    : ""}
                </Box>
              </Typography>
              <Typography variant="body2" color="text.secondary">
                Escalation clocks count only minutes inside these local windows and skip{" "}
                <MuiLink component={RouterLink} to={APP_ROUTES.MONITORING_PLAN} underline="hover">
                  non-working dates
                </MuiLink>{" "}
                (public holidays and shutdowns).
              </Typography>
            </Stack>
          </Paper>

          {!data.isConfigured && (
            <Alert severity="info">
              Working hours are not configured yet. Turn on at least one working day and save before creating escalation rules.
            </Alert>
          )}

          <Paper sx={{ p: 2.5 }}>
            <Box component="form" onSubmit={handleSubmit} noValidate>
              <SectionTitle>Weekly Schedule</SectionTitle>

              {saveError && (
                <Alert severity="error" role="alert" sx={{ mb: 2 }}>
                  {saveError}
                </Alert>
              )}

              <TableContainer>
                <Table aria-label="Laboratory working hours" size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Day</TableCell>
                      <TableCell>Working day</TableCell>
                      <TableCell>Start time</TableCell>
                      <TableCell>End time</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {rows.map((row, index) => {
                      const err = rowErrors[index];
                      return (
                        <TableRow key={row.dayOfWeek}>
                          <TableCell sx={{ fontWeight: 500, verticalAlign: "top", pt: 1.5 }}>
                            {row.dayOfWeek}
                            {err?.dayOfWeek && (
                              <FormHelperText error>{err.dayOfWeek}</FormHelperText>
                            )}
                          </TableCell>
                          <TableCell sx={{ verticalAlign: "top", pt: 1 }}>
                            <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                              <Switch
                                checked={row.isWorkingDay}
                                disabled={saving}
                                onChange={(e) => updateRow(index, { isWorkingDay: e.target.checked })}
                                slotProps={{ input: { "aria-label": `${row.dayOfWeek} working day` } }}
                              />
                              <Typography variant="body2" color="text.secondary">
                                {row.isWorkingDay ? "Open" : "Closed"}
                              </Typography>
                            </Stack>
                          </TableCell>
                          <TableCell sx={{ verticalAlign: "top" }}>
                            <TextField
                              type="time"
                              size="small"
                              value={row.startTime}
                              disabled={!row.isWorkingDay || saving}
                              error={Boolean(err?.startMinute)}
                              helperText={err?.startMinute}
                              onChange={(e) => updateRow(index, { startTime: e.target.value })}
                              slotProps={{
                                htmlInput: {
                                  "aria-label": `${row.dayOfWeek} start time`,
                                  step: 60
                                }
                              }}
                              sx={{ minWidth: 140, fontVariantNumeric: "tabular-nums" }}
                            />
                          </TableCell>
                          <TableCell sx={{ verticalAlign: "top" }}>
                            <TextField
                              type="time"
                              size="small"
                              value={row.endTime}
                              disabled={!row.isWorkingDay || saving}
                              error={Boolean(err?.endMinute)}
                              helperText={err?.endMinute}
                              onChange={(e) => updateRow(index, { endTime: e.target.value })}
                              slotProps={{
                                htmlInput: {
                                  "aria-label": `${row.dayOfWeek} end time`,
                                  step: 60
                                }
                              }}
                              sx={{ minWidth: 140, fontVariantNumeric: "tabular-nums" }}
                            />
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </TableContainer>

              <Stack direction="row" spacing={1.5} sx={{ mt: 2.5, justifyContent: "flex-end" }}>
                <Button
                  type="submit"
                  variant="contained"
                  disabled={saving}
                  startIcon={saving ? <CircularProgress size={16} color="inherit" /> : undefined}
                >
                  {saving ? "Saving..." : "Save working hours"}
                </Button>
              </Stack>
            </Box>
          </Paper>
        </Stack>
      )}
    </LabPage>
  );
}
