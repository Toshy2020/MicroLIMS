import { useMemo } from "react";
import { Box, Typography, Stack, TextField, Button, Alert, Radio, RadioGroup, FormControlLabel, Paper, Chip, useTheme, Checkbox, FormGroup, Select, MenuItem, InputLabel, FormControl } from "@mui/material";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import VerifiedUserOutlinedIcon from "@mui/icons-material/VerifiedUserOutlined";
import { Trans } from "react-i18next";
import { GatedAction } from "../../../components/GatedAction";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import type { ActionAvailability } from "../../../hooks/useActionAvailability";
import { StatusBadge } from "../../../components/StatusBadge";
import { SampleApprovalDecision } from "../services/SampleSummaryService";
import { sectionScope } from "../coaAggregation";
import { SampleSummary, SampleSectionSummaryDetail } from "../types/sampleSummaryTypes";
import { isRawMaterialPhyschemReview, isRetentionValid, RETENTION_UNITS } from "../utils/moistureRetentionHelpers";
import { UserRecord } from "../../users/services/UserService";
import { DECISION_OPTIONS, SIGNATURE_STATEMENTS, formatExactTime, isTestOrderNonPassing } from "./summaryFormatting";
import { OosBanner } from "../../oosTracking/components/OosBanner";
import type { OosInvestigationListItemDto } from "../../../services/oosInvestigationService";

