import { useCallback, useEffect, useRef, useState } from "react";
import {
  Alert, Box, Button, CircularProgress, Divider, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Tooltip, Typography
} from "@mui/material";
import { Trans } from "react-i18next";
import { toast } from "sonner";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { Ltr } from "../../../components/Ltr";
import { EmptyState } from "../../../components/lab/EmptyState";
import { PartialOutageBanner } from "../../../components/lab/PartialOutageBanner";
import { useTranslation } from "../../../i18n";
import { describeUserAgent } from "../../../utils/describeUserAgent";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { TrustedDeviceInfo, UserRecord, UserService, UserSession } from "../services/UserService";

interface Props {
  open: boolean;
  onClose: () => void;
  user: UserRecord | null;
  onRevoked: (message: string) => void;
}

const fmt = (iso: string | null) => (iso ? formatDateTimeWithTimeZone(iso) : "-");

// Active sign-in sessions (refresh tokens) and trusted computers of one user.
export function UserSessionsDialog({ open, onClose, user, onRevoked }: Props) {
  const { t } = useTranslation("users");
  const { t: tc } = useTranslation("common");
  const [sessions, setSessions] = useState<UserSession[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [revoking, setRevoking] = useState(false);

  const [trustedDevices, setTrustedDevices] = useState<TrustedDeviceInfo[]>([]);
  const [devicesLoading, setDevicesLoading] = useState(false);
  const [devicesFailed, setDevicesFailed] = useState(false);
  const [revokeTarget, setRevokeTarget] = useState<TrustedDeviceInfo | null>(null);
  const [revokingDevice, setRevokingDevice] = useState(false);
  const devicesAbortRef = useRef<AbortController | null>(null);

  const loadSessions = useCallback(async (id: number) => {
    setLoading(true);
    setError(null);
    try {
      setSessions(await UserService.getSessions(id));
    } catch (err) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("sessions.loadError")));
    } finally {
      setLoading(false);
    }
  }, [t]);

  const loadTrustedDevices = useCallback(async (id: number) => {
    devicesAbortRef.current?.abort();
    const controller = new AbortController();
    devicesAbortRef.current = controller;
    setDevicesLoading(true);
    setDevicesFailed(false);
    try {
      const devices = await UserService.getTrustedDevices(id, controller.signal);
      if (!controller.signal.aborted) {
        setTrustedDevices(devices);
      }
    } catch {
      if (!controller.signal.aborted) {
        setDevicesFailed(true);
      }
    } finally {
      if (!controller.signal.aborted) {
        setDevicesLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    setReason("");
    setSessions([]);
    setTrustedDevices([]);
    setError(null);
    setDevicesFailed(false);
    setRevokeTarget(null);
    if (open && user) {
      void loadSessions(user.id);
      void loadTrustedDevices(user.id);
    }
    return () => {
      devicesAbortRef.current?.abort();
    };
  }, [open, user, loadSessions, loadTrustedDevices]);

  const handleRevoke = async () => {
    if (!user || !reason.trim()) return;
    setRevoking(true);
    setError(null);
    try {
      const { revokedCount } = await UserService.revokeSessions(user.id, reason.trim());
      onRevoked(t("sessions.revokedSuccess", { count: revokedCount, username: user.username }));
      setReason("");
      await loadSessions(user.id);
    } catch (err) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("sessions.revokeError")));
    } finally {
      setRevoking(false);
    }
  };

  const handleConfirmRevokeDevice = async () => {
    if (!user || !revokeTarget || revokingDevice) return;
    setRevokingDevice(true);
    try {
      await UserService.revokeTrustedDevice(user.id, revokeTarget.id);
      toast.success(t("sessions.revokeDeviceSuccess"));
      setRevokeTarget(null);
      await loadTrustedDevices(user.id);
    } catch (err) {
      toast.error(serverMessage(err) ?? getErrorMessage(err, t("sessions.revokeDeviceError")));
    } finally {
      setRevokingDevice(false);
    }
  };

  return (
    <>
      <FloatingDialog
        open={open}
        onClose={onClose}
        maxWidth="md"
        title={t("sessions.title", { username: user?.username ?? "" })}
        actions={
          <>
            <Button onClick={onClose}>{tc("actions.close")}</Button>
            <Button
              variant="contained"
              color="error"
              onClick={handleRevoke}
              disabled={!reason.trim() || revoking || sessions.length === 0}
            >
              {t("sessions.revokeAll")}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && <Alert severity="error">{error}</Alert>}
          {loading ? (
            <CircularProgress size={24} />
          ) : sessions.length === 0 ? (
            <Typography variant="body2">{t("sessions.noActiveSessions")}</Typography>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>{t("sessions.columns.signedIn")}</TableCell>
                  <TableCell>{t("sessions.columns.ipAddress")}</TableCell>
                  <TableCell>{t("sessions.columns.device")}</TableCell>
                  <TableCell>{t("sessions.columns.secondFactor")}</TableCell>
                  <TableCell>{t("sessions.columns.lastUsed")}</TableCell>
                  <TableCell>{t("sessions.columns.expires")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {sessions.map((s) => (
                  <TableRow key={s.id}>
                    <TableCell><Ltr>{fmt(s.createdAt)}</Ltr></TableCell>
                    <TableCell><Ltr>{s.createdIp ?? "-"}</Ltr></TableCell>
                    <TableCell sx={{ maxWidth: 260, wordBreak: "break-word" }}>{s.userAgent ?? "-"}</TableCell>
                    <TableCell>{s.mfaVerified ? t("sessions.mfaYes") : t("sessions.mfaNo")}</TableCell>
                    <TableCell><Ltr>{fmt(s.lastUsedAt)}</Ltr></TableCell>
                    <TableCell><Ltr>{fmt(s.expiresAt)}</Ltr></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
          <TextField
            label={t("sessions.reasonLabel")}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            size="small"
            fullWidth
            multiline
          />
          <Typography variant="caption" color="text.secondary">
            {t("sessions.revokeNote")}
          </Typography>

          <Divider />

          <Box>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>
              {t("sessions.trustedComputersTitle")}
            </Typography>
            {devicesLoading ? (
              <CircularProgress size={24} />
            ) : devicesFailed ? (
              <PartialOutageBanner
                sources={[t("sessions.trustedComputersSource")]}
                onRetry={user ? () => void loadTrustedDevices(user.id) : undefined}
              />
            ) : trustedDevices.length === 0 ? (
              <EmptyState
                title={t("sessions.noTrustedTitle")}
                description={t("sessions.noTrustedDescription")}
              />
            ) : (
              <Stack spacing={1.25}>
                {trustedDevices.map((device) => {
                  const browserLabel = describeUserAgent(device.userAgent);
                  const ipLabel = device.createdIp || t("sessions.unknownIp");
                  return (
                    <Box
                      key={device.id}
                      sx={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        gap: 2,
                        p: 1.5,
                        borderRadius: 1,
                        border: 1,
                        borderColor: "divider"
                      }}
                    >
                      <Box sx={{ minWidth: 0 }}>
                        <Tooltip title={device.userAgent || t("sessions.unknownBrowser")}>
                          <Typography variant="body2" sx={{ fontWeight: 600 }} noWrap>
                            {browserLabel}
                          </Typography>
                        </Tooltip>
                        <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                          {t("sessions.ipPrefix", { ip: ipLabel })}
                        </Typography>
                        <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                          <Trans
                            i18nKey="sessions.trustedExpiry"
                            ns="users"
                            values={{
                              trusted: formatDateTimeWithTimeZone(device.createdAt),
                              expires: formatDateTimeWithTimeZone(device.expiresAt)
                            }}
                            components={{ trusted: <Ltr>{null}</Ltr>, expires: <Ltr>{null}</Ltr> }}
                          />
                        </Typography>
                      </Box>
                      <Button
                        variant="outlined"
                        size="small"
                        color="error"
                        aria-label={t("sessions.revokeAria", { browser: browserLabel, ip: ipLabel })}
                        onClick={() => setRevokeTarget(device)}
                      >
                        {t("sessions.revoke")}
                      </Button>
                    </Box>
                  );
                })}
              </Stack>
            )}
          </Box>
        </Stack>
      </FloatingDialog>

      <ConfirmationDialog
        open={Boolean(revokeTarget)}
        title={t("sessions.revokeDeviceTitle")}
        confirmText={t("sessions.revoke")}
        destructive
        loading={revokingDevice}
        message={
          revokeTarget
            ? t("sessions.revokeDeviceMessage", {
                username: user?.username ?? t("sessions.thisUser"),
                browser: describeUserAgent(revokeTarget.userAgent),
                ip: revokeTarget.createdIp || t("sessions.unknownIp")
              })
            : ""
        }
        onCancel={() => setRevokeTarget(null)}
        onConfirm={handleConfirmRevokeDevice}
      />
    </>
  );
}
