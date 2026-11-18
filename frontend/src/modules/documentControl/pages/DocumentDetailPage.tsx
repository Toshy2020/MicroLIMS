import { monospaceFontFamily } from "../../../theme/palette";
import { MIN_LABEL_FONT_SIZE, compactChipStrongSx, compactChipSx, documentCodeSx } from "../documentControlStyles";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import { RevisionLifecycleStrip } from "../components/RevisionLifecycleStrip";
import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { Ltr } from "../../../components/Ltr";
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
import { RecordChangedBanner } from "../../../components/RecordChangedBanner";
import { noteOwnSave } from "../../../hooks/useRecordChanged";
import { getErrorMessage } from "../../../utils/errorMessage";
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
import { useTrackRecent } from "../../navigation/useTrackRecent";
import { PeriodicReviewWorkspaceDialog } from "../components/PeriodicReviewWorkspaceDialog";
import { periodicReviewService } from "../services/periodicReviewService";
import type {
  DocumentMasterDto,
  DocumentAuditItemDto,
  DocumentReviewTaskDto,
  DocumentApprovalTaskDto,
  PeriodicReviewTaskDto
} from "../types/documentControlTypes";
import { useBreadcrumb } from "../../../components/breadcrumbContext";
import { formatDateTimeWithTimeZone, formatLabCalendarDate } from "../../../utils/formatDate";

