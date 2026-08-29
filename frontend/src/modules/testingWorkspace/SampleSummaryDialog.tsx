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
  Chip,
  useTheme,
  Checkbox,
  FormGroup,
  Select,
  MenuItem,
  InputLabel,
  FormControl
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import PrintIcon from "@mui/icons-material/Print";
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import VerifiedUserOutlinedIcon from "@mui/icons-material/VerifiedUserOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import TimelineOutlinedIcon from "@mui/icons-material/TimelineOutlined";
import AssignmentReturnOutlinedIcon from "@mui/icons-material/AssignmentReturnOutlined";
import { Link } from "react-router-dom";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import { ReturnToAnalystDialog } from "../../components/ReturnToAnalystDialog";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { StatusBadge } from "../../components/StatusBadge";
import { brandColors } from "../../theme";
import { useAuth } from "../../contexts/AuthContext";
import { SampleSummaryService, SampleApprovalDecision } from "./services/SampleSummaryService";
import { buildCoaMatrix, buildCoaSimpleRows } from "./coaAggregation";
import {
  SampleSummary,
  TestOrderSummaryDetail,
  SampleLocationDetail,
  IncubationDetail,
  SignatureTrailItem
} from "./types/sampleSummaryTypes";
import { pathogenObservationLabel } from "./utils/pathogenObservationLabel";
import { PathogenSessionDialog } from "./pathogenSession/PathogenSessionDialog";
import { UserService, UserRecord } from "../users/services/UserService";

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

