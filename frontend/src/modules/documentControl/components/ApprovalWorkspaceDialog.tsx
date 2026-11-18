import React, { useState, useEffect } from "react";
import { Trans } from "react-i18next";
import { i18n, useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Alert,
  Box,
  Typography,
  CircularProgress,
  Tabs,
  Tab,
  Chip,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Grid,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  useTheme
} from "@mui/material";
import { tableHeadSx } from "../../../theme";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CancelIcon from "@mui/icons-material/Cancel";
import LockIcon from "@mui/icons-material/Lock";
import DescriptionIcon from "@mui/icons-material/Description";
import HistoryEduIcon from "@mui/icons-material/HistoryEdu";
import AssignmentTurnedInIcon from "@mui/icons-material/AssignmentTurnedIn";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlined";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";
import CompareArrowsIcon from "@mui/icons-material/CompareArrows";
import SecurityIcon from "@mui/icons-material/Security";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { GatedAction } from "../../../components/GatedAction";
import { useActionAvailability } from "../../../hooks/useActionAvailability";
import { documentApprovalService } from "../services/documentApprovalService";
import { ElectronicSignatureDialog } from "./ElectronicSignatureDialog";
import type {
  ApprovalDossierDto,
  DocumentApprovalDecision,
  DocumentApprovalTaskDto
} from "../types/documentControlTypes";
import { StatusBadge } from "../../../components/StatusBadge";
import { DateField } from "../../../components/DateField";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

interface ApprovalWorkspaceDialogProps {
  open: boolean;
  onClose: () => void;
  approvalTaskId: number;
  currentUserId: number;
  onDecisionExecuted: (updatedTask: DocumentApprovalTaskDto) => void;
  onOpenComparison?: () => void;
  hasEffectiveRevision?: boolean;
}

