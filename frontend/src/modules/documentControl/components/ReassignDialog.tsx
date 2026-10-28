import { useEffect, useState } from "react";
import {
  Alert,
  Autocomplete,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  TextField,
  Typography
} from "@mui/material";
import { reassignmentService } from "../services/reassignmentService";
import { getErrorMessage } from "../../../utils/errorMessage";
import type { ReassignmentCandidate, ReassignmentItem } from "../types/reassignmentTypes";

interface ReassignDialogProps {
  item: ReassignmentItem | null;
  onClose: () => void;
  onReassigned: (item: ReassignmentItem, newUser: ReassignmentCandidate) => void;
}

// Hand a deactivated user's responsibility to someone else. The list is what the server would accept (an active
// member of the document's department whose role holds the needed permission and who passes the segregation of
// duties rules); the server checks all of it again on save.
export function ReassignDialog({ item, onClose, onReassigned }: ReassignDialogProps) {
  const [candidates, setCandidates] = useState<ReassignmentCandidate[]>([]);
  const [loading, setLoading] = useState(false);
  const [selected, setSelected] = useState<ReassignmentCandidate | null>(null);
  const [reason, setReason] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setSelected(null);
    setReason("");
    setError(null);
    setCandidates([]);
    if (!item) return;
    let live = true;
    setLoading(true);
    reassignmentService
      .getCandidates(item.itemType, item.itemId)
      .then((list) => { if (live) setCandidates(list); })
      .catch((e) => { if (live) setError(getErrorMessage(e, "Could not load the eligible users.")); })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [item]);

  const canSave = !!item && !!selected && reason.trim().length > 0 && !saving;

  const save = async () => {
    if (!item || !selected) return;
    setSaving(true);
    setError(null);
    try {
      await reassignmentService.reassign({ itemType: item.itemType, itemId: item.itemId, newUserId: selected.userId, reason: reason.trim() });
      onReassigned(item, selected);
    } catch (e) {
      setError(getErrorMessage(e, "Could not reassign this responsibility."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={!!item} onClose={saving ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Reassign: {item?.roleNeeded}</DialogTitle>
      <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
        {item && (
          <Typography variant="body2">
            <strong>{item.documentCode}</strong> &ndash; {item.documentTitle}
            {item.revisionNumber ? ` (revision ${item.revisionNumber})` : ""}
            <br />
            Currently held by an inactive user ({item.holderName ?? "name not shown"}). The new holder must be an active member of{" "}
            <strong>{item.departmentName}</strong> whose role holds <code>{item.requiredPermission}</code>.
          </Typography>
        )}
        {error && <Alert severity="error">{error}</Alert>}
        {loading ? (
          <CircularProgress size={24} aria-label="Loading eligible users" />
        ) : candidates.length === 0 && !error ? (
          <Alert severity="warning">
            Nobody is eligible. Add a user to {item?.departmentName} (Administration &gt; Organization) whose role holds{" "}
            {item?.requiredPermission}, then try again.
          </Alert>
        ) : (
          <Autocomplete
            options={candidates}
            value={selected}
            onChange={(_, value) => setSelected(value)}
            getOptionLabel={(c) => c.fullName}
            renderOption={(props, c) => (
              <li {...props} key={c.userId}>
                {c.fullName}
                <Typography component="span" variant="caption" sx={{ ml: 1, color: "text.secondary" }}>
                  {[c.jobTitle, c.roleName].filter(Boolean).join(" · ")}
                </Typography>
              </li>
            )}
            renderInput={(params) => <TextField {...params} label="New holder" required size="small" />}
          />
        )}
        <TextField
          label="Reason"
          size="small"
          multiline
          rows={2}
          required
          fullWidth
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder="Why this person takes over (recorded in the audit trail)"
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving} color="inherit">Cancel</Button>
        <Button variant="contained" onClick={save} disabled={!canSave}>
          {saving ? "Reassigning..." : "Reassign"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
