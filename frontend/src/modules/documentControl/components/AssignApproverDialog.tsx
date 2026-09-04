import React, { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Alert,
  Box,
  Typography,
  CircularProgress
} from "@mui/material";
import { documentApprovalService } from "../services/documentApprovalService";
import { apiClient } from "../../../services/apiClient";
import type { DocumentApprovalTaskDto } from "../types/documentControlTypes";

interface AssignApproverDialogProps {
  open: boolean;
  onClose: () => void;
  onAssigned: (task: DocumentApprovalTaskDto) => void;
  revisionId: number;
  companyDocumentCode: string;
  revisionNumber: string;
  authorUserId: number;
  reviewerUserIds: number[];
}

interface UserOption {
  id: number;
  fullName: string;
  username: string;
  role?: { type: string; name: string };
}

export const AssignApproverDialog: React.FC<AssignApproverDialogProps> = ({
  open,
  onClose,
  onAssigned,
  revisionId,
  companyDocumentCode,
  revisionNumber,
  authorUserId,
  reviewerUserIds
}) => {
  const [approverUserId, setApproverUserId] = useState<number | "">("");
  const [submissionNotes, setSubmissionNotes] = useState("");
  const [dueDate, setDueDate] = useState("");
  const [targetEffectiveDate, setTargetEffectiveDate] = useState("");
  const [users, setUsers] = useState<UserOption[]>([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setError(null);
      setApproverUserId("");
      setSubmissionNotes("");
      setDueDate("");
      setTargetEffectiveDate("");
      fetchEligibleApprovers();
    }
  }, [open]);

  const fetchEligibleApprovers = async () => {
    try {
      setLoading(true);
      const res = await apiClient.get("/users");
      const allUsers: UserOption[] = res.data.data || res.data || [];
      // Filter out Author and Reviewer per SoD rules (DC-URS-078)
      const eligible = allUsers.filter(
        (u) => u.id !== authorUserId && !reviewerUserIds.includes(u.id)
      );
      setUsers(eligible);
    } catch (err: any) {
      setError("Failed to load user list for approver selection.");
    } finally {
      setLoading(false);
    }
  };

  const handleAssign = async () => {
    if (!approverUserId) {
      setError("Please select a designated approver.");
      return;
    }

    try {
      setSubmitting(true);
      setError(null);
      const task = await documentApprovalService.createApprovalTask(revisionId, {
        approverUserId: Number(approverUserId),
        submissionNotes: submissionNotes.trim() || undefined,
        dueDate: dueDate ? new Date(dueDate).toISOString() : undefined,
        targetEffectiveDate: targetEffectiveDate
          ? new Date(targetEffectiveDate).toISOString()
          : undefined
      });
      onAssigned(task);
      onClose();
    } catch (err: any) {
      const msg =
        err.response?.data?.message ||
        err.response?.data?.errors?.[0] ||
        err.message ||
        "Failed to create approval task.";
      setError(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ fontWeight: 700 }}>
        Assign Approver — {companyDocumentCode} Rev {revisionNumber}
      </DialogTitle>

      <DialogContent dividers>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        <Alert severity="info" sx={{ mb: 2.5 }}>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            Segregation of Duties (SoD) Active:
          </Typography>
          <Typography variant="caption">
            The Author and all participating Technical Reviewers are strictly
            disqualified from approving this revision. System Administrators
            are non-exempt.
          </Typography>
        </Alert>

        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
            <CircularProgress size={28} />
          </Box>
        ) : (
          <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
            <FormControl fullWidth size="small" required>
              <InputLabel id="approver-select-label">Designated Approver</InputLabel>
              <Select
                labelId="approver-select-label"
                value={approverUserId}
                label="Designated Approver"
                onChange={(e) => setApproverUserId(e.target.value as number)}
              >
                {users.map((u) => (
                  <MenuItem key={u.id} value={u.id}>
                    {u.fullName} ({u.username})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <TextField
              label="Target Effective Date (Optional)"
              type="date"
              size="small"
              value={targetEffectiveDate}
              onChange={(e) => setTargetEffectiveDate(e.target.value)}
              InputLabelProps={{ shrink: true }}
              helperText="If set to a future date, document will be approved in Future Effective state until activation."
              fullWidth
            />

            <TextField
              label="Approval Due Date (Optional)"
              type="date"
              size="small"
              value={dueDate}
              onChange={(e) => setDueDate(e.target.value)}
              InputLabelProps={{ shrink: true }}
              fullWidth
            />

            <TextField
              label="Submission Notes for Approver (Optional)"
              multiline
              rows={3}
              size="small"
              value={submissionNotes}
              onChange={(e) => setSubmissionNotes(e.target.value)}
              placeholder="Provide background context or rationale for this approval request..."
              fullWidth
            />
          </Box>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={submitting}>
          Cancel
        </Button>
        <Button
          variant="contained"
          color="primary"
          onClick={handleAssign}
          disabled={submitting || loading || !approverUserId}
        >
          {submitting ? "Assigning..." : "Assign & Route for Approval"}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
