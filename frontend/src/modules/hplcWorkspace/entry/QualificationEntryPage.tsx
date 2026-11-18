import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate, Link as RouterLink } from "react-router-dom";
import { Trans } from "react-i18next";
import {
  Box, Typography, Button, Stack, Alert, CircularProgress,
  Paper, Table, TableBody, TableCell, TableContainer, TableHead,
  TableRow, Tooltip, Link, useTheme
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import SaveIcon from "@mui/icons-material/Save";
import SendIcon from "@mui/icons-material/Send";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import { toast } from "sonner";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { NumericCell, ResultSection } from "../../../components/lab";
import { StatusBadge } from "../../../components/StatusBadge";
import { ReplicateEntryTable, replicatesComplete } from "./ReplicateEntryTable";
import { buildReplicatePayload } from "./replicatePayload";
import { WorkingStandardPreviewCard } from "./WorkingStandardPreviewCard";
import { SendForReviewDialog } from "./SendForReviewDialog";
import { ReportUploadPanel } from "../evidence/ReportUploadPanel";
import { tableHeadSx } from "../../../theme";
import { useTechnique } from "../useTechnique";
import type { HplcQualificationEntryDto, HplcReplicateDto } from "../types";
import { useBreadcrumb } from "../../../components/breadcrumbContext";
import { Ltr } from "../../../components/Ltr";
import { i18n, useTranslation } from "../../../i18n";

export function QualificationEntryPage() {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();
  const navigate = useNavigate();
  const { basePath } = useTechnique();
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
  const [entry, setEntry] = useState<HplcQualificationEntryDto | null>(null);
  useBreadcrumb(entry ? `${entry.qualificationCode} ${entry.materialName}` : error ? t("entry.qualPage.breadcrumbUnavailable") : null);
  const [replicates, setReplicates] = useState<HplcReplicateDto[]>([]);
  const [reviewDialogOpen, setReviewDialogOpen] = useState(false);

  const sampleIdNum = Number(runSampleId);
  const parsedRunId = Number(runId);

  const loadData = useCallback(async () => {
    if (!sampleIdNum) return;
    try {
      const data = await HplcWorkspaceService.getQualificationEntry(sampleIdNum);
      setEntry(data);
      const reps = data.replicates?.length > 0 ? data.replicates : data.editable
        ? Array.from({ length: data.requiredReplicates || 6 }, (_, i) => ({
            replicateNo: i + 1, actualWeightMg: 0, internalStandardResponse: data.internalStandardName ? 0 : undefined,
            responses: (data.methodWeights || []).map((mw) => ({ hplcMethodAnalyteId: mw.hplcMethodAnalyteId, response: 0 }))
          }))
        : [];
      setReplicates(reps);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? i18n.t("entry.qualPage.loadError", { ns: "hplcWorkspace" }));
    } finally {
      setLoading(false);
    }
    // Texts are read through i18n, not t: a new t (language change) would reload the entry and
    // overwrite the replicate values being typed.
  }, [sampleIdNum]);

  useEffect(() => { loadData(); }, [loadData]);

  const handleSaveReplicates = async () => {
    if (!entry || !sampleIdNum) return;
    setSaving(true);
    setError(null);
    const payload = buildReplicatePayload(replicates, false, Boolean(entry.internalStandardName));
    try {
      const updated = await HplcWorkspaceService.saveQualificationReplicates(sampleIdNum, { replicates: payload });
      setEntry(updated);
      setReplicates(updated.replicates);
      toast.success(t("entry.qualPage.savedToast"));
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? t("entry.qualPage.saveError"));
    } finally {
      setSaving(false);
    }
  };

  const handleBack = () => { navigate(`${basePath}/${instrumentId}/run/${runId}/samples`); };

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (!entry) {
    return (
      <Box sx={{ p: 3 }}>
        <Alert severity="error">{t("entry.qualPage.notFoundAlert")}</Alert>
        <Button onClick={handleBack} sx={{ mt: 2 }} startIcon={<ArrowBackIcon />}>
          {t("entry.qualPage.backToSamples")}
        </Button>
      </Box>
    );
  }

  const isEditable = entry.editable && canOperate;
  const methodWeights = entry.methodWeights || [];
  const hasInternalStandard = Boolean(entry.internalStandardName);
  const canSave = replicatesComplete(methodWeights, replicates, false, hasInternalStandard);

  return (
    <Box sx={{ p: { xs: 2, md: 3 }, maxWidth: 1400, mx: "auto" }}>
      <Button startIcon={<ArrowBackIcon />} onClick={handleBack} sx={{ mb: 2, textTransform: "none" }}>
        {t("entry.qualPage.backToSamples")}
      </Button>

      {error && <Alert severity="error" sx={{ mb: 2.5 }} onClose={() => setError(null)}>{error}</Alert>}

      <Paper elevation={0} sx={{ p: 2.5, mb: 2.5, borderRadius: 2, border: `1px solid ${theme.palette.divider}` }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 2 }}>
          <Box>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 1, flexWrap: "wrap" }}>
              <Typography variant="h5" sx={{ fontWeight: 700 }}><Ltr>{entry.qualificationCode}</Ltr></Typography>
              <HplcStatusBadge status={entry.qualificationStatus} />
              <StatusBadge status="Primary" label={t("entry.qualPage.wsQualificationBadge")} />
              <HplcStatusBadge status={entry.sstStatus} label={t("entry.qualPage.sstBadge", { code: entry.sstCode, status: entry.sstStatus })} />
              {entry.submitted && <StatusBadge status="Completed" label={t("entry.qualPage.submittedBadge")} />}
            </Box>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              <strong>{t("entry.qualPage.material")}</strong> {entry.materialName}
              {entry.batchNumber ? t("entry.qualPage.batchSuffix", { batch: entry.batchNumber }) : ""}
              {entry.moisturePercent != null ? t("entry.qualPage.moistureSuffix", { moisture: entry.moisturePercent }) : ""}
              {t("entry.qualPage.runSuffix", { run: entry.runCode })}
            </Typography>
          </Box>
          <Tooltip title={!entry.canSubmit ? (entry.canSubmitReason || t("entry.qualPage.prerequisitesNotMet")) : ""}>
            <span>
              <Button variant="contained" color="primary" startIcon={<SendIcon />} onClick={() => setReviewDialogOpen(true)} disabled={!entry.canSubmit || entry.submitted || saving || !canOperate} sx={{ textTransform: "none", fontWeight: 600 }}>
                {t("entry.qualPage.sendForReview")}
              </Button>
            </span>
          </Tooltip>
        </Box>
        {entry.submitted && (
          <Alert severity="success" sx={{ mt: 2 }}>
            <Trans
              i18nKey="entry.qualPage.submittedBanner"
              ns="hplcWorkspace"
              components={{
                wsLink: <Link component={RouterLink} to="/working-standards" sx={{ fontWeight: 600, color: "inherit", textDecoration: "underline" }} />
              }}
            />
          </Alert>
        )}
        {!entry.editable && (
          <Alert severity="info" icon={<LockOutlinedIcon />} sx={{ mt: 2 }}>
            <strong>{t("entry.qualPage.entryLocked")}</strong> {entry.editableReason || t("entry.qualPage.readOnlyFallback")}
          </Alert>
        )}
      </Paper>

      <Stack spacing={2.5}>
        <ResultSection step={1} title={t("entry.qualPage.sectionMethodWeights")}>
          {methodWeights.length > 0 ? (
            <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
                  <TableRow>
                    <TableCell>{t("entry.qualPage.columns.analyte")}</TableCell>
                    <TableCell align="right">{t("entry.qualPage.columns.thWtStd")}</TableCell>
                    <TableCell align="right">{t("entry.qualPage.columns.thWtTest")}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {methodWeights.map((mw) => (
                    <TableRow key={mw.hplcMethodAnalyteId}>
                      <TableCell sx={{ fontWeight: 600 }}><Ltr>{mw.analyteName}</Ltr></TableCell>
                      <TableCell align="right"><NumericCell value={mw.theoreticalWeightStdMg} unit="mg" /></TableCell>
                      <TableCell align="right"><NumericCell value={mw.theoreticalWeightTestMg} unit="mg" /></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          ) : (
            <Typography variant="body2" sx={{ color: "text.secondary" }}>{t("entry.qualPage.noMethodWeights")}</Typography>
          )}
        </ResultSection>

        <ResultSection step={2} title={t("entry.qualPage.sectionReplicates")}>
          <ReplicateEntryTable
            methodWeights={methodWeights}
            replicates={replicates}
            onChange={setReplicates}
            requiredReplicates={entry.requiredReplicates || 6}
            hasInternalStandard={hasInternalStandard}
            internalStandardName={entry.internalStandardName}
            disabled={!isEditable || saving}
            footer={
              <Box sx={{ position: "sticky", bottom: 0, zIndex: 1, mt: 1.5, py: 1.25, display: "flex", justifyContent: "flex-end", alignItems: "center", gap: 1.5, bgcolor: "background.paper", borderTop: `1px solid ${theme.palette.divider}` }}>
                {isEditable && !canSave && (
                  <Typography variant="caption" sx={{ color: "text.secondary" }}>
                    {t("entry.qualPage.saveHint")}
                  </Typography>
                )}
                <Button variant="contained" color="primary" startIcon={<SaveIcon />} onClick={handleSaveReplicates} disabled={!isEditable || saving || !canSave} sx={{ textTransform: "none", fontWeight: 600 }}>
                  {saving ? t("entry.qualPage.saving") : t("entry.qualPage.saveReplicates")}
                </Button>
              </Box>
            }
          />
        </ResultSection>

        <ResultSection step={3} title={entry.submitted ? t("entry.qualPage.sectionResultsRecorded") : t("entry.qualPage.sectionResultsPreview")}>
          <WorkingStandardPreviewCard preview={entry.preview} submitted={entry.submitted} />
        </ResultSection>

        <ResultSection step={4} title={t("entry.qualPage.sectionEvidence")}>
          <ReportUploadPanel
            runId={parsedRunId || entry.hplcRunId}
            runSampleId={entry.runSampleId}
            context="Sample"
            kind="SampleReport"
            evidenceList={entry.evidence || []}
            onChanged={loadData}
          />
        </ResultSection>
      </Stack>

      <SendForReviewDialog
        open={reviewDialogOpen}
        qualificationEntry={entry}
        onClose={() => setReviewDialogOpen(false)}
        onSuccess={loadData}
      />
    </Box>
  );
}
