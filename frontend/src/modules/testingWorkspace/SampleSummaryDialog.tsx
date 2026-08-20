import { useEffect, useMemo, useState } from "react";
import {
  Box,
  Typography,
  Stack,
  Divider,
  TextField,
  Button,
  Alert,
  Radio,
  RadioGroup,
  FormControlLabel,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Paper,
  Chip
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import PrintIcon from "@mui/icons-material/Print";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import DescriptionIcon from "@mui/icons-material/Description";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import VerifiedUserOutlinedIcon from "@mui/icons-material/VerifiedUserOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import HistoryOutlinedIcon from "@mui/icons-material/HistoryOutlined";
import TimelineOutlinedIcon from "@mui/icons-material/TimelineOutlined";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { StatusBadge } from "../../components/StatusBadge";
import { brandColors } from "../../theme";
import { useAuth } from "../../contexts/AuthContext";
import { SampleSummaryService, SampleApprovalDecision } from "./services/SampleSummaryService";
import {
  SampleSummary,
  TestOrderSummaryDetail,
  SampleLocationDetail,
  IncubationDetail,
  SignatureTrailItem
} from "./types/sampleSummaryTypes";
import { pathogenObservationLabel } from "./utils/pathogenObservationLabel";
import { PathogenSessionDialog } from "./pathogenSession/PathogenSessionDialog";

interface Props {
  open: boolean;
  sampleId: number | null;
  onClose: () => void;
}

const formatDate = (d: string | null | undefined) =>
  d
    ? new Date(d).toLocaleString("en-GB", {
        day: "2-digit",
        month: "short",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit"
      })
    : "—";

const formatExactTime = (d: string | null | undefined) =>
  d
    ? new Date(d).toLocaleString("en-GB", {
        day: "2-digit",
        month: "short",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit"
      })
    : "—";

const DECISION_OPTIONS: { value: SampleApprovalDecision; label: string }[] = [
  { value: "Approve", label: "Approve — Results conform to specifications" },
  { value: "Reject", label: "Not Conform (Final Conclusion) — Close this sample" },
  { value: "NewSampleRequest", label: "New Sample Required — Close and request new sample" },
  { value: "RetestRetainedSample", label: "Retest Retained Sample — Return to testing" }
];

const SIGNATURE_STATEMENTS: Record<string, string> = {
  Reviewed: "I have reviewed the test data and confirm it is complete and accurate.",
  Approved: "I approve the release of this record for its intended use.",
  Rejected: "I reject this record; it does not conform to specification.",
  RetestRequested: "I am ordering a retest of the retained sample.",
  InvestigationOrdered: "I am ordering an investigation into these results."
};

// Reusable display field component
function SummaryField({
  label,
  value,
  secondaryValue,
  highlight = false
}: {
  label: string;
  value: string | number | null | undefined;
  secondaryValue?: string | null | undefined;
  highlight?: boolean;
}) {
  return (
    <Box sx={{ minWidth: 0 }}>
      <Typography sx={{ fontSize: 11, fontWeight: 600, color: "text.secondary", textTransform: "uppercase", letterSpacing: 0.3 }} noWrap>
        {label}
      </Typography>
      <Typography
        sx={{
          fontSize: 13,
          fontWeight: highlight ? 700 : 600,
          color: highlight ? brandColors.sectionTitle : "#1f2937",
          lineHeight: 1.3,
          wordBreak: "break-word"
        }}
      >
        {value != null && value !== "" ? value : "—"}
      </Typography>
      {secondaryValue && (
        <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 0.25 }}>
          {secondaryValue}
        </Typography>
      )}
    </Box>
  );
}

