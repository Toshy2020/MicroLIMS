import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Box,
  Typography,
  Button,
  Stack,
  Alert,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
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
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { NumericCell, ResultSection } from "../../../components/lab";
import { StatusBadge } from "../../../components/StatusBadge";
import { ReplicateEntryTable, replicatesComplete } from "./ReplicateEntryTable";
import { buildReplicatePayload } from "./replicatePayload";
import { RelatedSubstancesPeakTable, rsReplicatesComplete } from "./RelatedSubstancesPeakTable";
import { CalculationSummaryCard, OfficialResultsCard } from "./CalculationSummaryCard";
import { SendForReviewDialog } from "./SendForReviewDialog";
import { ReportUploadPanel } from "../evidence/ReportUploadPanel";
import { tableHeadSx } from "../../../theme";
import { useTechnique } from "../useTechnique";
import { TestWorkflowService } from "../../testingWorkspace/services/TestWorkflowService";
import { formatMoistureLabel } from "../../testingWorkspace/utils/moistureRetentionHelpers";
import { getErrorMessage } from "../../../utils/errorMessage";
import type { SampleMoistureResult } from "../../testingWorkspace/types/testWorkflowTypes";
import type {
  HplcSampleEntryDto,
  HplcReplicateDto,
} from "../types";
import { useBreadcrumb } from "../../../components/breadcrumbContext";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";

