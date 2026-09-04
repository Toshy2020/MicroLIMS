import { useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  TextField,
  MenuItem,
  Alert,
  CircularProgress
} from "@mui/material";
import PersonAddOutlinedIcon from "@mui/icons-material/PersonAddOutlined";
import { documentControlService } from "../services/documentControlService";
import type { AssignmentRole, CreateAssignmentRequest } from "../types/documentControlTypes";

interface AssignmentDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
  documentMasterId: number;
  companyDocumentCode: string;
}

export function AssignmentDialog({
  open,
  onClose,
  onSuccess,
  documentMasterId,
  companyDocumentCode
}: AssignmentDialogProps) {
  const [userId, setUserId] = useState<number | "">("");
  const [assignmentRole, setAssignmentRole] = useState<AssignmentRole>("Author");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSave = async () => {
    if (!userId || typeof userId !== "number" || userId <= 0) {
      setError("Please specify a valid User ID.");
      return;
    }

    setLoading(true);
    setError(null);

    const req: CreateAssignmentRequest = {
      userId,
      assignmentRole
    };

    try {
      await documentControlService.addAssignment(documentMasterId, req);
      setUserId("");
      onSuccess();
      onClose();
    } catch (err: any) {
      const msg = err.response?.data?.message || err.message || "Failed to add assignment.";
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const handleClose = () => {
    if (!loading) {
      setUserId("");
      setError(null);
      onClose();
    }
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="xs" fullWidth>
      <DialogTitle sx={{ pb: 1, display: "flex", alignItems: "center", gap: 1 }}>
        <PersonAddOutlinedIcon color="primary" />
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          Add Role Assignment
        </Typography>
      </DialogTitle>

      <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
        <Typography variant="caption" color="text.secondary">
          Document: <strong>{companyDocumentCode}</strong>
        </Typography>

        {error && <Alert severity="error">{error}</Alert>}

        <TextField
          select
          label="Assignment Role *"
          value={assignmentRole}
          onChange={(e) => setAssignmentRole(e.target.value as AssignmentRole)}
          disabled={loading}
          fullWidth
        >
          <MenuItem value="Author">Author (Can edit draft & upload files)</MenuItem>
          <MenuItem value="TechnicalReviewer">Technical Reviewer</MenuItem>
          <MenuItem value="Approver">Approver</MenuItem>
          <MenuItem value="Owner">Co-Owner</MenuItem>
        </TextField>

        <TextField
          label="Target User ID *"
          type="number"
          value={userId}
          onChange={(e) => setUserId(Number(e.target.value) || "")}
          disabled={loading}
          placeholder="e.g. 2"
          fullWidth
          helperText="Specify the numeric User ID of the personnel"
        />
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={handleClose} disabled={loading} color="inherit">
          Cancel
        </Button>
        <Button
          onClick={handleSave}
          variant="contained"
          disabled={loading || !userId}
          startIcon={loading ? <CircularProgress size={16} color="inherit" /> : null}
        >
          {loading ? "Assigning..." : "Assign Role"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
