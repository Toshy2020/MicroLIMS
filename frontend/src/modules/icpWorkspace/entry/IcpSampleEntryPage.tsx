import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Box,
  Typography,
  Button,
  Stack,
  Alert,
  CircularProgress,
  Paper,
  Tooltip,
  TextField,
  ToggleButtonGroup,
  ToggleButton,
  FormControl,
  FormLabel,
  FormHelperText,
  useTheme
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import SaveIcon from "@mui/icons-material/Save";
import SendIcon from "@mui/icons-material/Send";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import { toast } from "sonner";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import { ICP_ROUTES } from "../routes";
import { IcpStatusBadge } from "../components/IcpStatusBadge";
import { IcpElementStatusStrip } from "../components/IcpElementStatusStrip";
import { StatusBadge } from "../../../components/StatusBadge";
import { ResultSection } from "../../../components/lab";
import { IcpReplicateEntryTable } from "./IcpReplicateEntryTable";
import { IcpPreviewCard } from "./IcpPreviewCard";
import { IcpSendForReviewDialog } from "./IcpSendForReviewDialog";
import { IcpReportUploadPanel } from "../evidence/IcpReportUploadPanel";
import { buildSaveReplicatesPayload, isReplicatesComplete } from "./replicateMapping";
import type { IcpSampleEntryDto, IcpReplicateDto, IcpAmountUnit } from "../types";

