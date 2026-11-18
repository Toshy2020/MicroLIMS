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
import { DateField } from "../../../components/DateField";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";

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
  const { t } = useTranslation("documentControl");
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
      // /users is SystemAdministrator-only; the directory is open to any authenticated user.
      const res = await apiClient.get("/users/directory", { params: { permission: "Documents.Approve" } });
      const allUsers: UserOption[] = res.data.data || res.data || [];
      // Filter out Author and Reviewer per SoD rules (DC-URS-078)
      const eligible = allUsers.filter(
        (u) => u.id !== authorUserId && !reviewerUserIds.includes(u.id)
      );
      setUsers(eligible);
    } catch (_err: unknown) {
      setError(t("assignApprover.loadError"));
    } finally {
      setLoading(false);
    }
  };

  const handleAssign = async () => {
    if (!approverUserId) {
      setError(t("assignApprover.selectError"));
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
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("assignApprover.createError")));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ fontWeight: 700 }}>
        {t("assignApprover.title", { code: companyDocumentCode, rev: revisionNumber })}
      </DialogTitle>

      <DialogContent dividers>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        <Alert severity="info" sx={{ mb: 2.5 }}>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            {t("assignApprover.sodTitle")}
          </Typography>
          <Typography variant="caption">
            {t("assignApprover.sodBody")}
          </Typography>
        </Alert>

        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
            <CircularProgress size={28} />
          </Box>
        ) : (
          <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
            <FormControl fullWidth size="small" required>
              <InputLabel id="approver-select-label">{t("assignApprover.approverLabel")}</InputLabel>
              <Select
                labelId="approver-select-label"
                value={approverUserId}
                label={t("assignApprover.approverLabel")}
                onChange={(e) => setApproverUserId(e.target.value as number)}
              >
                {users.map((u) => (
                  <MenuItem key={u.id} value={u.id}>
                    {u.fullName} ({u.username})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <DateField
              label={t("assignApprover.targetEffectiveDateLabel")}
              value={targetEffectiveDate}
              onChange={(v) => setTargetEffectiveDate(v)}
              helperText={t("assignApprover.targetEffectiveDateHelper")}
              fullWidth
            />

            <DateField
              label={t("assignApprover.dueDateLabel")}
              value={dueDate}
              onChange={(v) => setDueDate(v)}
              fullWidth
            />

            <TextField
              label={t("assignApprover.notesLabel")}
              multiline
              rows={3}
              size="small"
              value={submissionNotes}
              onChange={(e) => setSubmissionNotes(e.target.value)}
              placeholder={t("assignApprover.notesPlaceholder")}
              fullWidth
            />
          </Box>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={submitting}>
          {t("common.cancel")}
        </Button>
        <Button
          variant="contained"
          color="primary"
          onClick={handleAssign}
          disabled={submitting || loading || !approverUserId}
        >
          {submitting ? t("assignApprover.assigning") : t("assignApprover.submit")}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
