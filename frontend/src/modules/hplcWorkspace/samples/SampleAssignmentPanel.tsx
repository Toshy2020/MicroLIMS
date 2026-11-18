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
import { workspacePathForCategory } from "../../../routes/physchemWorkspacePath";
import { ReasonDialog } from "../../laboratoryConfiguration/masterDataSimple/solutionMaster/ReasonDialog";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { RegisterTable } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
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
  const { t } = useTranslation("hplcWorkspace");
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
      toast.success(t("samples.panel.removedToast", { sampleNumber: sampleToRemove.sampleNumber }));
      setRemoveDialogOpen(false);
      setSampleToRemove(null);
      onRunUpdated();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setRemoveError(e.response?.data?.message ?? e.message ?? t("samples.panel.removeError"));
    } finally {
      setRemoving(false);
    }
  };

  const canAssign = run.canAssignSamples && canOperate && run.status === "Open";
  const assignTooltip = !run.canAssignSamples
    ? run.canAssignSamplesReason || t("samples.panel.tooltipSst")
    : run.status !== "Open"
    ? t("samples.panel.tooltipClosed")
    : !canOperate
    ? t("samples.panel.tooltipPermission")
    : "";

  const columns: RegisterColumn<HplcRunSampleSummaryDto>[] = [
    {
      key: "sampleNumber",
      label: t("samples.panel.columns.sampleNumber"),
      sortable: true,
      render: (s) => (
        <div>
          <span style={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}><Ltr>{s.sampleNumber}</Ltr></span>
          {s.subjectName && (
            <Typography variant="caption" sx={{ display: "block", color: "text.secondary" }}>
              {s.subjectName}
            </Typography>
          )}
        </div>
      )
    },
    { key: "batchNumber", label: t("samples.panel.columns.batch"), sortable: true, render: (s) => (s.batchNumber ? <Ltr>{s.batchNumber}</Ltr> : "—") },
    { key: "productName", label: t("samples.panel.columns.product"), sortable: true, render: (s) => s.productName ?? "—" },
    {
      key: "testCode",
      label: t("samples.panel.columns.testCode"),
      sortable: true,
      render: (s) => s.workingStandardQualificationId
        ? <Chip label={t("samples.panel.wsQualification")} size="small" color="secondary" />
        : <span style={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}><Ltr>{s.testCode}</Ltr></span>
    },
    { key: "status", label: t("samples.panel.columns.runStatus"), sortable: true, render: (s) => <HplcStatusBadge status={s.status} /> },
    {
      key: "submitted",
      label: t("samples.panel.columns.submission"),
      sortable: true,
      sortValue: (s) => (s.submitted ? 1 : 0),
      render: (s) => (s.submitted ? <StatusBadge status="Completed" label={t("samples.panel.submitted")} /> : <StatusBadge status="Pending" />)
    },
    {
      key: "actions",
      label: t("samples.panel.columns.actions"),
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
                  ? navigate(`${workspacePathForCategory("FinishedProduct")}?search=${encodeURIComponent(s.sampleNumber)}`)
                  : navigate(entryPath)
              }
              sx={{ textTransform: "none", py: 0.25 }}
            >
              {s.submitted
                ? t("samples.panel.viewEntry")
                : s.isDissolution
                ? t("samples.panel.enterDissolution")
                : t("samples.panel.enterReplicates")}
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
                {t("samples.panel.remove")}
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
          <strong>{t("samples.panel.lockedBold")}</strong>{" "}
          {run.canAssignSamplesReason || t("samples.panel.lockedFallback")}
        </Alert>
      ) : (
        <Alert severity="success">
          {t("samples.panel.unlockedAlert")}
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
            {t("samples.panel.assignedTitle", { count: run.samples.length })}
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {t("samples.panel.assignedSubtitle", { label })}
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
                {t("samples.panel.assignSamples")}
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
                {t("samples.panel.assignWs")}
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
          title: t("samples.panel.emptyTitle"),
          description: t("samples.panel.emptyDescription")
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
        title={t("samples.panel.removeDialogTitle", { sampleNumber: sampleToRemove?.sampleNumber ?? "" })}
        onClose={() => !removing && setRemoveDialogOpen(false)}
        onConfirm={handleConfirmRemove}
        confirmText={t("samples.panel.removeConfirmText")}
        confirmColor="error"
        loading={removing}
        loadingText={t("samples.panel.removingText")}
        error={removeError}
        reason={removeReason}
        onReasonChange={setRemoveReason}
        label={t("samples.panel.removeReasonLabel")}
        placeholder={t("samples.panel.removeReasonPlaceholder")}
      />
    </Stack>
  );
}
