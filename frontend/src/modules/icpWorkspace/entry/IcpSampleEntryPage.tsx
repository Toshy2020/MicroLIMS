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
import { IcpReportAutofill } from "./IcpReportAutofill";
import { applyReportValues, buildSaveEntryPayload, draftFromEntry, isEntryComplete, type EntryDraft } from "./entryMapping";
import { useTranslation } from "react-i18next";
import { i18n } from "../../../i18n";
import type { IcpSampleEntryDto } from "../types";
import { useBreadcrumb } from "../../../components/breadcrumbContext";
import { Ltr } from "../../../components/Ltr";

export function IcpSampleEntryPage() {
  const { t } = useTranslation("icpWorkspace");
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
  useBreadcrumb(sampleEntry ? `${sampleEntry.sampleNumber} ${sampleEntry.testCode}` : error ? t("entry.samplePage.breadcrumbUnavailable") : null);

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
      setError(e.response?.data?.message ?? e.message ?? i18n.t("entry.samplePage.loadError", { ns: "icpWorkspace" }));
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
      toast.success(t("entry.samplePage.savedToast"));
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? t("entry.samplePage.saveError"));
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
        <Alert severity="error">{t("entry.samplePage.notFoundAlert")}</Alert>
        <Button onClick={handleBack} sx={{ mt: 2 }} startIcon={<ArrowBackIcon />}>
          {t("entry.samplePage.backToSamples")}
        </Button>
      </Box>
    );
  }

  const isEditable = sampleEntry.editable && canOperate;
  const canSave = isEntryComplete(sampleEntry.mode, sampleEntry.elements, draft);
  const isAssay = sampleEntry.mode === "Assay";
  const basisText =
    sampleEntry.sampleForm === "Liquid"
      ? t("entry.samplePage.basisLiquid", {
          volume: sampleEntry.theoreticalAmount ?? "—",
          sg: sampleEntry.specificGravity ?? "—"
        })
      : t("entry.samplePage.basisSolid", {
          weight: sampleEntry.theoreticalAmount ?? "—"
        });

  return (
    <Box sx={{ p: { xs: 2, md: 3 }, maxWidth: 1400, mx: "auto" }}>
      <Button startIcon={<ArrowBackIcon />} onClick={handleBack} sx={{ mb: 2, textTransform: "none" }}>
        {t("entry.samplePage.backToSamples")}
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
              <Box>
                <Typography variant="h5" sx={{ fontWeight: 700 }}>
                  <Ltr>{sampleEntry.sampleNumber}</Ltr>
                </Typography>
                {sampleEntry.subjectName && (
                  <Typography variant="caption" sx={{ display: "block", color: "text.secondary" }}>
                    {sampleEntry.subjectName}
                  </Typography>
                )}
              </Box>
              <IcpStatusBadge status={sampleEntry.status} />
              <StatusBadge
                status="Prepared"
                label={t("entry.samplePage.testBadge", {
                  mode: isAssay ? t("entry.samplePage.modeAssay") : t("entry.samplePage.modeElemental")
                })}
              />
              <IcpStatusBadge
                status={sampleEntry.calibrationStatus}
                label={t("entry.samplePage.calBadge", {
                  code: sampleEntry.calibrationCode,
                  status: sampleEntry.calibrationStatus
                })}
              />
              {sampleEntry.submitted && <StatusBadge status="Completed" label={t("entry.samplePage.submittedBadge")} />}
            </Box>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              <strong>{t("entry.samplePage.testCode")}</strong> <Ltr>{sampleEntry.testCode}</Ltr>
              {sampleEntry.batchNumber ? ` · ${t("entry.samplePage.batchSuffix", { batch: sampleEntry.batchNumber })}` : ""}
              {sampleEntry.productName ? ` · ${t("entry.samplePage.productSuffix", { product: sampleEntry.productName })}` : ""}
              {sampleEntry.stageName ? ` · ${t("entry.samplePage.stageSuffix", { stage: sampleEntry.stageName })}` : ""}
              {` · ${t("entry.samplePage.runSuffix", { run: sampleEntry.runCode })}`}
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
              {saving ? t("entry.samplePage.saving") : t("entry.samplePage.saveResults")}
            </Button>

            <Tooltip title={!sampleEntry.canSubmit ? sampleEntry.canSubmitReason || t("entry.samplePage.prerequisitesNotMet") : ""}>
              <span>
                <Button
                  variant="contained"
                  color="primary"
                  startIcon={<SendIcon />}
                  onClick={() => setReviewDialogOpen(true)}
                  disabled={!sampleEntry.canSubmit || sampleEntry.submitted || saving || !canOperate}
                  sx={{ textTransform: "none", fontWeight: 600 }}
                >
                  {t("entry.samplePage.sendForReview")}
                </Button>
              </span>
            </Tooltip>
          </Stack>
        </Box>

        {!sampleEntry.editable && (
          <Alert severity="info" icon={<LockOutlinedIcon />} sx={{ mt: 2 }}>
            <strong>{t("entry.samplePage.entryLocked")}</strong> {sampleEntry.editableReason || t("entry.samplePage.readOnlyFallback")}
          </Alert>
        )}

      </Paper>

      {/* Main sections */}
      <Stack spacing={2.5}>
        <ResultSection step={1} title={t("entry.samplePage.sectionEvidence")}>
          <IcpReportUploadPanel
            runId={parsedRunId || sampleEntry.icpRunId}
            runSampleId={sampleEntry.runSampleId}
            context="Sample"
            kind="SampleReport"
            evidenceList={sampleEntry.evidence}
            onChanged={loadData}
            title={t("entry.samplePage.reportTitle")}
            description={t("entry.samplePage.reportDescription")}
            disabled={!isEditable}
          />
        </ResultSection>

        <ResultSection step={2} title={isAssay ? t("entry.samplePage.sectionWeightResults") : t("entry.samplePage.sectionResults")}>
          {isAssay && (
            <Box sx={{ mb: 2, display: "flex", flexDirection: { xs: "column", sm: "row" }, gap: 3, alignItems: { sm: "center" } }}>
              <TextField
                size="small"
                type="number"
                label={t("entry.samplePage.actualWeightLabel")}
                value={draft.weight}
                onChange={(e) => setDraft((prev) => ({ ...prev, weight: e.target.value }))}
                disabled={!isEditable || saving}
                helperText={t("entry.samplePage.actualWeightHelper")}
                slotProps={{ htmlInput: { min: 0, step: "any", style: { fontVariantNumeric: "tabular-nums" }, "aria-label": t("entry.samplePage.actualWeightAria") } }}
                sx={{ width: 260 }}
              />
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                {basisText}
              </Typography>
            </Box>
          )}
          <IcpReportAutofill
            runSampleId={sampleEntry.runSampleId}
            hasReport={sampleEntry.evidence.some((e) => e.kind === "SampleReport" && e.isCurrent)}
            disabled={!isEditable || saving}
            onUse={(values) => {
              setDraft((prev) => applyReportValues(prev, values));
              toast.info(t("entry.samplePage.autofillToast"));
            }}
          />
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
                {isAssay ? t("entry.samplePage.saveHintAssay") : t("entry.samplePage.saveHintElemental")}
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
              {saving ? t("entry.samplePage.saving") : t("entry.samplePage.saveResults")}
            </Button>
          </Box>
        </ResultSection>

        <ResultSection step={3} title={sampleEntry.submitted ? t("entry.samplePage.sectionResultsOfficial") : t("entry.samplePage.sectionResultsPreview")}>
          <IcpPreviewCard preview={sampleEntry.preview} official={sampleEntry.official} submitted={sampleEntry.submitted} />
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