// 1. Sample Identity Section (4-column responsive grid)
function SampleIdentityCard({ summary: s }: { summary: SampleSummary }) {
  return (
    <Paper sx={{ p: 2.5, border: "1px solid #e5e7eb", borderRadius: 2, bgcolor: "#ffffff" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 15, color: brandColors.sectionTitle, mb: 2 }}>
        Sample Identity
      </Typography>
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "repeat(4, 1fr)"
          },
          gap: 2
        }}
      >
        <SummaryField label="Reference Number" value={s.referenceNumber} highlight />
        <SummaryField label="Category" value={s.category} />
        <SummaryField label="Received At" value={formatDate(s.receivedAt)} />
        <SummaryField label="Exp Date" value={formatDate(s.expDate)} />

        <SummaryField label="Item / Point / Room / Machine" value={s.displayName} highlight />
        <SummaryField label="Production Stage" value={s.productionStage} />
        <SummaryField label="Sampled By" value={s.sampledBy} />
        <SummaryField label="Batch Number" value={s.batchNumber} />

        <SummaryField label="Control Number" value={s.controlNumber} />
        <SummaryField label="Received By" value={s.receivedByName} />
        <SummaryField label="Sample Quantity" value={s.sampleQuantity} />
        <SummaryField label="Mfg Date" value={formatDate(s.mfgDate)} />

        <SummaryField label="Cause of Testing" value={s.causeOfTesting} />
        {s.waterSamplingPointCode && (
          <SummaryField label="Sampling Point" value={`${s.waterSamplingPointCode} — ${s.waterSamplingPointLocation}`} />
        )}
        {s.storageCondition && (
          <SummaryField
            label="Storage Condition"
            value={s.storageCondition === "Refrigerator" ? `Refrigerator (${s.storageTimeHours ?? "?"}h)` : s.storageCondition}
          />
        )}
      </Box>
    </Paper>
  );
}

// Sample Preparation Card (if applicable)
function SamplePreparationCard({ preparation: p }: { preparation: NonNullable<SampleSummary["preparation"]> }) {
  return (
    <Paper sx={{ p: 2.5, border: "1px solid #e5e7eb", borderRadius: 2, bgcolor: "#ffffff" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 15, color: brandColors.sectionTitle, mb: 2 }}>
        Sample Preparation
      </Typography>
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "repeat(4, 1fr)"
          },
          gap: 2
        }}
      >
        <SummaryField label="Amount" value={`${p.amount} ${p.unit}`} />
        <SummaryField label="Technique" value={p.technique} />
        {p.technique === "Filtration" && (
          <>
            <SummaryField label="Filtration Volume" value={p.filtrationVolume ? `${p.filtrationVolume} mL` : "—"} />
            <SummaryField label="Washing Volume" value={p.washingVolume ? `${p.washingVolume} mL` : "—"} />
          </>
        )}
        <SummaryField label="Neutralizer" value={p.neutralizerName || "—"} />
        <SummaryField label="Prepared By" value={p.preparedByName} />
        <SummaryField label="Prepared At" value={formatDate(p.preparedAt)} />
      </Box>
    </Paper>
  );
}

