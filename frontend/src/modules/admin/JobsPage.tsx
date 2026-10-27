import { useCallback, useEffect, useState } from "react";
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Drawer, IconButton, Paper,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TablePagination, TextField, Typography
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { toast } from "sonner";
import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { formatLabDateTime } from "../../utils/formatDate";
import { getErrorMessage } from "../../utils/errorMessage";
import { BackgroundJobRun, BackgroundJobRunPage, BackgroundJobService, BackgroundJobStatus } from "./services/BackgroundJobService";

const when = (v: string | null) => (v ? formatLabDateTime(v) : "Never");

function OutcomeChip({ run }: { run: BackgroundJobRun | null }) {
  if (!run) return <Chip size="small" label="No runs yet" />;
  const color = run.outcome === "Succeeded" ? "success" : run.outcome === "Failed" ? "error" : run.outcome === "Running" ? "info" : "default";
  return <Chip size="small" color={color} label={run.outcome} />;
}

// Status of the background workers. Housekeeping jobs can be run by hand (with a reason, audited);
// GxP jobs change laboratory records and are shown for status only.
export function JobsPage() {
  const [jobs, setJobs] = useState<BackgroundJobStatus[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [runTarget, setRunTarget] = useState<BackgroundJobStatus | null>(null);
  const [reason, setReason] = useState("");
  const [running, setRunning] = useState(false);
  const [historyJob, setHistoryJob] = useState<BackgroundJobStatus | null>(null);
  const [history, setHistory] = useState<BackgroundJobRunPage | null>(null);
  const [historyPage, setHistoryPage] = useState(0);

  const load = useCallback(() => {
    BackgroundJobService.list().then(setJobs).catch(() => setError("Could not load the background jobs. Reload the page to try again."));
  }, []);
  useEffect(load, [load]);

  useEffect(() => {
    if (!historyJob) return;
    setHistory(null);
    BackgroundJobService.runs(historyJob.name, historyPage + 1, 20).then(setHistory).catch(() => toast.error("Could not load the run history."));
  }, [historyJob, historyPage]);

  const confirmRun = async () => {
    if (!runTarget || !reason.trim()) return;
    setRunning(true);
    try {
      const run = await BackgroundJobService.run(runTarget.name, reason.trim());
      if (run.outcome === "Failed") toast.error(`${runTarget.name} failed: ${run.error ?? "unknown error"}`);
      else toast.success(`${runTarget.name} finished.`);
      setRunTarget(null);
      setReason("");
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, "The job could not be run."));
    } finally {
      setRunning(false);
    }
  };

  return (
    <>
      <PageHeader title="Background Jobs" subtitle="Status of the scheduled workers. Housekeeping jobs can be run now; jobs that change GxP records run on their schedule only." />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {!jobs && !error && <LoadingSpinner />}
      {jobs && (
        <TableContainer component={Paper}>
          <Table size="small" aria-label="Background jobs">
            <TableHead>
              <TableRow>
                <TableCell>Job</TableCell>
                <TableCell>Type</TableCell>
                <TableCell>Schedule</TableCell>
                <TableCell>Last run</TableCell>
                <TableCell>Last success</TableCell>
                <TableCell>Last failure</TableCell>
                <TableCell>Actions</TableCell>
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
                    <Chip size="small" variant="outlined" label={job.classification === "Gxp" ? "GxP (status only)" : "Housekeeping"} />
                  </TableCell>
                  <TableCell>{job.schedule}</TableCell>
                  <TableCell>
                    {job.runningNow ? <Chip size="small" color="info" label="Running now" /> : <OutcomeChip run={job.lastRun} />}
                    {job.lastRun && <Typography variant="caption" sx={{ display: "block" }}>{when(job.lastRun.startedAt)}</Typography>}
                  </TableCell>
                  <TableCell>{when(job.lastSuccessAt)}</TableCell>
                  <TableCell>{when(job.lastFailureAt)}</TableCell>
                  <TableCell>
                    <Box sx={{ display: "flex", gap: 1 }}>
                      <Button size="small" onClick={() => { setHistoryPage(0); setHistoryJob(job); }}>History</Button>
                      {job.canRunManually && (
                        <Button size="small" variant="outlined" disabled={job.runningNow} onClick={() => { setReason(""); setRunTarget(job); }}>
                          Run now
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
        <DialogTitle>Run {runTarget?.name} now</DialogTitle>
        <DialogContent>
          <Typography variant="body2" sx={{ mb: 2 }}>
            This runs the same pass as the schedule. Your name and the reason are recorded in the security audit trail.
          </Typography>
          <TextField
            label="Reason" value={reason} onChange={(e) => setReason(e.target.value)}
            fullWidth multiline minRows={2} required slotProps={{ htmlInput: { maxLength: 500 } }} autoFocus
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRunTarget(null)} disabled={running}>Cancel</Button>
          <Button variant="contained" onClick={confirmRun} disabled={running || !reason.trim()}>Run now</Button>
        </DialogActions>
      </Dialog>

      <Drawer anchor="right" open={!!historyJob} onClose={() => setHistoryJob(null)}>
        <Box sx={{ width: { xs: "100vw", sm: 560 }, p: 2 }} role="region" aria-label="Run history">
          <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 1 }}>
            <Typography variant="h6" component="h2">{historyJob?.name} runs</Typography>
            <IconButton aria-label="Close" onClick={() => setHistoryJob(null)}><CloseIcon /></IconButton>
          </Box>
          {!history && <LoadingSpinner />}
          {history && (
            <>
              <Table size="small" aria-label="Run history table">
                <TableHead>
                  <TableRow>
                    <TableCell>Started</TableCell>
                    <TableCell>Outcome</TableCell>
                    <TableCell>Items</TableCell>
                    <TableCell>Trigger</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {history.items.length === 0 && <TableRow><TableCell colSpan={4}>No runs recorded yet.</TableCell></TableRow>}
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
