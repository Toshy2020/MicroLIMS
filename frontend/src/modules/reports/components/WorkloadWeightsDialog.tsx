import { useState, useEffect } from "react";
import {
  Dialog, DialogTitle, DialogContent, DialogActions, Button, Table, TableHead, TableRow, TableCell,
  TableBody, TextField, Typography, Box, Alert, IconButton, Tooltip, Chip,
  useTheme
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import CheckIcon from "@mui/icons-material/Check";
import CloseIcon from "@mui/icons-material/Close";
import { WorkloadWeightConfig } from "../types/reportingTypes";
import { AnalystKpiService } from "../services/AnalystKpiService";
import { useAuth } from "../../../contexts/AuthContext";
import { brandColors } from "../../../theme";

interface WorkloadWeightsDialogProps {
  open: boolean;
  onClose: () => void;
  onUpdated?: () => void;
}

export function WorkloadWeightsDialog({ open, onClose, onUpdated }: WorkloadWeightsDialogProps) {
  const theme = useTheme();
  const { role, fullName } = useAuth();
  const isAuthorized = role === "SectionHead" || role === "SystemAdministrator";

  const [weights, setWeights] = useState<WorkloadWeightConfig[]>([]);
  const [editingCode, setEditingCode] = useState<string | null>(null);
  const [editValue, setEditValue] = useState<number>(1.0);
  const [editReason, setEditReason] = useState<string>("");
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      AnalystKpiService.getWorkloadWeights().then(setWeights);
      setEditingCode(null);
      setError(null);
      setSuccess(null);
    }
  }, [open]);

  const startEdit = (w: WorkloadWeightConfig) => {
    setEditingCode(w.testCode);
    setEditValue(w.workloadWeight);
    setEditReason("");
    setError(null);
  };

  const cancelEdit = () => {
    setEditingCode(null);
    setError(null);
  };

  const saveEdit = async (testCode: string) => {
    if (editValue <= 0) {
      setError("Workload weight must be greater than 0.");
      return;
    }
    if (!editReason.trim()) {
      setError("Reason for change is required for audit traceability.");
      return;
    }

    try {
      await AnalystKpiService.updateWorkloadWeight(
        testCode,
        editValue,
        editReason.trim()
      );
      const updated = await AnalystKpiService.getWorkloadWeights();
      setWeights(updated);
      setEditingCode(null);
      setSuccess(`Workload weight for ${testCode} updated successfully.`);
      if (onUpdated) onUpdated();
    } catch {
      setError("Failed to update workload weight.");
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ borderBottom: 1, borderColor: "divider", pb: 1.5 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <Typography sx={{ fontSize: 17, fontWeight: 700, color: theme.palette.primary.main }}>
            Configured Workload Units (Test Complexity Weights)
          </Typography>
          <Chip
            size="small"
            label={isAuthorized ? "Section Head / Admin Access" : "Read-Only View"}
            color={isAuthorized ? "primary" : "default"}
          />
        </Box>
        <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.5 }}>
          Operational normalization metric used to account for procedural complexity across test categories. Persisted with reason for change and audit trail.
        </Typography>
      </DialogTitle>

      <DialogContent sx={{ pt: 2.5 }}>
        {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>{error}</Alert>}
        {success && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccess(null)}>{success}</Alert>}

        <Table size="small" sx={{ "& th": { fontWeight: 700, fontSize: 12 } }}>
          <TableHead>
            <TableRow>
              <TableCell>Test Code</TableCell>
              <TableCell>Test Name</TableCell>
              <TableCell>Category</TableCell>
              <TableCell align="center">Workload Weight</TableCell>
              <TableCell>Effective Date</TableCell>
              <TableCell>Reason for Change / Audit</TableCell>
              {isAuthorized && <TableCell align="center">Action</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {weights.map((w) => {
              const isEditing = editingCode === w.testCode;

              return (
                <TableRow key={w.testCode} hover>
                  <TableCell sx={{ fontWeight: 600 }}>{w.testCode}</TableCell>
                  <TableCell>{w.testName}</TableCell>
                  <TableCell>{w.category}</TableCell>
                  <TableCell align="center">
                    {isEditing ? (
                      <TextField
                        size="small"
                        type="number"
                        inputProps={{ step: 0.1, min: 0.1 }}
                        value={editValue}
                        onChange={(e) => setEditValue(parseFloat(e.target.value) || 1.0)}
                        sx={{ width: 80 }}
                      />
                    ) : (
                      <Chip
                        size="small"
                        label={`${w.workloadWeight}x`}
                        sx={{ fontWeight: 700, bgcolor: "#ede9fe", color: "#6d28d9" }}
                      />
                    )}
                  </TableCell>
                  <TableCell>{w.effectiveDate}</TableCell>
                  <TableCell sx={{ maxWidth: 220 }}>
                    {isEditing ? (
                      <TextField
                        size="small"
                        placeholder="Reason for change..."
                        value={editReason}
                        onChange={(e) => setEditReason(e.target.value)}
                        fullWidth
                      />
                    ) : (
                      <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                        {w.reasonForChange || "Baseline configuration"}
                        {w.changedBy ? ` (by ${w.changedBy})` : ""}
                      </Typography>
                    )}
                  </TableCell>
                  {isAuthorized && (
                    <TableCell align="center">
                      {isEditing ? (
                        <Box sx={{ display: "flex", gap: 0.5 }}>
                          <IconButton size="small" color="primary" onClick={() => saveEdit(w.testCode)}>
                            <CheckIcon fontSize="small" />
                          </IconButton>
                          <IconButton size="small" onClick={cancelEdit}>
                            <CloseIcon fontSize="small" />
                          </IconButton>
                        </Box>
                      ) : (
                        <Tooltip title="Modify test complexity weight">
                          <IconButton size="small" onClick={() => startEdit(w)}>
                            <EditIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}
                    </TableCell>
                  )}
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 1.5, borderTop: 1, borderColor: "divider" }}>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
}
