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
import { HplcMethodService } from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { NumericCell, ResultSection } from "../../../components/lab";
import { StatusBadge } from "../../../components/StatusBadge";
import { ReplicateEntryTable, replicatesComplete } from "./ReplicateEntryTable";
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
  HplcReplicateInput,
  HplcMethodSnapshot
} from "../types";

export function buildReplicatePayload(
  replicates: HplcReplicateDto[],
  isRelatedSubstances: boolean,
  hasInternalStandard: boolean
): HplcReplicateInput[] {
  return replicates.map((r) => {
    const item: HplcReplicateInput = {
      actualWeightMg: isRelatedSubstances ? 0 : (r.actualWeightMg ?? 0),
      responses: r.responses.map((resp) => ({
        hplcMethodAnalyteId: resp.hplcMethodAnalyteId,
        response: resp.response
      }))
    };
    if (hasInternalStandard) {
      item.internalStandardResponse = r.internalStandardResponse ?? null;
    }
    if (isRelatedSubstances && r.peaks) {
      item.peaks = r.peaks
        .filter((p) => p.hplcMethodImpurityId != null || (typeof p.area === "number" && p.area > 0))
        .map((p) => ({
          hplcMethodImpurityId: p.hplcMethodImpurityId ?? null,
          relativeRetention: p.relativeRetention ?? null,
          area: p.area
        }));
    }
    return item;
  });
}