// 3. Bottom Information Area: Column 3 - Approval & Electronic Signatures
export function ApprovalSignaturesCard({
  summary,
  canReview,
  canApprove,
  reviewAvailability,
  approvalAvailability,
  comment,
  setComment,
  decision,
  setDecision,
  certificateRemarks,
  setCertificateRemarks,
  selectedTestOrderIds,
  setSelectedTestOrderIds,
  analysts,
  analystsLoadFailed,
  newSampleAnalystOneId,
  setNewSampleAnalystOneId,
  newSampleAnalystTwoId,
  setNewSampleAnalystTwoId,
  onReviewClick,
  onApproveClick,
  reviewableSections,
  reviewSectionId,
  onReviewSectionChange,
  retentionQuantity = "",
  setRetentionQuantity,
  retentionQuantityUnit = "",
  setRetentionQuantityUnit,
  approvableSections,
  approvalSectionId,
  onApprovalSectionChange,
  effectiveApprovalSectionId,
  canCloseTesting,
  closableSections,
  closingSectionId,
  onClosingSectionChange,
  onCloseTestingClick,
  oosInvestigations = [],
  canOpenInvestigation = false,
  openingInvestigation = false,
  openInvestigationError = null,
  onOpenInvestigation
}: {
  summary: SampleSummary;
  // Whether one of the viewer's sections is waiting for a review / a decision. Whether the viewer may act is
  // the server's answer, in the two availabilities.
  canReview: boolean;
  canApprove: boolean;
  reviewAvailability: ActionAvailability;
  approvalAvailability: ActionAvailability;
  comment: string;
  setComment: (c: string) => void;
  decision: SampleApprovalDecision;
  setDecision: (d: SampleApprovalDecision) => void;
  certificateRemarks: string;
  setCertificateRemarks: (r: string) => void;
  selectedTestOrderIds: number[];
  setSelectedTestOrderIds: (ids: number[]) => void;
  analysts: UserRecord[];
  // The eligible-analyst list failed to load, so the pickers are empty for
  // that reason - not because no analyst is eligible.
  analystsLoadFailed: boolean;
  newSampleAnalystOneId: number | "";
  setNewSampleAnalystOneId: (id: number | "") => void;
  newSampleAnalystTwoId: number | "";
  setNewSampleAnalystTwoId: (id: number | "") => void;
  onReviewClick: () => void;
  onApproveClick: () => void;
  reviewableSections: SampleSectionSummaryDetail[];
  reviewSectionId: number | "";
  onReviewSectionChange: (id: number | "") => void;
  retentionQuantity?: string;
  setRetentionQuantity?: (q: string) => void;
  retentionQuantityUnit?: string;
  setRetentionQuantityUnit?: (u: string) => void;
  approvableSections: SampleSectionSummaryDetail[];
  approvalSectionId: number | "";
  onApprovalSectionChange: (id: number | "") => void;
  effectiveApprovalSectionId: number | undefined;
  canCloseTesting: boolean;
  closableSections: SampleSectionSummaryDetail[];
  closingSectionId: number | "";
  onClosingSectionChange: (id: number | "") => void;
  onCloseTestingClick: () => void;
  oosInvestigations?: OosInvestigationListItemDto[];
  canOpenInvestigation?: boolean;
  openingInvestigation?: boolean;
  openInvestigationError?: string | null;
  onOpenInvestigation?: () => void;
}) {
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();
  const hasSignatures = summary.signatures.length > 0;
  const isApproved = summary.status === "Approved";
  const isRejected = summary.status === "Rejected";

  const isRetestDecision = decision === "RetestRetainedSample" || decision === "NewSampleRequest";
  const sectionChosen = approvableSections.length <= 1 || approvalSectionId !== "";
  const decisionValid =
    sectionChosen &&
    (!isRetestDecision ||
      (selectedTestOrderIds.length > 0 &&
        (decision !== "NewSampleRequest" ||
          (newSampleAnalystOneId !== "" && newSampleAnalystTwoId !== "" && newSampleAnalystOneId !== newSampleAnalystTwoId))));

  const eligibleRetestOrders = summary.testOrders.filter(
    (tOrder) => !tOrder.isSuperseded && (effectiveApprovalSectionId == null || tOrder.sectionId === effectiveApprovalSectionId)
  );

  const activeReviewSectionId = reviewableSections.length === 1 ? reviewableSections[0].sectionId : reviewSectionId;
  const activeReviewSection = reviewableSections.find((s) => s.sectionId === activeReviewSectionId);
  const showRetention = isRawMaterialPhyschemReview(summary.category, activeReviewSection?.sectionCode);
  const retentionValid = !showRetention || isRetentionValid(retentionQuantity, retentionQuantityUnit);

  const oosWarning = useMemo(() => {
    if (decision !== "Approve" || effectiveApprovalSectionId == null) {
      return null;
    }
    const targetSection = approvableSections.find((s) => s.sectionId === effectiveApprovalSectionId);
    const sectionName = targetSection?.sectionName ?? "";
    // Which results fail is decided on the server (summary.certificate).
    const scope = sectionScope(summary.certificate, effectiveApprovalSectionId);
    const matrix = scope?.matrix ?? null;
    const simple = scope?.simple ?? null;

    const failingNames: string[] = [];
    if (matrix) {
      for (const c of matrix.testConclusions) {
        if (c.failingLocationNames.length > 0) {
          failingNames.push(c.testDisplayName || c.testCode);
        }
      }
    } else if (simple) {
      for (const r of simple.rows) {
        if (!r.conform && !r.limitsNotConfigured && !r.noResult) {
          failingNames.push(r.testDisplayName || r.testCode);
        }
      }
    }

    const uniqueFailing = Array.from(new Set(failingNames));
    if (uniqueFailing.length === 0) {
      return null;
    }

    return {
      sectionName,
      failingList: uniqueFailing.join(", ")
    };
  }, [decision, effectiveApprovalSectionId, approvableSections, summary.certificate]);

  const retestEntry = approvalAvailability.get("approval.retestRetainedSample");
  const newSampleEntry = approvalAvailability.get("approval.newSampleRequest");

  const visibleDecisionOptions = useMemo(
    () =>
      DECISION_OPTIONS.filter((opt) => {
        if (opt.value === "RetestRetainedSample") return retestEntry === null || retestEntry.allowed;
        if (opt.value === "NewSampleRequest") return newSampleEntry === null || newSampleEntry.allowed;
        return true;
      }),
    [retestEntry, newSampleEntry]
  );

  const DECISION_ACTION: Record<SampleApprovalDecision, string> = {
    Approve: "approval.approve",
    Reject: "approval.reject",
    RetestRetainedSample: "approval.retestRetainedSample",
    NewSampleRequest: "approval.newSampleRequest"
  };

  const approveEntry = approvalAvailability.get("approval.approve");
  const rejectEntry = approvalAvailability.get("approval.reject");
  const activeEntry =
    approvalAvailability.get(DECISION_ACTION[decision]) ??
    (approveEntry && !approveEntry.allowed ? approveEntry : rejectEntry);
  const approvalBlockedReason =
    activeEntry && !activeEntry.allowed ? (activeEntry.reason ?? null) : null;
  const approvalBlockedCode =
    activeEntry && !activeEntry.allowed ? (activeEntry.reasonCode ?? null) : null;
  const canOpenMissingInvestigation =
    canOpenInvestigation &&
    approvalBlockedCode === "Oos.FailingResultWithoutInvestigation";

  return (
    <Paper sx={{ p: 2.5, border: "1px solid", borderColor: "divider", borderRadius: 2, height: "100%", bgcolor: "background.paper" }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <VerifiedUserOutlinedIcon sx={{ color: theme.palette.primary.main, fontSize: 20 }} />
          <Typography sx={{ fontWeight: 700, fontSize: 14, color: theme.palette.primary.main }}>
            {t("sampleSummary.approval.title")}
          </Typography>
        </Box>
        <StatusBadge status={summary.status} />
      </Box>

      {/* Signature Trail Display */}
      {hasSignatures ? (
        <Stack spacing={2} sx={{ mb: 2 }}>
          {summary.signatures.map((sig, idx) => (
            <Box
              key={idx}
              sx={{
                p: 1.5,
                bgcolor: "background.default",
                borderRadius: 1.5,
                border: "1px solid",
                borderColor: "divider"
              }}
            >
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 0.5 }}>
                <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.primary" }}>
                  {sig.role}: <Ltr>{`${sig.printedName} (${sig.username})`}</Ltr>
                </Typography>
                <Chip
                  size="small"
                  icon={<CheckCircleOutlineIcon sx={{ fontSize: "14px !important" }} />}
                  label={t("sampleSummary.approval.verified")}
                  sx={{
                    bgcolor: theme.custom.status.notDetected.bg,
                    color: theme.custom.status.notDetected.text,
                    fontWeight: 700,
                    fontSize: 12,
                    height: 20
                  }}
                />
              </Box>
              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.5 }}>
                <Ltr>{formatExactTime(sig.signedAt)}</Ltr>
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", fontStyle: "italic" }}>
                "{sig.meaningText}"
              </Typography>
              {sig.comment && (
                <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.5 }}>
                  {t("sampleSummary.approval.commentLabel")}: {sig.comment}
                </Typography>
              )}

            </Box>
          ))}
        </Stack>
      ) : isApproved || isRejected ? (
        <Stack spacing={1.5} sx={{ mb: 2 }}>
          {summary.reviewedByName && (
            <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.primary" }}>
                <Trans
                  i18nKey="sampleSummary.approval.reviewer"
                  ns="testingWorkspace"
                  values={{ name: summary.reviewedByName }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                <Ltr>{formatExactTime(summary.reviewedAt)}</Ltr> · {t("sampleSummary.approval.verified")}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", fontStyle: "italic", mt: 0.5 }}>
                "{SIGNATURE_STATEMENTS.Reviewed}"
              </Typography>

            </Box>
          )}
          {summary.approvedByName && (
            <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.primary" }}>
                <Trans
                  i18nKey="sampleSummary.approval.sectionHead"
                  ns="testingWorkspace"
                  values={{ name: summary.approvedByName }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                <Ltr>{formatExactTime(summary.approvedAt)}</Ltr> · {t("sampleSummary.approval.verified")}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", fontStyle: "italic", mt: 0.5 }}>
                "{isRejected ? SIGNATURE_STATEMENTS.Rejected : SIGNATURE_STATEMENTS.Approved}"
              </Typography>
            </Box>
          )}
        </Stack>
      ) : (
        <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 2 }}>
          {summary.status === "UnderReview"
            ? t("sampleSummary.approval.awaitingReview")
            : summary.status === "UnderApproval"
            ? t("sampleSummary.approval.awaitingApproval")
            : t("sampleSummary.approval.noSignatures")}
        </Typography>
      )}

      {/* Review Action Form */}
      {canReview && (
        <Box sx={{ mt: 2, pt: 2, borderTop: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1, color: theme.palette.primary.main }}>
            {t("sampleSummary.approval.submitTechnicalReview")}
          </Typography>
          {reviewableSections.length > 1 && (
            <TextField
              select
              fullWidth
              size="small"
              label={t("sampleSummary.approval.laboratorySection")}
              required
              value={reviewSectionId}
              onChange={(e) => onReviewSectionChange(Number(e.target.value))}
              sx={{ mb: 1.5 }}
            >
              {reviewableSections.map((sec) => (
                <MenuItem key={sec.sectionId} value={sec.sectionId}>
                  {sec.sectionName}
                </MenuItem>
              ))}
            </TextField>
          )}
          <Alert severity="info" sx={{ fontSize: 12, py: 0.5, mb: 1.5 }}>
            {activeReviewSection
              ? t("sampleSummary.approval.confirmReviewSection", { section: activeReviewSection.sectionName })
              : t("sampleSummary.approval.confirmReviewSample")}
          </Alert>
          {showRetention && (
            <Box sx={{ display: "flex", gap: 1.5, mb: 1.5 }}>
              <TextField
                fullWidth
                size="small"
                type="number"
                label={t("sampleSummary.approval.retentionQuantity")}
                required
                value={retentionQuantity}
                onChange={(e) => setRetentionQuantity?.(e.target.value)}
                slotProps={{ htmlInput: { step: "any", min: 0, dir: "ltr" } }}
              />
              <TextField
                select
                size="small"
                label={t("sampleSummary.approval.unit")}
                required
                value={retentionQuantityUnit}
                onChange={(e) => setRetentionQuantityUnit?.(e.target.value)}
                sx={{ minWidth: 100 }}
              >
                {RETENTION_UNITS.map((u) => (
                  <MenuItem key={u} value={u}>
                    <Ltr>{u}</Ltr>
                  </MenuItem>
                ))}
              </TextField>
            </Box>
          )}
          <TextField
            fullWidth
            size="small"
            label={t("sampleSummary.approval.commentOptional")}
            multiline
            rows={2}
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            sx={{ mb: 1.5 }}
          />
          <GatedAction action="review.complete" availability={reviewAvailability}>
            <Button
              variant="contained"
              color="primary"
              fullWidth
              disabled={Boolean((reviewableSections.length > 1 && !reviewSectionId) || !retentionValid)}
              onClick={onReviewClick}
            >
              {t("sampleSummary.approval.submitReview")}
            </Button>
          </GatedAction>
        </Box>
      )}

      {/* Approval Action Form */}
      {canApprove && (
        <Box sx={{ mt: 2, pt: 2, borderTop: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1, color: theme.palette.primary.main }}>
            {t("sampleSummary.approval.submitReleaseDecision")}
          </Typography>
          {approvableSections.length > 1 && (
            <TextField
              select
              fullWidth
              size="small"
              label={t("sampleSummary.approval.laboratorySection")}
              required
              value={approvalSectionId}
              onChange={(e) => onApprovalSectionChange(Number(e.target.value))}
              sx={{ mb: 1.5 }}
            >
              {approvableSections.map((sec) => (
                <MenuItem key={sec.sectionId} value={sec.sectionId}>
                  {sec.sectionName}
                </MenuItem>
              ))}
            </TextField>
          )}
          <TextField
            fullWidth
            size="small"
            label={t("sampleSummary.approval.commentOptional")}
            multiline
            rows={2}
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            sx={{ mb: 1.5 }}
          />
          <RadioGroup
            value={decision}
            onChange={(e) => setDecision(e.target.value as SampleApprovalDecision)}
            sx={{ mb: 1.5 }}
          >
            {visibleDecisionOptions.map((opt) => (
              <FormControlLabel
                key={opt.value}
                value={opt.value}
                control={<Radio size="small" />}
                label={<Typography sx={{ fontSize: 12 }}>{t(`sampleSummary.decisions.${opt.value}`)}</Typography>}
              />
            ))}
          </RadioGroup>
          {(oosInvestigations.length > 0 || Boolean(approvalBlockedReason) || canOpenMissingInvestigation) && (
            <Box sx={{ mb: 1.5 }}>
              <OosBanner
                investigations={oosInvestigations}
                blockReason={approvalBlockedReason}
                canOpenManually={canOpenMissingInvestigation}
                openingInvestigation={openingInvestigation}
                openError={openInvestigationError}
                onOpenInvestigation={onOpenInvestigation}
              />
            </Box>
          )}
          {decision === "Approve" && (
            <TextField
              fullWidth
              size="small"
              label={t("sampleSummary.approval.certificateRemarksLabel")}
              helperText={t("sampleSummary.approval.certificateRemarksHelper")}
              multiline
              rows={2}
              value={certificateRemarks}
              onChange={(e) => setCertificateRemarks(e.target.value)}
              sx={{ mb: 1.5 }}
            />
          )}

          {(decision === "RetestRetainedSample" || decision === "NewSampleRequest") && (
            <Box sx={{ mb: 1.5 }}>
              <Typography sx={{ fontSize: 12, fontWeight: 700, mb: 0.5 }}>
                {t("sampleSummary.approval.testsToRetest")}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
                {decision === "NewSampleRequest"
                  ? t("sampleSummary.approval.testsToRetestHelpNewSample")
                  : t("sampleSummary.approval.testsToRetestHelpRetained")}
              </Typography>
              {approvableSections.length > 1 && !approvalSectionId ? (
                <Alert severity="info" sx={{ fontSize: 12, py: 0.5, mb: 1 }}>
                  {t("sampleSummary.approval.selectSectionForRetest")}
                </Alert>
              ) : (
                <FormGroup>
                  {eligibleRetestOrders.map((tOrder) => (
                    <FormControlLabel
                      key={tOrder.testOrderId}
                      control={
                        <Checkbox
                          size="small"
                          checked={selectedTestOrderIds.includes(tOrder.testOrderId)}
                          onChange={(e) =>
                            setSelectedTestOrderIds(
                              e.target.checked
                                ? [...selectedTestOrderIds, tOrder.testOrderId]
                                : selectedTestOrderIds.filter((id) => id !== tOrder.testOrderId)
                            )
                          }
                        />
                      }
                      label={
                        <Typography sx={{ fontSize: 12 }}>
                          {tOrder.testDisplayName}
                          {tOrder.sectionName && (
                            <Chip
                              size="small"
                              label={tOrder.sectionName}
                              variant="outlined"
                              sx={{ ml: 0.5, height: 20, fontSize: 12 }}
                            />
                          )}
                          {isTestOrderNonPassing(tOrder) && (
                            <Chip
                              size="small"
                              label={t("sampleSummary.approval.nonConforming")}
                              color="error"
                              sx={{ ml: 0.5, height: 20, fontSize: 12 }}
                            />
                          )}
                        </Typography>
                      }
                    />
                  ))}
                </FormGroup>
              )}
              {selectedTestOrderIds.length === 0 && (approvableSections.length <= 1 || approvalSectionId !== "") && (
                <Alert severity="warning" sx={{ fontSize: 12, py: 0, mt: 0.5 }}>
                  {t("sampleSummary.approval.selectAtLeastOneRetest")}
                </Alert>
              )}
            </Box>
          )}

          {decision === "NewSampleRequest" && (
            <Box sx={{ mb: 1.5 }}>
              <Typography sx={{ fontSize: 12, fontWeight: 700, mb: 0.5 }}>
                {t("sampleSummary.approval.analystsForNewSamples")}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
                {t("sampleSummary.approval.analystsDifferentHint")}
              </Typography>
              {analystsLoadFailed && (
                <Alert severity="error" sx={{ mb: 1 }}>
                  {t("sampleSummary.approval.analystsLoadFailed")}
                </Alert>
              )}
              <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
                <FormControl fullWidth size="small">
                  <InputLabel>{t("sampleSummary.approval.newSample1Analyst")}</InputLabel>
                  <Select<number | "">
                    label={t("sampleSummary.approval.newSample1Analyst")}
                    value={newSampleAnalystOneId}
                    onChange={(e) => setNewSampleAnalystOneId(e.target.value === "" ? "" : Number(e.target.value))}
                    inputProps={{ "aria-label": t("sampleSummary.approval.newSample1Analyst") }}
                  >
                    {analysts.map((a) => (
                      <MenuItem key={a.id} value={a.id}><Ltr>{`${a.fullName} (${a.username})`}</Ltr></MenuItem>
                    ))}
                  </Select>
                </FormControl>
                <FormControl fullWidth size="small">
                  <InputLabel>{t("sampleSummary.approval.newSample2Analyst")}</InputLabel>
                  <Select<number | "">
                    label={t("sampleSummary.approval.newSample2Analyst")}
                    value={newSampleAnalystTwoId}
                    onChange={(e) => setNewSampleAnalystTwoId(e.target.value === "" ? "" : Number(e.target.value))}
                    inputProps={{ "aria-label": t("sampleSummary.approval.newSample2Analyst") }}
                  >
                    {analysts.map((a) => (
                      <MenuItem key={a.id} value={a.id}><Ltr>{`${a.fullName} (${a.username})`}</Ltr></MenuItem>
                    ))}
                  </Select>
                </FormControl>
              </Stack>
              {newSampleAnalystOneId !== "" && newSampleAnalystOneId === newSampleAnalystTwoId && (
                <Alert severity="warning" sx={{ fontSize: 12, py: 0, mt: 0.5 }}>
                  {t("sampleSummary.approval.analystsMustDiffer")}
                </Alert>
              )}
            </Box>
          )}

          {oosWarning && (
            <Alert severity="warning" sx={{ fontSize: 12, py: 0.5, mb: 1.5 }}>
              {t("sampleSummary.approval.oosWarning", { section: oosWarning.sectionName, failingList: oosWarning.failingList })}
            </Alert>
          )}

          <GatedAction
            action={DECISION_ACTION[decision]}
            availability={approvalAvailability}
          >
            <Button
              variant="contained"
              color="primary"
              fullWidth
              disabled={!decisionValid}
              onClick={onApproveClick}
            >
              {t("sampleSummary.approval.submitDecision")}
            </Button>
          </GatedAction>
        </Box>
      )}

      {/* Close Testing Action - only once another lab has rejected the
          sample and this viewer's own lab still has open work (design.md
          §5.3). Independent of canReview/canApprove: closing is available
          across InTesting/UnderReview/UnderApproval, not just the last one. */}
      {canCloseTesting && (
        <Box sx={{ mt: 2, pt: 2, borderTop: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1, color: theme.palette.error.main }}>
            {t("sampleSummary.approval.closeTesting")}
          </Typography>
          <Alert severity="warning" sx={{ fontSize: 12, py: 0.5, mb: 1.5 }}>
            {t("sampleSummary.approval.closeTestingAlert")}
          </Alert>
          {closableSections.length > 1 && (
            <TextField
              select
              fullWidth
              size="small"
              label={t("sampleSummary.approval.laboratorySection")}
              required
              value={closingSectionId}
              onChange={(e) => onClosingSectionChange(Number(e.target.value))}
              sx={{ mb: 1.5 }}
            >
              {closableSections.map((sec) => (
                <MenuItem key={sec.sectionId} value={sec.sectionId}>
                  {sec.sectionName}
                </MenuItem>
              ))}
            </TextField>
          )}
          <Button
            variant="outlined"
            color="error"
            fullWidth
            disabled={closableSections.length > 1 && !closingSectionId}
            onClick={onCloseTestingClick}
          >
            {t("sampleSummary.approval.closeTesting")}
          </Button>
        </Box>
      )}
    </Paper>
  );
}
