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
  sampleEntry?: HplcSampleEntryDto;
  qualificationEntry?: HplcQualificationEntryDto;
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

  const isQualification = qualificationEntry != null;
  const target = qualificationEntry ?? sampleEntry;
  if (!target) return null;

  const hasRequiredReps =
    target.requiredReplicates == null ||
    target.replicates.length >= target.requiredReplicates;

  const currentEvidenceCount = target.evidence.filter((e) => e.isCurrent).length;
  const sstPassed = target.sstStatus === "Passed";

  const handleProceedToSign = () => {
    setSubmitError(null);
    setSignatureOpen(true);
  };

  const handleSignatureConfirm = async (password: string) => {
    setSubmitting(true);
    setSubmitError(null);
    try {
      if (qualificationEntry) {
        await HplcWorkspaceService.submitQualification(qualificationEntry.runSampleId, {
          password,
          comment: comment.trim() || null
        });
        toast.success(`Qualification ${qualificationEntry.qualificationCode} submitted for review.`);
      } else if (sampleEntry) {
        await HplcWorkspaceService.submitSample(sampleEntry.runSampleId, {
          password,
          comment: comment.trim() || null
        });
        toast.success(`Sample ${sampleEntry.sampleNumber} submitted for review.`);
      }
      setSignatureOpen(false);
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      const msg = e.response?.data?.message ?? e.message ?? "Submission failed.";
      setSubmitError(msg);
      throw err; // rethrow so SignatureDialog catches it and shows error
    } finally {
      setSubmitting(false);
    }
  };

  const meaningStatement = qualificationEntry
    ? `I confirm that chromatographic replicate data and evidence for working standard ${qualificationEntry.qualificationCode} (${qualificationEntry.materialName}) are complete, accurate, and ready for review.`
    : `I confirm that chromatographic replicate data and evidence for sample ${sampleEntry?.sampleNumber} (${sampleEntry?.testCode}) are complete, accurate, and ready for review.`;

  return (
    <>
      <Dialog
        open={open}
        onClose={() => !submitting && onClose()}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle sx={{ fontWeight: 700, pb: 1 }}>
          {isQualification ? "Submit Qualification for Review" : "Send Sample for Review"}
        </DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2.5}>
            {submitError && <Alert severity="error">{submitError}</Alert>}

            {!target.canSubmit && (
              <Alert severity="warning">
                <strong>Cannot submit for review:</strong>{" "}
                {target.canSubmitReason || "Submission criteria are not met."}
              </Alert>
            )}

            <Box>
              <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
                {qualificationEntry ? (
                  <>
                    Qualification: <strong>{qualificationEntry.qualificationCode}</strong> · Material:{" "}
                    <strong>{qualificationEntry.materialName}</strong>
                  </>
                ) : (
                  <>
                    Sample: <strong>{sampleEntry?.sampleNumber}</strong> · Test:{" "}
                    <strong>{sampleEntry?.testCode}</strong>
                    {sampleEntry?.productName ? ` · Product: ${sampleEntry.productName}` : ""}
                  </>
                )}
              </Typography>
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                Verify the pre-submission readiness checklist below before applying your electronic signature.
              </Typography>
            </Box>

            <Divider />

            <List dense disablePadding>
              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  {sstPassed ? (
                    <CheckCircleIcon color="success" fontSize="small" />
                  ) : (
                    <CancelIcon color="error" fontSize="small" />
                  )}
                </ListItemIcon>
                <ListItemText
                  primary="System Suitability"
                  secondary={`Status: ${target.sstStatus} (Code: ${target.sstCode})`}
                  slotProps={{
                    primary: { sx: { fontWeight: 600, fontSize: 13 } },
                    secondary: { sx: { fontSize: 12 } }
                  }}
                />
              </ListItem>

              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  {hasRequiredReps ? (
                    <CheckCircleIcon color="success" fontSize="small" />
                  ) : (
                    <CancelIcon color="error" fontSize="small" />
                  )}
                </ListItemIcon>
                <ListItemText
                  primary="Replicate Count"
                  secondary={`${target.replicates.length} replicate(s) entered · Required: ${target.requiredReplicates ?? "—"}`}
                  slotProps={{
                    primary: { sx: { fontWeight: 600, fontSize: 13 } },
                    secondary: { sx: { fontSize: 12 } }
                  }}
                />
              </ListItem>

              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  {currentEvidenceCount > 0 ? (
                    <CheckCircleIcon color="success" fontSize="small" />
                  ) : (
                    <CancelIcon color="warning" fontSize="small" />
                  )}
                </ListItemIcon>
                <ListItemText
                  primary="Evidence Reports"
                  secondary={`${currentEvidenceCount} current chromatogram/evidence file(s) attached`}
                  slotProps={{
                    primary: { sx: { fontWeight: 600, fontSize: 13 } },
                    secondary: { sx: { fontSize: 12 } }
                  }}
                />
              </ListItem>

              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  {target.canSubmit ? (
                    <CheckCircleIcon color="success" fontSize="small" />
                  ) : (
                    <CancelIcon color="error" fontSize="small" />
                  )}
                </ListItemIcon>
                <ListItemText
                  primary="Submission Gate"
                  secondary={target.canSubmit ? "Ready for electronic signature" : (target.canSubmitReason || "Gated")}
                  slotProps={{
                    primary: { sx: { fontWeight: 600, fontSize: 13 } },
                    secondary: { sx: { fontSize: 12 } }
                  }}
                />
              </ListItem>
            </List>

            <Alert severity="info" sx={{ fontSize: 12 }}>
              {isQualification
                ? "Signing will freeze replicate data, record qualification results, and move the working standard qualification to Assayed status for review."
                : (
                  <>
                    Signing will freeze this sample entry, calculate official results, and move the test order to <strong>Under Review</strong> in the testing workspace.
                  </>
                )}
            </Alert>
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button
            onClick={onClose}
            disabled={submitting}
          >
            Cancel
          </Button>
          <Button
            variant="contained"
            color="primary"
            onClick={handleProceedToSign}
            disabled={!target.canSubmit || submitting}
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
        meaningStatement={meaningStatement}
        showComment={true}
        comment={comment}
        onCommentChange={setComment}
        onCancel={() => setSignatureOpen(false)}
        onConfirm={handleSignatureConfirm}
      />
    </>
  );
}
