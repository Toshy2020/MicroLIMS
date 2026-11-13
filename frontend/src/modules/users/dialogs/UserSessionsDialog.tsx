import { useCallback, useEffect, useRef, useState } from "react";
import {
  Alert, Box, Button, CircularProgress, Divider, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Tooltip, Typography
} from "@mui/material";
import { toast } from "sonner";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { EmptyState } from "../../../components/lab/EmptyState";
import { PartialOutageBanner } from "../../../components/lab/PartialOutageBanner";
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

const fmt = (iso: string | null) => (iso ? new Date(iso).toLocaleString() : "-");

// Active sign-in sessions (refresh tokens) and trusted computers of one user.
export function UserSessionsDialog({ open, onClose, user, onRevoked }: Props) {
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
      setError(serverMessage(err) ?? getErrorMessage(err, "Failed to load sessions."));
    } finally {
      setLoading(false);
    }
  }, []);

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
      onRevoked(`Revoked ${revokedCount} session(s) for ${user.username}.`);
      setReason("");
      await loadSessions(user.id);
    } catch (err) {
      setError(serverMessage(err) ?? getErrorMessage(err, "Failed to revoke sessions."));
    } finally {
      setRevoking(false);
    }
  };

  const handleConfirmRevokeDevice = async () => {
    if (!user || !revokeTarget || revokingDevice) return;
    setRevokingDevice(true);
    try {
      await UserService.revokeTrustedDevice(user.id, revokeTarget.id);
      toast.success("Trusted computer revoked.");
      setRevokeTarget(null);
      await loadTrustedDevices(user.id);
    } catch (err) {
      toast.error(serverMessage(err) ?? getErrorMessage(err, "Could not revoke trusted computer."));
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
        title={`Sessions: ${user?.username ?? ""}`}
        actions={
          <>
            <Button onClick={onClose}>Close</Button>
            <Button
              variant="contained"
              color="error"
              onClick={handleRevoke}
              disabled={!reason.trim() || revoking || sessions.length === 0}
            >
              Revoke all sessions
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && <Alert severity="error">{error}</Alert>}
          {loading ? (
            <CircularProgress size={24} />
          ) : sessions.length === 0 ? (
            <Typography variant="body2">No active sessions.</Typography>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Signed in</TableCell>
                  <TableCell>IP address</TableCell>
                  <TableCell>Device</TableCell>
                  <TableCell>Second factor</TableCell>
                  <TableCell>Last used</TableCell>
                  <TableCell>Expires</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {sessions.map((s) => (
                  <TableRow key={s.id}>
                    <TableCell>{fmt(s.createdAt)}</TableCell>
                    <TableCell>{s.createdIp ?? "-"}</TableCell>
                    <TableCell sx={{ maxWidth: 260, wordBreak: "break-word" }}>{s.userAgent ?? "-"}</TableCell>
                    <TableCell>{s.mfaVerified ? "Second factor: Yes" : "Second factor: No"}</TableCell>
                    <TableCell>{fmt(s.lastUsedAt)}</TableCell>
                    <TableCell>{fmt(s.expiresAt)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
          <TextField
            label="Reason (required to revoke)"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            size="small"
            fullWidth
            multiline
          />
          <Typography variant="caption" color="text.secondary">
            Revoking ends refresh immediately. An access token already issued remains valid until it expires
            (see Security Policy: access token lifetime).
          </Typography>

          <Divider />

          <Box>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>
              Trusted computers
            </Typography>
            {devicesLoading ? (
              <CircularProgress size={24} />
            ) : devicesFailed ? (
              <PartialOutageBanner
                sources={["trusted computers"]}
                onRetry={user ? () => void loadTrustedDevices(user.id) : undefined}
              />
            ) : trustedDevices.length === 0 ? (
              <EmptyState
                title="No trusted computers"
                description="Computers this user chooses to trust during sign-in will appear here."
              />
            ) : (
              <Stack spacing={1.25}>
                {trustedDevices.map((device) => {
                  const browserLabel = describeUserAgent(device.userAgent);
                  const ipLabel = device.createdIp || "Unknown IP";
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
                        <Tooltip title={device.userAgent || "Unknown browser"}>
                          <Typography variant="body2" sx={{ fontWeight: 600 }} noWrap>
                            {browserLabel}
                          </Typography>
                        </Tooltip>
                        <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                          IP: {ipLabel}
                        </Typography>
                        <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                          Trusted: {formatDateTimeWithTimeZone(device.createdAt)} · Expires:{" "}
                          {formatDateTimeWithTimeZone(device.expiresAt)}
                        </Typography>
                      </Box>
                      <Button
                        variant="outlined"
                        size="small"
                        color="error"
                        aria-label={`Revoke ${browserLabel} (${ipLabel})`}
                        onClick={() => setRevokeTarget(device)}
                      >
                        Revoke
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
        title="Revoke trusted computer"
        confirmText="Revoke"
        destructive
        loading={revokingDevice}
        message={
          revokeTarget
            ? `Next time ${user?.username ?? "this user"} signs in from ${describeUserAgent(revokeTarget.userAgent)} (${revokeTarget.createdIp || "Unknown IP"}), a 6-digit code will be required.`
            : ""
        }
        onCancel={() => setRevokeTarget(null)}
        onConfirm={handleConfirmRevokeDevice}
      />
    </>
  );
}
