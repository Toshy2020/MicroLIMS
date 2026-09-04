import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Box,
  Typography,
  Button,
  Paper,
  Tabs,
  Tab,
  Chip,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  IconButton,
  Tooltip,
  Alert,
  CircularProgress,
  Divider
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import CloudUploadOutlinedIcon from "@mui/icons-material/CloudUploadOutlined";
import PictureAsPdfOutlinedIcon from "@mui/icons-material/PictureAsPdfOutlined";
import DownloadOutlinedIcon from "@mui/icons-material/DownloadOutlined";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import PersonAddOutlinedIcon from "@mui/icons-material/PersonAddOutlined";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import RateReviewOutlinedIcon from "@mui/icons-material/RateReviewOutlined";
import AutorenewOutlinedIcon from "@mui/icons-material/AutorenewOutlined";
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import FormatListBulletedOutlinedIcon from "@mui/icons-material/FormatListBulletedOutlined";
import AssignmentTurnedInIcon from "@mui/icons-material/AssignmentTurnedIn";
import CompareArrowsIcon from "@mui/icons-material/CompareArrows";
import EventRepeatIcon from "@mui/icons-material/EventRepeat";
import SecurityIcon from "@mui/icons-material/Security";
import PrecisionManufacturingIcon from "@mui/icons-material/PrecisionManufacturing";
import PersonOutlineIcon from "@mui/icons-material/PersonOutline";

import { PageHeader } from "../../../components/PageHeader";
import { StatusBadge } from "../../../components/StatusBadge";
import { useAuth } from "../../../contexts/AuthContext";
import { documentControlService } from "../services/documentControlService";
import { documentReviewService } from "../services/documentReviewService";
import { documentApprovalService } from "../services/documentApprovalService";
import { ControlledPdfViewer } from "../components/ControlledPdfViewer";
import { DualPdfComparisonViewer } from "../components/DualPdfComparisonViewer";
import { FileUploadDialog } from "../components/FileUploadDialog";
import { DraftMetadataDialog } from "../components/DraftMetadataDialog";
import { VoidDocumentDialog } from "../components/VoidDocumentDialog";
import { CancelDraftDialog } from "../components/CancelDraftDialog";
import { AssignmentDialog } from "../components/AssignmentDialog";
import { SubmitForReviewDialog } from "../components/SubmitForReviewDialog";
import { TechnicalReviewDrawer } from "../components/TechnicalReviewDrawer";
import { CreateRevisionDialog } from "../components/CreateRevisionDialog";
import { RevisionImpactAssessmentDialog } from "../components/RevisionImpactAssessmentDialog";
import { RevisionChangeItemsDialog } from "../components/RevisionChangeItemsDialog";
import { AssignApproverDialog } from "../components/AssignApproverDialog";
import { ApprovalWorkspaceDialog } from "../components/ApprovalWorkspaceDialog";
import { PeriodicReviewWorkspaceDialog } from "../components/PeriodicReviewWorkspaceDialog";
import { periodicReviewService } from "../services/periodicReviewService";
import type {
  DocumentMasterDto,
  DocumentAuditItemDto,
  DocumentReviewTaskDto,
  DocumentApprovalTaskDto,
  PeriodicReviewTaskDto
} from "../types/documentControlTypes";

