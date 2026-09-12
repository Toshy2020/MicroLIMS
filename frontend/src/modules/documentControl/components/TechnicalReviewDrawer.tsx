import { compactChipSx } from "../documentControlStyles";
import React, { useState } from "react";
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
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to add finding.");
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
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to submit response.");
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
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to verify finding.");
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
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to resolve finding.");
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
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to record review decision.");
    } finally {
      setSubmitting(false);
    }
  };

  const getStatusChip = (status: ReviewFindingStatus) => {
    switch (status) {
      case "Open":
        return <Chip label="Open" size="small" color="error" variant="outlined" />;
      case "AuthorResponded":
        return <Chip label="Author Responded" size="small" color="warning" />;
      case "ReviewerVerified":
        return <Chip label="Verified" size="small" color="info" />;
      case "Resolved":
        return <Chip label="Resolved" size="small" color="success" />;
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
              Technical Review
            </Typography>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>
              {reviewTask.companyDocumentCode} • Revision {reviewTask.revisionNumber}
            </Typography>
          </Box>
          <IconButton aria-label="Close technical review" onClick={onClose} size="small">
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
              Segregation of Duties Enforcement (DC-URS-078)
            </Typography>
            <Typography variant="caption">
              You are the designated author/submitter of this revision. Per cGMP regulations, you cannot perform the technical review or resolve review findings.
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
                Review Status:
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
                Assigned Reviewer:
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
                  Due Date:
                </Typography>
                <Typography variant="body2">
                  {new Date(reviewTask.dueDate).toLocaleDateString()}
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
                  Author Submission Notes:
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
                    ? "Inspect Changes: Side-by-Side Dual PDF (DC-URS-067)"
                    : "Inspect Proposed Revision PDF (DC-URS-067)"}
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
              Add Finding
            </Button>

            <Tooltip
              title={
                openMandatoryCount > 0
                  ? `Cannot complete review while ${openMandatoryCount} mandatory finding(s) remain open.`
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
                  Complete Review
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
              Return
            </Button>
          </Box>
        )}

        {/* Findings List */}
        <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
          Review Findings ({reviewTask.findings.length})
        </Typography>

        {reviewTask.findings.length === 0 ? (
          <Box sx={{ py: 4, textAlign: "center", color: "text.secondary" }}>
            <Typography variant="body2">No review findings recorded yet.</Typography>
          </Box>
        ) : (
          <Stack spacing={1.5} sx={{ flexGrow: 1, overflowY: "auto", pr: 0.5 }}>
            {reviewTask.findings.map((f) => (
              <Card key={f.id} variant="outlined" sx={{ p: 1.5 }}>
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    {getStatusChip(f.status)}
                    {f.isMandatory && (
                      <Chip label="Mandatory" size="small" color="error" sx={compactChipSx} />
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
                    {f.pageNumber ? `Page ${f.pageNumber} ` : ""}
                    {f.sectionNumber ? `• Section ${f.sectionNumber}` : ""}
                  </Typography>
                )}

                <Typography variant="body2" sx={{ mb: 1 }}>
                  {f.commentText}
                </Typography>

                {/* Author Response Block */}
                {f.authorResponse && (
                  <Box sx={{ bgcolor: "action.hover", p: 1, borderRadius: 1, mb: 1 }}>
                    <Typography variant="caption" sx={{ fontWeight: 600, color: "text.secondary" }}>
                      Author Response ({f.authorResponseUsername}):
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
                      Reviewer Verification:
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
                      Respond
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
                      Verify
                    </Button>
                  )}

                  {/* Resolved is reachable only from ReviewerVerified - the
                      author responds, the reviewer verifies, then the finding is
                      resolved (ML-DC-FRS-1B-001 §3.3:251). This used to offer
                      Resolve for any status except Resolved, so a finding could
                      be closed straight from Open without the author ever
                      answering it. The service now refuses that too. */}
                  {isReviewer && f.status === "ReviewerVerified" && isPendingOrInProgress && (
                    <Button
                      size="small"
                      variant="contained"
                      color="success"
                      startIcon={<CheckCircleOutlineIcon />}
                      onClick={() => handleResolve(f.id)}
                    >
                      Resolve
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
        <DialogTitle>Add Review Finding</DialogTitle>
        <DialogContent dividers>
          <Box sx={{ display: "flex", flexDirection: "column", gap: 2, mt: 1 }}>
            <Box sx={{ display: "flex", gap: 2 }}>
              <TextField
                label="Page #"
                type="number"
                size="small"
                value={pageNumber}
                onChange={(e) => setPageNumber(e.target.value === "" ? "" : Number(e.target.value))}
                sx={{ width: 100 }}
              />
              <TextField
                label="Section #"
                size="small"
                value={sectionNumber}
                onChange={(e) => setSectionNumber(e.target.value)}
                placeholder="e.g. 4.2"
                fullWidth
              />
            </Box>
            <TextField
              label="Comment / Finding Details"
              multiline
              rows={4}
              required
              value={commentText}
              onChange={(e) => setCommentText(e.target.value)}
              placeholder="Describe the required clarification or technical correction..."
              fullWidth
            />
            <FormControlLabel
              control={<Checkbox checked={isMandatory} onChange={(e) => setIsMandatory(e.target.checked)} />}
              label="Mandatory (blocks review completion until resolved)"
            />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAddFindingOpen(false)}>Cancel</Button>
          <Button onClick={handleAddFinding} variant="contained" disabled={!commentText.trim() || submitting}>
            Add Finding
          </Button>
        </DialogActions>
      </Dialog>

      {/* Author Respond Modal */}
      <Dialog open={respondOpen} onClose={() => setRespondOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Respond to Review Finding</DialogTitle>
        <DialogContent dividers>
          <TextField
            label="Author Response Notes"
            multiline
            rows={4}
            required
            value={authorResponse}
            onChange={(e) => setAuthorResponse(e.target.value)}
            placeholder="Explain the changes made or provide technical clarification..."
            fullWidth
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRespondOpen(false)}>Cancel</Button>
          <Button onClick={handleRespond} variant="contained" disabled={!authorResponse.trim() || submitting}>
            Submit Response
          </Button>
        </DialogActions>
      </Dialog>

      {/* Reviewer Verify Modal */}
      <Dialog open={verifyOpen} onClose={() => setVerifyOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Verify Finding Resolution</DialogTitle>
        <DialogContent dividers>
          <TextField
            label="Verification Notes"
            multiline
            rows={3}
            value={verificationNotes}
            onChange={(e) => setVerificationNotes(e.target.value)}
            placeholder="Confirm that the author's change adequately satisfies the requirement..."
            fullWidth
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setVerifyOpen(false)}>Cancel</Button>
          <Button onClick={handleVerify} variant="contained" color="info" disabled={submitting}>
            Verify
          </Button>
        </DialogActions>
      </Dialog>

      {/* Review Decision Modal */}
      <Dialog open={decisionOpen} onClose={() => setDecisionOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>
          {decisionType === "CompleteReview" ? "Complete Technical Review" : "Return for Correction"}
        </DialogTitle>
        <DialogContent dividers>
          <Alert
            severity={decisionType === "CompleteReview" ? "success" : "warning"}
            sx={{ mb: 2, fontSize: "0.85rem" }}
          >
            {decisionType === "CompleteReview"
              ? "Completing technical review will advance the document revision to 'AwaitingApproval'."
              : "Returning will revert the document revision status back to 'Draft' so the author can modify content."}
          </Alert>
          <TextField
            label="Summary Review Notes"
            multiline
            rows={3}
            value={reviewNotes}
            onChange={(e) => setReviewNotes(e.target.value)}
            placeholder="Optional summary remarks for audit record..."
            fullWidth
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDecisionOpen(false)}>Cancel</Button>
          <Button
            onClick={handleDecision}
            variant="contained"
            color={decisionType === "CompleteReview" ? "success" : "warning"}
            disabled={submitting}
          >
            {submitting ? <CircularProgress size={24} /> : "Confirm Decision"}
          </Button>
        </DialogActions>
      </Dialog>
    </Drawer>
  );
};
