import { useCallback, useEffect, useId, useState } from "react";
import {
  Alert,
  Button,
  CircularProgress,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography
} from "@mui/material";
import { getErrorMessage } from "../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import {
  type EmailOutboxSummaryDto,
  notificationService
} from "../notifications/services/notificationService";

export function EmailDeliveryCard() {
  const headingId = useId();
  const [summary, setSummary] = useState<EmailOutboxSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  const load = useCallback((signal: AbortSignal) => {
    setLoading(true);
    setError(null);
    notificationService
      .getEmailOutboxSummary(signal)
      .then((data) => {
        if (signal.aborted) return;
        setSummary(data);
        setLoading(false);
      })
      .catch((err: unknown) => {
        if (signal.aborted) return;
        setError(getErrorMessage(err, "Could not load email delivery summary."));
        setLoading(false);
      });
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal);
    return () => controller.abort();
  }, [load, reloadToken]);

  return (
    <Paper component="section" aria-labelledby={headingId} sx={{ mb: 2, p: 2 }}>
      <Typography id={headingId} variant="h6" component="h2" sx={{ mb: 1 }}>
        Email delivery
      </Typography>

      {loading && (
        <Stack direction="row" spacing={1.5} sx={{ alignItems: "center", py: 1 }} role="status" aria-live="polite">
          <CircularProgress size={18} />
          <Typography variant="body2" color="text.secondary">
            Loading email delivery status...
          </Typography>
        </Stack>
      )}

      {!loading && error && (
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => setReloadToken((n) => n + 1)}>
              Retry
            </Button>
          }
        >
          {error}
        </Alert>
      )}

      {!loading && !error && summary && (
        <>
          <Table size="small" aria-label="Email delivery summary" sx={{ mb: 2 }}>
            <TableBody>
              <TableRow>
                <TableCell>Waiting</TableCell>
                <TableCell sx={{ fontVariantNumeric: "tabular-nums" }}>{summary.waiting}</TableCell>
              </TableRow>
              <TableRow>
                <TableCell>Failed</TableCell>
                <TableCell sx={{ fontVariantNumeric: "tabular-nums" }}>{summary.failed}</TableCell>
              </TableRow>
              <TableRow>
                <TableCell>Last sent</TableCell>
                <TableCell>
                  {summary.lastSentAt ? formatDateTimeWithTimeZone(summary.lastSentAt) : "Never"}
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>

          <Typography variant="subtitle2" sx={{ mb: 1 }}>
            Users without an email address
          </Typography>

          {summary.usersWithoutEmail.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              All active users have an email address.
            </Typography>
          ) : (
            <TableContainer>
              <Table size="small" aria-label="Users without an email address">
                <TableHead>
                  <TableRow>
                    <TableCell>Name</TableCell>
                    <TableCell>User ID</TableCell>
                    <TableCell>Role</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {summary.usersWithoutEmail.map((u) => (
                    <TableRow key={u.id}>
                      <TableCell>{u.fullName}</TableCell>
                      <TableCell>{u.username}</TableCell>
                      <TableCell>{u.roleName}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </>
      )}
    </Paper>
  );
}