export function DocumentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { role, userId } = useAuth();

  const [document, setDocument] = useState<DocumentMasterDto | null>(null);
  const [auditLogs, setAuditLogs] = useState<DocumentAuditItemDto[]>([]);
  const [reviewTasks, setReviewTasks] = useState<DocumentReviewTaskDto[]>([]);
  const [currentTab, setCurrentTab] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Dialogs state
  const [pdfViewerOpen, setPdfViewerOpen] = useState(false);
  const [activePdfFileId, setActivePdfFileId] = useState<number | null>(null);
  const [activePdfFileName, setActivePdfFileName] = useState<string>("");
  const [dualPdfViewerOpen, setDualPdfViewerOpen] = useState(false);

  const [uploadDialogOpen, setUploadDialogOpen] = useState(false);
  const [editMetadataOpen, setEditMetadataOpen] = useState(false);
  const [voidDialogOpen, setVoidDialogOpen] = useState(false);
  const [cancelDraftOpen, setCancelDraftOpen] = useState(false);
  const [assignmentOpen, setAssignmentOpen] = useState(false);
  const [submitReviewOpen, setSubmitReviewOpen] = useState(false);
  const [reviewDrawerOpen, setReviewDrawerOpen] = useState(false);
  const [selectedReviewTask, setSelectedReviewTask] = useState<DocumentReviewTaskDto | null>(null);
  const [createRevisionOpen, setCreateRevisionOpen] = useState(false);
  const [impactAssessmentOpen, setImpactAssessmentOpen] = useState(false);
  const [changeItemsOpen, setChangeItemsOpen] = useState(false);
  const [approvalTasks, setApprovalTasks] = useState<DocumentApprovalTaskDto[]>([]);
  const [assignApproverOpen, setAssignApproverOpen] = useState(false);
  const [approvalWorkspaceOpen, setApprovalWorkspaceOpen] = useState(false);
  const [selectedApprovalTaskId, setSelectedApprovalTaskId] = useState<number | null>(null);
  const [periodicReviewTasks, setPeriodicReviewTasks] = useState<PeriodicReviewTaskDto[]>([]);
  const [periodicReviewDialogOpen, setPeriodicReviewDialogOpen] = useState(false);
  const [selectedPeriodicReviewTaskId, setSelectedPeriodicReviewTaskId] = useState<number | null>(null);

  const fetchDocument = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);

    try {
      const data = await documentControlService.getById(Number(id));
      setDocument(data);

      // Load record audit
      const audit = await documentControlService.getDocumentAudit(Number(id));
      setAuditLogs(audit);

      // Load periodic review history across all revisions of this master
      try {
        const pTasks = await periodicReviewService.getMasterHistory(Number(id));
        setPeriodicReviewTasks(Array.isArray(pTasks) ? pTasks : []);
      } catch {
        setPeriodicReviewTasks([]);
      }

      // Load review and approval tasks for in-flight revision or effective revision
      const inFlightRev = data.revisions.find(
        (r) => r.revisionStatus === "Draft" || r.revisionStatus === "InReview" || r.revisionStatus === "InApproval" || r.revisionStatus === "AwaitingApproval"
      );
      const effectiveRev = data.currentEffectiveRevisionId
        ? data.revisions.find((r) => r.id === data.currentEffectiveRevisionId)
        : null;
      const targetRev = inFlightRev || effectiveRev || data.revisions[0];
      if (targetRev) {
        try {
          const tasks = await documentReviewService.getReviewTasksByRevisionId(targetRev.id);
          setReviewTasks(tasks);
        } catch {
          // non-blocking
        }

        try {
          const appTasks = await documentApprovalService.getApprovalTasks({ revisionId: targetRev.id });
          setApprovalTasks(appTasks);
        } catch {
          // non-blocking
        }
      }
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to load document details.");
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchDocument();
  }, [fetchDocument]);

  if (loading) {
    return (
      <Box sx={{ p: 6, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center" }}>
        <CircularProgress size={40} />
        <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
          Loading document record...
        </Typography>
      </Box>
    );
  }

  if (error || !document) {
    return (
      <Box sx={{ p: 4, maxWidth: 800, mx: "auto" }}>
        <Button startIcon={<ArrowBackIcon />} onClick={() => navigate("/document-control/library")} sx={{ mb: 2 }}>
          Back to Library
        </Button>
        <Alert severity="error">{error || "Document not found."}</Alert>
      </Box>
    );
  }

  // Authoritative permissions
  const isController = role === "SectionHead";
  const isAdmin = role === "SystemAdministrator";
  const isOwner = document.documentOwnerUserId === userId;
  const isAssignedAuthor = document.assignments.some(
    (a) => a.userId === userId && a.assignmentRole === "Author" && a.isActive
  );
  const effectiveRevision = document.currentEffectiveRevisionId
    ? document.revisions.find((r) => r.id === document.currentEffectiveRevisionId)
    : null;

  const inWorkflowRevision = document.revisions.find(
    (r) => r.revisionStatus === "Draft" || r.revisionStatus === "InReview" || r.revisionStatus === "InApproval" || r.revisionStatus === "AwaitingApproval"
  );

  const currentRevision = inWorkflowRevision || effectiveRevision || document.revisions[0];

  const hasEffectiveRev = !!effectiveRevision;
  const hasInFlightRev = !!inWorkflowRevision;
  const canCreateRevision = hasEffectiveRev && !hasInFlightRev && document.recordStatus === "Active" && (isAdmin || isController || isOwner || isAssignedAuthor);

  const canEditDraft = (isAdmin || isController || isOwner || isAssignedAuthor) &&
    document.recordStatus === "Active" &&
    currentRevision?.revisionStatus === "Draft";

  const activePdf = currentRevision?.files.find((f) => f.fileRole === "ControlledPdf" && f.isActive);
  const activeSource = currentRevision?.files.find((f) => f.fileRole === "SourceFile" && f.isActive);
  const historicalFiles = currentRevision?.files.filter((f) => !f.isActive) || [];

  // Effective PDF for side-by-side comparative inspection (DC-URS-067)
  const effectivePdf = effectiveRevision?.files.find((f) => f.fileRole === "ControlledPdf" && f.isActive);

  const handleOpenPdfViewer = (fileId: number, fileName: string) => {
    setActivePdfFileId(fileId);
    setActivePdfFileName(fileName);
    setPdfViewerOpen(true);
  };

  const handleDownloadFile = async (fileId: number, fileName: string) => {
    try {
      await documentControlService.downloadFile(fileId, fileName);
    } catch (err: any) {
      alert("Download failed: " + (err.response?.data?.message || err.message));
    }
  };

  const handleRemoveAssignment = async (assignmentId: number) => {
    if (!window.confirm("Are you sure you want to remove this role assignment?")) return;
    try {
      await documentControlService.removeAssignment(document.id, assignmentId);
      fetchDocument();
    } catch (err: any) {
      alert("Failed to remove assignment: " + (err.response?.data?.message || err.message));
    }
  };

  return (
    <Box sx={{ p: 3, maxWidth: 1600, mx: "auto" }}>
      {/* Back button & Breadcrumbs */}
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
        <Button
          size="small"
          startIcon={<ArrowBackIcon />}
          onClick={() => navigate("/document-control/library")}
          color="inherit"
        >
          Document Library
        </Button>
      </Box>

      {/* Header Record Card */}
      <Paper sx={{ p: 3, mb: 3 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 2 }}>
          <Box>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 0.5 }}>
              <Typography variant="h5" sx={{ fontWeight: 800, color: "primary.main" }}>
                {document.companyDocumentCode}
              </Typography>
              <Chip label={document.documentTypeCode} size="small" variant="outlined" sx={{ fontWeight: 600 }} />
              <StatusBadge
                status={document.recordStatus === "Void" ? "Void" : currentRevision?.revisionStatus || "Draft"}
              />
              {document.recordOrigin === "Migrated" && (
                <Chip label="MIGRATED RECORD" size="small" color="secondary" variant="outlined" />
              )}
            </Box>

            <Typography variant="h6" sx={{ fontWeight: 600, mb: 0.5 }}>
              {document.title}
            </Typography>

            <Box sx={{ display: "flex", gap: 3, flexWrap: "wrap", color: "text.secondary" }}>
              <Typography variant="caption">
                MicroLIMS ID: <strong style={{ fontFamily: "monospace" }}>{document.microLimsDocumentId}</strong>
              </Typography>
              <Typography variant="caption">
                Department: <strong>{document.departmentName}</strong> ({document.sectionName})
              </Typography>
              <Typography variant="caption">
                Current Revision: <strong>{currentRevision?.revisionNumber || "01"}</strong> ({currentRevision?.revisionStatus})
              </Typography>
              <Typography variant="caption">
                Owner: <strong>{document.documentOwnerUserName}</strong>
              </Typography>
            </Box>
          </Box>

          {/* Action Toolbar */}
          <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap" }}>
            {canEditDraft && (
              <>
                <Button
                  variant="outlined"
                  size="small"
                  startIcon={<EditOutlinedIcon />}
                  onClick={() => setEditMetadataOpen(true)}
                >
                  Edit Metadata
                </Button>

                {currentRevision?.revisionStatus === "Draft" && (
                  <>
                    <Button
                      variant="contained"
                      size="small"
                      startIcon={<CloudUploadOutlinedIcon />}
                      onClick={() => setUploadDialogOpen(true)}
                    >
                      {activePdf ? "Replace File" : "Upload Draft File"}
                    </Button>

                    <Button
                      variant="outlined"
                      color="warning"
                      size="small"
                      startIcon={<CancelOutlinedIcon />}
                      onClick={() => setCancelDraftOpen(true)}
                    >
                      Cancel Draft
                    </Button>

                    <Button
                      variant="outlined"
                      size="small"
                      startIcon={<FormatListBulletedOutlinedIcon />}
                      onClick={() => setChangeItemsOpen(true)}
                    >
                      Change Items
                    </Button>

                    <Button
                      variant="outlined"
                      size="small"
                      startIcon={<FactCheckOutlinedIcon />}
                      onClick={() => setImpactAssessmentOpen(true)}
                    >
                      Impact Assessment
                    </Button>

                    {activePdf && (
                      <Button
                        variant="contained"
                        color="primary"
                        size="small"
                        startIcon={<RateReviewOutlinedIcon />}
                        onClick={() => setSubmitReviewOpen(true)}
                      >
                        Submit for Review
                      </Button>
                    )}
                  </>
                )}
              </>
            )}

            {canCreateRevision && (
              <Button
                variant="contained"
                color="secondary"
                size="small"
                startIcon={<AutorenewOutlinedIcon />}
                onClick={() => setCreateRevisionOpen(true)}
              >
                Create Revision
              </Button>
            )}

            {reviewTasks.length > 0 && (
              <Button
                variant="outlined"
                color="info"
                size="small"
                startIcon={<RateReviewOutlinedIcon />}
                onClick={() => {
                  setSelectedReviewTask(reviewTasks[0]);
                  setReviewDrawerOpen(true);
                }}
              >
                Technical Review ({reviewTasks[0].status})
              </Button>
            )}

            {/* Approval Workflow Buttons */}
            {(currentRevision?.revisionStatus === "AwaitingApproval" ||
              currentRevision?.revisionStatus === "InApproval") && (
              <>
                {!approvalTasks.some((t) => t.status === "Pending") && canEditDraft && (
                  <Button
                    variant="contained"
                    color="primary"
                    size="small"
                    startIcon={<AssignmentTurnedInIcon />}
                    onClick={() => setAssignApproverOpen(true)}
                  >
                    Assign Approver
                  </Button>
                )}

                {approvalTasks.length > 0 && (
                  <Button
                    variant="contained"
                    color="success"
                    size="small"
                    startIcon={<FactCheckOutlinedIcon />}
                    onClick={() => {
                      setSelectedApprovalTaskId(approvalTasks[0].id);
                      setApprovalWorkspaceOpen(true);
                    }}
                  >
                    Approval Workspace ({approvalTasks[0].status})
                  </Button>
                )}
              </>
            )}

            {/* Void Button: Strictly Document Controller role and document was never effective */}
            {isController && document.recordStatus === "Active" && !document.currentEffectiveRevisionId && (
              <Button
                variant="outlined"
                color="error"
                size="small"
                startIcon={<BlockOutlinedIcon />}
                onClick={() => setVoidDialogOpen(true)}
              >
                Void Document
              </Button>
            )}
          </Box>
        </Box>

        {/* Future Effective Activation Pending Banner (DC-URS-177) */}
        {currentRevision?.revisionStatus === "FutureEffective" && (
          <Alert
            severity="info"
            icon={<EventRepeatIcon />}
            sx={{
              mt: 2,
              bgcolor: (t) => (t.palette.mode === "dark" ? "#102A43" : "#E3F2FD"),
              border: "1px solid",
              borderColor: (t) => (t.palette.mode === "dark" ? "#244E72" : "#90CAF9")
            }}
          >
            <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "primary.main" }}>
              Revision {currentRevision.revisionNumber} Scheduled for Automatic Activation (Future Effective)
            </Typography>
            <Typography variant="caption" sx={{ display: "block" }}>
              This revision has been approved and signed under 21 CFR Part 11. It is scheduled for automated activation on{" "}
              <strong>{currentRevision.effectiveDate ? new Date(currentRevision.effectiveDate).toLocaleDateString() : "the designated target date"}</strong>.
              The current effective revision remains fully active and authoritative until that time.
            </Typography>
          </Alert>
        )}

        {document.recordStatus === "Void" && (
          <Alert severity="error" sx={{ mt: 2 }}>
            <strong>RECORD VOIDED:</strong> This Document Master was voided on {new Date(document.voidedAt!).toLocaleString()} by {document.voidedByUserName}.<br />
            <strong>Mandatory Justification:</strong> <em>"{document.voidReason}"</em>
          </Alert>
        )}
      </Paper>

      {/* Navigation Tabs */}
      <Paper sx={{ mb: 3 }}>
        <Tabs
          value={currentTab}
          onChange={(_, val) => setCurrentTab(val)}
          indicatorColor="primary"
          textColor="primary"
          sx={{ borderBottom: 1, borderColor: "divider", px: 2 }}
        >
          <Tab label="Overview" />
          <Tab label={`Document & Files (${(activePdf ? 1 : 0) + (activeSource ? 1 : 0) + historicalFiles.length})`} />
          <Tab label={`Version History (${document.revisions.length})`} />
          <Tab label={`Assignments (${document.assignments.length})`} />
          <Tab label={`Audit Trail (${auditLogs.length})`} />
          <Tab label={`Technical Review (${reviewTasks.length})`} />
          <Tab label={`Periodic Review (${periodicReviewTasks.length})`} />
        </Tabs>

        {/* Tab 0: Overview */}
        {currentTab === 0 && (
          <Box sx={{ p: 3 }}>
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "2fr 1fr" }, gap: 3 }}>
              {/* Left Column: Metadata Cards */}
              <Box sx={{ display: "flex", flexDirection: "column", gap: 2.5 }}>
                <Box>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.secondary", mb: 1 }}>
                    Document Governance Properties
                  </Typography>
                  <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, bgcolor: "grey.50", p: 2, borderRadius: 1.5 }}>
                    <Box>
                      <Typography variant="caption" color="text.secondary">Document Classification</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{document.documentTypeName} ({document.documentTypeCode})</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" color="text.secondary">Confidentiality Level</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{document.confidentiality}</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" color="text.secondary">Department & Section</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{document.departmentName} — {document.sectionName}</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" color="text.secondary">Category</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{document.category || "—"}</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" color="text.secondary">Review Cycle</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{currentRevision?.reviewCycleMonths || 24} Months</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" color="text.secondary">Record Origin</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{document.recordOrigin}</Typography>
                    </Box>
                  </Box>
                </Box>

                <Box>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.secondary", mb: 1 }}>
                    Keywords & Search Tags
                  </Typography>
                  <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
                    {document.keywords.length > 0 ? (
                      document.keywords.map((kw) => <Chip key={kw} label={kw} size="small" />)
                    ) : (
                      <Typography variant="caption" color="text.secondary">No keywords assigned.</Typography>
                    )}
                  </Box>
                </Box>

                <Box>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.secondary", mb: 1 }}>
                    Life-Cycle Timestamps & Traceability
                  </Typography>
                  <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, bgcolor: "grey.50", p: 2, borderRadius: 1.5 }}>
                    <Box>
                      <Typography variant="caption" color="text.secondary">Created By</Typography>
                      <Typography variant="body2">{document.createdByUserName} ({new Date(document.createdAt).toLocaleString()})</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" color="text.secondary">Last Modified By</Typography>
                      <Typography variant="body2">
                        {document.modifiedByUserName
                          ? `${document.modifiedByUserName} (${new Date(document.modifiedAt!).toLocaleString()})`
                          : "None"}
                      </Typography>
                    </Box>
                  </Box>
                </Box>
              </Box>

              {/* Right Column: Quick File Action Cards */}
              <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
                <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.secondary" }}>
                  Current Controlled Files
                </Typography>

                {activePdf ? (
                  <Paper variant="outlined" sx={{ p: 2, borderColor: "primary.light" }}>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 1 }}>
                      <PictureAsPdfOutlinedIcon color="error" sx={{ fontSize: 32 }} />
                      <Box sx={{ minWidth: 0, flexGrow: 1 }}>
                        <Typography variant="subtitle2" sx={{ fontWeight: 700, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                          {activePdf.fileName}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                          {(activePdf.sizeBytes / 1024).toFixed(1)} KB | Uploaded by {activePdf.uploadedByUserName}
                        </Typography>
                      </Box>
                    </Box>
                    <Box sx={{ display: "flex", gap: 1, mt: 1.5 }}>
                      <Button
                        size="small"
                        variant="contained"
                        fullWidth
                        startIcon={<PictureAsPdfOutlinedIcon />}
                        onClick={() => handleOpenPdfViewer(activePdf.id, activePdf.fileName)}
                      >
                        View PDF
                      </Button>
                      <Button
                        size="small"
                        variant="outlined"
                        onClick={() => handleDownloadFile(activePdf.id, activePdf.fileName)}
                      >
                        <DownloadOutlinedIcon fontSize="small" />
                      </Button>
                    </Box>
                    {effectivePdf && activePdf && activePdf.id !== effectivePdf.id && (
                      <Button
                        size="small"
                        variant="outlined"
                        color="secondary"
                        fullWidth
                        startIcon={<CompareArrowsIcon />}
                        onClick={() => setDualPdfViewerOpen(true)}
                        sx={{ mt: 1 }}
                      >
                        Compare with Effective (Side-by-Side)
                      </Button>
                    )}
                  </Paper>
                ) : (
                  <Alert severity="info">
                    No controlled PDF is currently attached to this revision.
                  </Alert>
                )}

                {activeSource && (
                  <Paper variant="outlined" sx={{ p: 2 }}>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 1 }}>
                      <DescriptionOutlinedIcon color="primary" sx={{ fontSize: 32 }} />
                      <Box sx={{ minWidth: 0, flexGrow: 1 }}>
                        <Typography variant="subtitle2" sx={{ fontWeight: 700, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                          {activeSource.fileName}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                          Editable Source (Word .docx)
                        </Typography>
                      </Box>
                    </Box>
                    <Button
                      size="small"
                      variant="outlined"
                      fullWidth
                      startIcon={<DownloadOutlinedIcon />}
                      onClick={() => handleDownloadFile(activeSource.id, activeSource.fileName)}
                    >
                      Download Source File
                    </Button>
                  </Paper>
                )}
              </Box>
            </Box>
          </Box>
        )}

        {/* Tab 1: Document & Files */}
        {currentTab === 1 && (
          <Box sx={{ p: 3 }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                Controlled Files on Revision {currentRevision?.revisionNumber}
              </Typography>
              {canEditDraft && currentRevision?.revisionStatus === "Draft" && (
                <Button
                  size="small"
                  variant="contained"
                  startIcon={<CloudUploadOutlinedIcon />}
                  onClick={() => setUploadDialogOpen(true)}
                >
                  Upload or Replace File
                </Button>
              )}
            </Box>

            <TableContainer component={Paper} variant="outlined" sx={{ mb: 3 }}>
              <Table size="small">
                <TableHead sx={{ bgcolor: "grey.50" }}>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700 }}>File Role</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>File Name</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Size</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>SHA-256 Checksum</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Status</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Uploaded By</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Date</TableCell>
                    <TableCell sx={{ fontWeight: 700, textAlign: "right" }}>Action</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {currentRevision?.files.map((file) => (
                    <TableRow key={file.id} hover>
                      <TableCell>
                        <Chip
                          label={file.fileRole === "ControlledPdf" ? "Controlled PDF" : "Source DOCX"}
                          size="small"
                          color={file.fileRole === "ControlledPdf" ? "primary" : "default"}
                          variant="outlined"
                        />
                      </TableCell>
                      <TableCell sx={{ fontWeight: 600 }}>{file.fileName}</TableCell>
                      <TableCell>{(file.sizeBytes / 1024).toFixed(1)} KB</TableCell>
                      <TableCell sx={{ fontFamily: "monospace", fontSize: 11 }}>
                        <Tooltip title={file.contentSha256}>
                          <span>{file.contentSha256.substring(0, 16)}...</span>
                        </Tooltip>
                      </TableCell>
                      <TableCell>
                        {file.isActive ? (
                          <Chip label="ACTIVE" size="small" color="success" sx={{ height: 20, fontSize: 10 }} />
                        ) : (
                          <Chip label="SUPERSEDED" size="small" color="default" sx={{ height: 20, fontSize: 10 }} />
                        )}
                      </TableCell>
                      <TableCell>{file.uploadedByUserName}</TableCell>
                      <TableCell>{new Date(file.uploadedAt).toLocaleString()}</TableCell>
                      <TableCell align="right">
                        {file.fileRole === "ControlledPdf" && (
                          <IconButton
                            size="small"
                            color="error"
                            onClick={() => handleOpenPdfViewer(file.id, file.fileName)}
                          >
                            <PictureAsPdfOutlinedIcon fontSize="small" />
                          </IconButton>
                        )}
                        <IconButton
                          size="small"
                          onClick={() => handleDownloadFile(file.id, file.fileName)}
                        >
                          <DownloadOutlinedIcon fontSize="small" />
                        </IconButton>
                      </TableCell>
                    </TableRow>
                  ))}
                  {(!currentRevision?.files || currentRevision.files.length === 0) && (
                    <TableRow>
                      <TableCell colSpan={8} align="center" sx={{ py: 3, color: "text.secondary" }}>
                        No files currently uploaded for this revision.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          </Box>
        )}

        {/* Tab 2: Version History */}
        {currentTab === 2 && (
          <Box sx={{ p: 3 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
              Revision History Register
            </Typography>
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead sx={{ bgcolor: "grey.50" }}>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700 }}>Revision</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Seq</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Type</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Status</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Effective Date</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Next Review</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Reason for Revision</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Files</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Created By</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {document.revisions.map((rev) => (
                    <TableRow key={rev.id} hover>
                      <TableCell sx={{ fontWeight: 700 }}>{rev.revisionNumber}</TableCell>
                      <TableCell>{rev.revisionSequence}</TableCell>
                      <TableCell>{rev.revisionType}</TableCell>
                      <TableCell>
                        <StatusBadge status={rev.revisionStatus} />
                      </TableCell>
                      <TableCell>{rev.effectiveDate ? new Date(rev.effectiveDate).toLocaleDateString() : "—"}</TableCell>
                      <TableCell>{rev.nextReviewDate ? new Date(rev.nextReviewDate).toLocaleDateString() : "—"}</TableCell>
                      <TableCell sx={{ maxWidth: 260 }}>
                        <Typography variant="caption" display="block">
                          {rev.reasonForRevision || "—"}
                        </Typography>
                        {rev.cancelReason && (
                          <Typography variant="caption" color="error">
                            Cancelled: {rev.cancelReason}
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell>{rev.files.length} attached</TableCell>
                      <TableCell>
                        <Typography variant="caption" display="block">{rev.createdByUserName}</Typography>
                        <Typography variant="caption" color="text.secondary">{new Date(rev.createdAt).toLocaleDateString()}</Typography>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </Box>
        )}

        {/* Tab 3: Assignments */}
        {currentTab === 3 && (
          <Box sx={{ p: 3 }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                Document Workflow Role Assignments
              </Typography>
              {canEditDraft && (
                <Button
                  size="small"
                  variant="outlined"
                  startIcon={<PersonAddOutlinedIcon />}
                  onClick={() => setAssignmentOpen(true)}
                >
                  Add Assignment
                </Button>
              )}
            </Box>

            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead sx={{ bgcolor: "grey.50" }}>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700 }}>Role</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Assigned Personnel</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Username</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Assigned At</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Assigned By</TableCell>
                    <TableCell sx={{ fontWeight: 700, textAlign: "right" }}>Actions</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {/* Primary Document Owner */}
                  <TableRow>
                    <TableCell>
                      <Chip label="Document Owner" size="small" color="primary" />
                    </TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{document.documentOwnerUserName}</TableCell>
                    <TableCell>User #{document.documentOwnerUserId}</TableCell>
                    <TableCell>{new Date(document.createdAt).toLocaleString()}</TableCell>
                    <TableCell>{document.createdByUserName}</TableCell>
                    <TableCell align="right">
                      <Typography variant="caption" color="text.secondary">Primary Owner</Typography>
                    </TableCell>
                  </TableRow>

                  {/* Supplemental Assignments */}
                  {document.assignments.map((assign) => (
                    <TableRow key={assign.id} hover>
                      <TableCell>
                        <Chip label={assign.assignmentRole} size="small" variant="outlined" />
                      </TableCell>
                      <TableCell sx={{ fontWeight: 600 }}>{assign.userFullName}</TableCell>
                      <TableCell>{assign.username}</TableCell>
                      <TableCell>{new Date(assign.assignedAt).toLocaleString()}</TableCell>
                      <TableCell>{assign.assignedByUserName}</TableCell>
                      <TableCell align="right">
                        {canEditDraft && (
                          <IconButton
                            size="small"
                            color="error"
                            onClick={() => handleRemoveAssignment(assign.id)}
                          >
                            <DeleteOutlineIcon fontSize="small" />
                          </IconButton>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </Box>
        )}

        {/* Tab 4: Audit Trail */}
        {currentTab === 4 && (
          <Box sx={{ p: 3 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
              Immutable Audit History for Record {document.microLimsDocumentId}
            </Typography>

            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead sx={{ bgcolor: "grey.50" }}>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700 }}>Timestamp (UTC)</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Action Code</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Category</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Actor</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Reason / Justification</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Field Changes</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {auditLogs.map((log) => (
                    <TableRow key={log.id} hover>
                      <TableCell sx={{ whiteSpace: "nowrap" }}>
                        {new Date(log.timestamp).toLocaleString()}
                      </TableCell>
                      <TableCell sx={{ fontWeight: 600 }}>
                        <Box sx={{ display: "flex", flexDirection: "column" }}>
                          <Typography variant="body2" sx={{ fontWeight: 700 }}>
                            {log.actionCode || log.action}
                          </Typography>
                          {log.sourceContext && (
                            <Typography variant="caption" color="text.secondary" sx={{ fontSize: 10 }}>
                              Ctx: {log.sourceContext}
                            </Typography>
                          )}
                        </Box>
                      </TableCell>
                      <TableCell>
                        <Chip
                          label={log.actionCategory}
                          size="small"
                          color={log.actionCategory === "Security" ? "error" : log.actionCategory === "Approval" ? "success" : "default"}
                          variant="outlined"
                          sx={{ height: 20, fontSize: 10 }}
                        />
                      </TableCell>
                      <TableCell>
                        <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                          {log.actorType === "System" ? (
                            <PrecisionManufacturingIcon sx={{ fontSize: 16, color: "text.secondary" }} />
                          ) : (
                            <PersonOutlineIcon sx={{ fontSize: 16, color: "primary.main" }} />
                          )}
                          <Typography variant="body2" sx={{ fontWeight: 500 }}>
                            {log.userName || log.systemProcessName || "System"}
                          </Typography>
                          {log.actorType === "System" && (
                            <Chip label="AUTO" size="small" color="secondary" sx={{ height: 16, fontSize: "0.6rem", fontWeight: 700 }} />
                          )}
                        </Box>
                      </TableCell>
                      <TableCell sx={{ maxWidth: 260 }}>
                        <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                          {log.reason || "—"}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        {log.changes.length > 0 ? (
                          <Box sx={{ display: "flex", flexDirection: "column", gap: 0.25 }}>
                            {log.changes.map((c, i) => (
                              <Typography key={i} variant="caption" sx={{ fontFamily: "monospace" }}>
                                <strong>{c.fieldName}</strong>: {c.previousValue ? `"${c.previousValue}"` : "null"} → "{c.newValue}"
                              </Typography>
                            ))}
                          </Box>
                        ) : (
                          <Typography variant="caption" color="text.secondary">—</Typography>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                  {auditLogs.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={6} align="center" sx={{ py: 3, color: "text.secondary" }}>
                        No audit events recorded for this record.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          </Box>
        )}

        {/* Tab 5: Technical Review */}
        {currentTab === 5 && (
          <Box sx={{ p: 3 }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                Technical Review History & Tasks
              </Typography>
              {currentRevision?.revisionStatus === "Draft" && activePdf && canEditDraft && (
                <Button
                  variant="contained"
                  size="small"
                  startIcon={<RateReviewOutlinedIcon />}
                  onClick={() => setSubmitReviewOpen(true)}
                >
                  Submit for Review
                </Button>
              )}
            </Box>

            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead sx={{ bgcolor: "grey.50" }}>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 600 }}>Task ID</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Revision</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Assigned Reviewer</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Assigned Date</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Due Date</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Findings</TableCell>
                    <TableCell sx={{ fontWeight: 600 }} align="right">Actions</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {reviewTasks.map((t) => (
                    <TableRow key={t.id} hover>
                      <TableCell sx={{ fontWeight: 600 }}>#{t.id}</TableCell>
                      <TableCell>Rev {t.revisionNumber}</TableCell>
                      <TableCell>{t.assignedReviewerFullName} ({t.assignedReviewerUsername})</TableCell>
                      <TableCell>
                        <Chip
                          label={t.status}
                          size="small"
                          color={
                            t.status === "Completed"
                              ? "success"
                              : t.status === "ReturnedForCorrection"
                              ? "warning"
                              : "primary"
                          }
                        />
                      </TableCell>
                      <TableCell>{new Date(t.assignedAt).toLocaleDateString()}</TableCell>
                      <TableCell>{t.dueDate ? new Date(t.dueDate).toLocaleDateString() : "—"}</TableCell>
                      <TableCell>
                        <Box sx={{ display: "flex", gap: 0.5, alignItems: "center" }}>
                          <Chip label={`${t.totalFindingsCount} Total`} size="small" variant="outlined" />
                          {t.openMandatoryFindingsCount > 0 && (
                            <Chip
                              label={`${t.openMandatoryFindingsCount} Open Mandatory`}
                              size="small"
                              color="error"
                            />
                          )}
                        </Box>
                      </TableCell>
                      <TableCell align="right">
                        <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 1 }}>
                          {effectivePdf && activePdf && activePdf.id !== effectivePdf.id && (
                            <Button
                              size="small"
                              variant="outlined"
                              color="secondary"
                              startIcon={<CompareArrowsIcon />}
                              onClick={() => setDualPdfViewerOpen(true)}
                            >
                              Dual PDF
                            </Button>
                          )}
                          <Button
                            size="small"
                            variant="outlined"
                            startIcon={<RateReviewOutlinedIcon />}
                            onClick={() => {
                              setSelectedReviewTask(t);
                              setReviewDrawerOpen(true);
                            }}
                          >
                            Open Review
                          </Button>
                        </Box>
                      </TableCell>
                    </TableRow>
                  ))}
                  {reviewTasks.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={8} align="center" sx={{ py: 3, color: "text.secondary" }}>
                        No technical review tasks initiated for this revision.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          </Box>
        )}

        {/* Tab 6: Periodic Review */}
        {currentTab === 6 && (
          <Box sx={{ p: 3 }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
              <Box>
                <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                  Independent Periodic Review History
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Complete independent review records preserved across all revisions of this document master
                </Typography>
              </Box>
            </Box>

            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead sx={{ bgcolor: "grey.50" }}>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 600 }}>Task ID</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Revision</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Due Date</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Outcome</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Assigned / Completed By</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Findings</TableCell>
                    <TableCell sx={{ fontWeight: 600 }} align="right">Actions</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {periodicReviewTasks.map((t) => (
                    <TableRow key={t.id} hover>
                      <TableCell sx={{ fontWeight: 700 }}>#{t.id}</TableCell>
                      <TableCell sx={{ fontWeight: 600 }}>Rev {t.revisionNumber}</TableCell>
                      <TableCell>
                        <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                          <span>{new Date(t.scheduledDueDate).toLocaleDateString()}</span>
                          {t.isOverdue && <Chip label="OVERDUE" color="error" size="small" sx={{ height: 16, fontSize: "0.6rem", fontWeight: 700 }} />}
                        </Box>
                      </TableCell>
                      <TableCell>
                        <Chip
                          label={t.status}
                          size="small"
                          color={t.status === "Completed" ? "success" : t.status === "InProgress" ? "info" : "default"}
                        />
                      </TableCell>
                      <TableCell>
                        {t.outcome ? (
                          <Chip
                            label={t.outcome}
                            size="small"
                            color={t.outcome === "RemainsValid" ? "success" : t.outcome === "RevisionRequired" ? "warning" : "default"}
                          />
                        ) : "—"}
                      </TableCell>
                      <TableCell>
                        {t.completedByFullName || t.assignedReviewerFullName || "—"}
                      </TableCell>
                      <TableCell>{t.totalFindingsCount} notes</TableCell>
                      <TableCell align="right">
                        <Button
                          size="small"
                          variant="outlined"
                          onClick={() => {
                            setSelectedPeriodicReviewTaskId(t.id);
                            setPeriodicReviewDialogOpen(true);
                          }}
                        >
                          Workspace
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                  {periodicReviewTasks.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={8} align="center" sx={{ py: 3, color: "text.secondary" }}>
                        No periodic review tasks recorded for this document master.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          </Box>
        )}
      </Paper>

      {/* Periodic Review Workspace Dialog */}
      <PeriodicReviewWorkspaceDialog
        open={periodicReviewDialogOpen}
        taskId={selectedPeriodicReviewTaskId}
        onClose={() => setPeriodicReviewDialogOpen(false)}
        onSuccess={() => fetchDocument()}
        onTriggerCreateRevision={() => {
          setCreateRevisionOpen(true);
        }}
      />

      {/* Controlled PDF Viewer Modal */}
      <ControlledPdfViewer
        open={pdfViewerOpen}
        onClose={() => setPdfViewerOpen(false)}
        fileId={activePdfFileId}
        fileName={activePdfFileName}
        companyDocumentCode={document.companyDocumentCode}
        microLimsDocumentId={document.microLimsDocumentId}
        revisionNumber={currentRevision?.revisionNumber || "01"}
        title={document.title}
        revisionStatus={currentRevision?.revisionStatus || "Draft"}
      />

      {/* File Upload Modal */}
      {currentRevision && (
        <FileUploadDialog
          open={uploadDialogOpen}
          onClose={() => setUploadDialogOpen(false)}
          onSuccess={() => fetchDocument()}
          revisionId={currentRevision.id}
          revisionNumber={currentRevision.revisionNumber}
          companyDocumentCode={document.companyDocumentCode}
          hasExistingActiveFile={!!activePdf}
        />
      )}

      {/* Edit Draft Metadata Modal */}
      <DraftMetadataDialog
        open={editMetadataOpen}
        onClose={() => setEditMetadataOpen(false)}
        onSuccess={(updated) => setDocument(updated)}
        document={document}
      />

      {/* Void Dialog */}
      <VoidDocumentDialog
        open={voidDialogOpen}
        onClose={() => setVoidDialogOpen(false)}
        onSuccess={() => fetchDocument()}
        documentMasterId={document.id}
        companyDocumentCode={document.companyDocumentCode}
        documentTitle={document.title}
      />

      {/* Cancel Draft Revision Dialog */}
      {currentRevision && (
        <CancelDraftDialog
          open={cancelDraftOpen}
          onClose={() => setCancelDraftOpen(false)}
          onSuccess={() => fetchDocument()}
          revisionId={currentRevision.id}
          revisionNumber={currentRevision.revisionNumber}
          companyDocumentCode={document.companyDocumentCode}
        />
      )}

      {/* Assignment Modal */}
      <AssignmentDialog
        open={assignmentOpen}
        onClose={() => setAssignmentOpen(false)}
        onSuccess={() => fetchDocument()}
        documentMasterId={document.id}
        companyDocumentCode={document.companyDocumentCode}
      />

      {/* Submit for Review Dialog */}
      {currentRevision && (
        <SubmitForReviewDialog
          open={submitReviewOpen}
          onClose={() => setSubmitReviewOpen(false)}
          onSubmitted={() => fetchDocument()}
          revisionId={currentRevision.id}
          authorUserId={document.documentOwnerUserId}
          currentUserId={userId || 0}
        />
      )}

      {/* Technical Review Drawer */}
      <TechnicalReviewDrawer
        open={reviewDrawerOpen}
        onClose={() => setReviewDrawerOpen(false)}
        reviewTask={selectedReviewTask}
        onTaskUpdated={() => fetchDocument()}
        currentUserId={userId || 0}
        onOpenComparison={() => setDualPdfViewerOpen(true)}
        hasEffectiveRevision={!!effectivePdf}
      />

      {/* Side-by-Side Dual PDF Comparison Workspace Modal (DC-URS-067) */}
      <DualPdfComparisonViewer
        open={dualPdfViewerOpen}
        onClose={() => setDualPdfViewerOpen(false)}
        effectiveFileId={effectivePdf?.id || null}
        effectiveRevisionNumber={effectiveRevision?.revisionNumber || "—"}
        effectiveFileName={effectivePdf?.fileName}
        effectiveSha256={effectivePdf?.contentSha256}
        proposedFileId={activePdf?.id || null}
        proposedRevisionNumber={currentRevision?.revisionNumber || "—"}
        proposedFileName={activePdf?.fileName}
        proposedSha256={activePdf?.contentSha256}
        companyDocumentCode={document.companyDocumentCode}
        documentTitle={document.title}
      />

      {/* Create Revision Dialog */}
      <CreateRevisionDialog
        open={createRevisionOpen}
        onClose={() => setCreateRevisionOpen(false)}
        onCreated={() => fetchDocument()}
        masterId={document.id}
        companyDocumentCode={document.companyDocumentCode}
        currentEffectiveRevisionNumber={effectiveRevision?.revisionNumber || "01"}
        isController={isController || isAdmin}
      />

      {/* Revision Impact Assessment Dialog */}
      {currentRevision && (
        <RevisionImpactAssessmentDialog
          open={impactAssessmentOpen}
          onClose={() => setImpactAssessmentOpen(false)}
          revisionId={currentRevision.id}
          revisionNumber={currentRevision.revisionNumber}
          isEditable={currentRevision.revisionStatus === "Draft" && canEditDraft}
          onSaved={() => fetchDocument()}
        />
      )}

      {/* Revision Change Items Dialog */}
      {currentRevision && (
        <RevisionChangeItemsDialog
          open={changeItemsOpen}
          onClose={() => setChangeItemsOpen(false)}
          revisionId={currentRevision.id}
          revisionNumber={currentRevision.revisionNumber}
          isEditable={currentRevision.revisionStatus === "Draft" && canEditDraft}
          onChanged={() => fetchDocument()}
        />
      )}

      {/* Assign Approver Dialog */}
      {assignApproverOpen && currentRevision && (
        <AssignApproverDialog
          open={assignApproverOpen}
          onClose={() => setAssignApproverOpen(false)}
          onAssigned={() => fetchDocument()}
          revisionId={currentRevision.id}
          companyDocumentCode={document.companyDocumentCode}
          revisionNumber={currentRevision.revisionNumber}
          authorUserId={currentRevision.createdByUserId}
          reviewerUserIds={reviewTasks.map((t) => t.assignedReviewerUserId)}
        />
      )}

      {/* Approval Workspace Dialog */}
      {approvalWorkspaceOpen && selectedApprovalTaskId && (
        <ApprovalWorkspaceDialog
          open={approvalWorkspaceOpen}
          onClose={() => {
            setApprovalWorkspaceOpen(false);
            setSelectedApprovalTaskId(null);
          }}
          approvalTaskId={selectedApprovalTaskId}
          currentUserId={userId || 0}
          onDecisionExecuted={() => fetchDocument()}
          onOpenComparison={() => setDualPdfViewerOpen(true)}
          hasEffectiveRevision={!!effectivePdf}
        />
      )}
    </Box>
  );
}
