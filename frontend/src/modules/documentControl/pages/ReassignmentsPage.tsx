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

const ALL = "all";

function daysSince(iso: string | null): number | null {
  if (!iso) return null;
  return Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000));
}

// Document responsibilities held by deactivated users. Computed from the records on every load, never copied:
// the moment someone is reactivated or the item is handed over it drops off. Nothing is reassigned automatically;
// items waiting longer than the escalation period are flagged and the department's Section Heads are notified.
export function ReassignmentsPage() {
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
      setError(getErrorMessage(e, "Could not load the reassignment queue."));
    } finally {
      setLoading(false);
    }
  }, []);

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
        title="Needs reassignment"
        subtitle="Document responsibilities held by deactivated users. Each one is handed to an active member of the document's department by an explicit, audited act."
      />

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {queue && (
        <Alert severity={queue.overdue > 0 ? "warning" : "info"} sx={{ mb: 2 }}>
          {queue.total === 0
            ? "Nothing is waiting for reassignment."
            : `${queue.total} item(s) waiting, ${queue.overdue} overdue. `}
          {queue.total > 0 && `Items waiting more than ${queue.escalationDays} days since the user was deactivated are flagged overdue and the department's Section Heads are notified. Nothing is reassigned automatically.`}
        </Alert>
      )}

      {departments.length > 1 && (
        <TextField
          select
          size="small"
          label="Department"
          value={department}
          onChange={(e) => setDepartment(e.target.value)}
          sx={{ minWidth: 240, mb: 2 }}
        >
          <MenuItem value={ALL}>All departments</MenuItem>
          {departments.map(([id, name]) => <MenuItem key={id} value={String(id)}>{name}</MenuItem>)}
        </TextField>
      )}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}><CircularProgress aria-label="Loading" /></Box>
      ) : (
        <Paper variant="outlined">
          <TableContainer>
            <Table size="small" aria-label="Responsibilities needing reassignment">
              <TableHead sx={tableHeadSx}>
                <TableRow>
                  <TableCell>Document</TableCell>
                  <TableCell>Revision</TableCell>
                  <TableCell>Role needed</TableCell>
                  <TableCell>Held by</TableCell>
                  <TableCell>Inactive since</TableCell>
                  <TableCell>Department</TableCell>
                  <TableCell align="right">Action</TableCell>
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
                        {i.inactiveSince ? formatLabDate(i.inactiveSince) : "Unknown"}
                        {days !== null && <Typography variant="caption" sx={{ display: "block" }} color="text.secondary">{days} day(s)</Typography>}
                        {i.isOverdue && <Chip size="small" color="error" label="Overdue" sx={{ mt: 0.5 }} />}
                      </TableCell>
                      <TableCell>{i.departmentName}</TableCell>
                      <TableCell align="right">
                        <Button size="small" variant="contained" onClick={() => setTarget(i)}>Reassign</Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
                {items.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={7} align="center" sx={{ py: 3, color: "text.secondary" }}>
                      No responsibilities need reassignment.
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
          toast.success(`${item.documentCode}: ${item.roleNeeded} reassigned to ${user.fullName}.`);
          load();
        }}
      />
    </Box>
  );
}