export function DocumentDetailPage() {
  const { t } = useTranslation("documentControl");
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { role, userId } = useAuth();
  const theme = useTheme();

  const [document, setDocument] = useState<DocumentMasterDto | null>(null);
  useTrackRecent("document", document?.id);
  const [auditLogs, setAuditLogs] = useState<DocumentAuditItemDto[]>([]);
  const [reviewTasks, setReviewTasks] = useState<DocumentReviewTaskDto[]>([]);
  const [currentTab, setCurrentTab] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  useBreadcrumb(document ? [document.companyDocumentCode, document.title].filter(Boolean).join(" ") : error ? t("detail.unavailable") : null);

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
    } catch (err: unknown) {
      setError(getErrorMessage(err, t("detail.loadError")));
    } finally {
      setLoading(false);
    }
  }, [id, t]);

  useEffect(() => {
    fetchDocument();
  }, [fetchDocument]);

  // Called after this tab saved something on the document: marks the save as our own so the
  // "changed by someone else" banner does not fire for its echo, then refreshes.
  const afterOwnAction = useCallback(() => {
    noteOwnSave("document", id ? Number(id) : null);
    void fetchDocument();
  }, [id, fetchDocument]);

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
          {t("detail.loading")}
        </Typography>
      </Box>
    );
  }

  if (error || !document) {
    return (
      <Box sx={{ p: 4, maxWidth: 800, mx: "auto" }}>
        <Button startIcon={<ArrowBackIcon />} onClick={() => navigate("/document-control/library")} sx={{ mb: 2 }}>
          {t("detail.backToLibrary")}
        </Button>
        <Alert severity="error">{error || t("detail.notFound")}</Alert>
      </Box>
    );
  }

  // Authoritative permissions
  const isController = role === "SectionHead";
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
  const canCreateRevision = hasEffectiveRev && !hasInFlightRev && document.recordStatus === "Active" && (isController || isOwner || isAssignedAuthor);

  // Authority over this document master, mirroring the server's
  // CanEditDraftMetadataAsync: the Document Controller, the owner, or an assigned
  // author - a System Administrator controls the system, not the document.
  // Deliberately carries NO revision-status condition,
  // because the server's does not either.
  const canManageDocument =
    (isController || isOwner || isAssignedAuthor) && document.recordStatus === "Active";

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
      label: t("detail.actions.uploadControlledFile"),
      icon: <CloudUploadOutlinedIcon fontSize="small" />,
      onClick: () => setUploadDialogOpen(true),
      lead: true
    },
    canEditDraft && isDraft && activePdf && {
      key: "submit",
      label: t("detail.actions.submitForTechnicalReview"),
      icon: <RateReviewOutlinedIcon fontSize="small" />,
      onClick: () => setSubmitReviewOpen(true),
      lead: true
    },
    // In review: the reviewer's workspace is the next step.
    reviewTasks.length > 0 && {
      key: "review",
      label: t("detail.actions.openTechnicalReview", { status: reviewTasks[0].status }),
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
      label: t("detail.actions.assignApprover"),
      icon: <AssignmentTurnedInIcon fontSize="small" />,
      onClick: () => setAssignApproverOpen(true),
      lead: true
    },
    isAwaitingApproval && approvalTasks.length > 0 && {
      key: "approval",
      label: t("detail.actions.openApprovalWorkspace", { status: approvalTasks[0].status }),
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
      label: t("detail.actions.createRevision"),
      icon: <AutorenewOutlinedIcon fontSize="small" />,
      onClick: () => setCreateRevisionOpen(true),
      lead: true
    },
    // Supporting draft work - available, but never the headline.
    canEditDraft && {
      key: "edit-metadata",
      label: t("detail.actions.editMetadata"),
      icon: <EditOutlinedIcon fontSize="small" />,
      onClick: () => setEditMetadataOpen(true)
    },
    canEditDraft && isDraft && activePdf && {
      key: "replace",
      label: t("detail.actions.replaceControlledFile"),
      icon: <CloudUploadOutlinedIcon fontSize="small" />,
      onClick: () => setUploadDialogOpen(true)
    },
    canEditDraft && isDraft && {
      key: "change-items",
      label: t("detail.actions.changeItems"),
      icon: <FormatListBulletedOutlinedIcon fontSize="small" />,
      onClick: () => setChangeItemsOpen(true)
    },
    canEditDraft && isDraft && {
      key: "impact",
      label: t("detail.actions.impactAssessment"),
      icon: <FactCheckOutlinedIcon fontSize="small" />,
      onClick: () => setImpactAssessmentOpen(true)
    },
    // Destructive, and deliberately last.
    canEditDraft && isDraft && {
      key: "cancel-draft",
      label: t("detail.actions.cancelDraftRevision"),
      icon: <CancelOutlinedIcon fontSize="small" />,
      onClick: () => setCancelDraftOpen(true),
      danger: true
    },
    canVoid && {
      key: "void",
      label: t("detail.actions.voidDocumentMaster"),
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
    } catch (err: unknown) {
      toast.error(t("detail.files.downloadFailed", { error: getErrorMessage(err, t("common.unknownError")) }));
    }
  };

  const handleRemoveAssignment = (assignmentId: number) => {
    setAssignmentToRemove(assignmentId);
  };

  const confirmRemoveAssignment = async () => {
    if (!assignmentToRemove) return;
    try {
      await documentControlService.removeAssignment(document.id, assignmentToRemove);
      toast.success(t("detail.assignments.removedToast"));
      afterOwnAction();
    } catch (err: unknown) {
      toast.error(t("detail.assignments.removeFailedToast", { error: getErrorMessage(err, t("common.unknownError")) }));
    } finally {
      setAssignmentToRemove(null);
    }
  };

  return (
    <Box sx={{ pb: 4 }}>
      <RecordChangedBanner type="document" id={document.id} dirty={false} onReload={() => void fetchDocument()} />
      {/* Back button & Breadcrumbs */}
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
        <Button
          size="small"
          startIcon={<ArrowBackIcon />}
          onClick={() => navigate("/document-control/library")}
          color="inherit"
        >
          {t("detail.documentLibrary")}
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
                <Chip label={t("detail.migratedRecord")} size="small" color="secondary" variant="outlined" />
              )}
            </Box>

            <Typography variant="h6" sx={{ fontWeight: 600, mb: 0.5 }}>
              {document.title}
            </Typography>

            <Box sx={{ display: "flex", gap: 3, flexWrap: "wrap", color: "text.secondary" }}>
              <Typography variant="caption">
                <Trans
                  i18nKey="detail.microLimsId"
                  ns="documentControl"
                  values={{ id: document.microLimsDocumentId }}
                  components={{ ltr: <strong style={{ fontFamily: monospaceFontFamily }} /> }}
                />
              </Typography>
              <Typography variant="caption">
                <Trans
                  i18nKey="detail.department"
                  ns="documentControl"
                  values={{
                    department: document.departmentName,
                    sections: document.sectionNames && document.sectionNames.length > 0 ? document.sectionNames.join(", ") : document.sectionName
                  }}
                  components={{ strong: <strong /> }}
                />
              </Typography>
              <Typography variant="caption">
                <Trans
                  i18nKey="detail.currentRevision"
                  ns="documentControl"
                  values={{
                    number: currentRevision?.revisionNumber || "01",
                    status: currentRevision?.revisionStatus
                  }}
                  components={{ strong: <strong /> }}
                />
              </Typography>
              <Typography variant="caption">
                <Trans
                  i18nKey="detail.owner"
                  ns="documentControl"
                  values={{
                    owner: document.documentOwnerUserName || document.documentOwnerName || t("common.systemOwner")
                  }}
                  components={{ strong: <strong /> }}
                />
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
                  {t("detail.moreActions")}
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
                    ? t("detail.waiting.approverAssigned")
                    : t("detail.waiting.approverUnassigned")
                  : currentRevision?.revisionStatus === "InReview"
                    ? t("detail.waiting.technicalReviewer")
                    : t("detail.waiting.noActions")}
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
              bgcolor: (t) => t.custom.status.info.bg,
              border: "1px solid",
              borderColor: (t) => t.custom.status.info.border
            }}
          >
            <Typography variant="subtitle2" sx={{ fontWeight: 700, color: (t) => t.palette.mode === "dark" ? "info.light" : "primary.main" }}>
              {t("detail.futureEffective.title", { number: currentRevision.revisionNumber })}
            </Typography>
            <Typography variant="caption" sx={{ display: "block" }}>
              <Trans
                i18nKey="detail.futureEffective.body"
                ns="documentControl"
                values={{
                  date: currentRevision.effectiveDate
                    ? formatLabCalendarDate(currentRevision.effectiveDate)
                    : t("detail.futureEffective.designatedDate")
                }}
                components={{ ltr: <Ltr>{null}</Ltr>, strong: <strong /> }}
              />
            </Typography>
          </Alert>
        )}

        {document.recordStatus === "Void" && (
          <Alert severity="error" sx={{ mt: 2 }}>
            <Trans
              i18nKey="detail.voided.banner"
              ns="documentControl"
              values={{
                date: formatDateTimeWithTimeZone(document.voidedAt!),
                user: document.voidedByUserName
              }}
              components={{ strong: <strong /> }}
            />
            <br />
            <Trans
              i18nKey="detail.voided.justification"
              ns="documentControl"
              values={{ reason: document.voidReason }}
              components={{ strong: <strong />, em: <em /> }}
            />
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
          <Tab label={t("detail.tabs.document")} />
          <Tab label={t("detail.tabs.revisions", { count: document.revisions.length })} />
          <Tab
            label={t("detail.tabs.workflow", { count: document.assignments.length + reviewTasks.length + periodicReviewTasks.length })}
          />
          <Tab label={t("detail.tabs.auditTrail", { count: auditLogs.length })} />
        </Tabs>

        {/* Tab 0: Overview */}
        {currentTab === 0 && (
          <Box sx={{ p: 3 }}>
            <Box sx={{ display: "flex", flexDirection: "column", gap: 3 }}>
              {/* Governance and classification metadata. The controlled files themselves are listed once, in the section below. */}
              <Box sx={{ display: "flex", flexDirection: "column", gap: 2.5 }}>
                <Box>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.secondary", mb: 1 }}>
                    {t("detail.overview.governanceProperties")}
                  </Typography>
                  <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50", p: 2, borderRadius: 1.5 }}>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>{t("detail.overview.documentClassification")}</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{document.documentTypeName} ({document.documentTypeCode})</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>{t("detail.overview.confidentialityLevel")}</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{document.confidentiality}</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>{t("detail.overview.departmentAndSection")}</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{document.departmentName} / {document.sectionNames && document.sectionNames.length > 0 ? document.sectionNames.join(", ") : document.sectionName}</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>{t("detail.overview.category")}</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{document.category || "—"}</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>{t("detail.overview.reviewCycle")}</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{t("common.months", { count: currentRevision?.reviewCycleMonths || 24 })}</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>{t("detail.overview.recordOrigin")}</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{document.recordOrigin}</Typography>
                    </Box>
                  </Box>
                </Box>

                <Box>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.secondary", mb: 1 }}>
                    {t("detail.overview.keywordsTitle")}
                  </Typography>
                  <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
                    {document.keywords.length > 0 ? (
                      document.keywords.map((kw) => <Chip key={kw} label={kw} size="small" />)
                    ) : (
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>{t("detail.overview.noKeywords")}</Typography>
                    )}
                  </Box>
                </Box>

                <Box>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.secondary", mb: 1 }}>
                    {t("detail.overview.lifecycleTitle")}
                  </Typography>
                  <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2, bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50", p: 2, borderRadius: 1.5 }}>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>{t("detail.overview.createdBy")}</Typography>
                      <Typography variant="body2">{document.createdByUserName} ({formatDateTimeWithTimeZone(document.createdAt)})</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>{t("detail.overview.lastModifiedBy")}</Typography>
                      <Typography variant="body2">
                        {document.modifiedByUserName
                          ? `${document.modifiedByUserName} (${formatDateTimeWithTimeZone(document.modifiedAt!)})`
                          : t("common.none")}
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
                {t("detail.files.title", { number: currentRevision?.revisionNumber })}
              </Typography>
              {canEditDraft && currentRevision?.revisionStatus === "Draft" && (
                <Button
                  size="small"
                  variant="contained"
                  startIcon={<CloudUploadOutlinedIcon />}
                  onClick={() => setUploadDialogOpen(true)}
                >
                  {t("detail.files.uploadOrReplace")}
                </Button>
              )}
            </Box>

            <TableContainer component={Paper} variant="outlined" sx={{ mb: 3 }}>
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.files.columns.fileRole")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.files.columns.fileName")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.files.columns.size")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.files.columns.checksum")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.files.columns.status")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.files.columns.uploadedBy")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.files.columns.date")}</TableCell>
                    <TableCell sx={{ fontWeight: 700, textAlign: "right" }}>{t("detail.files.columns.action")}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {currentRevision?.files.map((file) => (
                    <TableRow key={file.id} hover>
                      <TableCell>
                        <Chip
                          label={file.fileRole === "ControlledPdf" ? t("detail.files.controlledPdf") : t("detail.files.sourceDocx")}
                          size="small"
                          color={file.fileRole === "ControlledPdf" ? "primary" : "default"}
                          variant="outlined"
                        />
                      </TableCell>
                      <TableCell sx={{ fontWeight: 600 }}>{file.fileName}</TableCell>
                      <TableCell>{t("detail.files.sizeKb", { size: (file.sizeBytes / 1024).toFixed(1) })}</TableCell>
                      <TableCell sx={documentCodeSx}>
                        <Tooltip title={file.contentSha256}>
                          <span>{file.contentSha256.substring(0, 16)}...</span>
                        </Tooltip>
                      </TableCell>
                      <TableCell>
                        {file.isActive ? (
                          <Chip label={t("common.active")} size="small" color="success" sx={compactChipSx} />
                        ) : (
                          <Chip label={t("common.superseded")} size="small" color="default" sx={compactChipSx} />
                        )}
                      </TableCell>
                      <TableCell>{file.uploadedByUserName}</TableCell>
                      <TableCell>{formatDateTimeWithTimeZone(file.uploadedAt)}</TableCell>
                      <TableCell align="right">
                        {file.fileRole === "ControlledPdf" && (
                          <IconButton aria-label={t("detail.files.viewPdfAria", { name: file.fileName })}
                            size="small"
                            color="error"
                            onClick={() => handleOpenPdfViewer(file.id, file.fileName)}
                          >
                            <PictureAsPdfOutlinedIcon fontSize="small" />
                          </IconButton>
                        )}
                        <IconButton aria-label={t("detail.files.downloadAria", { name: file.fileName })}
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
                        {t("detail.files.empty")}
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
              {t("detail.revisions.title")}
            </Typography>
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.revisions.columns.revision")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.revisions.columns.seq")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.revisions.columns.type")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.revisions.columns.status")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.revisions.columns.effectiveDate")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.revisions.columns.nextReview")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.revisions.columns.reasonForRevision")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.revisions.columns.files")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.revisions.columns.createdBy")}</TableCell>
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
                      <TableCell>{rev.effectiveDate ? formatLabCalendarDate(rev.effectiveDate) : "—"}</TableCell>
                      <TableCell>{rev.nextReviewDate ? formatLabCalendarDate(rev.nextReviewDate) : "—"}</TableCell>
                      <TableCell sx={{ maxWidth: 260 }}>
                        <Typography variant="caption" sx={{
                          display: "block"
                        }}>
                          {rev.reasonForRevision || "—"}
                        </Typography>
                        {rev.cancelReason && (
                          <Typography variant="caption" color="error">
                            {t("detail.revisions.cancelled", { reason: rev.cancelReason })}
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell>{t("detail.revisions.attachedCount", { count: rev.files.length })}</TableCell>
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
                {t("detail.assignments.title")}
              </Typography>
              {canEditDraft && (
                <Button
                  size="small"
                  variant="outlined"
                  startIcon={<PersonAddOutlinedIcon />}
                  onClick={() => setAssignmentOpen(true)}
                >
                  {t("detail.assignments.addAssignment")}
                </Button>
              )}
            </Box>

            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.assignments.columns.role")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.assignments.columns.assignedPersonnel")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.assignments.columns.username")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.assignments.columns.assignedAt")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.assignments.columns.assignedBy")}</TableCell>
                    <TableCell sx={{ fontWeight: 700, textAlign: "right" }}>{t("detail.assignments.columns.actions")}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {/* Primary Document Owner */}
                  <TableRow>
                    <TableCell>
                      <Chip label={t("detail.assignments.documentOwner")} size="small" color="primary" />
                    </TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{document.documentOwnerUserName || document.documentOwnerName || t("common.systemOwner")}</TableCell>
                    <TableCell>{t("detail.assignments.primarySystemOwner")}</TableCell>
                    <TableCell>{formatDateTimeWithTimeZone(document.createdAt)}</TableCell>
                    <TableCell>{document.createdByUserName || document.createdByName || t("common.system")}</TableCell>
                    <TableCell align="right">
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>{t("detail.assignments.primaryOwner")}</Typography>
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
                      <TableCell>{formatDateTimeWithTimeZone(assign.assignedAt)}</TableCell>
                      <TableCell>{assign.assignedByUserName}</TableCell>
                      <TableCell align="right">
                        {canEditDraft && (
                          <Tooltip title={t("detail.assignments.removeTooltip")}>
                            <IconButton
                              size="small"
                              color="error"
                              onClick={() => handleRemoveAssignment(assign.id)}
                              aria-label={t("detail.assignments.removeAria", { role: assign.assignmentRole, name: assign.userFullName })}
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
              {t("detail.audit.title", { id: document.microLimsDocumentId })}
            </Typography>

            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.audit.columns.timestamp")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.audit.columns.actionCode")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.audit.columns.category")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.audit.columns.actor")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.audit.columns.reason")}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{t("detail.audit.columns.fieldChanges")}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {auditLogs.map((log) => (
                    <TableRow key={log.id} hover>
                      <TableCell sx={{ whiteSpace: "nowrap" }}>
                        {formatDateTimeWithTimeZone(log.timestamp)}
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
                              {t("detail.audit.ctx", { context: log.sourceContext })}
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
                            {log.userName || log.systemProcessName || t("common.system")}
                          </Typography>
                          {log.actorType === "System" && (
                            <Chip label={t("detail.audit.auto")} size="small" color="secondary" sx={compactChipStrongSx} />
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
                        {t("detail.audit.empty")}
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
                {t("detail.technicalReview.title")}
              </Typography>
              {currentRevision?.revisionStatus === "Draft" && activePdf && canEditDraft && (
                <Button
                  variant="contained"
                  size="small"
                  startIcon={<RateReviewOutlinedIcon />}
                  onClick={() => setSubmitReviewOpen(true)}
                >
                  {t("detail.technicalReview.submitForReview")}
                </Button>
              )}
            </Box>

            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 600 }}>{t("detail.technicalReview.columns.taskId")}</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{t("detail.technicalReview.columns.revision")}</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{t("detail.technicalReview.columns.assignedReviewer")}</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{t("detail.technicalReview.columns.status")}</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{t("detail.technicalReview.columns.assignedDate")}</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{t("detail.technicalReview.columns.dueDate")}</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{t("detail.technicalReview.columns.findings")}</TableCell>
                    <TableCell sx={{ fontWeight: 600 }} align="right">{t("detail.technicalReview.columns.actions")}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {reviewTasks.map((task) => (
                    <TableRow key={task.id} hover>
                      <TableCell sx={{ fontWeight: 600 }}>#{task.id}</TableCell>
                      <TableCell>{t("common.rev", { number: task.revisionNumber })}</TableCell>
                      <TableCell>{task.assignedReviewerFullName} ({task.assignedReviewerUsername})</TableCell>
                      <TableCell>
                        <Chip
                          label={task.status}
                          size="small"
                          color={
                            task.status === "Completed"
                              ? "success"
                              : task.status === "ReturnedForCorrection"
                              ? "warning"
                              : "primary"
                          }
                        />
                      </TableCell>
                      <TableCell>{new Date(task.assignedAt).toLocaleDateString()}</TableCell>
                      <TableCell>{task.dueDate ? formatLabCalendarDate(task.dueDate) : "—"}</TableCell>
                      <TableCell>
                        <Box sx={{ display: "flex", gap: 0.5, alignItems: "center" }}>
                          <Chip label={t("detail.technicalReview.totalFindings", { count: task.totalFindingsCount })} size="small" variant="outlined" />
                          {task.openMandatoryFindingsCount > 0 && (
                            <Chip
                              label={t("detail.technicalReview.openMandatoryFindings", { count: task.openMandatoryFindingsCount })}
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
                              {t("detail.technicalReview.dualPdf")}
                            </Button>
                          )}
                          <Button
                            size="small"
                            variant="outlined"
                            startIcon={<RateReviewOutlinedIcon />}
                            onClick={() => {
                              setSelectedReviewTask(task);
                              setReviewDrawerOpen(true);
                            }}
                          >
                            {t("detail.technicalReview.openReview")}
                          </Button>
                        </Box>
                      </TableCell>
                    </TableRow>
                  ))}
                  {reviewTasks.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={8} align="center" sx={{ py: 3, color: "text.secondary" }}>
                        {t("detail.technicalReview.empty")}
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
                  {t("detail.periodicReview.title")}
                </Typography>
                <Typography variant="caption" sx={{
                  color: "text.secondary"
                }}>
                  {t("detail.periodicReview.subtitle")}
                </Typography>
              </Box>
            </Box>

            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead sx={tableHeadSx(theme)}>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 600 }}>{t("detail.periodicReview.columns.taskId")}</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{t("detail.periodicReview.columns.revision")}</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{t("detail.periodicReview.columns.dueDate")}</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{t("detail.periodicReview.columns.status")}</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{t("detail.periodicReview.columns.outcome")}</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{t("detail.periodicReview.columns.assignedCompletedBy")}</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{t("detail.periodicReview.columns.findings")}</TableCell>
                    <TableCell sx={{ fontWeight: 600 }} align="right">{t("detail.periodicReview.columns.actions")}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {periodicReviewTasks.map((task) => (
                    <TableRow key={task.id} hover>
                      <TableCell sx={{ fontWeight: 700 }}>#{task.id}</TableCell>
                      <TableCell sx={{ fontWeight: 600 }}>{t("common.rev", { number: task.revisionNumber })}</TableCell>
                      <TableCell>
                        <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                          <span>{formatLabCalendarDate(task.scheduledDueDate)}</span>
                          {task.isOverdue && <Chip label={t("detail.periodicReview.overdue")} color="error" size="small" sx={compactChipStrongSx} />}
                        </Box>
                      </TableCell>
                      <TableCell>
                        <Chip
                          label={task.status}
                          size="small"
                          color={task.status === "Completed" ? "success" : task.status === "InProgress" ? "info" : "default"}
                        />
                      </TableCell>
                      <TableCell>
                        {task.outcome ? (
                          <Chip
                            label={task.outcome}
                            size="small"
                            color={task.outcome === "RemainsValid" ? "success" : task.outcome === "RevisionRequired" ? "warning" : "default"}
                          />
                        ) : "—"}
                      </TableCell>
                      <TableCell>
                        {task.completedByFullName || task.assignedReviewerFullName || "—"}
                      </TableCell>
                      <TableCell>{t("detail.periodicReview.notesCount", { count: task.totalFindingsCount })}</TableCell>
                      <TableCell align="right">
                        <Button
                          size="small"
                          variant="outlined"
                          onClick={() => {
                            setSelectedPeriodicReviewTaskId(task.id);
                            setPeriodicReviewDialogOpen(true);
                          }}
                        >
                          {t("detail.periodicReview.workspace")}
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                  {periodicReviewTasks.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={8} align="center" sx={{ py: 3, color: "text.secondary" }}>
                        {t("detail.periodicReview.empty")}
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
        onSuccess={afterOwnAction}
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
          onSuccess={afterOwnAction}
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
        onSuccess={afterOwnAction}
        documentMasterId={document.id}
        companyDocumentCode={document.companyDocumentCode}
        documentTitle={document.title}
      />

      {/* Cancel Draft Revision Dialog */}
      {currentRevision && (
        <CancelDraftDialog
          open={cancelDraftOpen}
          onClose={() => setCancelDraftOpen(false)}
          onSuccess={afterOwnAction}
          revisionId={currentRevision.id}
          revisionNumber={currentRevision.revisionNumber}
          companyDocumentCode={document.companyDocumentCode}
        />
      )}

      {/* Assignment Modal */}
      <AssignmentDialog
        open={assignmentOpen}
        onClose={() => setAssignmentOpen(false)}
        onSuccess={afterOwnAction}
        documentMasterId={document.id}
        companyDocumentCode={document.companyDocumentCode}
      />

      {/* Submit for Review Dialog */}
      {currentRevision && (
        <SubmitForReviewDialog
          open={submitReviewOpen}
          onClose={() => setSubmitReviewOpen(false)}
          onSubmitted={afterOwnAction}
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
        onTaskUpdated={afterOwnAction}
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
        onCreated={afterOwnAction}
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
          onSaved={afterOwnAction}
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
          onChanged={afterOwnAction}
        />
      )}

      {/* Assign Approver Dialog */}
      {assignApproverOpen && currentRevision && (
        <AssignApproverDialog
          open={assignApproverOpen}
          onClose={() => setAssignApproverOpen(false)}
          onAssigned={afterOwnAction}
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
          onDecisionExecuted={afterOwnAction}
          onOpenComparison={() => setDualPdfViewerOpen(true)}
          hasEffectiveRevision={!!effectivePdf}
        />
      )}

      <ConfirmationDialog
        open={assignmentToRemove !== null}
        title={t("detail.assignments.removeDialogTitle")}
        message={t("detail.assignments.removeDialogMessage")}
        confirmText={t("detail.assignments.removeDialogConfirm")}
        destructive
        onConfirm={confirmRemoveAssignment}
        onCancel={() => setAssignmentToRemove(null)}
      />
    </Box>
  );
}