export const ApprovalWorkspaceDialog: React.FC<ApprovalWorkspaceDialogProps> = ({
  open,
  onClose,
  approvalTaskId,
  currentUserId,
  onDecisionExecuted,
  onOpenComparison,
  hasEffectiveRevision = false
}) => {
  const { t } = useTranslation("documentControl");
  const theme = useTheme();
  const [activeTab, setActiveTab] = useState(0);
  const [dossier, setDossier] = useState<ApprovalDossierDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Decision state
  const [decisionMode, setDecisionMode] = useState<DocumentApprovalDecision | null>(null);
  const [decisionNotes, setDecisionNotes] = useState("");
  const [effectiveDateOverride, setEffectiveDateOverride] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [showSignatureDialog, setShowSignatureDialog] = useState(false);

  // The server's answer about approving this revision (and why not), once the dossier says which revision it is.
  const availability = useActionAvailability("document-revision", open ? (dossier?.revision.id ?? null) : null);
  const refreshAvailability = availability.refresh;

  useEffect(() => {
    if (open && approvalTaskId) {
      loadDossier();
      setDecisionMode(null);
      setDecisionNotes("");
      setEffectiveDateOverride("");
      setShowSignatureDialog(false);
    }
  }, [open, approvalTaskId]);

  const loadDossier = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await documentApprovalService.getApprovalDossier(approvalTaskId);
      setDossier(data);
      if (data.task.targetEffectiveDate) {
        setEffectiveDateOverride(data.task.targetEffectiveDate.split("T")[0]);
      }
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("approval.loadError")));
    } finally {
      setLoading(false);
    }
  };

  const handleExecuteDecision = async () => {
    if (!decisionMode) return;

    if (
      (decisionMode === "ReturnForCorrection" || decisionMode === "Decline") &&
      (!decisionNotes || decisionNotes.trim().length < 10)
    ) {
      setError(t("approval.mandatoryReasonError"));
      return;
    }

    if (decisionMode === "Approve") {
      setShowSignatureDialog(true);
      return;
    }

    try {
      setSubmitting(true);
      setError(null);
      const updated = await documentApprovalService.executeApprovalDecision(
        approvalTaskId,
        {
          decision: decisionMode,
          decisionNotes: decisionNotes.trim() || undefined,
          effectiveDate: effectiveDateOverride
            ? new Date(effectiveDateOverride).toISOString()
            : undefined
        }
      );

      onDecisionExecuted(updated);
      onClose();
    } catch (err: unknown) {
      refreshAvailability();
      setError(serverMessage(err) ?? getErrorMessage(err, t("approval.executeError")));
    } finally {
      setSubmitting(false);
    }
  };

  const handleConfirmSignature = async (password: string) => {
    let updated: DocumentApprovalTaskDto;
    try {
      updated = await documentApprovalService.signApproval(
        approvalTaskId,
        {
          password,
          decisionNotes: decisionNotes.trim() || undefined,
          effectiveDate: effectiveDateOverride
            ? new Date(effectiveDateOverride).toISOString()
            : undefined
        }
      );
    } catch (err) {
      refreshAvailability();
      throw err;
    }

    onDecisionExecuted(updated);
    onClose();
  };

  if (!open) return null;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ pb: 1, borderBottom: "1px solid", borderColor: "divider" }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <Box>
            <Typography variant="h6" sx={{ fontWeight: 700 }}>
              {t("approval.title", { code: dossier?.master.companyDocumentCode, rev: dossier?.revision.revisionNumber })}
            </Typography>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>
              {dossier?.master.title}
            </Typography>
          </Box>
          {dossier && (
            <Box sx={{ display: "flex", gap: 1, alignItems: "center" }}>
              <Typography variant="caption" sx={{
                color: "text.secondary"
              }}>
                {t("approval.taskStatus")}
              </Typography>
              <StatusBadge status={dossier.task.status} />
            </Box>
          )}
        </Box>
      </DialogTitle>

      <DialogContent sx={{ p: 0 }}>
        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 8 }}>
            <CircularProgress />
          </Box>
        ) : error && !dossier ? (
          <Box sx={{ p: 3 }}>
            <Alert severity="error">{error}</Alert>
          </Box>
        ) : dossier ? (
          <Box>
            {error && (
              <Box sx={{ px: 3, pt: 2 }}>
                <Alert severity="error">{error}</Alert>
              </Box>
            )}

            {/* Electronic Signature Notice or Active Signature Attribution */}
            {dossier.activeSignature ? (
              <Box sx={{ px: 3, pt: 2 }}>
                <Alert
                  severity="success"
                  icon={<VerifiedUserIcon fontSize="inherit" />}
                  sx={{
                    bgcolor: (theme) => theme.custom.status.notDetected.bg,
                    border: "1px solid",
                    borderColor: (theme) => theme.custom.status.notDetected.border}}
                >
                  <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                    {t("approval.signedBannerTitle")}
                  </Typography>
                  <Typography variant="caption" sx={{ display: "block" }}>
                    <Trans
                      i18nKey="approval.signedBannerDetails"
                      ns="documentControl"
                      values={{
                        name: dossier.activeSignature.userFullNameSnapshot,
                        username: dossier.activeSignature.usernameSnapshot,
                        role: dossier.activeSignature.roleNameSnapshot ?? dossier.activeSignature.roleSnapshot,
                        meaning: dossier.activeSignature.meaningOfSignature,
                        timestamp: new Date(dossier.activeSignature.signedAt).toUTCString()
                      }}
                      components={{ strong: <strong /> }}
                    />
                  </Typography>
                </Alert>
              </Box>
            ) : (
              <Box sx={{ px: 3, pt: 2 }}>
                <Alert
                  severity="info"
                  icon={<LockIcon fontSize="inherit" />}
                  sx={{
                    bgcolor: (theme) => theme.custom.status.info.bg,
                    border: "1px solid",
                    borderColor: (theme) => theme.custom.status.info.border}}
                >
                  <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                    {t("approval.signatureNoticeTitle")}
                  </Typography>
                  <Typography variant="caption">
                    {t("approval.signatureNoticeBody")}
                  </Typography>
                </Alert>
              </Box>
            )}

            {/* Obsolescence Recommendation Warning (DC-URS-052) */}
            {dossier.task.submissionNotes?.toLowerCase().includes("obsolescence") && (
              <Box sx={{ px: 3, pt: 2 }}>
                <Alert
                  severity="warning"
                  icon={<WarningAmberIcon fontSize="inherit" />}
                  sx={{
                    bgcolor: (theme) => theme.custom.status.inconclusive.bg,
                    border: "1px solid",
                    borderColor: (theme) => theme.custom.status.inconclusive.border}}
                >
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "warning.dark" }}>
                    {t("approval.obsolescenceTitle")}
                  </Typography>
                  <Typography variant="caption" sx={{ display: "block" }}>
                    {t("approval.obsolescenceBody")}
                  </Typography>
                </Alert>
              </Box>
            )}

            {/* Segregation of Duties Notice (DC-URS-078) */}
            {!dossier.readiness.isSegregationOfDutiesSatisfied && (
              <Box sx={{ px: 3, pt: 2 }}>
                <Alert severity="error" icon={<SecurityIcon fontSize="inherit" />}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                    {t("approval.sodTitle")}
                  </Typography>
                  <Typography variant="caption">
                    {t("approval.sodBody")}
                  </Typography>
                </Alert>
              </Box>
            )}

            {/* Navigation Tabs */}
            <Box sx={{ borderBottom: 1, borderColor: "divider", px: 3, mt: 1 }}>
              <Tabs value={activeTab} onChange={(_, val) => setActiveTab(val)}>
                <Tab label={t("approval.tabs.inspectionDossier")} icon={<DescriptionIcon />} iconPosition="start" />
                <Tab
                  label={
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                      <span>{t("approval.tabs.readinessChecklist")}</span>
                      {dossier.readiness.isReady ? (
                        <CheckCircleIcon color="success" sx={{ fontSize: 16 }} />
                      ) : (
                        <ErrorOutlineIcon color="error" sx={{ fontSize: 16 }} />
                      )}
                    </Box>
                  }
                  icon={<AssignmentTurnedInIcon />}
                  iconPosition="start"
                />
                <Tab label={t("approval.tabs.decisionControls")} icon={<HistoryEduIcon />} iconPosition="start" />
              </Tabs>
            </Box>

            {/* Tab 0: Dossier Inspection */}
            {activeTab === 0 && (
              <Box sx={{ p: 3, display: "flex", flexDirection: "column", gap: 2.5 }}>
                {/* Revision & Change Metadata */}
                <Paper variant="outlined" sx={{ p: 2 }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                    {t("approval.dossier.metadataTitle")}
                  </Typography>
                  <Grid container spacing={2}>
                    <Grid size={6}>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>
                        {t("approval.dossier.codeAndTitle")}
                      </Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {dossier.master.companyDocumentCode}: {dossier.master.title}
                      </Typography>
                    </Grid>
                    <Grid size={3}>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>
                        {t("approval.dossier.revSeqNumber")}
                      </Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {t("approval.dossier.revSeqValue", { rev: dossier.revision.revisionNumber, seq: dossier.revision.revisionSequence })}
                      </Typography>
                    </Grid>
                    <Grid size={3}>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>
                        {t("approval.dossier.classification")}
                      </Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {dossier.revision.revisionType}
                      </Typography>
                    </Grid>
                    <Grid size={12}>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>
                        {t("approval.dossier.reasonForRevision")}
                      </Typography>
                      <Typography variant="body2">{dossier.revision.reasonForRevision}</Typography>
                    </Grid>

                    {dossier.revision.changeReference && (
                      <Grid size={6}>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>
                          {t("approval.dossier.changeReference")}
                        </Typography>
                        <Typography variant="body2">{dossier.revision.changeReference}</Typography>
                      </Grid>
                    )}
                  </Grid>
                </Paper>

                {/* Controlled PDF File Preview */}
                <Paper variant="outlined" sx={{ p: 2 }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                    {t("approval.dossier.pdfEvidenceTitle")}
                  </Typography>
                  {dossier.controlledPdf ? (
                    <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                      <Box>
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                          {dossier.controlledPdf.fileName}
                        </Typography>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>
                          {t("approval.dossier.pdfMeta", { sha: dossier.controlledPdf.contentSha256, size: (dossier.controlledPdf.sizeBytes / 1024).toFixed(1) })}
                        </Typography>
                      </Box>
                      <Box sx={{ display: "flex", gap: 1 }}>
                        {onOpenComparison && (
                          <Button
                            variant="outlined"
                            color="secondary"
                            size="small"
                            startIcon={<CompareArrowsIcon />}
                            onClick={onOpenComparison}
                          >
                            {hasEffectiveRevision ? t("approval.dossier.compareDualPdf") : t("approval.dossier.inspectRevisionPdf")}
                          </Button>
                        )}
                        <Button
                          variant="outlined"
                          size="small"
                          href={`/api/document-control/files/${dossier.controlledPdf.id}/content`}
                          target="_blank"
                        >
                          {t("approval.dossier.inspectPdf")}
                        </Button>
                      </Box>
                    </Box>
                  ) : (
                    <Alert severity="error">
                      {t("approval.dossier.noPdfBlocked")}
                    </Alert>
                  )}
                </Paper>

                {/* Electronic Signature Record (if already approved) */}
                {dossier.activeSignature && (
                  <Paper
                    variant="outlined"
                    sx={{
                      p: 2,
                      bgcolor: (theme) => theme.custom.status.notDetected.bg,
                      borderColor: (theme) => theme.custom.status.notDetected.border}}
                  >
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
                      <VerifiedUserIcon color="success" />
                      <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "success.main" }}>
                        {t("approval.dossier.signatureRecordTitle")}
                      </Typography>
                    </Box>
                    <Grid container spacing={1.5}>
                      <Grid
                        size={{
                          xs: 6,
                          sm: 3
                        }}>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{t("approval.dossier.signerName")}</Typography>
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>{dossier.activeSignature.userFullNameSnapshot}</Typography>
                      </Grid>
                      <Grid
                        size={{
                          xs: 6,
                          sm: 3
                        }}>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{t("approval.dossier.username")}</Typography>
                        <Typography variant="body2">{dossier.activeSignature.usernameSnapshot}</Typography>
                      </Grid>
                      <Grid
                        size={{
                          xs: 6,
                          sm: 3
                        }}>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{t("approval.dossier.signerRole")}</Typography>
                        <Typography variant="body2">{dossier.activeSignature.roleNameSnapshot ?? dossier.activeSignature.roleSnapshot}</Typography>
                      </Grid>
                      <Grid
                        size={{
                          xs: 6,
                          sm: 3
                        }}>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{t("approval.dossier.meaningOfSignature")}</Typography>
                        <Typography variant="body2" sx={{ fontWeight: 700, color: "success.main" }}>{dossier.activeSignature.meaningOfSignature}</Typography>
                      </Grid>
                      <Grid
                        size={{
                          xs: 12,
                          sm: 6
                        }}>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{t("approval.dossier.signedTimestamp")}</Typography>
                        <Typography variant="body2">{new Date(dossier.activeSignature.signedAt).toUTCString()}</Typography>
                      </Grid>
                      {dossier.activeSignature.ipAddress && (
                        <Grid
                          size={{
                            xs: 12,
                            sm: 6
                          }}>
                          <Typography variant="caption" sx={{
                            color: "text.secondary"
                          }}>{t("approval.dossier.signerIp")}</Typography>
                          <Typography variant="body2">{dossier.activeSignature.ipAddress}</Typography>
                        </Grid>
                      )}
                      {dossier.activeSignature.comment && (
                        <Grid size={12}>
                          <Typography variant="caption" sx={{
                            color: "text.secondary"
                          }}>{t("approval.dossier.signatureNote")}</Typography>
                          <Typography variant="body2">{dossier.activeSignature.comment}</Typography>
                        </Grid>
                      )}
                    </Grid>
                  </Paper>
                )}

                {/* Section Change Items */}
                <Accordion defaultExpanded variant="outlined">
                  <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                      {t("approval.dossier.sectionChangesTitle", { count: dossier.changeItems.length })}
                    </Typography>
                  </AccordionSummary>
                  <AccordionDetails sx={{ p: 0 }}>
                    {dossier.changeItems.length === 0 ? (
                      <Box sx={{ p: 2 }}>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>
                          {t("approval.dossier.noChangeItems")}
                        </Typography>
                      </Box>
                    ) : (
                      <TableContainer>
                        <Table size="small">
                          <TableHead sx={tableHeadSx(theme)}>
                            <TableRow>
                              <TableCell sx={{ fontWeight: 700 }}>{t("approval.dossier.changeColumns.section")}</TableCell>
                              <TableCell sx={{ fontWeight: 700 }}>{t("approval.dossier.changeColumns.title")}</TableCell>
                              <TableCell sx={{ fontWeight: 700 }}>{t("approval.dossier.changeColumns.description")}</TableCell>
                              <TableCell sx={{ fontWeight: 700 }}>{t("approval.dossier.changeColumns.rationale")}</TableCell>
                              <TableCell sx={{ fontWeight: 700 }}>{t("approval.dossier.changeColumns.status")}</TableCell>
                            </TableRow>
                          </TableHead>
                          <TableBody>
                            {dossier.changeItems.map((item) => (
                              <TableRow key={item.id}>
                                <TableCell sx={{ fontWeight: 600 }}>{item.sectionNumber}</TableCell>
                                <TableCell>{item.sectionTitle}</TableCell>
                                <TableCell>{item.descriptionOfChange}</TableCell>
                                <TableCell>{item.changeRationale}</TableCell>
                                <TableCell>
                                  <Chip
                                    size="small"
                                    label={item.status}
                                    color={item.status === "Addressed" ? "success" : "default"}
                                  />
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      </TableContainer>
                    )}
                  </AccordionDetails>
                </Accordion>

                {/* 9-Category Impact Assessment */}
                <Accordion defaultExpanded variant="outlined">
                  <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                      {t("approval.dossier.impactTitle")}
                    </Typography>
                  </AccordionSummary>
                  <AccordionDetails>
                    {dossier.impactAssessment ? (
                      <Grid container spacing={1.5}>
                        {[
                          { label: t("approval.dossier.impactCategories.procedureOrMethod"), flag: dossier.impactAssessment.procedureOrMethodImpact, details: dossier.impactAssessment.procedureOrMethodDetails },
                          { label: t("approval.dossier.impactCategories.training"), flag: dossier.impactAssessment.trainingImpact, details: dossier.impactAssessment.trainingDetails },
                          { label: t("approval.dossier.impactCategories.formsOrTemplates"), flag: dossier.impactAssessment.formsOrTemplatesImpact, details: dossier.impactAssessment.formsOrTemplatesDetails },
                          { label: t("approval.dossier.impactCategories.specifications"), flag: dossier.impactAssessment.specificationsImpact, details: dossier.impactAssessment.specificationsDetails },
                          { label: t("approval.dossier.impactCategories.equipment"), flag: dossier.impactAssessment.equipmentImpact, details: dossier.impactAssessment.equipmentDetails },
                          { label: t("approval.dossier.impactCategories.materialsOrMedia"), flag: dossier.impactAssessment.materialsOrMediaImpact, details: dossier.impactAssessment.materialsOrMediaDetails },
                          { label: t("approval.dossier.impactCategories.validation"), flag: dossier.impactAssessment.validationImpact, details: dossier.impactAssessment.validationDetails },
                          { label: t("approval.dossier.impactCategories.regulatoryCommitment"), flag: dossier.impactAssessment.regulatoryCommitmentImpact, details: dossier.impactAssessment.regulatoryCommitmentDetails },
                          { label: t("approval.dossier.impactCategories.relatedDocuments"), flag: dossier.impactAssessment.relatedDocumentsImpact, details: dossier.impactAssessment.relatedDocumentsDetails }
                        ].map((cat, idx) => (
                          <Grid
                            key={idx}
                            size={{
                              xs: 12,
                              sm: 6
                            }}>
                            <Box sx={{ display: "flex", alignItems: "flex-start", gap: 1 }}>
                              <Chip
                                size="small"
                                label={cat.flag ? t("approval.dossier.impact") : t("approval.dossier.noImpact")}
                                color={cat.flag ? "warning" : "default"}
                                sx={{ minWidth: 80 }}
                              />
                              <Box>
                                <Typography variant="caption" sx={{ fontWeight: 700 }}>
                                  {cat.label}
                                </Typography>
                                {cat.flag && cat.details && (
                                  <Typography
                                    variant="body2"
                                    sx={{
                                      color: "text.secondary",
                                      fontSize: 12
                                    }}>
                                    {cat.details}
                                  </Typography>
                                )}
                              </Box>
                            </Box>
                          </Grid>
                        ))}
                      </Grid>
                    ) : (
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>
                        {t("approval.dossier.noImpactInitial")}
                      </Typography>
                    )}
                  </AccordionDetails>
                </Accordion>

                {/* Technical Review History */}
                <Accordion defaultExpanded variant="outlined">
                  <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                      {t("approval.dossier.reviewHistoryTitle")}
                    </Typography>
                  </AccordionSummary>
                  <AccordionDetails>
                    {dossier.reviewHistory.map((rt) => (
                      <Box key={rt.id} sx={{ mb: 2, pb: 1, borderBottom: "1px dashed", borderColor: "divider" }}>
                        <Box sx={{ display: "flex", justifyContent: "space-between" }}>
                          <Typography variant="body2" sx={{ fontWeight: 600 }}>
                            {t("approval.dossier.reviewerLine", { name: rt.assignedReviewerFullName, username: rt.assignedReviewerUsername })}
                          </Typography>
                          <Typography variant="caption" sx={{
                            color: "text.secondary"
                          }}>
                            {t("approval.dossier.decisionLine", { decision: rt.decision, date: rt.decisionAt ? formatDateTimeWithTimeZone(rt.decisionAt) : t("common.na") })}
                          </Typography>
                        </Box>
                        {rt.reviewNotes && (
                          <Typography variant="caption" sx={{
                            color: "text.secondary"
                          }}>
                            {t("approval.dossier.notesLine", { notes: rt.reviewNotes })}
                          </Typography>
                        )}
                        <Box sx={{ mt: 1 }}>
                          <Typography variant="caption" sx={{ fontWeight: 700 }}>
                            {t("approval.dossier.findingsSummary", { total: rt.findings.length, openMandatory: rt.openMandatoryFindingsCount })}
                          </Typography>
                        </Box>
                      </Box>
                    ))}
                  </AccordionDetails>
                </Accordion>
              </Box>
            )}

            {/* Tab 1: Readiness Checklist */}
            {activeTab === 1 && (
              <Box sx={{ p: 3 }}>
                <Box sx={{ mb: 3 }}>
                  {dossier.readiness.isReady ? (
                    <Alert severity="success" icon={<CheckCircleIcon fontSize="inherit" />}>
                      <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                        {t("approval.readiness.readyTitle")}
                      </Typography>
                      <Typography variant="caption">
                        {t("approval.readiness.readyBody")}
                      </Typography>
                    </Alert>
                  ) : (
                    <Alert severity="error" icon={<CancelIcon fontSize="inherit" />}>
                      <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                        {t("approval.readiness.blockedTitle")}
                      </Typography>
                      <Typography variant="caption">
                        {t("approval.readiness.blockedBody")}
                      </Typography>
                      <Box component="ul" sx={{ pl: 2, mt: 1, mb: 0 }}>
                        {dossier.readiness.validationErrors.map((err, i) => (
                          <li key={i}>
                            <Typography variant="caption">{err}</Typography>
                          </li>
                        ))}
                      </Box>
                    </Alert>
                  )}
                </Box>

                <Paper variant="outlined" sx={{ p: 2.5 }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 2 }}>
                    {t("approval.readiness.checklistTitle")}
                  </Typography>

                  <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
                    {[
                      { title: t("approval.readiness.checks.pdfAttached"), satisfied: dossier.readiness.hasActiveControlledPdf },
                      { title: t("approval.readiness.checks.reviewCompleted"), satisfied: dossier.readiness.isTechnicalReviewCompleted },
                      { title: t("approval.readiness.checks.findingsResolved"), satisfied: dossier.readiness.areMandatoryFindingsResolved },
                      { title: t("approval.readiness.checks.impactCompleted"), satisfied: dossier.readiness.isImpactAssessmentCompleted },
                      { title: t("approval.readiness.checks.changeItemsAddressed"), satisfied: dossier.readiness.areChangeItemsAddressed },
                      { title: t("approval.readiness.checks.sodVerified"), satisfied: dossier.readiness.isSegregationOfDutiesSatisfied }
                    ].map((chk, i) => (
                      <Box
                        key={i}
                        sx={{
                          display: "flex",
                          alignItems: "center",
                          justifyContent: "space-between",
                          py: 1,
                          borderBottom: "1px solid",
                          borderColor: "divider"
                        }}
                      >
                        <Typography variant="body2">{chk.title}</Typography>
                        <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                          {chk.satisfied ? (
                            <Chip size="small" color="success" icon={<CheckCircleIcon />} label={t("approval.readiness.verified")} />
                          ) : (
                            <Chip size="small" color="error" icon={<CancelIcon />} label={t("approval.readiness.failed")} />
                          )}
                        </Box>
                      </Box>
                    ))}
                  </Box>
                </Paper>
              </Box>
            )}

            {/* Tab 2: Decision Controls */}
            {activeTab === 2 && (
              <Box sx={{ p: 3 }}>
                <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                  {t("approval.decision.selectTitle")}
                </Typography>
                <Typography
                  variant="caption"
                  sx={{
                    color: "text.secondary",
                    display: "block",
                    mb: 2.5
                  }}>
                  {t("approval.decision.designatedApprover", { name: dossier.task.assignedApproverFullName, username: dossier.task.assignedApproverUsername })}
                </Typography>

                <Grid container spacing={2} sx={{ mb: 3 }}>
                  <Grid size={4}>
                    <GatedAction action="document.approve" availability={availability}>
                      <Button
                        fullWidth
                        variant={decisionMode === "Approve" ? "contained" : "outlined"}
                        color={dossier.task.submissionNotes?.toLowerCase().includes("obsolescence") ? "warning" : "success"}
                        onClick={() => setDecisionMode("Approve")}
                        sx={{ py: 1.5, fontWeight: 700 }}
                      >
                        {dossier.task.submissionNotes?.toLowerCase().includes("obsolescence")
                          ? t("approval.decision.approveObsolescence")
                          : t("approval.decision.approveRevision")}
                      </Button>
                    </GatedAction>
                  </Grid>
                  <Grid size={4}>
                    <Button
                      fullWidth
                      variant={decisionMode === "ReturnForCorrection" ? "contained" : "outlined"}
                      color="warning"
                      onClick={() => setDecisionMode("ReturnForCorrection")}
                      sx={{ py: 1.5, fontWeight: 700 }}
                    >
                      {t("approval.decision.returnForCorrection")}
                    </Button>
                  </Grid>
                  <Grid size={4}>
                    <Button
                      fullWidth
                      variant={decisionMode === "Decline" ? "contained" : "outlined"}
                      color="error"
                      onClick={() => setDecisionMode("Decline")}
                      sx={{ py: 1.5, fontWeight: 700 }}
                    >
                      {t("approval.decision.declineCancel")}
                    </Button>
                  </Grid>
                </Grid>

                {decisionMode === "Approve" && (
                  <Paper variant="outlined" sx={{ p: 2.5, bgcolor: (t) => t.custom.status.notDetected.bg}}>
                    <Typography
                      variant="subtitle2"
                      sx={{
                        color: "success.main",
                        fontWeight: 700,
                        mb: 1
                      }}>
                      {t("approval.decision.approvingTitle", { rev: dossier.revision.revisionNumber })}
                    </Typography>
                    <Typography variant="body2" sx={{ mb: 2 }}>
                      {effectiveDateOverride && new Date(effectiveDateOverride) > new Date()
                        ? t("approval.decision.futureEffectiveNote", { date: effectiveDateOverride })
                        : t("approval.decision.immediateEffectiveNote")}
                    </Typography>

                    <DateField
                      label={t("approval.decision.effectiveDateLabel")}
                      value={effectiveDateOverride}
                      onChange={(v) => setEffectiveDateOverride(v)}
                      helperText={t("approval.decision.effectiveDateHelper")}
                      fullWidth
                      sx={{ mb: 2 }}
                    />

                    <TextField
                      label={t("approval.decision.approvalNotesLabel")}
                      multiline
                      rows={3}
                      size="small"
                      value={decisionNotes}
                      onChange={(e) => setDecisionNotes(e.target.value)}
                      placeholder={t("approval.decision.approvalNotesPlaceholder")}
                      fullWidth
                    />
                  </Paper>
                )}

                {decisionMode === "ReturnForCorrection" && (
                  <Paper variant="outlined" sx={{ p: 2.5, bgcolor: (t) => t.custom.status.inconclusive.bg}}>
                    <Typography
                      variant="subtitle2"
                      sx={{
                        color: "warning.main",
                        fontWeight: 700,
                        mb: 1
                      }}>
                      {t("approval.decision.returnTitle")}
                    </Typography>
                    <Typography variant="body2" sx={{ mb: 2 }}>
                      <Trans
                        i18nKey="approval.decision.returnBody"
                        ns="documentControl"
                        components={{ strong: <strong /> }}
                      />
                    </Typography>

                    <TextField
                      label={t("approval.decision.returnReasonLabel")}
                      multiline
                      rows={3}
                      required
                      size="small"
                      value={decisionNotes}
                      onChange={(e) => setDecisionNotes(e.target.value)}
                      placeholder={t("approval.decision.returnReasonPlaceholder")}
                      fullWidth
                      helperText={t("approval.decision.charsRequired", { count: decisionNotes.trim().length })}
                      error={decisionNotes.length > 0 && decisionNotes.trim().length < 10}
                    />
                  </Paper>
                )}

                {decisionMode === "Decline" && (
                  <Paper variant="outlined" sx={{ p: 2.5, bgcolor: (t) => t.custom.status.detected.bg}}>
                    <Typography
                      variant="subtitle2"
                      sx={{
                        color: "error.main",
                        fontWeight: 700,
                        mb: 1
                      }}>
                      {t("approval.decision.declineTitle")}
                    </Typography>
                    <Typography variant="body2" sx={{ mb: 2 }}>
                      <Trans
                        i18nKey="approval.decision.declineBody"
                        ns="documentControl"
                        components={{ strong: <strong /> }}
                      />
                    </Typography>

                    <TextField
                      label={t("approval.decision.declineReasonLabel")}
                      multiline
                      rows={3}
                      required
                      size="small"
                      value={decisionNotes}
                      onChange={(e) => setDecisionNotes(e.target.value)}
                      placeholder={t("approval.decision.declineReasonPlaceholder")}
                      fullWidth
                      helperText={t("approval.decision.charsRequired", { count: decisionNotes.trim().length })}
                      error={decisionNotes.length > 0 && decisionNotes.trim().length < 10}
                    />
                  </Paper>
                )}
              </Box>
            )}
          </Box>
        ) : null}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2, borderTop: "1px solid", borderColor: "divider" }}>
        <Button onClick={onClose} disabled={submitting}>
          {t("approval.decision.close")}
        </Button>
        {activeTab === 2 && decisionMode && (
          <Button
            variant="contained"
            color={decisionMode === "Approve" ? "success" : decisionMode === "ReturnForCorrection" ? "warning" : "error"}
            onClick={handleExecuteDecision}
            disabled={
              submitting ||
              ((decisionMode === "ReturnForCorrection" || decisionMode === "Decline") &&
                decisionNotes.trim().length < 10)
            }
          >
            {submitting ? t("approval.decision.processing") : decisionMode === "Approve" ? t("approval.decision.signAndApprove") : t("approval.decision.confirmDecision", { decision: decisionMode })}
          </Button>
        )}
      </DialogActions>

      {/* 21 CFR Part 11 Electronic Signature Ceremony Modal */}
      {dossier && (
        <ElectronicSignatureDialog
          open={showSignatureDialog}
          onClose={() => setShowSignatureDialog(false)}
          onConfirm={handleConfirmSignature}
          documentCode={dossier.master.companyDocumentCode}
          documentTitle={dossier.master.title}
          revisionNumber={dossier.revision.revisionNumber}
          signerFullName={dossier.task.assignedApproverFullName}
          signerUsername={dossier.task.assignedApproverUsername}
          signerRole={t("approval.designatedApproverRole")}
          meaning={i18n.getFixedT("en", "documentControl")("approval.signatureMeaning")}
        />
      )}
    </Dialog>
  );
};
