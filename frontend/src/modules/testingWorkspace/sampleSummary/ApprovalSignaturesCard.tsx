import { useMemo } from "react";
import { Box, Typography, Stack, TextField, Button, Alert, Radio, RadioGroup, FormControlLabel, Paper, Chip, useTheme, Checkbox, FormGroup, Select, MenuItem, InputLabel, FormControl } from "@mui/material";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import VerifiedUserOutlinedIcon from "@mui/icons-material/VerifiedUserOutlined";
import { StatusBadge } from "../../../components/StatusBadge";
import { SampleApprovalDecision } from "../services/SampleSummaryService";
import { sectionScope } from "../coaAggregation";
import { SampleSummary, SampleSectionSummaryDetail } from "../types/sampleSummaryTypes";
import { UserRecord } from "../../users/services/UserService";
import { DECISION_OPTIONS, SIGNATURE_STATEMENTS, formatExactTime, isTestOrderNonPassing } from "./summaryFormatting";

// 3. Bottom Information Area: Column 3 - Approval & Electronic Signatures
export function ApprovalSignaturesCard({
  summary,
  canReview,
  canApprove,
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
  approvableSections,
  approvalSectionId,
  onApprovalSectionChange,
  effectiveApprovalSectionId,
  canCloseTesting,
  closableSections,
  closingSectionId,
  onClosingSectionChange,
  onCloseTestingClick
}: {
  summary: SampleSummary;
  canReview: boolean;
  canApprove: boolean;
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
  approvableSections: SampleSectionSummaryDetail[];
  approvalSectionId: number | "";
  onApprovalSectionChange: (id: number | "") => void;
  effectiveApprovalSectionId: number | undefined;
  canCloseTesting: boolean;
  closableSections: SampleSectionSummaryDetail[];
  closingSectionId: number | "";
  onClosingSectionChange: (id: number | "") => void;
  onCloseTestingClick: () => void;
}) {
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
    (t) => !t.isSuperseded && (effectiveApprovalSectionId == null || t.sectionId === effectiveApprovalSectionId)
  );

  const activeReviewSectionId = reviewableSections.length === 1 ? reviewableSections[0].sectionId : reviewSectionId;
  const activeReviewSection = reviewableSections.find((s) => s.sectionId === activeReviewSectionId);

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

  return (
    <Paper sx={{ p: 2.5, border: "1px solid", borderColor: "divider", borderRadius: 2, height: "100%", bgcolor: "background.paper" }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <VerifiedUserOutlinedIcon sx={{ color: theme.palette.primary.main, fontSize: 20 }} />
          <Typography sx={{ fontWeight: 700, fontSize: 14, color: theme.palette.primary.main }}>
            Approval & Electronic Signatures
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
                  {sig.role}: {sig.printedName} ({sig.username})
                </Typography>
                <Chip
                  size="small"
                  icon={<CheckCircleOutlineIcon sx={{ fontSize: "14px !important" }} />}
                  label="Verified"
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
                {formatExactTime(sig.signedAt)}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", fontStyle: "italic" }}>
                "{SIGNATURE_STATEMENTS[sig.meaning] ?? sig.meaning}"
              </Typography>
              {sig.comment && (
                <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.5 }}>
                  Comment: {sig.comment}
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
                Reviewer: {summary.reviewedByName}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                {formatExactTime(summary.reviewedAt)} · Verified
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", fontStyle: "italic", mt: 0.5 }}>
                "{SIGNATURE_STATEMENTS.Reviewed}"
              </Typography>
            </Box>
          )}
          {summary.approvedByName && (
            <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.primary" }}>
                Section Head: {summary.approvedByName}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                {formatExactTime(summary.approvedAt)} · Verified
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
            ? "Awaiting technical review by authorized reviewer."
            : summary.status === "UnderApproval"
            ? "Reviewed. Awaiting release approval decision by Section Head."
            : "No formal signatures recorded."}
        </Typography>
      )}

      {/* Review Action Form */}
      {canReview && (
        <Box sx={{ mt: 2, pt: 2, borderTop: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1, color: theme.palette.primary.main }}>
            Submit Technical Review
          </Typography>
          {reviewableSections.length > 1 && (
            <TextField
              select
              fullWidth
              size="small"
              label="Laboratory Section"
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
              ? `By submitting, I confirm I have reviewed all test results for ${activeReviewSection.sectionName}.`
              : "By submitting, I confirm I have reviewed all test results for this sample."}
          </Alert>
          <TextField
            fullWidth
            size="small"
            label="Comment (optional)"
            multiline
            rows={2}
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            sx={{ mb: 1.5 }}
          />
          <Button
            variant="contained"
            color="primary"
            fullWidth
            disabled={reviewableSections.length > 1 && !reviewSectionId}
            onClick={onReviewClick}
          >
            Submit Review
          </Button>
        </Box>
      )}

      {/* Approval Action Form */}
      {canApprove && (
        <Box sx={{ mt: 2, pt: 2, borderTop: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1, color: theme.palette.primary.main }}>
            Submit Release Decision
          </Typography>
          {approvableSections.length > 1 && (
            <TextField
              select
              fullWidth
              size="small"
              label="Laboratory Section"
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
            label="Comment (optional)"
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
            {DECISION_OPTIONS.map((opt) => (
              <FormControlLabel
                key={opt.value}
                value={opt.value}
                control={<Radio size="small" />}
                label={<Typography sx={{ fontSize: 12 }}>{opt.label}</Typography>}
              />
            ))}
          </RadioGroup>
          {decision === "Approve" && (
            <TextField
              fullWidth
              size="small"
              label="Certificate Remarks (optional)"
              helperText="Printed on the Certificate of Analysis exactly as typed. Leave blank to show &quot;No remarks.&quot; Separate from the internal comment above, which never appears on the certificate."
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
                Tests to Retest
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
                Non-conforming tests are pre-checked. Adjust as needed - only the checked test(s) move to the new sample{decision === "NewSampleRequest" ? "s" : ""}; everything else on this sample is left untouched.
              </Typography>
              {approvableSections.length > 1 && !approvalSectionId ? (
                <Alert severity="info" sx={{ fontSize: 12, py: 0.5, mb: 1 }}>
                  Please select a laboratory section above to view tests to retest.
                </Alert>
              ) : (
                <FormGroup>
                  {eligibleRetestOrders.map((t) => (
                    <FormControlLabel
                      key={t.testOrderId}
                      control={
                        <Checkbox
                          size="small"
                          checked={selectedTestOrderIds.includes(t.testOrderId)}
                          onChange={(e) =>
                            setSelectedTestOrderIds(
                              e.target.checked
                                ? [...selectedTestOrderIds, t.testOrderId]
                                : selectedTestOrderIds.filter((id) => id !== t.testOrderId)
                            )
                          }
                        />
                      }
                      label={
                        <Typography sx={{ fontSize: 12 }}>
                          {t.testDisplayName}
                          {t.sectionName && (
                            <Chip
                              size="small"
                              label={t.sectionName}
                              variant="outlined"
                              sx={{ ml: 0.5, height: 20, fontSize: 12 }}
                            />
                          )}
                          {isTestOrderNonPassing(t) && (
                            <Chip
                              size="small"
                              label="Non-conforming"
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
                  Select at least one test to retest.
                </Alert>
              )}
            </Box>
          )}

          {decision === "NewSampleRequest" && (
            <Box sx={{ mb: 1.5 }}>
              <Typography sx={{ fontSize: 12, fontWeight: 700, mb: 0.5 }}>
                Analysts for the Two New Samples
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
                Two different analysts are required, and neither may be whoever tested the original sample.
              </Typography>
              {analystsLoadFailed && (
                <Alert severity="error" sx={{ mb: 1 }}>
                  The list of eligible analysts could not be loaded. Close and reopen this sample to try again.
                </Alert>
              )}
              <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
                <FormControl fullWidth size="small">
                  <InputLabel>New Sample 1 - Analyst</InputLabel>
                  <Select<number | "">
                    label="New Sample 1 - Analyst"
                    value={newSampleAnalystOneId}
                    onChange={(e) => setNewSampleAnalystOneId(e.target.value === "" ? "" : Number(e.target.value))}
                    inputProps={{ "aria-label": "New Sample 1 - Analyst" }}
                  >
                    {analysts.map((a) => (
                      <MenuItem key={a.id} value={a.id}>{a.fullName} ({a.username})</MenuItem>
                    ))}
                  </Select>
                </FormControl>
                <FormControl fullWidth size="small">
                  <InputLabel>New Sample 2 - Analyst</InputLabel>
                  <Select<number | "">
                    label="New Sample 2 - Analyst"
                    value={newSampleAnalystTwoId}
                    onChange={(e) => setNewSampleAnalystTwoId(e.target.value === "" ? "" : Number(e.target.value))}
                    inputProps={{ "aria-label": "New Sample 2 - Analyst" }}
                  >
                    {analysts.map((a) => (
                      <MenuItem key={a.id} value={a.id}>{a.fullName} ({a.username})</MenuItem>
                    ))}
                  </Select>
                </FormControl>
              </Stack>
              {newSampleAnalystOneId !== "" && newSampleAnalystOneId === newSampleAnalystTwoId && (
                <Alert severity="warning" sx={{ fontSize: 12, py: 0, mt: 0.5 }}>
                  The two new samples must be assigned to two different analysts.
                </Alert>
              )}
            </Box>
          )}

          {oosWarning && (
            <Alert severity="warning" sx={{ fontSize: 12, py: 0.5, mb: 1.5 }}>
              Out-of-specification results in {oosWarning.sectionName}: {oosWarning.failingList}. Approving will certify them as conforming - consider Not Conform or a retest.
            </Alert>
          )}

          <Button
            variant="contained"
            color="primary"
            fullWidth
            disabled={!decisionValid}
            onClick={onApproveClick}
          >
            Submit Decision
          </Button>
        </Box>
      )}

      {/* Close Testing Action - only once another lab has rejected the
          sample and this viewer's own lab still has open work (design.md
          §5.3). Independent of canReview/canApprove: closing is available
          across InTesting/UnderReview/UnderApproval, not just the last one. */}
      {canCloseTesting && (
        <Box sx={{ mt: 2, pt: 2, borderTop: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1, color: theme.palette.error.main }}>
            Close Testing
          </Typography>
          <Alert severity="warning" sx={{ fontSize: 12, py: 0.5, mb: 1.5 }}>
            Another laboratory has rejected this sample. You may close your own laboratory's remaining
            tests instead of continuing them.
          </Alert>
          {closableSections.length > 1 && (
            <TextField
              select
              fullWidth
              size="small"
              label="Laboratory Section"
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
            Close Testing
          </Button>
        </Box>
      )}
    </Paper>
  );
}
