import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  MenuItem,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography
} from "@mui/material";
import { toast } from "sonner";
import { PageHeader } from "../../../components/PageHeader";
import { tableHeadSx } from "../../../theme";
import { getErrorMessage } from "../../../utils/errorMessage";
import { formatLabDate } from "../../../utils/formatDate";
import { ReassignDialog } from "../components/ReassignDialog";
import { reassignmentService } from "../services/reassignmentService";
import type { ReassignmentItem, ReassignmentQueue } from "../types/reassignmentTypes";
import { useTranslation } from "../../../i18n";

const ALL = "all";

function daysSince(iso: string | null): number | null {
  if (!iso) return null;
  return Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000));
}

// Document responsibilities held by deactivated users. Computed from the records on every load, never copied:
// the moment someone is reactivated or the item is handed over it drops off. Nothing is reassigned automatically;
// items waiting longer than the escalation period are flagged and the department's Section Heads are notified.
export function ReassignmentsPage() {
  const { t } = useTranslation("documentControl");
  const [queue, setQueue] = useState<ReassignmentQueue | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [department, setDepartment] = useState<string>(ALL);
  const [target, setTarget] = useState<ReassignmentItem | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setQueue(await reassignmentService.getQueue());
    } catch (e) {
      setError(getErrorMessage(e, t("reassignments.loadError")));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => { load(); }, [load]);

  const departments = useMemo(() => {
    const seen = new Map<number, string>();
    queue?.items.forEach((i) => seen.set(i.departmentId, i.departmentName));
    return [...seen.entries()].sort((a, b) => a[1].localeCompare(b[1]));
  }, [queue]);

  const items = useMemo(
    () => (queue?.items ?? []).filter((i) => department === ALL || String(i.departmentId) === department),
    [queue, department]
  );

  return (
    <Box>
      <PageHeader
        title={t("reassignments.title")}
        subtitle={t("reassignments.subtitle")}
      />

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {queue && (
        <Alert severity={queue.overdue > 0 ? "warning" : "info"} sx={{ mb: 2 }}>
          {queue.total === 0
            ? t("reassignments.emptyQueue")
            : t("reassignments.queueSummary", { total: queue.total, overdue: queue.overdue })}
          {queue.total > 0 && t("reassignments.escalationNotice", { days: queue.escalationDays })}
        </Alert>
      )}

      {departments.length > 1 && (
        <TextField
          select
          size="small"
          label={t("reassignments.departmentLabel")}
          value={department}
          onChange={(e) => setDepartment(e.target.value)}
          sx={{ minWidth: 240, mb: 2 }}
        >
          <MenuItem value={ALL}>{t("reassignments.allDepartments")}</MenuItem>
          {departments.map(([id, name]) => <MenuItem key={id} value={String(id)}>{name}</MenuItem>)}
        </TextField>
      )}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}><CircularProgress aria-label={t("reassignments.loadingAria")} /></Box>
      ) : (
        <Paper variant="outlined">
          <TableContainer>
            <Table size="small" aria-label={t("reassignments.tableAria")}>
              <TableHead sx={tableHeadSx}>
                <TableRow>
                  <TableCell>{t("reassignments.columns.document")}</TableCell>
                  <TableCell>{t("reassignments.columns.revision")}</TableCell>
                  <TableCell>{t("reassignments.columns.roleNeeded")}</TableCell>
                  <TableCell>{t("reassignments.columns.heldBy")}</TableCell>
                  <TableCell>{t("reassignments.columns.inactiveSince")}</TableCell>
                  <TableCell>{t("reassignments.columns.department")}</TableCell>
                  <TableCell align="right">{t("reassignments.columns.action")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {items.map((i) => {
                  const days = daysSince(i.inactiveSince);
                  return (
                    <TableRow key={`${i.itemType}-${i.itemId}`}>
                      <TableCell>
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>{i.documentCode}</Typography>
                        <Typography variant="caption" color="text.secondary">{i.documentTitle}</Typography>
                      </TableCell>
                      <TableCell>{i.revisionNumber ?? "—"}</TableCell>
                      <TableCell><Chip size="small" variant="outlined" label={i.roleNeeded} /></TableCell>
                      <TableCell>
                        {i.holderLabel}
                        {i.holderName && <Typography variant="caption" sx={{ display: "block" }} color="text.secondary">{i.holderName}</Typography>}
                      </TableCell>
                      <TableCell>
                        {i.inactiveSince ? formatLabDate(i.inactiveSince) : t("reassignments.unknown")}
                        {days !== null && <Typography variant="caption" sx={{ display: "block" }} color="text.secondary">{t("reassignments.daysCount", { count: days })}</Typography>}
                        {i.isOverdue && <Chip size="small" color="error" label={t("reassignments.overdue")} sx={{ mt: 0.5 }} />}
                      </TableCell>
                      <TableCell>{i.departmentName}</TableCell>
                      <TableCell align="right">
                        <Button size="small" variant="contained" onClick={() => setTarget(i)}>{t("reassignments.reassign")}</Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
                {items.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={7} align="center" sx={{ py: 3, color: "text.secondary" }}>
                      {t("reassignments.emptyTable")}
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      )}

      <ReassignDialog
        item={target}
        onClose={() => setTarget(null)}
        onReassigned={(item, user) => {
          setTarget(null);
          toast.success(t("reassignments.reassignedToast", { code: item.documentCode, role: item.roleNeeded, user: user.fullName }));
          load();
        }}
      />
    </Box>
  );
}
