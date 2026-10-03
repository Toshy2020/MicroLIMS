import { useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  Stack,
  Alert,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Divider
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CancelIcon from "@mui/icons-material/Cancel";
import DrawIcon from "@mui/icons-material/Draw";
import { toast } from "sonner";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import type { HplcSampleEntryDto, HplcQualificationEntryDto } from "../types";

export interface SendForReviewDialogProps {
  open: boolean;
  sampleEntry?: HplcSampleEntryDto | null;
  qualificationEntry?: HplcQualificationEntryDto | null;
  onClose: () => void;
  onSuccess: () => void;
}

export function SendForReviewDialog({
  open,
  sampleEntry,
  qualificationEntry,
  onClose,
  onSuccess
}: SendForReviewDialogProps) {
  const [signatureOpen, setSignatureOpen] = useState(false);
  const [comment, setComment] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const entry = qualificationEntry
    ? {
        isQualification: true,
        runSampleId: qualificationEntry.runSampleId,
        label: qualificationEntry.qualificationCode,
        detail: "WS qualification",
        product: qualificationEntry.materialName,
        sstStatus: qualificationEntry.sstStatus,
        sstCode: qualificationEntry.sstCode,
        replicatesCount: qualificationEntry.replicates.length,
        requiredReplicates: qualificationEntry.requiredReplicates,
        evidence: qualificationEntry.evidence,
        canSubmit: qualificationEntry.canSubmit,
        canSubmitReason: qualificationEntry.canSubmitReason,
        title: "Submit Qualification for Review",
        meaning: `I confirm that chromatographic replicate data and evidence for working standard ${qualificationEntry.qualificationCode} (${qualificationEntry.materialName}) are complete, accurate, and ready for review.`,
        infoText: "Signing will freeze replicate data, record qualification results, and move the working standard qualification to Assayed status for review."
      }
    : sampleEntry
    ? {
        isQualification: false,
        runSampleId: sampleEntry.runSampleId,
        label: sampleEntry.sampleNumber,
        detail: sampleEntry.testCode,
        product: sampleEntry.productName,
        sstStatus: sampleEntry.sstStatus,
        sstCode: sampleEntry.sstCode,
        replicatesCount: sampleEntry.replicates.length,
        requiredReplicates: sampleEntry.requiredReplicates,
        evidence: sampleEntry.evidence,
        canSubmit: sampleEntry.canSubmit,
        canSubmitReason: sampleEntry.canSubmitReason,
        title: "Send Sample for Review",
        meaning: `I confirm that chromatographic replicate data and evidence for sample ${sampleEntry.sampleNumber} (${sampleEntry.testCode}) are complete, accurate, and ready for review.`,
        infoText: "Signing will freeze this sample entry, calculate official results, and move the test order to Under Review in the testing workspace."
      }
    : null;

  const hasRequiredReps =
    entry != null &&
    (entry.requiredReplicates == null || entry.replicatesCount >= entry.requiredReplicates);

  const currentEvidenceCount = entry ? entry.evidence.filter((e) => e.isCurrent).length : 0;
  const sstPassed = entry?.sstStatus === "Passed";

  const handleProceedToSign = () => {
    setSubmitError(null);
    setSignatureOpen(true);
  };

  const handleSignatureConfirm = async (password: string) => {
    if (!entry) return;
    setSubmitting(true);
    setSubmitError(null);
    try {
      if (entry.isQualification) {
        await HplcWorkspaceService.submitQualification(entry.runSampleId, {
          password,
          comment: comment.trim() || null
        });
        toast.success(`Qualification ${entry.label} submitted for review.`);
      } else {
        await HplcWorkspaceService.submitSample(entry.runSampleId, {
          password,
          comment: comment.trim() || null
        });
        toast.success(`Sample ${entry.label} submitted for review.`);
      }
      setSignatureOpen(false);
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      const msg = e.response?.data?.message ?? e.message ?? "Submission failed.";
      setSubmitError(msg);
      throw err;
    } finally {
      setSubmitting(false);
    }
  };

  if (!entry) return null;

  return (
    <>
      <Dialog
        open={open}
        onClose={() => !submitting && onClose()}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle sx={{ fontWeight: 700, pb: 1 }}>{entry.title}</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2.5}>
            {submitError && <Alert severity="error">{submitError}</Alert>}

            {!entry.canSubmit && (
              <Alert severity="warning">
                <strong>Cannot submit for review:</strong>{" "}
                {entry.canSubmitReason || "Submission criteria are not met."}
              </Alert>
            )}

            <Box>
              <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
                {entry.isQualification ? "Qualification: " : "Sample: "}
                <strong>{entry.label}</strong> · {entry.isQualification ? "Material: " : "Test: "}
                <strong>{entry.product ?? entry.detail}</strong>
                {!entry.isQualification && entry.product ? ` · Product: ${entry.product}` : ""}
              </Typography>
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                Verify the pre-submission readiness checklist below before applying your electronic signature.
              </Typography>
            </Box>

            <Divider />

            <List dense disablePadding>
              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  {sstPassed ? <CheckCircleIcon color="success" fontSize="small" /> : <CancelIcon color="error" fontSize="small" />}
                </ListItemIcon>
                <ListItemText
                  primary="System Suitability"
                  secondary={`Status: ${entry.sstStatus} (Code: ${entry.sstCode})`}
                  slotProps={{ primary: { sx: { fontWeight: 600, fontSize: 13 } }, secondary: { sx: { fontSize: 12 } } }}
                />
              </ListItem>

              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  {hasRequiredReps ? <CheckCircleIcon color="success" fontSize="small" /> : <CancelIcon color="error" fontSize="small" />}
                </ListItemIcon>
                <ListItemText
                  primary="Replicate Count"
                  secondary={`${entry.replicatesCount} replicate(s) entered · Required: ${entry.requiredReplicates ?? "—"}`}
                  slotProps={{ primary: { sx: { fontWeight: 600, fontSize: 13 } }, secondary: { sx: { fontSize: 12 } } }}
                />
              </ListItem>

              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  {currentEvidenceCount > 0 ? <CheckCircleIcon color="success" fontSize="small" /> : <CancelIcon color="warning" fontSize="small" />}
                </ListItemIcon>
                <ListItemText
                  primary="Evidence Reports"
                  secondary={`${currentEvidenceCount} current chromatogram/evidence file(s) attached`}
                  slotProps={{ primary: { sx: { fontWeight: 600, fontSize: 13 } }, secondary: { sx: { fontSize: 12 } } }}
                />
              </ListItem>

              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  {entry.canSubmit ? <CheckCircleIcon color="success" fontSize="small" /> : <CancelIcon color="error" fontSize="small" />}
                </ListItemIcon>
                <ListItemText
                  primary="Submission Gate"
                  secondary={entry.canSubmit ? "Ready for electronic signature" : (entry.canSubmitReason || "Gated")}
                  slotProps={{ primary: { sx: { fontWeight: 600, fontSize: 13 } }, secondary: { sx: { fontSize: 12 } } }}
                />
              </ListItem>
            </List>

            <Alert severity="info" sx={{ fontSize: 12 }}>{entry.infoText}</Alert>
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={onClose} disabled={submitting}>Cancel</Button>
          <Button
            variant="contained"
            color="primary"
            onClick={handleProceedToSign}
            disabled={!entry.canSubmit || submitting}
            startIcon={<DrawIcon />}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            Proceed to Sign
          </Button>
        </DialogActions>
      </Dialog>

      {/* 21 CFR Part 11 Electronic Signature Dialog */}
      <SignatureDialog
        open={signatureOpen}
        meaningStatement={entry.meaning}
        showComment={true}
        comment={comment}
        onCommentChange={setComment}
        onCancel={() => setSignatureOpen(false)}
        onConfirm={handleSignatureConfirm}
      />
    </>
  );
}
