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
  Chip,
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
import { ReplicateEntryTable } from "./ReplicateEntryTable";
import { CalculationSummaryCard } from "./CalculationSummaryCard";
import { SendForReviewDialog } from "./SendForReviewDialog";
import { ReportUploadPanel } from "../evidence/ReportUploadPanel";
import { tableHeadSx } from "../../../theme";
import type {
  HplcSampleEntryDto,
  HplcReplicateDto,
  HplcReplicateInput
} from "../types";

export function HplcSampleEntryPage() {
  const theme = useTheme();
  const navigate = useNavigate();
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
    navigate(`/hplc-workspace/${instrumentId}/run/${runId}/samples`);
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
              <Chip
                label={`Basis: ${sampleEntry.basis}`}
                color="primary"
                size="small"
                variant="outlined"
              />
              <Chip
                label={`SST: ${sampleEntry.sstCode} (${sampleEntry.sstStatus})`}
                color={sampleEntry.sstStatus === "Passed" ? "success" : "warning"}
                size="small"
                variant="outlined"
              />
              {sampleEntry.submitted && (
                <Chip label="Submitted for Review" color="success" size="small" />
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
              disabled={!isEditable || saving}
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
        {/* Method Theoretical Weights snapshot */}
        {sampleEntry.methodWeights && sampleEntry.methodWeights.length > 0 && (
          <Paper
            elevation={0}
            sx={{
              borderRadius: 2,
              border: `1px solid ${theme.palette.divider}`,
              overflow: "hidden"
            }}
          >
            <Box sx={{ p: 2, borderBottom: `1px solid ${theme.palette.divider}` }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Method Theoretical Weights
              </Typography>
            </Box>
            <TableContainer>
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
                  <TableRow>
                    <TableCell>Analyte</TableCell>
                    <TableCell>Theoretical Weight Std (mg)</TableCell>
                    <TableCell>Theoretical Weight Test (mg)</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {sampleEntry.methodWeights.map((mw) => (
                    <TableRow key={mw.hplcMethodAnalyteId}>
                      <TableCell sx={{ fontWeight: 600 }}>{mw.analyteName}</TableCell>
                      <TableCell>{mw.theoreticalWeightStdMg}</TableCell>
                      <TableCell>{mw.theoreticalWeightTestMg}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </Paper>
        )}

        {/* Replicate Entry Table */}
        <ReplicateEntryTable
          methodWeights={sampleEntry.methodWeights || []}
          replicates={replicates}
          onChange={setReplicates}
          requiredReplicates={sampleEntry.requiredReplicates}
          disabled={!isEditable || saving}
        />

        {/* Calculation Summary Card */}
        <CalculationSummaryCard
          preview={sampleEntry.preview || []}
          basis={sampleEntry.basis}
        />

        {/* Chromatogram / Evidence Upload Panel */}
        <Paper
          elevation={0}
          sx={{
            p: 2,
            borderRadius: 2,
            border: `1px solid ${theme.palette.divider}`
          }}
        >
          <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1.5 }}>
            Sample Chromatograms & Reports
          </Typography>
          <ReportUploadPanel
            runId={parsedRunId || sampleEntry.hplcRunId}
            runSampleId={sampleEntry.runSampleId}
            context="Sample"
            kind="SampleReport"
            evidenceList={sampleEntry.evidence || []}
            onChanged={loadData}
          />
        </Paper>
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
