import { useEffect, useState } from "react";
import {
  Box, Typography, Stack, Divider, TextField, Button, Alert, Radio, RadioGroup,
  FormControlLabel, Accordion, AccordionSummary, AccordionDetails,
  Table, TableHead, TableRow, TableCell, TableBody
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import PrintIcon from "@mui/icons-material/Print";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import DescriptionIcon from "@mui/icons-material/Description";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { StatusBadge } from "../../components/StatusBadge";
import { useAuth } from "../../contexts/AuthContext";
import { SampleSummaryService, SampleApprovalDecision } from "./services/SampleSummaryService";
import { SampleSummary, TestOrderSummaryDetail, SampleLocationDetail } from "./types/sampleSummaryTypes";

interface Props {
  open: boolean;
  sampleId: number | null;
  onClose: () => void;
}

const formatDate = (d: string | null) => (d ? new Date(d).toLocaleString() : "—");

const DECISION_OPTIONS: { value: SampleApprovalDecision; label: string }[] = [
  { value: "Approve", label: "Approve - Results conform to specifications" },
  { value: "Reject", label: "Not Conform (Final Conclusion) - Close this sample" },
  { value: "NewSampleRequest", label: "New Sample Required - Close and request new sample" },
  { value: "RetestRetainedSample", label: "Retest Retained Sample - Return to testing" }
];

// The floating page that appears when a reviewer or Section Head clicks
// a Sample's lifecycle badge in the Testing Workspace - identity,
// preparation, every TestOrder's full result history, the lifecycle
// timeline, and (based on status + role) the review/approval action area.
export function SampleSummaryDialog({ open, sampleId, onClose }: Props) {
  const { role } = useAuth();
  const [summary, setSummary] = useState<SampleSummary | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [comment, setComment] = useState("");
  const [decision, setDecision] = useState<SampleApprovalDecision>("Approve");
  const [confirmingReview, setConfirmingReview] = useState(false);
  const [confirmingDecision, setConfirmingDecision] = useState(false);
  const [exporting, setExporting] = useState<"pdf" | "word" | null>(null);
  const [exportError, setExportError] = useState<string | null>(null);

  useEffect(() => {
    if (open && sampleId) {
      setSummary(null);
      setLoadError(null);
      setComment("");
      setDecision("Approve");
      SampleSummaryService.getSummary(sampleId).then(setSummary).catch((e) => {
        setLoadError(e?.response?.data?.message ?? "Failed to load sample summary.");
      });
    }
  }, [open, sampleId]);

  const handleReviewConfirm = async (password: string) => {
    if (!sampleId) return;
    await SampleSummaryService.completeReview(sampleId, password, comment || undefined);
    setConfirmingReview(false);
    onClose();
  };

  const handleDecisionConfirm = async (password: string) => {
    if (!sampleId) return;
    await SampleSummaryService.decideApproval(sampleId, password, decision, comment || undefined);
    setConfirmingDecision(false);
    onClose();
  };

  const canReview = summary?.status === "UnderReview" && (role === "Reviewer" || role === "SectionHead" || role === "SystemAdministrator");
  const canApprove = summary?.status === "UnderApproval" && (role === "SectionHead" || role === "SystemAdministrator");

  const handleExport = async (format: "pdf" | "word") => {
    if (!sampleId || !summary) return;
    setExporting(format);
    setExportError(null);
    try {
      if (format === "pdf") await SampleSummaryService.exportPdf(sampleId, summary.referenceNumber);
      else await SampleSummaryService.exportWord(sampleId, summary.referenceNumber);
    } catch (e: any) {
      setExportError(e?.response?.data?.message ?? "Export failed.");
    } finally {
      setExporting(null);
    }
  };

  return (
    <>
      <FloatingDialog open={open && !confirmingReview && !confirmingDecision} title="Sample Summary" onClose={onClose}>
        {loadError && <Alert severity="error">{loadError}</Alert>}
        {!summary && !loadError && <LoadingSpinner />}
        {summary && (
          <Stack spacing={3} sx={{ minWidth: { md: 800 } }}>
            <Stack direction="row" spacing={1.5} justifyContent="flex-end">
              {exportError && <Alert severity="error" sx={{ flexGrow: 1 }}>{exportError}</Alert>}
              <Button
                variant="outlined" size="small" startIcon={<PrintIcon />}
                onClick={() => window.open(`/samples/${sampleId}/report`, "_blank", "noopener")}
              >
                Printable Report
              </Button>
              <Button variant="outlined" size="small" startIcon={<PictureAsPdfIcon />} disabled={!!exporting} onClick={() => handleExport("pdf")}>
                {exporting === "pdf" ? "Exporting…" : "Export PDF"}
              </Button>
              <Button variant="outlined" size="small" startIcon={<DescriptionIcon />} disabled={!!exporting} onClick={() => handleExport("word")}>
                {exporting === "word" ? "Exporting…" : "Export Word"}
              </Button>
            </Stack>
            <IdentitySection summary={summary} />
            {summary.preparation && <PreparationSection preparation={summary.preparation} />}
            <TestResultsSection testOrders={summary.testOrders} />
            <TimelineSection summary={summary} />

            {canReview && (
              <ActionSection title="Submit Review">
                <Alert severity="info">By submitting, I confirm I have reviewed all test results for this sample.</Alert>
                <TextField label="Comment (optional)" multiline rows={2} value={comment} onChange={(e) => setComment(e.target.value)} />
                <Button variant="contained" onClick={() => setConfirmingReview(true)}>Submit Review</Button>
              </ActionSection>
            )}

            {canApprove && (
              <ActionSection title="Submit Decision">
                <TextField label="Comment (optional)" multiline rows={2} value={comment} onChange={(e) => setComment(e.target.value)} />
                <RadioGroup value={decision} onChange={(e) => setDecision(e.target.value as SampleApprovalDecision)}>
                  {DECISION_OPTIONS.map((opt) => (
                    <FormControlLabel key={opt.value} value={opt.value} control={<Radio />} label={opt.label} />
                  ))}
                </RadioGroup>
                <Button variant="contained" onClick={() => setConfirmingDecision(true)}>Submit Decision</Button>
              </ActionSection>
            )}

            {summary.status === "Approved" && (
              <ActionSection title="Approval Details">
                <Typography sx={{ fontSize: 13 }}>Approved by <strong>{summary.approvedByName}</strong> on {formatDate(summary.approvedAt)}.</Typography>
                <Button
                  variant="outlined"
                  startIcon={<PrintIcon />}
                  onClick={() => window.open(`/samples/${sampleId}/report`, "_blank", "noopener")}
                >
                  Open Printable Report
                </Button>
              </ActionSection>
            )}

            {summary.status === "Rejected" && (
              <ActionSection title="Rejection Details">
                <Typography sx={{ fontSize: 13 }}>
                  Decision: <strong>{summary.approvalDecision}</strong> — recorded {formatDate(summary.approvedAt ?? summary.reviewedAt)}.
                </Typography>
              </ActionSection>
            )}
          </Stack>
        )}
      </FloatingDialog>

      {summary && (
        <SignatureDialog
          open={confirmingReview}
          meaningStatement="By submitting, I confirm I have reviewed all test results for this sample."
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
    </>
  );
}

function ActionSection({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Box>
      <Divider sx={{ mb: 1.5 }} />
      <Typography sx={{ fontWeight: 700, fontSize: 14, mb: 1 }}>{title}</Typography>
      <Stack spacing={1.5}>{children}</Stack>
    </Box>
  );
}

function Field({ label, value }: { label: string; value: string | number | null | undefined }) {
  return (
    <Box>
      <Typography sx={{ fontSize: 11, color: "text.secondary" }}>{label}</Typography>
      <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{value ?? "—"}</Typography>
    </Box>
  );
}

function IdentitySection({ summary: s }: { summary: SampleSummary }) {
  return (
    <Box>
      <Typography sx={{ fontWeight: 700, fontSize: 15, mb: 1 }}>Sample Identity</Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 1.5 }}>
        <Field label="Reference Number" value={s.referenceNumber} />
        <Field label="Category" value={s.category} />
        <Field label="Item / Point / Room / Machine" value={s.displayName} />
        <Field label="Production Stage" value={s.productionStage} />
        <Field label="Batch Number" value={s.batchNumber} />
        <Field label="Control Number" value={s.controlNumber} />
        <Field label="Cause of Testing" value={s.causeOfTesting} />
        <Field label="Received By" value={s.receivedByName} />
        <Field label="Received At" value={formatDate(s.receivedAt)} />
        <Field label="Sampled By" value={s.sampledBy} />
        <Field label="Sample Quantity" value={s.sampleQuantity} />
        <Field label="Mfg Date" value={formatDate(s.mfgDate)} />
        <Field label="Exp Date" value={formatDate(s.expDate)} />
        {s.waterSamplingPointCode && <Field label="Sampling Point" value={`${s.waterSamplingPointCode} — ${s.waterSamplingPointLocation}`} />}
        {s.storageCondition && (
          <Field label="Storage Condition" value={s.storageCondition === "Refrigerator" ? `Refrigerator (${s.storageTimeHours ?? "?"}h)` : s.storageCondition} />
        )}
      </Box>
    </Box>
  );
}

function PreparationSection({ preparation: p }: { preparation: NonNullable<SampleSummary["preparation"]> }) {
  return (
    <Box>
      <Typography sx={{ fontWeight: 700, fontSize: 15, mb: 1 }}>Sample Preparation</Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 1.5 }}>
        <Field label="Amount" value={`${p.amount} ${p.unit}`} />
        <Field label="Technique" value={p.technique} />
        {p.technique === "Filtration" && <Field label="Filtration Volume" value={p.filtrationVolume} />}
        {p.technique === "Filtration" && <Field label="Washing Volume" value={p.washingVolume} />}
        <Field label="Neutralizer" value={p.neutralizerName} />
        <Field label="Prepared By" value={p.preparedByName} />
        <Field label="Prepared At" value={formatDate(p.preparedAt)} />
      </Box>
    </Box>
  );
}

