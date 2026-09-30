import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Box,
  Paper,
  Typography,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  Alert,
  Stack,
  Tooltip,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import EditNoteIcon from "@mui/icons-material/EditNote";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import VisibilityIcon from "@mui/icons-material/Visibility";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import HourglassEmptyIcon from "@mui/icons-material/HourglassEmpty";
import { toast } from "sonner";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { EligibleSampleTable } from "./EligibleSampleTable";
import { ReasonDialog } from "../../laboratoryConfiguration/masterDataSimple/solutionMaster/ReasonDialog";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { tableHeadSx } from "../../../theme";
import type { HplcRunDto, HplcRunSampleSummaryDto } from "../types";

export interface SampleAssignmentPanelProps {
  run: HplcRunDto;
  canOperate: boolean;
  onRunUpdated: () => void;
}

export function SampleAssignmentPanel({
  run,
  canOperate,
  onRunUpdated
}: SampleAssignmentPanelProps) {
  const theme = useTheme();
  const navigate = useNavigate();

  const [eligibleDialogOpen, setEligibleDialogOpen] = useState(false);
  const [removeDialogOpen, setRemoveDialogOpen] = useState(false);
  const [sampleToRemove, setSampleToRemove] = useState<HplcRunSampleSummaryDto | null>(null);
  const [removeReason, setRemoveReason] = useState("");
  const [removing, setRemoving] = useState(false);
  const [removeError, setRemoveError] = useState<string | null>(null);

  const handleOpenRemoveDialog = (sample: HplcRunSampleSummaryDto) => {
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
      await HplcWorkspaceService.removeSample(run.id, sampleToRemove.id, removeReason.trim());
      toast.success(`Sample ${sampleToRemove.sampleNumber} removed from run.`);
      setRemoveDialogOpen(false);
      setSampleToRemove(null);
      onRunUpdated();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setRemoveError(e.response?.data?.message ?? e.message ?? "Could not remove sample.");
    } finally {
      setRemoving(false);
    }
  };

  const canAssign = run.canAssignSamples && canOperate && run.status === "Open";

  return (
    <Stack spacing={2.5}>
      {/* Gating Alert */}
      {!run.canAssignSamples ? (
        <Alert severity="warning" icon={<LockOutlinedIcon />}>
          <strong>Sample assignment is locked.</strong>{" "}
          {run.canAssignSamplesReason || "Sample assignment is locked until system suitability passes."}
        </Alert>
      ) : (
        <Alert severity="success">
          System suitability is passed. Samples can be assigned and tested in this run.
        </Alert>
      )}

      {/* Main Panel */}
      <Paper
        elevation={0}
        sx={{
          borderRadius: 2,
          border: `1px solid ${theme.palette.divider}`,
          overflow: "hidden"
        }}
      >
        <Box
          sx={{
            p: 2,
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: 1.5,
            borderBottom: `1px solid ${theme.palette.divider}`
          }}
        >
          <Box>
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              Assigned Run Samples ({run.samples.length})
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              Samples linked to this HPLC run for testing and replicate entry.
            </Typography>
          </Box>

          <Tooltip
            title={
              !run.canAssignSamples
                ? run.canAssignSamplesReason || "System suitability must pass first"
                : run.status !== "Open"
                ? "Run is no longer open"
                : !canOperate
                ? "Permission required"
                : ""
            }
          >
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

        <TableContainer>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell>Sample Number</TableCell>
                <TableCell>Batch</TableCell>
                <TableCell>Product</TableCell>
                <TableCell>Test Code</TableCell>
                <TableCell>Run Status</TableCell>
                <TableCell>Submission</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {run.samples.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} sx={{ textAlign: "center", py: 5, color: "text.secondary" }}>
                    No samples currently assigned to this run. Click &ldquo;Assign Samples...&rdquo; to add test orders.
                  </TableCell>
                </TableRow>
              ) : (
                run.samples.map((s) => {
                  const isAssigned = s.status === "Assigned";
                  const canRemove =
                    canOperate && run.status === "Open" && isAssigned && !s.submitted;

                  return (
                    <TableRow key={s.id} hover>
                      <TableCell sx={{ fontWeight: 600 }}>{s.sampleNumber}</TableCell>
                      <TableCell>{s.batchNumber ?? "—"}</TableCell>
                      <TableCell>{s.productName ?? "—"}</TableCell>
                      <TableCell sx={{ fontWeight: 600 }}>{s.testCode}</TableCell>
                      <TableCell>
                        <HplcStatusBadge status={s.status} />
                      </TableCell>
                      <TableCell>
                        {s.submitted ? (
                          <Chip
                            icon={<CheckCircleOutlinedIcon fontSize="small" />}
                            label="Submitted"
                            color="success"
                            size="small"
                            variant="outlined"
                          />
                        ) : (
                          <Chip
                            icon={<HourglassEmptyIcon fontSize="small" />}
                            label="Pending"
                            color="default"
                            size="small"
                            variant="outlined"
                          />
                        )}
                      </TableCell>
                      <TableCell align="right">
                        <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                          <Button
                            size="small"
                            variant={s.submitted ? "outlined" : "contained"}
                            color="primary"
                            startIcon={s.submitted ? <VisibilityIcon fontSize="small" /> : <EditNoteIcon fontSize="small" />}
                            onClick={() =>
                              navigate(
                                `/hplc-workspace/${run.equipmentId}/run/${run.id}/sample/${s.id}`
                              )
                            }
                            sx={{ textTransform: "none", py: 0.25 }}
                          >
                            {s.submitted ? "View Entry" : "Enter Replicates"}
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
                })
              )}
            </TableBody>
          </Table>
        </TableContainer>
      </Paper>

      {/* Eligible Samples Dialog */}
      <EligibleSampleTable
        open={eligibleDialogOpen}
        runId={run.id}
        onClose={() => setEligibleDialogOpen(false)}
        onAssigned={onRunUpdated}
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
