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
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
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
  const { t } = useTranslation("icpWorkspace");
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
      toast.success(t("samples.panel.removedToast", { sampleNumber: sampleToRemove.sampleNumber }));
      setRemoveDialogOpen(false);
      setSampleToRemove(null);
      onRunUpdated?.();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setRemoveError(e.response?.data?.message ?? e.message ?? t("samples.panel.removeError"));
    } finally {
      setRemoving(false);
    }
  };

  const canAssign = run.canAssignSamples && canOperate && run.status === "Open";
  const assignTooltip = !run.canAssignSamples
    ? run.canAssignSamplesReason || t("samples.panel.tooltipCal")
    : run.status !== "Open"
    ? t("samples.panel.tooltipClosed")
    : !canOperate
    ? t("samples.panel.tooltipPermission")
    : "";

  return (
    <Stack spacing={2.5}>
      {/* Gating alert */}
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
              {t("samples.panel.assignedTitle", { count: samples.length })}
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              {t("samples.panel.assignedSubtitle")}
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
                {t("samples.panel.assignSamples")}
              </Button>
            </span>
          </Tooltip>
        </Box>

        {samples.length === 0 ? (
          <Box sx={{ py: 6, textAlign: "center" }}>
            <ScienceOutlinedIcon sx={{ fontSize: 48, color: "text.disabled", mb: 1 }} />
            <Typography variant="body1" sx={{ fontWeight: 600, color: "text.secondary" }}>
              {t("samples.panel.emptyTitle")}
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
              {run.canAssignSamples
                ? t("samples.panel.emptyDescription")
                : run.canAssignSamplesReason || t("samples.panel.emptyLocked")}
            </Typography>
          </Box>
        ) : (
          <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1.5, overflowX: "auto" }}>
            <Table size="small">
              <TableHead sx={tableHeadSx(theme)}>
                <TableRow>
                  <TableCell>{t("samples.panel.columns.sampleNumber")}</TableCell>
                  <TableCell>{t("samples.panel.columns.batch")}</TableCell>
                  <TableCell>{t("samples.panel.columns.product")}</TableCell>
                  <TableCell>{t("samples.panel.columns.testCode")}</TableCell>
                  <TableCell>{t("samples.panel.columns.status")}</TableCell>
                  <TableCell align="right">{t("samples.panel.columns.actions")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {samples.map((s) => {
                  const canRemove = canOperate && (run.status === "Open" || run.status === "Completed") && s.status === "Assigned";
                  const entryUrl = ICP_ROUTES.sampleEntry(run.equipmentId, run.id, s.id);

                  return (
                    <TableRow key={s.id} hover>
                      <TableCell sx={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>
                        <div><Ltr>{s.sampleNumber}</Ltr></div>
                        {s.subjectName && (
                          <Typography variant="caption" sx={{ display: "block", color: "text.secondary", fontWeight: 400 }}>
                            {s.subjectName}
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell>{s.batchNumber ? <Ltr>{s.batchNumber}</Ltr> : "—"}</TableCell>
                      <TableCell>{s.productName ?? "—"}</TableCell>
                      <TableCell sx={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>
                        <Ltr>{s.testCode}</Ltr>
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
                            {t("samples.panel.enterResults")}
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
