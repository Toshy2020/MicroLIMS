import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Chip, MenuItem, Paper, Stack, TextField, Typography
} from "@mui/material";
import { DialogBodySkeleton } from "../../../components/DialogBodySkeleton";
import { Ltr } from "../../../components/Ltr";
import { i18n, useTranslation } from "../../../i18n";
import { serverMessage } from "../../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
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
  const { t } = useTranslation("errorMonitoring");
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
    } catch (err: unknown) {
      setError(serverMessage(err) ?? i18n.t("errors.detail", { ns: "errorMonitoring" }));
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
    } catch (err: unknown) {
      setError(serverMessage(err) ?? t("errors.action"));
    } finally {
      setBusy(false);
    }
  };

  if (loading) return <Box sx={{ p: 2 }}><DialogBodySkeleton blocks={3} blockHeight={28} /></Box>;
  if (error && !detail) return <Alert severity="error" sx={{ m: 2 }}>{error}</Alert>;
  if (!detail) return null;

  return (
    <Box sx={{ p: 2, bgcolor: "action.hover" }}>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Stack
        direction="row"
        spacing={2}
        useFlexGap
        sx={{
          alignItems: "center",
          flexWrap: "wrap",
          mb: 2
        }}>
        <Typography variant="body2" sx={{
          color: "text.secondary"
        }}>
          {t("row.correlationId")} <Ltr><code>{detail.correlationId}</code></Ltr>
        </Typography>

        {detail.status === "Open" ? (
          <>
            <TextField
              size="small" label={t("row.resolutionNotes")} sx={{ minWidth: 280 }}
              value={notes} onChange={(e) => setNotes(e.target.value)} disabled={busy}
            />
            <Button
              size="small" variant="contained" disabled={busy}
              onClick={() => act(() => ErrorMonitoringService.resolve(detail.id, notes.trim() || null))}
            >
              {t("row.markResolved")}
            </Button>
          </>
        ) : (
          <>
            <Typography variant="body2" sx={{
              color: "text.secondary"
            }}>
              {t("row.resolved", { date: detail.resolvedAtUtc ? formatDateTimeWithTimeZone(detail.resolvedAtUtc) : "" })}
              {detail.resolvedByUserName ? t("row.resolvedBy", { user: detail.resolvedByUserName }) : ""}
              {detail.resolutionNotes ? t("row.resolvedNotes", { notes: detail.resolutionNotes }) : ""}
            </Typography>
            <Button
              size="small" variant="outlined" disabled={busy}
              onClick={() => act(() => ErrorMonitoringService.reopen(detail.id))}
            >
              {t("row.reopen")}
            </Button>
          </>
        )}
      </Stack>

      <Stack spacing={2}>
        {detail.errorLogs.map((log) => (
          <Paper key={log.id} variant="outlined" sx={{ p: 2 }}>
            <Stack
              direction="row"
              spacing={1}
              useFlexGap
              sx={{
                alignItems: "center",
                flexWrap: "wrap",
                mb: 1
              }}>
              <Chip size="small" label={log.source} />
              <Chip size="small" color={SEVERITY_COLORS[log.effectiveSeverity]} label={log.effectiveSeverity} />
              {log.severityOverride && (
                <Chip size="small" variant="outlined" label={t("row.overridden", { severity: log.severity })} />
              )}
              <Typography variant="body2" sx={{
                color: "text.secondary"
              }}>
                {formatDateTimeWithTimeZone(log.occurredAtUtc)}
              </Typography>
              {log.httpMethod && log.requestPath && (
                <Typography variant="body2" sx={{
                  color: "text.secondary"
                }}>
                  <Ltr>{log.httpMethod} {log.requestPath}{log.statusCode ? ` -> ${log.statusCode}` : ""}</Ltr>
                </Typography>
              )}
              {!log.httpMethod && log.requestPath && (
                <Typography variant="body2" sx={{
                  color: "text.secondary"
                }}><Ltr>{log.requestPath}</Ltr></Typography>
              )}
              {log.userName && (
                <Typography variant="body2" sx={{
                  color: "text.secondary"
                }}>{t("row.by", { user: log.userName })}</Typography>
              )}

              <Box sx={{ flexGrow: 1 }} />

              <TextField
                select size="small" label={t("row.override")} sx={{ minWidth: 150 }}
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
                <MenuItem value="">{t("row.auto", { severity: log.severity })}</MenuItem>
                {SEVERITIES.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
              </TextField>
            </Stack>

            <Typography variant="body2" sx={{ fontWeight: 600 }}>{log.exceptionType}</Typography>
            <Typography variant="body2" sx={{ mb: 1 }}>{log.message}</Typography>

            {log.stackTrace && (
              <Box
                component="pre"
                sx={{
                  m: 0, mt: 1, p: 1, fontSize: 12, maxHeight: 220,
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
                  m: 0, mt: 1, p: 1, fontSize: 12, maxHeight: 160,
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