export function HplcSampleEntryPage() {
  const theme = useTheme();
  const navigate = useNavigate();
  const { basePath } = useTechnique();
  const { permissions, role } = useAuth();
  const canOperate =
    permissions.includes(PERMISSIONS.HPLC_OPERATE) ||
    role === "SystemAdministrator";

  const { instrumentId, runId, runSampleId } = useParams<{
    instrumentId: string;
    runId: string;
    runSampleId: string;
  }>();

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sampleEntry, setSampleEntry] = useState<HplcSampleEntryDto | null>(null);
  const [method, setMethod] = useState<HplcMethodSnapshot | null>(null);
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

      const runIdToFetch = parsedRunId || data.hplcRunId;
      let loadedMethod: HplcMethodSnapshot | null = null;
      if (runIdToFetch) {
        try {
          const runData = await HplcWorkspaceService.getRun(runIdToFetch);
          if (runData?.hplcMethodId) {
            loadedMethod = await HplcMethodService.getById(runData.hplcMethodId);
            setMethod(loadedMethod);
          }
        } catch {
          // Method snapshot is optional
        }
      }

      const isRS = loadedMethod?.resultMode === "RelatedSubstances";
      const isResidual = loadedMethod?.resultMode === "ResidualSolvents";

      setMoistureError("");
      if (!isRS && !isResidual && Boolean(data.moistureRole) && data.testOrderId) {
        try {
          setMoistureStatus("loading");
          const m = await TestWorkflowService.getMoisture(data.testOrderId);
          setMoisture(m);
          setMoistureStatus("loaded");
        } catch (err: unknown) {
          setMoisture(null);
          setMoistureError(getErrorMessage(err, "Could not load the sample's moisture result."));
          setMoistureStatus("error");
        }
      } else {
        setMoisture(null);
        setMoistureStatus("idle");
      }
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Failed to load sample entry data.");
    } finally {
      setLoading(false);
    }
  }, [sampleIdNum, parsedRunId]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const isResidualSolvents = method?.resultMode === "ResidualSolvents";
  const isRelatedSubstances = method?.resultMode === "RelatedSubstances";

  const hasInternalStandard = Boolean(method?.internalStandardName);
  const principalAnalyteId = method?.analytes?.length === 1 ? method.analytes[0].id : null;
  const hasExactlyOneAnalyte = method?.analytes?.length === 1;

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
      toast.success("Replicate values saved and preview updated.");
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      // Preserve entered replicate values on error
      setError(e.response?.data?.message ?? e.message ?? "Could not save replicates.");
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
        <Alert severity="error">Sample entry record not found.</Alert>
        <Button onClick={handleBack} sx={{ mt: 2 }} startIcon={<ArrowBackIcon />}>
          Back to Run Samples
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
        Back to Run Samples
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
              <Typography variant="h5" sx={{ fontWeight: 700 }}>
                {sampleEntry.sampleNumber}
              </Typography>
              <HplcStatusBadge status={sampleEntry.status} />
              <StatusBadge status="Prepared" label={`Basis: ${sampleEntry.basis}`} />
              <HplcStatusBadge
                status={sampleEntry.sstStatus}
                label={`SST: ${sampleEntry.sstCode} (${sampleEntry.sstStatus})`}
              />
              {sampleEntry.submitted && (
                <StatusBadge status="Completed" label="Submitted for Review" />
              )}
            </Box>

            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              <strong>Test Code:</strong> {sampleEntry.testCode}
              {sampleEntry.batchNumber ? ` · Batch: ${sampleEntry.batchNumber}` : ""}
              {sampleEntry.productName ? ` · Product: ${sampleEntry.productName}` : ""}
              {sampleEntry.stageName ? ` · Stage: ${sampleEntry.stageName}` : ""}
              {` · Run: ${sampleEntry.runCode}`}
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
              {saving ? "Saving..." : "Save Replicates"}
            </Button>

            <Tooltip
              title={
                !sampleEntry.canSubmit
                  ? (sampleEntry.canSubmitReason || "Prerequisites not met")
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
                  Send for Review
                </Button>
              </span>
            </Tooltip>
          </Stack>
        </Box>

        {/* Read-only / Lock reason */}
        {!sampleEntry.editable && (
          <Alert severity="info" icon={<LockOutlinedIcon />} sx={{ mt: 2 }}>
            <strong>Entry Locked:</strong> {sampleEntry.editableReason || "This test order is read-only."}
          </Alert>
        )}

        {/* Content basis moisture line: only when moistureRole is set, and not RS / residual */}
        {Boolean(sampleEntry.moistureRole && !isRelatedSubstances && !isResidualSolvents) && (
          <Box sx={{ mt: 2, pt: 1.5, borderTop: `1px solid ${theme.palette.divider}` }}>
            {moistureStatus === "loading" ? (
              <Typography sx={{ fontSize: 13, color: "text.secondary", py: 0.5 }}>
                Loading moisture result...
              </Typography>
            ) : moistureStatus === "error" ? (
              <Alert severity="error" sx={{ width: "100%", fontSize: 13, py: 0.5 }}>
                {moistureError || "Could not load the sample's moisture result."}
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
          <ResultSection step={1} title="Method & theoretical weights">
            {methodWeights.length > 0 ? (
              <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
                <Table size="small">
                  <TableHead sx={tableHeadSx(theme)}>
                    <TableRow>
                      <TableCell>Analyte</TableCell>
                      <TableCell align="right">Theoretical Weight Std</TableCell>
                      <TableCell align="right">Theoretical Weight Test</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {methodWeights.map((mw) => (
                      <TableRow key={mw.hplcMethodAnalyteId}>
                        <TableCell sx={{ fontWeight: 600 }}>{mw.analyteName}</TableCell>
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
                No method theoretical weights are configured for this test.
              </Typography>
            )}
          </ResultSection>
        )}

        {/* 2. Replicate entry */}
        <ResultSection
          step={isResidualSolvents || isRelatedSubstances ? 1 : 2}
          title={isRelatedSubstances ? "Peak area entry" : "Replicates"}
        >
          {isRelatedSubstances ? (
            <RelatedSubstancesPeakTable
              method={method}
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
                        ? "This related-substances method must have exactly one analyte."
                        : "Enter a positive reference solution area, non-negative peak areas, and positive RRTs to save."}
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
          ) : (
            <ReplicateEntryTable
              methodWeights={methodWeights}
              replicates={replicates}
              onChange={setReplicates}
              requiredReplicates={sampleEntry.requiredReplicates}
              disabled={!isEditable || saving}
              isResidualSolvents={isResidualSolvents}
              hasInternalStandard={hasInternalStandard}
              internalStandardName={method?.internalStandardName}
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
                        ? "Enter a positive weight, analyte area, and IS area in every row to save."
                        : isResidualSolvents
                        ? "Enter a positive weight and area (≥ 0) in every cell to save."
                        : "Enter a positive weight and response in every cell to save."}
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
          )}
        </ResultSection>

        {/* 3. Results: Official when submitted, otherwise the server-calculated preview */}
        <ResultSection
          step={isResidualSolvents || isRelatedSubstances ? 2 : 3}
          title={sampleEntry.submitted ? "Results (official)" : "Results (preview)"}
        >
          {sampleEntry.submitted ? (
            <OfficialResultsCard official={sampleEntry.official || []} basis={sampleEntry.basis} />
          ) : (
            <CalculationSummaryCard preview={sampleEntry.preview || []} basis={sampleEntry.basis} />
          )}
        </ResultSection>

        {/* 4. Chromatogram / evidence upload */}
        <ResultSection
          step={isResidualSolvents || isRelatedSubstances ? 3 : 4}
          title="Evidence: chromatograms & reports"
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
