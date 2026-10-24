import { useEffect, useMemo, useState } from "react";
import { Box, Typography, Stack, Button, Alert, Paper, Chip, useTheme, alpha } from "@mui/material";
import PrintIcon from "@mui/icons-material/Print";
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import TimelineOutlinedIcon from "@mui/icons-material/TimelineOutlined";
import { Link } from "react-router-dom";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import { ReturnToAnalystDialog } from "../../components/ReturnToAnalystDialog";
import { DialogBodySkeleton } from "../../components/DialogBodySkeleton";
import { StatusBadge } from "../../components/StatusBadge";
import { brandColors } from "../../theme";
import { workflowEventLabel } from "../../utils/workflowEventLabel";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { SampleSummaryService, SampleApprovalDecision } from "./services/SampleSummaryService";
import { SampleSummary, TestOrderSummaryDetail } from "./types/sampleSummaryTypes";
import { PathogenSessionDialog } from "./pathogenSession/PathogenSessionDialog";
import { UserService, UserRecord } from "../users/services/UserService";
import { CloseTestingDialog } from "./dialogs/CloseTestingDialog";
import { DECISION_OPTIONS, formatDate, formatSectionStatus, isTestOrderNonPassing } from "./sampleSummary/summaryFormatting";
import { SampleIdentityCard, SamplePreparationCard } from "./sampleSummary/SummaryCards";
import { TestResultsSection } from "./sampleSummary/TestResultsSection";
import { ApprovalSignaturesCard } from "./sampleSummary/ApprovalSignaturesCard";

interface Props {
  open: boolean;
  sampleId: number | null;
  onClose: () => void;
}

// Workflow History, horizontal - moved from the bottom information area to
// the top of the page, above Approval & Electronic Signatures, so the
// sample's lifecycle is visible at a glance rather than buried below the
// results. Step History (per-test-order step transitions) was removed as a
// separate card - this lifecycle timeline already covers what matters.
function WorkflowHistoryStrip({ summary }: { summary: SampleSummary }) {
  const theme = useTheme();
  return (
    <Paper sx={{ p: 2.5, border: "1px solid", borderColor: "divider", borderRadius: 2, bgcolor: "background.paper" }}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 2 }}>
        <TimelineOutlinedIcon sx={{ color: theme.palette.primary.main, fontSize: 20 }} />
        <Typography sx={{ fontWeight: 700, fontSize: 14, color: theme.palette.primary.main }}>
          Workflow History
        </Typography>
      </Box>

      {summary.timeline.length === 0 ? (
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>No lifecycle events yet.</Typography>
      ) : (
        <Stack
          direction="row"
          spacing={0}
          role="region"
          aria-label="Workflow history events"
          tabIndex={0}
          sx={{
            overflowX: "auto",
            pb: 0.5,
            // Scroll shadows: the "local" covers travel with the content and
            // hide the fixed "scroll" shadows only at an edge with nothing
            // more to show, so a timeline wider than the dialog visibly
            // continues instead of looking cut off at the edge.
            background: `
              linear-gradient(to right, ${theme.palette.background.paper} 30%, transparent) left / 40px 100% no-repeat local,
              linear-gradient(to left, ${theme.palette.background.paper} 30%, transparent) right / 40px 100% no-repeat local,
              radial-gradient(farthest-side at 0 50%, ${alpha(theme.palette.text.primary, 0.18)}, transparent) left / 14px 100% no-repeat scroll,
              radial-gradient(farthest-side at 100% 50%, ${alpha(theme.palette.text.primary, 0.18)}, transparent) right / 14px 100% no-repeat scroll`
          }}
        >
          {summary.timeline.map((e, idx) => (
            <Box key={idx} sx={{ display: "flex", alignItems: "flex-start", flexShrink: 0 }}>
              {idx > 0 && (
                <Box sx={{ width: 32, height: 0, borderTop: "2px solid", borderColor: "divider", mt: 1.6, flexShrink: 0 }} />
              )}
              <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", width: 160, textAlign: "center", px: 1 }}>
                <Box
                  sx={{
                    color: e.decision === "Reject" ? brandColors.err : brandColors.ok,
                    display: "flex",
                    alignItems: "center"
                  }}
                >
                  <CheckCircleOutlineIcon sx={{ fontSize: 18 }} />
                </Box>
                <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.primary", mt: 0.5 }}>
                  {workflowEventLabel(e.eventType, e.decision)}
                </Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  {e.performedByName}
                </Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  {formatDate(e.timestamp)}
                </Typography>
                {e.comment && (
                  <Typography sx={{ fontSize: 12, color: "text.secondary", fontStyle: "italic", mt: 0.25 }}>
                    "{e.comment}"
                  </Typography>
                )}
              </Box>
            </Box>
          ))}
        </Stack>
      )}
    </Paper>
  );
}

