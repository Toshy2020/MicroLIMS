import React, { useState, useEffect } from "react";
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
  Tooltip,
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
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";
import CompareArrowsIcon from "@mui/icons-material/CompareArrows";
import SecurityIcon from "@mui/icons-material/Security";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { documentApprovalService } from "../services/documentApprovalService";
import { ElectronicSignatureDialog } from "./ElectronicSignatureDialog";
import type {
  ApprovalDossierDto,
  DocumentApprovalDecision,
  DocumentApprovalTaskDto
} from "../types/documentControlTypes";
import { StatusBadge } from "../../../components/StatusBadge";

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
    } catch (err: any) {
      const msg =
        err.response?.data?.message ||
        err.message ||
        "Failed to load approval inspection dossier.";
      setError(msg);
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
      setError("A mandatory justification of at least 10 characters is required for return or decline.");
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
    } catch (err: any) {
      const msg =
        err.response?.data?.message ||
        err.response?.data?.errors?.[0] ||
        err.message ||
        "Failed to execute approval decision.";
      setError(msg);
    } finally {
      setSubmitting(false);
    }
  };

  const handleConfirmSignature = async (password: string) => {
    const updated = await documentApprovalService.signApproval(
      approvalTaskId,
      {
        password,
        decisionNotes: decisionNotes.trim() || undefined,
        effectiveDate: effectiveDateOverride
          ? new Date(effectiveDateOverride).toISOString()
          : undefined
      }
    );

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
              Approval Workspace — {dossier?.master.companyDocumentCode} Rev {dossier?.revision.revisionNumber}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {dossier?.master.title}
            </Typography>
          </Box>
          {dossier && (
            <Box sx={{ display: "flex", gap: 1, alignItems: "center" }}>
              <Typography variant="caption" color="text.secondary">
                Task Status:
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
                    bgcolor: (theme) =>
                      theme.palette.mode === "dark" ? "#142D1B" : "#EDF7ED",
                    border: "1px solid",
                    borderColor: (theme) =>
                      theme.palette.mode === "dark" ? "#2E5A36" : "#B7DFB9"
                  }}
                >
                  <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                    21 CFR Part 11 Electronic Signature Applied
                  </Typography>
                  <Typography variant="caption" sx={{ display: "block" }}>
                    Signed by <strong>{dossier.activeSignature.userFullNameSnapshot}</strong> ({dossier.activeSignature.usernameSnapshot})
                    {" — "}Role: <strong>{dossier.activeSignature.roleSnapshot}</strong>
                    {" — "}Meaning: <strong>{dossier.activeSignature.meaningOfSignature}</strong>
                    {" — "}Timestamp: <strong>{new Date(dossier.activeSignature.signedAt).toUTCString()}</strong>
                  </Typography>
                </Alert>
              </Box>
            ) : (
              <Box sx={{ px: 3, pt: 2 }}>
                <Alert
                  severity="info"
                  icon={<LockIcon fontSize="inherit" />}
                  sx={{
                    bgcolor: (theme) =>
                      theme.palette.mode === "dark" ? "#102A43" : "#F0F4F8",
                    border: "1px solid",
                    borderColor: (theme) =>
                      theme.palette.mode === "dark" ? "#244E72" : "#D9E2EC"
                  }}
                >
                  <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                    Approval Governed by 21 CFR Part 11 Electronic Signature
                  </Typography>
                  <Typography variant="caption">
                    Per FDA 21 CFR Part 11 and cGMP requirements, approving this document revision
                    requires cryptographic re-authentication with your account password.
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
                    bgcolor: (theme) =>
                      theme.palette.mode === "dark" ? "#2E2415" : "#FFF8E1",
                    border: "1px solid",
                    borderColor: (theme) =>
                      theme.palette.mode === "dark" ? "#664D03" : "#FFE082"
                  }}
                >
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "warning.dark" }}>
                    Obsolescence Approval Dossier (DC-URS-052)
                  </Typography>
                  <Typography variant="caption" sx={{ display: "block" }}>
                    This approval task originated from a formal periodic review recommendation for document obsolescence. Approving this task will decommission the document and transition its status upon quality verification.
                  </Typography>
                </Alert>
              </Box>
            )}

            {/* Segregation of Duties Notice (DC-URS-078) */}
            {!dossier.readiness.isSegregationOfDutiesSatisfied && (
              <Box sx={{ px: 3, pt: 2 }}>
                <Alert severity="error" icon={<SecurityIcon fontSize="inherit" />}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                    Segregation of Duties Conflict Detected (DC-URS-078)
                  </Typography>
                  <Typography variant="caption">
                    Current user was identified as either the document author or technical reviewer for this revision. Per 21 CFR Part 11 and cGMP rules, approval decision controls are locked. System Administrators are non-exempt.
                  </Typography>
                </Alert>
              </Box>
            )}

            {/* Navigation Tabs */}
            <Box sx={{ borderBottom: 1, borderColor: "divider", px: 3, mt: 1 }}>
              <Tabs value={activeTab} onChange={(_, val) => setActiveTab(val)}>
                <Tab label="Inspection Dossier" icon={<DescriptionIcon />} iconPosition="start" />
                <Tab
                  label={
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                      <span>Readiness Checklist</span>
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
                <Tab label="Decision Controls" icon={<HistoryEduIcon />} iconPosition="start" />
              </Tabs>
            </Box>

            {/* Tab 0: Dossier Inspection */}
            {activeTab === 0 && (
              <Box sx={{ p: 3, display: "flex", flexDirection: "column", gap: 2.5 }}>
                {/* Revision & Change Metadata */}
                <Paper variant="outlined" sx={{ p: 2 }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                    Revision Metadata & Change Rationale
                  </Typography>
                  <Grid container spacing={2}>
                    <Grid item xs={6}>
                      <Typography variant="caption" color="text.secondary">
                        Document Code / Title:
                      </Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {dossier.master.companyDocumentCode} — {dossier.master.title}
                      </Typography>
                    </Grid>
                    <Grid item xs={3}>
                      <Typography variant="caption" color="text.secondary">
                        Revision Sequence / Number:
                      </Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        Rev {dossier.revision.revisionNumber} (Seq #{dossier.revision.revisionSequence})
                      </Typography>
                    </Grid>
                    <Grid item xs={3}>
                      <Typography variant="caption" color="text.secondary">
                        Classification:
                      </Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {dossier.revision.revisionType}
                      </Typography>
                    </Grid>
                    <Grid item xs={12}>
                      <Typography variant="caption" color="text.secondary">
                        Reason for Revision:
                      </Typography>
                      <Typography variant="body2">{dossier.revision.reasonForRevision}</Typography>
                    </Grid>

                    {dossier.revision.changeReference && (
                      <Grid item xs={6}>
                        <Typography variant="caption" color="text.secondary">
                          Change Reference (CAPA/CR):
                        </Typography>
                        <Typography variant="body2">{dossier.revision.changeReference}</Typography>
                      </Grid>
                    )}
                  </Grid>
                </Paper>

                {/* Controlled PDF File Preview */}
                <Paper variant="outlined" sx={{ p: 2 }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                    Controlled PDF Evidence
                  </Typography>
                  {dossier.controlledPdf ? (
                    <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                      <Box>
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                          {dossier.controlledPdf.fileName}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                          SHA-256: {dossier.controlledPdf.contentSha256} | Size:{" "}
                          {(dossier.controlledPdf.sizeBytes / 1024).toFixed(1)} KB
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
                            {hasEffectiveRevision ? "Compare Dual PDF" : "Inspect Revision PDF"}
                          </Button>
                        )}
                        <Button
                          variant="outlined"
                          size="small"
                          href={`/api/document-control/files/${dossier.controlledPdf.id}/content`}
                          target="_blank"
                        >
                          Inspect PDF
                        </Button>
                      </Box>
                    </Box>
                  ) : (
                    <Alert severity="error">
                      No active Controlled PDF attached to this revision. Approval is blocked.
                    </Alert>
                  )}
                </Paper>

                {/* Electronic Signature Record (if already approved) */}
                {dossier.activeSignature && (
                  <Paper
                    variant="outlined"
                    sx={{
                      p: 2,
                      bgcolor: (theme) =>
                        theme.palette.mode === "dark" ? "#142D1B" : "#F4FAF5",
                      borderColor: (theme) =>
                        theme.palette.mode === "dark" ? "#2E5A36" : "#A3D9A5"
                    }}
                  >
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
                      <VerifiedUserIcon color="success" />
                      <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "success.main" }}>
                        21 CFR Part 11 Electronic Signature Record
                      </Typography>
                    </Box>
                    <Grid container spacing={1.5}>
                      <Grid item xs={6} sm={3}>
                        <Typography variant="caption" color="text.secondary">Signer Name:</Typography>
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>{dossier.activeSignature.userFullNameSnapshot}</Typography>
                      </Grid>
                      <Grid item xs={6} sm={3}>
                        <Typography variant="caption" color="text.secondary">Username:</Typography>
                        <Typography variant="body2">{dossier.activeSignature.usernameSnapshot}</Typography>
                      </Grid>
                      <Grid item xs={6} sm={3}>
                        <Typography variant="caption" color="text.secondary">Signer Role:</Typography>
                        <Typography variant="body2">{dossier.activeSignature.roleSnapshot}</Typography>
                      </Grid>
                      <Grid item xs={6} sm={3}>
                        <Typography variant="caption" color="text.secondary">Meaning of Signature:</Typography>
                        <Typography variant="body2" sx={{ fontWeight: 700, color: "success.main" }}>{dossier.activeSignature.meaningOfSignature}</Typography>
                      </Grid>
                      <Grid item xs={12} sm={6}>
                        <Typography variant="caption" color="text.secondary">Signed Timestamp (UTC):</Typography>
                        <Typography variant="body2">{new Date(dossier.activeSignature.signedAt).toUTCString()}</Typography>
                      </Grid>
                      {dossier.activeSignature.ipAddress && (
                        <Grid item xs={12} sm={6}>
                          <Typography variant="caption" color="text.secondary">Signer IP Address:</Typography>
                          <Typography variant="body2">{dossier.activeSignature.ipAddress}</Typography>
                        </Grid>
                      )}
                      {dossier.activeSignature.comment && (
                        <Grid item xs={12}>
                          <Typography variant="caption" color="text.secondary">Signature Note / Comment:</Typography>
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
                      Structured Section Changes ({dossier.changeItems.length})
                    </Typography>
                  </AccordionSummary>
                  <AccordionDetails sx={{ p: 0 }}>
                    {dossier.changeItems.length === 0 ? (
                      <Box sx={{ p: 2 }}>
                        <Typography variant="caption" color="text.secondary">
                          No specific section change items recorded.
                        </Typography>
                      </Box>
                    ) : (
                      <TableContainer>
                        <Table size="small">
                          <TableHead sx={tableHeadSx(theme)}>
                            <TableRow>
                              <TableCell sx={{ fontWeight: 700 }}>Section</TableCell>
                              <TableCell sx={{ fontWeight: 700 }}>Title</TableCell>
                              <TableCell sx={{ fontWeight: 700 }}>Description</TableCell>
                              <TableCell sx={{ fontWeight: 700 }}>Rationale</TableCell>
                              <TableCell sx={{ fontWeight: 700 }}>Status</TableCell>
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
                      GMP Impact Assessment
                    </Typography>
                  </AccordionSummary>
                  <AccordionDetails>
                    {dossier.impactAssessment ? (
                      <Grid container spacing={1.5}>
                        {[
                          { label: "Procedure / Method", flag: dossier.impactAssessment.procedureOrMethodImpact, details: dossier.impactAssessment.procedureOrMethodDetails },
                          { label: "Training", flag: dossier.impactAssessment.trainingImpact, details: dossier.impactAssessment.trainingDetails },
                          { label: "Forms / Templates", flag: dossier.impactAssessment.formsOrTemplatesImpact, details: dossier.impactAssessment.formsOrTemplatesDetails },
                          { label: "Specifications", flag: dossier.impactAssessment.specificationsImpact, details: dossier.impactAssessment.specificationsDetails },
                          { label: "Equipment", flag: dossier.impactAssessment.equipmentImpact, details: dossier.impactAssessment.equipmentDetails },
                          { label: "Materials / Media", flag: dossier.impactAssessment.materialsOrMediaImpact, details: dossier.impactAssessment.materialsOrMediaDetails },
                          { label: "Validation", flag: dossier.impactAssessment.validationImpact, details: dossier.impactAssessment.validationDetails },
                          { label: "Regulatory Commitment", flag: dossier.impactAssessment.regulatoryCommitmentImpact, details: dossier.impactAssessment.regulatoryCommitmentDetails },
                          { label: "Related Documents", flag: dossier.impactAssessment.relatedDocumentsImpact, details: dossier.impactAssessment.relatedDocumentsDetails }
                        ].map((cat, idx) => (
                          <Grid item xs={12} sm={6} key={idx}>
                            <Box sx={{ display: "flex", alignItems: "flex-start", gap: 1 }}>
                              <Chip
                                size="small"
                                label={cat.flag ? "IMPACT" : "NO IMPACT"}
                                color={cat.flag ? "warning" : "default"}
                                sx={{ minWidth: 80 }}
                              />
                              <Box>
                                <Typography variant="caption" sx={{ fontWeight: 700 }}>
                                  {cat.label}
                                </Typography>
                                {cat.flag && cat.details && (
                                  <Typography variant="body2" color="text.secondary" sx={{ fontSize: 12 }}>
                                    {cat.details}
                                  </Typography>
                                )}
                              </Box>
                            </Box>
                          </Grid>
                        ))}
                      </Grid>
                    ) : (
                      <Typography variant="caption" color="text.secondary">
                        No impact assessment required for initial revision sequence #1.
                      </Typography>
                    )}
                  </AccordionDetails>
                </Accordion>

                {/* Technical Review History */}
                <Accordion defaultExpanded variant="outlined">
                  <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                      Technical Review History & Findings
                    </Typography>
                  </AccordionSummary>
                  <AccordionDetails>
                    {dossier.reviewHistory.map((rt) => (
                      <Box key={rt.id} sx={{ mb: 2, pb: 1, borderBottom: "1px dashed", borderColor: "divider" }}>
                        <Box sx={{ display: "flex", justifyContent: "space-between" }}>
                          <Typography variant="body2" sx={{ fontWeight: 600 }}>
                            Reviewer: {rt.assignedReviewerFullName} ({rt.assignedReviewerUsername})
                          </Typography>
                          <Typography variant="caption" color="text.secondary">
                            Decision: {rt.decision} on {rt.decisionAt ? new Date(rt.decisionAt).toLocaleDateString() : "N/A"}
                          </Typography>
                        </Box>
                        {rt.reviewNotes && (
                          <Typography variant="caption" color="text.secondary">
                            Notes: {rt.reviewNotes}
                          </Typography>
                        )}
                        <Box sx={{ mt: 1 }}>
                          <Typography variant="caption" sx={{ fontWeight: 700 }}>
                            Review Findings: {rt.findings.length} total, {rt.openMandatoryFindingsCount} mandatory unresolved
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
                        Ready for Approval
                      </Typography>
                      <Typography variant="caption">
                        All mandatory technical review prerequisites, impact assessments, change items, and SoD constraints are fully satisfied.
                      </Typography>
                    </Alert>
                  ) : (
                    <Alert severity="error" icon={<CancelIcon fontSize="inherit" />}>
                      <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                        Approval Blocked — Prerequisites Incomplete
                      </Typography>
                      <Typography variant="caption">
                        The following items must be resolved before this revision can be approved:
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
                    Prerequisite Verification Checklist
                  </Typography>

                  <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
                    {[
                      { title: "Active Controlled PDF Attached", satisfied: dossier.readiness.hasActiveControlledPdf },
                      { title: "Technical Review Formally Completed", satisfied: dossier.readiness.isTechnicalReviewCompleted },
                      { title: "Mandatory Review Findings Resolved", satisfied: dossier.readiness.areMandatoryFindingsResolved },
                      { title: "GMP Impact Assessment Completed", satisfied: dossier.readiness.isImpactAssessmentCompleted },
                      { title: "Originating Review Change Items Addressed", satisfied: dossier.readiness.areChangeItemsAddressed },
                      { title: "Segregation of Duties Verified (Author ≠ Reviewer ≠ Approver)", satisfied: dossier.readiness.isSegregationOfDutiesSatisfied }
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
                            <Chip size="small" color="success" icon={<CheckCircleIcon />} label="VERIFIED" />
                          ) : (
                            <Chip size="small" color="error" icon={<CancelIcon />} label="FAILED" />
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
                  Select Approver Decision
                </Typography>
                <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 2.5 }}>
                  Designated approver: {dossier.task.assignedApproverFullName} ({dossier.task.assignedApproverUsername})
                </Typography>

                <Grid container spacing={2} sx={{ mb: 3 }}>
                  <Grid item xs={4}>
                    <Tooltip
                      title={
                        !dossier.readiness.isSegregationOfDutiesSatisfied
                          ? "Segregation of Duties Violation: You cannot approve a revision that you authored or technically reviewed."
                          : !dossier.readiness.isReady
                          ? `Cannot approve: ${dossier.readiness.validationErrors.join("; ")}`
                          : ""
                      }
                    >
                      <span>
                        <Button
                          fullWidth
                          variant={decisionMode === "Approve" ? "contained" : "outlined"}
                          color={dossier.task.submissionNotes?.toLowerCase().includes("obsolescence") ? "warning" : "success"}
                          onClick={() => setDecisionMode("Approve")}
                          disabled={!dossier.readiness.isReady}
                          sx={{ py: 1.5, fontWeight: 700 }}
                        >
                          {dossier.task.submissionNotes?.toLowerCase().includes("obsolescence")
                            ? "Approve Obsolescence"
                            : "Approve Revision"}
                        </Button>
                      </span>
                    </Tooltip>
                  </Grid>
                  <Grid item xs={4}>
                    <Button
                      fullWidth
                      variant={decisionMode === "ReturnForCorrection" ? "contained" : "outlined"}
                      color="warning"
                      onClick={() => setDecisionMode("ReturnForCorrection")}
                      sx={{ py: 1.5, fontWeight: 700 }}
                    >
                      Return for Correction
                    </Button>
                  </Grid>
                  <Grid item xs={4}>
                    <Button
                      fullWidth
                      variant={decisionMode === "Decline" ? "contained" : "outlined"}
                      color="error"
                      onClick={() => setDecisionMode("Decline")}
                      sx={{ py: 1.5, fontWeight: 700 }}
                    >
                      Decline / Cancel
                    </Button>
                  </Grid>
                </Grid>

                {decisionMode === "Approve" && (
                  <Paper variant="outlined" sx={{ p: 2.5, bgcolor: (t) => t.palette.mode === "dark" ? "#1B2A1E" : "#F4FAF5" }}>
                    <Typography variant="subtitle2" color="success.main" sx={{ fontWeight: 700, mb: 1 }}>
                      Approving Revision {dossier.revision.revisionNumber}
                    </Typography>
                    <Typography variant="body2" sx={{ mb: 2 }}>
                      {effectiveDateOverride && new Date(effectiveDateOverride) > new Date()
                        ? `Document will transition to Future Effective status until scheduled activation on ${effectiveDateOverride}. Current effective SOP remains active.`
                        : "Document will transition to Effective immediately. The prior effective revision will be superseded in the same transaction."}
                    </Typography>

                    <TextField
                      label="Effective Date"
                      type="date"
                      size="small"
                      value={effectiveDateOverride}
                      onChange={(e) => setEffectiveDateOverride(e.target.value)}
                      InputLabelProps={{ shrink: true }}
                      helperText="Leave blank or select today for immediate activation; select a future date for deferred activation."
                      fullWidth
                      sx={{ mb: 2 }}
                    />

                    <TextField
                      label="Approval Notes (Optional)"
                      multiline
                      rows={3}
                      size="small"
                      value={decisionNotes}
                      onChange={(e) => setDecisionNotes(e.target.value)}
                      placeholder="Optional remarks or quality rationale..."
                      fullWidth
                    />
                  </Paper>
                )}

                {decisionMode === "ReturnForCorrection" && (
                  <Paper variant="outlined" sx={{ p: 2.5, bgcolor: (t) => t.palette.mode === "dark" ? "#2E2415" : "#FFFBF2" }}>
                    <Typography variant="subtitle2" color="warning.main" sx={{ fontWeight: 700, mb: 1 }}>
                      Return Revision for Author Correction
                    </Typography>
                    <Typography variant="body2" sx={{ mb: 2 }}>
                      The revision status will revert to <strong>Draft</strong>. The author will be notified to revise documentation and address your feedback.
                    </Typography>

                    <TextField
                      label="Mandatory Reason for Return (≥ 10 characters)"
                      multiline
                      rows={3}
                      required
                      size="small"
                      value={decisionNotes}
                      onChange={(e) => setDecisionNotes(e.target.value)}
                      placeholder="Specify the corrections, missing evidence, or changes required..."
                      fullWidth
                      helperText={`${decisionNotes.trim().length} / 10 characters required.`}
                      error={decisionNotes.length > 0 && decisionNotes.trim().length < 10}
                    />
                  </Paper>
                )}

                {decisionMode === "Decline" && (
                  <Paper variant="outlined" sx={{ p: 2.5, bgcolor: (t) => t.palette.mode === "dark" ? "#2F1919" : "#FFF5F5" }}>
                    <Typography variant="subtitle2" color="error.main" sx={{ fontWeight: 700, mb: 1 }}>
                      Decline & Cancel Revision
                    </Typography>
                    <Typography variant="body2" sx={{ mb: 2 }}>
                      The revision status will transition to <strong>Cancelled</strong>. This action is terminal and will be permanently recorded in the regulatory audit trail.
                    </Typography>

                    <TextField
                      label="Mandatory Reason for Rejection (≥ 10 characters)"
                      multiline
                      rows={3}
                      required
                      size="small"
                      value={decisionNotes}
                      onChange={(e) => setDecisionNotes(e.target.value)}
                      placeholder="Specify the reason for declining and cancelling this proposed revision..."
                      fullWidth
                      helperText={`${decisionNotes.trim().length} / 10 characters required.`}
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
          Close
        </Button>
        {activeTab === 2 && decisionMode && (
          <Button
            variant="contained"
            color={decisionMode === "Approve" ? "success" : decisionMode === "ReturnForCorrection" ? "warning" : "error"}
            onClick={handleExecuteDecision}
            disabled={
              submitting ||
              (decisionMode === "Approve" && !dossier?.readiness.isReady) ||
              ((decisionMode === "ReturnForCorrection" || decisionMode === "Decline") &&
                decisionNotes.trim().length < 10)
            }
          >
            {submitting ? "Processing..." : decisionMode === "Approve" ? "Sign & Approve..." : `Confirm ${decisionMode}`}
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
          signerRole="Designated Approver"
          meaning="Approved"
        />
      )}
    </Dialog>
  );
};
