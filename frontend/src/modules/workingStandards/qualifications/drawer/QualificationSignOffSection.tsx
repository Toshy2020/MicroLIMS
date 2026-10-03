import React from "react";
import {
  Card,
  CardContent,
  Typography,
  Box,
  Button,
  Stack
} from "@mui/material";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import HighlightOffIcon from "@mui/icons-material/HighlightOff";
import UndoIcon from "@mui/icons-material/Undo";
import RateReviewIcon from "@mui/icons-material/RateReview";
import { formatLabDateTime } from "../../../../utils/formatDate";
import type { WorkingStandardQualificationDto } from "../../types";
import type { SignOffActionType } from "../SignOffDialog";

interface QualificationSignOffSectionProps {
  qualification: WorkingStandardQualificationDto;
  canReview: boolean;
  canApprove: boolean;
  onInitiateSignOff: (actionType: SignOffActionType) => void;
}

export const QualificationSignOffSection: React.FC<QualificationSignOffSectionProps> = ({
  qualification,
  canReview,
  canApprove,
  onInitiateSignOff
}) => {
  const { status, preparedByUserName, preparedAt, reviewedByUserName, reviewedAt, approvedByUserName, approvedAt, rejectedByUserName, rejectedAt } = qualification;

  const showReviewActions = status === "Assayed" && canReview;
  const showApproveActions = status === "Reviewed" && canApprove;

  return (
    <Card variant="outlined" sx={{ borderRadius: 2 }}>
      <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.secondary" }}>
          WORKFLOW & SIGN-OFF
        </Typography>

        {/* Trail history */}
        <Stack spacing={1} sx={{ mb: 2 }}>
          {preparedByUserName && (
            <Box>
              <Typography variant="caption" color="text.secondary">Prepared by:</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {preparedByUserName} {preparedAt && `on ${formatLabDateTime(preparedAt)}`}
              </Typography>
            </Box>
          )}

          {reviewedByUserName && (
            <Box>
              <Typography variant="caption" color="text.secondary">Reviewed by:</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {reviewedByUserName} {reviewedAt && `on ${formatLabDateTime(reviewedAt)}`}
              </Typography>
            </Box>
          )}

          {approvedByUserName && (
            <Box>
              <Typography variant="caption" color="text.secondary">Approved by:</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600, color: "success.main" }}>
                {approvedByUserName} {approvedAt && `on ${formatLabDateTime(approvedAt)}`}
              </Typography>
            </Box>
          )}

          {rejectedByUserName && (
            <Box>
              <Typography variant="caption" color="text.secondary">Rejected by:</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600, color: "error.main" }}>
                {rejectedByUserName} {rejectedAt && `on ${formatLabDateTime(rejectedAt)}`}
              </Typography>
            </Box>
          )}
        </Stack>

        {/* Action Buttons */}
        {showReviewActions && (
          <Box sx={{ pt: 1.5, borderTop: 1, borderColor: "divider" }}>
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1, fontWeight: 600 }}>
              Review Actions (Samples.Review required)
            </Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
              <Button
                variant="contained"
                color="primary"
                size="small"
                startIcon={<RateReviewIcon fontSize="small" />}
                onClick={() => onInitiateSignOff("review")}
              >
                Review
              </Button>
              <Button
                variant="outlined"
                color="warning"
                size="small"
                startIcon={<UndoIcon fontSize="small" />}
                onClick={() => onInitiateSignOff("return")}
              >
                Return to Draft
              </Button>
              <Button
                variant="outlined"
                color="error"
                size="small"
                startIcon={<HighlightOffIcon fontSize="small" />}
                onClick={() => onInitiateSignOff("rejectReview")}
              >
                Reject
              </Button>
            </Box>
          </Box>
        )}

        {showApproveActions && (
          <Box sx={{ pt: 1.5, borderTop: 1, borderColor: "divider" }}>
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1, fontWeight: 600 }}>
              Approval Actions (Samples.Approve required)
            </Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
              <Button
                variant="contained"
                color="success"
                size="small"
                startIcon={<CheckCircleOutlinedIcon fontSize="small" />}
                onClick={() => onInitiateSignOff("approve")}
              >
                Approve
              </Button>
              <Button
                variant="outlined"
                color="error"
                size="small"
                startIcon={<HighlightOffIcon fontSize="small" />}
                onClick={() => onInitiateSignOff("rejectApproval")}
              >
                Reject
              </Button>
            </Box>
          </Box>
        )}
      </CardContent>
    </Card>
  );
};
