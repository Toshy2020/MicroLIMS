import { useEffect, useState } from "react";
import {
  Box,
  Typography,
  Button,
  Chip,
  IconButton,
  Tooltip,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  CircularProgress,
  Alert,
  TextField,
  Stack
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import BlockIcon from "@mui/icons-material/Block";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { SignatureDialog } from "../../../../components/SignatureDialog";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import { AddLogEntryDialog } from "./AddLogEntryDialog";
import { formatLabDate, formatLabDateTime } from "../../../../utils/formatDate";
import type {
  MaintenanceEntryType,
  MaintenanceLogEntryResponse
} from "../types/equipmentTypes";
import { MAINTENANCE_ENTRY_TYPE_LABELS } from "../types/equipmentTypes";

interface Props {
  equipmentId: number;
  equipmentCode?: string;
  onLogChanged?: () => void;
}

const TYPE_COLORS: Record<MaintenanceEntryType, "primary" | "warning" | "default"> = {
  PreventiveMaintenance: "primary",
  BreakdownRepair: "warning",
  Other: "default"
};

export function MaintenanceLogSection({ equipmentId, equipmentCode = "", onLogChanged }: Props) {
  const [entries, setEntries] = useState<MaintenanceLogEntryResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isAddOpen, setIsAddOpen] = useState(false);

  // Voiding state
  const [voidTarget, setVoidTarget] = useState<MaintenanceLogEntryResponse | null>(null);
  const [voidReason, setVoidReason] = useState("");
  const [voidSigning, setVoidSigning] = useState(false);
  const [voidError, setVoidError] = useState<string | null>(null);

  const loadEntries = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await EquipmentPlanService.getMaintenanceLog(equipmentId);
      setEntries(data);
    } catch {
      setError("Failed to load maintenance log entries.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadEntries();
  }, [equipmentId]);

  const handleVoidSign = async (password: string) => {
    if (!voidTarget) return;
    await EquipmentPlanService.voidLogEntry(voidTarget.id, {
      reason: voidReason.trim(),
      password
    });
    setVoidSigning(false);
    setVoidTarget(null);
    setVoidReason("");
    await loadEntries();
    onLogChanged?.();
  };

  const handleVoidCancel = () => {
    setVoidTarget(null);
    setVoidReason("");
    setVoidError(null);
    setVoidSigning(false);
  };

  return (
    <Box sx={{ p: 2 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
        <Box>
          <Typography variant="h6" sx={{ fontSize: 16, fontWeight: 700, color: "text.primary" }}>
            Equipment Maintenance Logbook
          </Typography>
          <Typography sx={{ fontSize: 13, color: "text.secondary", mt: 0.25 }}>
            Every entry is signed and kept. A wrong entry is voided with a reason, never deleted.
          </Typography>
        </Box>
        <Button
          id="add-log-entry-btn"
          variant="contained"
          size="small"
          startIcon={<AddIcon fontSize="small" />}
          onClick={() => setIsAddOpen(true)}
          sx={{ textTransform: "none", fontSize: 12 }}
        >
          Add Log Entry
        </Button>
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
          <CircularProgress size={24} />
        </Box>
      ) : entries.length === 0 ? (
        <Paper
          variant="outlined"
          sx={{
            p: 3,
            textAlign: "center",
            bgcolor: "background.default",
            color: "text.secondary"
          }}
        >
          <Typography sx={{ fontSize: 13, fontStyle: "italic" }}>
            No maintenance log entries recorded yet. Click &quot;Add Log Entry&quot; to log servicing or repairs.
          </Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead sx={{ bgcolor: "background.default" }}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Date</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Type</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Work Done</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Parts Replaced</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Findings</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Performed By</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Status</TableCell>
                <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {entries.map((entry) => {
                const isVoided = Boolean(entry.voidedAt);
                return (
                  <TableRow
                    key={entry.id}
                    sx={{
                      opacity: isVoided ? 0.6 : 1,
                      bgcolor: isVoided ? "action.hover" : "inherit"
                    }}
                  >
                    <TableCell sx={{ fontSize: 12, whiteSpace: "nowrap" }}>
                      {formatLabDate(entry.performedOn)}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>
                      <Chip
                        label={MAINTENANCE_ENTRY_TYPE_LABELS[entry.entryType] ?? entry.entryType}
                        color={TYPE_COLORS[entry.entryType] ?? "default"}
                        size="small"
                        sx={{ fontSize: 11, height: 20 }}
                      />
                    </TableCell>
                    <TableCell sx={{ fontSize: 12, maxWidth: 220 }}>
                      <Typography sx={{ fontSize: 12, fontWeight: 500 }}>
                        {entry.workDone}
                      </Typography>
                    </TableCell>
                    <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                      {entry.partsReplaced || "—"}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                      {entry.findings || "—"}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                      {entry.performedByUserName || `User #${entry.performedByUserId}`}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>
                      {isVoided ? (
                        <Tooltip
                          title={`Voided by user #${entry.voidedByUserId}: "${entry.voidReason}" at ${
                            entry.voidedAt ? formatLabDateTime(entry.voidedAt) : ""
                          }`}
                        >
                          <Chip
                            label="Voided"
                            color="error"
                            size="small"
                            sx={{ fontSize: 11, height: 20 }}
                          />
                        </Tooltip>
                      ) : (
                        <Chip
                          label="Recorded"
                          color="success"
                          variant="outlined"
                          size="small"
                          sx={{ fontSize: 11, height: 20 }}
                        />
                      )}
                    </TableCell>
                    <TableCell sx={{ textAlign: "right", whiteSpace: "nowrap" }}>
                      {!isVoided && (
                        <Tooltip title="Void this entry (requires reason & electronic signature)">
                          <IconButton
                            size="small"
                            color="error"
                            onClick={() => {
                              setVoidTarget(entry);
                              setVoidReason("");
                              setVoidSigning(false);
                              setVoidError(null);
                            }}
                          >
                            <BlockIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Add Log Entry Dialog */}
      <AddLogEntryDialog
        open={isAddOpen}
        equipmentId={equipmentId}
        equipmentCode={equipmentCode}
        onClose={() => setIsAddOpen(false)}
        onSuccess={() => {
          setIsAddOpen(false);
          loadEntries();
          onLogChanged?.();
        }}
      />

      {/* Void Entry Dialog */}
      <FloatingDialog
        open={Boolean(voidTarget) && !voidSigning}
        title="Void Maintenance Log Entry"
        onClose={handleVoidCancel}
        maxWidth="xs"
        actions={
          <>
            <Button
              onClick={handleVoidCancel}
              color="inherit"
            >
              Cancel
            </Button>
            <Button
              variant="contained"
              color="error"
              onClick={() => {
                if (!voidReason.trim()) {
                  setVoidError("Void reason is required.");
                  return;
                }
                setVoidSigning(true);
              }}
              disabled={!voidReason.trim()}
            >
              Continue to sign
            </Button>
          </>
        }
      >
        <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 2 }}>
          Voiding removes the effect of this entry while preserving it in the audit trail. A valid reason and password signature are required.
        </Typography>

        {voidError && (
          <Alert severity="error" sx={{ mb: 2 }} onClose={() => setVoidError(null)}>
            {voidError}
          </Alert>
        )}

        {voidTarget && (
          <Box sx={{ mb: 2, p: 1.5, bgcolor: "background.default", borderRadius: 1 }}>
            <Typography sx={{ fontSize: 12, fontWeight: 600 }}>
              {formatLabDate(voidTarget.performedOn)} · {MAINTENANCE_ENTRY_TYPE_LABELS[voidTarget.entryType]}
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.5 }}>
              {voidTarget.workDone}
            </Typography>
          </Box>
        )}

        <Stack spacing={2}>
          <TextField
            id="void-log-reason"
            label="Reason for Voiding"
            multiline
            rows={2}
            size="small"
            required
            fullWidth
            value={voidReason}
            onChange={(e) => setVoidReason(e.target.value)}
            placeholder="Explain why this entry is being voided"
          />
        </Stack>
      </FloatingDialog>

      {voidTarget && (
        <SignatureDialog
          open={voidSigning}
          meaningStatement={`I am voiding logbook entry #${voidTarget.id} for ${equipmentCode}. Reason: ${voidReason.trim()}.`}
          onCancel={() => setVoidSigning(false)}
          onConfirm={handleVoidSign}
        />
      )}
    </Box>
  );
}
