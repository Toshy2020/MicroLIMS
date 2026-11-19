import { useEffect, useState } from "react";
import { Alert, Box, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from "@mui/material";
import { PageHeader } from "../../components/PageHeader";
import { TableSkeleton } from "../../components/TableSkeleton";
import {
  DatabaseOverviewService,
  type BackupRun,
  type DatabaseOverview
} from "./services/DatabaseOverviewService";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import type { TFunction } from "i18next";
import { i18n, useTranslation } from "../../i18n";
import { formatCount } from "../../utils/formatCount";

const tl = (key: string, options?: Record<string, unknown>): string => i18n.t(key, { ns: "admin", ...options });

// A successful backup this recent counts as current. Presentation only: the
// backend reports the run, and this decides how the status is coloured.
export const BACKUP_FRESH_WINDOW_MS = 24 * 60 * 60 * 1000;

export type BackupStatusTone = "success" | "warning";

export function backupStatusTone(backup: BackupRun | null, now: Date): BackupStatusTone {
  if (!backup || !backup.succeeded) return "warning";
  const finished = new Date(backup.finishedAtUtc).getTime();
  return now.getTime() - finished <= BACKUP_FRESH_WINDOW_MS ? "success" : "warning";
}

function backupMessage(backup: BackupRun | null, now: Date, t: TFunction<"admin">): string {
  if (!backup) return t("dbOverview.noBackup");
  if (!backup.succeeded) return t("dbOverview.backupFailed", { time: formatDateTimeWithTimeZone(backup.finishedAtUtc), error: backup.error ?? t("dbOverview.unknownError") });
  const ageHours = (now.getTime() - new Date(backup.finishedAtUtc).getTime()) / 3600000;
  const when = `${formatDateTimeWithTimeZone(backup.finishedAtUtc)} (${backup.fileName})`;
  return ageHours <= 24
    ? t("dbOverview.backupOk", { when })
    : t("dbOverview.backupOld", { when });
}

function formatRows(value: number | null, notAnalyzed: string): string {
  return value === null ? notAnalyzed : formatCount(value);
}

export function DatabaseOverviewPage() {
  const { t } = useTranslation("admin");
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
        if (!cancelled) setError(message ?? tl("dbOverview.loadError"));
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
      <PageHeader title={t("dbOverview.title")} subtitle={t("dbOverview.subtitle")} />

      {error && <Alert severity="error">{error}</Alert>}
      {loading && <TableSkeleton rows={6} cols={3} />}

      {overview && (
        <Stack spacing={2}>
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="subtitle1" component="h2" sx={{ fontWeight: 600 }}>{t("dbOverview.size")}</Typography>
            <Typography variant="h5" component="p">{overview.databaseSizePretty}</Typography>
            <Typography variant="body2" color="text.secondary">{t("dbOverview.bytes", { bytes: formatCount(overview.databaseSizeBytes) })}</Typography>
          </Paper>

          <Alert severity={backupStatusTone(overview.latestBackup, new Date()) === "success" ? "success" : "warning"}>
            {backupMessage(overview.latestBackup, new Date(), t)}
          </Alert>

          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="subtitle1" component="h2" sx={{ fontWeight: 600, mb: 1 }}>{t("dbOverview.largestTables")}</Typography>
            <Box sx={{ overflowX: "auto" }}>
              <Table size="small" aria-label={t("dbOverview.largestTables")}>
                <TableHead>
                  <TableRow>
                    <TableCell>{t("dbOverview.table")}</TableCell>
                    <TableCell align="right">{t("dbOverview.totalSize")}</TableCell>
                    <TableCell align="right">{t("dbOverview.estimatedRows")}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {overview.topTables.map((tbl) => (
                    <TableRow key={`${tbl.schema}.${tbl.name}`}>
                      <TableCell>{tbl.schema}.{tbl.name}</TableCell>
                      <TableCell align="right">{tbl.totalSizePretty}</TableCell>
                      <TableCell align="right">{formatRows(tbl.estimatedRows, t("dbOverview.notAnalyzed"))}</TableCell>
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
