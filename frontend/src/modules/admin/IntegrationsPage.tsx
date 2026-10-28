import { useCallback, useEffect, useState } from "react";
import {
  Alert, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Paper, Stack, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, TextField, Typography
} from "@mui/material";
import { toast } from "sonner";
import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { formatLabDateTime } from "../../utils/formatDate";
import { getErrorMessage } from "../../utils/errorMessage";
import { TestActionDialog } from "./TestActionDialog";
import { TestSendOutcome } from "./TestSendOutcome";
import { IntegrationAgent, IntegrationsService, SmtpStatus, StorageProbeResult, StorageStatus } from "./services/IntegrationsService";

const when = (v: string | null) => (v ? formatLabDateTime(v) : "Never");

const formatBytes = (bytes: number | null) => {
  if (bytes === null) return "Not reported";
  const units = ["B", "KB", "MB", "GB", "TB"];
  let value = bytes, i = 0;
  while (value >= 1024 && i < units.length - 1) { value /= 1024; i++; }
  return `${value.toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
};

const agentColor = (status: string) => (status === "Active" ? "success" : status === "Revoked" ? "default" : "warning");

// What the system talks to: instrument agents, SMTP and file storage. Status is read-only;
// the checks (test email, storage probe) need a reason and are audited and rate limited on the server.
export function IntegrationsPage() {
  const { permissions } = useAuth();
  const canManageAgents = permissions.includes(PERMISSIONS.MASTER_DATA_MANAGE);

  const [agents, setAgents] = useState<IntegrationAgent[] | null>(null);
  const [smtp, setSmtp] = useState<SmtpStatus | null>(null);
  const [storage, setStorage] = useState<StorageStatus | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [emailOpen, setEmailOpen] = useState(false);
  const [probeOpen, setProbeOpen] = useState(false);

  const [revokeTarget, setRevokeTarget] = useState<IntegrationAgent | null>(null);
  const [revokeReason, setRevokeReason] = useState("");
  const [newKey, setNewKey] = useState<{ code: string; key: string } | null>(null);

  const load = useCallback(() => {
    Promise.all([IntegrationsService.agents(), IntegrationsService.smtp(), IntegrationsService.storage()])
      .then(([a, m, s]) => { setAgents(a); setSmtp(m); setStorage(s); })
      .catch(() => setError("Could not load the integrations. Reload the page to try again."));
  }, []);
  useEffect(load, [load]);

  const rotate = async (agent: IntegrationAgent) => {
    try {
      const result = await IntegrationsService.rotateAgentKey(agent.id);
      setNewKey({ code: agent.code, key: result.plainKey });
      load();
    } catch (e) {
      toast.error(getErrorMessage(e, "Could not rotate the key."));
    }
  };

  const revoke = async () => {
    if (!revokeTarget || !revokeReason.trim()) return;
    try {
      await IntegrationsService.revokeAgent(revokeTarget.id, revokeReason.trim());
      toast.success(`${revokeTarget.code} revoked.`);
      setRevokeTarget(null); setRevokeReason("");
      load();
    } catch (e) {
      toast.error(getErrorMessage(e, "Could not revoke the agent."));
    }
  };

  const probeOutcome = (r: StorageProbeResult) => (
    <Stack spacing={0.5} aria-label="Probe result">
      <Alert severity={r.succeeded ? "success" : "error"}>
        {r.succeeded ? "Write, read and delete all succeeded." : "The storage probe failed."}
      </Alert>
      {r.steps.map((s) => (
        <Typography key={s.step} variant="body2">
          {s.step}: {s.succeeded ? "ok" : `failed - ${s.error}`} ({s.elapsedMs} ms)
        </Typography>
      ))}
      {r.warning && <Alert severity="warning">{r.warning}</Alert>}
    </Stack>
  );

  return (
    <>
      <PageHeader title="Integrations" subtitle="Instrument agents, email and file storage. Secrets are never shown." />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {!error && (!agents || !smtp || !storage) && <LoadingSpinner />}

      {agents && (
        <Paper sx={{ mb: 2, p: 2 }}>
          <Typography variant="h6" component="h2" sx={{ mb: 1 }}>Instrument agents</Typography>
          {agents.length === 0 ? (
            <Typography variant="body2">No instrument agents are registered.</Typography>
          ) : (
            <TableContainer>
              <Table size="small" aria-label="Instrument agents">
                <TableHead>
                  <TableRow>
                    <TableCell>Agent</TableCell>
                    <TableCell>Equipment</TableCell>
                    <TableCell>Status</TableCell>
                    <TableCell>Last seen</TableCell>
                    <TableCell>Key</TableCell>
                    {canManageAgents && <TableCell align="right">Actions</TableCell>}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {agents.map((a) => (
                    <TableRow key={a.id}>
                      <TableCell>
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>{a.code}</Typography>
                        <Typography variant="caption" color="text.secondary">{a.name}</Typography>
                      </TableCell>
                      <TableCell>{a.equipmentCode ?? `#${a.equipmentId}`}</TableCell>
                      <TableCell>
                        <Chip size="small" color={agentColor(a.status)} label={a.status} />
                        {a.revokeReason && (
                          <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                            {a.revokeReason}
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell>{when(a.lastSeenAt)}</TableCell>
                      <TableCell>{a.keyPrefix}...</TableCell>
                      {canManageAgents && (
                        <TableCell align="right">
                          {a.status !== "Revoked" && (
                            <>
                              <Button size="small" onClick={() => rotate(a)} aria-label={`Rotate key of ${a.code}`}>Rotate key</Button>
                              <Button size="small" color="error" onClick={() => { setRevokeTarget(a); setRevokeReason(""); }} aria-label={`Revoke ${a.code}`}>Revoke</Button>
                            </>
                          )}
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
          {!canManageAgents && (
            <Typography variant="caption" color="text.secondary">
              Rotating or revoking a key needs the Master Data management permission.
            </Typography>
          )}
        </Paper>
      )}

      {smtp && (
        <Paper sx={{ mb: 2, p: 2 }}>
          <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography variant="h6" component="h2">Email (SMTP)</Typography>
            <Button variant="outlined" size="small" onClick={() => setEmailOpen(true)}>Send test email</Button>
          </Stack>
          {!smtp.isConfigured && <Alert severity="warning" sx={{ mb: 1 }}>SMTP is not configured: no email is sent.</Alert>}
          <Table size="small" aria-label="SMTP configuration">
            <TableBody>
              <TableRow><TableCell>Host</TableCell><TableCell>{smtp.host || "Not set"}</TableCell></TableRow>
              <TableRow><TableCell>Port</TableCell><TableCell>{smtp.port}</TableCell></TableRow>
              <TableRow><TableCell>From</TableCell><TableCell>{smtp.fromAddress}</TableCell></TableRow>
              <TableRow><TableCell>TLS</TableCell><TableCell>{smtp.enableSsl ? "On" : "Off"}</TableCell></TableRow>
              <TableRow>
                <TableCell>Password</TableCell>
                <TableCell>{smtp.authenticationConfigured ? smtp.password : "Not set"}</TableCell>
              </TableRow>
              <TableRow>
                <TableCell>Last send (since the last restart)</TableCell>
                <TableCell>
                  {smtp.lastSendOutcome ? `${smtp.lastSendOutcome} at ${when(smtp.lastSendAtUtc)}` : "No email sent yet"}
                  {smtp.lastSendError && (
                    <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>{smtp.lastSendError}</Typography>
                  )}
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </Paper>
      )}

      {storage && (
        <Paper sx={{ mb: 2, p: 2 }}>
          <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography variant="h6" component="h2">File storage</Typography>
            <Button variant="outlined" size="small" onClick={() => setProbeOpen(true)}>Run storage probe</Button>
          </Stack>
          <Table size="small" aria-label="Storage configuration">
            <TableBody>
              <TableRow><TableCell>Provider</TableCell><TableCell>{storage.provider}</TableCell></TableRow>
              <TableRow><TableCell>Location</TableCell><TableCell>{storage.location}</TableCell></TableRow>
              {storage.provider === "Local" && (
                <TableRow>
                  <TableCell>Free space</TableCell>
                  <TableCell>{formatBytes(storage.freeBytes)} of {formatBytes(storage.totalBytes)}</TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </Paper>
      )}

      <TestActionDialog
        open={emailOpen}
        title="Send test email"
        description="Sends a clearly labelled test message through the same sender the system uses."
        confirmLabel="Send test email"
        addressLabel="Send to"
        onClose={() => { setEmailOpen(false); load(); }}
        onSubmit={async (reason, address) => <TestSendOutcome result={await IntegrationsService.sendTestEmail(address, reason)} />}
      />
      <TestActionDialog
        open={probeOpen}
        title="Run storage probe"
        description="Writes, reads back and deletes a small temporary object."
        confirmLabel="Run probe"
        onClose={() => setProbeOpen(false)}
        onSubmit={async (reason) => probeOutcome(await IntegrationsService.probeStorage(reason))}
      />

      <Dialog open={Boolean(revokeTarget)} onClose={() => setRevokeTarget(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Revoke {revokeTarget?.code}</DialogTitle>
        <DialogContent>
          <Typography variant="body2" sx={{ mb: 2 }}>The agent can no longer send data. This cannot be undone; register a new agent to replace it.</Typography>
          <TextField label="Reason" size="small" fullWidth required multiline minRows={2} value={revokeReason} onChange={(e) => setRevokeReason(e.target.value)} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRevokeTarget(null)}>Cancel</Button>
          <Button color="error" variant="contained" disabled={!revokeReason.trim()} onClick={revoke}>Revoke</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={Boolean(newKey)} onClose={() => setNewKey(null)} maxWidth="sm" fullWidth>
        <DialogTitle>New key for {newKey?.code}</DialogTitle>
        <DialogContent>
          <Alert severity="warning" sx={{ mb: 2 }}>Copy this key now. It is shown once and cannot be retrieved later.</Alert>
          <TextField fullWidth size="small" value={newKey?.key ?? ""} slotProps={{ htmlInput: { readOnly: true, "aria-label": "New agent key" } }} />
        </DialogContent>
        <DialogActions><Button onClick={() => setNewKey(null)}>Done</Button></DialogActions>
      </Dialog>
    </>
  );
}
