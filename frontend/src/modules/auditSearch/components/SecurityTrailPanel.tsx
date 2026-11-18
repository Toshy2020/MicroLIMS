import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography
} from "@mui/material";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { tableHeadSx } from "../../../theme";
import { AuditSearchService } from "../services/AuditSearchService";
import type { LoginHistoryItem, SecurityEventItem } from "../types/auditTypes";
import { i18n, useTranslation } from "../../../i18n";

const tl = (key: string): string => i18n.t(key, { ns: "auditSearch" });

interface Props {
  kind: "security-events" | "login-history";
}

const PAGE = 100;

// Security Events and Login History. Both need System.ViewSecurityAudit on the server;
// the parent only shows the tabs to holders of it, and a 403 here is reported, not hidden.
export function SecurityTrailPanel({ kind }: Props) {
  const { t } = useTranslation("auditSearch");
  const [events, setEvents] = useState<SecurityEventItem[]>([]);
  const [logins, setLogins] = useState<LoginHistoryItem[]>([]);
  const [cursor, setCursor] = useState<number | null>(null);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(
    async (before: number | null, append: boolean) => {
      setLoading(true);
      setError(null);
      try {
        if (kind === "security-events") {
          const res = await AuditSearchService.getSecurityEvents({ take: PAGE, beforeId: before });
          setEvents((prev) => (append ? [...prev, ...res.items] : res.items));
          setCursor(res.nextCursor);
          setHasMore(res.hasMore);
        } else {
          const res = await AuditSearchService.getLoginHistory({ take: PAGE, beforeId: before });
          setLogins((prev) => (append ? [...prev, ...res.items] : res.items));
          setCursor(res.nextCursor);
          setHasMore(res.hasMore);
        }
      } catch {
        setError(tl("security.loadError"));
      } finally {
        setLoading(false);
      }
    },
    [kind]
  );

  useEffect(() => {
    setEvents([]);
    setLogins([]);
    void load(null, false);
  }, [load]);

  const headCell = { fontWeight: 700, fontSize: 12 } as const;
  const empty = !loading && (kind === "security-events" ? events.length === 0 : logins.length === 0);

  return (
    <Box>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Paper sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflow: "hidden" }}>
        <TableContainer sx={{ maxHeight: { md: "calc(100vh - 260px)" } }}>
          {kind === "security-events" ? (
            <Table stickyHeader size="small">
              <TableHead sx={tableHeadSx}>
                <TableRow>
                  <TableCell sx={headCell}>{t("results.dateTime")}</TableCell>
                  <TableCell sx={headCell}>{t("security.event")}</TableCell>
                  <TableCell sx={headCell}>{t("security.outcome")}</TableCell>
                  <TableCell sx={headCell}>{t("security.actor")}</TableCell>
                  <TableCell sx={headCell}>{t("security.target")}</TableCell>
                  <TableCell sx={headCell}>{t("security.ip")}</TableCell>
                  <TableCell sx={headCell}>{t("security.reason")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {events.map((e) => (
                  <TableRow key={e.id} hover>
                    <TableCell sx={{ fontSize: 12, whiteSpace: "nowrap" }}>{formatDateTimeWithTimeZone(e.occurredAtUtc)}</TableCell>
                    <TableCell sx={{ fontSize: 12, fontFamily: "monospace" }}>{e.eventCode}</TableCell>
                    <TableCell>
                      <Chip
                        size="small"
                        label={e.outcome}
                        color={e.outcome === "Success" ? "success" : "error"}
                        sx={{ fontSize: 11, height: 20 }}
                      />
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>
                      {e.actorIsSystem ? t("security.system") : e.actorUserId ? t("security.userNum", { id: e.actorUserId }) : "-"}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>{e.targetUsername ?? (e.targetUserId ? t("security.userNum", { id: e.targetUserId }) : "-")}</TableCell>
                    <TableCell sx={{ fontSize: 12, fontFamily: "monospace" }}>{e.ipAddress ?? "-"}</TableCell>
                    <TableCell sx={{ fontSize: 12 }}>{e.reason ?? "-"}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ) : (
            <Table stickyHeader size="small">
              <TableHead sx={tableHeadSx}>
                <TableRow>
                  <TableCell sx={headCell}>{t("results.dateTime")}</TableCell>
                  <TableCell sx={headCell}>{t("security.username")}</TableCell>
                  <TableCell sx={headCell}>{t("security.result")}</TableCell>
                  <TableCell sx={headCell}>{t("security.failureReason")}</TableCell>
                  <TableCell sx={headCell}>{t("security.ip")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {logins.map((l) => (
                  <TableRow key={l.id} hover>
                    <TableCell sx={{ fontSize: 12, whiteSpace: "nowrap" }}>{formatDateTimeWithTimeZone(l.timestamp)}</TableCell>
                    <TableCell sx={{ fontSize: 12 }}>{l.username}</TableCell>
                    <TableCell>
                      <Chip
                        size="small"
                        label={l.success ? t("security.success") : t("security.failed")}
                        color={l.success ? "success" : "error"}
                        sx={{ fontSize: 11, height: 20 }}
                      />
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>{l.failureReason ?? "-"}</TableCell>
                    <TableCell sx={{ fontSize: 12, fontFamily: "monospace" }}>{l.ipAddress ?? "-"}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </TableContainer>
        {empty && !error && (
          <Typography sx={{ p: 4, textAlign: "center", fontSize: 13, color: "text.secondary" }}>{t("security.noRecords")}</Typography>
        )}
        {hasMore && (
          <Box sx={{ p: 1.5, textAlign: "center", borderTop: "1px solid", borderColor: "divider" }}>
            <Button size="small" disabled={loading} onClick={() => void load(cursor, true)} sx={{ textTransform: "none" }}>
              {loading ? t("security.loading") : t("security.loadOlder")}
            </Button>
          </Box>
        )}
      </Paper>
    </Box>
  );
}
