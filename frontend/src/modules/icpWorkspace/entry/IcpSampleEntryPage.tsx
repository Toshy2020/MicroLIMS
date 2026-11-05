import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Box,
  Typography,
  Button,
  Stack,
  Alert,
  Paper,
  Tooltip,
  TextField,
  useTheme
} from "@mui/material";
import { WorkspaceSkeleton } from "../../../components/WorkspaceSkeleton";
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
import { StatusBadge } from "../../../components/StatusBadge";
import { ResultSection } from "../../../components/lab";
import { IcpResultEntryTable } from "./IcpResultEntryTable";
import { IcpPreviewCard } from "./IcpPreviewCard";
import { IcpSendForReviewDialog } from "./IcpSendForReviewDialog";
import { IcpReportUploadPanel } from "../evidence/IcpReportUploadPanel";
import { buildSaveEntryPayload, draftFromEntry, isEntryComplete, type EntryDraft } from "./entryMapping";
import type { IcpSampleEntryDto } from "../types";

export function IcpSampleEntryPage() {
  const theme = useTheme();
  const navigate = useNavigate();
  const { permissions } = useAuth();
  const canOperate = permissions.includes(PERMISSIONS.HPLC_OPERATE);

  const { instrumentId, runId, runSampleId } = useParams<{
    instrumentId: string;
    runId: string;
    runSampleId: string;
  }>();

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sampleEntry, setSampleEntry] = useState<IcpSampleEntryDto | null>(null);

  const [draft, setDraft] = useState<EntryDraft>({ weight: "", results: {} });
  const [reviewDialogOpen, setReviewDialogOpen] = useState(false);

  const sampleIdNum = Number(runSampleId);
  const parsedRunId = Number(runId);

  const loadData = useCallback(async () => {
    if (!sampleIdNum) return;
    try {
      const data = await IcpWorkspaceService.getSampleEntry(sampleIdNum);
      setSampleEntry(data);
      setDraft(draftFromEntry(data.elements, data.actualWeightG));
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

  const handleSaveEntry = async () => {
    if (!sampleEntry || !sampleIdNum) return;
    setSaving(true);
    setError(null);

    const payload = buildSaveEntryPayload(sampleEntry.mode, sampleEntry.elements, draft);

    try {
      const updated = await IcpWorkspaceService.saveEntry(sampleIdNum, payload);
      setSampleEntry(updated);
      setDraft(draftFromEntry(updated.elements, updated.actualWeightG));
      toast.success("Results saved and calculation preview updated.");
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not save the results.");
    } finally {
      setSaving(false);
    }
  };

  const handleBack = () => {
    if (instrumentId && runId) navigate(ICP_ROUTES.samples(instrumentId, runId));
    else navigate(ICP_ROUTES.root);
  };

  if (loading) {
    return <WorkspaceSkeleton />;
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
  const canSave = isEntryComplete(sampleEntry.mode, sampleEntry.elements, draft);
  const isAssay = sampleEntry.mode === "Assay";
  const basisText =
    sampleEntry.sampleForm === "Liquid"
      ? `Liquid sample: theoretical volume ${sampleEntry.theoreticalAmount ?? "—"} mL × specific gravity ${sampleEntry.specificGravity ?? "—"}`
      : `Solid sample: theoretical weight ${sampleEntry.theoreticalAmount ?? "—"} g`;

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
                label={`Test: ${isAssay ? "Assay" : "Elemental impurity"}`}
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
              onClick={handleSaveEntry}
              disabled={!isEditable || saving || !canSave}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {saving ? "Saving..." : "Save results"}
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

      </Paper>

      {/* Main sections */}
      <Stack spacing={2.5}>
        <ResultSection step={1} title={isAssay ? "Sample weight & instrument results" : "Instrument results"}>
          {isAssay && (
            <Box sx={{ mb: 2, display: "flex", flexDirection: { xs: "column", sm: "row" }, gap: 3, alignItems: { sm: "center" } }}>
              <TextField
                size="small"
                type="number"
                label="Actual weight of sample (g)"
                value={draft.weight}
                onChange={(e) => setDraft((prev) => ({ ...prev, weight: e.target.value }))}
                disabled={!isEditable || saving}
                helperText="Act.Wt: the weight actually taken."
                slotProps={{ htmlInput: { min: 0, step: "any", style: { fontVariantNumeric: "tabular-nums" }, "aria-label": "Actual weight (g)" } }}
                sx={{ width: 260 }}
              />
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                {basisText}
              </Typography>
            </Box>
          )}
          <IcpResultEntryTable
            mode={sampleEntry.mode}
            elements={sampleEntry.elements}
            results={draft.results}
            onResultChange={(id, value) => setDraft((prev) => ({ ...prev, results: { ...prev.results, [id]: value } }))}
            disabled={!isEditable || saving}
          />
          <Box
            sx={{
              mt: 1.5,
              py: 1.25,
              display: "flex",
              justifyContent: "flex-end",
              alignItems: "center",
              gap: 1.5,
              borderTop: `1px solid ${theme.palette.divider}`
            }}
          >
            {isEditable && !canSave && (
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {isAssay ? "Enter the actual weight and a result (≥ 0) for every element to save." : "Enter a result (≥ 0) for every element to save."}
              </Typography>
            )}
            <Button
              variant="contained"
              color="primary"
              startIcon={<SaveIcon />}
              onClick={handleSaveEntry}
              disabled={!isEditable || saving || !canSave}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {saving ? "Saving..." : "Save results"}
            </Button>
          </Box>
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
