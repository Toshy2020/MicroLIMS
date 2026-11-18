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
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import { getErrorMessage } from "../../utils/errorMessage";
import { TestActionDialog } from "./TestActionDialog";
import { TestSendOutcome } from "./TestSendOutcome";
import { EmailDeliveryCard } from "./EmailDeliveryCard";
import { i18n, useTranslation } from "../../i18n";
import { IntegrationAgent, IntegrationsService, SmtpStatus, StorageProbeResult, StorageStatus } from "./services/IntegrationsService";

const tl = (key: string, options?: Record<string, unknown>): string => i18n.t(key, { ns: "admin", ...options });

const formatBytes = (bytes: number | null, notReported: string) => {
  if (bytes === null) return notReported;
  const units = ["B", "KB", "MB", "GB", "TB"];
  let value = bytes, i = 0;
  while (value >= 1024 && i < units.length - 1) { value /= 1024; i++; }
  return `${value.toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
};

const agentColor = (status: string) => (status === "Active" ? "success" : status === "Revoked" ? "default" : "warning");

// What the system talks to: instrument agents, SMTP and file storage. Status is read-only;
// the checks (test email, storage probe) need a reason and are audited and rate limited on the server.
export function IntegrationsPage() {
  const { t } = useTranslation("admin");
  const { t: tc } = useTranslation("common");
  const when = (v: string | null) => (v ? formatDateTimeWithTimeZone(v) : t("common.never"));
  const { permissions } = useAuth();
  const canManageAgents = permissions.includes(PERMISSIONS.MASTER_DATA_MANAGE);
  const canManageUsers = permissions.includes(PERMISSIONS.USERS_MANAGE);

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
      .catch(() => setError(tl("integrations.loadError")));
  }, []);
  useEffect(load, [load]);

  const rotate = async (agent: IntegrationAgent) => {
    try {
      const result = await IntegrationsService.rotateAgentKey(agent.id);
      setNewKey({ code: agent.code, key: result.plainKey });
      load();
    } catch (e) {
      toast.error(getErrorMessage(e, t("integrations.rotateError")));
    }
  };

  const revoke = async () => {
    if (!revokeTarget || !revokeReason.trim()) return;
    try {
      await IntegrationsService.revokeAgent(revokeTarget.id, revokeReason.trim());
      toast.success(t("integrations.revoked", { code: revokeTarget.code }));
      setRevokeTarget(null); setRevokeReason("");
      load();
    } catch (e) {
      toast.error(getErrorMessage(e, t("integrations.revokeError")));
    }
  };

  const probeOutcome = (r: StorageProbeResult) => (
    <Stack spacing={0.5} aria-label={t("integrations.probeRegion")}>
      <Alert severity={r.succeeded ? "success" : "error"}>
        {r.succeeded ? t("integrations.probeOk") : t("integrations.probeFailed")}
      </Alert>
      {r.steps.map((s) => (
        <Typography key={s.step} variant="body2">
          {s.step}: {s.succeeded ? t("integrations.stepOk") : t("integrations.stepFailed", { error: s.error })} ({s.elapsedMs} ms)
        </Typography>
      ))}
      {r.warning && <Alert severity="warning">{r.warning}</Alert>}
    </Stack>
  );

  return (
    <>
      <PageHeader title={t("integrations.title")} subtitle={t("integrations.subtitle")} />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {!error && (!agents || !smtp || !storage) && <LoadingSpinner />}

      {agents && (
        <Paper sx={{ mb: 2, p: 2 }}>
          <Typography variant="h6" component="h2" sx={{ mb: 1 }}>{t("integrations.agents")}</Typography>
          {agents.length === 0 ? (
            <Typography variant="body2">{t("integrations.noAgents")}</Typography>
          ) : (
            <TableContainer>
              <Table size="small" aria-label={t("integrations.agents")}>
                <TableHead>
                  <TableRow>
                    <TableCell>{t("integrations.agent")}</TableCell>
                    <TableCell>{t("integrations.equipment")}</TableCell>
                    <TableCell>{t("common.status")}</TableCell>
                    <TableCell>{t("integrations.lastSeen")}</TableCell>
                    <TableCell>{t("integrations.key")}</TableCell>
                    {canManageAgents && <TableCell align="right">{t("common.actions")}</TableCell>}
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
                              <Button size="small" onClick={() => rotate(a)} aria-label={t("integrations.rotateKeyOf", { code: a.code })}>{t("integrations.rotateKey")}</Button>
                              <Button size="small" color="error" onClick={() => { setRevokeTarget(a); setRevokeReason(""); }} aria-label={t("integrations.revokeOf", { code: a.code })}>{t("integrations.revoke")}</Button>
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
              {t("integrations.needsMasterData")}
            </Typography>
          )}
        </Paper>
      )}

      {smtp && (
        <Paper sx={{ mb: 2, p: 2 }}>
          <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography variant="h6" component="h2">{t("integrations.smtp")}</Typography>
            <Button variant="outlined" size="small" onClick={() => setEmailOpen(true)}>{t("integrations.sendTestEmail")}</Button>
          </Stack>
          {!smtp.isConfigured && <Alert severity="warning" sx={{ mb: 1 }}>{t("integrations.smtpNotConfigured")}</Alert>}
          <Table size="small" aria-label={t("integrations.smtpConfig")}>
            <TableBody>
              <TableRow><TableCell>{t("integrations.host")}</TableCell><TableCell>{smtp.host || t("integrations.notSet")}</TableCell></TableRow>
              <TableRow><TableCell>{t("integrations.port")}</TableCell><TableCell>{smtp.port}</TableCell></TableRow>
              <TableRow><TableCell>{t("integrations.from")}</TableCell><TableCell>{smtp.fromAddress}</TableCell></TableRow>
              <TableRow><TableCell>TLS</TableCell><TableCell>{smtp.enableSsl ? t("integrations.on") : t("integrations.off")}</TableCell></TableRow>
              <TableRow>
                <TableCell>{t("integrations.password")}</TableCell>
                <TableCell>{smtp.authenticationConfigured ? smtp.password : t("integrations.notSet")}</TableCell>
              </TableRow>
              <TableRow>
                <TableCell>{t("integrations.lastSend")}</TableCell>
                <TableCell>
                  {smtp.lastSendOutcome ? t("integrations.lastSendLine", { outcome: smtp.lastSendOutcome, time: when(smtp.lastSendAtUtc) }) : t("integrations.noEmailSent")}
                  {smtp.lastSendError && (
                    <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>{smtp.lastSendError}</Typography>
                  )}
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </Paper>
      )}

      {canManageUsers && <EmailDeliveryCard />}

      {storage && (
        <Paper sx={{ mb: 2, p: 2 }}>
          <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography variant="h6" component="h2">{t("integrations.fileStorage")}</Typography>
            <Button variant="outlined" size="small" onClick={() => setProbeOpen(true)}>{t("integrations.runProbe")}</Button>
          </Stack>
          <Table size="small" aria-label={t("integrations.storageConfig")}>
            <TableBody>
              <TableRow><TableCell>{t("integrations.provider")}</TableCell><TableCell>{storage.provider}</TableCell></TableRow>
              <TableRow><TableCell>{t("integrations.location")}</TableCell><TableCell>{storage.location}</TableCell></TableRow>
              {storage.provider === "Local" && (
                <TableRow>
                  <TableCell>{t("integrations.freeSpace")}</TableCell>
                  <TableCell>{t("integrations.freeOf", { free: formatBytes(storage.freeBytes, t("integrations.notReported")), total: formatBytes(storage.totalBytes, t("integrations.notReported")) })}</TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </Paper>
      )}

      <TestActionDialog
        open={emailOpen}
        title={t("integrations.sendTestEmail")}
        description={t("integrations.testEmailDescription")}
        confirmLabel={t("integrations.sendTestEmail")}
        addressLabel={t("integrations.sendTo")}
        onClose={() => { setEmailOpen(false); load(); }}
        onSubmit={async (reason, address) => <TestSendOutcome result={await IntegrationsService.sendTestEmail(address, reason)} />}
      />
      <TestActionDialog
        open={probeOpen}
        title={t("integrations.runProbe")}
        description={t("integrations.probeDescription")}
        confirmLabel={t("integrations.runProbeConfirm")}
        onClose={() => setProbeOpen(false)}
        onSubmit={async (reason) => probeOutcome(await IntegrationsService.probeStorage(reason))}
      />

      <Dialog open={Boolean(revokeTarget)} onClose={() => setRevokeTarget(null)} maxWidth="xs" fullWidth>
        <DialogTitle>{t("integrations.revokeTitle", { code: revokeTarget?.code ?? "" })}</DialogTitle>
        <DialogContent>
          <Typography variant="body2" sx={{ mb: 2 }}>{t("integrations.revokeIntro")}</Typography>
          <TextField label={t("common.reason")} size="small" fullWidth required multiline minRows={2} value={revokeReason} onChange={(e) => setRevokeReason(e.target.value)} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRevokeTarget(null)}>{tc("actions.cancel")}</Button>
          <Button color="error" variant="contained" disabled={!revokeReason.trim()} onClick={revoke}>{t("integrations.revoke")}</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={Boolean(newKey)} onClose={() => setNewKey(null)} maxWidth="sm" fullWidth>
        <DialogTitle>{t("integrations.newKeyTitle", { code: newKey?.code ?? "" })}</DialogTitle>
        <DialogContent>
          <Alert severity="warning" sx={{ mb: 2 }}>{t("integrations.copyNow")}</Alert>
          <TextField fullWidth size="small" value={newKey?.key ?? ""} slotProps={{ htmlInput: { readOnly: true, "aria-label": t("integrations.newKeyAria") } }} />
        </DialogContent>
        <DialogActions><Button onClick={() => setNewKey(null)}>{t("common.done")}</Button></DialogActions>
      </Dialog>
    </>
  );
}
