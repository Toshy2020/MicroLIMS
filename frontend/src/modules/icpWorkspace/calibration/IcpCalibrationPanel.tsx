import { useState, useEffect, useMemo } from "react";
import {
  Box,
  Paper,
  Typography,
  Button,
  Stack,
  Alert,
  Tooltip,
  Divider,
  useTheme
} from "@mui/material";
import SaveIcon from "@mui/icons-material/Save";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import { toast } from "sonner";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import { MaterialService } from "../../inventory/materials/services/MaterialService";
import { IcpCalibrationLotPicker, StandardMaterialOption } from "./IcpCalibrationLotPicker";
import { IcpCalibrationElementTable } from "./IcpCalibrationElementTable";
import { buildSaveCalibrationPayload, type CalibrationRowInput } from "./calibrationPayload";
import { IcpReportUploadPanel } from "../evidence/IcpReportUploadPanel";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { StatusBadge } from "../../../components/StatusBadge";
import type { IcpRunDto } from "../types";

export interface IcpCalibrationPanelProps {
  run: IcpRunDto;
  canOperate: boolean;
  onRunUpdated: () => void;
}

export function IcpCalibrationPanel({
  run,
  canOperate,
  onRunUpdated
}: IcpCalibrationPanelProps) {
  const theme = useTheme();
  const cal = run.calibration;
  const isConfirmed = cal?.status === "Confirmed";

  // Usable reference standard lots
  const [usableLots, setUsableLots] = useState<StandardMaterialOption[]>([]);
  const [loadingLots, setLoadingLots] = useState(true);

  // Lot selections
  const [selectedCalLotId, setSelectedCalLotId] = useState<number | null>(
    cal?.calibrationStandardMaterialId ?? null
  );

  // The elements the analyst calibrated, with the r from the curve
  const [rows, setRows] = useState<CalibrationRowInput[]>([]);

  // Actions state
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  // E-Signature dialog state
  const [signOpen, setSignOpen] = useState(false);
  const [signComment, setSignComment] = useState("");

  // Sync state when run updates
  useEffect(() => {
    setSelectedCalLotId(cal?.calibrationStandardMaterialId ?? null);
    setRows(
      (cal?.elements ?? []).map((el) => ({
        symbol: el.symbol,
        correlationR: el.correlationR != null ? String(el.correlationR) : ""
      }))
    );
  }, [cal]);

  // Load usable reference standard lots
  useEffect(() => {
    let active = true;
    setLoadingLots(true);
    MaterialService.getUsableReferenceStandards()
      .then((data: unknown) => {
        if (!active) return;
        setUsableLots(Array.isArray(data) ? (data as StandardMaterialOption[]) : []);
      })
      .catch(() => {
        if (active) setUsableLots([]);
      })
      .finally(() => {
        if (active) setLoadingLots(false);
      });

    return () => {
      active = false;
    };
  }, []);

  // Check if expired
  const isExpired = useMemo(() => {
    if (!isConfirmed || !cal?.expiresAt) return false;
    return new Date(cal.expiresAt).getTime() < Date.now();
  }, [isConfirmed, cal?.expiresAt]);

  const handleSave = async () => {
    if (!cal) return;
    setSaving(true);
    setSaveError(null);

    const payload = buildSaveCalibrationPayload(selectedCalLotId, rows);

    try {
      await IcpWorkspaceService.saveCalibration(run.id, payload);
      toast.success("Calibration saved.");
      onRunUpdated();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setSaveError(e.response?.data?.message ?? e.message ?? "Could not save calibration.");
    } finally {
      setSaving(false);
    }
  };

  const handleConfirmSignature = async (password: string) => {
    await IcpWorkspaceService.confirmCalibration(run.id, {
      password,
      comment: signComment.trim() || null
    });
    setSignOpen(false);
    setSignComment("");
    toast.success("Calibration confirmed successfully.");
    onRunUpdated();
  };

  return (
    <Stack spacing={3}>
      {/* Confirmed / Expired State Card */}
      {isConfirmed && (
        <Paper
          elevation={0}
          sx={{
            p: 2.5,
            borderRadius: 2,
            border: `1px solid ${isExpired ? theme.palette.warning.main : theme.palette.success.main}`,
            backgroundColor: isExpired
              ? theme.palette.mode === "dark" ? "rgba(237, 108, 2, 0.08)" : "#fff8e1"
              : theme.palette.mode === "dark" ? "rgba(46, 125, 50, 0.08)" : "#f1f8e9"
          }}
        >
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 1.5 }}>
            <Box>
              <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                {isExpired ? (
                  <WarningAmberIcon color="warning" />
                ) : (
                  <CheckCircleIcon color="success" />
                )}
                <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 16 }}>
                  Calibration {isExpired ? "Expired" : "Confirmed"} ({cal.code})
                </Typography>
              </Stack>
              <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
                Confirmed by <strong>{cal.confirmedByUserName ?? "Analyst"}</strong> on{" "}
                {cal.confirmedAt ? new Date(cal.confirmedAt).toLocaleString() : "—"}.
              </Typography>
            </Box>

            <Box sx={{ textAlign: { xs: "left", sm: "right" } }}>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                Validity window
              </Typography>
              <Typography
                variant="body2"
                sx={{
                  fontWeight: 700,
                  color: isExpired ? "error.main" : "text.primary"
                }}
              >
                Valid until: {cal.expiresAt ? new Date(cal.expiresAt).toLocaleString() : "—"}
              </Typography>
            </Box>
          </Box>

          {isExpired && (
            <Alert severity="warning" sx={{ mt: 2 }}>
              This calibration has expired. A fresh calibration run is required before further sample testing.
            </Alert>
          )}

          <Divider sx={{ my: 1.5 }} />

          <Box sx={{ display: "flex", gap: 2, flexWrap: "wrap" }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Calibration Standard Lot:
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {cal.calibrationStandardLotLabel || "Lot unrecorded"}
              </Typography>
            </Box>

          </Box>
        </Paper>
      )}

      {/* Main Calibration Form Card */}
      <Paper
        elevation={0}
        sx={{
          p: 2.5,
          borderRadius: 2,
          border: `1px solid ${theme.palette.divider}`,
          backgroundColor: theme.palette.background.paper
        }}
      >
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2, flexWrap: "wrap", gap: 1.5 }}>
          <Box>
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              Calibration standard & elements
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              Choose the standard lot, then list each metal calibrated and the correlation coefficient (r) read from the calibration curve.
            </Typography>
          </Box>

          <StatusBadge status={cal?.status ?? "Pending"} />
        </Box>

        {saveError && (
          <Alert severity="error" sx={{ mb: 2 }} onClose={() => setSaveError(null)}>
            {saveError}
          </Alert>
        )}

        {/* Standard lot (visible when Pending) */}
        {!isConfirmed && (
          <Box sx={{ mb: 3 }}>
            <IcpCalibrationLotPicker
              selectedLotId={selectedCalLotId}
              onSelectLotId={setSelectedCalLotId}
              usableLots={usableLots}
              loadingLots={loadingLots}
              disabled={!canOperate || run.status !== "Open"}
            />
          </Box>
        )}

        {/* Elements */}
        <Box sx={{ mb: 3 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
            Calibrated elements
          </Typography>
          <IcpCalibrationElementTable
            rows={rows}
            onRowsChange={setRows}
            savedElements={cal?.elements || []}
            minCorrelation={cal?.minCorrelation ?? 0.999}
            disabled={isConfirmed || !canOperate || run.status !== "Open"}
          />
        </Box>

        {/* Action Buttons for Pending */}
        {!isConfirmed && canOperate && run.status === "Open" && (
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", pt: 1, flexWrap: "wrap", gap: 1.5 }}>
            <Button
              variant="outlined"
              startIcon={<SaveIcon />}
              onClick={handleSave}
              disabled={saving}
              sx={{ textTransform: "none" }}
            >
              {saving ? "Saving..." : "Save calibration"}
            </Button>

            <Tooltip title={!run.canConfirmCalibration ? (run.canConfirmCalibrationReason || "Cannot confirm calibration") : ""}>
              <span>
                <Button
                  variant="contained"
                  color="primary"
                  startIcon={<VerifiedUserIcon />}
                  onClick={() => setSignOpen(true)}
                  disabled={!run.canConfirmCalibration}
                  sx={{ textTransform: "none" }}
                >
                  Confirm Calibration
                </Button>
              </span>
            </Tooltip>
          </Box>
        )}

        {!isConfirmed && !run.canConfirmCalibration && run.canConfirmCalibrationReason && run.status === "Open" && (
          <Alert severity="info" sx={{ mt: 2 }}>
            {run.canConfirmCalibrationReason}
          </Alert>
        )}
      </Paper>

      {/* Calibration Report Upload Panel */}
      <IcpReportUploadPanel
        runId={run.id}
        context="Calibration"
        kind="CalibrationReport"
        evidenceList={run.evidence}
        title="Calibration curve report"
        description="Upload the Syngistix report showing the calibration curve. It is the evidence for the r values above and is required to confirm."
        disabled={isConfirmed || !canOperate || run.status !== "Open"}
        onChanged={onRunUpdated}
      />

      {/* 21 CFR Part 11 Electronic Signature Dialog */}
      <SignatureDialog
        open={signOpen}
        meaningStatement="I confirm the ICP-OES is calibrated and ready for testing."
        showComment={true}
        comment={signComment}
        onCommentChange={setSignComment}
        onCancel={() => {
          setSignOpen(false);
          setSignComment("");
        }}
        onConfirm={handleConfirmSignature}
      />
    </Stack>
  );
}
