import { useCallback, useEffect, useState } from "react";
import {
  Alert, Button, CircularProgress, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography
} from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { UserRecord, UserService, UserSession } from "../services/UserService";

interface Props {
  open: boolean;
  onClose: () => void;
  user: UserRecord | null;
  onRevoked: (message: string) => void;
}

const fmt = (iso: string | null) => (iso ? new Date(iso).toLocaleString() : "-");

function errorMessage(err: unknown, fallback: string): string {
  const msg = (err as { response?: { data?: { message?: unknown } } })?.response?.data?.message;
  return typeof msg === "string" && msg.trim() ? msg : fallback;
}

// Active sign-in sessions (refresh tokens) of one user. Revoking ends every
// session; an access token already issued keeps working until it expires.
export function UserSessionsDialog({ open, onClose, user, onRevoked }: Props) {
  const [sessions, setSessions] = useState<UserSession[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [revoking, setRevoking] = useState(false);

  const load = useCallback(async (id: number) => {
    setLoading(true);
    setError(null);
    try {
      setSessions(await UserService.getSessions(id));
    } catch (err) {
      setError(errorMessage(err, "Failed to load sessions."));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    setReason("");
    setSessions([]);
    if (open && user) void load(user.id);
  }, [open, user, load]);

  const handleRevoke = async () => {
    if (!user || !reason.trim()) return;
    setRevoking(true);
    setError(null);
    try {
      const { revokedCount } = await UserService.revokeSessions(user.id, reason.trim());
      onRevoked(`Revoked ${revokedCount} session(s) for ${user.username}.`);
      setReason("");
      await load(user.id);
    } catch (err) {
      setError(errorMessage(err, "Failed to revoke sessions."));
    } finally {
      setRevoking(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
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
      </Stack>
    </FloatingDialog>
  );
}