// Advisory only - pre-checks the retest checklist toward whichever tests
// actually failed, mirroring overallStatus's own per-sample rollup below
// but scoped to a single TestOrder. The Section Head can still freely
// check/uncheck anything; the backend is the actual authority on which
// TestOrderIds are valid to retest (it just requires "at least one").
function isTestOrderNonPassing(order: TestOrderSummaryDetail): boolean {
  if (order.locations.some((l) => l.status && l.status !== "WithinLimits" && l.status !== "Absent")) return true;
  if (order.countTestReadings.some((r) => r.status !== "WithinLimits")) return true;
  const biochemical = order.biochemicalResults;
  if (biochemical.some((b) => b.organismDetected === true)) return true;
  const pathogens = order.pathogenObservations;
  const anyConforming = pathogens.some((p) => p.observation === "GrowthConforming");
  const biochemicalRulesOutAll = biochemical.length > 0 && biochemical.every((b) => b.organismDetected === false);
  if (anyConforming && !biochemicalRulesOutAll) return true;
  return false;
}

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
          color: highlight ? brandColors.sectionTitle : "text.primary",
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
  const theme = useTheme();
  return (
    <Paper sx={{ p: 2.5, border: "1px solid", borderColor: "divider", borderRadius: 2, bgcolor: "background.paper" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 15, color: theme.palette.primary.main, mb: 2 }}>
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
        <SummaryField label="Assigned Analyst" value={s.assignedAnalystName ?? "Unassigned"} highlight={Boolean(s.assignedAnalystName)} />
        {s.category === "AfterCleaning" && (
          <>
            <SummaryField label="Previous Product" value={s.previousProductName || "—"} highlight />
            <SummaryField label="Previous Product Batch" value={s.previousProductBatchNumber || s.batchNumber || "—"} highlight />
          </>
        )}
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
  const theme = useTheme();
  return (
    <Paper sx={{ p: 2.5, border: "1px solid", borderColor: "divider", borderRadius: 2, bgcolor: "background.paper" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 15, color: theme.palette.primary.main, mb: 2 }}>
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
        border: "1px solid",
        borderColor: "divider",
        bgcolor: "background.default"
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
        <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary" }}>
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
  const severityOrder = ["WithinLimits", "Absent", "LimitsNotConfigured", "AlertLimitExceeded", "ActionLimitExceeded", "OutOfSpecification", "Detected"];
  const worstStatus = locations.reduce<string>((worst, l) => {
    if (!l.status) return worst;
    return severityOrder.indexOf(l.status) > severityOrder.indexOf(worst) ? l.status : worst;
  }, "WithinLimits");
  // Unit comes from the location data (set at result-entry time) - EM/
  // After Cleaning/Water mix CFU/plate/4 hours, CFU/25 cm2, and CFU/mL
  // depending on sampling method, never a single assumed "CFU".
  const unit = locations.find((l) => l.unit)?.unit ?? "CFU";

  return (
    <Box>
      <Table size="small" sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
        <TableHead sx={{ bgcolor: "background.default" }}>
          <TableRow>
            <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Location</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Limits (Alert / Action / Spec)</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>{unit}</TableCell>
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
  const hasBiochemical = order.biochemicalResults.length > 0;
  const hasResults = order.results.length > 0;

  if (!hasLocations && !hasCountReadings && !hasPathogens && !hasBiochemical && !hasResults) {
    return (
      <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5, bgcolor: "background.default" }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary", mb: 0.5 }}>Final Result</Typography>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>No result recorded yet.</Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5, bgcolor: "background.default" }}>
      <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary", mb: 1.5 }}>
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
                bgcolor: "background.paper",
                border: "1px solid",
                borderColor: "divider",
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

      {hasBiochemical && (
        <Stack spacing={1} sx={{ mt: (hasCountReadings || hasPathogens) ? 1.5 : 0 }}>
          {order.biochemicalResults.map((b, idx) => (
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
                bgcolor: "background.paper",
                border: "1px solid",
                borderColor: "divider",
                borderRadius: 1
              }}
            >
              <SummaryField label="Step" value={b.stepName} />
              <SummaryField
                label="Interpretation"
                value={b.organismDetected === true ? "Detected" : b.organismDetected === false ? "Not Detected" : "Undetermined"}
                highlight
              />
              <SummaryField label="Result" value={b.biochemicalResultText} />
              <SummaryField label="Submitted By" value={b.submittedByName} />
            </Box>
          ))}
        </Stack>
      )}

      {hasResults && (
        <Stack spacing={1} sx={{ mt: (hasCountReadings || hasPathogens || hasBiochemical) ? 1.5 : 0 }}>
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
                bgcolor: "background.paper",
                border: "1px solid",
                borderColor: "divider",
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
function TestResultsSection({
  testOrders,
  overallStatus,
  canReturn,
  onReturnTest
}: {
  testOrders: TestOrderSummaryDetail[];
  overallStatus: string;
  canReturn?: boolean;
  onReturnTest?: (order: TestOrderSummaryDetail) => void;
}) {
  const theme = useTheme();
  return (
    <Box>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
        <Typography sx={{ fontWeight: 700, fontSize: 16, color: theme.palette.primary.main }}>
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
        {testOrders.map((order, i) => {
          const isCountTest =
            (order.countTestReadings.length > 0 ||
              (order.results.length > 0 && order.biochemicalResults.length === 0 && order.locations.length === 0)) &&
            order.pathogenObservations.length === 0;
          const canReturnOrder = Boolean(
            canReturn && !order.isSuperseded && order.status === "ResultEntered" && isCountTest
          );

          return (
            <Accordion
              key={order.testOrderId}
              defaultExpanded={!order.isSuperseded && (i === 0 || order.status === "ResultEntered")}
              sx={{
                border: "1px solid",
                borderColor: "divider",
                borderRadius: "8px !important",
                overflow: "hidden",
                "&:before": { display: "none" }
              }}
            >
              <AccordionSummary
                expandIcon={<ExpandMoreIcon />}
                sx={{ bgcolor: "background.default", px: 2.5, py: 0.5, borderBottom: "1px solid", borderBottomColor: "divider" }}
              >
                <Stack direction="row" spacing={1.5} alignItems="center" flexWrap="wrap">
                  <Typography sx={{ fontWeight: 700, fontSize: 14, color: "text.primary" }}>
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

                  {/* Return to Analyst Action */}
                  {canReturnOrder && (
                    <Box sx={{ display: "flex", justifyContent: "flex-end", pt: 0.5 }}>
                      <Button
                        variant="outlined"
                        color="warning"
                        size="small"
                        startIcon={<AssignmentReturnOutlinedIcon />}
                        onClick={() => onReturnTest?.(order)}
                        sx={{
                          fontWeight: 600,
                          fontSize: 12,
                          borderColor: "warning.main",
                          color: "warning.dark",
                          "&:hover": {
                            borderColor: "warning.dark",
                            bgcolor: "rgba(237, 108, 2, 0.08)"
                          }
                        }}
                      >
                        Return to Analyst
                      </Button>
                    </Box>
                  )}
                </Stack>
              </AccordionDetails>
            </Accordion>
          );
        })}
      </Stack>
    </Box>
  );
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
        <Stack direction="row" spacing={0} sx={{ overflowX: "auto", pb: 0.5 }}>
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
                  {e.eventType}{e.decision ? ` (${e.decision})` : ""}
                </Typography>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                  {e.performedByName}
                </Typography>
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                  {formatDate(e.timestamp)}
                </Typography>
                {e.comment && (
                  <Typography sx={{ fontSize: 11, color: "text.secondary", fontStyle: "italic", mt: 0.25 }}>
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
  certificateRemarks,
  setCertificateRemarks,
  selectedTestOrderIds,
  setSelectedTestOrderIds,
  analysts,
  newSampleAnalystOneId,
  setNewSampleAnalystOneId,
  newSampleAnalystTwoId,
  setNewSampleAnalystTwoId,
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
  certificateRemarks: string;
  setCertificateRemarks: (r: string) => void;
  selectedTestOrderIds: number[];
  setSelectedTestOrderIds: (ids: number[]) => void;
  analysts: UserRecord[];
  newSampleAnalystOneId: number | "";
  setNewSampleAnalystOneId: (id: number | "") => void;
  newSampleAnalystTwoId: number | "";
  setNewSampleAnalystTwoId: (id: number | "") => void;
  onReviewClick: () => void;
  onApproveClick: () => void;
}) {
  const theme = useTheme();
  const hasSignatures = summary.signatures.length > 0;
  const isApproved = summary.status === "Approved";
  const isRejected = summary.status === "Rejected";

  const isRetestDecision = decision === "RetestRetainedSample" || decision === "NewSampleRequest";
  const decisionValid =
    !isRetestDecision ||
    (selectedTestOrderIds.length > 0 &&
      (decision !== "NewSampleRequest" ||
        (newSampleAnalystOneId !== "" && newSampleAnalystTwoId !== "" && newSampleAnalystOneId !== newSampleAnalystTwoId)));

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
                    fontSize: 10,
                    height: 20
                  }}
                />
              </Box>
              <Typography sx={{ fontSize: 11, color: "text.secondary", mb: 0.5 }}>
                {formatExactTime(sig.signedAt)}
              </Typography>
              <Typography sx={{ fontSize: 11, color: "text.secondary", fontStyle: "italic" }}>
                "{SIGNATURE_STATEMENTS[sig.meaning] ?? sig.meaning}"
              </Typography>
              {sig.comment && (
                <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 0.5 }}>
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
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                {formatExactTime(summary.reviewedAt)} · Verified
              </Typography>
              <Typography sx={{ fontSize: 11, color: "text.secondary", fontStyle: "italic", mt: 0.5 }}>
                "{SIGNATURE_STATEMENTS.Reviewed}"
              </Typography>
            </Box>
          )}
          {summary.approvedByName && (
            <Box sx={{ p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.primary" }}>
                Section Head: {summary.approvedByName}
              </Typography>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                {formatExactTime(summary.approvedAt)} · Verified
              </Typography>
              <Typography sx={{ fontSize: 11, color: "text.secondary", fontStyle: "italic", mt: 0.5 }}>
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
        <Box sx={{ mt: 2, pt: 2, borderTop: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1, color: theme.palette.primary.main }}>
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
              <Typography sx={{ fontSize: 11, color: "text.secondary", mb: 1 }}>
                Non-conforming tests are pre-checked. Adjust as needed - only the checked test(s) move to the new sample{decision === "NewSampleRequest" ? "s" : ""}; everything else on this sample is left untouched.
              </Typography>
              <FormGroup>
                {summary.testOrders
                  .filter((t) => !t.isSuperseded)
                  .map((t) => (
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
                          {t.testDisplayName} {isTestOrderNonPassing(t) && <Chip size="small" label="Non-conforming" color="error" sx={{ ml: 0.5, height: 16, fontSize: 9 }} />}
                        </Typography>
                      }
                    />
                  ))}
              </FormGroup>
              {selectedTestOrderIds.length === 0 && (
                <Alert severity="warning" sx={{ fontSize: 11, py: 0, mt: 0.5 }}>
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
              <Typography sx={{ fontSize: 11, color: "text.secondary", mb: 1 }}>
                Two different analysts are required, and neither may be whoever tested the original sample.
              </Typography>
              <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
                <FormControl fullWidth size="small">
                  <InputLabel>New Sample 1 - Analyst</InputLabel>
                  <Select
                    label="New Sample 1 - Analyst"
                    value={newSampleAnalystOneId}
                    onChange={(e) => setNewSampleAnalystOneId(e.target.value === "" ? "" : Number(e.target.value))}
                  >
                    {analysts.map((a) => (
                      <MenuItem key={a.id} value={a.id}>{a.fullName} ({a.username})</MenuItem>
                    ))}
                  </Select>
                </FormControl>
                <FormControl fullWidth size="small">
                  <InputLabel>New Sample 2 - Analyst</InputLabel>
                  <Select
                    label="New Sample 2 - Analyst"
                    value={newSampleAnalystTwoId}
                    onChange={(e) => setNewSampleAnalystTwoId(e.target.value === "" ? "" : Number(e.target.value))}
                  >
                    {analysts.map((a) => (
                      <MenuItem key={a.id} value={a.id}>{a.fullName} ({a.username})</MenuItem>
                    ))}
                  </Select>
                </FormControl>
              </Stack>
              {newSampleAnalystOneId !== "" && newSampleAnalystOneId === newSampleAnalystTwoId && (
                <Alert severity="warning" sx={{ fontSize: 11, py: 0, mt: 0.5 }}>
                  The two new samples must be assigned to two different analysts.
                </Alert>
              )}
            </Box>
          )}

          <Button
            variant="contained"
            fullWidth
            disabled={!decisionValid}
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
  const theme = useTheme();
  const { role } = useAuth();
  const [summary, setSummary] = useState<SampleSummary | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [comment, setComment] = useState("");
  const [decision, setDecision] = useState<SampleApprovalDecision>("Approve");
  const [certificateRemarks, setCertificateRemarks] = useState("");
  const [selectedTestOrderIds, setSelectedTestOrderIds] = useState<number[]>([]);
  const [analysts, setAnalysts] = useState<UserRecord[]>([]);
  const [newSampleAnalystOneId, setNewSampleAnalystOneId] = useState<number | "">("");
  const [newSampleAnalystTwoId, setNewSampleAnalystTwoId] = useState<number | "">("");
  const [confirmingReview, setConfirmingReview] = useState(false);
  const [confirmingDecision, setConfirmingDecision] = useState(false);
  const [returningTestOrder, setReturningTestOrder] = useState<TestOrderSummaryDetail | null>(null);
  const [openPathogenDialog, setOpenPathogenDialog] = useState(false);
  const [exporting, setExporting] = useState<"pdf" | null>(null);
  const [exportError, setExportError] = useState<string | null>(null);

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
      SampleSummaryService.getSummary(sampleId)
        .then(setSummary)
        .catch((e) => {
          setLoadError(e?.response?.data?.message ?? "Failed to load sample summary.");
        });
      if (role === "SectionHead" || role === "SystemAdministrator") {
        UserService.getEligibleAnalysts().then(setAnalysts).catch(() => setAnalysts([]));
      }
    }
  }, [open, sampleId, role]);

  // Re-pre-check the retest checklist toward whichever tests are actually
  // non-conforming whenever the decision switches to a retest flavor (or
  // the summary first loads) - the Section Head can still freely adjust it.
  useEffect(() => {
    if (!summary) return;
    if (decision !== "RetestRetainedSample" && decision !== "NewSampleRequest") return;
    const nonPassing = summary.testOrders.filter((t) => !t.isSuperseded && isTestOrderNonPassing(t)).map((t) => t.testOrderId);
    setSelectedTestOrderIds(nonPassing);
  }, [decision, summary]);

  const handleReturnConfirm = async (reason?: string) => {
    if (!sampleId || !returningTestOrder) return;
    await SampleSummaryService.returnTestToAnalyst(sampleId, returningTestOrder.testOrderId, reason);
    setReturningTestOrder(null);
    const updated = await SampleSummaryService.getSummary(sampleId);
    setSummary(updated);
  };

  const handleReviewConfirm = async (password: string) => {
    if (!sampleId) return;
    await SampleSummaryService.completeReview(sampleId, password, comment || undefined);
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
      decision === "NewSampleRequest" && newSampleAnalystTwoId !== "" ? newSampleAnalystTwoId : undefined
    );
    setConfirmingDecision(false);
    onClose();
  };

  const canReview =
    summary?.status === "UnderReview" &&
    (role === "Reviewer" || role === "SectionHead" || role === "SystemAdministrator");
  const canReturn =
    role === "Reviewer" || role === "SectionHead" || role === "SystemAdministrator";
  const canApprove =
    summary?.status === "UnderApproval" &&
    (role === "SectionHead" || role === "SystemAdministrator");

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
  const coaEligible = useMemo(
    () =>
      !!summary &&
      (summary.status === "Approved" || summary.status === "Rejected") &&
      (buildCoaMatrix(summary.testOrders) !== null || buildCoaSimpleRows(summary.testOrders) !== null),
    [summary]
  );

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
              newSampleAnalystOneId={newSampleAnalystOneId}
              setNewSampleAnalystOneId={setNewSampleAnalystOneId}
              newSampleAnalystTwoId={newSampleAnalystTwoId}
              setNewSampleAnalystTwoId={setNewSampleAnalystTwoId}
              onReviewClick={() => setConfirmingReview(true)}
              onApproveClick={() => setConfirmingDecision(true)}
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
              <Typography sx={{ fontSize: 11 }}>
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
