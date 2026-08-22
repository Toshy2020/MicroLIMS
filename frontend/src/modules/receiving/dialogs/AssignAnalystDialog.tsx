import React, { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  TextField,
  Typography,
  Box,
  Alert,
  CircularProgress,
  Stack,
  Divider,
  useTheme
} from "@mui/material";
import PersonOutlineIcon from "@mui/icons-material/PersonOutline";
import AssignmentIndIcon from "@mui/icons-material/AssignmentInd";
import { SampleRecord } from "../types/receivingTypes";
import { ReceiveService } from "../services/ReceiveService";
import { UserService, UserRecord } from "../../users/services/UserService";
import { CategoryBadge } from "../../../components/StatusBadge";

interface AssignAnalystDialogProps {
  open: boolean;
  sample: SampleRecord | null;
  onClose: () => void;
  onAssigned: (updatedSample: SampleRecord) => void;
}

export function AssignAnalystDialog({
  open,
  sample,
  onClose,
  onAssigned
}: AssignAnalystDialogProps) {
  const theme = useTheme();
  const [analysts, setAnalysts] = useState<UserRecord[]>([]);
  const [selectedAnalystId, setSelectedAnalystId] = useState<string>("unassigned");
  const [reason, setReason] = useState("");
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadAnalysts = async () => {
    setError(null);
    setLoading(true);
    try {
      const activeAnalysts = await UserService.getEligibleAnalysts();
      setAnalysts(activeAnalysts);
    } catch (err: any) {
      setError(
        err?.response?.data?.message ||
        "Unable to load eligible analysts. Please retry or contact an administrator."
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (open && sample) {
      setReason("");

      // Determine existing assigned analyst
      const existingId =
        sample.assignedAnalystId ||
        sample.assignedTests.find((t) => t.assignedAnalystId != null)?.assignedAnalystId;
      setSelectedAnalystId(existingId ? String(existingId) : "unassigned");

      loadAnalysts();
    }
  }, [open, sample]);

  const handleSubmit = async () => {
    if (!sample) return;
    setError(null);
    setSubmitting(true);

    try {
      const analystUserId =
        selectedAnalystId === "unassigned" ? null : Number(selectedAnalystId);
      const updated = await ReceiveService.assignAnalyst(sample.sampleId, analystUserId, reason.trim() || undefined);
      onAssigned(updated);
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message || "Failed to assign analyst.");
    } finally {
      setSubmitting(false);
    }
  };

  if (!sample) return null;

  const currentAssignedName =
    sample.assignedAnalystName ||
    sample.assignedTests.find((t) => !stringIsEmpty(t.assignedAnalystName))?.assignedAnalystName ||
    "Unassigned";

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ pb: 1, display: "flex", alignItems: "center", gap: 1 }}>
        <AssignmentIndIcon sx={{ color: theme.palette.primary.main }} />
        <Typography variant="h6" component="span" sx={{ fontWeight: 700 }}>
          Assign Responsible Analyst
        </Typography>
      </DialogTitle>

      <DialogContent dividers>
        {error && (
          <Alert
            severity="error"
            sx={{ mb: 2 }}
            action={
              <Button color="inherit" size="small" onClick={loadAnalysts}>
                Retry
              </Button>
            }
          >
            {error}
          </Alert>
        )}

        {/* Sample Metadata Card */}
        <Box
          sx={{
            p: 2,
            mb: 2.5,
            bgcolor: "background.default",
            borderRadius: 1.5,
            border: 1,
            borderColor: "divider"
          }}
        >
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.palette.primary.main }}>
              {sample.displayName}
            </Typography>
            <CategoryBadge category={sample.category} />
          </Box>
          <Stack spacing={0.5} sx={{ fontSize: 12, color: "text.secondary" }}>
            <Box sx={{ display: "flex", justifyContent: "space-between" }}>
              <span>Reference Number:</span>
              <strong>{sample.referenceNumber}</strong>
            </Box>
            {sample.batchNumber && (
              <Box sx={{ display: "flex", justifyContent: "space-between" }}>
                <span>Batch / Control No.:</span>
                <strong>{sample.batchNumber} / {sample.controlNumber || "—"}</strong>
              </Box>
            )}
            <Box sx={{ display: "flex", justifyContent: "space-between" }}>
              <span>Current Assignment:</span>
              <strong style={{ color: currentAssignedName === "Unassigned" ? theme.palette.text.disabled : theme.palette.primary.main }}>
                {currentAssignedName}
              </strong>
            </Box>
          </Stack>
        </Box>

        <Typography sx={{ fontSize: 12.5, color: "text.secondary", mb: 2 }}>
          Section Head Assignment Rule: Assigning an analyst designates them as the responsible analyst for sample preparation and initial testing.
        </Typography>

        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
            <CircularProgress size={28} />
          </Box>
        ) : (
          <FormControl fullWidth size="small">
            <InputLabel id="assign-analyst-label">Select Analyst</InputLabel>
            <Select
              labelId="assign-analyst-label"
              label="Select Analyst"
              value={selectedAnalystId}
              onChange={(e) => setSelectedAnalystId(e.target.value)}
            >
              <MenuItem value="unassigned">
                <em>Unassigned (Open for any authorized analyst to prepare)</em>
              </MenuItem>
              {analysts.map((a: UserRecord) => (
                <MenuItem key={a.id} value={String(a.id)}>
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    <PersonOutlineIcon sx={{ fontSize: 18, color: "text.secondary" }} />
                    <Typography sx={{ fontSize: 13, fontWeight: 500 }}>
                      {a.fullName} ({a.username}) — {a.role?.name}
                    </Typography>
                  </Box>
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}

        <Box sx={{ mt: 2.5 }}>
          <TextField
            fullWidth
            size="small"
            label="Reason for Assignment / Reassignment (recorded in Audit Trail)"
            placeholder="e.g. Workload balancing, primary analyst on leave, sectional scheduling"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            multiline
            rows={2}
          />
        </Box>
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={submitting} variant="outlined">
          Cancel
        </Button>
        <Button
          onClick={handleSubmit}
          disabled={submitting || loading}
          variant="contained"
          startIcon={submitting ? <CircularProgress size={16} /> : <AssignmentIndIcon />}
        >
          {submitting ? "Saving..." : "Save Assignment"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

function stringIsEmpty(str?: string | null): boolean {
  return !str || str.trim().length === 0;
}
