import { useCallback, useEffect, useState } from "react";
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Drawer, IconButton, Paper,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TablePagination, TextField, Typography
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { toast } from "sonner";
import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import { getErrorMessage } from "../../utils/errorMessage";
import { i18n, useTranslation } from "../../i18n";
import { BackgroundJobRun, BackgroundJobRunPage, BackgroundJobService, BackgroundJobStatus } from "./services/BackgroundJobService";

const tl = (key: string, options?: Record<string, unknown>): string => i18n.t(key, { ns: "admin", ...options });

function OutcomeChip({ run }: { run: BackgroundJobRun | null }) {
  const { t } = useTranslation("admin");
  if (!run) return <Chip size="small" label={t("jobs.noRuns")} />;
  const color = run.outcome === "Succeeded" ? "success" : run.outcome === "Failed" ? "error" : run.outcome === "Running" ? "info" : "default";
  return <Chip size="small" color={color} label={run.outcome} />;
}

// Status of the background workers. Housekeeping jobs can be run by hand (with a reason, audited);
// GxP jobs change laboratory records and are shown for status only.
export function JobsPage() {
  const { t } = useTranslation("admin");
  const { t: tc } = useTranslation("common");
  const when = (v: string | null) => (v ? formatDateTimeWithTimeZone(v) : t("common.never"));
  const [jobs, setJobs] = useState<BackgroundJobStatus[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [runTarget, setRunTarget] = useState<BackgroundJobStatus | null>(null);
  const [reason, setReason] = useState("");
  const [running, setRunning] = useState(false);
  const [historyJob, setHistoryJob] = useState<BackgroundJobStatus | null>(null);
  const [history, setHistory] = useState<BackgroundJobRunPage | null>(null);
  const [historyPage, setHistoryPage] = useState(0);

  const load = useCallback(() => {
    BackgroundJobService.list().then(setJobs).catch(() => setError(tl("jobs.loadError")));
  }, []);
  useEffect(load, [load]);

  useEffect(() => {
    if (!historyJob) return;
    setHistory(null);
    BackgroundJobService.runs(historyJob.name, historyPage + 1, 20).then(setHistory).catch(() => toast.error(tl("jobs.historyError")));
  }, [historyJob, historyPage]);

  const confirmRun = async () => {
    if (!runTarget || !reason.trim()) return;
    setRunning(true);
    try {
      const run = await BackgroundJobService.run(runTarget.name, reason.trim());
      if (run.outcome === "Failed") toast.error(t("jobs.failedToast", { name: runTarget.name, error: run.error ?? t("jobs.unknownError") }));
      else toast.success(t("jobs.finished", { name: runTarget.name }));
      setRunTarget(null);
      setReason("");
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, t("jobs.runError")));
    } finally {
      setRunning(false);
    }
  };

  return (
    <>
      <PageHeader title={t("jobs.title")} subtitle={t("jobs.subtitle")} />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {!jobs && !error && <LoadingSpinner />}
      {jobs && (
        <TableContainer component={Paper}>
          <Table size="small" aria-label={t("jobs.table")}>
            <TableHead>
              <TableRow>
                <TableCell>{t("jobs.job")}</TableCell>
                <TableCell>{t("jobs.type")}</TableCell>
                <TableCell>{t("jobs.schedule")}</TableCell>
                <TableCell>{t("jobs.lastRun")}</TableCell>
                <TableCell>{t("jobs.lastSuccess")}</TableCell>
                <TableCell>{t("jobs.lastFailure")}</TableCell>
                <TableCell>{t("common.actions")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {jobs.map((job) => (
                <TableRow key={job.name}>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>{job.name}</Typography>
                    <Typography variant="caption" color="text.secondary">{job.description}</Typography>
                  </TableCell>
                  <TableCell title={job.classificationReason}>
                    <Chip size="small" variant="outlined" label={job.classification === "Gxp" ? t("jobs.gxpStatusOnly") : t("jobs.housekeeping")} />
                  </TableCell>
                  <TableCell>{job.schedule}</TableCell>
                  <TableCell>
                    {job.runningNow ? <Chip size="small" color="info" label={t("jobs.runningNow")} /> : <OutcomeChip run={job.lastRun} />}
                    {job.lastRun && <Typography variant="caption" sx={{ display: "block" }}>{when(job.lastRun.startedAt)}</Typography>}
                  </TableCell>
                  <TableCell>{when(job.lastSuccessAt)}</TableCell>
                  <TableCell>{when(job.lastFailureAt)}</TableCell>
                  <TableCell>
                    <Box sx={{ display: "flex", gap: 1 }}>
                      <Button size="small" onClick={() => { setHistoryPage(0); setHistoryJob(job); }}>{t("jobs.history")}</Button>
                      {job.canRunManually && (
                        <Button size="small" variant="outlined" disabled={job.runningNow} onClick={() => { setReason(""); setRunTarget(job); }}>
                          {t("jobs.runNow")}
                        </Button>
                      )}
                    </Box>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={!!runTarget} onClose={running ? undefined : () => setRunTarget(null)} maxWidth="xs" fullWidth>
        <DialogTitle>{t("jobs.runTitle", { name: runTarget?.name ?? "" })}</DialogTitle>
        <DialogContent>
          <Typography variant="body2" sx={{ mb: 2 }}>
            {t("jobs.runIntro")}
          </Typography>
          <TextField
            label={t("common.reason")} value={reason} onChange={(e) => setReason(e.target.value)}
            fullWidth multiline minRows={2} required slotProps={{ htmlInput: { maxLength: 500 } }} autoFocus
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRunTarget(null)} disabled={running}>{tc("actions.cancel")}</Button>
          <Button variant="contained" onClick={confirmRun} disabled={running || !reason.trim()}>{t("jobs.runNow")}</Button>
        </DialogActions>
      </Dialog>

      <Drawer anchor="right" open={!!historyJob} onClose={() => setHistoryJob(null)}>
        <Box sx={{ width: { xs: "100vw", sm: 560 }, p: 2 }} role="region" aria-label={t("jobs.historyRegion")}>
          <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 1 }}>
            <Typography variant="h6" component="h2">{t("jobs.runsTitle", { name: historyJob?.name ?? "" })}</Typography>
            <IconButton aria-label={tc("actions.close")} onClick={() => setHistoryJob(null)}><CloseIcon /></IconButton>
          </Box>
          {!history && <LoadingSpinner />}
          {history && (
            <>
              <Table size="small" aria-label={t("jobs.historyTable")}>
                <TableHead>
                  <TableRow>
                    <TableCell>{t("jobs.started")}</TableCell>
                    <TableCell>{t("jobs.outcome")}</TableCell>
                    <TableCell>{t("jobs.items")}</TableCell>
                    <TableCell>{t("jobs.trigger")}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {history.items.length === 0 && <TableRow><TableCell colSpan={4}>{t("jobs.noRunsRecorded")}</TableCell></TableRow>}
                  {history.items.map((r) => (
                    <TableRow key={r.id}>
                      <TableCell>{when(r.startedAt)}</TableCell>
                      <TableCell>
                        <OutcomeChip run={r} />
                        {r.error && <Typography variant="caption" color="error" sx={{ display: "block" }}>{r.error}</Typography>}
                      </TableCell>
                      <TableCell>{r.itemsProcessed ?? ""}</TableCell>
                      <TableCell>
                        {r.triggeredBy}
                        {r.reason && <Typography variant="caption" sx={{ display: "block" }}>{r.reason}</Typography>}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              <TablePagination
                component="div" count={history.totalCount} page={historyPage} rowsPerPage={20} rowsPerPageOptions={[20]}
                onPageChange={(_, p) => setHistoryPage(p)}
              />
            </>
          )}
        </Box>
      </Drawer>
    </>
  );
}