export function IcpSampleEntryPage() {
  const theme = useTheme();
  const navigate = useNavigate();
  const { permissions, role } = useAuth();
  const canOperate = permissions.includes(PERMISSIONS.HPLC_OPERATE) || role === "SystemAdministrator";

  const { instrumentId, runId, runSampleId } = useParams<{
    instrumentId: string;
    runId: string;
    runSampleId: string;
  }>();

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sampleEntry, setSampleEntry] = useState<IcpSampleEntryDto | null>(null);

  const [amountUnit, setAmountUnit] = useState<IcpAmountUnit>("Gram");
  const [unitAmountDraft, setUnitAmountDraft] = useState<string>("");
  const [replicates, setReplicates] = useState<IcpReplicateDto[]>([]);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [reviewDialogOpen, setReviewDialogOpen] = useState(false);

  const sampleIdNum = Number(runSampleId);
  const parsedRunId = Number(runId);

  const loadData = useCallback(async () => {
    if (!sampleIdNum) return;
    try {
      const data = await IcpWorkspaceService.getSampleEntry(sampleIdNum);
      setSampleEntry(data);
      setAmountUnit(data.amountUnit || "Gram");
      setUnitAmountDraft(data.unitAmount != null ? String(data.unitAmount) : "");
      setReplicates(data.replicates || []);
      setDrafts({});
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Failed to load sample entry data.");
    } finally {
      setLoading(false);
    }
  }, [sampleIdNum]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleDraftChange = (key: string, value: string) => {
    setDrafts((prev) => ({ ...prev, [key]: value }));
  };

  const handleSaveReplicates = async () => {
    if (!sampleEntry || !sampleIdNum) return;
    setSaving(true);
    setError(null);

    const neededElementIds = sampleEntry.elements.map((e) => e.icpMethodElementId);
    const payload = buildSaveReplicatesPayload({
      amountUnit,
      mode: sampleEntry.mode,
      unitAmountDraft,
      unitAmountFallback: sampleEntry.unitAmount,
      replicates,
      drafts,
      neededElementIds
    });

    try {
      const updated = await IcpWorkspaceService.saveReplicates(sampleIdNum, payload);
      setSampleEntry(updated);
      setAmountUnit(updated.amountUnit);
      setUnitAmountDraft(updated.unitAmount != null ? String(updated.unitAmount) : "");
      setReplicates(updated.replicates);
      setDrafts({});
      toast.success("Replicate values saved and calculation preview updated.");
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not save replicates.");
    } finally {
      setSaving(false);
    }
  };

  const handleBack = () => {
    if (instrumentId && runId) navigate(ICP_ROUTES.samples(instrumentId, runId));
    else navigate(ICP_ROUTES.root);
  };

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (!sampleEntry) {
    return (
      <Box sx={{ p: 3 }}>
        <Alert severity="error">Sample entry record not found.</Alert>
        <Button onClick={handleBack} sx={{ mt: 2 }} startIcon={<ArrowBackIcon />}>
          Back to Run Samples
        </Button>
      </Box>
    );
  }

  const isEditable = sampleEntry.editable && canOperate;
  const neededElementIds = sampleEntry.elements.map((e) => e.icpMethodElementId);
  const canSave = isReplicatesComplete(
    replicates,
    neededElementIds,
    drafts,
    sampleEntry.mode,
    unitAmountDraft,
    sampleEntry.unitAmount
  );
  const unitAmountLabel = amountUnit === "Milliliter" ? "Dose (mL)" : "Average unit weight (g)";

  return (
    <Box sx={{ p: { xs: 2, md: 3 }, maxWidth: 1400, mx: "auto" }}>
      <Button startIcon={<ArrowBackIcon />} onClick={handleBack} sx={{ mb: 2, textTransform: "none" }}>
        Back to Run Samples
      </Button>

      {error && (
        <Alert severity="error" sx={{ mb: 2.5 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {/* Header and Identity */}
      <Paper elevation={0} sx={{ p: 2.5, mb: 2.5, borderRadius: 2, border: `1px solid ${theme.palette.divider}` }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 2 }}>
          <Box>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 1, flexWrap: "wrap" }}>
              <Typography variant="h5" sx={{ fontWeight: 700 }}>
                {sampleEntry.sampleNumber}
              </Typography>
              <IcpStatusBadge status={sampleEntry.status} />
              <StatusBadge
                status="Prepared"
                label={`Mode: ${sampleEntry.mode === "MineralAssay" ? "Mineral Assay" : "Elemental Impurities"}`}
              />
              <IcpStatusBadge
                status={sampleEntry.calibrationStatus}
                label={`Calibration: ${sampleEntry.calibrationCode} (${sampleEntry.calibrationStatus})`}
              />
              {sampleEntry.submitted && <StatusBadge status="Completed" label="Submitted for Review" />}
            </Box>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              <strong>Test Code:</strong> {sampleEntry.testCode}
              {sampleEntry.batchNumber ? ` · Batch: ${sampleEntry.batchNumber}` : ""}
              {sampleEntry.productName ? ` · Product: ${sampleEntry.productName}` : ""}
              {sampleEntry.stageName ? ` · Stage: ${sampleEntry.stageName}` : ""}
              {` · Run: ${sampleEntry.runCode}`}
            </Typography>
          </Box>

          <Stack direction="row" spacing={1.5}>
            <Button
              variant="outlined"
              color="primary"
              startIcon={<SaveIcon />}
              onClick={handleSaveReplicates}
              disabled={!isEditable || saving || !canSave}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {saving ? "Saving..." : "Save Replicates"}
            </Button>

            <Tooltip title={!sampleEntry.canSubmit ? sampleEntry.canSubmitReason || "Prerequisites not met" : ""}>
              <span>
                <Button
                  variant="contained"
                  color="primary"
                  startIcon={<SendIcon />}
                  onClick={() => setReviewDialogOpen(true)}
                  disabled={!sampleEntry.canSubmit || sampleEntry.submitted || saving || !canOperate}
                  sx={{ textTransform: "none", fontWeight: 600 }}
                >
                  Send for Review
                </Button>
              </span>
            </Tooltip>
          </Stack>
        </Box>

        {!sampleEntry.editable && (
          <Alert severity="info" icon={<LockOutlinedIcon />} sx={{ mt: 2 }}>
            <strong>Entry Locked:</strong> {sampleEntry.editableReason || "This test order is read-only."}
          </Alert>
        )}

        <Box sx={{ mt: 2 }}>
          <IcpElementStatusStrip elementStates={sampleEntry.elements} />
        </Box>
      </Paper>

      {/* Amount Configuration */}
      <Paper elevation={0} sx={{ p: 2.5, mb: 2.5, borderRadius: 2, border: `1px solid ${theme.palette.divider}` }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
          Sample Preparation & Dosage Settings
        </Typography>
        <Box sx={{ display: "flex", flexDirection: { xs: "column", sm: "row" }, gap: 3, alignItems: "flex-start" }}>
          <FormControl>
            <FormLabel sx={{ fontSize: 12, fontWeight: 600, mb: 0.5 }}>Amount Unit</FormLabel>
            <ToggleButtonGroup
              size="small"
              exclusive
              value={amountUnit}
              onChange={(_, val) => val && setAmountUnit(val)}
              disabled={!isEditable || saving}
            >
              <ToggleButton value="Gram" sx={{ px: 2.5, textTransform: "none", fontWeight: 600 }}>
                Gram (g)
              </ToggleButton>
              <ToggleButton
                value="Milliliter"
                disabled={sampleEntry.mode === "ElementalImpurities" || !isEditable || saving}
                sx={{ px: 2.5, textTransform: "none", fontWeight: 600 }}
              >
                Milliliter (mL)
              </ToggleButton>
            </ToggleButtonGroup>
            {sampleEntry.mode === "ElementalImpurities" && (
              <FormHelperText sx={{ fontSize: 11 }}>
                Elemental impurities are reported per gram (mL disabled).
              </FormHelperText>
            )}
          </FormControl>

          {sampleEntry.mode === "MineralAssay" && (
            <Box sx={{ minWidth: 260 }}>
              <TextField
                size="small"
                fullWidth
                label={unitAmountLabel}
                placeholder={amountUnit === "Milliliter" ? "e.g. 5.0" : "e.g. 0.5000"}
                value={unitAmountDraft}
                onChange={(e) => setUnitAmountDraft(e.target.value)}
                disabled={!isEditable || saving}
                helperText="Required for Amount per unit and % label claim specifications."
                slotProps={{
                  htmlInput: {
                    style: { fontVariantNumeric: "tabular-nums" },
                    "aria-label": unitAmountLabel
                  }
                }}
              />
            </Box>
          )}
        </Box>
      </Paper>

      {/* Main sections */}
      <Stack spacing={2.5}>
        <ResultSection step={1} title="Replicate Measurements">
          <IcpReplicateEntryTable
            elements={sampleEntry.elements}
            replicates={replicates}
            onChange={setReplicates}
            amountUnit={amountUnit}
            defaultVolumeMl={sampleEntry.sampleVolumeMl}
            defaultDilutionFactor={sampleEntry.dilutionFactor}
            drafts={drafts}
            onDraftChange={handleDraftChange}
            disabled={!isEditable || saving}
            footer={
              <Box
                sx={{
                  position: "sticky",
                  bottom: 0,
                  zIndex: 1,
                  mt: 1.5,
                  py: 1.25,
                  display: "flex",
                  justifyContent: "flex-end",
                  alignItems: "center",
                  gap: 1.5,
                  bgcolor: "background.paper",
                  borderTop: `1px solid ${theme.palette.divider}`
                }}
              >
                {isEditable && !canSave && (
                  <Typography variant="caption" sx={{ color: "text.secondary" }}>
                    Enter positive sample amount, volume, dilution factor (≥ 1), and concentrations (≥ 0) to save.
                  </Typography>
                )}
                <Button
                  variant="contained"
                  color="primary"
                  startIcon={<SaveIcon />}
                  onClick={handleSaveReplicates}
                  disabled={!isEditable || saving || !canSave}
                  sx={{ textTransform: "none", fontWeight: 600 }}
                >
                  {saving ? "Saving..." : "Save Replicates"}
                </Button>
              </Box>
            }
          />
        </ResultSection>

        <ResultSection step={2} title={sampleEntry.submitted ? "Results (Official)" : "Results (Server Preview)"}>
          <IcpPreviewCard preview={sampleEntry.preview} official={sampleEntry.official} submitted={sampleEntry.submitted} />
        </ResultSection>

        <ResultSection step={3} title="Evidence: Sample Reports">
          <IcpReportUploadPanel
            runId={parsedRunId || sampleEntry.icpRunId}
            runSampleId={sampleEntry.runSampleId}
            context="Sample"
            kind="SampleReport"
            evidenceList={sampleEntry.evidence}
            onChanged={loadData}
            title="Sample Report Upload"
            description="Upload the instrument Syngistix/raw export or PDF report for this sample. Required before sending for review."
            disabled={!isEditable}
          />
        </ResultSection>
      </Stack>

      {/* Send for Review Dialog */}
      <IcpSendForReviewDialog
        open={reviewDialogOpen}
        entry={sampleEntry}
        onClose={() => setReviewDialogOpen(false)}
        onSuccess={loadData}
      />
    </Box>
  );
}