// Incubation Stage Card (Stage 1, Stage 2, or Standard Single Stage)
function IncubationStageBlock({
  inc,
  totalStages,
  stageIndex
}: {
  inc: IncubationDetail;
  totalStages: number;
  stageIndex: number;
}) {
  const isMultiStage = totalStages > 1 || inc.stageNumber > 1 || !!inc.transferredAt || !!inc.transferredByName;
  const isStage1 = inc.stageNumber === 1;
  const isStage2 = inc.stageNumber === 2;

  const stageTitle = isMultiStage
    ? isStage1
      ? `Stage 1 Incubation (${inc.stepName})`
      : isStage2
      ? `Stage 2 Incubation (${inc.stepName})`
      : `Stage ${inc.stageNumber} Incubation (${inc.stepName})`
    : `Incubation (${inc.stepName})`;

  return (
    <Box
      sx={{
        p: 2,
        borderRadius: 1.5,
        border: "1px solid #e5e7eb",
        bgcolor: "#fcfcfd"
      }}
    >
      <Box sx={{ display: "flex", alignItems: "center", gap: 1.25, mb: 1.5 }}>
        {isMultiStage && (
          <Box
            sx={{
              width: 22,
              height: 22,
              borderRadius: "50%",
              bgcolor: brandColors.sectionTitle,
              color: "#ffffff",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 12,
              fontWeight: 700,
              flexShrink: 0
            }}
          >
            {inc.stageNumber}
          </Box>
        )}
        <Typography sx={{ fontSize: 13, fontWeight: 700, color: "#1f2937" }}>
          {stageTitle}
        </Typography>
      </Box>

      {/* Primary Incubation Parameters */}
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "repeat(4, 1fr)"
          },
          gap: 1.5,
          mb: 1.5
        }}
      >
        <SummaryField
          label="Media Lot"
          value={inc.mediaLotNumber ? `${inc.mediaLotNumber} (${inc.mediaMaterialName ?? "—"})` : "—"}
        />
        <SummaryField label="Incubator" value={inc.incubatorName} />
        <SummaryField label="Temperature" value={inc.temperature} />
        <SummaryField label="Duration" value={inc.duration} />
        <SummaryField
          label="Started At"
          value={formatDate(inc.startedAt)}
          secondaryValue={inc.startedByName ? `By ${inc.startedByName}` : undefined}
        />
        <SummaryField label="Started By" value={inc.startedByName} />
      </Box>

      <Divider sx={{ my: 1.25, borderStyle: "dashed" }} />

      {/* Stage Transition / Completion Details */}
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "repeat(3, 1fr)"
          },
          gap: 1.5
        }}
      >
        {isStage1 && isMultiStage ? (
          <>
            <SummaryField
              label="Transferred At"
              value={formatDate(inc.transferredAt ?? inc.completedAt)}
            />
            <SummaryField
              label="Transferred By"
              value={inc.transferredByName ?? inc.completedByName ?? "—"}
            />
            <SummaryField
              label="Outcome"
              value={inc.outcome ?? "Transferred to stage 2 incubation."}
            />
          </>
        ) : (
          <>
            <SummaryField
              label="Completed At"
              value={formatDate(inc.completedAt)}
            />
            <SummaryField
              label="Completed By"
              value={inc.completedByName ?? "—"}
            />
            <SummaryField
              label="Outcome"
              value={inc.outcome ?? "—"}
            />
          </>
        )}
      </Box>
    </Box>
  );
}

