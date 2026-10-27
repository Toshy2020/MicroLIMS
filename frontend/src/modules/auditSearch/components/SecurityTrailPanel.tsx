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
import { formatLabDateTime } from "../../../utils/formatDate";
import { tableHeadSx } from "../../../theme";
import { AuditSearchService } from "../services/AuditSearchService";
import type { LoginHistoryItem, SecurityEventItem } from "../types/auditTypes";

interface Props {
  kind: "security-events" | "login-history";
}

const PAGE = 100;

// Security Events and Login History. Both need System.ViewSecurityAudit on the server;
// the parent only shows the tabs to holders of it, and a 403 here is reported, not hidden.
export function SecurityTrailPanel({ kind }: Props) {
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
        setError("Failed to load the security audit trail. You may not hold the System.ViewSecurityAudit permission.");
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
                  <TableCell sx={headCell}>Date / Time</TableCell>
                  <TableCell sx={headCell}>Event</TableCell>
                  <TableCell sx={headCell}>Outcome</TableCell>
                  <TableCell sx={headCell}>Actor</TableCell>
                  <TableCell sx={headCell}>Target</TableCell>
                  <TableCell sx={headCell}>IP</TableCell>
                  <TableCell sx={headCell}>Reason</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {events.map((e) => (
                  <TableRow key={e.id} hover>
                    <TableCell sx={{ fontSize: 12, whiteSpace: "nowrap" }}>{formatLabDateTime(e.occurredAtUtc)}</TableCell>
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
                      {e.actorIsSystem ? "System" : e.actorUserId ? `User #${e.actorUserId}` : "-"}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>{e.targetUsername ?? (e.targetUserId ? `User #${e.targetUserId}` : "-")}</TableCell>
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
                  <TableCell sx={headCell}>Date / Time</TableCell>
                  <TableCell sx={headCell}>Username</TableCell>
                  <TableCell sx={headCell}>Result</TableCell>
                  <TableCell sx={headCell}>Failure reason</TableCell>
                  <TableCell sx={headCell}>IP</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {logins.map((l) => (
                  <TableRow key={l.id} hover>
                    <TableCell sx={{ fontSize: 12, whiteSpace: "nowrap" }}>{formatLabDateTime(l.timestamp)}</TableCell>
                    <TableCell sx={{ fontSize: 12 }}>{l.username}</TableCell>
                    <TableCell>
                      <Chip
                        size="small"
                        label={l.success ? "Success" : "Failed"}
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
          <Typography sx={{ p: 4, textAlign: "center", fontSize: 13, color: "text.secondary" }}>No records found.</Typography>
        )}
        {hasMore && (
          <Box sx={{ p: 1.5, textAlign: "center", borderTop: "1px solid", borderColor: "divider" }}>
            <Button size="small" disabled={loading} onClick={() => void load(cursor, true)} sx={{ textTransform: "none" }}>
              {loading ? "Loading..." : "Load older"}
            </Button>
          </Box>
        )}
      </Paper>
    </Box>
  );
}
