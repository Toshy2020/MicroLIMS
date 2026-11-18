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
import { i18n, useTranslation } from "../../i18n";

const tl = (key: string, options?: Record<string, unknown>): string => i18n.t(key, { ns: "admin", ...options });

export function EmailDeliveryCard() {
  const { t } = useTranslation("admin");
  const { t: tc } = useTranslation("common");
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
        setError(getErrorMessage(err, tl("emailDelivery.loadError")));
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
        {t("emailDelivery.title")}
      </Typography>

      {loading && (
        <Stack direction="row" spacing={1.5} sx={{ alignItems: "center", py: 1 }} role="status" aria-live="polite">
          <CircularProgress size={18} />
          <Typography variant="body2" color="text.secondary">
            {t("emailDelivery.loading")}
          </Typography>
        </Stack>
      )}

      {!loading && error && (
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => setReloadToken((n) => n + 1)}>
              {tc("actions.retry")}
            </Button>
          }
        >
          {error}
        </Alert>
      )}

      {!loading && !error && summary && (
        <>
          <Table size="small" aria-label={t("emailDelivery.summary")} sx={{ mb: 2 }}>
            <TableBody>
              <TableRow>
                <TableCell>{t("emailDelivery.waiting")}</TableCell>
                <TableCell sx={{ fontVariantNumeric: "tabular-nums" }}>{summary.waiting}</TableCell>
              </TableRow>
              <TableRow>
                <TableCell>{t("emailDelivery.failed")}</TableCell>
                <TableCell sx={{ fontVariantNumeric: "tabular-nums" }}>{summary.failed}</TableCell>
              </TableRow>
              <TableRow>
                <TableCell>{t("emailDelivery.lastSent")}</TableCell>
                <TableCell>
                  {summary.lastSentAt ? formatDateTimeWithTimeZone(summary.lastSentAt) : t("common.never")}
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>

          <Typography variant="subtitle2" sx={{ mb: 1 }}>
            {t("emailDelivery.noEmailTitle")}
          </Typography>

          {summary.usersWithoutEmail.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              {t("emailDelivery.allHaveEmail")}
            </Typography>
          ) : (
            <TableContainer>
              <Table size="small" aria-label={t("emailDelivery.noEmailTitle")}>
                <TableHead>
                  <TableRow>
                    <TableCell>{t("emailDelivery.name")}</TableCell>
                    <TableCell>{t("emailDelivery.userId")}</TableCell>
                    <TableCell>{t("common.role")}</TableCell>
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
