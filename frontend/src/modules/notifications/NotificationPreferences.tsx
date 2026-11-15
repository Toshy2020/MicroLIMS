import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography
} from "@mui/material";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import { visuallyHidden } from "@mui/utils";
import { toast } from "sonner";
import { SectionTitle } from "../../components/SectionTitle";
import { getErrorMessage } from "../../utils/errorMessage";
import { getFieldErrors } from "../../utils/fieldErrors";
import {
  type EmailMode,
  type NotificationPreferenceRowDto,
  notificationService
} from "./services/notificationService";

const EMAIL_MODE_OPTIONS: { value: EmailMode; label: string }[] = [
  { value: "Off", label: "Off" },
  { value: "Immediate", label: "Immediate" },
  { value: "Digest", label: "Digest" }
];

interface NotificationPreferencesProps {
  refreshKey?: number;
}

function buildDraft(rows: NotificationPreferenceRowDto[]): Record<string, EmailMode> {
  const next: Record<string, EmailMode> = {};
  for (const row of rows) {
    if (row.emailAllowed) {
      next[row.type] = row.emailMode;
    }
  }
  return next;
}

export function NotificationPreferences({ refreshKey = 0 }: NotificationPreferencesProps) {
  const [rows, setRows] = useState<NotificationPreferenceRowDto[] | null>(null);
  const [draft, setDraft] = useState<Record<string, EmailMode>>({});
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [rowErrors, setRowErrors] = useState<Record<string, string>>({});
  const [reloadToken, setReloadToken] = useState(0);

  const load = useCallback(
    (signal: AbortSignal) => {
      setLoading(true);
      setLoadError(null);
      notificationService
        .getPreferences(signal)
        .then((data) => {
          if (signal.aborted) return;
          setRows(data);
          setDraft(buildDraft(data));
          setRowErrors({});
          setSaveError(null);
          setLoading(false);
        })
        .catch((err: unknown) => {
          if (signal.aborted) return;
          setLoadError(getErrorMessage(err, "Could not load notification preferences."));
          setLoading(false);
        });
    },
    []
  );

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal);
    return () => controller.abort();
  }, [load, refreshKey, reloadToken]);

  const editableRows = useMemo(
    () => (rows ?? []).filter((row) => row.emailAllowed && !row.mandatory),
    [rows]
  );

  const handleModeChange = (type: string, mode: EmailMode) => {
    setDraft((prev) => ({ ...prev, [type]: mode }));
    setRowErrors((prev) => {
      if (!(type in prev)) return prev;
      const next = { ...prev };
      delete next[type];
      return next;
    });
    if (saveError) setSaveError(null);
  };

  const handleSave = async () => {
    if (!rows || editableRows.length === 0 || saving) return;
    setSaving(true);
    setSaveError(null);
    setRowErrors({});

    const payload = editableRows.map((row) => ({
      type: row.type,
      emailMode: draft[row.type] ?? row.emailMode
    }));

    try {
      const updated = await notificationService.putPreferences(payload);
      setRows(updated);
      setDraft(buildDraft(updated));
      toast.success("Notification preferences saved.");
    } catch (err: unknown) {
      const message = getErrorMessage(err, "Could not save notification preferences.");
      const fieldErrors = getFieldErrors(err);
      const nextRowErrors: Record<string, string> = {};
      const generalMessages: string[] = [];

      for (const fe of fieldErrors) {
        const match = /^preferences\[(\d+)\]\./.exec(fe.field);
        if (match) {
          const item = payload[Number(match[1])];
          if (item && !(item.type in nextRowErrors)) {
            nextRowErrors[item.type] = fe.message;
          }
        } else if (!generalMessages.includes(fe.message)) {
          generalMessages.push(fe.message);
        }
      }

      setRowErrors(nextRowErrors);
      setSaveError(generalMessages.length > 0 ? generalMessages.join(" ") : message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Box component="section" aria-label="Notifications">
      <SectionTitle>Notifications</SectionTitle>
      <Paper sx={{ p: 2.5, maxWidth: 760, mb: 2 }}>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          In-app notifications stay on for all operational events. Email delivery can be set per notification type unless your section requires immediate delivery.
        </Typography>

        {loading && (
          <Stack direction="row" spacing={1.5} sx={{ alignItems: "center", py: 2 }} role="status" aria-live="polite">
            <CircularProgress size={20} />
            <Typography variant="body2" color="text.secondary">
              Loading notification preferences...
            </Typography>
          </Stack>
        )}

        {!loading && loadError && (
          <Alert
            severity="error"
            action={
              <Button color="inherit" size="small" onClick={() => setReloadToken((n) => n + 1)}>
                Retry
              </Button>
            }
          >
            {loadError}
          </Alert>
        )}

        {!loading && !loadError && rows && rows.length === 0 && (
          <Typography variant="body2" color="text.secondary">
            No notification types are configured.
          </Typography>
        )}

        {!loading && !loadError && rows && rows.length > 0 && (
          <>
            {saveError && (
              <Alert severity="error" sx={{ mb: 2 }}>
                {saveError}
              </Alert>
            )}

            <TableContainer>
              <Table size="small" aria-label="Notification preferences">
                <TableHead>
                  <TableRow>
                    <TableCell>Notification</TableCell>
                    <TableCell>In app</TableCell>
                    <TableCell sx={{ minWidth: 180 }}>Email</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {rows.map((row) => {
                    const labelId = `pref-email-label-${row.type}`;
                    const errorText = rowErrors[row.type];
                    const mode = draft[row.type] ?? row.emailMode;

                    return (
                      <TableRow key={row.type}>
                        <TableCell>
                          <Typography variant="body2" sx={{ fontWeight: 500 }}>
                            {row.title}
                          </Typography>
                          {row.mandatory && (
                            <Stack direction="row" spacing={0.5} sx={{ alignItems: "center", mt: 0.5 }}>
                              <LockOutlinedIcon fontSize="inherit" color="action" aria-hidden="true" />
                              <Typography variant="caption" color="text.secondary">
                                {row.mandatoryReason ?? "Mandatory for your section"}
                              </Typography>
                            </Stack>
                          )}
                        </TableCell>

                        <TableCell>
                          <Typography variant="body2" sx={{ fontWeight: 500 }}>
                            Always on
                          </Typography>
                          <Box component="span" sx={visuallyHidden}>
                            In-app notifications are required for all users and cannot be turned off.
                          </Box>
                        </TableCell>

                        <TableCell>
                          {!row.emailAllowed ? (
                            <Typography variant="body2" color="text.secondary">
                              In-app only
                            </Typography>
                          ) : (
                            <FormControl size="small" fullWidth error={Boolean(errorText)}>
                              <InputLabel id={labelId} sx={visuallyHidden}>
                                {row.title} email delivery
                              </InputLabel>
                              <Select
                                labelId={labelId}
                                value={mode}
                                disabled={row.mandatory || saving}
                                onChange={(event) => handleModeChange(row.type, event.target.value as EmailMode)}
                              >
                                {EMAIL_MODE_OPTIONS.map((option) => (
                                  <MenuItem key={option.value} value={option.value}>
                                    {option.label}
                                  </MenuItem>
                                ))}
                              </Select>
                              {errorText && <FormHelperText>{errorText}</FormHelperText>}
                            </FormControl>
                          )}
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </TableContainer>

            <Stack direction="row" sx={{ justifyContent: "flex-end", mt: 2 }}>
              <Button
                variant="contained"
                onClick={handleSave}
                disabled={saving || editableRows.length === 0}
              >
                {saving ? "Saving..." : "Save preferences"}
              </Button>
            </Stack>
          </>
        )}
      </Paper>
    </Box>
  );
}
