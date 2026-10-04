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
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  useTheme
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
import { CalculationSummaryCard, OfficialResultsCard } from "./CalculationSummaryCard";
import { SendForReviewDialog } from "./SendForReviewDialog";
import { ReportUploadPanel } from "../evidence/ReportUploadPanel";
import { tableHeadSx } from "../../../theme";
import { useTechnique } from "../useTechnique";
import type {
  HplcSampleEntryDto,
  HplcReplicateDto,
  HplcReplicateInput
} from "../types";

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
  const [replicates, setReplicates] = useState<HplcReplicateDto[]>([]);
  const [reviewDialogOpen, setReviewDialogOpen] = useState(false);

  const sampleIdNum = Number(runSampleId);
  const parsedRunId = Number(runId);

  const loadData = useCallback(async () => {
    if (!sampleIdNum) return;
    try {
      const data = await HplcWorkspaceService.getSampleEntry(sampleIdNum);
      setSampleEntry(data);
      setReplicates(data.replicates || []);
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

  const handleSaveReplicates = async () => {
    if (!sampleEntry || !sampleIdNum) return;
    setSaving(true);
    setError(null);

    const payload: HplcReplicateInput[] = replicates.map((r) => ({
      actualWeightMg: r.actualWeightMg,
      responses: r.responses.map((resp) => ({
        hplcMethodAnalyteId: resp.hplcMethodAnalyteId,
        response: resp.response
      }))
    }));

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
  const methodWeights = sampleEntry.methodWeights || [];
  // Every weight and response must be a positive number before saving.
  const canSave = replicatesComplete(methodWeights, replicates);

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

            <Tooltip title={!sampleEntry.canSubmit ? (sampleEntry.canSubmitReason || "Prerequisites not met") : ""}>
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
      </Paper>

      <Stack spacing={2.5}>
        {/* 1. Method theoretical weights snapshot */}
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

        {/* 2. Replicate entry */}
        <ResultSection step={2} title="Replicates">
          <ReplicateEntryTable
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
                  <Typography variant="caption" sx={{ color: "text.secondary" }}>
                    Enter a positive weight and response in every cell to save.
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

        {/* 3. Results: Official when submitted, otherwise the server-calculated preview */}
        <ResultSection step={3} title={sampleEntry.submitted ? "Results (official)" : "Results (preview)"}>
          {sampleEntry.submitted ? (
            <OfficialResultsCard official={sampleEntry.official || []} basis={sampleEntry.basis} />
          ) : (
            <CalculationSummaryCard preview={sampleEntry.preview || []} basis={sampleEntry.basis} />
          )}
        </ResultSection>

        {/* 4. Chromatogram / evidence upload */}
        <ResultSection step={4} title="Evidence: chromatograms & reports">
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
