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
  const { t } = useTranslation("hplcWorkspace");
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
        toast.success(t("entry.sendDialog.submittedQualToast", { code: qualificationEntry.qualificationCode }));
      } else if (sampleEntry) {
        await HplcWorkspaceService.submitSample(sampleEntry.runSampleId, {
          password,
          comment: comment.trim() || null
        });
        toast.success(t("entry.sendDialog.submittedSampleToast", { sampleNumber: sampleEntry.sampleNumber }));
      }
      setSignatureOpen(false);
      onSuccess();
      onClose();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      const msg = e.response?.data?.message ?? e.message ?? t("entry.sendDialog.submitFailed");
      setSubmitError(msg);
      throw err; // rethrow so SignatureDialog catches it and shows error
    } finally {
      setSubmitting(false);
    }
  };

  const meaningKey = qualificationEntry ? "entry.sendDialog.meaningQual" : "entry.sendDialog.meaningSample";
  const meaningValues = qualificationEntry
    ? { code: qualificationEntry.qualificationCode, materialName: qualificationEntry.materialName }
    : { sampleNumber: sampleEntry?.sampleNumber ?? "", testCode: sampleEntry?.testCode ?? "" };
  const meaningStatement = i18n.getFixedT("en", "hplcWorkspace")(meaningKey, meaningValues);
  const meaningStatementTranslation = t(meaningKey, meaningValues);

  return (
    <>
      <Dialog
        open={open}
        onClose={() => !submitting && onClose()}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle sx={{ fontWeight: 700, pb: 1 }}>
          {isQualification ? t("entry.sendDialog.titleQual") : t("entry.sendDialog.titleSample")}
        </DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2.5}>
            {submitError && <Alert severity="error">{submitError}</Alert>}

            {!target.canSubmit && (
              <Alert severity="warning">
                <strong>{t("entry.sendDialog.cannotSubmit")}</strong>{" "}
                {target.canSubmitReason || t("entry.sendDialog.criteriaNotMet")}
              </Alert>
            )}

            <Box>
              <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
                {qualificationEntry ? (
                  <Trans
                    i18nKey="entry.sendDialog.qualSummary"
                    ns="hplcWorkspace"
                    values={{ code: qualificationEntry.qualificationCode, materialName: qualificationEntry.materialName }}
                    components={{ ltr: <Ltr>{null}</Ltr>, strong: <strong /> }}
                  />
                ) : (
                  <>
                    <Trans
                      i18nKey="entry.sendDialog.sampleSummary"
                      ns="hplcWorkspace"
                      values={{ sampleNumber: sampleEntry?.sampleNumber ?? "", testCode: sampleEntry?.testCode ?? "" }}
                      components={{ ltr: <Ltr>{null}</Ltr>, strong: <strong /> }}
                    />
                    {sampleEntry?.productName ? t("entry.sendDialog.productSuffix", { productName: sampleEntry.productName }) : ""}
                  </>
                )}
              </Typography>
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                {t("entry.sendDialog.verifyChecklist")}
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
                  primary={t("entry.sendDialog.sstTitle")}
                  secondary={t("entry.sendDialog.sstSecondary", { status: target.sstStatus, code: target.sstCode })}
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
                  primary={t("entry.sendDialog.repTitle")}
                  secondary={t("entry.sendDialog.repSecondary", { entered: target.replicates.length, required: target.requiredReplicates ?? "—" })}
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
                  primary={t("entry.sendDialog.evidenceTitle")}
                  secondary={t("entry.sendDialog.evidenceSecondary", { count: currentEvidenceCount })}
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
                  primary={t("entry.sendDialog.gateTitle")}
                  secondary={target.canSubmit ? t("entry.sendDialog.gateReady") : (target.canSubmitReason || t("entry.sendDialog.gateGated"))}
                  slotProps={{
                    primary: { sx: { fontWeight: 600, fontSize: 13 } },
                    secondary: { sx: { fontSize: 12 } }
                  }}
                />
              </ListItem>
            </List>

            <Alert severity="info" sx={{ fontSize: 12 }}>
              {isQualification
                ? t("entry.sendDialog.freezeInfoQual")
                : (
                  <Trans
                    i18nKey="entry.sendDialog.freezeInfoSample"
                    ns="hplcWorkspace"
                    components={{ strong: <strong /> }}
                  />
                )}
            </Alert>
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button
            onClick={onClose}
            disabled={submitting}
          >
            {t("entry.sendDialog.cancel")}
          </Button>
          <Button
            variant="contained"
            color="primary"
            onClick={handleProceedToSign}
            disabled={!target.canSubmit || submitting}
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
        onCancel={() => setSignatureOpen(false)}
        onConfirm={handleSignatureConfirm}
      />
    </>
  );
}
