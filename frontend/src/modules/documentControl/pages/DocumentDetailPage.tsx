import { monospaceFontFamily } from "../../../theme/palette";
import { MIN_LABEL_FONT_SIZE, compactChipStrongSx, compactChipSx, documentCodeSx } from "../documentControlStyles";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import { RevisionLifecycleStrip } from "../components/RevisionLifecycleStrip";
import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Box,
  Menu,
  MenuItem,
  ListItemIcon,
  ListItemText,
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
  useTheme
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import CloudUploadOutlinedIcon from "@mui/icons-material/CloudUploadOutlined";
import PictureAsPdfOutlinedIcon from "@mui/icons-material/PictureAsPdfOutlined";
import DownloadOutlinedIcon from "@mui/icons-material/DownloadOutlined";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import PersonAddOutlinedIcon from "@mui/icons-material/PersonAddOutlined";
import RemoveCircleOutlineIcon from "@mui/icons-material/RemoveCircleOutlined";
import RateReviewOutlinedIcon from "@mui/icons-material/RateReviewOutlined";
import AutorenewOutlinedIcon from "@mui/icons-material/AutorenewOutlined";
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import FormatListBulletedOutlinedIcon from "@mui/icons-material/FormatListBulletedOutlined";
import AssignmentTurnedInIcon from "@mui/icons-material/AssignmentTurnedIn";
import CompareArrowsIcon from "@mui/icons-material/CompareArrows";
import EventRepeatIcon from "@mui/icons-material/EventRepeat";
import PrecisionManufacturingIcon from "@mui/icons-material/PrecisionManufacturing";
import PersonOutlineIcon from "@mui/icons-material/PersonOutlined";