// Location-based Results Table (EM / After Cleaning)
function LocationResultsTable({ locations }: { locations: SampleLocationDetail[] }) {
  const conformCount = locations.filter((l) => l.status === "WithinLimits" || l.status === "Absent").length;
  const severityOrder = ["WithinLimits", "Absent", "AlertLimitExceeded", "ActionLimitExceeded", "OutOfSpecification", "Detected"];
  const worstStatus = locations.reduce<string>((worst, l) => {
    if (!l.status) return worst;
    return severityOrder.indexOf(l.status) > severityOrder.indexOf(worst) ? l.status : worst;
  }, "WithinLimits");

  return (
    <Box>
      <Table size="small" sx={{ border: "1px solid #e5e7eb", borderRadius: 1 }}>
        <TableHead sx={{ bgcolor: "#f9fafb" }}>
          <TableRow>
            <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Location</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Limits (Alert / Action / Spec)</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>CFU</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Reported Result</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "center" }}>Status</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Entered By</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Entered At</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {locations.map((l, idx) => (
            <TableRow key={idx} hover>
              <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                {l.locationName}{l.gradeClassification ? ` (${l.gradeClassification})` : ""}
              </TableCell>
              <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                {l.alertLimit || "—"} / {l.actionLimit || "—"} / {l.specLimit || "—"}
              </TableCell>
              <TableCell sx={{ fontSize: 12, textAlign: "right", fontWeight: 700 }}>
                {l.cfuResult ?? "—"}
              </TableCell>
              <TableCell sx={{ fontSize: 12 }}>
                {l.reportedResult ?? "—"}
              </TableCell>
              <TableCell sx={{ textAlign: "center" }}>
                {l.status ? <StatusBadge status={l.status} /> : "—"}
              </TableCell>
              <TableCell sx={{ fontSize: 12 }}>
                {l.enteredByName || "—"}
              </TableCell>
              <TableCell sx={{ fontSize: 12 }}>
                {formatDate(l.enteredAt)}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      <Box sx={{ mt: 1.25, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          <strong>{conformCount}/{locations.length}</strong> locations within specification
        </Typography>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>Worst status:</Typography>
          <StatusBadge status={worstStatus} />
        </Box>
      </Box>
    </Box>
  );
}

// Final Result Section component (Separated from Incubation)
function FinalResultBlock({ order }: { order: TestOrderSummaryDetail }) {
  const hasLocations = order.locations.length > 0;
  const hasCountReadings = order.countTestReadings.length > 0;
  const hasPathogens = order.pathogenObservations.length > 0;
  const hasResults = order.results.length > 0;

  if (!hasLocations && !hasCountReadings && !hasPathogens && !hasResults) {
    return (
      <Box sx={{ p: 2, border: "1px solid #e5e7eb", borderRadius: 1.5, bgcolor: "#f9fafb" }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700, color: "#1f2937", mb: 0.5 }}>Final Result</Typography>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>No result recorded yet.</Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ p: 2, border: "1px solid #e5e7eb", borderRadius: 1.5, bgcolor: "#f9fafb" }}>
      <Typography sx={{ fontSize: 13, fontWeight: 700, color: "#1f2937", mb: 1.5 }}>
        Final Result
      </Typography>

      {hasLocations && <LocationResultsTable locations={order.locations} />}

      {!hasLocations && hasCountReadings && (
        <Stack spacing={1.5}>
          {order.countTestReadings.map((r, idx) => (
            <Box
              key={idx}
              sx={{
                display: "grid",
                gridTemplateColumns: {
                  xs: "1fr",
                  sm: "repeat(2, 1fr)",
                  md: "repeat(5, 1fr)"
                },
                gap: 1.5
              }}
            >
              <SummaryField label="Plate Readings" value={r.plateReadings} />
              <SummaryField label="Dilution Factor" value={r.dilutionFactor} />
              <SummaryField label="Average" value={r.average} />
              <SummaryField label="Calculated" value={r.calculatedResult} />
              <SummaryField label="Reported Result" value={r.reportedResult} highlight />
              <SummaryField label="Alert / Action / Spec" value={`${r.alertLimit ?? "—"} / ${r.actionLimit ?? "—"} / ${r.specLimit ?? "—"}`} />
              <Box>
                <Typography sx={{ fontSize: 11, fontWeight: 600, color: "text.secondary", textTransform: "uppercase" }}>Status</Typography>
                <Box sx={{ mt: 0.5 }}>
                  <StatusBadge status={r.status} />
                </Box>
              </Box>
              <SummaryField label="Entered By" value={r.enteredByName} />
              <SummaryField label="Entered At" value={formatDate(r.enteredAt)} />
            </Box>
          ))}
        </Stack>
      )}

      {hasPathogens && (
        <Stack spacing={1} sx={{ mt: hasCountReadings ? 1.5 : 0 }}>
          {order.pathogenObservations.map((p, idx) => (
            <Box
              key={idx}
              sx={{
                display: "grid",
                gridTemplateColumns: {
                  xs: "1fr",
                  sm: "repeat(2, 1fr)",
                  md: "repeat(4, 1fr)"
                },
                gap: 1.5,
                p: 1.25,
                bgcolor: "#ffffff",
                border: "1px solid #e5e7eb",
                borderRadius: 1
              }}
            >
              <SummaryField label="Step" value={p.stepName} />
              <SummaryField label="Observation" value={pathogenObservationLabel(p.observation)} highlight />
              <SummaryField label="Observed By" value={p.observedByName} />
              <SummaryField label="Observed At" value={formatDate(p.observedAt)} />
            </Box>
          ))}
        </Stack>
      )}

      {hasResults && (
        <Stack spacing={1} sx={{ mt: (hasCountReadings || hasPathogens) ? 1.5 : 0 }}>
          {order.results.map((r, idx) => (
            <Box
              key={idx}
              sx={{
                display: "grid",
                gridTemplateColumns: {
                  xs: "1fr",
                  sm: "repeat(3, 1fr)"
                },
                gap: 1.5,
                p: 1.25,
                bgcolor: "#ffffff",
                border: "1px solid #e5e7eb",
                borderRadius: 1
              }}
            >
              <SummaryField label="Result" value={r.interpretedValue ?? r.rawValue} highlight />
              <SummaryField label="Entered By" value={r.enteredByName} />
              <SummaryField label="Entered At" value={formatDate(r.enteredAt)} />
            </Box>
          ))}
        </Stack>
      )}
    </Box>
  );
}

// 2. Test Results Section
function TestResultsSection({ testOrders, overallStatus }: { testOrders: TestOrderSummaryDetail[]; overallStatus: string }) {
  return (
    <Box>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
        <Typography sx={{ fontWeight: 700, fontSize: 16, color: brandColors.sectionTitle }}>
          Test Results
        </Typography>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary" }}>
            Overall Status:
          </Typography>
          <StatusBadge status={overallStatus} />
        </Box>
      </Box>

      <Stack spacing={2}>
        {testOrders.map((order, i) => (
          <Accordion
            key={order.testOrderId}
            defaultExpanded={i === 0 && !order.isSuperseded}
            sx={{
              border: "1px solid #e5e7eb",
              borderRadius: "8px !important",
              overflow: "hidden",
              boxShadow: "0 1px 3px rgba(0,0,0,0.04)",
              "&:before": { display: "none" }
            }}
          >
            <AccordionSummary
              expandIcon={<ExpandMoreIcon />}
              sx={{ bgcolor: "#fafafa", px: 2.5, py: 0.5, borderBottom: "1px solid #f3f4f6" }}
            >
              <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap">
                <Typography sx={{ fontWeight: 700, fontSize: 14, color: "#1f2937" }}>
                  {order.testCode} — {order.testDisplayName}
                </Typography>
                <StatusBadge status={order.workflowStateDisplay || order.status} />
                {order.isSuperseded && <StatusBadge status="Superseded" />}
              </Stack>
            </AccordionSummary>
            <AccordionDetails sx={{ p: 2.5 }}>
              <Stack spacing={2}>
                {/* Incubation Stages */}
                {order.incubations.map((inc, idx) => (
                  <IncubationStageBlock
                    key={idx}
                    inc={inc}
                    totalStages={order.incubations.length}
                    stageIndex={idx}
                  />
                ))}

                {/* Final Result */}
                <FinalResultBlock order={order} />
              </Stack>
            </AccordionDetails>
          </Accordion>
        ))}
      </Stack>
    </Box>
  );
}

// 3. Bottom Information Area: Column 1 - Step History
function StepHistoryCard({ testOrders }: { testOrders: TestOrderSummaryDetail[] }) {
  // Aggregate all step history entries across test orders
  const allEvents = useMemo(() => {
    const list: {
      testCode: string;
      fromStep: string;
      toStep: string;
      performedByName: string;
      timestamp: string;
      note: string | null;
    }[] = [];

    testOrders.forEach((o) => {
      o.workflowHistory.forEach((w) => {
        list.push({
          testCode: o.testCode,
          fromStep: w.fromStep,
          toStep: w.toStep,
          performedByName: w.performedByName,
          timestamp: w.timestamp,
          note: w.note
        });
      });
    });

    return list.sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime());
  }, [testOrders]);

  return (
    <Paper sx={{ p: 2.5, border: "1px solid #e5e7eb", borderRadius: 2, height: "100%", bgcolor: "#ffffff" }}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 2 }}>
        <HistoryOutlinedIcon sx={{ color: brandColors.sectionTitle, fontSize: 20 }} />
        <Typography sx={{ fontWeight: 700, fontSize: 14, color: brandColors.sectionTitle }}>
          Step History
        </Typography>
      </Box>

      {allEvents.length === 0 ? (
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>No step transitions recorded.</Typography>
      ) : (
        <Stack spacing={2} sx={{ position: "relative", pl: 1 }}>
          {allEvents.map((w, idx) => (
            <Box key={idx} sx={{ display: "flex", gap: 1.5, position: "relative" }}>
              <Box
                sx={{
                  width: 8,
                  height: 8,
                  borderRadius: "50%",
                  bgcolor: brandColors.sectionTitle,
                  mt: 0.6,
                  flexShrink: 0
                }}
              />
              <Box sx={{ minWidth: 0 }}>
                <Typography sx={{ fontSize: 12, fontWeight: 700, color: "#1f2937" }}>
                  {w.fromStep} → {w.toStep}
                </Typography>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                  {w.performedByName} · {formatDate(w.timestamp)}
                </Typography>
                {w.note && (
                  <Typography sx={{ fontSize: 11, color: "#4b5563", fontStyle: "italic", mt: 0.25 }}>
                    "{w.note}"
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

// 3. Bottom Information Area: Column 2 - Workflow History
function WorkflowHistoryCard({ summary }: { summary: SampleSummary }) {
  return (
    <Paper sx={{ p: 2.5, border: "1px solid #e5e7eb", borderRadius: 2, height: "100%", bgcolor: "#ffffff" }}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 2 }}>
        <TimelineOutlinedIcon sx={{ color: brandColors.sectionTitle, fontSize: 20 }} />
        <Typography sx={{ fontWeight: 700, fontSize: 14, color: brandColors.sectionTitle }}>
          Workflow History
        </Typography>
      </Box>

      {summary.timeline.length === 0 ? (
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>No lifecycle events yet.</Typography>
      ) : (
        <Stack spacing={2} sx={{ pl: 1 }}>
          {summary.timeline.map((e, idx) => (
            <Box key={idx} sx={{ display: "flex", gap: 1.5 }}>
              <Box
                sx={{
                  color: e.decision === "Reject" ? brandColors.err : brandColors.ok,
                  display: "flex",
                  alignItems: "center",
                  mt: 0.2,
                  flexShrink: 0
                }}
              >
                <CheckCircleOutlineIcon sx={{ fontSize: 15 }} />
              </Box>
              <Box sx={{ minWidth: 0 }}>
                <Typography sx={{ fontSize: 12, fontWeight: 700, color: "#1f2937" }}>
                  {e.eventType}{e.decision ? ` (${e.decision})` : ""}
                </Typography>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                  {e.performedByName} · {formatDate(e.timestamp)}
                </Typography>
                {e.comment && (
                  <Typography sx={{ fontSize: 11, color: "#4b5563", fontStyle: "italic", mt: 0.25 }}>
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

// 3. Bottom Information Area: Column 3 - Approval & Electronic Signatures
function ApprovalSignaturesCard({
  summary,
  canReview,
  canApprove,
  comment,
  setComment,
  decision,
  setDecision,
  onReviewClick,
  onApproveClick
}: {
  summary: SampleSummary;
  canReview: boolean;
  canApprove: boolean;
  comment: string;
  setComment: (c: string) => void;
  decision: SampleApprovalDecision;
  setDecision: (d: SampleApprovalDecision) => void;
  onReviewClick: () => void;
  onApproveClick: () => void;
}) {
  const hasSignatures = summary.signatures.length > 0;
  const isApproved = summary.status === "Approved";
  const isRejected = summary.status === "Rejected";

  return (
    <Paper sx={{ p: 2.5, border: "1px solid #e5e7eb", borderRadius: 2, height: "100%", bgcolor: "#ffffff" }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <VerifiedUserOutlinedIcon sx={{ color: brandColors.sectionTitle, fontSize: 20 }} />
          <Typography sx={{ fontWeight: 700, fontSize: 14, color: brandColors.sectionTitle }}>
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
                bgcolor: "#f9fafb",
                borderRadius: 1.5,
                border: "1px solid #e5e7eb"
              }}
            >
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 0.5 }}>
                <Typography sx={{ fontSize: 12, fontWeight: 700, color: "#1f2937" }}>
                  {sig.role}: {sig.printedName} ({sig.username})
                </Typography>
                <Chip
                  size="small"
                  icon={<CheckCircleOutlineIcon sx={{ fontSize: "14px !important" }} />}
                  label="Verified"
                  sx={{
                    bgcolor: "#ecfdf5",
                    color: "#065f46",
                    fontWeight: 700,
                    fontSize: 10,
                    height: 20
                  }}
                />
              </Box>
              <Typography sx={{ fontSize: 11, color: "text.secondary", mb: 0.5 }}>
                {formatExactTime(sig.signedAt)}
              </Typography>
              <Typography sx={{ fontSize: 11, color: "#4b5563", fontStyle: "italic" }}>
                "{SIGNATURE_STATEMENTS[sig.meaning] ?? sig.meaning}"
              </Typography>
              {sig.comment && (
                <Typography sx={{ fontSize: 11, color: "#374151", mt: 0.5 }}>
                  Comment: {sig.comment}
                </Typography>
              )}
            </Box>
          ))}
        </Stack>
      ) : isApproved || isRejected ? (
        <Stack spacing={1.5} sx={{ mb: 2 }}>
          {summary.reviewedByName && (
            <Box sx={{ p: 1.5, bgcolor: "#f9fafb", borderRadius: 1.5, border: "1px solid #e5e7eb" }}>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "#1f2937" }}>
                Reviewer: {summary.reviewedByName}
              </Typography>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                {formatExactTime(summary.reviewedAt)} · Verified
              </Typography>
              <Typography sx={{ fontSize: 11, color: "#4b5563", fontStyle: "italic", mt: 0.5 }}>
                "{SIGNATURE_STATEMENTS.Reviewed}"
              </Typography>
            </Box>
          )}
          {summary.approvedByName && (
            <Box sx={{ p: 1.5, bgcolor: "#f9fafb", borderRadius: 1.5, border: "1px solid #e5e7eb" }}>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "#1f2937" }}>
                Section Head: {summary.approvedByName}
              </Typography>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                {formatExactTime(summary.approvedAt)} · Verified
              </Typography>
              <Typography sx={{ fontSize: 11, color: "#4b5563", fontStyle: "italic", mt: 0.5 }}>
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
        <Box sx={{ mt: 2, pt: 2, borderTop: "1px solid #e5e7eb" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1, color: brandColors.sectionTitle }}>
            Submit Technical Review
          </Typography>
          <Alert severity="info" sx={{ fontSize: 11, py: 0.5, mb: 1.5 }}>
            By submitting, I confirm I have reviewed all test results for this sample.
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
            fullWidth
            onClick={onReviewClick}
            sx={{ bgcolor: brandColors.sectionTitle, "&:hover": { bgcolor: brandColors.pageTitle } }}
          >
            Submit Review
          </Button>
        </Box>
      )}

      {/* Approval Action Form */}
      {canApprove && (
        <Box sx={{ mt: 2, pt: 2, borderTop: "1px solid #e5e7eb" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1, color: brandColors.sectionTitle }}>
            Submit Release Decision
          </Typography>
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
          <Button
            variant="contained"
            fullWidth
            onClick={onApproveClick}
            sx={{ bgcolor: brandColors.sectionTitle, "&:hover": { bgcolor: brandColors.pageTitle } }}
          >
            Submit Decision
          </Button>
        </Box>
      )}
    </Paper>
  );
}

export function SampleSummaryDialog({ open, sampleId, onClose }: Props) {
  const { role } = useAuth();
  const [summary, setSummary] = useState<SampleSummary | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [comment, setComment] = useState("");
  const [decision, setDecision] = useState<SampleApprovalDecision>("Approve");
  const [confirmingReview, setConfirmingReview] = useState(false);
  const [confirmingDecision, setConfirmingDecision] = useState(false);
  const [openPathogenDialog, setOpenPathogenDialog] = useState(false);
  const [exporting, setExporting] = useState<"pdf" | "word" | null>(null);
  const [exportError, setExportError] = useState<string | null>(null);

  useEffect(() => {
    if (open && sampleId) {
      setSummary(null);
      setLoadError(null);
      setComment("");
      setDecision("Approve");
      SampleSummaryService.getSummary(sampleId)
        .then(setSummary)
        .catch((e) => {
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

  const canReview =
    summary?.status === "UnderReview" &&
    (role === "Reviewer" || role === "SectionHead" || role === "SystemAdministrator");
  const canApprove =
    summary?.status === "UnderApproval" &&
    (role === "SectionHead" || role === "SystemAdministrator");

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

    const allPathogens = summary.testOrders.flatMap((t) => t.pathogenObservations);
    if (allPathogens.some((p) => p.observation === "GrowthConforming")) return "Detected";

    return summary.status || "WithinLimits";
  }, [summary]);

  return (
    <>
      <FloatingDialog
        open={open && !confirmingReview && !confirmingDecision}
        title="Sample Summary"
        onClose={onClose}
      >
        {loadError && <Alert severity="error" sx={{ mb: 2 }}>{loadError}</Alert>}
        {!summary && !loadError && <LoadingSpinner />}
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
                <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                  Complete overview of test execution, incubation stages, results, and approvals.
                </Typography>
              </Box>
              <Stack direction="row" spacing={1} sx={{ flexShrink: 0, flexWrap: "wrap", gap: 1 }}>
                {exportError && <Alert severity="error" sx={{ py: 0, px: 1 }}>{exportError}</Alert>}
                <Button
                  variant="contained"
                  size="small"
                  startIcon={<ScienceOutlinedIcon />}
                  onClick={() => setOpenPathogenDialog(true)}
                  sx={{
                    bgcolor: brandColors.sectionTitle,
                    color: "#ffffff",
                    fontWeight: 700,
                    fontSize: 12,
                    "&:hover": { bgcolor: brandColors.pageTitle }
                  }}
                >
                  Open Pathogen Workflow
                </Button>
                <Button
                  variant="outlined"
                  size="small"
                  startIcon={<PrintIcon />}
                  onClick={() => window.open(`/samples/${sampleId}/report`, "_blank", "noopener")}
                  sx={{ borderColor: "#d1d5db", color: "#374151" }}
                >
                  Printable Report
                </Button>
                <Button
                  variant="outlined"
                  size="small"
                  startIcon={<PictureAsPdfIcon />}
                  disabled={!!exporting}
                  onClick={() => handleExport("pdf")}
                  sx={{ borderColor: "#d1d5db", color: "#374151" }}
                >
                  {exporting === "pdf" ? "Exporting…" : "Export PDF"}
                </Button>
                <Button
                  variant="outlined"
                  size="small"
                  startIcon={<DescriptionIcon />}
                  disabled={!!exporting}
                  onClick={() => handleExport("word")}
                  sx={{ borderColor: "#d1d5db", color: "#374151" }}
                >
                  {exporting === "word" ? "Exporting…" : "Export Word"}
                </Button>
              </Stack>
            </Box>

            {/* 1. Sample Identity */}
            <SampleIdentityCard summary={summary} />

            {/* Preparation (if available) */}
            {summary.preparation && <SamplePreparationCard preparation={summary.preparation} />}

            {/* 2. Test Results */}
            <TestResultsSection testOrders={summary.testOrders} overallStatus={overallStatus} />

            {/* 3. Bottom Three-Column Information Area */}
            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: {
                  xs: "1fr",
                  md: "repeat(3, 1fr)"
                },
                gap: 2.5
              }}
            >
              <StepHistoryCard testOrders={summary.testOrders} />
              <WorkflowHistoryCard summary={summary} />
              <ApprovalSignaturesCard
                summary={summary}
                canReview={!!canReview}
                canApprove={!!canApprove}
                comment={comment}
                setComment={setComment}
                decision={decision}
                setDecision={setDecision}
                onReviewClick={() => setConfirmingReview(true)}
                onApproveClick={() => setConfirmingDecision(true)}
              />
            </Box>

            {/* 4. Full-Width Open Printable Report Button */}
            <Box sx={{ pt: 1 }}>
              <Button
                variant="contained"
                fullWidth
                size="large"
                startIcon={<PrintIcon />}
                onClick={() => window.open(`/samples/${sampleId}/report`, "_blank", "noopener")}
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
            </Box>

            {/* 5. Controlled Document Footer Information */}
            <Box
              sx={{
                textAlign: "center",
                py: 1,
                borderTop: "1px solid #f3f4f6",
                color: "text.secondary"
              }}
            >
              <Typography sx={{ fontSize: 11 }}>
                Sample Reference: <strong>{summary.referenceNumber}</strong> · Record Status: <strong>{summary.status}</strong> · MicroLIMS Operational View
              </Typography>
            </Box>
          </Stack>
        )}
      </FloatingDialog>

      {/* Signature Confirmation Dialogs */}
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
