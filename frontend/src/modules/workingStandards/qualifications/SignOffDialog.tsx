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

  let title = "Electronic Signature";
  let meaningStatement = "";
  let confirmLabel = "Sign";
  let confirmColor: "primary" | "error" | "warning" = "primary";

  if (isReview) {
    title = `Review Qualification ${qualificationCode}`;
    meaningStatement = "I confirm that I have reviewed this working standard qualification record and verified the analytical results.";
    confirmLabel = "Sign & Review";
  } else if (isApprove) {
    title = `Approve Qualification ${qualificationCode}`;
    meaningStatement = "I approve this working standard qualification and release the standard for analytical testing use.";
    confirmLabel = "Sign & Approve";
  } else if (isReject) {
    title = `Reject Qualification ${qualificationCode}`;
    meaningStatement = "I am rejecting this working standard qualification. A detailed justification is required.";
    confirmLabel = "Sign & Reject";
    confirmColor = "error";
  } else if (isReturn) {
    title = `Return Qualification ${qualificationCode}`;
    meaningStatement = "Return qualification to Draft status for correction. Please provide a reason.";
    confirmLabel = "Return to Draft";
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
        toast.success(`Qualification ${qualificationCode} reviewed successfully`);
      } else if (isApprove) {
        result = await WorkingStandardService.approve(qualificationId, {
          password,
          comment: comment.trim() || undefined
        });
        toast.success(`Qualification ${qualificationCode} approved successfully`);
      } else if (actionType === "rejectReview") {
        result = await WorkingStandardService.rejectAtReview(qualificationId, {
          password,
          reason: reason.trim()
        });
        toast.success(`Qualification ${qualificationCode} rejected`);
      } else if (actionType === "rejectApproval") {
        result = await WorkingStandardService.rejectAtApproval(qualificationId, {
          password,
          reason: reason.trim()
        });
        toast.success(`Qualification ${qualificationCode} rejected`);
      } else {
        result = await WorkingStandardService.returnQualification(qualificationId, {
          reason: reason.trim()
        });
        toast.success(`Qualification ${qualificationCode} returned to Draft`);
      }

      onSuccess(result);
      onClose();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
        ?? "Action failed. Please check your credentials and try again.";
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
          <Button onClick={onClose} disabled={submitting}>Cancel</Button>
          <Button
            variant="contained"
            color={confirmColor}
            onClick={handleConfirm}
            disabled={!isValid || submitting}
          >
            {submitting ? "Processing..." : confirmLabel}
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
            label="Reason / Justification"
            multiline
            rows={3}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            required
            autoFocus={!isPasswordRequired}
            placeholder="Enter reason..."
          />
        )}

        {(isReview || isApprove) && (
          <TextField
            label="Comment (optional)"
            multiline
            rows={2}
            value={comment}
            onChange={(e) => setComment(e.target.value)}
          />
        )}

        {isPasswordRequired && (
          <TextField
            label="Password"
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
