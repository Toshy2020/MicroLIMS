import { useState, useEffect, useCallback, useMemo } from "react";
import {
  Box,
  Typography,
  Paper,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  Alert,
  CircularProgress,
  Tabs,
  Tab,
  TextField,
  InputAdornment,
  IconButton,
  Tooltip,
  LinearProgress,
  Stack,
  Card,
  CardContent,
  Grid,
  useTheme
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import RefreshIcon from "@mui/icons-material/Refresh";
import MenuBookIcon from "@mui/icons-material/MenuBook";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import ClearIcon from "@mui/icons-material/Clear";

import { PageHeader } from "../../../components/PageHeader";
import { tableHeadSx } from "../../../theme";
import { StatusBadge } from "../../../components/StatusBadge";
import { useAuth } from "../../../contexts/AuthContext";
import { trainingAssignmentService } from "../services/trainingAssignmentService";
import { documentAcknowledgementService } from "../services/documentAcknowledgementService";
import { documentRevisionService } from "../services/documentRevisionService";
import { ControlledPdfViewer } from "../components/ControlledPdfViewer";
import { DocumentAcknowledgementDialog } from "../components/DocumentAcknowledgementDialog";
import type {
  DocumentTrainingAssignmentDto,
  TrainingAssignmentStatus
} from "../types/trainingAssignmentTypes";
import type { AcknowledgementPresentationDto } from "../types/acknowledgementTypes";
import { compactChipStrongSx, compactChipSx } from "../documentControlStyles";

/**
 * The views ML-DC-FRS-1C-001 §2:87 requires of My Reading List: Pending ("active
 * assignments where DueDateUtc >= UtcNow"), Overdue ("DueDateUtc < UtcNow") and
 * Completed, plus the superseded archive §2:174 asks for.
 *
 * Note that Pending deliberately EXCLUDES overdue work. The spec splits them on
 * the due date, and an assignment cannot sensibly be counted under both - the
 * previous filter put overdue items in its "Action Required" group as well as in
 * Overdue, which double-counted them.
 */
type ReadingCategory = "PENDING" | "OVERDUE" | "COMPLETED" | "SUPERSEDED" | "OTHER";

function categoryOf(a: DocumentTrainingAssignmentDto): ReadingCategory {
  if (a.status === "Acknowledged" || a.status === "CompletedPassed") return "COMPLETED";
  if (a.status === "SupersededIncomplete" || a.status === "TrainedOnSupersededOnly")
    return "SUPERSEDED";
  // Overdue is checked before Pending: an assignment past its due date belongs to
  // Overdue even though its status is still Assigned or Reading.
  if (a.isOverdue || a.status === "Overdue") return "OVERDUE";
  if (a.status === "Assigned" || a.status === "Reading") return "PENDING";
  return "OTHER";
}

const READING_TABS: { value: ReadingCategory | "ALL"; label: string }[] = [
  { value: "PENDING", label: "To read" },
  { value: "OVERDUE", label: "Overdue" },
  { value: "COMPLETED", label: "Completed" },
  { value: "SUPERSEDED", label: "Superseded" },
  { value: "ALL", label: "All" }
];

export function MyReadingListPage() {
  const theme = useTheme();
  const { fullName, username } = useAuth();

  // Assignment List State
  const [assignments, setAssignments] = useState<DocumentTrainingAssignmentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  // Opens on work the reader still has to do, which is why they came here.
  const [statusFilter, setStatusFilter] = useState<ReadingCategory | "ALL">("PENDING");
  const [searchQuery, setSearchQuery] = useState("");

  // Reading Progress State tracking per assignment (assignmentId -> percent)
  const [readingProgressMap, setReadingProgressMap] = useState<Record<number, number>>({});

  // Controlled Viewer State
  const [viewerOpen, setViewerOpen] = useState(false);
  const [activeAssignment, setActiveAssignment] = useState<DocumentTrainingAssignmentDto | null>(null);
  const [activeFileId, setActiveFileId] = useState<number | null>(null);
  const [activeFileName, setActiveFileName] = useState<string>("");
  const [viewerLoading, setViewerLoading] = useState(false);

  // Acknowledgement Dialog State
  const [ackDialogOpen, setAckDialogOpen] = useState(false);
  const [ackContext, setAckContext] = useState<AcknowledgementPresentationDto | null>(null);
  const [ackLoading, setAckLoading] = useState(false);

  // Load assignments for current authenticated user
  const loadAssignments = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await trainingAssignmentService.getMyAssignments();
      setAssignments(data);
    } catch (err: any) {
      setError(err?.response?.data?.message || err?.message || "Failed to load your reading assignments.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadAssignments();
  }, [loadAssignments]);

  // Filtered Assignments
  const filteredAssignments = useMemo(() => {
    return assignments.filter((item) => {
      if (statusFilter !== "ALL" && categoryOf(item) !== statusFilter) return false;

      // Search query filter (Document title, code, MicroLIMS ID, or revision)
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase();
        const matchTitle = item.documentTitle.toLowerCase().includes(q);
        const matchCode = item.companyDocumentCode.toLowerCase().includes(q);
        const matchMicroId = item.microLimsDocumentId.toLowerCase().includes(q);
        const matchRev = item.revisionNumber.toLowerCase().includes(q);
        if (!matchTitle && !matchCode && !matchMicroId && !matchRev) {
          return false;
        }
      }

      return true;
    });
  }, [assignments, statusFilter, searchQuery]);

  // Metrics calculation. Derived from categoryOf so the figures on the cards and
  // the counts on the tabs can never disagree with what the tab actually lists -
  // "pending" used to be defined once here and again inside the filter with
  // different rules, so an assignment that was both Assigned and overdue counted
  // as pending here while the Overdue view also claimed it.
  const metrics = useMemo(() => {
    const count = (c: ReadingCategory) =>
      assignments.filter((a) => categoryOf(a) === c).length;
    return {
      total: assignments.length,
      pending: count("PENDING"),
      overdue: count("OVERDUE"),
      completed: count("COMPLETED"),
      superseded: count("SUPERSEDED")
    };
  }, [assignments]);

  // Open Controlled Document Viewer for an assignment
  const handleOpenViewer = async (assignment: DocumentTrainingAssignmentDto) => {
    setActiveAssignment(assignment);
    setViewerLoading(true);
    setError(null);

    try {
      // Fetch revision details to locate the active ControlledPdf file
      const revDetails = await documentRevisionService.getRevisionDetails(assignment.documentRevisionId);
      const controlledPdf = revDetails.files.find(
        (f) => f.fileRole === "ControlledPdf" && f.isActive
      );

      if (controlledPdf) {
        setActiveFileId(controlledPdf.id);
        setActiveFileName(controlledPdf.fileName);
      } else {
        setActiveFileId(null);
        setActiveFileName("");
      }

      setViewerOpen(true);
    } catch (err: any) {
      setError(
        err?.response?.data?.message ||
          err?.message ||
          "Failed to locate controlled document file for this revision."
      );
    } finally {
      setViewerLoading(false);
    }
  };

  // Record Informational Reading Progress
  const handleProgressUpdate = async (assignmentId: number, revisionId: number, progress: number) => {
    setReadingProgressMap((prev) => ({ ...prev, [assignmentId]: progress }));

    try {
      await documentAcknowledgementService.recordReadingProgress(assignmentId, {
        assignmentId,
        documentRevisionId: revisionId,
        progressPercentage: progress
      });
      // Silently refresh reading state without interrupting viewer
      setAssignments((prev) =>
        prev.map((item) =>
          item.id === assignmentId && item.status === "Assigned"
            ? { ...item, status: "Reading" as TrainingAssignmentStatus }
            : item
        )
      );
    } catch (err) {
      console.error("Failed to record reading progress:", err);
    }
  };

  // Open Acknowledgement Dialog
  const handleOpenAcknowledgement = async (assignment: DocumentTrainingAssignmentDto) => {
    try {
      setAckLoading(true);
      setError(null);
      const context = await documentAcknowledgementService.getAcknowledgementContext(assignment.id);
      setAckContext(context);
      setActiveAssignment(assignment);
      setAckDialogOpen(true);
    } catch (err: any) {
      setError(
        err?.response?.data?.message ||
          err?.message ||
          "Failed to retrieve legal acknowledgement statement."
      );
    } finally {
      setAckLoading(false);
    }
  };

  // Submit Conscious Acknowledgement
  const handleConfirmAcknowledgement = async (confirmed: boolean, comments?: string) => {
    if (!ackContext || !activeAssignment) return;

    await documentAcknowledgementService.submitAcknowledgement(activeAssignment.id, {
      assignmentId: activeAssignment.id,
      documentRevisionId: activeAssignment.documentRevisionId,
      confirmedLegalStatement: confirmed,
      comments: comments || null
    });

    // Refresh assignment list to reflect newly recorded evidence
    await loadAssignments();
  };

  return (
    <Box sx={{ pb: 4 }}>
      <PageHeader
        title="My Reading List"
        subtitle={`Active document training and standard operating procedures assigned to ${fullName || username || "you"}.`}
      />

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {/* Summary KPI Cards */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid
          size={{
            xs: 12,
            sm: 6,
            md: 3
          }}>
          <Card variant="outlined">
            <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
              <Typography
                variant="caption"
                sx={{
                  color: "text.secondary",
                  fontWeight: "bold"
                }}>
                TOTAL ASSIGNMENTS
              </Typography>
              <Typography
                variant="h5"
                sx={{
                  fontWeight: "bold",
                  color: "text.primary"
                }}>
                {metrics.total}
              </Typography>
            </CardContent>
          </Card>
        </Grid>

        <Grid
          size={{
            xs: 12,
            sm: 6,
            md: 3
          }}>
          <Card variant="outlined" sx={{ bgcolor: metrics.pending > 0 ? "info.50" : "inherit" }}>
            <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
              <Typography
                variant="caption"
                sx={{
                  color: "info.main",
                  fontWeight: "bold"
                }}>
                PENDING READING / ACKNOWLEDGEMENT
              </Typography>
              <Typography
                variant="h5"
                sx={{
                  fontWeight: "bold",
                  color: "info.main"
                }}>
                {metrics.pending}
              </Typography>
            </CardContent>
          </Card>
        </Grid>

        <Grid
          size={{
            xs: 12,
            sm: 6,
            md: 3
          }}>
          <Card variant="outlined" sx={{ bgcolor: metrics.overdue > 0 ? "error.50" : "inherit" }}>
            <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
              <Typography
                variant="caption"
                sx={{
                  color: "error.main",
                  fontWeight: "bold"
                }}>
                OVERDUE ASSIGNMENTS
              </Typography>
              <Typography
                variant="h5"
                sx={{
                  fontWeight: "bold",
                  color: "error.main"
                }}>
                {metrics.overdue}
              </Typography>
            </CardContent>
          </Card>
        </Grid>

        <Grid
          size={{
            xs: 12,
            sm: 6,
            md: 3
          }}>
          <Card variant="outlined" sx={{ bgcolor: metrics.completed > 0 ? "success.50" : "inherit" }}>
            <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
              <Typography
                variant="caption"
                sx={{
                  color: "success.main",
                  fontWeight: "bold"
                }}>
                COMPLETED & ACKNOWLEDGED
              </Typography>
              <Typography
                variant="h5"
                sx={{
                  fontWeight: "bold",
                  color: "success.main"
                }}>
                {metrics.completed}
              </Typography>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {/* Filter Toolbar */}
      <Paper variant="outlined" sx={{ p: 2, mb: 3 }}>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{
            alignItems: "center",
            justifyContent: "space-between"
          }}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={2}
            sx={{
              alignItems: "center",
              width: "100%",
              maxWidth: 800
            }}>
            <TextField
              size="small"
              placeholder="Search document title, code, MicroLIMS ID..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              sx={{ minWidth: 320 }}
              slotProps={{
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <SearchIcon fontSize="small" color="action" />
                    </InputAdornment>
                  ),
                  endAdornment: searchQuery ? (
                    <InputAdornment position="end">
                      <IconButton aria-label="Clear search" size="small" onClick={() => setSearchQuery("")}>
                        <ClearIcon fontSize="small" />
                      </IconButton>
                    </InputAdornment>
                  ) : null
                }
              }}
            />

          </Stack>

          <Button
            size="small"
            variant="outlined"
            startIcon={<RefreshIcon />}
            onClick={() => loadAssignments()}
            disabled={loading}
          >
            Refresh List
          </Button>
        </Stack>
      </Paper>

      {/* Assignments Table */}
      {/* The three views ML-DC-FRS-1C-001 §2:87 requires, plus the superseded
          archive from §2:174. These replaced an eleven-option status dropdown that
          mixed grouped semantics ("Action Required") with raw enum values
          ("Assigned", "Reading") and offered two near-duplicate ways to see
          overdue work. Tabs also put the counts on screen: whether anything is
          overdue is the question this page exists to answer, and a dropdown hid
          it behind a click. */}
      <Paper variant="outlined" sx={{ mb: 2 }}>
        <Tabs
          value={statusFilter}
          onChange={(_e, v: ReadingCategory | "ALL") => setStatusFilter(v)}
          variant="scrollable"
          scrollButtons="auto"
          aria-label="Reading list views"
          sx={{ px: 1 }}
        >
          {READING_TABS.map((t) => {
            const n =
              t.value === "ALL"
                ? metrics.total
                : t.value === "PENDING"
                  ? metrics.pending
                  : t.value === "OVERDUE"
                    ? metrics.overdue
                    : t.value === "COMPLETED"
                      ? metrics.completed
                      : metrics.superseded;
            return <Tab key={t.value} value={t.value} label={`${t.label} (${n})`} />;
          })}
        </Tabs>
      </Paper>

      <TableContainer component={Paper} variant="outlined">
        <Table size="medium">
          <TableHead sx={tableHeadSx(theme)}>
            <TableRow>
              <TableCell sx={{ fontWeight: 700 }}>Company Document Code & Title</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Revision</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Assignment Type</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Status</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Due Date (UTC)</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Reading Progress</TableCell>
              <TableCell align="right" sx={{ fontWeight: 700 }}>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 6 }}>
                  <CircularProgress size={32} sx={{ mb: 1 }} />
                  <Typography variant="body2" sx={{
                    color: "text.secondary"
                  }}>
                    Loading your document training assignments...
                  </Typography>
                </TableCell>
              </TableRow>
            ) : filteredAssignments.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 6 }}>
                  <MenuBookIcon sx={{ fontSize: 48, color: "text.disabled", mb: 1 }} />
                  <Typography variant="subtitle1" sx={{
                    fontWeight: "bold"
                  }}>
                    No Training Assignments Found
                  </Typography>
                  <Typography variant="body2" sx={{
                    color: "text.secondary"
                  }}>
                    {searchQuery || statusFilter !== "ALL"
                      ? "No assignments matched your selected filter criteria."
                      : "You have no active or completed document reading assignments."}
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              filteredAssignments.map((assignment) => {
                const isOverdue = assignment.isOverdue || assignment.status === "Overdue";
                const isAcknowledged =
                  assignment.status === "Acknowledged" ||
                  assignment.status === "CompletedPassed";
                const isSuperseded =
                  assignment.status === "SupersededIncomplete" ||
                  assignment.status === "TrainedOnSupersededOnly";
                const isCancelled = assignment.status === "Cancelled";
                const currentProgress = readingProgressMap[assignment.id] ?? (isAcknowledged ? 100 : 0);

                const canAcknowledge = !isAcknowledged && !isSuperseded && !isCancelled;

                return (
                  <TableRow
                    key={assignment.id}
                    hover
                    sx={{
                      bgcolor: isOverdue
                        ? (t) => t.palette.mode === "dark" ? "rgba(211, 47, 47, 0.15)" : "error.50"
                        : isSuperseded
                        ? (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50"
                        : "inherit"
                    }}
                  >
                    <TableCell>
                      <Box sx={{ display: "flex", flexDirection: "column" }}>
                        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                          <Typography
                            variant="subtitle2"
                            sx={{
                              fontWeight: "bold",
                              color: "primary.main"
                            }}>
                            {assignment.companyDocumentCode}
                          </Typography>
                          <Chip
                            label={`MicroLIMS: ${assignment.microLimsDocumentId}`}
                            size="small"
                            variant="outlined"
                            sx={compactChipSx}
                          />
                        </Box>
                        <Typography
                          variant="body2"
                          sx={{
                            fontWeight: "medium",
                            mt: 0.5
                          }}>
                          {assignment.documentTitle}
                        </Typography>
                      </Box>
                    </TableCell>

                    <TableCell>
                      <Chip
                        label={`Rev ${assignment.revisionNumber}`}
                        size="small"
                        color="default"
                        sx={{ fontWeight: 600 }}
                      />
                    </TableCell>

                    <TableCell>
                      <Typography variant="body2" sx={{
                        color: "text.secondary"
                      }}>
                        {assignment.assignmentType}
                      </Typography>
                    </TableCell>

                    <TableCell>
                      <Box sx={{ display: "flex", flexDirection: "column", gap: 0.5, alignItems: "flex-start" }}>
                        <StatusBadge status={assignment.status} />

                        {isOverdue && (
                          <Chip
                            icon={<ErrorOutlineIcon />}
                            label={`Overdue by ${Math.abs(assignment.daysRemainingOrOverdue)}d`}
                            size="small"
                            color="error"
                            sx={compactChipStrongSx}
                          />
                        )}

                        {isSuperseded && (
                          <Chip
                            icon={<WarningAmberIcon />}
                            label="Superseded Revision"
                            size="small"
                            color="warning"
                            sx={compactChipSx}
                          />
                        )}
                      </Box>
                    </TableCell>

                    <TableCell>
                      <Box sx={{ display: "flex", flexDirection: "column" }}>
                        <Typography variant="body2">
                          {new Date(assignment.dueDateUtc).toLocaleDateString()}
                        </Typography>
                        {!isAcknowledged && !isOverdue && (
                          <Typography variant="caption" sx={{
                            color: "text.secondary"
                          }}>
                            {assignment.daysRemainingOrOverdue > 0
                              ? `${assignment.daysRemainingOrOverdue} days remaining`
                              : "Due today"}
                          </Typography>
                        )}
                        {isAcknowledged && assignment.acknowledgedAtUtc && (
                          <Typography
                            variant="caption"
                            sx={{
                              color: "success.main",
                              display: "flex",
                              alignItems: "center",
                              gap: 0.5
                            }}>
                            <CheckCircleOutlineIcon fontSize="inherit" />
                            Ack: {new Date(assignment.acknowledgedAtUtc).toLocaleDateString()}
                          </Typography>
                        )}
                      </Box>
                    </TableCell>

                    <TableCell sx={{ minWidth: 160 }}>
                      <Box sx={{ display: "flex", flexDirection: "column", gap: 0.5 }}>
                        <Box sx={{ display: "flex", justifyContent: "space-between" }}>
                          <Typography variant="caption" sx={{
                            color: "text.secondary"
                          }}>
                            {isAcknowledged ? "Completed" : `${currentProgress}% Read`}
                          </Typography>
                        </Box>
                        <LinearProgress
                          variant="determinate"
                          value={currentProgress}
                          sx={{ height: 6, borderRadius: 1 }}
                          color={isAcknowledged ? "success" : currentProgress > 0 ? "primary" : "inherit"}
                        />
                      </Box>
                    </TableCell>

                    <TableCell align="right">
                      <Stack direction="row" spacing={1} sx={{
                        justifyContent: "flex-end"
                      }}>
                        <Tooltip title="Open Controlled Revision in PDF Viewer">
                          <Button
                            variant="outlined"
                            size="small"
                            startIcon={<PictureAsPdfIcon />}
                            onClick={() => handleOpenViewer(assignment)}
                            disabled={viewerLoading}
                          >
                            Read
                          </Button>
                        </Tooltip>

                        {canAcknowledge && (
                          <Tooltip title="Record Legal Read-and-Understand Acknowledgement">
                            <Button
                              variant="contained"
                              color="primary"
                              size="small"
                              startIcon={<VerifiedUserIcon />}
                              onClick={() => handleOpenAcknowledgement(assignment)}
                              disabled={ackLoading}
                            >
                              Acknowledge
                            </Button>
                          </Tooltip>
                        )}
                      </Stack>
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Controlled Document Viewer Modal */}
      {activeAssignment && (
        <ControlledPdfViewer
          open={viewerOpen}
          onClose={() => setViewerOpen(false)}
          fileId={activeFileId}
          fileName={activeFileName}
          companyDocumentCode={activeAssignment.companyDocumentCode}
          microLimsDocumentId={activeAssignment.microLimsDocumentId}
          revisionNumber={activeAssignment.revisionNumber}
          title={activeAssignment.documentTitle}
          revisionStatus={activeAssignment.status === "SupersededIncomplete" ? "Superseded" : "Effective"}
          assignmentId={activeAssignment.id}
          readingProgress={readingProgressMap[activeAssignment.id] ?? (activeAssignment.status === "Acknowledged" ? 100 : 0)}
          onProgressUpdate={(progress) =>
            handleProgressUpdate(activeAssignment.id, activeAssignment.documentRevisionId, progress)
          }
          isAcknowledged={
            activeAssignment.status === "Acknowledged" ||
            activeAssignment.status === "CompletedPassed"
          }
          canAcknowledge={
            activeAssignment.status !== "Acknowledged" &&
            activeAssignment.status !== "CompletedPassed" &&
            activeAssignment.status !== "SupersededIncomplete" &&
            activeAssignment.status !== "Cancelled"
          }
          onAcknowledgeClick={() => {
            setViewerOpen(false);
            handleOpenAcknowledgement(activeAssignment);
          }}
        />
      )}

      {/* Conscious Legal Acknowledgement Dialog */}
      {ackContext && activeAssignment && (
        <DocumentAcknowledgementDialog
          open={ackDialogOpen}
          context={ackContext}
          loading={ackLoading}
          readingProgress={readingProgressMap[activeAssignment.id] ?? 100}
          onClose={() => {
            setAckDialogOpen(false);
            setAckContext(null);
          }}
          onConfirmAcknowledgement={handleConfirmAcknowledgement}
        />
      )}
    </Box>
  );
}
