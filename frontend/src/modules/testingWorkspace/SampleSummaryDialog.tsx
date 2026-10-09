import { useEffect, useMemo, useState } from "react";
import { Box, Typography, Stack, TextField, Button, Alert, Radio, RadioGroup, FormControlLabel, Accordion, AccordionSummary, AccordionDetails, Table, TableHead, TableRow, TableCell, TableBody, Paper, Chip, useTheme, Checkbox, FormGroup, Select, MenuItem, InputLabel, FormControl, TableContainer } from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import PrintIcon from "@mui/icons-material/Print";
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import VerifiedUserOutlinedIcon from "@mui/icons-material/VerifiedUserOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import TimelineOutlinedIcon from "@mui/icons-material/TimelineOutlined";
import AssignmentReturnOutlinedIcon from "@mui/icons-material/AssignmentReturnOutlined";
import { Link } from "react-router-dom";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import { ReturnToAnalystDialog } from "../../components/ReturnToAnalystDialog";
import { DialogBodySkeleton } from "../../components/DialogBodySkeleton";
import { StatusBadge } from "../../components/StatusBadge";
import { brandColors } from "../../theme";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { SampleSummaryService, SampleApprovalDecision } from "./services/SampleSummaryService";
import { sectionScope } from "./coaAggregation";
import { SampleSummary, TestOrderSummaryDetail, SampleSectionSummaryDetail, AnalysisDetail, ParameterResultDetail, ResultReadingDetail } from "./types/sampleSummaryTypes";
import { pathogenObservationLabel } from "./utils/pathogenObservationLabel";
import { HplcReviewPanel } from "../hplcWorkspace/review/HplcReviewPanel";
import { IcpReviewPanel } from "./icp/IcpReviewPanel";
import { isIcpCalculation } from "./icp/icpReviewTypes";
import { PathogenSessionDialog } from "./pathogenSession/PathogenSessionDialog";
import { UserService, UserRecord } from "../users/services/UserService";
import { CloseTestingDialog } from "../approval/CloseTestingDialog";
import { humanize } from "./SampleReportPage";
import { parseTitrationSnapshot } from "./utils/titrationSnapshot";
import { decimalsOf, withoutUnit } from "./utils/resultDisplay";
import { DECISION_OPTIONS, SIGNATURE_STATEMENTS, formatDate, formatExactTime, formatSectionStatus, isTestOrderNonPassing } from "./sampleSummary/summaryFormatting";
import { IncubationStageBlock, LocationResultsTable, SampleIdentityCard, SamplePreparationCard, SummaryField } from "./sampleSummary/SummaryCards";

interface Props {
  open: boolean;
  sampleId: number | null;
  onClose: () => void;
}

const num = (v: number | null | undefined, digits?: number) =>
  v === null || v === undefined ? "—" : digits === undefined ? String(v) : v.toFixed(digits);

