import { compactChipSx } from "../documentControlStyles";
import React, { useState } from "react";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";
import {
  Drawer,
  Box,
  Typography,
  IconButton,
  Button,
  Chip,
  Divider,
  Card,
  CardContent,
  TextField,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  FormControlLabel,
  Checkbox,
  Alert,
  CircularProgress,
  Stack,
  Tooltip
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import AddCommentIcon from "@mui/icons-material/AddComment";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import AssignmentReturnIcon from "@mui/icons-material/AssignmentReturn";
import DoneAllIcon from "@mui/icons-material/DoneAll";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";
import CompareArrowsIcon from "@mui/icons-material/CompareArrows";
import SecurityIcon from "@mui/icons-material/Security";
import type {
  DocumentReviewTaskDto,
  DocumentReviewFindingDto,
  ReviewFindingStatus
} from "../types/documentControlTypes";
import { documentReviewService } from "../services/documentReviewService";
import { formatLabCalendarDate } from "../../../utils/formatDate";

interface TechnicalReviewDrawerProps {
  open: boolean;
  onClose: () => void;
  reviewTask: DocumentReviewTaskDto | null;
  onTaskUpdated: () => void;
  currentUserId: number;
  onOpenComparison?: () => void;
  hasEffectiveRevision?: boolean;
}

export const TechnicalReviewDrawer: React.FC<TechnicalReviewDrawerProps> = ({
  open,
  onClose,
  reviewTask,
  onTaskUpdated,
  currentUserId,
  onOpenComparison,
  hasEffectiveRevision = false
}) => {
  const { t } = useTranslation("documentControl");
  const [addFindingOpen, setAddFindingOpen] = useState(false);
  const [pageNumber, setPageNumber] = useState<number | "">("");
  const [sectionNumber, setSectionNumber] = useState("");
  const [commentText, setCommentText] = useState("");
  const [isMandatory, setIsMandatory] = useState(true);

  const [respondOpen, setRespondOpen] = useState(false);
  const [activeFinding, setActiveFinding] = useState<DocumentReviewFindingDto | null>(null);
  const [authorResponse, setAuthorResponse] = useState("");

  const [verifyOpen, setVerifyOpen] = useState(false);
  const [verificationNotes, setVerificationNotes] = useState("");

  const [decisionOpen, setDecisionOpen] = useState(false);
  const [decisionType, setDecisionType] = useState<"CompleteReview" | "ReturnForCorrection">("CompleteReview");
  const [reviewNotes, setReviewNotes] = useState("");

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!reviewTask) return null;

  const isReviewer = currentUserId === reviewTask.assignedReviewerUserId;
  const isAuthor = currentUserId === reviewTask.assignedByUserId;
  const isPendingOrInProgress =
    reviewTask.status === "Pending" || reviewTask.status === "InProgress";

  const openMandatoryCount = reviewTask.openMandatoryFindingsCount;

  const handleAddFinding = async () => {
    if (!commentText.trim()) return;
    setSubmitting(true);
    setError(null);
    try {
      await documentReviewService.addReviewFinding(reviewTask.id, {
        pageNumber: pageNumber ? Number(pageNumber) : null,
        sectionNumber: sectionNumber.trim() || null,
        commentText: commentText.trim(),
        isMandatory
      });
      setAddFindingOpen(false);
      setCommentText("");
      setPageNumber("");
      setSectionNumber("");
      setIsMandatory(true);
      onTaskUpdated();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("technicalReview.addFindingError")));
    } finally {
      setSubmitting(false);
    }
  };

  const handleRespond = async () => {
    if (!activeFinding || !authorResponse.trim()) return;
    setSubmitting(true);
    setError(null);
    try {
      await documentReviewService.respondToFinding(activeFinding.id, {
        response: authorResponse.trim()
      });
      setRespondOpen(false);
      setAuthorResponse("");
      setActiveFinding(null);
      onTaskUpdated();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("technicalReview.respondError")));
    } finally {
      setSubmitting(false);
    }
  };

  const handleVerify = async () => {
    if (!activeFinding) return;
    setSubmitting(true);
    setError(null);
    try {
      await documentReviewService.verifyFinding(activeFinding.id, {
        verificationNotes: verificationNotes.trim() || undefined
      });
      setVerifyOpen(false);
      setVerificationNotes("");
      setActiveFinding(null);
      onTaskUpdated();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("technicalReview.verifyError")));
    } finally {
      setSubmitting(false);
    }
  };

  const handleResolve = async (findingId: number) => {
    setSubmitting(true);
    setError(null);
    try {
      await documentReviewService.resolveFinding(findingId);
      onTaskUpdated();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("technicalReview.resolveError")));
    } finally {
      setSubmitting(false);
    }
  };

  const handleDecision = async () => {
    setSubmitting(true);
    setError(null);
    try {
      await documentReviewService.decideReview(reviewTask.id, {
        decision: decisionType,
        reviewNotes: reviewNotes.trim() || undefined
      });
      setDecisionOpen(false);
      setReviewNotes("");
      onTaskUpdated();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("technicalReview.decideError")));
    } finally {
      setSubmitting(false);
    }
  };

  const getStatusChip = (status: ReviewFindingStatus) => {
    switch (status) {
      case "Open":
        return <Chip label={t("technicalReview.findingStatuses.Open")} size="small" color="error" variant="outlined" />;
      case "AuthorResponded":
        return <Chip label={t("technicalReview.findingStatuses.AuthorResponded")} size="small" color="warning" />;
      case "ReviewerVerified":
        return <Chip label={t("technicalReview.findingStatuses.ReviewerVerified")} size="small" color="info" />;
      case "Resolved":
        return <Chip label={t("technicalReview.findingStatuses.Resolved")} size="small" color="success" />;
      default:
        return <Chip label={status} size="small" />;
    }
  };

  return (
    <Drawer anchor="right" open={open} onClose={onClose} slotProps={{
      paper: { sx: { width: { xs: "100%", sm: 540 } } }
    }}>
      <Box sx={{ p: 3, display: "flex", flexDirection: "column", height: "100%" }}>
        {/* Header */}
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
          <Box>
            <Typography variant="h6" sx={{ fontWeight: 600 }}>
              {t("technicalReview.title")}
            </Typography>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>
              {t("technicalReview.subtitle", { code: reviewTask.companyDocumentCode, rev: reviewTask.revisionNumber })}
            </Typography>
          </Box>
          <IconButton aria-label={t("technicalReview.closeAria")} onClick={onClose} size="small">
            <CloseIcon />
          </IconButton>
        </Box>

        <Divider sx={{ mb: 2 }} />

        {error && (
          <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        {/* Segregation of Duties Banner (DC-URS-078) */}
        {isAuthor && (
          <Alert severity="warning" icon={<SecurityIcon />} sx={{ mb: 2 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              {t("technicalReview.sodTitle")}
            </Typography>
            <Typography variant="caption">
              {t("technicalReview.sodBody")}
            </Typography>
          </Alert>
        )}

        {/* Task Overview */}
        <Card variant="outlined" sx={{ mb: 2.5, bgcolor: "background.default" }}>
          <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", mb: 1 }}>
              <Typography variant="body2" sx={{
                color: "text.secondary"
              }}>
                {t("technicalReview.overview.reviewStatus")}
              </Typography>
              <Chip
                label={reviewTask.status}
                size="small"
                color={
                  reviewTask.status === "Completed"
                    ? "success"
                    : reviewTask.status === "ReturnedForCorrection"
                    ? "warning"
                    : "primary"
                }
              />
            </Box>
            <Box sx={{ display: "flex", justifyContent: "space-between", mb: 1 }}>
              <Typography variant="body2" sx={{
                color: "text.secondary"
              }}>
                {t("technicalReview.overview.assignedReviewer")}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 500 }}>
                {reviewTask.assignedReviewerFullName}
              </Typography>
            </Box>
            {reviewTask.dueDate && (
              <Box sx={{ display: "flex", justifyContent: "space-between", mb: 1 }}>
                <Typography variant="body2" sx={{
                  color: "text.secondary"
                }}>
                  {t("technicalReview.overview.dueDate")}
                </Typography>
                <Typography variant="body2">
                  {formatLabCalendarDate(reviewTask.dueDate)}
                </Typography>
              </Box>
            )}
            {reviewTask.submissionNotes && (
              <Box sx={{ mt: 1, pt: 1, borderTop: "1px dashed", borderColor: "divider" }}>
                <Typography
                  variant="caption"
                  sx={{
                    color: "text.secondary",
                    display: "block"
                  }}>
                  {t("technicalReview.overview.submissionNotes")}
                </Typography>
                <Typography variant="body2" sx={{ fontStyle: "italic", mt: 0.5 }}>
                  "{reviewTask.submissionNotes}"
                </Typography>
              </Box>
            )}

            {/* Side-by-Side Dual PDF Comparison Link (DC-URS-067) */}
            {onOpenComparison && (
              <Box sx={{ mt: 1.5, pt: 1.5, borderTop: "1px solid", borderColor: "divider" }}>
                <Button
                  fullWidth
                  variant="outlined"
                  color="primary"
                  size="small"
                  startIcon={<CompareArrowsIcon />}
                  onClick={onOpenComparison}
                >
                  {hasEffectiveRevision
                    ? t("technicalReview.overview.inspectDualPdf")
                    : t("technicalReview.overview.inspectProposedPdf")}
                </Button>
              </Box>
            )}
          </CardContent>
        </Card>

        {/* Action Toolbar for Reviewer */}
        {isReviewer && isPendingOrInProgress && (
          <Box sx={{ mb: 2.5, display: "flex", gap: 1 }}>
            <Button
              variant="outlined"
              size="small"
              startIcon={<AddCommentIcon />}
              onClick={() => setAddFindingOpen(true)}
            >
              {t("technicalReview.toolbar.addFinding")}
            </Button>

            <Tooltip
              title={
                openMandatoryCount > 0
                  ? t("technicalReview.toolbar.blockedTooltip", { count: openMandatoryCount })
                  : ""
              }
            >
              <span>
                <Button
                  variant="contained"
                  size="small"
                  color="success"
                  disabled={openMandatoryCount > 0 || submitting}
                  startIcon={<DoneAllIcon />}
                  onClick={() => {
                    setDecisionType("CompleteReview");
                    setDecisionOpen(true);
                  }}
                >
                  {t("technicalReview.toolbar.completeReview")}
                </Button>
              </span>
            </Tooltip>

            <Button
              variant="outlined"
              size="small"
              color="warning"
              startIcon={<AssignmentReturnIcon />}
              onClick={() => {
                setDecisionType("ReturnForCorrection");
                setDecisionOpen(true);
              }}
            >
              {t("technicalReview.toolbar.return")}
            </Button>
          </Box>
        )}

        {/* Findings List */}
        <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
          {t("technicalReview.findings.title", { count: reviewTask.findings.length })}
        </Typography>

        {reviewTask.findings.length === 0 ? (
          <Box sx={{ py: 4, textAlign: "center", color: "text.secondary" }}>
            <Typography variant="body2">{t("technicalReview.findings.empty")}</Typography>
          </Box>
        ) : (
          <Stack spacing={1.5} sx={{ flexGrow: 1, overflowY: "auto", pr: 0.5 }}>
            {reviewTask.findings.map((f) => (
              <Card key={f.id} variant="outlined" sx={{ p: 1.5 }}>
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    {getStatusChip(f.status)}
                    {f.isMandatory && (
                      <Chip label={t("technicalReview.findings.mandatory")} size="small" color="error" sx={compactChipSx} />
                    )}
                  </Box>
                  <Typography variant="caption" sx={{
                    color: "text.secondary"
                  }}>
                    {new Date(f.createdAt).toLocaleDateString()}
                  </Typography>
                </Box>

                {(f.pageNumber || f.sectionNumber) && (
                  <Typography variant="caption" sx={{ fontWeight: 600, color: "primary.main", display: "block", mb: 0.5 }}>
                    {f.pageNumber ? t("technicalReview.findings.pageLabel", { page: f.pageNumber }) : ""}
                    {f.sectionNumber ? t("technicalReview.findings.sectionLabel", { section: f.sectionNumber }) : ""}
                  </Typography>
                )}

                <Typography variant="body2" sx={{ mb: 1 }}>
                  {f.commentText}
                </Typography>

                {/* Author Response Block */}
                {f.authorResponse && (
                  <Box sx={{ bgcolor: "action.hover", p: 1, borderRadius: 1, mb: 1 }}>
                    <Typography variant="caption" sx={{ fontWeight: 600, color: "text.secondary" }}>
                      {t("technicalReview.findings.authorResponse", { username: f.authorResponseUsername })}
                    </Typography>
                    <Typography variant="body2" sx={{ mt: 0.5 }}>
                      {f.authorResponse}
                    </Typography>
                  </Box>
                )}

                {/* Reviewer Verification Block */}
                {f.reviewerVerificationNotes && (
                  <Box sx={{ bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "rgba(2, 136, 209, 0.08)", p: 1, borderRadius: 1, mb: 1, borderLeft: "3px solid", borderColor: "info.main" }}>
                    <Typography variant="caption" sx={{ fontWeight: 600, color: "info.main" }}>
                      {t("technicalReview.findings.reviewerVerification")}
                    </Typography>
                    <Typography variant="body2" sx={{ mt: 0.5 }}>
                      {f.reviewerVerificationNotes}
                    </Typography>
                  </Box>
                )}

                {/* Finding Action Buttons */}
                <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 1, mt: 1 }}>
                  {isAuthor && f.status === "Open" && isPendingOrInProgress && (
                    <Button
                      size="small"
                      variant="outlined"
                      onClick={() => {
                        setActiveFinding(f);
                        setAuthorResponse("");
                        setRespondOpen(true);
                      }}
                    >
                      {t("technicalReview.findings.respond")}
                    </Button>
                  )}

                  {isReviewer && f.status === "AuthorResponded" && isPendingOrInProgress && (
                    <Button
                      size="small"
                      variant="outlined"
                      color="info"
                      startIcon={<VerifiedUserIcon />}
                      onClick={() => {
                        setActiveFinding(f);
                        setVerificationNotes("");
                        setVerifyOpen(true);
                      }}
                    >
                      {t("technicalReview.findings.verify")}
                    </Button>
                  )}

                  {isReviewer && f.status === "ReviewerVerified" && isPendingOrInProgress && (
                    <Button
                      size="small"
                      variant="contained"
                      color="success"
                      startIcon={<CheckCircleOutlineIcon />}
                      onClick={() => handleResolve(f.id)}
                    >
                      {t("technicalReview.findings.resolve")}
                    </Button>
                  )}
                </Box>
              </Card>
            ))}
          </Stack>
        )}
      </Box>

      {/* Add Finding Modal */}
      <Dialog open={addFindingOpen} onClose={() => setAddFindingOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{t("technicalReview.addModal.title")}</DialogTitle>
        <DialogContent dividers>
          <Box sx={{ display: "flex", flexDirection: "column", gap: 2, mt: 1 }}>
            <Box sx={{ display: "flex", gap: 2 }}>
              <TextField
                label={t("technicalReview.addModal.pageLabel")}
                type="number"
                size="small"
                value={pageNumber}
                onChange={(e) => setPageNumber(e.target.value === "" ? "" : Number(e.target.value))}
                sx={{ width: 100 }}
              />
              <TextField
                label={t("technicalReview.addModal.sectionLabel")}
                size="small"
                value={sectionNumber}
                onChange={(e) => setSectionNumber(e.target.value)}
                placeholder={t("technicalReview.addModal.sectionPlaceholder")}
                fullWidth
              />
            </Box>
            <TextField
              label={t("technicalReview.addModal.commentLabel")}
              multiline
              rows={4}
              required
              value={commentText}
              onChange={(e) => setCommentText(e.target.value)}
              placeholder={t("technicalReview.addModal.commentPlaceholder")}
              fullWidth
            />
            <FormControlLabel
              control={<Checkbox checked={isMandatory} onChange={(e) => setIsMandatory(e.target.checked)} />}
              label={t("technicalReview.addModal.mandatoryLabel")}
            />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAddFindingOpen(false)}>{t("common.cancel")}</Button>
          <Button onClick={handleAddFinding} variant="contained" disabled={!commentText.trim() || submitting}>
            {t("technicalReview.addModal.submit")}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Author Respond Modal */}
      <Dialog open={respondOpen} onClose={() => setRespondOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{t("technicalReview.respondModal.title")}</DialogTitle>
        <DialogContent dividers>
          <TextField
            label={t("technicalReview.respondModal.label")}
            multiline
            rows={4}
            required
            value={authorResponse}
            onChange={(e) => setAuthorResponse(e.target.value)}
            placeholder={t("technicalReview.respondModal.placeholder")}
            fullWidth
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRespondOpen(false)}>{t("common.cancel")}</Button>
          <Button onClick={handleRespond} variant="contained" disabled={!authorResponse.trim() || submitting}>
            {t("technicalReview.respondModal.submit")}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Reviewer Verify Modal */}
      <Dialog open={verifyOpen} onClose={() => setVerifyOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{t("technicalReview.verifyModal.title")}</DialogTitle>
        <DialogContent dividers>
          <TextField
            label={t("technicalReview.verifyModal.label")}
            multiline
            rows={3}
            value={verificationNotes}
            onChange={(e) => setVerificationNotes(e.target.value)}
            placeholder={t("technicalReview.verifyModal.placeholder")}
            fullWidth
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setVerifyOpen(false)}>{t("common.cancel")}</Button>
          <Button onClick={handleVerify} variant="contained" color="info" disabled={submitting}>
            {t("technicalReview.verifyModal.submit")}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Review Decision Modal */}
      <Dialog open={decisionOpen} onClose={() => setDecisionOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>
          {decisionType === "CompleteReview" ? t("technicalReview.decisionModal.completeTitle") : t("technicalReview.decisionModal.returnTitle")}
        </DialogTitle>
        <DialogContent dividers>
          <Alert
            severity={decisionType === "CompleteReview" ? "success" : "warning"}
            sx={{ mb: 2, fontSize: "0.85rem" }}
          >
            {decisionType === "CompleteReview"
              ? t("technicalReview.decisionModal.completeInfo")
              : t("technicalReview.decisionModal.returnInfo")}
          </Alert>
          <TextField
            label={t("technicalReview.decisionModal.notesLabel")}
            multiline
            rows={3}
            value={reviewNotes}
            onChange={(e) => setReviewNotes(e.target.value)}
            placeholder={t("technicalReview.decisionModal.notesPlaceholder")}
            fullWidth
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDecisionOpen(false)}>{t("common.cancel")}</Button>
          <Button
            onClick={handleDecision}
            variant="contained"
            color={decisionType === "CompleteReview" ? "success" : "warning"}
            disabled={submitting}
          >
            {submitting ? <CircularProgress size={24} /> : t("technicalReview.decisionModal.confirm")}
          </Button>
        </DialogActions>
      </Dialog>
    </Drawer>
  );
};
