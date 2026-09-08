import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Chip, MenuItem, Paper, Stack, TextField, Typography
} from "@mui/material";
import { LoadingSpinner } from "../../../components/LoadingSpinner";
import { formatLabDateTime } from "../../../utils/formatDate";
import { ErrorMonitoringService } from "../services/ErrorMonitoringService";
import type { ErrorSeverity, IncidentDetail, IncidentListItem } from "../types/errorMonitoringTypes";
import { SEVERITIES, SEVERITY_COLORS } from "../types/errorMonitoringTypes";

interface Props {
  incident: IncidentListItem;
  onChanged: () => void;
}

// Expanded row: the child ErrorLog entries with their full context, plus
// the two triage actions. Deliberately not a dialog - the admin is
// comparing entries within one incident, and a modal would hide the list.
export function IncidentRowDetail({ incident, onChanged }: Props) {
  const [detail, setDetail] = useState<IncidentDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [notes, setNotes] = useState("");

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      setDetail(await ErrorMonitoringService.get(incident.id));
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "Could not load incident detail.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [incident.id]);

  const act = async (action: () => Promise<void>) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      await load();
      onChanged();
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "The action could not be completed.");
    } finally {
      setBusy(false);
    }
  };

  if (loading) return <Box sx={{ p: 2 }}><LoadingSpinner /></Box>;
  if (error && !detail) return <Alert severity="error" sx={{ m: 2 }}>{error}</Alert>;
  if (!detail) return null;

  return (
    <Box sx={{ p: 2, bgcolor: "action.hover" }}>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 2 }} flexWrap="wrap" useFlexGap>
        <Typography variant="body2" color="text.secondary">
          Correlation id: <code>{detail.correlationId}</code>
        </Typography>

        {detail.status === "Open" ? (
          <>
            <TextField
              size="small" label="Resolution notes" sx={{ minWidth: 280 }}
              value={notes} onChange={(e) => setNotes(e.target.value)} disabled={busy}
            />
            <Button
              size="small" variant="contained" disabled={busy}
              onClick={() => act(() => ErrorMonitoringService.resolve(detail.id, notes.trim() || null))}
            >
              Mark resolved
            </Button>
          </>
        ) : (
          <>
            <Typography variant="body2" color="text.secondary">
              Resolved {detail.resolvedAtUtc ? formatLabDateTime(detail.resolvedAtUtc) : ""}
              {detail.resolvedByUserName ? ` by ${detail.resolvedByUserName}` : ""}
              {detail.resolutionNotes ? ` - ${detail.resolutionNotes}` : ""}
            </Typography>
            <Button
              size="small" variant="outlined" disabled={busy}
              onClick={() => act(() => ErrorMonitoringService.reopen(detail.id))}
            >
              Reopen
            </Button>
          </>
        )}
      </Stack>

      <Stack spacing={2}>
        {detail.errorLogs.map((log) => (
          <Paper key={log.id} variant="outlined" sx={{ p: 2 }}>
            <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap sx={{ mb: 1 }}>
              <Chip size="small" label={log.source} />
              <Chip size="small" color={SEVERITY_COLORS[log.effectiveSeverity]} label={log.effectiveSeverity} />
              {log.severityOverride && (
                <Chip size="small" variant="outlined" label={`overridden from ${log.severity}`} />
              )}
              <Typography variant="body2" color="text.secondary">
                {formatLabDateTime(log.occurredAtUtc)}
              </Typography>
              {log.httpMethod && log.requestPath && (
                <Typography variant="body2" color="text.secondary">
                  {log.httpMethod} {log.requestPath}{log.statusCode ? ` -> ${log.statusCode}` : ""}
                </Typography>
              )}
              {!log.httpMethod && log.requestPath && (
                <Typography variant="body2" color="text.secondary">{log.requestPath}</Typography>
              )}
              {log.userName && (
                <Typography variant="body2" color="text.secondary">by {log.userName}</Typography>
              )}

              <Box sx={{ flexGrow: 1 }} />

              <TextField
                select size="small" label="Override" sx={{ minWidth: 150 }}
                value={log.severityOverride ?? ""}
                disabled={busy}
                onChange={(e) =>
                  act(() =>
                    ErrorMonitoringService.overrideSeverity(
                      log.id,
                      (e.target.value || null) as ErrorSeverity | null
                    )
                  )
                }
              >
                <MenuItem value="">Auto ({log.severity})</MenuItem>
                {SEVERITIES.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
              </TextField>
            </Stack>

            <Typography variant="body2" sx={{ fontWeight: 600 }}>{log.exceptionType}</Typography>
            <Typography variant="body2" sx={{ mb: 1 }}>{log.message}</Typography>

            {log.stackTrace && (
              <Box
                component="pre"
                sx={{
                  m: 0, mt: 1, p: 1, fontSize: 11, maxHeight: 220,
                  overflow: "auto", bgcolor: "background.default", borderRadius: 1
                }}
              >
                {log.stackTrace}
              </Box>
            )}

            {log.rawContext && (
              <Box
                component="pre"
                sx={{
                  m: 0, mt: 1, p: 1, fontSize: 11, maxHeight: 160,
                  overflow: "auto", bgcolor: "background.default", borderRadius: 1
                }}
              >
                {formatRawContext(log.rawContext)}
              </Box>
            )}
          </Paper>
        ))}
      </Stack>
    </Box>
  );
}

function formatRawContext(raw: string): string {
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}
