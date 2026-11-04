import { useEffect, useState } from "react";
import { Alert, Box, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from "@mui/material";
import { PageHeader } from "../../components/PageHeader";
import { TableSkeleton } from "../../components/TableSkeleton";
import {
  DatabaseOverviewService,
  type BackupRun,
  type DatabaseOverview
} from "./services/DatabaseOverviewService";

// A successful backup this recent counts as current. Presentation only: the
// backend reports the run, and this decides how the status is coloured.
export const BACKUP_FRESH_WINDOW_MS = 24 * 60 * 60 * 1000;

export type BackupStatusTone = "success" | "warning";

export function backupStatusTone(backup: BackupRun | null, now: Date): BackupStatusTone {
  if (!backup || !backup.succeeded) return "warning";
  const finished = new Date(backup.finishedAtUtc).getTime();
  return now.getTime() - finished <= BACKUP_FRESH_WINDOW_MS ? "success" : "warning";
}

function backupMessage(backup: BackupRun | null, now: Date): string {
  if (!backup) return "No backup has been recorded yet.";
  if (!backup.succeeded) return `Last backup failed at ${new Date(backup.finishedAtUtc).toLocaleString()}: ${backup.error ?? "unknown error"}`;
  const ageHours = (now.getTime() - new Date(backup.finishedAtUtc).getTime()) / 3600000;
  const when = `${new Date(backup.finishedAtUtc).toLocaleString()} (${backup.fileName})`;
  return ageHours <= 24
    ? `Last successful backup: ${when}.`
    : `Last successful backup is older than 24 hours: ${when}.`;
}

function formatRows(value: number | null): string {
  return value === null ? "not analyzed yet" : value.toLocaleString();
}

export function DatabaseOverviewPage() {
  const [overview, setOverview] = useState<DatabaseOverview | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    DatabaseOverviewService.get()
      .then((data) => {
        if (!cancelled) setOverview(data);
      })
      .catch((err: unknown) => {
        const message = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
        if (!cancelled) setError(message ?? "Could not load the database overview.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <>
      <PageHeader title="Database overview" subtitle="Size of the database and the status of the last backup. Read-only." />

      {error && <Alert severity="error">{error}</Alert>}
      {loading && <TableSkeleton rows={6} cols={3} />}

      {overview && (
        <Stack spacing={2}>
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="subtitle1" component="h2" sx={{ fontWeight: 600 }}>Database size</Typography>
            <Typography variant="h5" component="p">{overview.databaseSizePretty}</Typography>
            <Typography variant="body2" color="text.secondary">{overview.databaseSizeBytes.toLocaleString()} bytes</Typography>
          </Paper>

          <Alert severity={backupStatusTone(overview.latestBackup, new Date()) === "success" ? "success" : "warning"}>
            {backupMessage(overview.latestBackup, new Date())}
          </Alert>

          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="subtitle1" component="h2" sx={{ fontWeight: 600, mb: 1 }}>Largest tables</Typography>
            <Box sx={{ overflowX: "auto" }}>
              <Table size="small" aria-label="Largest tables">
                <TableHead>
                  <TableRow>
                    <TableCell>Table</TableCell>
                    <TableCell align="right">Total size</TableCell>
                    <TableCell align="right">Estimated rows</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {overview.topTables.map((t) => (
                    <TableRow key={`${t.schema}.${t.name}`}>
                      <TableCell>{t.schema}.{t.name}</TableCell>
                      <TableCell align="right">{t.totalSizePretty}</TableCell>
                      <TableCell align="right">{formatRows(t.estimatedRows)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
          </Paper>
        </Stack>
      )}
    </>
  );
}
