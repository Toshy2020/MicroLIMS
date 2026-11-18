import React, { useState, useEffect } from "react";
import {
  Box,
  Button,
  Stack,
  TextField,
  Typography,
  Alert
} from "@mui/material";
import { toast } from "sonner";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { useAuth } from "../../../contexts/AuthContext";
import { useTranslation } from "../../../i18n";
import { serverMessage } from "../../../utils/errorMessage";
import { WorkingStandardService } from "../services/WorkingStandardService";
import type { WorkingStandardQualificationDto } from "../types";

export type SignOffActionType = "review" | "return" | "rejectReview" | "approve" | "rejectApproval";

interface SignOffDialogProps {
  open: boolean;
  actionType: SignOffActionType | null;
  qualificationId: number;
  qualificationCode: string;
  onClose: () => void;
  onSuccess: (updated: WorkingStandardQualificationDto) => void;
}

export const SignOffDialog: React.FC<SignOffDialogProps> = ({
  open,
  actionType,
  qualificationId,
  qualificationCode,
  onClose,
  onSuccess
}) => {
  const { t } = useTranslation("workingStandards");
  const { fullName, username, role } = useAuth();
  const [password, setPassword] = useState("");
  const [comment, setComment] = useState("");
  const [reason, setReason] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setPassword("");
      setComment("");
      setReason("");
      setError(null);
      setSubmitting(false);
    }
  }, [open, actionType]);

  if (!actionType) return null;

  const isReturn = actionType === "return";
  const isReject = actionType === "rejectReview" || actionType === "rejectApproval";
  const isApprove = actionType === "approve";
  const isReview = actionType === "review";

  let title = t("signOffDialog.defaultTitle");
  let meaningStatement = "";
  let confirmLabel = t("signOffDialog.defaultConfirm");
  let confirmColor: "primary" | "error" | "warning" = "primary";

  if (isReview) {
    title = t("signOffDialog.reviewTitle", { code: qualificationCode });
    meaningStatement = t("signOffDialog.reviewMeaning");
    confirmLabel = t("signOffDialog.reviewConfirm");
  } else if (isApprove) {
    title = t("signOffDialog.approveTitle", { code: qualificationCode });
    meaningStatement = t("signOffDialog.approveMeaning");
    confirmLabel = t("signOffDialog.approveConfirm");
  } else if (isReject) {
    title = t("signOffDialog.rejectTitle", { code: qualificationCode });
    meaningStatement = t("signOffDialog.rejectMeaning");
    confirmLabel = t("signOffDialog.rejectConfirm");
    confirmColor = "error";
  } else if (isReturn) {
    title = t("signOffDialog.returnTitle", { code: qualificationCode });
    meaningStatement = t("signOffDialog.returnMeaning");
    confirmLabel = t("signOffDialog.returnConfirm");
    confirmColor = "warning";
  }

  const isPasswordRequired = !isReturn;
  const isReasonRequired = isReject || isReturn;

  const isValid = (!isPasswordRequired || password.trim().length > 0) &&
    (!isReasonRequired || reason.trim().length > 0);

  const handleConfirm = async () => {
    if (!isValid || submitting) return;
    setSubmitting(true);
    setError(null);

    try {
      let result: WorkingStandardQualificationDto;
      if (isReview) {
        result = await WorkingStandardService.review(qualificationId, {
          password,
          comment: comment.trim() || undefined
        });
        toast.success(t("signOffDialog.toastReviewed", { code: qualificationCode }));
      } else if (isApprove) {
        result = await WorkingStandardService.approve(qualificationId, {
          password,
          comment: comment.trim() || undefined
        });
        toast.success(t("signOffDialog.toastApproved", { code: qualificationCode }));
      } else if (actionType === "rejectReview") {
        result = await WorkingStandardService.rejectAtReview(qualificationId, {
          password,
          reason: reason.trim()
        });
        toast.success(t("signOffDialog.toastRejected", { code: qualificationCode }));
      } else if (actionType === "rejectApproval") {
        result = await WorkingStandardService.rejectAtApproval(qualificationId, {
          password,
          reason: reason.trim()
        });
        toast.success(t("signOffDialog.toastRejected", { code: qualificationCode }));
      } else {
        result = await WorkingStandardService.returnQualification(qualificationId, {
          reason: reason.trim()
        });
        toast.success(t("signOffDialog.toastReturned", { code: qualificationCode }));
      }

      onSuccess(result);
      onClose();
    } catch (err: unknown) {
      const msg = serverMessage(err) ?? t("signOffDialog.actionFailed");
      setError(msg);
      setPassword("");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      title={title}
      onClose={onClose}
      maxWidth="xs"
      actions={
        <>
          <Button onClick={onClose} disabled={submitting}>{t("actions.cancel")}</Button>
          <Button
            variant="contained"
            color={confirmColor}
            onClick={handleConfirm}
            disabled={!isValid || submitting}
          >
            {submitting ? t("actions.processing") : confirmLabel}
          </Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ mt: 1 }}>
        {error && <Alert severity="error">{error}</Alert>}

        <Box>
          <Typography sx={{ fontWeight: 700 }}>{fullName ?? username}</Typography>
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{role}</Typography>
        </Box>

        <Alert severity={isReject ? "error" : isReturn ? "warning" : "info"}>
          {meaningStatement}
        </Alert>

        {isReasonRequired && (
          <TextField
            label={t("signOffDialog.reasonLabel")}
            multiline
            rows={3}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            required
            autoFocus={!isPasswordRequired}
            placeholder={t("signOffDialog.reasonPlaceholder")}
          />
        )}

        {(isReview || isApprove) && (
          <TextField
            label={t("signOffDialog.commentLabel")}
            multiline
            rows={2}
            value={comment}
            onChange={(e) => setComment(e.target.value)}
          />
        )}

        {isPasswordRequired && (
          <TextField
            label={t("signOffDialog.passwordLabel")}
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            autoFocus={!isReasonRequired}
            onKeyDown={(e) => {
              if (e.key === "Enter" && isValid) handleConfirm();
            }}
          />
        )}
      </Stack>
    </FloatingDialog>
  );
};