export function SampleSummaryDialog({ open, sampleId, onClose }: Props) {
  const theme = useTheme();
  const { permissions } = useAuth();
  const canAssignAnalyst = permissions.includes(PERMISSIONS.SAMPLES_ASSIGN_ANALYST);
  const [summary, setSummary] = useState<SampleSummary | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [comment, setComment] = useState("");
  const [decision, setDecision] = useState<SampleApprovalDecision>("Approve");
  const [certificateRemarks, setCertificateRemarks] = useState("");
  const [selectedTestOrderIds, setSelectedTestOrderIds] = useState<number[]>([]);
  const [analysts, setAnalysts] = useState<UserRecord[]>([]);
  const [analystsLoadFailed, setAnalystsLoadFailed] = useState(false);
  const [newSampleAnalystOneId, setNewSampleAnalystOneId] = useState<number | "">("");
  const [newSampleAnalystTwoId, setNewSampleAnalystTwoId] = useState<number | "">("");
  const [confirmingReview, setConfirmingReview] = useState(false);
  const [confirmingDecision, setConfirmingDecision] = useState(false);
  const [returningTestOrder, setReturningTestOrder] = useState<TestOrderSummaryDetail | null>(null);
  const [openPathogenDialog, setOpenPathogenDialog] = useState(false);
  const [exporting, setExporting] = useState<"pdf" | null>(null);
  const [exportError, setExportError] = useState<string | null>(null);

  const [reviewSectionId, setReviewSectionId] = useState<number | "">("");
  const [approvalSectionId, setApprovalSectionId] = useState<number | "">("");
  const [closingSectionId, setClosingSectionId] = useState<number | "">("");
  const [confirmingClose, setConfirmingClose] = useState(false);

  const reviewableSections = useMemo(
    () => (summary?.sections ?? []).filter((s) => s.canView && s.status === "UnderReview"),
    [summary?.sections]
  );
  const approvableSections = useMemo(
    () => (summary?.sections ?? []).filter((s) => s.canView && s.status === "UnderApproval"),
    [summary?.sections]
  );
  // Available only after another lab has rejected the sample (design.md
  // §5.3) - a lab that itself rejected already shows Status "Rejected",
  // not one of these open statuses, so it's excluded here automatically.
  const closableSections = useMemo(
    () =>
      (summary?.sections ?? []).filter(
        (s) => s.canView && (s.status === "InTesting" || s.status === "UnderReview" || s.status === "UnderApproval")
      ),
    [summary?.sections]
  );
  const rejectingSection = useMemo(
    () => (summary?.sections ?? []).find((s) => s.status === "Rejected"),
    [summary?.sections]
  );

  const effectiveReviewSectionId =
    reviewableSections.length === 1
      ? reviewableSections[0].sectionId
      : reviewSectionId !== ""
      ? Number(reviewSectionId)
      : undefined;

  const effectiveApprovalSectionId =
    approvableSections.length === 1
      ? approvableSections[0].sectionId
      : approvalSectionId !== ""
      ? Number(approvalSectionId)
      : undefined;

  useEffect(() => {
    if (open && sampleId) {
      setSummary(null);
      setLoadError(null);
      setComment("");
      setDecision("Approve");
      setCertificateRemarks("");
      setSelectedTestOrderIds([]);
      setNewSampleAnalystOneId("");
      setNewSampleAnalystTwoId("");
      setReturningTestOrder(null);
      setReviewSectionId("");
      setApprovalSectionId("");
      setClosingSectionId("");
      SampleSummaryService.getSummary(sampleId)
        .then((data) => {
          setSummary(data);
          const rev = (data.sections ?? []).filter((s) => s.canView && s.status === "UnderReview");
          if (rev.length === 1) {
            setReviewSectionId(rev[0].sectionId);
          }
          const app = (data.sections ?? []).filter((s) => s.canView && s.status === "UnderApproval");
          if (app.length === 1) {
            setApprovalSectionId(app[0].sectionId);
            setCertificateRemarks(app[0].certificateRemarks ?? "");
          }
          const closable = (data.sections ?? []).filter(
            (s) => s.canView && (s.status === "InTesting" || s.status === "UnderReview" || s.status === "UnderApproval")
          );
          if (closable.length === 1) {
            setClosingSectionId(closable[0].sectionId);
          }
        })
        .catch((e) => {
          setLoadError(e?.response?.data?.message ?? "Failed to load sample summary.");
        });
      if (canAssignAnalyst) {
        setAnalystsLoadFailed(false);
        UserService.getEligibleAnalysts()
          .then(setAnalysts)
          .catch(() => {
            setAnalysts([]);
            setAnalystsLoadFailed(true);
          });
      }
    }
  }, [open, sampleId, canAssignAnalyst]);

  const handleApprovalSectionChange = (newSectionId: number | "") => {
    setApprovalSectionId(newSectionId);
    if (newSectionId !== "") {
      const sec = approvableSections.find((s) => s.sectionId === newSectionId);
      setCertificateRemarks(sec?.certificateRemarks ?? "");
    } else {
      setCertificateRemarks("");
    }
  };

  // Re-pre-check the retest checklist toward whichever tests are actually
  // non-conforming whenever the decision switches to a retest flavor (or
  // the summary first loads) - the Section Head can still freely adjust it.
  useEffect(() => {
    if (!summary) return;
    if (decision !== "RetestRetainedSample" && decision !== "NewSampleRequest") return;
    const nonPassing = summary.testOrders
      .filter((t) => !t.isSuperseded && (effectiveApprovalSectionId == null || t.sectionId === effectiveApprovalSectionId) && isTestOrderNonPassing(t))
      .map((t) => t.testOrderId);
    setSelectedTestOrderIds(nonPassing);
  }, [decision, summary, effectiveApprovalSectionId]);

  const handleCloseTestingSuccess = async () => {
    setConfirmingClose(false);
    if (!sampleId) return;
    const updated = await SampleSummaryService.getSummary(sampleId);
    setSummary(updated);
  };

  const handleReturnConfirm = async (reason?: string) => {
    if (!sampleId || !returningTestOrder) return;
    await SampleSummaryService.returnTestToAnalyst(sampleId, returningTestOrder.testOrderId, reason);
    setReturningTestOrder(null);
    const updated = await SampleSummaryService.getSummary(sampleId);
    setSummary(updated);
  };

  const handleReviewConfirm = async (password: string) => {
    if (!sampleId) return;
    await SampleSummaryService.completeReview(sampleId, password, comment || undefined, effectiveReviewSectionId);
    setConfirmingReview(false);
    onClose();
  };

  const handleDecisionConfirm = async (password: string) => {
    if (!sampleId) return;
    const isRetestDecision = decision === "RetestRetainedSample" || decision === "NewSampleRequest";
    await SampleSummaryService.decideApproval(
      sampleId, password, decision, comment || undefined,
      decision === "Approve" ? (certificateRemarks || undefined) : undefined,
      isRetestDecision ? selectedTestOrderIds : undefined,
      decision === "NewSampleRequest" && newSampleAnalystOneId !== "" ? newSampleAnalystOneId : undefined,
      decision === "NewSampleRequest" && newSampleAnalystTwoId !== "" ? newSampleAnalystTwoId : undefined,
      effectiveApprovalSectionId
    );
    setConfirmingDecision(false);
    onClose();
  };

  const canReview =
    // Only when one of the viewer's own sections is waiting - the sample can
    // be under review while none of them is.
    reviewableSections.length > 0 &&
    permissions.includes(PERMISSIONS.SAMPLES_REVIEW);
  const canReturn = permissions.includes(PERMISSIONS.SAMPLES_REVIEW);
  const canApprove =
    approvableSections.length > 0 &&
    permissions.includes(PERMISSIONS.SAMPLES_APPROVE);
  // Close testing (design.md §5.3): another lab already rejected the
  // sample and the viewer's own lab still has open work. Permission-gated here
  // in addition to the backend's own enforcement, matching the review/
  // approval buttons above.
  const canCloseTesting =
    summary?.overallStatus === "Rejected" &&
    closableSections.length > 0 &&
    permissions.includes(PERMISSIONS.SAMPLES_APPROVE);
  const effectiveClosingSectionId =
    closableSections.length === 1 ? closableSections[0].sectionId : closingSectionId !== "" ? Number(closingSectionId) : undefined;
  const closingSection = closableSections.find((s) => s.sectionId === effectiveClosingSectionId);

  const handleExport = async (format: "pdf") => {
    if (!sampleId || !summary) return;
    setExporting(format);
    setExportError(null);
    try {
      await SampleSummaryService.exportPdf(sampleId, summary.referenceNumber);
    } catch (e: any) {
      setExportError(e?.response?.data?.message ?? "Export failed.");
    } finally {
      setExporting(null);
    }
  };

  // Compute live overall status across all tests
  const overallStatus = useMemo(() => {
    if (!summary) return "—";
    if (summary.status === "Approved") return "Approved";
    if (summary.status === "Rejected") return "Rejected";

    const allLocations = summary.testOrders.flatMap((t) => t.locations);
    if (allLocations.length > 0) {
      const nonConforming = allLocations.some((l) => l.status && l.status !== "WithinLimits" && l.status !== "Absent");
      if (nonConforming) return "OutOfSpecification";
    }

    const allReadings = summary.testOrders.flatMap((t) => t.countTestReadings);
    if (allReadings.some((r) => r.status === "OutOfSpecification")) return "OutOfSpecification";

    // Biochemical identification's explicit call overrides selective-plating
    // morphology alone when present - same override as ReportDocumentMapper
    // and TestResultCards apply, so this banner can't disagree with either.
    const allBiochemical = summary.testOrders.flatMap((t) => t.biochemicalResults);
    if (allBiochemical.some((b) => b.organismDetected === true)) return "Detected";
    const allPathogens = summary.testOrders.flatMap((t) => t.pathogenObservations);
    const anyConforming = allPathogens.some((p) => p.observation === "GrowthConforming");
    const biochemicalRulesOutAll = allBiochemical.length > 0 && allBiochemical.every((b) => b.organismDetected === false);
    if (anyConforming && !biochemicalRulesOutAll) return "Detected";

    return summary.status || "WithinLimits";
  }, [summary]);

  // COA is only meaningful once the sample has a final disposition
  // (Approved or Rejected - matches SampleCoaPage.tsx's own gate) and
  // there's something to report: either per-location results (Water/EM/
  // After Cleaning) for the matrix layout, or plain single-value results
  // (Product/RM/PM) for the simple layout. For an OOS origin/intermediate
  // sample resolved via retest propagation, "something to report" comes
  // from the pulled-through resolving retest's results (see
  // SampleSummaryService.ResolveEffectiveTestOrdersAsync), not this
  // sample's own now-superseded TestOrders.
  const coaEligible = useMemo(() => {
    if (!summary) return false;
    const hasResults =
      summary.certificate.sample.matrix !== null || summary.certificate.sample.simple !== null;
    if (!hasResults) return false;

    // Single-section sample (sections <= 1): combinedCoaAvailable already
    // covers "no lab still open" (Approved or Rejected sample.Status), the
    // same rule this used to re-derive from status locally.
    if (!summary.sections || summary.sections.length <= 1) {
      return summary.combinedCoaAvailable;
    }

    // Multi-section sample: available if Combined is eligible (every
    // requested lab final - design.md §5.5, D10: a rejected sample
    // qualifies too, not just an all-Approved one) or any single section
    // is itself eligible (2b).
    const isCombinedEligible = Boolean(summary.allSectionsVisible && summary.combinedCoaAvailable);
    const hasEligibleSection = summary.sections.some((s) => s.canView && s.coaAvailable);
    return isCombinedEligible || hasEligibleSection;
  }, [summary]);

  return (
    <>
      <FloatingDialog
        open={open && !confirmingReview && !confirmingDecision}
        title="Sample Summary"
        onClose={onClose}
      >
        {loadError && <Alert severity="error" sx={{ mb: 2 }}>{loadError}</Alert>}
        {!summary && !loadError && <DialogBodySkeleton blocks={6} blockHeight={64} />}
        {summary && (
          <Stack spacing={3} sx={{ minWidth: { md: 840 } }}>
            {/* Header Description & Export Actions */}
            <Box
              sx={{
                display: "flex",
                flexDirection: { xs: "column", sm: "row" },
                justifyContent: "space-between",
                alignItems: { xs: "flex-start", sm: "center" },
                gap: 1.5,
                pb: 0.5
              }}
            >
              <Box>
                <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 0.5 }}>
                  <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>
                    Overall Status:
                  </Typography>
                  <StatusBadge status={summary.overallStatus} />
                </Stack>
                <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                  Complete overview of test execution, incubation stages, results, and approvals.
                </Typography>
              </Box>
              <Stack useFlexGap direction="row" spacing={1} sx={{ flexShrink: 0, flexWrap: "wrap", gap: 1 }}>
                {exportError && <Alert severity="error" sx={{ py: 0, px: 1 }}>{exportError}</Alert>}
                {summary.testOrders.some((t) => t.usesSharedTsb) && (
                <Button
                  variant="contained"
                  size="small"
                  startIcon={<ScienceOutlinedIcon />}
                  onClick={() => setOpenPathogenDialog(true)}
                  sx={{
                    bgcolor: brandColors.sectionTitle,
                    color: "common.white",
                    fontWeight: 700,
                    fontSize: 12,
                    "&:hover": { bgcolor: brandColors.pageTitle }
                  }}
                >
                  Open Pathogen Workflow
                </Button>
                )}
                <Button
                  component={Link}
                  to={`/samples/${sampleId}/report`}
                  target="_blank"
                  rel="noopener"
                  variant="outlined"
                  size="small"
                  startIcon={<PrintIcon />}
                  sx={{ borderColor: "divider", color: "text.secondary" }}
                >
                  Printable Report
                </Button>
                {coaEligible && (
                  <Button
                    component={Link}
                    to={`/samples/${sampleId}/coa`}
                    target="_blank"
                    rel="noopener"
                    variant="outlined"
                    size="small"
                    startIcon={<FactCheckOutlinedIcon />}
                    sx={{ borderColor: "divider", color: "text.secondary" }}
                  >
                    View COA
                  </Button>
                )}
                <Button
                  variant="outlined"
                  size="small"
                  startIcon={<PictureAsPdfIcon />}
                  disabled={!!exporting}
                  onClick={() => handleExport("pdf")}
                  sx={{ borderColor: "divider", color: "text.secondary" }}
                >
                  {exporting === "pdf" ? "Exporting…" : "Export PDF"}
                </Button>
              </Stack>
            </Box>

            {/* Laboratory Sections Chips */}
            {summary.sections && summary.sections.length > 0 && (
              <Box sx={{ display: "flex", flexDirection: "column", gap: 0.75 }}>
                <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1, alignItems: "center" }}>
                  <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>
                    Sections:
                  </Typography>
                  {summary.sections.map((sec) => (
                    <Chip
                      key={sec.sectionId}
                      size="small"
                      label={`${sec.sectionName}: ${formatSectionStatus(sec.status)}`}
                      variant={sec.canView ? "filled" : "outlined"}
                      sx={{
                        fontSize: 12,
                        fontWeight: 600,
                        ...(!sec.canView ? { color: "text.secondary", borderColor: "divider" } : {})
                      }}
                    />
                  ))}
                </Box>
                {/* Closed section detail (design.md §5.3) - who closed it,
                    when, and the required reason (default "Sample rejected
                    by <lab>", but freely editable by the closer). */}
                {summary.sections
                  .filter((sec) => sec.status === "Closed" && sec.canView)
                  .map((sec) => (
                    <Typography key={`closed-${sec.sectionId}`} sx={{ fontSize: 12, color: "text.secondary", pl: 0.5 }}>
                      {sec.sectionName} closed by {sec.closedByName ?? "—"} on {formatDate(sec.closedAt)}
                      {sec.closeReason ? `: ${sec.closeReason}` : ""}
                    </Typography>
                  ))}
              </Box>
            )}

            {/* 1. Sample Identity */}
            <SampleIdentityCard summary={summary} />

            {/* Workflow History - horizontal, right under Sample Identity so
                the sample's lifecycle is visible at a glance, above
                Approval & Signatures */}
            <WorkflowHistoryStrip summary={summary} />

            {/* Preparation (if available) */}
            {summary.preparation && <SamplePreparationCard preparation={summary.preparation} />}

            {/* 2. Test Results */}
            <TestResultsSection
              testOrders={summary.testOrders}
              overallStatus={overallStatus}
              canReturn={canReturn}
              onReturnTest={(order) => setReturningTestOrder(order)}
            />

            {/* 3. Approval & Electronic Signatures */}
            <ApprovalSignaturesCard
              summary={summary}
              canReview={!!canReview}
              canApprove={!!canApprove}
              comment={comment}
              setComment={setComment}
              decision={decision}
              setDecision={setDecision}
              certificateRemarks={certificateRemarks}
              setCertificateRemarks={setCertificateRemarks}
              selectedTestOrderIds={selectedTestOrderIds}
              setSelectedTestOrderIds={setSelectedTestOrderIds}
              analysts={analysts}
              analystsLoadFailed={analystsLoadFailed}
              newSampleAnalystOneId={newSampleAnalystOneId}
              setNewSampleAnalystOneId={setNewSampleAnalystOneId}
              newSampleAnalystTwoId={newSampleAnalystTwoId}
              setNewSampleAnalystTwoId={setNewSampleAnalystTwoId}
              onReviewClick={() => setConfirmingReview(true)}
              onApproveClick={() => setConfirmingDecision(true)}
              reviewableSections={reviewableSections}
              reviewSectionId={reviewSectionId}
              onReviewSectionChange={setReviewSectionId}
              approvableSections={approvableSections}
              approvalSectionId={approvalSectionId}
              onApprovalSectionChange={handleApprovalSectionChange}
              effectiveApprovalSectionId={effectiveApprovalSectionId}
              canCloseTesting={!!canCloseTesting}
              closableSections={closableSections}
              closingSectionId={closingSectionId}
              onClosingSectionChange={setClosingSectionId}
              onCloseTestingClick={() => setConfirmingClose(true)}
            />

            {/* 4. Full-Width Open Printable Report / View COA Buttons */}
            <Box sx={{ pt: 1 }}>
              <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
                <Button
                  component={Link}
                  to={`/samples/${sampleId}/report`}
                  target="_blank"
                  rel="noopener"
                  variant="contained"
                  fullWidth
                  size="large"
                  startIcon={<PrintIcon />}
                  sx={{
                    bgcolor: brandColors.sectionTitle,
                    py: 1.25,
                    fontWeight: 600,
                    fontSize: 14,
                    "&:hover": { bgcolor: brandColors.pageTitle }
                  }}
                >
                  Open Printable Report
                </Button>
                {coaEligible && (
                  <Button
                    component={Link}
                    to={`/samples/${sampleId}/coa`}
                    target="_blank"
                    rel="noopener"
                    variant="outlined"
                    fullWidth
                    size="large"
                    startIcon={<FactCheckOutlinedIcon />}
                    sx={{
                      py: 1.25,
                      fontWeight: 600,
                      fontSize: 14,
                      borderColor: brandColors.sectionTitle,
                      color: brandColors.sectionTitle
                    }}
                  >
                    View Certificate of Analysis
                  </Button>
                )}
              </Stack>
            </Box>

            {/* 5. Controlled Document Footer Information */}
            <Box
              sx={{
                textAlign: "center",
                py: 1,
                borderTop: "1px solid",
                borderColor: "divider",
                color: "text.secondary"
              }}
            >
              <Typography sx={{ fontSize: 12 }}>
                Sample Reference: <strong>{summary.referenceNumber}</strong> · Record Status: <strong>{summary.status}</strong> · MicroLIMS Operational View
              </Typography>
            </Box>
          </Stack>
        )}
      </FloatingDialog>

      {/* Return to Analyst Reason Dialog */}
      {returningTestOrder && (
        <ReturnToAnalystDialog
          open={Boolean(returningTestOrder)}
          testCode={returningTestOrder.testCode}
          testDisplayName={returningTestOrder.testDisplayName}
          onCancel={() => setReturningTestOrder(null)}
          onConfirm={handleReturnConfirm}
        />
      )}

      {/* Signature Confirmation Dialogs */}
      {summary && (
        <SignatureDialog
          open={confirmingReview}
          meaningStatement={
            reviewableSections.find((s) => s.sectionId === effectiveReviewSectionId)
              ? `By submitting, I confirm I have reviewed all test results for ${reviewableSections.find((s) => s.sectionId === effectiveReviewSectionId)?.sectionName}.`
              : "By submitting, I confirm I have reviewed all test results for this sample."
          }
          onCancel={() => setConfirmingReview(false)}
          onConfirm={handleReviewConfirm}
        />
      )}
      {summary && (
        <SignatureDialog
          open={confirmingDecision}
          meaningStatement={DECISION_OPTIONS.find((o) => o.value === decision)?.label ?? ""}
          onCancel={() => setConfirmingDecision(false)}
          onConfirm={handleDecisionConfirm}
        />
      )}

      {/* Close Testing Dialog (design.md §5.3) */}
      <CloseTestingDialog
        open={confirmingClose}
        sampleId={sampleId}
        sectionId={effectiveClosingSectionId ?? null}
        sectionName={closingSection?.sectionName}
        rejectingLabName={rejectingSection?.sectionName}
        onClose={() => setConfirmingClose(false)}
        onSuccess={handleCloseTestingSuccess}
      />

      {/* Pathogen Testing Session Workspace Dialog */}
      <PathogenSessionDialog
        open={openPathogenDialog}
        sampleId={sampleId}
        onClose={() => setOpenPathogenDialog(false)}
        onSessionCompleted={() => {
          setOpenPathogenDialog(false);
          if (sampleId) {
            SampleSummaryService.getSummary(sampleId).then(setSummary);
          }
        }}
      />
    </>
  );
}