function TestResultsSection({ testOrders }: { testOrders: TestOrderSummaryDetail[] }) {
  return (
    <Box>
      <Typography sx={{ fontWeight: 700, fontSize: 15, mb: 1 }}>Test Results</Typography>
      {testOrders.map((order, i) => (
        <Accordion key={order.testOrderId} defaultExpanded={i === 0 && !order.isSuperseded}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Stack direction="row" spacing={1} alignItems="center">
              <Typography sx={{ fontWeight: 700, fontSize: 13 }}>{order.testCode} — {order.testDisplayName}</Typography>
              <StatusBadge status={order.status} />
              {order.isSuperseded && <StatusBadge status="Superseded" />}
            </Stack>
          </AccordionSummary>
          <AccordionDetails>
            <Stack spacing={2}>
              {order.incubations.map((inc, idx) => (
                <Box key={idx} sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))", gap: 1 }}>
                  <Field label="Step" value={inc.stepName} />
                  <Field label="Media Lot" value={inc.mediaLotNumber ? `${inc.mediaLotNumber} (${inc.mediaMaterialName})` : null} />
                  <Field label="Incubator" value={inc.incubatorName} />
                  <Field label="Temperature" value={inc.temperature} />
                  <Field label="Duration" value={inc.duration} />
                  <Field label="Expected Reading" value={formatDate(inc.expectedReadingAt)} />
                  <Field label="Actual Reading" value={formatDate(inc.completedAt)} />
                  <Field label="Outcome" value={inc.outcome} />
                </Box>
              ))}

              {order.locations.length > 0 ? (
                <LocationResultsTable locations={order.locations} />
              ) : (
                order.countTestReadings.map((r, idx) => (
                  <Box key={idx} sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))", gap: 1 }}>
                    <Field label="Plate Readings" value={r.plateReadings} />
                    <Field label="Dilution Factor" value={r.dilutionFactor} />
                    <Field label="Average" value={r.average} />
                    <Field label="Calculated" value={r.calculatedResult} />
                    <Field label="Reported Result" value={r.reportedResult} />
                    <Field label="Alert / Action / Spec" value={`${r.alertLimit ?? "—"} / ${r.actionLimit ?? "—"} / ${r.specLimit ?? "—"}`} />
                    <Box>
                      <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Status</Typography>
                      <StatusBadge status={r.status} />
                    </Box>
                    <Field label="Entered By" value={`${r.enteredByName} — ${formatDate(r.enteredAt)}`} />
                  </Box>
                ))
              )}

              {order.pathogenObservations.map((p, idx) => (
                <Box key={idx} sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))", gap: 1 }}>
                  <Field label="Step" value={p.stepName} />
                  <Field label="Growth Observed" value={p.growthObserved ? "Yes" : "No"} />
                  <Field label="Observed By" value={`${p.observedByName} — ${formatDate(p.observedAt)}`} />
                </Box>
              ))}

              {order.results.map((r, idx) => (
                <Box key={idx} sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))", gap: 1 }}>
                  <Field label="Result" value={r.interpretedValue ?? r.rawValue} />
                  <Field label="Entered By" value={`${r.enteredByName} — ${formatDate(r.enteredAt)}`} />
                </Box>
              ))}

              {order.workflowHistory.length > 0 && (
                <Box>
                  <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 0.5 }}>Step History</Typography>
                  <Stack spacing={0.5}>
                    {order.workflowHistory.map((w, idx) => (
                      <Typography key={idx} sx={{ fontSize: 12 }}>
                        {w.fromStep} → {w.toStep} by <strong>{w.performedByName}</strong> at {formatDate(w.timestamp)}{w.note ? ` — ${w.note}` : ""}
                      </Typography>
                    ))}
                  </Stack>
                </Box>
              )}
            </Stack>
          </AccordionDetails>
        </Accordion>
      ))}
    </Box>
  );
}

