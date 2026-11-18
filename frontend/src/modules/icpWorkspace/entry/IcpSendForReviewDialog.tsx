import { useState } from "react";
import { Trans } from "react-i18next";
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
import { Ltr } from "../../../components/Ltr";
import { i18n, useTranslation } from "../../../i18n";
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
  const { t } = useTranslation("icpWorkspace");
  const [signatureOpen, setSignatureOpen] = useState(false);
  const [comment, setComment] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const calConfirmed = entry.calibrationStatus === "Confirmed";
  const allElementsValid = entry.elements.length > 0 && entry.elements.every((e) => e.valid);
  const firstInvalidElement = entry.elements.find((e) => !e.valid);
  const hasResults = entry.elements.length > 0 && entry.elements.every((e) => e.resultPpm != null);
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
      toast.success(t("entry.sendDialog.submittedToast", { sampleNumber: entry.sampleNumber }));
      setSignatureOpen(false);
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      const msg = e.response?.data?.message ?? e.message ?? t("entry.sendDialog.submitFailed");
      setSubmitError(msg);
      throw err;
    } finally {
      setSubmitting(false);
    }
  };

  const meaningValues = { sampleNumber: entry.sampleNumber, testCode: entry.testCode };
  const meaningStatement = i18n.getFixedT("en", "icpWorkspace")("entry.sendDialog.meaning", meaningValues);
  const meaningStatementTranslation = t("entry.sendDialog.meaning", meaningValues);

  return (
    <>
      <Dialog
        open={open}
        onClose={() => !submitting && onClose()}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle sx={{ fontWeight: 700, pb: 1 }}>
          {t("entry.sendDialog.title")}
        </DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2.5}>
            {submitError && <Alert severity="error">{submitError}</Alert>}

            {!entry.canSubmit && (
              <Alert severity="warning">
                <strong>{t("entry.sendDialog.cannotSubmit")}</strong>{" "}
                {entry.canSubmitReason || t("entry.sendDialog.criteriaNotMet")}
              </Alert>
            )}

            <Box>
              <Typography variant="body2" sx={{ color: "text.secondary", mb: 0.5 }}>
                <Trans
                  i18nKey="entry.sendDialog.sampleSummary"
                  ns="icpWorkspace"
                  values={{ sampleNumber: entry.sampleNumber, testCode: entry.testCode }}
                  components={{ ltr: <Ltr>{null}</Ltr>, strong: <strong /> }}
                />
                {entry.productName ? t("entry.sendDialog.productSuffix", { productName: entry.productName }) : ""}
                {entry.batchNumber ? t("entry.sendDialog.batchSuffix", { batchNumber: entry.batchNumber }) : ""}
              </Typography>
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                {t("entry.sendDialog.verifyChecklist")}
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
                  primary={t("entry.sendDialog.calTitle")}
                  secondary={t("entry.sendDialog.calSecondary", { status: entry.calibrationStatus, code: entry.calibrationCode })}
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
                  primary={t("entry.sendDialog.elementsTitle")}
                  secondary={
                    allElementsValid
                      ? t("entry.sendDialog.elementsValid", { count: entry.elements.length })
                      : firstInvalidElement
                      ? `${firstInvalidElement.symbol}: ${firstInvalidElement.reason || t("entry.sendDialog.elementsInvalidFallback")}`
                      : t("entry.sendDialog.elementsRequiredInvalid")
                  }
                  slotProps={{
                    primary: { sx: { fontWeight: 600, fontSize: 13 } },
                    secondary: { sx: { fontSize: 12 } }
                  }}
                />
              </ListItem>

              {/* 3. Instrument results */}
              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  {hasResults ? (
                    <CheckCircleIcon color="success" fontSize="small" />
                  ) : (
                    <CancelIcon color="error" fontSize="small" />
                  )}
                </ListItemIcon>
                <ListItemText
                  primary={t("entry.sendDialog.resultsTitle")}
                  secondary={t("entry.sendDialog.resultsSecondary", {
                    recorded: entry.elements.filter((e) => e.resultPpm != null).length,
                    total: entry.elements.length
                  })}
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
                  primary={t("entry.sendDialog.evidenceTitle")}
                  secondary={
                    currentReportCount > 0
                      ? t("entry.sendDialog.evidenceAttached", { count: currentReportCount })
                      : t("entry.sendDialog.evidenceMissing")
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
                  primary={t("entry.sendDialog.previewTitle")}
                  secondary={
                    !hasPreview
                      ? t("entry.sendDialog.previewNone")
                      : hasPreviewProblem
                      ? t("entry.sendDialog.previewProblem")
                      : t("entry.sendDialog.previewOk", { count: entry.preview.length })
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
                  primary={t("entry.sendDialog.gateTitle")}
                  secondary={entry.canSubmit ? t("entry.sendDialog.gateReady") : (entry.canSubmitReason || t("entry.sendDialog.gateGated"))}
                  slotProps={{
                    primary: { sx: { fontWeight: 600, fontSize: 13 } },
                    secondary: { sx: { fontSize: 12 } }
                  }}
                />
              </ListItem>
            </List>

            <Alert severity="info" sx={{ fontSize: 12 }}>
              <Trans
                i18nKey="entry.sendDialog.freezeInfo"
                ns="icpWorkspace"
                components={{ strong: <strong /> }}
              />
            </Alert>
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={onClose} disabled={submitting}>
            {t("entry.sendDialog.cancel")}
          </Button>
          <Button
            variant="contained"
            color="primary"
            onClick={handleProceedToSign}
            disabled={!entry.canSubmit || submitting}
            startIcon={<DrawIcon />}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            {t("entry.sendDialog.proceedToSign")}
          </Button>
        </DialogActions>
      </Dialog>

      {/* 21 CFR Part 11 Electronic Signature Dialog */}
      <SignatureDialog
        open={signatureOpen}
        meaningStatement={meaningStatement}
        meaningStatementTranslation={meaningStatementTranslation}
        showComment={true}
        comment={comment}
        onCommentChange={setComment}
        onConfirm={handleSignatureConfirm}
        onCancel={() => !submitting && setSignatureOpen(false)}
      />
    </>
  );
}