import { StatusBadge } from "../../../components/StatusBadge";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { tableHeadSx } from "../../../theme";
import { toast } from "sonner";
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
  const theme = useTheme();

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
  const [assignmentToRemove, setAssignmentToRemove] = useState<number | null>(null);
  const [selectedPeriodicReviewTaskId, setSelectedPeriodicReviewTaskId] = useState<number | null>(null);
  const [actionMenuAnchor, setActionMenuAnchor] = useState<HTMLElement | null>(null);

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
        (r) => r.revisionStatus === "Draft" || r.revisionStatus === "InReview" || r.revisionStatus === "AwaitingApproval"
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
        <Typography
          variant="body2"
          sx={{
            color: "text.secondary",
            mt: 2
          }}>
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
    (r) => r.revisionStatus === "Draft" || r.revisionStatus === "InReview" || r.revisionStatus === "AwaitingApproval"
  );

  const currentRevision = inWorkflowRevision || effectiveRevision || document.revisions[0];

  const hasEffectiveRev = !!effectiveRevision;
  const hasInFlightRev = !!inWorkflowRevision;
  const canCreateRevision = hasEffectiveRev && !hasInFlightRev && document.recordStatus === "Active" && (isAdmin || isController || isOwner || isAssignedAuthor);

  // Authority over this document master, mirroring the server's
  // CanEditDraftMetadataAsync: Administrator, Document Controller, the owner, or
  // an assigned author. Deliberately carries NO revision-status condition,
  // because the server's does not either.
  const canManageDocument =
    (isAdmin || isController || isOwner || isAssignedAuthor) && document.recordStatus === "Active";

  // The subset of that authority which only applies while the revision is still a
  // draft - editing metadata, replacing files, cancelling. Do not use this to gate
  // anything that happens after the draft stage: it is false by definition once
  // the revision moves on, which is what previously made "Assign approver"
  // unreachable (it required both AwaitingApproval and this flag, and those two
  // can never hold at the same time).
  const canEditDraft = canManageDocument && currentRevision?.revisionStatus === "Draft";

  const activePdf = currentRevision?.files.find((f) => f.fileRole === "ControlledPdf" && f.isActive);

  // Effective PDF for side-by-side comparative inspection (DC-URS-067)
  const effectivePdf = effectiveRevision?.files.find((f) => f.fileRole === "ControlledPdf" && f.isActive);

  const isDraft = currentRevision?.revisionStatus === "Draft";
  const isAwaitingApproval = currentRevision?.revisionStatus === "AwaitingApproval";
  const hasPendingApprovalTask = approvalTasks.some((t) => t.status === "Pending");
  const canVoid =
    isController && document.recordStatus === "Active" && !document.currentEffectiveRevisionId;

  /**
   * Everything a person can do to this document right now, in the order they would
   * reach for it. Exactly one entry is marked `lead`: the action the lifecycle is
   * actually waiting on, which gets the prominent button. The rest sit behind
   * "More actions".
   *
   * This replaced a flat toolbar that rendered every permitted action at equal
   * weight - a Draft showed seven buttons side by side, three of them contained,
   * so nothing indicated which one moved the document forward. The conditions
   * below are unchanged from that toolbar; only the presentation differs.
   */
  const actions: {
    key: string;
    label: string;
    icon: React.ReactNode;
    onClick: () => void;
    lead?: boolean;
    danger?: boolean;
  }[] = [
    // Draft: the document is waiting on its author to attach a file, then submit.
    canEditDraft && isDraft && !activePdf && {
      key: "upload",
      label: "Upload controlled file",
      icon: <CloudUploadOutlinedIcon fontSize="small" />,
      onClick: () => setUploadDialogOpen(true),
      lead: true
    },
    canEditDraft && isDraft && activePdf && {
      key: "submit",
      label: "Submit for technical review",
      icon: <RateReviewOutlinedIcon fontSize="small" />,
      onClick: () => setSubmitReviewOpen(true),
      lead: true
    },
    // In review: the reviewer's workspace is the next step.
    reviewTasks.length > 0 && {
      key: "review",
      label: `Open technical review (${reviewTasks[0].status})`,
      icon: <RateReviewOutlinedIcon fontSize="small" />,
      onClick: () => {
        setSelectedReviewTask(reviewTasks[0]);
        setReviewDrawerOpen(true);
      },
      lead: currentRevision?.revisionStatus === "InReview"
    },
    // Awaiting approval: route it to an approver, or open the decision workspace.
    isAwaitingApproval && !hasPendingApprovalTask && canManageDocument && {
      key: "assign-approver",
      label: "Assign approver",
      icon: <AssignmentTurnedInIcon fontSize="small" />,
      onClick: () => setAssignApproverOpen(true),
      lead: true
    },
    isAwaitingApproval && approvalTasks.length > 0 && {
      key: "approval",
      label: `Open approval workspace (${approvalTasks[0].status})`,
      icon: <FactCheckOutlinedIcon fontSize="small" />,
      onClick: () => {
        setSelectedApprovalTaskId(approvalTasks[0].id);
        setApprovalWorkspaceOpen(true);
      },
      lead: true
    },
    // Effective: the only forward move is a new revision.
    canCreateRevision && {
      key: "create-revision",
      label: "Create revision",
      icon: <AutorenewOutlinedIcon fontSize="small" />,
      onClick: () => setCreateRevisionOpen(true),
      lead: true
    },
    // Supporting draft work - available, but never the headline.
    canEditDraft && {
      key: "edit-metadata",
      label: "Edit metadata",
      icon: <EditOutlinedIcon fontSize="small" />,
      onClick: () => setEditMetadataOpen(true)
    },
    canEditDraft && isDraft && activePdf && {
      key: "replace",
      label: "Replace controlled file",
      icon: <CloudUploadOutlinedIcon fontSize="small" />,
      onClick: () => setUploadDialogOpen(true)
    },
    canEditDraft && isDraft && {
      key: "change-items",
      label: "Change items",
      icon: <FormatListBulletedOutlinedIcon fontSize="small" />,
      onClick: () => setChangeItemsOpen(true)
    },
    canEditDraft && isDraft && {
      key: "impact",
      label: "Impact assessment",
      icon: <FactCheckOutlinedIcon fontSize="small" />,
      onClick: () => setImpactAssessmentOpen(true)
    },
    // Destructive, and deliberately last.
    canEditDraft && isDraft && {
      key: "cancel-draft",
      label: "Cancel draft revision",
      icon: <CancelOutlinedIcon fontSize="small" />,
      onClick: () => setCancelDraftOpen(true),
      danger: true
    },
    canVoid && {
      key: "void",
      label: "Void document master",
      icon: <BlockOutlinedIcon fontSize="small" />,
      onClick: () => setVoidDialogOpen(true),
      danger: true
    }
  ].filter(Boolean) as {
    key: string;
    label: string;
    icon: React.ReactNode;
    onClick: () => void;
    lead?: boolean;
    danger?: boolean;
  }[];

  const leadAction = actions.find((a) => a.lead);
  const menuActions = actions.filter((a) => a !== leadAction);

  const handleOpenPdfViewer = (fileId: number, fileName: string) => {
    setActivePdfFileId(fileId);
    setActivePdfFileName(fileName);
    setPdfViewerOpen(true);
  };

  const handleDownloadFile = async (fileId: number, fileName: string) => {
    try {
      await documentControlService.downloadFile(fileId, fileName);
    } catch (err: any) {
      toast.error("Download failed: " + (err.response?.data?.message || err.message));
    }
  };

  const handleRemoveAssignment = (assignmentId: number) => {
    setAssignmentToRemove(assignmentId);
  };

  const confirmRemoveAssignment = async () => {
    if (!assignmentToRemove) return;
    try {
      await documentControlService.removeAssignment(document.id, assignmentToRemove);
      toast.success("Role assignment removed");
      fetchDocument();
    } catch (err: any) {
      toast.error("Failed to remove assignment: " + (err.response?.data?.message || err.message));
    } finally {
      setAssignmentToRemove(null);
    }
  };

  return (
    <Box sx={{ pb: 4 }}>
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
                MicroLIMS ID: <strong style={{ fontFamily: monospaceFontFamily }}>{document.microLimsDocumentId}</strong>
              </Typography>
              <Typography variant="caption">
                Department: <strong>{document.departmentName}</strong> ({document.sectionName})
              </Typography>
              <Typography variant="caption">
                Current Revision: <strong>{currentRevision?.revisionNumber || "01"}</strong> ({currentRevision?.revisionStatus})
              </Typography>
              <Typography variant="caption">
                Owner: <strong>{document.documentOwnerUserName || document.documentOwnerName || "System Owner"}</strong>
              </Typography>
            </Box>
          </Box>

          {/* One prominent action - whatever the lifecycle is waiting on - with
              the remaining permitted actions behind a menu. */}
          <Box sx={{ display: "flex", gap: 1, alignItems: "center", flexWrap: "wrap" }}>
            {leadAction && (
              <Button
                variant="contained"
                startIcon={leadAction.icon}
                onClick={leadAction.onClick}
              >
                {leadAction.label}
              </Button>
            )}

            {menuActions.length > 0 && (
              <>
                <Button
                  variant="outlined"
                  endIcon={<MoreVertIcon />}
                  onClick={(e) => setActionMenuAnchor(e.currentTarget)}
                  aria-haspopup="menu"
                  aria-expanded={Boolean(actionMenuAnchor)}
                >
                  More actions
                </Button>
                <Menu
                  anchorEl={actionMenuAnchor}
                  open={Boolean(actionMenuAnchor)}
                  onClose={() => setActionMenuAnchor(null)}
                  anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
                  transformOrigin={{ vertical: "top", horizontal: "right" }}
                >
                  {menuActions.map((a) => (
                    <MenuItem
                      key={a.key}
                      onClick={() => {
                        setActionMenuAnchor(null);
                        a.onClick();
                      }}
                      sx={a.danger ? { color: "error.main" } : undefined}
                    >
                      <ListItemIcon sx={a.danger ? { color: "error.main" } : undefined}>
                        {a.icon}
                      </ListItemIcon>
                      <ListItemText>{a.label}</ListItemText>
                    </MenuItem>
                  ))}
                </Menu>
              </>
            )}

            {actions.length === 0 && (
              <Typography
                variant="body2"
                sx={{
                  color: "text.secondary",
                  maxWidth: "48ch"
                }}>
                {isAwaitingApproval
                  ? hasPendingApprovalTask
                    ? "Waiting on the assigned approver. Only they can record the approval decision."
                    : "Waiting for an approver to be assigned. The document owner, an assigned author or the Document Controller can route it for approval."
                  : currentRevision?.revisionStatus === "InReview"
                    ? "Waiting on the assigned technical reviewer."
                    : "No actions are available to you on this document in its current state."}
              </Typography>
            )}
          </Box>
        </Box>

        {/* Lifecycle position, directly under the record header so the controlled
            state is the first thing read rather than one chip among several. */}
        <Box sx={{ mt: 2.5 }}>
          <RevisionLifecycleStrip
            status={currentRevision?.revisionStatus ?? null}
            revisionNumber={currentRevision?.revisionNumber}
          />
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
            <Typography variant="subtitle2" sx={{ fontWeight: 700, color: (t) => t.palette.mode === "dark" ? "info.light" : "primary.main" }}>
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
          {/* Four groups, down from seven. "Overview" and "Document & Files" were
              showing the same controlled files twice, so they are one tab now;
              Assignments, Technical Review and Periodic Review are all "who is
              involved and what is in flight", so they are one Workflow tab. The
              audit trail keeps its own place - it is the regulatory record, and a
              reader going to it is doing something different from the others. */}
          <Tab label="Document" />
          <Tab label={`Revisions (${document.revisions.length})`} />
          <Tab
            label={`Workflow (${document.assignments.length + reviewTasks.length + periodicReviewTasks.length})`}
          />
          <Tab label={`Audit trail (${auditLogs.length})`} />
        </Tabs>

        {/* Tab 0: Overview */}
        {currentTab === 0 && (
          <Box sx={{ p: 3 }}>
            <Box sx={{ display: "flex", flexDirection: "column", gap: 3 }}>
              {/* Governance and classification metadata. The controlled files themselves are listed once, in the section below. */}
              <Box sx={{ display: "flex", flexDirection: "column", gap: 2.5 }}>
                <Box>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.secondary", mb: 1 }}>
                    Document Governance Properties
                  </Typography>
                  <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50", p: 2, borderRadius: 1.5 }}>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>Document Classification</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{document.documentTypeName} ({document.documentTypeCode})</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>Confidentiality Level</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{document.confidentiality}</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>Department & Section</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{document.departmentName} — {document.sectionName}</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>Category</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{document.category || "—"}</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>Review Cycle</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{currentRevision?.reviewCycleMonths || 24} Months</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>Record Origin</Typography>
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
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>No keywords assigned.</Typography>
                    )}
                  </Box>
                </Box>

                <Box>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.secondary", mb: 1 }}>
                    Life-Cycle Timestamps & Traceability
                  </Typography>
                  <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50", p: 2, borderRadius: 1.5 }}>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>Created By</Typography>
                      <Typography variant="body2">{document.createdByUserName} ({new Date(document.createdAt).toLocaleString()})</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>Last Modified By</Typography>
                      <Typography variant="body2">
                        {document.modifiedByUserName
                          ? `${document.modifiedByUserName} (${new Date(document.modifiedAt!).toLocaleString()})`
                          : "None"}
                      </Typography>
                    </Box>
                  </Box>
                </Box>
              </Box>

            </Box>
          </Box>
        )}

        {/* Tab 1: Document & Files */}
        {currentTab === 0 && (
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
                <TableHead sx={tableHeadSx(theme)}>
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
                      <TableCell sx={documentCodeSx}>
                        <Tooltip title={file.contentSha256}>
                          <span>{file.contentSha256.substring(0, 16)}...</span>
                        </Tooltip>
                      </TableCell>
                      <TableCell>
                        {file.isActive ? (
                          <Chip label="ACTIVE" size="small" color="success" sx={compactChipSx} />
                        ) : (
                          <Chip label="SUPERSEDED" size="small" color="default" sx={compactChipSx} />
                        )}
                      </TableCell>
                      <TableCell>{file.uploadedByUserName}</TableCell>
                      <TableCell>{new Date(file.uploadedAt).toLocaleString()}</TableCell>
                      <TableCell align="right">
                        {file.fileRole === "ControlledPdf" && (
                          <IconButton aria-label={`View controlled PDF ${file.fileName}`}
                            size="small"
                            color="error"
                            onClick={() => handleOpenPdfViewer(file.id, file.fileName)}
                          >
                            <PictureAsPdfOutlinedIcon fontSize="small" />
                          </IconButton>
                        )}
                        <IconButton aria-label={`Download ${file.fileName}`}
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
        {currentTab === 1 && (
          <Box sx={{ p: 3 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
              Revision History Register
            </Typography>
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
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
                        <Typography variant="caption" sx={{
                          display: "block"
                        }}>
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
                        <Typography variant="caption" sx={{
                          display: "block"
                        }}>{rev.createdByUserName}</Typography>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{new Date(rev.createdAt).toLocaleDateString()}</Typography>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </Box>
        )}

        {/* Tab 3: Assignments */}
        {currentTab === 2 && (
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
                <TableHead sx={tableHeadSx(theme)}>
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
                    <TableCell sx={{ fontWeight: 600 }}>{document.documentOwnerUserName || document.documentOwnerName || "System Owner"}</TableCell>
                    <TableCell>Primary System Owner</TableCell>
                    <TableCell>{new Date(document.createdAt).toLocaleString()}</TableCell>
                    <TableCell>{document.createdByUserName || document.createdByName || "System"}</TableCell>
                    <TableCell align="right">
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>Primary Owner</Typography>
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
                          <Tooltip title="Remove this workflow assignment">
                            <IconButton
                              size="small"
                              color="error"
                              onClick={() => handleRemoveAssignment(assign.id)}
                              aria-label={`Remove ${assign.assignmentRole} assignment for ${assign.userFullName}`}
                            >
                              {/* Deactivates and retains the assignment record -
                                  never a delete (DC-URS-184, FS-1a-170). */}
                              <RemoveCircleOutlineIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
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
        {currentTab === 3 && (
          <Box sx={{ p: 3 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
              Immutable Audit History for Record {document.microLimsDocumentId}
            </Typography>

            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
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
                            <Typography
                              variant="caption"
                              sx={{
                                color: "text.secondary",
                                fontSize: MIN_LABEL_FONT_SIZE
                              }}>
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
                          sx={compactChipSx}
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
                            <Chip label="AUTO" size="small" color="secondary" sx={compactChipStrongSx} />
                          )}
                        </Box>
                      </TableCell>
                      <TableCell sx={{ maxWidth: 260 }}>
                        <Typography
                          variant="caption"
                          sx={{
                            color: "text.secondary",
                            display: "block"
                          }}>
                          {log.reason || "—"}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        {log.changes.length > 0 ? (
                          <Box sx={{ display: "flex", flexDirection: "column", gap: 0.25 }}>
                            {log.changes.map((c, i) => (
                              <Typography key={i} variant="caption" sx={{ fontFamily: monospaceFontFamily }}>
                                <strong>{c.fieldName}</strong>: {c.previousValue ? `"${c.previousValue}"` : "null"} → "{c.newValue}"
                              </Typography>
                            ))}
                          </Box>
                        ) : (
                          <Typography variant="caption" sx={{
                            color: "text.secondary"
                          }}>—</Typography>
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
        {currentTab === 2 && (
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
                <TableHead sx={tableHeadSx(theme)}>
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
        {currentTab === 2 && (
          <Box sx={{ p: 3 }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
              <Box>
                <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                  Independent Periodic Review History
                </Typography>
                <Typography variant="caption" sx={{
                  color: "text.secondary"
                }}>
                  Complete independent review records preserved across all revisions of this document master
                </Typography>
              </Box>
            </Box>

            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
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
                          {t.isOverdue && <Chip label="OVERDUE" color="error" size="small" sx={compactChipStrongSx} />}
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
          authorUserId={currentRevision.createdByUserId}
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
        // Document Controller only - the revision-number override is one of the
        // controls a System Administrator is explicitly not granted (FRS-1A
        // permission matrix; FRS-1B §3.2:184).
        isController={isController}
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

      <ConfirmationDialog
        open={assignmentToRemove !== null}
        title="Remove Role Assignment"
        message="Remove this role assignment from the document? The assignment record is retained and remains visible in the audit trail, as controlled records are never deleted."
        confirmText="Remove Assignment"
        destructive
        onConfirm={confirmRemoveAssignment}
        onCancel={() => setAssignmentToRemove(null)}
      />
    </Box>
  );
}