// Read-only EM/After Cleaning batch result view - same columns as
// LocationResultGridDialog, plus who/when entered each one.
function LocationResultsTable({ locations }: { locations: SampleLocationDetail[] }) {
  const conformCount = locations.filter((l) => l.status === "WithinLimits" || l.status === "Absent").length;
  // WithinLimits/Absent are both "conforming" outcomes (CFU vs pathogen);
  // everything else escalates - Detected is listed last since it's a
  // pathogen positive, the worst possible outcome for that test type.
  const severityOrder = ["WithinLimits", "Absent", "AlertLimitExceeded", "ActionLimitExceeded", "OutOfSpecification", "Detected"];
  const worstStatus = locations.reduce<string>((worst, l) => {
    if (!l.status) return worst;
    return severityOrder.indexOf(l.status) > severityOrder.indexOf(worst) ? l.status : worst;
  }, "WithinLimits");

  return (
    <Stack spacing={1}>
      <Box sx={{ overflowX: "auto" }}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Location</TableCell>
              <TableCell>Limits (Alert / Action / Spec)</TableCell>
              <TableCell>CFU</TableCell>
              <TableCell>Reported Result</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Entered By</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {locations.map((l, idx) => (
              <TableRow key={idx}>
                <TableCell>{l.locationName}{l.gradeClassification ? ` (${l.gradeClassification})` : ""}</TableCell>
                <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                  {l.alertLimit ?? "—"} / {l.actionLimit ?? "—"} / {l.specLimit ?? "—"}
                </TableCell>
                <TableCell>{l.cfuResult ?? "—"}</TableCell>
                <TableCell>{l.reportedResult ?? "—"}</TableCell>
                <TableCell>{l.status ? <StatusBadge status={l.status} /> : "—"}</TableCell>
                <TableCell sx={{ fontSize: 12 }}>{l.enteredByName ? `${l.enteredByName} — ${formatDate(l.enteredAt)}` : "—"}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Box>
      <Typography sx={{ fontSize: 13 }}>
        {conformCount}/{locations.length} locations within spec — worst status: <StatusBadge status={worstStatus} />
      </Typography>
    </Stack>
  );
}

function TimelineSection({ summary }: { summary: SampleSummary }) {
  return (
    <Box>
      <Typography sx={{ fontWeight: 700, fontSize: 15, mb: 1 }}>Timeline</Typography>
      <Stack spacing={1}>
        {summary.timeline.map((e, idx) => (
          <Box key={idx}>
            <Typography sx={{ fontSize: 13 }}>
              <strong>{e.eventType}</strong>{e.decision ? ` (${e.decision})` : ""} — {e.performedByName} — {formatDate(e.timestamp)}
            </Typography>
            {e.comment && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>"{e.comment}"</Typography>}
          </Box>
        ))}
        {summary.timeline.length === 0 && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>No lifecycle events yet.</Typography>}
      </Stack>
    </Box>
  );
}
