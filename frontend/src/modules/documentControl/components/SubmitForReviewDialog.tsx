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
  CircularProgress
} from "@mui/material";
import { documentReviewService } from "../services/documentReviewService";
import { apiClient } from "../../../services/apiClient";

interface SubmitForReviewDialogProps {
  open: boolean;
  onClose: () => void;
  onSubmitted: () => void;
  revisionId: number;
  authorUserId: number;
  currentUserId: number;
}

interface UserOption {
  id: number;
  fullName: string;
  username: string;
}

export const SubmitForReviewDialog: React.FC<SubmitForReviewDialogProps> = ({
  open,
  onClose,
  onSubmitted,
  revisionId,
  authorUserId,
  currentUserId
}) => {
  const [reviewerUserId, setReviewerUserId] = useState<number | "">("");
  const [submissionNotes, setSubmissionNotes] = useState("");
  const [dueDate, setDueDate] = useState("");
  const [users, setUsers] = useState<UserOption[]>([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setError(null);
      setReviewerUserId("");
      setSubmissionNotes("");
      setDueDate("");
      fetchEligibleReviewers();
    }
  }, [open]);

  const fetchEligibleReviewers = async () => {
    setLoading(true);
    try {
      // /users is SystemAdministrator-only; the directory is open to any authenticated user.
      const res = await apiClient.get("/users/directory");
      const allUsers = res.data?.data || res.data || [];
      // Segregation of Duties filter: author cannot be reviewer!
      const eligible = allUsers.filter(
        (u: any) => u.isActive && u.id !== authorUserId && u.id !== currentUserId
      );
      setUsers(eligible);
    } catch (err: any) {
      setError("Failed to load eligible reviewers.");
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async () => {
    if (!reviewerUserId) {
      setError("Please select a technical reviewer.");
      return;
    }

    if (reviewerUserId === authorUserId) {
      setError("Segregation of Duties: Author cannot be assigned as technical reviewer.");
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      await documentReviewService.submitForReview(revisionId, {
        reviewerUserId: Number(reviewerUserId),
        submissionNotes: submissionNotes.trim() || undefined,
        dueDate: dueDate ? new Date(dueDate).toISOString() : undefined
      });
      onSubmitted();
      onClose();
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to submit for review.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ fontWeight: 600 }}>Submit for Technical Review</DialogTitle>
      <DialogContent dividers>
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2.5, mt: 1 }}>
          <Alert severity="info" sx={{ fontSize: "0.85rem" }}>
            <strong>Segregation of Duties Enforcement:</strong> In accordance with GMP regulations, the author of this revision cannot be assigned as its technical reviewer.
          </Alert>

          {error && <Alert severity="error">{error}</Alert>}

          {loading ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
              <CircularProgress size={32} />
            </Box>
          ) : (
            <FormControl fullWidth required>
              <InputLabel id="reviewer-select-label">Technical Reviewer</InputLabel>
              <Select
                labelId="reviewer-select-label"
                value={reviewerUserId}
                label="Technical Reviewer"
                onChange={(e) => setReviewerUserId(e.target.value as number)}
              >
                {users.map((u) => (
                  <MenuItem key={u.id} value={u.id}>
                    {u.fullName} ({u.username})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}

          <TextField
            label="Review Due Date"
            type="date"
            value={dueDate}
            onChange={(e) => setDueDate(e.target.value)}
            fullWidth
            slotProps={{
              inputLabel: { shrink: true }
            }}
          />

          <TextField
            label="Submission Notes"
            multiline
            rows={3}
            value={submissionNotes}
            onChange={(e) => setSubmissionNotes(e.target.value)}
            placeholder="Provide context or highlight key changes for the technical reviewer..."
            fullWidth
          />
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={submitting}>
          Cancel
        </Button>
        <Button
          onClick={handleSubmit}
          variant="contained"
          disabled={submitting || loading || !reviewerUserId}
        >
          {submitting ? <CircularProgress size={24} /> : "Submit for Review"}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
