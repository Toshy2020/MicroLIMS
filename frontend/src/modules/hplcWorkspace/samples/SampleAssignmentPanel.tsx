import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Box,
  Typography,
  Button,
  Alert,
  Stack,
  Tooltip,
  Chip
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import EditNoteIcon from "@mui/icons-material/EditNote";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import VisibilityIcon from "@mui/icons-material/Visibility";
import { toast } from "sonner";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { EligibleSampleTable } from "./EligibleSampleTable";
import { EligibleQualificationTable } from "./EligibleQualificationTable";
import { ReasonDialog } from "../../laboratoryConfiguration/masterDataSimple/solutionMaster/ReasonDialog";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { RegisterTable } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { StatusBadge } from "../../../components/StatusBadge";
import { monospaceFontFamily } from "../../../theme/palette";
import { useTechnique } from "../useTechnique";
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
  const navigate = useNavigate();
  const { basePath, label } = useTechnique();

  const [eligibleDialogOpen, setEligibleDialogOpen] = useState(false);
  const [eligibleWsDialogOpen, setEligibleWsDialogOpen] = useState(false);
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
  const assignTooltip = !run.canAssignSamples
    ? run.canAssignSamplesReason || "System suitability must pass first"
    : run.status !== "Open"
    ? "Run is no longer open"
    : !canOperate
    ? "Permission required"
    : "";

  const columns: RegisterColumn<HplcRunSampleSummaryDto>[] = [
    {
      key: "sampleNumber",
      label: "Sample Number",
      sortable: true,
      render: (s) => <span style={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>{s.sampleNumber}</span>
    },
    { key: "batchNumber", label: "Batch", sortable: true, render: (s) => s.batchNumber ?? "—" },
    { key: "productName", label: "Product", sortable: true, render: (s) => s.productName ?? "—" },
    {
      key: "testCode",
      label: "Test Code",
      sortable: true,
      render: (s) => s.workingStandardQualificationId
        ? <Chip label="WS qualification" size="small" color="secondary" />
        : <span style={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>{s.testCode}</span>
    },
    { key: "status", label: "Run Status", sortable: true, render: (s) => <HplcStatusBadge status={s.status} /> },
    {
      key: "submitted",
      label: "Submission",
      sortable: true,
      sortValue: (s) => (s.submitted ? 1 : 0),
      render: (s) => (s.submitted ? <StatusBadge status="Completed" label="Submitted" /> : <StatusBadge status="Pending" />)
    },
    {
      key: "actions",
      label: "Actions",
      align: "right",
      render: (s) => {
        const canRemove = canOperate && run.status === "Open" && s.status === "Assigned" && !s.submitted;
        const entryPath = s.workingStandardQualificationId
          ? `${basePath}/${run.equipmentId}/run/${run.id}/qualification/${s.id}`
          : `${basePath}/${run.equipmentId}/run/${run.id}/sample/${s.id}`;
        return (
          <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
            <Button
              size="small"
              variant={s.submitted ? "outlined" : "contained"}
              color="primary"
              startIcon={s.submitted ? <VisibilityIcon fontSize="small" /> : <EditNoteIcon fontSize="small" />}
              onClick={() =>
                s.isDissolution
                  ? navigate(`/physicochemical/workspace?search=${encodeURIComponent(s.sampleNumber)}`)
                  : navigate(entryPath)
              }
              sx={{ textTransform: "none", py: 0.25 }}
            >
              {s.submitted ? "View Entry" : s.isDissolution ? "Enter Dissolution" : "Enter Replicates"}
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
        );
      }
    }
  ];

  return (
    <Stack spacing={2}>
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

      <Box
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: 1.5
        }}
      >
        <Box>
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
            Assigned Run Samples ({run.samples.length})
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Samples linked to this {label} run for testing and replicate entry.
          </Typography>
        </Box>

        <Stack direction="row" spacing={1.5} sx={{ flexWrap: "wrap" }}>
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

          <Tooltip title={assignTooltip}>
            <span>
              <Button
                variant="outlined"
                color="primary"
                size="small"
                startIcon={<AddIcon />}
                disabled={!canAssign}
                onClick={() => setEligibleWsDialogOpen(true)}
                sx={{ textTransform: "none", fontWeight: 600 }}
              >
                Assign Working Standard...
              </Button>
            </span>
          </Tooltip>
        </Stack>
      </Box>

      <RegisterTable
        columns={columns}
        rows={run.samples}
        getRowId={(s) => s.id}
        empty={{
          title: "No samples assigned",
          description: "Click “Assign Samples...” to add test orders to this run."
        }}
      />

      {/* Eligible Samples Dialog */}
      <EligibleSampleTable
        open={eligibleDialogOpen}
        runId={run.id}
        onClose={() => setEligibleDialogOpen(false)}
        onAssigned={onRunUpdated}
      />

      {/* Eligible Working Standard Qualifications Dialog */}
      <EligibleQualificationTable
        open={eligibleWsDialogOpen}
        runId={run.id}
        onClose={() => setEligibleWsDialogOpen(false)}
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
        label="Reason for Removal"
        placeholder="Explain why this sample is being removed from the run..."
      />
    </Stack>
  );
}
