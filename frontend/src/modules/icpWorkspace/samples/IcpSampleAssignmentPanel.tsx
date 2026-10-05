import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Box,
  Typography,
  Button,
  Alert,
  Stack,
  Tooltip,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import EditNoteIcon from "@mui/icons-material/EditNote";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { toast } from "sonner";
import { IcpStatusBadge } from "../components/IcpStatusBadge";
import { IcpEligibleSampleDialog } from "./IcpEligibleSampleDialog";
import { ReasonDialog } from "../../laboratoryConfiguration/masterDataSimple/solutionMaster/ReasonDialog";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import { ICP_ROUTES } from "../routes";
import { tableHeadSx } from "../../../theme";
import { monospaceFontFamily } from "../../../theme/palette";
import type { IcpRunDto, IcpRunSampleSummaryDto } from "../types";

export interface IcpSampleAssignmentPanelProps {
  run: IcpRunDto;
  canOperate: boolean;
  onRunUpdated?: () => void;
}

export function IcpSampleAssignmentPanel({
  run,
  canOperate,
  onRunUpdated
}: IcpSampleAssignmentPanelProps) {
  const theme = useTheme();
  const navigate = useNavigate();

  const [eligibleDialogOpen, setEligibleDialogOpen] = useState(false);
  const [removeDialogOpen, setRemoveDialogOpen] = useState(false);
  const [sampleToRemove, setSampleToRemove] = useState<IcpRunSampleSummaryDto | null>(null);
  const [removeReason, setRemoveReason] = useState("");
  const [removing, setRemoving] = useState(false);
  const [removeError, setRemoveError] = useState<string | null>(null);

  const samples = run.samples || [];

  const handleOpenRemoveDialog = (sample: IcpRunSampleSummaryDto) => {
    setSampleToRemove(sample);
    setRemoveReason("");
    setRemoveError(null);
    setRemoveDialogOpen(true);
  };

  const handleConfirmRemove = async () => {
    if (!sampleToRemove || !removeReason.trim()) return;
    setRemoving(true);
    setRemoveError(null);
    try {
      await IcpWorkspaceService.removeSample(run.id, sampleToRemove.id, removeReason.trim());
      toast.success(`Sample ${sampleToRemove.sampleNumber} removed from run.`);
      setRemoveDialogOpen(false);
      setSampleToRemove(null);
      onRunUpdated?.();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setRemoveError(e.response?.data?.message ?? e.message ?? "Could not remove sample.");
    } finally {
      setRemoving(false);
    }
  };

  const canAssign = run.canAssignSamples && canOperate && run.status === "Open";
  const assignTooltip = !run.canAssignSamples
    ? run.canAssignSamplesReason || "Sample assignment locked until calibration is confirmed"
    : run.status !== "Open"
    ? "Run is no longer open"
    : !canOperate
    ? "Permission required"
    : "";

  return (
    <Stack spacing={2.5}>
      {/* Gating alert */}
      {!run.canAssignSamples ? (
        <Alert severity="warning" icon={<LockOutlinedIcon />}>
          <strong>Sample assignment is locked.</strong>{" "}
          {run.canAssignSamplesReason || "Sample assignment is locked until calibration is confirmed and element availability is verified."}
        </Alert>
      ) : (
        <Alert severity="success">
          Calibration confirmed. Eligible test orders can be assigned to this ICP run.
        </Alert>
      )}

      <Paper
        elevation={0}
        sx={{
          p: 2.5,
          borderRadius: 2,
          border: `1px solid ${theme.palette.divider}`,
          backgroundColor: theme.palette.background.paper
        }}
      >
        <Box
          sx={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: 1.5,
            mb: 2
          }}
        >
          <Box>
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              Assigned Run Samples ({samples.length})
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              Samples linked to this ICP run for analysis and replicate entry.
            </Typography>
          </Box>

          <Tooltip title={assignTooltip}>
            <span>
              <Button
                variant="contained"
                color="primary"
                size="small"
                startIcon={<AddIcon />}
                disabled={!canAssign}
                onClick={() => setEligibleDialogOpen(true)}
                sx={{ textTransform: "none", fontWeight: 600 }}
              >
                Assign Samples...
              </Button>
            </span>
          </Tooltip>
        </Box>

        {samples.length === 0 ? (
          <Box sx={{ py: 6, textAlign: "center" }}>
            <ScienceOutlinedIcon sx={{ fontSize: 48, color: "text.disabled", mb: 1 }} />
            <Typography variant="body1" sx={{ fontWeight: 600, color: "text.secondary" }}>
              No samples assigned to this run yet
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
              {run.canAssignSamples
                ? "Click “Assign Samples...” to link eligible test orders to this run."
                : run.canAssignSamplesReason || "Sample assignment is locked."}
            </Typography>
          </Box>
        ) : (
          <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1.5, overflowX: "auto" }}>
            <Table size="small">
              <TableHead sx={tableHeadSx(theme)}>
                <TableRow>
                  <TableCell>Sample Number</TableCell>
                  <TableCell>Batch</TableCell>
                  <TableCell>Product</TableCell>
                  <TableCell>Test Code</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell align="right">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {samples.map((s) => {
                  const canRemove = canOperate && (run.status === "Open" || run.status === "Completed") && s.status === "Assigned";
                  const entryUrl = ICP_ROUTES.sampleEntry(run.equipmentId, run.id, s.id);

                  return (
                    <TableRow key={s.id} hover>
                      <TableCell sx={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>
                        {s.sampleNumber}
                      </TableCell>
                      <TableCell>{s.batchNumber ?? "—"}</TableCell>
                      <TableCell>{s.productName ?? "—"}</TableCell>
                      <TableCell sx={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>
                        {s.testCode}
                      </TableCell>
                      <TableCell>
                        <IcpStatusBadge status={s.status} />
                      </TableCell>
                      <TableCell align="right">
                        <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                          <Button
                            size="small"
                            variant="contained"
                            color="primary"
                            startIcon={<EditNoteIcon fontSize="small" />}
                            onClick={() => navigate(entryUrl)}
                            sx={{ textTransform: "none", py: 0.25 }}
                          >
                            Enter Replicates
                          </Button>

                          {canRemove && (
                            <Button
                              size="small"
                              variant="outlined"
                              color="error"
                              startIcon={<DeleteOutlineIcon fontSize="small" />}
                              onClick={() => handleOpenRemoveDialog(s)}
                              sx={{ textTransform: "none", py: 0.25 }}
                            >
                              Remove
                            </Button>
                          )}
                        </Stack>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Paper>

      {/* Eligible Samples Dialog */}
      <IcpEligibleSampleDialog
        open={eligibleDialogOpen}
        runId={run.id}
        onClose={() => setEligibleDialogOpen(false)}
        onAssigned={() => {
          onRunUpdated?.();
        }}
      />

      {/* Remove Sample Reason Dialog */}
      <ReasonDialog
        open={removeDialogOpen}
        title={`Remove Sample ${sampleToRemove?.sampleNumber ?? ""}`}
        onClose={() => !removing && setRemoveDialogOpen(false)}
        onConfirm={handleConfirmRemove}
        confirmText="Remove Sample"
        confirmColor="error"
        loading={removing}
        loadingText="Removing..."
        error={removeError}
        reason={removeReason}
        onReasonChange={setRemoveReason}
        label="Reason for Removal *"
        placeholder="Explain why this sample is being removed from the run..."
      />
    </Stack>
  );
}
