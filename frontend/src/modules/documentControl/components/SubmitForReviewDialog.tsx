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
import { DateField } from "../../../components/DateField";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";

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
  isActive?: boolean;
}

export const SubmitForReviewDialog: React.FC<SubmitForReviewDialogProps> = ({
  open,
  onClose,
  onSubmitted,
  revisionId,
  authorUserId,
  currentUserId
}) => {
  const { t } = useTranslation("documentControl");
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
      const res = await apiClient.get("/users/directory", { params: { permission: "Documents.Review" } });
      const allUsers: UserOption[] = res.data?.data || res.data || [];
      // Segregation of Duties filter: author cannot be reviewer!
      const eligible = allUsers.filter(
        (u) => u.isActive && u.id !== authorUserId && u.id !== currentUserId
      );
      setUsers(eligible);
    } catch (_err: unknown) {
      setError(t("submitForReview.loadError"));
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async () => {
    if (!reviewerUserId) {
      setError(t("submitForReview.selectError"));
      return;
    }

    if (reviewerUserId === authorUserId) {
      setError(t("submitForReview.sodError"));
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
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("submitForReview.submitError")));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ fontWeight: 600 }}>{t("submitForReview.title")}</DialogTitle>
      <DialogContent dividers>
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2.5, mt: 1 }}>
          <Alert severity="info" sx={{ fontSize: "0.85rem" }}>
            <Trans
              i18nKey="submitForReview.sodInfo"
              ns="documentControl"
              components={{ strong: <strong /> }}
            />
          </Alert>

          {error && <Alert severity="error">{error}</Alert>}

          {loading ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
              <CircularProgress size={32} />
            </Box>
          ) : (
            <FormControl fullWidth required>
              <InputLabel id="reviewer-select-label">{t("submitForReview.reviewerLabel")}</InputLabel>
              <Select
                labelId="reviewer-select-label"
                value={reviewerUserId}
                label={t("submitForReview.reviewerLabel")}
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

          <DateField
            label={t("submitForReview.dueDateLabel")}
            value={dueDate}
            onChange={(v) => setDueDate(v)}
            fullWidth
            size="medium"
          />

          <TextField
            label={t("submitForReview.notesLabel")}
            multiline
            rows={3}
            value={submissionNotes}
            onChange={(e) => setSubmissionNotes(e.target.value)}
            placeholder={t("submitForReview.notesPlaceholder")}
            fullWidth
          />
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={submitting}>
          {t("common.cancel")}
        </Button>
        <Button
          onClick={handleSubmit}
          variant="contained"
          disabled={submitting || loading || !reviewerUserId}
        >
          {submitting ? <CircularProgress size={24} /> : t("submitForReview.submit")}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
