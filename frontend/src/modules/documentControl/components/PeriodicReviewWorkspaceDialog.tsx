import { compactChipStrongSx } from "../documentControlStyles";
import { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  Chip,
  Paper,
  Tabs,
  Tab,
  TextField,
  MenuItem,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Alert,
  CircularProgress,
  IconButton,
  Tooltip,
  useTheme
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import AddCommentIcon from "@mui/icons-material/AddComment";
import DoneIcon from "@mui/icons-material/Done";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import AutoModeIcon from "@mui/icons-material/AutoMode";

import { ControlledPdfViewer } from "./ControlledPdfViewer";
import { periodicReviewService } from "../services/periodicReviewService";
import type {
  PeriodicReviewWorkspaceDto,
  PeriodicReviewOutcome
} from "../types/documentControlTypes";
import { useAuth } from "../../../contexts/AuthContext";
import { tableHeadSx } from "../../../theme";

interface PeriodicReviewWorkspaceDialogProps {
  open: boolean;
  taskId: number | null;
  onClose: () => void;
  onSuccess: () => void;
  onTriggerCreateRevision?: (masterId: number, originatingTaskId: number) => void;
}

export function PeriodicReviewWorkspaceDialog({
  open,
  taskId,
  onClose,
  onSuccess,
  onTriggerCreateRevision
}: PeriodicReviewWorkspaceDialogProps) {
  const { userId } = useAuth();
  const theme = useTheme();
  const [loading, setLoading] = useState(false);
  const [workspace, setWorkspace] = useState<PeriodicReviewWorkspaceDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [currentTab, setCurrentTab] = useState(0);

  // Finding form
  const [pageNumber, setPageNumber] = useState<string>("");
  const [sectionNumber, setSectionNumber] = useState<string>("");
  const [noteText, setNoteText] = useState<string>("");
  const [submittingFinding, setSubmittingFinding] = useState(false);

  // Decision form
  const [outcome, setOutcome] = useState<PeriodicReviewOutcome>("RemainsValid");
  const [reviewSummary, setReviewSummary] = useState<string>("");
  const [submittingDecision, setSubmittingDecision] = useState(false);

  // PDF dialog state
  const [pdfOpen, setPdfOpen] = useState(false);

  useEffect(() => {
    if (open && taskId) {
      loadWorkspace(taskId);
    } else {
      setWorkspace(null);
      setError(null);
      setCurrentTab(0);
      setPageNumber("");
      setSectionNumber("");
      setNoteText("");
      setOutcome("RemainsValid");
      setReviewSummary("");
    }
  }, [open, taskId]);

  const loadWorkspace = async (id: number) => {
    setLoading(true);
    setError(null);
    try {
      const data = await periodicReviewService.getWorkspace(id);
      setWorkspace(data);
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to load review workspace.");
    } finally {
      setLoading(false);
    }
  };

  const handleAddFinding = async () => {
    if (!taskId || !noteText.trim() || noteText.trim().length < 5) return;
    setSubmittingFinding(true);
    setError(null);
    try {
      await periodicReviewService.addFinding(taskId, {
        pageNumber: pageNumber ? parseInt(pageNumber, 10) : undefined,
        sectionNumber: sectionNumber.trim() || undefined,
        noteText: noteText.trim()
      });
      setPageNumber("");
      setSectionNumber("");
      setNoteText("");
      await loadWorkspace(taskId);
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to add review note.");
    } finally {
      setSubmittingFinding(false);
    }
  };

  const handleResolveFinding = async (findingId: number) => {
    if (!taskId) return;
    setError(null);
    try {
      await periodicReviewService.resolveFinding(taskId, findingId);
      await loadWorkspace(taskId);
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to resolve finding.");
    }
  };

  const handleCompleteReview = async () => {
    if (!taskId || !reviewSummary.trim() || reviewSummary.trim().length < 10) {
      setError("A review summary of at least 10 characters is mandatory.");
      return;
    }

    setSubmittingDecision(true);
    setError(null);
    try {
      const completed = await periodicReviewService.completeReview(taskId, {
        outcome,
        reviewSummary: reviewSummary.trim()
      });

      onSuccess();

      if (outcome === "RevisionRequired" && onTriggerCreateRevision && workspace) {
        onClose();
        onTriggerCreateRevision(workspace.master.id, completed.id);
      } else {
        onClose();
      }
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to complete periodic review.");
    } finally {
      setSubmittingDecision(false);
    }
  };

  if (!open) return null;

  const isCompleted = workspace?.task.status === "Completed" || workspace?.task.status === "Cancelled";
  const isAuthor = workspace?.revision.createdByUserId === userId;

  return (
    <>
      <Dialog open={open} onClose={onClose} maxWidth="lg" fullWidth>
        <DialogTitle sx={{ m: 0, p: 2, display: "flex", justifyContent: "space-between", alignItems: "center", bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50" }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
            <AutoModeIcon color="primary" />
            <Box>
              <Typography variant="h6" sx={{ fontWeight: 700, lineHeight: 1.2 }}>
                Periodic Review Workspace
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {workspace?.master.companyDocumentCode} — {workspace?.master.title} (Rev {workspace?.revision.revisionNumber})
              </Typography>
            </Box>
          </Box>
          <IconButton aria-label="Close periodic review workspace" onClick={onClose} size="small">
            <CloseIcon />
          </IconButton>
        </DialogTitle>

        <DialogContent dividers sx={{ p: 3 }}>
          {loading && (
            <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}>
              <CircularProgress />
            </Box>
          )}

          {error && (
            <Alert severity="error" sx={{ mb: 3 }} onClose={() => setError(null)}>
              {error}
            </Alert>
          )}

          {isAuthor && !isCompleted && (
            <Alert severity="warning" sx={{ mb: 2 }}>
              Segregation of Duties Enforcement: You are the author of Revision {workspace?.revision.revisionNumber}. You cannot record notes or complete this periodic review.
            </Alert>
          )}

          {!loading && workspace && (
            <Box sx={{ display: "flex", flexDirection: "column", gap: 2.5 }}>
              {/* Header Metadata Ribbon */}
              <Paper variant="outlined" sx={{ p: 2, bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50" }}>
                <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)", md: "repeat(6, 1fr)" }, gap: 1.5 }}>
                  <Box>
                    <Typography variant="caption" color="text.secondary" display="block">MicroLIMS ID</Typography>
                    <Typography variant="body2" sx={{ fontWeight: 700 }}>{workspace.master.microLimsDocumentId}</Typography>
                  </Box>
                  <Box>
                    <Typography variant="caption" color="text.secondary" display="block">Effective Revision</Typography>
                    <Typography variant="body2" sx={{ fontWeight: 700 }}>Rev {workspace.revision.revisionNumber}</Typography>
                  </Box>
                  <Box>
                    <Typography variant="caption" color="text.secondary" display="block">Review Due Date</Typography>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                      <Typography variant="body2" sx={{ fontWeight: 700, color: workspace.task.isOverdue ? "error.main" : "text.primary" }}>
                        {new Date(workspace.task.scheduledDueDate).toLocaleDateString()}
                      </Typography>
                      {workspace.task.isOverdue && <Chip label="OVERDUE" color="error" size="small" sx={compactChipStrongSx} />}
                    </Box>
                  </Box>
                  <Box>
                    <Typography variant="caption" color="text.secondary" display="block">Review Cycle</Typography>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>{workspace.task.reviewCycleMonths} Months</Typography>
                  </Box>
                  <Box>
                    <Typography variant="caption" color="text.secondary" display="block">Assigned Reviewer</Typography>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>{workspace.task.assignedReviewerFullName || "Unassigned"}</Typography>
                  </Box>
                  <Box>
                    <Typography variant="caption" color="text.secondary" display="block">Status</Typography>
                    <Chip
                      label={workspace.task.status}
                      color={workspace.task.status === "Completed" ? "success" : workspace.task.status === "InProgress" ? "info" : "default"}
                      size="small"
                      sx={{ fontWeight: 700 }}
                    />
                  </Box>
                </Box>
              </Paper>

              {/* Controlled PDF Inspection Bar */}
              <Paper variant="outlined" sx={{ p: 2, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
                  <PictureAsPdfIcon color="error" sx={{ fontSize: 36 }} />
                  <Box>
                    <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                      Controlled Effective PDF
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {workspace.controlledPdf ? `${workspace.controlledPdf.fileName} (SHA-256 Verified)` : "No active controlled PDF attached"}
                    </Typography>
                  </Box>
                </Box>
                {workspace.controlledPdf && (
                  <Button
                    variant="contained"
                    size="small"
                    startIcon={<PictureAsPdfIcon />}
                    onClick={() => setPdfOpen(true)}
                  >
                    Inspect Controlled PDF
                  </Button>
                )}
              </Paper>

              {/* Tabs for Findings vs Review History */}
              <Tabs value={currentTab} onChange={(_, val) => setCurrentTab(val)}>
                <Tab label={`Review Notes & Findings (${workspace.findings.length})`} />
                <Tab label={`Independent Review History (${workspace.historicalReviews.length})`} />
                {!isCompleted && !isAuthor && <Tab label="Outcome & Decision" />}
              </Tabs>

              {/* Tab 0: Notes & Findings */}
              {currentTab === 0 && (
                <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
                  {!isCompleted && !isAuthor && (
                    <Paper variant="outlined" sx={{ p: 2 }}>
                      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
                        Record Page / Section Specific Review Finding
                      </Typography>
                      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "120px 140px 1fr auto" }, gap: 1.5, alignItems: "flex-start" }}>
                        <TextField
                          size="small"
                          label="Page #"
                          type="number"
                          value={pageNumber}
                          onChange={(e) => setPageNumber(e.target.value)}
                        />
                        <TextField
                          size="small"
                          label="Section #"
                          value={sectionNumber}
                          onChange={(e) => setSectionNumber(e.target.value)}
                        />
                        <TextField
                          size="small"
                          label="Review Note / Observation *"
                          value={noteText}
                          onChange={(e) => setNoteText(e.target.value)}
                          placeholder="Document observations, required updates, or verified points..."
                        />
                        <Button
                          variant="outlined"
                          startIcon={<AddCommentIcon />}
                          disabled={submittingFinding || !noteText.trim() || noteText.trim().length < 5}
                          onClick={handleAddFinding}
                          sx={{ height: 40 }}
                        >
                          Add Note
                        </Button>
                      </Box>
                    </Paper>
                  )}

                  <TableContainer component={Paper} variant="outlined">
                    <Table size="small">
                      <TableHead sx={tableHeadSx(theme)}>
                        <TableRow>
                          <TableCell sx={{ fontWeight: 700, width: 90 }}>Page #</TableCell>
                          <TableCell sx={{ fontWeight: 700, width: 110 }}>Section</TableCell>
                          <TableCell sx={{ fontWeight: 700 }}>Note / Observation</TableCell>
                          <TableCell sx={{ fontWeight: 700, width: 140 }}>Recorded By</TableCell>
                          <TableCell sx={{ fontWeight: 700, width: 110 }}>Status</TableCell>
                          {!isCompleted && !isAuthor && <TableCell sx={{ fontWeight: 700, width: 90 }} align="right">Action</TableCell>}
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {workspace.findings.map((f) => (
                          <TableRow key={f.id} hover>
                            <TableCell>{f.pageNumber ?? "—"}</TableCell>
                            <TableCell>{f.sectionNumber || "—"}</TableCell>
                            <TableCell>{f.noteText}</TableCell>
                            <TableCell>
                              <Typography variant="caption" display="block">{f.createdByFullName}</Typography>
                              <Typography variant="caption" color="text.secondary">{new Date(f.createdAt).toLocaleDateString()}</Typography>
                            </TableCell>
                            <TableCell>
                              <Chip
                                label={f.status}
                                size="small"
                                color={f.status === "Resolved" ? "success" : "default"}
                              />
                            </TableCell>
                            {!isCompleted && !isAuthor && (
                              <TableCell align="right">
                                {f.status === "Open" && (
                                  <Tooltip title="Mark Resolved">
                                    <IconButton aria-label="Mark finding resolved" size="small" color="success" onClick={() => handleResolveFinding(f.id)}>
                                      <DoneIcon fontSize="small" />
                                    </IconButton>
                                  </Tooltip>
                                )}
                              </TableCell>
                            )}
                          </TableRow>
                        ))}
                        {workspace.findings.length === 0 && (
                          <TableRow>
                            <TableCell colSpan={6} align="center" sx={{ py: 3, color: "text.secondary" }}>
                              No review notes or findings recorded yet.
                            </TableCell>
                          </TableRow>
                        )}
                      </TableBody>
                    </Table>
                  </TableContainer>
                </Box>
              )}

              {/* Tab 1: Historical Reviews */}
              {currentTab === 1 && (
                <TableContainer component={Paper} variant="outlined">
                  <Table size="small">
                    <TableHead sx={tableHeadSx(theme)}>
                      <TableRow>
                        <TableCell sx={{ fontWeight: 700 }}>Review Date</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>Revision</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>Outcome</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>Reviewer</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>Summary & Notes</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>Findings</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {workspace.historicalReviews.map((h) => (
                        <TableRow key={h.id} hover>
                          <TableCell>{h.completedAt ? new Date(h.completedAt).toLocaleDateString() : new Date(h.createdAt).toLocaleDateString()}</TableCell>
                          <TableCell sx={{ fontWeight: 700 }}>Rev {h.revisionNumber}</TableCell>
                          <TableCell>
                            <Chip
                              label={h.outcome || h.status}
                              size="small"
                              color={h.outcome === "RemainsValid" ? "success" : h.outcome === "RevisionRequired" ? "warning" : "default"}
                            />
                          </TableCell>
                          <TableCell>{h.completedByFullName || h.assignedReviewerFullName || "—"}</TableCell>
                          <TableCell sx={{ maxWidth: 300 }}>{h.reviewSummary || "—"}</TableCell>
                          <TableCell>{h.totalFindingsCount} notes</TableCell>
                        </TableRow>
                      ))}
                      {workspace.historicalReviews.length === 0 && (
                        <TableRow>
                          <TableCell colSpan={6} align="center" sx={{ py: 3, color: "text.secondary" }}>
                            No prior periodic reviews recorded for this document master.
                          </TableCell>
                        </TableRow>
                      )}
                    </TableBody>
                  </Table>
                </TableContainer>
              )}

              {/* Tab 2: Outcome & Decision */}
              {currentTab === 2 && !isCompleted && !isAuthor && (
                <Paper variant="outlined" sx={{ p: 3, display: "flex", flexDirection: "column", gap: 2.5 }}>
                  <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                    Select Periodic Review Outcome
                  </Typography>

                  <TextField
                    select
                    label="Decision Outcome *"
                    value={outcome}
                    onChange={(e) => setOutcome(e.target.value as PeriodicReviewOutcome)}
                    sx={{ maxWidth: 350 }}
                  >
                    <MenuItem value="RemainsValid">
                      <strong>Remains Valid</strong> — No changes required; advances next review date
                    </MenuItem>
                    <MenuItem value="RevisionRequired">
                      <strong>Revision Required</strong> — Findings transferred into new revision
                    </MenuItem>
                    <MenuItem value="ObsolescenceRecommended">
                      <strong>Obsolescence Recommended</strong> — Route to quality approval workflow
                    </MenuItem>
                  </TextField>

                  {outcome === "RemainsValid" && (
                    <Alert severity="info">
                      <strong>Remains Valid Impact:</strong> Current effective revision will be preserved. No new revision will be created. The Next Review Date will be advanced by <strong>{workspace.task.reviewCycleMonths} months</strong>.
                    </Alert>
                  )}

                  {outcome === "RevisionRequired" && (
                    <Alert severity="warning">
                      <strong>Revision Required Impact:</strong> Closes this review task and enables creating a new draft revision linked directly to this review. All {workspace.findings.length} findings will be preserved for transfer into change items.
                    </Alert>
                  )}

                  {outcome === "ObsolescenceRecommended" && (
                    <Alert severity="error">
                      <strong>Obsolescence Recommended Impact:</strong> Does NOT directly obsolete the document. Creates an Obsolescence Approval task routed to Quality Assurance and Document Controller for formal sign-off.
                    </Alert>
                  )}

                  <TextField
                    multiline
                    rows={4}
                    label="Review Summary & Conclusion *"
                    value={reviewSummary}
                    onChange={(e) => setReviewSummary(e.target.value)}
                    placeholder="Provide a comprehensive summary of the review rationale, regulatory compliance verification, and outcome justification (minimum 10 characters)..."
                  />

                  <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
                    <Button
                      variant="contained"
                      color="primary"
                      disabled={submittingDecision || !reviewSummary.trim() || reviewSummary.trim().length < 10}
                      onClick={handleCompleteReview}
                    >
                      {submittingDecision ? <CircularProgress size={24} /> : "Submit & Complete Review"}
                    </Button>
                  </Box>
                </Paper>
              )}
            </Box>
          )}
        </DialogContent>

        <DialogActions sx={{ p: 2 }}>
          <Button onClick={onClose} color="inherit">
            Close Workspace
          </Button>
        </DialogActions>
      </Dialog>

      {/* Controlled PDF Viewer Modal */}
      {workspace?.controlledPdf && (
        <ControlledPdfViewer
          open={pdfOpen}
          onClose={() => setPdfOpen(false)}
          fileId={workspace.controlledPdf.id}
          fileName={workspace.controlledPdf.fileName}
          companyDocumentCode={workspace.master.companyDocumentCode}
          microLimsDocumentId={workspace.master.microLimsDocumentId}
          revisionNumber={workspace.revision.revisionNumber}
          title={workspace.master.title}
          revisionStatus={workspace.revision.revisionStatus}
        />
      )}
    </>
  );
}