function AnalysisResultBlock({ analysis }: { analysis: AnalysisDetail }) {
  const cellSx = { fontSize: 12, py: 0.75 };
  const headSx = { fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" as const, py: 0.75 };

  let conditionsDisplay: string | null = null;
  const titration = analysis.analysisType === "Titration" ? parseTitrationSnapshot(analysis.conditionsJson) : null;
  if (titration) {
    conditionsDisplay =
      [
        titration.titrantCode ? `Titrant: ${titration.titrantCode}` : null,
        titration.factor != null ? `Factor used: ${titration.factor.toFixed(4)}${titration.factorState ? ` (${humanize(titration.factorState)})` : ""}` : null,
        titration.warnings.length > 0 ? `Warnings: ${titration.warnings.join("; ")}` : null,
        titration.dueTitrantAcknowledgement?.justification
          ? `Due titrant acknowledged: ${titration.dueTitrantAcknowledgement.justification}`
          : null
      ].filter(Boolean).join(" · ") || null;
  } else if (analysis.conditionsJson) {
    try {
      const parsed = JSON.parse(analysis.conditionsJson);
      if (typeof parsed === "object" && parsed !== null && !Array.isArray(parsed)) {
        conditionsDisplay = Object.entries(parsed).map(([k, v]) => `${k}: ${String(v)}`).join(" · ");
      } else {
        conditionsDisplay = JSON.stringify(parsed);
      }
    } catch {
      conditionsDisplay = analysis.conditionsJson;
    }
  }

  return (
    <Box sx={{ p: 2, border: "1px solid", borderColor: "divider", borderRadius: 1.5, bgcolor: "background.default" }}>
      <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1.5 }}>Analysis Results</Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(4, 1fr)" }, gap: 1.5, mb: 1.5 }}>
        <SummaryField label="Instrument" value={analysis.equipmentCode ? `${analysis.equipmentCode}${analysis.equipmentName ? ` (${analysis.equipmentName})` : ""}` : (analysis.equipmentName ?? "—")} />
        <SummaryField label="Analysis Time" value={formatDate(analysis.analysedAt)} />
        {analysis.unitAmount !== null && <SummaryField label="Unit Amount" value={num(analysis.unitAmount)} />}
        {conditionsDisplay && <SummaryField label="Conditions" value={conditionsDisplay} />}
        <SummaryField label="Entered By / At" value={`${analysis.enteredByName ?? "—"} · ${formatDate(analysis.enteredAt)}`} />
      </Box>
      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={headSx}>Parameter</TableCell>
              <TableCell sx={headSx}>Reported</TableCell>
              <TableCell sx={headSx}>Unit</TableCell>
              <TableCell sx={headSx}>Specification</TableCell>
              <TableCell sx={headSx}>Status</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {analysis.parameterResults.map((p, idx) => {
              const flags = [p.overRange ? ">Range" : null, p.belowLoq ? "<LOQ" : null].filter(Boolean).join(", ");
              return (
                <TableRow key={idx}>
                  <TableCell sx={cellSx}>{p.parameterName}</TableCell>
                  <TableCell sx={{ ...cellSx, fontWeight: 600 }}>
                    {withoutUnit(p.reportedDisplay, p.unit)}
                    {flags ? ` [${flags}]` : ""}
                  </TableCell>
                  <TableCell sx={cellSx}>{p.unit ?? "—"}</TableCell>
                  <TableCell sx={cellSx}>{p.specLimit ? `${p.specLimit}${p.unit ? ` ${p.unit}` : ""}` : "—"}</TableCell>
                  <TableCell sx={cellSx}>
                    <StatusBadge status={p.comparisonStatus} />
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>
      {analysis.analysisType === "HplcMethodAssay" &&
        analysis.parameterResults.map((p) => (
          <HplcReviewPanel
            key={`hplc-review-${p.id}`}
            parameter={p}
            testOrderId={analysis.testOrderId}
          />
        ))}
      {(analysis.analysisType === "IcpMethodAssay" || analysis.parameterResults.some((p) => isIcpCalculation(p.calculationJson))) &&
        analysis.parameterResults
          .filter((p) => isIcpCalculation(p.calculationJson) || analysis.analysisType === "IcpMethodAssay")
          .map((p) => (
            <IcpReviewPanel
              key={`icp-review-${p.id}`}
              parameter={p}
              testOrderId={analysis.testOrderId}
            />
          ))}
      {analysis.analysisType !== "HplcMethodAssay" && analysis.analysisType !== "IcpMethodAssay" &&
        analysis.parameterResults
          .filter((p) => p.readings.length > 0 && !isIcpCalculation(p.calculationJson))
          .map((p) => (
            <AnalysisReadingsTable key={p.id} parameter={p} analysisType={analysis.analysisType} />
          ))}
    </Box>
  );
}

// Raw readings behind one parameter result; only columns with a value are shown.
function AnalysisReadingsTable({ parameter, analysisType }: { parameter: ParameterResultDetail; analysisType?: string }) {
  const cellSx = { fontSize: 12, py: 0.5 };
  const headSx = { fontSize: 12, fontWeight: 700, color: "text.secondary", py: 0.5 };
  const r = parameter.readings;
  const isVessel = r.some((x) => x.kind === "Vessel");
  const isDisintegration = analysisType === "Disintegration";
  const isWeightVariation = analysisType === "WeightVariation";
  const isTitration = analysisType === "Titration";
  const isTablet = isWeightVariation && r.every((x) => x.value2 == null);
  type ReadingColumn = { label: string; get: (x: ResultReadingDetail) => string | null };
  const allCols: ReadingColumn[] = [
    {
      label: "Stage",
      get: (x) =>
        x.stage != null
          ? isVessel || isDisintegration || isWeightVariation
            ? `S${x.stage}`
            : String(x.stage)
          : null
    },
    {
      label: "Time (min)",
      get: (x) => (!isDisintegration && !isWeightVariation && x.timePointMinutes != null ? num(x.timePointMinutes) : null)
    },
    {
      label: isVessel
        ? "Peak Area"
        : isDisintegration
        ? "Time (min)"
        : isWeightVariation
        ? (isTablet ? "Weight (mg)" : "Gross (mg)")
        : isTitration
        ? "Sample weight (mg)"
        : "Value 1",
      get: (x) => (x.value1 != null ? num(x.value1) : isDisintegration && x.text ? x.text : null)
    },
    {
      label: isWeightVariation ? "Shell (mg)" : isTitration ? "Titrant volume (mL)" : "Value 2",
      get: (x) => (!isTablet && x.value2 != null ? num(x.value2) : null)
    },
    {
      label: isWeightVariation ? "Deviation %" : isTitration ? "Corrected volume (mL)" : "Value 3",
      get: (x) => (x.value3 != null ? (isWeightVariation ? `${num(x.value3)} %` : num(x.value3)) : null)
    },
    {
      label: "Text",
      get: (x) => (isDisintegration || isWeightVariation ? null : (x.text || null))
    },
    {
      label: isVessel ? "% Dissolved" : isWeightVariation ? "Net (mg)" : isTitration ? "Result" : "Computed",
      get: (x) =>
        isDisintegration || isTablet
          ? null
          : x.computedValue != null
          ? x.kind === "Vessel"
            ? `${num(x.computedValue)} %`
            : num(x.computedValue, isTitration ? decimalsOf(parameter.reportedDisplay) : undefined)
          : null
    },
    {
      label: "Pass",
      get: (x) => (x.passed == null ? null : x.passed ? "Pass" : "Fail")
    }
  ];
  const cols = allCols.filter((c) => r.some((x) => c.get(x) !== null));

  return (
    <Box sx={{ mt: 1.5 }}>
      <Typography sx={{ fontSize: 12, fontWeight: 700, mb: 0.5 }}>Readings · {parameter.parameterName}</Typography>
      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={headSx}>#</TableCell>
              {cols.map((c) => <TableCell key={c.label} sx={headSx}>{c.label}</TableCell>)}
            </TableRow>
          </TableHead>
          <TableBody>
            {r.map((x) => (
              <TableRow key={x.id}>
                <TableCell sx={cellSx}>{x.index}</TableCell>
                {cols.map((c) => <TableCell key={c.label} sx={cellSx}>{c.get(x) ?? "—"}</TableCell>)}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
}

// Final Result Section component (Separated from Incubation)
function FinalResultBlock({ order }: { order: TestOrderSummaryDetail }) {
  if (order.analysis) return <AnalysisResultBlock analysis={order.analysis} />;

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
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary", textTransform: "uppercase" }}>Status</Typography>
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
                <Stack useFlexGap
                  direction="row"
                  spacing={1.5}
                  sx={{
                    alignItems: "center",
                    flexWrap: "wrap"
                  }}>
                  <Typography sx={{ fontWeight: 700, fontSize: 14, color: "text.primary" }}>
                    {order.testCode}: {order.testDisplayName}
                  </Typography>
                  {order.sectionName && (
                    <Chip
                      label={order.sectionName}
                      size="small"
                      variant="outlined"
                      sx={{ fontSize: 12, height: 20 }}
                    />
                  )}
                  <StatusBadge status={order.workflowStateDisplay || order.status} />
                  {order.isSuperseded && <StatusBadge status="Superseded" />}
                  {/* Closed-testing cancellation (design.md §5.3): the step
                      (and incubation stage, if any) this test had reached
                      when another lab's rejection closed this one's work. */}
                  {order.status === "Cancelled" && order.cancelledAtStep && (
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      Cancelled at {humanize(order.cancelledAtStep)}
                      {order.cancelledAtStage != null ? `, stage ${order.cancelledAtStage}` : ""}
                    </Typography>
                  )}
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
