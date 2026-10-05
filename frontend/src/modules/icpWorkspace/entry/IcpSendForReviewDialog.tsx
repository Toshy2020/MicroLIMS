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
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import type { IcpSampleEntryDto } from "../types";

export interface IcpSendForReviewDialogProps {
  open: boolean;
  entry: IcpSampleEntryDto;
  onClose: () => void;
  onSuccess: () => void;
}

export function IcpSendForReviewDialog({
  open,
  entry,
  onClose,
  onSuccess
}: IcpSendForReviewDialogProps) {
  const [signatureOpen, setSignatureOpen] = useState(false);
  const [comment, setComment] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const calConfirmed = entry.calibrationStatus === "Confirmed";
  const allElementsValid = entry.elements.length > 0 && entry.elements.every((e) => e.valid);
  const firstInvalidElement = entry.elements.find((e) => !e.valid);
  const hasReplicates = entry.replicates.length > 0;
  const currentReportCount = (entry.evidence || []).filter(
    (e) => e.context === "Sample" && e.kind === "SampleReport" && e.isCurrent
  ).length;
  const hasPreview = entry.preview.length > 0;
  const hasPreviewProblem = entry.preview.some((p) => p.problem != null);

  const handleProceedToSign = () => {
    setSubmitError(null);
    setSignatureOpen(true);
  };

  const handleSignatureConfirm = async (password: string) => {
    setSubmitting(true);
    setSubmitError(null);
    try {
      await IcpWorkspaceService.submitSample(entry.runSampleId, {
        password,
        comment: comment.trim() || null
      });
      toast.success(`Sample ${entry.sampleNumber} submitted for peer review.`);
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

  const meaningStatement = `I confirm that ICP replicate data, element concentrations, and evidence for sample ${entry.sampleNumber} (${entry.testCode}) are complete, accurate, and ready for review.`;

  return (
    <>
      <Dialog
        open={open}
        onClose={() => !submitting && onClose()}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle sx={{ fontWeight: 700, pb: 1 }}>
          Send ICP Sample for Review
        </DialogTitle>
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
              <Typography variant="body2" sx={{ color: "text.secondary", mb: 0.5 }}>
                Sample: <strong>{entry.sampleNumber}</strong> · Test: <strong>{entry.testCode}</strong>
                {entry.productName ? ` · Product: ${entry.productName}` : ""}
                {entry.batchNumber ? ` · Batch: ${entry.batchNumber}` : ""}
              </Typography>
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                Verify the pre-submission readiness checklist below before applying your electronic signature.
              </Typography>
            </Box>

            <Divider />

            <List dense disablePadding>
              {/* 1. Calibration Status */}
              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  {calConfirmed ? (
                    <CheckCircleIcon color="success" fontSize="small" />
                  ) : (
                    <CancelIcon color="error" fontSize="small" />
                  )}
                </ListItemIcon>
                <ListItemText
                  primary="Instrument Calibration"
                  secondary={`Status: ${entry.calibrationStatus} (Code: ${entry.calibrationCode})`}
                  slotProps={{
                    primary: { sx: { fontWeight: 600, fontSize: 13 } },
                    secondary: { sx: { fontSize: 12 } }
                  }}
                />
              </ListItem>

              {/* 2. Element Availability */}
              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  {allElementsValid ? (
                    <CheckCircleIcon color="success" fontSize="small" />
                  ) : (
                    <CancelIcon color="error" fontSize="small" />
                  )}
                </ListItemIcon>
                <ListItemText
                  primary="Element Availability"
                  secondary={
                    allElementsValid
                      ? `All ${entry.elements.length} required element(s) are valid`
                      : firstInvalidElement
                      ? `${firstInvalidElement.symbol}: ${firstInvalidElement.reason || "Invalid"}`
                      : "Required elements invalid"
                  }
                  slotProps={{
                    primary: { sx: { fontWeight: 600, fontSize: 13 } },
                    secondary: { sx: { fontSize: 12 } }
                  }}
                />
              </ListItem>

              {/* 3. Replicate Count */}
              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  {hasReplicates ? (
                    <CheckCircleIcon color="success" fontSize="small" />
                  ) : (
                    <CancelIcon color="error" fontSize="small" />
                  )}
                </ListItemIcon>
                <ListItemText
                  primary="Replicate Measurements"
                  secondary={`${entry.replicates.length} replicate measurement(s) recorded`}
                  slotProps={{
                    primary: { sx: { fontWeight: 600, fontSize: 13 } },
                    secondary: { sx: { fontSize: 12 } }
                  }}
                />
              </ListItem>

              {/* 4. Evidence Report */}
              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  {currentReportCount > 0 ? (
                    <CheckCircleIcon color="success" fontSize="small" />
                  ) : (
                    <CancelIcon color="warning" fontSize="small" />
                  )}
                </ListItemIcon>
                <ListItemText
                  primary="Instrument Report Evidence"
                  secondary={
                    currentReportCount > 0
                      ? `${currentReportCount} current sample report(s) attached`
                      : "Upload sample report before submitting"
                  }
                  slotProps={{
                    primary: { sx: { fontWeight: 600, fontSize: 13 } },
                    secondary: { sx: { fontSize: 12 } }
                  }}
                />
              </ListItem>

              {/* 5. Calculation Preview */}
              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  {hasPreview && !hasPreviewProblem ? (
                    <CheckCircleIcon color="success" fontSize="small" />
                  ) : (
                    <CancelIcon color="error" fontSize="small" />
                  )}
                </ListItemIcon>
                <ListItemText
                  primary="Server Calculation Preview"
                  secondary={
                    !hasPreview
                      ? "No preview calculation generated"
                      : hasPreviewProblem
                      ? "One or more elements have calculation or range warnings"
                      : `${entry.preview.length} parameter(s) evaluated within range`
                  }
                  slotProps={{
                    primary: { sx: { fontWeight: 600, fontSize: 13 } },
                    secondary: { sx: { fontSize: 12 } }
                  }}
                />
              </ListItem>

              {/* 6. Submission Gate */}
              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  {entry.canSubmit ? (
                    <CheckCircleIcon color="success" fontSize="small" />
                  ) : (
                    <CancelIcon color="error" fontSize="small" />
                  )}
                </ListItemIcon>
                <ListItemText
                  primary="Submission Gate"
                  secondary={entry.canSubmit ? "Ready for electronic signature" : (entry.canSubmitReason || "Gated")}
                  slotProps={{
                    primary: { sx: { fontWeight: 600, fontSize: 13 } },
                    secondary: { sx: { fontSize: 12 } }
                  }}
                />
              </ListItem>
            </List>

            <Alert severity="info" sx={{ fontSize: 12 }}>
              Signing will freeze replicate entries, record official ICP assay results, and move the test order to <strong>Under Review</strong> in the testing workspace.
            </Alert>
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={onClose} disabled={submitting}>
            Cancel
          </Button>
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
        meaningStatement={meaningStatement}
        showComment={true}
        comment={comment}
        onCommentChange={setComment}
        onConfirm={handleSignatureConfirm}
        onCancel={() => !submitting && setSignatureOpen(false)}
      />
    </>
  );
}