export function HplcSampleEntryPage() {
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
  const [sampleEntry, setSampleEntry] = useState<HplcSampleEntryDto | null>(null);
  useBreadcrumb(sampleEntry ? `${sampleEntry.sampleNumber} ${sampleEntry.testCode}` : error ? t("entry.samplePage.breadcrumbUnavailable") : null);
  const [replicates, setReplicates] = useState<HplcReplicateDto[]>([]);
  const [reviewDialogOpen, setReviewDialogOpen] = useState(false);

  // Moisture state for content basis
  const [moisture, setMoisture] = useState<SampleMoistureResult | null>(null);
  const [moistureStatus, setMoistureStatus] = useState<"idle" | "loading" | "loaded" | "error">("idle");
  const [moistureError, setMoistureError] = useState("");

  const sampleIdNum = Number(runSampleId);
  const parsedRunId = Number(runId);

  const loadData = useCallback(async () => {
    if (!sampleIdNum) return;
    try {
      const data = await HplcWorkspaceService.getSampleEntry(sampleIdNum);
      setSampleEntry(data);
      const normalizedReplicates = (data.replicates || []).map((r) => ({
        ...r,
        peaks: r.peaks?.map((p, idx) => ({
          ...p,
          clientKey: p.clientKey || (p.hplcMethodImpurityId == null ? `unsp-${r.replicateNo}-${idx}` : undefined)
        }))
      }));
      setReplicates(normalizedReplicates);

      // What the server validates against is the run's snapshot, carried on the entry - not the live method.
      const isRS = data.resultMode === "RelatedSubstances";
      const isResidual = data.resultMode === "ResidualSolvents";

      setMoistureError("");
      if (!isRS && !isResidual && Boolean(data.moistureRole) && data.testOrderId) {
        try {
          setMoistureStatus("loading");
          const m = await TestWorkflowService.getMoisture(data.testOrderId);
          setMoisture(m);
          setMoistureStatus("loaded");
        } catch (err: unknown) {
          setMoisture(null);
          setMoistureError(getErrorMessage(err, t("entry.samplePage.moistureLoadError")));
          setMoistureStatus("error");
        }
      } else {
        setMoisture(null);
        setMoistureStatus("idle");
      }
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? t("entry.samplePage.loadError"));
    } finally {
      setLoading(false);
    }
  }, [sampleIdNum, t]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const isResidualSolvents = sampleEntry?.resultMode === "ResidualSolvents";
  const isRelatedSubstances = sampleEntry?.resultMode === "RelatedSubstances";

  const hasInternalStandard = Boolean(sampleEntry?.internalStandardName);
  const principalAnalyteId = sampleEntry?.principalAnalyte?.id ?? null;
  const hasExactlyOneAnalyte = principalAnalyteId != null;

  const handleSaveReplicates = async () => {
    if (!sampleEntry || !sampleIdNum) return;
    setSaving(true);
    setError(null);

    const payload = buildReplicatePayload(replicates, Boolean(isRelatedSubstances), hasInternalStandard);

    try {
      const updated = await HplcWorkspaceService.saveReplicates(sampleIdNum, {
        replicates: payload
      });
      setSampleEntry(updated);
      setReplicates(updated.replicates);
      toast.success(t("entry.samplePage.savedToast"));
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      // Preserve entered replicate values on error
      setError(e.response?.data?.message ?? e.message ?? t("entry.samplePage.saveError"));
    } finally {
      setSaving(false);
    }
  };

  const handleBack = () => {
    navigate(`${basePath}/${instrumentId}/run/${runId}/samples`);
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
  const methodWeights = sampleEntry.methodWeights || [];

  const canSave = isRelatedSubstances
    ? rsReplicatesComplete(replicates, principalAnalyteId)
    : replicatesComplete(methodWeights, replicates, isResidualSolvents, hasInternalStandard);

  return (
    <Box sx={{ p: { xs: 2, md: 3 }, maxWidth: 1400, mx: "auto" }}>
      {/* Back button */}
      <Button
        startIcon={<ArrowBackIcon />}
        onClick={handleBack}
        sx={{ mb: 2, textTransform: "none" }}
      >
        {t("entry.samplePage.backToSamples")}
      </Button>

      {error && (
        <Alert severity="error" sx={{ mb: 2.5 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {/* Header and Identity */}
      <Paper
        elevation={0}
        sx={{
          p: 2.5,
          mb: 2.5,
          borderRadius: 2,
          border: `1px solid ${theme.palette.divider}`
        }}
      >
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
              <HplcStatusBadge status={sampleEntry.status} />
              <StatusBadge status="Prepared" label={t("entry.samplePage.basis", { basis: sampleEntry.basis })} />
              <HplcStatusBadge
                status={sampleEntry.sstStatus}
                label={t("entry.samplePage.sstBadge", { code: sampleEntry.sstCode, status: sampleEntry.sstStatus })}
              />
              {sampleEntry.submitted && (
                <StatusBadge status="Completed" label={t("entry.samplePage.submittedBadge")} />
              )}
            </Box>

            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              <strong>{t("entry.samplePage.testCode")}</strong> <Ltr>{sampleEntry.testCode}</Ltr>
              {sampleEntry.batchNumber ? t("entry.samplePage.batchSuffix", { batch: sampleEntry.batchNumber }) : ""}
              {sampleEntry.productName ? t("entry.samplePage.productSuffix", { product: sampleEntry.productName }) : ""}
              {sampleEntry.stageName ? t("entry.samplePage.stageSuffix", { stage: sampleEntry.stageName }) : ""}
              {t("entry.samplePage.runSuffix", { run: sampleEntry.runCode })}
            </Typography>
          </Box>

          {/* Action buttons */}
          <Stack direction="row" spacing={1.5}>
            <Button
              variant="outlined"
              color="primary"
              startIcon={<SaveIcon />}
              onClick={handleSaveReplicates}
              disabled={!isEditable || saving || !canSave}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {saving ? t("entry.samplePage.saving") : t("entry.samplePage.saveReplicates")}
            </Button>

            <Tooltip
              title={
                !sampleEntry.canSubmit
                  ? (sampleEntry.canSubmitReason || t("entry.samplePage.prerequisitesNotMet"))
                  : ""
              }
            >
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

        {/* Read-only / Lock reason */}
        {!sampleEntry.editable && (
          <Alert severity="info" icon={<LockOutlinedIcon />} sx={{ mt: 2 }}>
            <strong>{t("entry.samplePage.entryLocked")}</strong> {sampleEntry.editableReason || t("entry.samplePage.readOnlyFallback")}
          </Alert>
        )}

        {/* Content basis moisture line: only when moistureRole is set, and not RS / residual */}
        {Boolean(sampleEntry.moistureRole && !isRelatedSubstances && !isResidualSolvents) && (
          <Box sx={{ mt: 2, pt: 1.5, borderTop: `1px solid ${theme.palette.divider}` }}>
            {moistureStatus === "loading" ? (
              <Typography sx={{ fontSize: 13, color: "text.secondary", py: 0.5 }}>
                {t("entry.samplePage.loadingMoisture")}
              </Typography>
            ) : moistureStatus === "error" ? (
              <Alert severity="error" sx={{ width: "100%", fontSize: 13, py: 0.5 }}>
                {moistureError || t("entry.samplePage.moistureLoadError")}
              </Alert>
            ) : moisture ? (
              <Typography sx={{ fontSize: 13, color: "text.primary", py: 0.5, fontWeight: 600 }}>
                {formatMoistureLabel(moisture)}
              </Typography>
            ) : sampleEntry.canSubmitReason ? (
              <Alert severity="warning" sx={{ width: "100%", fontSize: 13, py: 0.5 }}>
                {sampleEntry.canSubmitReason}
              </Alert>
            ) : null}
          </Box>
        )}
      </Paper>

      <Stack spacing={2.5}>
        {/* 1. Method theoretical weights snapshot (Assay mode only) */}
        {!isResidualSolvents && !isRelatedSubstances && (
          <ResultSection step={1} title={t("entry.samplePage.sectionMethodWeights")}>
            {methodWeights.length > 0 ? (
              <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
                <Table size="small">
                  <TableHead sx={tableHeadSx(theme)}>
                    <TableRow>
                      <TableCell>{t("entry.samplePage.columns.analyte")}</TableCell>
                      <TableCell align="right">{t("entry.samplePage.columns.thWtStd")}</TableCell>
                      <TableCell align="right">{t("entry.samplePage.columns.thWtTest")}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {methodWeights.map((mw) => (
                      <TableRow key={mw.hplcMethodAnalyteId}>
                        <TableCell sx={{ fontWeight: 600 }}><Ltr>{mw.analyteName}</Ltr></TableCell>
                        <TableCell align="right">
                          <NumericCell value={mw.theoreticalWeightStdMg} unit="mg" />
                        </TableCell>
                        <TableCell align="right">
                          <NumericCell value={mw.theoreticalWeightTestMg} unit="mg" />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            ) : (
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                {t("entry.samplePage.noMethodWeights")}
              </Typography>
            )}
          </ResultSection>
        )}

        {/* 2. Replicate entry */}
        <ResultSection
          step={isResidualSolvents || isRelatedSubstances ? 1 : 2}
          title={isRelatedSubstances ? t("entry.samplePage.sectionPeakArea") : t("entry.samplePage.sectionReplicates")}
        >
          {isRelatedSubstances ? (
            <RelatedSubstancesPeakTable
              method={{
                analytes: sampleEntry.principalAnalyte ? [sampleEntry.principalAnalyte] : [],
                impurities: sampleEntry.impurities ?? [],
                referenceSolutionPercent: sampleEntry.referenceSolutionPercent,
                disregardLimitPercent: sampleEntry.disregardLimitPercent
              }}
              methodWeights={methodWeights}
              replicates={replicates}
              onChange={setReplicates}
              requiredReplicates={sampleEntry.requiredReplicates}
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
                    <Typography
                      variant="caption"
                      sx={{ color: !hasExactlyOneAnalyte ? "error.main" : "text.secondary" }}
                    >
                      {!hasExactlyOneAnalyte
                        ? t("entry.rsPeakTable.oneAnalyteError")
                        : t("entry.samplePage.rsSaveHint")}
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
                    {saving ? t("entry.samplePage.saving") : t("entry.samplePage.saveReplicates")}
                  </Button>
                </Box>
              }
            />
          ) : (
            <ReplicateEntryTable
              methodWeights={methodWeights}
              replicates={replicates}
              onChange={setReplicates}
              requiredReplicates={sampleEntry.requiredReplicates}
              disabled={!isEditable || saving}
              isResidualSolvents={isResidualSolvents}
              hasInternalStandard={hasInternalStandard}
              internalStandardName={sampleEntry.internalStandardName}
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
                      {hasInternalStandard
                        ? t("entry.samplePage.isSaveHint")
                        : isResidualSolvents
                        ? t("entry.samplePage.residualSaveHint")
                        : t("entry.samplePage.defaultSaveHint")}
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
                    {saving ? t("entry.samplePage.saving") : t("entry.samplePage.saveReplicates")}
                  </Button>
                </Box>
              }
            />
          )}
        </ResultSection>

        {/* 3. Results: Official when submitted, otherwise the server-calculated preview */}
        <ResultSection
          step={isResidualSolvents || isRelatedSubstances ? 2 : 3}
          title={sampleEntry.submitted ? t("entry.samplePage.sectionResultsOfficial") : t("entry.samplePage.sectionResultsPreview")}
        >
          {sampleEntry.submitted ? (
            <OfficialResultsCard official={sampleEntry.official || []} basis={sampleEntry.basis} />
          ) : (
            <CalculationSummaryCard preview={sampleEntry.preview || []} basis={sampleEntry.basis} relatedSubstances={isRelatedSubstances} />
          )}
        </ResultSection>

        {/* 4. Chromatogram / evidence upload */}
        <ResultSection
          step={isResidualSolvents || isRelatedSubstances ? 3 : 4}
          title={t("entry.samplePage.sectionEvidence")}
        >
          <ReportUploadPanel
            runId={parsedRunId || sampleEntry.hplcRunId}
            runSampleId={sampleEntry.runSampleId}
            context="Sample"
            kind="SampleReport"
            evidenceList={sampleEntry.evidence || []}
            onChanged={loadData}
          />
        </ResultSection>
      </Stack>

      {/* Send for Review Dialog */}
      <SendForReviewDialog
        open={reviewDialogOpen}
        sampleEntry={sampleEntry}
        onClose={() => setReviewDialogOpen(false)}
        onSuccess={loadData}
      />
    </Box>
  );
}
