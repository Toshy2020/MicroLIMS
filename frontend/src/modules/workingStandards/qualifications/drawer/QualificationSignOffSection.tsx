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
import { Trans } from "react-i18next";
import { Ltr } from "../../../../components/Ltr";
import { useTranslation } from "../../../../i18n";
import { formatDateTimeWithTimeZone } from "../../../../utils/formatDate";
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
  const { t } = useTranslation("workingStandards");
  const { status, preparedByUserName, preparedAt, reviewedByUserName, reviewedAt, approvedByUserName, approvedAt, rejectedByUserName, rejectedAt } = qualification;

  const showReviewActions = status === "Assayed" && canReview;
  const showApproveActions = status === "Reviewed" && canApprove;

  const renderUserDate = (user: string, date?: string | null) =>
    date ? (
      <Trans
        i18nKey="signOffSection.userAtDate"
        ns="workingStandards"
        values={{ user, date: formatDateTimeWithTimeZone(date) }}
        components={{ ltr: <Ltr>{null}</Ltr> }}
      />
    ) : (
      user
    );

  return (
    <Card variant="outlined" sx={{ borderRadius: 2 }}>
      <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.secondary" }}>
          {t("signOffSection.heading")}
        </Typography>

        {/* Trail history */}
        <Stack spacing={1} sx={{ mb: 2 }}>
          {preparedByUserName && (
            <Box>
              <Typography variant="caption" color="text.secondary">{t("signOffSection.preparedBy")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {renderUserDate(preparedByUserName, preparedAt)}
              </Typography>
            </Box>
          )}

          {reviewedByUserName && (
            <Box>
              <Typography variant="caption" color="text.secondary">{t("signOffSection.reviewedBy")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {renderUserDate(reviewedByUserName, reviewedAt)}
              </Typography>
            </Box>
          )}

          {approvedByUserName && (
            <Box>
              <Typography variant="caption" color="text.secondary">{t("signOffSection.approvedBy")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600, color: "success.main" }}>
                {renderUserDate(approvedByUserName, approvedAt)}
              </Typography>
            </Box>
          )}

          {rejectedByUserName && (
            <Box>
              <Typography variant="caption" color="text.secondary">{t("signOffSection.rejectedBy")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600, color: "error.main" }}>
                {renderUserDate(rejectedByUserName, rejectedAt)}
              </Typography>
            </Box>
          )}
        </Stack>

        {/* Action Buttons */}
        {showReviewActions && (
          <Box sx={{ pt: 1.5, borderTop: 1, borderColor: "divider" }}>
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1, fontWeight: 600 }}>
              {t("signOffSection.reviewActionsTitle", { permission: "Samples.Review" })}
            </Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
              <Button
                variant="contained"
                color="primary"
                size="small"
                startIcon={<RateReviewIcon fontSize="small" />}
                onClick={() => onInitiateSignOff("review")}
              >
                {t("signOffSection.review")}
              </Button>
              <Button
                variant="outlined"
                color="warning"
                size="small"
                startIcon={<UndoIcon fontSize="small" />}
                onClick={() => onInitiateSignOff("return")}
              >
                {t("signOffSection.returnToDraft")}
              </Button>
              <Button
                variant="outlined"
                color="error"
                size="small"
                startIcon={<HighlightOffIcon fontSize="small" />}
                onClick={() => onInitiateSignOff("rejectReview")}
              >
                {t("signOffSection.reject")}
              </Button>
            </Box>
          </Box>
        )}

        {showApproveActions && (
          <Box sx={{ pt: 1.5, borderTop: 1, borderColor: "divider" }}>
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1, fontWeight: 600 }}>
              {t("signOffSection.approvalActionsTitle", { permission: "Samples.Approve" })}
            </Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
              <Button
                variant="contained"
                color="success"
                size="small"
                startIcon={<CheckCircleOutlinedIcon fontSize="small" />}
                onClick={() => onInitiateSignOff("approve")}
              >
                {t("signOffSection.approve")}
              </Button>
              <Button
                variant="outlined"
                color="error"
                size="small"
                startIcon={<HighlightOffIcon fontSize="small" />}
                onClick={() => onInitiateSignOff("rejectApproval")}
              >
                {t("signOffSection.reject")}
              </Button>
            </Box>
          </Box>
        )}
      </CardContent>
    </Card>
  );
};
