import { compactChipStrongSx } from "../documentControlStyles";
import { useState, useEffect } from "react";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";
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
import { formatDateTimeWithTimeZone, formatLabCalendarDate } from "../../../utils/formatDate";

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
  const { t } = useTranslation("documentControl");
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
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("periodicReview.loadError")));
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
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("periodicReview.addNoteError")));
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
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("periodicReview.resolveError")));
    }
  };

  const handleCompleteReview = async () => {
    if (!taskId || !reviewSummary.trim() || reviewSummary.trim().length < 10) {
      setError(t("periodicReview.summaryMandatoryError"));
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
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("periodicReview.completeError")));
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
                {t("periodicReview.title")}
              </Typography>
              <Typography variant="caption" sx={{
                color: "text.secondary"
              }}>
                {t("periodicReview.subtitle", { code: workspace?.master.companyDocumentCode, title: workspace?.master.title, rev: workspace?.revision.revisionNumber })}
              </Typography>
            </Box>
          </Box>
          <IconButton aria-label={t("periodicReview.closeAria")} onClick={onClose} size="small">
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
              {t("periodicReview.sodWarning", { rev: workspace?.revision.revisionNumber })}
            </Alert>
          )}

          {!loading && workspace && (
            <Box sx={{ display: "flex", flexDirection: "column", gap: 2.5 }}>
              {/* Header Metadata Ribbon */}
              <Paper variant="outlined" sx={{ p: 2, bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50" }}>
                <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)", md: "repeat(6, 1fr)" }, gap: 1.5 }}>
                  <Box>
                    <Typography
                      variant="caption"
                      sx={{
                        color: "text.secondary",
                        display: "block"
                      }}>{t("periodicReview.meta.microLimsId")}</Typography>
                    <Typography variant="body2" sx={{ fontWeight: 700 }}>{workspace.master.microLimsDocumentId}</Typography>
                  </Box>
                  <Box>
                    <Typography
                      variant="caption"
                      sx={{
                        color: "text.secondary",
                        display: "block"
                      }}>{t("periodicReview.meta.effectiveRevision")}</Typography>
                    <Typography variant="body2" sx={{ fontWeight: 700 }}>{t("common.rev", { number: workspace.revision.revisionNumber })}</Typography>
                  </Box>
                  <Box>
                    <Typography
                      variant="caption"
                      sx={{
                        color: "text.secondary",
                        display: "block"
                      }}>{t("periodicReview.meta.reviewDueDate")}</Typography>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                      <Typography variant="body2" sx={{ fontWeight: 700, color: workspace.task.isOverdue ? "error.main" : "text.primary" }}>
                        {formatLabCalendarDate(workspace.task.scheduledDueDate)}
                      </Typography>
                      {workspace.task.isOverdue && <Chip label={t("periodicReview.meta.overdue")} color="error" size="small" sx={compactChipStrongSx} />}
                    </Box>
                  </Box>
                  <Box>
                    <Typography
                      variant="caption"
                      sx={{
                        color: "text.secondary",
                        display: "block"
                      }}>{t("periodicReview.meta.reviewCycle")}</Typography>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>{t("common.months", { count: workspace.task.reviewCycleMonths })}</Typography>
                  </Box>
                  <Box>
                    <Typography
                      variant="caption"
                      sx={{
                        color: "text.secondary",
                        display: "block"
                      }}>{t("periodicReview.meta.assignedReviewer")}</Typography>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>{workspace.task.assignedReviewerFullName || t("periodicReview.meta.unassigned")}</Typography>
                  </Box>
                  <Box>
                    <Typography
                      variant="caption"
                      sx={{
                        color: "text.secondary",
                        display: "block"
                      }}>{t("periodicReview.meta.status")}</Typography>
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
                      {t("periodicReview.pdfBar.title")}
                    </Typography>
                    <Typography variant="caption" sx={{
                      color: "text.secondary"
                    }}>
                      {workspace.controlledPdf ? t("periodicReview.pdfBar.verified", { fileName: workspace.controlledPdf.fileName }) : t("periodicReview.pdfBar.none")}
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
                    {t("periodicReview.pdfBar.inspectButton")}
                  </Button>
                )}
              </Paper>

              {/* Tabs for Findings vs Review History */}
              <Tabs value={currentTab} onChange={(_, val) => setCurrentTab(val)}>
                <Tab label={t("periodicReview.tabs.notesAndFindings", { count: workspace.findings.length })} />
                <Tab label={t("periodicReview.tabs.historicalReviews", { count: workspace.historicalReviews.length })} />
                {!isCompleted && !isAuthor && <Tab label={t("periodicReview.tabs.outcomeAndDecision")} />}
              </Tabs>

              {/* Tab 0: Notes & Findings */}
              {currentTab === 0 && (
                <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
                  {!isCompleted && !isAuthor && (
                    <Paper variant="outlined" sx={{ p: 2 }}>
                      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
                        {t("periodicReview.findings.recordTitle")}
                      </Typography>
                      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "120px 140px 1fr auto" }, gap: 1.5, alignItems: "flex-start" }}>
                        <TextField
                          size="small"
                          label={t("periodicReview.findings.pageLabel")}
                          type="number"
                          value={pageNumber}
                          onChange={(e) => setPageNumber(e.target.value)}
                        />
                        <TextField
                          size="small"
                          label={t("periodicReview.findings.sectionLabel")}
                          value={sectionNumber}
                          onChange={(e) => setSectionNumber(e.target.value)}
                        />
                        <TextField
                          size="small"
                          label={t("periodicReview.findings.noteLabel")}
                          value={noteText}
                          onChange={(e) => setNoteText(e.target.value)}
                          placeholder={t("periodicReview.findings.notePlaceholder")}
                        />
                        <Button
                          variant="outlined"
                          startIcon={<AddCommentIcon />}
                          disabled={submittingFinding || !noteText.trim() || noteText.trim().length < 5}
                          onClick={handleAddFinding}
                          sx={{ height: 40 }}
                        >
                          {t("periodicReview.findings.addNote")}
                        </Button>
                      </Box>
                    </Paper>
                  )}

                  <TableContainer component={Paper} variant="outlined">
                    <Table size="small">
                      <TableHead sx={tableHeadSx(theme)}>
                        <TableRow>
                          <TableCell sx={{ fontWeight: 700, width: 90 }}>{t("periodicReview.findings.columns.page")}</TableCell>
                          <TableCell sx={{ fontWeight: 700, width: 110 }}>{t("periodicReview.findings.columns.section")}</TableCell>
                          <TableCell sx={{ fontWeight: 700 }}>{t("periodicReview.findings.columns.note")}</TableCell>
                          <TableCell sx={{ fontWeight: 700, width: 140 }}>{t("periodicReview.findings.columns.recordedBy")}</TableCell>
                          <TableCell sx={{ fontWeight: 700, width: 110 }}>{t("periodicReview.findings.columns.status")}</TableCell>
                          {!isCompleted && !isAuthor && <TableCell sx={{ fontWeight: 700, width: 90 }} align="right">{t("periodicReview.findings.columns.action")}</TableCell>}
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {workspace.findings.map((f) => (
                          <TableRow key={f.id} hover>
                            <TableCell>{f.pageNumber ?? "—"}</TableCell>
                            <TableCell>{f.sectionNumber || "—"}</TableCell>
                            <TableCell>{f.noteText}</TableCell>
                            <TableCell>
                              <Typography variant="caption" sx={{
                                display: "block"
                              }}>{f.createdByFullName}</Typography>
                              <Typography variant="caption" sx={{
                                color: "text.secondary"
                              }}>{new Date(f.createdAt).toLocaleDateString()}</Typography>
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
                                  <Tooltip title={t("periodicReview.findings.markResolvedTooltip")}>
                                    <IconButton aria-label={t("periodicReview.findings.markResolvedAria")} size="small" color="success" onClick={() => handleResolveFinding(f.id)}>
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
                              {t("periodicReview.findings.empty")}
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
                        <TableCell sx={{ fontWeight: 700 }}>{t("periodicReview.history.columns.reviewDate")}</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{t("periodicReview.history.columns.revision")}</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{t("periodicReview.history.columns.outcome")}</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{t("periodicReview.history.columns.reviewer")}</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{t("periodicReview.history.columns.summaryNotes")}</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{t("periodicReview.history.columns.findings")}</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {workspace.historicalReviews.map((h) => (
                        <TableRow key={h.id} hover>
                          <TableCell>{h.completedAt ? formatDateTimeWithTimeZone(h.completedAt) : new Date(h.createdAt).toLocaleDateString()}</TableCell>
                          <TableCell sx={{ fontWeight: 700 }}>{t("common.rev", { number: h.revisionNumber })}</TableCell>
                          <TableCell>
                            <Chip
                              label={h.outcome || h.status}
                              size="small"
                              color={h.outcome === "RemainsValid" ? "success" : h.outcome === "RevisionRequired" ? "warning" : "default"}
                            />
                          </TableCell>
                          <TableCell>{h.completedByFullName || h.assignedReviewerFullName || "—"}</TableCell>
                          <TableCell sx={{ maxWidth: 300 }}>{h.reviewSummary || "—"}</TableCell>
                          <TableCell>{t("periodicReview.history.notesCount", { count: h.totalFindingsCount })}</TableCell>
                        </TableRow>
                      ))}
                      {workspace.historicalReviews.length === 0 && (
                        <TableRow>
                          <TableCell colSpan={6} align="center" sx={{ py: 3, color: "text.secondary" }}>
                            {t("periodicReview.history.empty")}
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
                    {t("periodicReview.decision.title")}
                  </Typography>

                  <TextField
                    select
                    label={t("periodicReview.decision.outcomeLabel")}
                    value={outcome}
                    onChange={(e) => setOutcome(e.target.value as PeriodicReviewOutcome)}
                    sx={{ maxWidth: 350 }}
                  >
                    <MenuItem value="RemainsValid">
                      <Trans i18nKey="periodicReview.decision.remainsValidOption" ns="documentControl" components={{ strong: <strong /> }} />
                    </MenuItem>
                    <MenuItem value="RevisionRequired">
                      <Trans i18nKey="periodicReview.decision.revisionRequiredOption" ns="documentControl" components={{ strong: <strong /> }} />
                    </MenuItem>
                    <MenuItem value="ObsolescenceRecommended">
                      <Trans i18nKey="periodicReview.decision.obsolescenceOption" ns="documentControl" components={{ strong: <strong /> }} />
                    </MenuItem>
                  </TextField>

                  {outcome === "RemainsValid" && (
                    <Alert severity="info">
                      <Trans
                        i18nKey="periodicReview.decision.remainsValidImpact"
                        ns="documentControl"
                        values={{ months: workspace.task.reviewCycleMonths }}
                        components={{ strong: <strong /> }}
                      />
                    </Alert>
                  )}

                  {outcome === "RevisionRequired" && (
                    <Alert severity="warning">
                      <Trans
                        i18nKey="periodicReview.decision.revisionRequiredImpact"
                        ns="documentControl"
                        values={{ count: workspace.findings.length }}
                        components={{ strong: <strong /> }}
                      />
                    </Alert>
                  )}

                  {outcome === "ObsolescenceRecommended" && (
                    <Alert severity="error">
                      <Trans
                        i18nKey="periodicReview.decision.obsolescenceImpact"
                        ns="documentControl"
                        components={{ strong: <strong /> }}
                      />
                    </Alert>
                  )}

                  <TextField
                    multiline
                    rows={4}
                    label={t("periodicReview.decision.summaryLabel")}
                    value={reviewSummary}
                    onChange={(e) => setReviewSummary(e.target.value)}
                    placeholder={t("periodicReview.decision.summaryPlaceholder")}
                  />

                  <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
                    <Button
                      variant="contained"
                      color="primary"
                      disabled={submittingDecision || !reviewSummary.trim() || reviewSummary.trim().length < 10}
                      onClick={handleCompleteReview}
                    >
                      {submittingDecision ? <CircularProgress size={24} /> : t("periodicReview.decision.submit")}
                    </Button>
                  </Box>
                </Paper>
              )}
            </Box>
          )}
        </DialogContent>

        <DialogActions sx={{ p: 2 }}>
          <Button onClick={onClose} color="inherit">
            {t("periodicReview.closeWorkspace")}
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
