import { useEffect, useState } from "react";
import { Dialog, DialogTitle, DialogContent, DialogActions, Button, Table, TableHead, TableRow, TableCell, TableBody, Typography } from "@mui/material";
import { AuditSearchService } from "../modules/auditSearch/services/AuditSearchService";

interface AuditHistoryDialogProps {
  open: boolean;
  onClose: () => void;
  entityName: string;
  entityId: number | null;
}

// "Who added what and when" for a single row - reads the same AuditLog
// that MicroLimsDbContext.SaveChanges captures automatically for every
// Create/Update (Frozen Principle #5). Restricted server-side to
// Section Head / System Administrator, same as the Audit Search screen.
export function AuditHistoryDialog({ open, onClose, entityName, entityId }: AuditHistoryDialogProps) {
  const [entries, setEntries] = useState<any[] | null>(null);

  useEffect(() => {
    if (open && entityId != null) {
      setEntries(null);
      AuditSearchService.getForEntity(entityName, entityId).then(setEntries);
    }
  }, [open, entityName, entityId]);

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>Change History</DialogTitle>
      <DialogContent>
        {!entries ? (
          <Typography color="text.secondary">Loading…</Typography>
        ) : entries.length === 0 ? (
          <Typography color="text.secondary">No changes recorded yet.</Typography>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow><TableCell>Date/Time</TableCell><TableCell>Action</TableCell><TableCell>User ID</TableCell></TableRow>
            </TableHead>
            <TableBody>
              {entries.map((e) => (
                <TableRow key={e.id}>
                  <TableCell>{new Date(e.timestamp).toLocaleString()}</TableCell>
                  <TableCell>{e.action}</TableCell>
                  <TableCell>{e.userId}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </DialogContent>
      <DialogActions><Button onClick={onClose}>Close</Button></DialogActions>
    </Dialog>
  );
}
