import { useState, useMemo } from "react";
import {
  Box,
  Typography,
  Stack,
  Button,
  Paper,
  Chip,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Alert,
  Divider,
  CircularProgress,
  Collapse,
  IconButton,
  Card,
  CardContent,
  Tooltip,
  useTheme
} from "@mui/material";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import VerifiedUserOutlinedIcon from "@mui/icons-material/VerifiedUserOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import TaskAltIcon from "@mui/icons-material/TaskAlt";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import {
  PathogenTestingSessionDto,
  MatrixCellResultDto,
  SessionLocationDto,
  GrowthObservation,
  ConfirmationResult
} from "../types/pathogenSessionTypes";
import { PathogenSessionService } from "../services/PathogenSessionService";
import { SharedTsbStatusCard } from "./SharedTsbStatusCard";
import { brandColors } from "../../../theme";

interface Props {
  session: PathogenTestingSessionDto;
  onSessionCompleted: (updatedSession: PathogenTestingSessionDto) => void;
  onBackToMatrix: () => void;
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

interface CellDerivationProps {
  cell: MatrixCellResultDto | undefined;
  testCode: string;
  testDisplayName: string;
  expanded: boolean;
}

function CellDerivationDetail({ cell, testCode, testDisplayName, expanded }: CellDerivationProps) {
  const theme = useTheme();
  if (!cell || !expanded) return null;

  const isQuant = cell.resultType === "Quantitative";

  return (
    <Box
      sx={{
        mt: 1,
        p: 1.5,
        bgcolor: "background.default",
        borderRadius: 1.5,
        borderLeft: `3px solid ${brandColors.sectionTitle}`,
        border: "1px solid",
        borderColor: "divider",
        borderLeftWidth: 3,
        fontSize: 11,
        textAlign: "left",
        color: "text.secondary"
      }}
    >
      <Typography sx={{ fontSize: 11, fontWeight: 800, color: theme.palette.primary.main, mb: 0.5 }}>
        Evidence Chain & Derivation:
      </Typography>

      {!isQuant ? (
        <Stack spacing={0.75}>
          {/* Primary Observation */}
          <Box>
            <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.primary" }}>
              1. Primary Plate Observation:
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary", pl: 1 }}>
              Observation: <strong>{cell.primaryObservation || "No Growth recorded"}</strong>
            </Typography>
          </Box>

          {/* Confirmatory Plate Details */}
          {cell.confirmatoryPlates && cell.confirmatoryPlates.length > 0 ? (
            <Box>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.primary" }}>
                2. Confirmatory Plating ({cell.confirmatoryPlates.length} Media):
              </Typography>
              {cell.confirmatoryPlates.map((plate) => (
                <Box key={plate.id} sx={{ pl: 1, mt: 0.25 }}>
                  <Typography sx={{ fontSize: 10.5, color: "text.secondary" }}>
                    • Medium #{plate.mediumIndex + 1} ({plate.mediumName ?? "Selective Agar"}):{" "}
                    <strong>{plate.observation}</strong>
                  </Typography>
                  {plate.expectedAppearanceSnapshot && (
                    <Typography sx={{ fontSize: 10, color: "text.secondary", pl: 1.25 }}>
                      Expected: <em>{plate.expectedAppearanceSnapshot}</em>
                    </Typography>
                  )}
                  <Typography sx={{ fontSize: 10, color: "text.secondary", pl: 1.25 }}>
                    Read at {formatDate(plate.recordedAtUtc)} by {plate.recordedByUserName ?? "Analyst"}
                  </Typography>
                </Box>
              ))}
            </Box>
          ) : (
            <Box>
              <Typography sx={{ fontSize: 10.5, color: "text.secondary", pl: 1 }}>
                2. Confirmatory Plating: <em>Not required (resolved at primary plate)</em>
              </Typography>
            </Box>
          )}

          {/* Agreement Evaluation */}
          <Box>
            <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.primary" }}>
              3. Agreement Evaluation:
            </Typography>
            <Typography sx={{ fontSize: 11, color: theme.custom.status.notDetected.text, pl: 1, fontWeight: 600 }}>
              {cell.resultDisplay?.includes("Detected (+)")
                ? "All confirmatory media conforming → Detected (+)"
                : cell.resultDisplay?.includes("Not Detected (-)")
                ? "All media non-conforming / no growth → Not Detected (-)"
                : cell.resultDisplay?.includes("Inconclusive")
                ? "Media disagreement across plates → Inconclusive (Retest Required)"
                : cell.resultDisplay ?? "—"}
            </Typography>
          </Box>
        </Stack>
      ) : (
        <Box>
          <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.primary" }}>
            Plate Colony Count:
          </Typography>
          <Typography sx={{ fontSize: 11, color: theme.custom.status.info.text, pl: 1, fontWeight: 700 }}>
            {cell.numericValue !== null ? `${cell.numericValue} CFU` : cell.resultDisplay}
          </Typography>
        </Box>
      )}

      <Divider sx={{ my: 0.75, borderColor: "divider" }} />

      {/* Audit Stamp */}
      <Typography sx={{ fontSize: 10, color: "text.secondary" }}>
        Entered by: <strong>{cell.enteredByUserName ?? "Analyst"}</strong> at {formatDate(cell.enteredAt)}
      </Typography>
    </Box>
  );
}

export function SessionReviewPanel({ session, onSessionCompleted, onBackToMatrix }: Props) {
  const theme = useTheme();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [expandedCells, setExpandedCells] = useState<Set<string>>(new Set());

  const isSessionCompleted =
    session.overallSessionStatus === "COMPLETED" ||
    session.overallSessionStatus === "Approved" ||
    session.overallSessionStatus === "READY_FOR_REVIEW" ||
    session.missingResults.length === 0 && session.completedResultCount === session.requiredResultCount && session.overallSessionStatus.includes("Completed");

  const isAllComplete = session.completedResultCount === session.requiredResultCount && session.requiredResultCount > 0;

  const toggleExpanded = (sampleLocationId: number, testCode: string) => {
    const key = `${sampleLocationId}_${testCode}`;
    setExpandedCells((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  };

  const handleCompleteSession = async () => {
    if (!isAllComplete) {
      setError(`Cannot complete session: ${session.pendingResultCount} required results are still missing.`);
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      const completed = await PathogenSessionService.completeSession(session.sampleId);
      onSessionCompleted(completed);
    } catch (e: any) {
      setError(e?.response?.data?.message ?? e?.message ?? "Failed to complete testing session.");
    } finally {
      setSubmitting(false);
    }
  };

  // Outcome rollups
  const outcomeStats = useMemo(() => {
    let detected = 0;
    let notDetected = 0;
    let inconclusive = 0;
    let countTotal = 0;

    for (const cell of session.resultMatrix) {
      if (cell.resultCode === "DETECTED" || cell.resultDisplay?.includes("Detected (+)")) {
        detected++;
      } else if (cell.resultCode === "NOT_DETECTED" || cell.resultDisplay?.includes("Not Detected (-)")) {
        notDetected++;
      } else if (cell.resultDisplay?.includes("Inconclusive")) {
        inconclusive++;
      }
      if (cell.resultType === "Quantitative") {
        countTotal++;
      }
    }

    return { detected, notDetected, inconclusive, countTotal };
  }, [session.resultMatrix]);

  return (
    <Stack spacing={3}>
      {/* Session Metadata Banner */}
      <Paper sx={{ p: 3, borderRadius: 2, border: "1px solid", borderColor: "divider", bgcolor: theme.custom.status.purple.bg }}>
        <Stack direction={{ xs: "column", md: "row" }} justifyContent="space-between" alignItems={{ md: "center" }} spacing={2}>
          <Box>
            <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 1 }}>
              <Typography sx={{ fontSize: 18, fontWeight: 800, color: "text.primary" }}>
                Testing Session Review — {session.sampleReferenceNumber}
              </Typography>
              <Chip
                label={session.overallSessionStatusDisplay ?? session.overallSessionStatus}
                size="small"
                sx={{ bgcolor: brandColors.sectionTitle, color: "#ffffff", fontWeight: 700 }}
              />
            </Stack>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              Program: <strong>{session.programName}</strong> · Area: <strong>{session.departmentOrAreaName}</strong> · Control: <strong>{session.controlNumber}</strong>
            </Typography>
          </Box>

          <Button
            variant="contained"
            color="success"
            size="large"
            startIcon={submitting ? <CircularProgress size={18} color="inherit" /> : <TaskAltIcon />}
            onClick={handleCompleteSession}
            disabled={submitting || !isAllComplete || isSessionCompleted}
            sx={{ px: 4, py: 1.25, fontWeight: 800, fontSize: 14 }}
          >
            {isSessionCompleted ? "Session Completed & Submitted" : "Complete Testing Session"}
          </Button>
        </Stack>
      </Paper>

      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}

      {!isAllComplete && (
        <Alert
          severity="warning"
          action={
            <Button color="inherit" size="small" onClick={onBackToMatrix} disabled={isSessionCompleted}>
              Edit Matrix
            </Button>
          }
        >
          There are {session.pendingResultCount} missing results. Please complete all Location × Test entries before final completion.
        </Alert>
      )}

      {outcomeStats.inconclusive > 0 && (
        <Alert severity="warning" icon={<WarningAmberOutlinedIcon />} sx={{ fontWeight: 600 }}>
          {outcomeStats.inconclusive} sampling location(s) resulted in <strong>Inconclusive (Retest)</strong> due to confirmatory plate media disagreement. These have been flagged for laboratory investigation / re-test.
        </Alert>
      )}

      {/* Shared TSB Record if applicable */}
      {session.sharedTsb.isStarted && (
        <SharedTsbStatusCard sharedTsb={session.sharedTsb} />
      )}

      {/* Results Matrix Review Table with Evidence Expansion */}
      <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
        <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
          <Box>
            <Typography sx={{ fontSize: 16, fontWeight: 800, color: "text.primary" }}>
              Final Analytical Results Summary
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              {session.completedResultCount} of {session.requiredResultCount} results verified ({session.locations.length} Locations × {session.assignedTests.length} Tests)
              · Click any result badge to toggle evidence derivation details.
            </Typography>
          </Box>

          <Stack direction="row" spacing={1.5} alignItems="center">
            <Button
              variant="outlined"
              size="small"
              onClick={onBackToMatrix}
              disabled={isSessionCompleted}
              sx={{ fontWeight: 600 }}
            >
              Modify Matrix
            </Button>
          </Stack>
        </Stack>

        <Box sx={{ maxHeight: 520, overflow: "auto" }}>
          <Table size="small" stickyHeader>
            <TableHead>
              <TableRow>
                <TableCell sx={{ position: "sticky", left: 0, zIndex: 3, bgcolor: "background.default", fontWeight: 800, minWidth: 220, borderRight: "2px solid", borderColor: "divider" }}>
                  Sampling Location ({session.locations.length})
                </TableCell>
                {session.assignedTests.map((t) => (
                  <TableCell key={t.testCode} align="center" sx={{ bgcolor: "background.default", fontWeight: 800, minWidth: 180, borderRight: "1px solid", borderColor: "divider" }}>
                    <Typography sx={{ fontSize: 13, fontWeight: 800 }}>
                      {t.testCode}
                    </Typography>
                    <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                      {t.displayName}
                    </Typography>
                  </TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {session.locations.map((loc, idx) => (
                <TableRow key={loc.id} hover sx={{ bgcolor: idx % 2 === 0 ? "background.paper" : "background.default" }}>
                  {/* Sticky Location Column */}
                  <TableCell
                    sx={{
                      position: "sticky",
                      left: 0,
                      zIndex: 2,
                      bgcolor: idx % 2 === 0 ? "background.paper" : "background.default",
                      fontWeight: 700,
                      fontSize: 13,
                      borderRight: "2px solid",
                      borderColor: "divider",
                      verticalAlign: "top",
                      py: 1.5
                    }}
                  >
                    <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary" }}>
                      {loc.locationName}
                    </Typography>
                    <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                      {loc.locationType} {loc.gradeClassification ? `· Grade ${loc.gradeClassification}` : ""}
                    </Typography>
                  </TableCell>

                  {/* Test Cells with Clickable Derivation Expansion */}
                  {session.assignedTests.map((t) => {
                    const slocId = loc.testLocationMap[t.testCode] ?? loc.primarySampleLocationId;
                    const cell = session.resultMatrix.find(
                      (c) => c.sampleLocationId === slocId && c.testCode === t.testCode
                    );

                    const key = `${slocId}_${t.testCode}`;
                    const isExpanded = expandedCells.has(key);

                    const isDetected = cell?.resultCode === "DETECTED" || cell?.resultDisplay?.includes("Detected (+)");
                    const isNotDetected = cell?.resultCode === "NOT_DETECTED" || cell?.resultDisplay?.includes("Not Detected (-)");
                    const isInconclusive = cell?.resultDisplay?.includes("Inconclusive");
                    const isCfu = cell?.resultType === "Quantitative";

                    return (
                      <TableCell
                        key={t.testCode}
                        align="center"
                        sx={{
                          borderRight: "1px solid",
                          borderColor: "divider",
                          verticalAlign: "top",
                          p: 1.25
                        }}
                      >
                        {/* Interactive Result Chip */}
                        {isDetected ? (
                          <Chip
                            icon={isExpanded ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
                            label="Detected (+)"
                            size="small"
                            onClick={() => toggleExpanded(slocId, t.testCode)}
                            sx={{
                              fontWeight: 800,
                              bgcolor: theme.custom.status.detected.bg,
                              color: theme.custom.status.detected.text,
                              fontSize: 11,
                              cursor: "pointer",
                              "&:hover": { bgcolor: theme.custom.status.detected.border }
                            }}
                          />
                        ) : isNotDetected ? (
                          <Chip
                            icon={isExpanded ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
                            label="Not Detected (-)"
                            size="small"
                            onClick={() => toggleExpanded(slocId, t.testCode)}
                            sx={{
                              fontWeight: 700,
                              bgcolor: theme.custom.status.notDetected.bg,
                              color: theme.custom.status.notDetected.text,
                              fontSize: 11,
                              cursor: "pointer",
                              "&:hover": { bgcolor: theme.custom.status.notDetected.border }
                            }}
                          />
                        ) : isInconclusive ? (
                          <Tooltip title="Disagreement across confirmatory media. Click for details.">
                            <Chip
                              icon={isExpanded ? <KeyboardArrowUpIcon /> : <WarningAmberOutlinedIcon sx={{ fontSize: 13 }} />}
                              label="Inconclusive (Retest)"
                              size="small"
                              onClick={() => toggleExpanded(slocId, t.testCode)}
                              sx={{
                                fontWeight: 800,
                                bgcolor: theme.custom.status.action.bg,
                                color: theme.custom.status.action.text,
                                fontSize: 11,
                                cursor: "pointer",
                                "&:hover": { bgcolor: theme.custom.status.action.border }
                              }}
                            />
                          </Tooltip>
                        ) : isCfu && cell?.numericValue !== null && cell?.numericValue !== undefined ? (
                          <Chip
                            icon={isExpanded ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
                            label={`${cell.numericValue} CFU`}
                            size="small"
                            onClick={() => toggleExpanded(slocId, t.testCode)}
                            sx={{
                              fontWeight: 700,
                              bgcolor: theme.custom.status.info.bg,
                              color: theme.custom.status.info.text,
                              fontSize: 11,
                              cursor: "pointer",
                              "&:hover": { bgcolor: theme.custom.status.info.border }
                            }}
                          />
                        ) : (
                          (() => {
                            let label = "Pending";
                            let bgcolor: string = theme.custom.status.detected.bg;
                            let color: string = theme.custom.status.detected.text;
                            let border = `1px solid ${theme.custom.status.detected.border}`;

                            if (cell?.cellState === "LOCKED_PREREQUISITE") {
                              label = "Locked";
                              bgcolor = theme.custom.status.pending.bg;
                              color = theme.custom.status.pending.text;
                              border = `1px solid ${theme.custom.status.pending.border}`;
                            } else {
                              switch (t.testSessionState) {
                                case "TSB_INCUBATING":
                                  label = "TSB Incubating"; bgcolor = theme.custom.status.info.bg; color = theme.custom.status.info.text; border = `1px solid ${theme.custom.status.info.border}`; break;
                                case "DOWNSTREAM_INCUBATING":
                                  label = "Plating In Progress"; bgcolor = theme.custom.status.info.bg; color = theme.custom.status.info.text; border = `1px solid ${theme.custom.status.info.border}`; break;
                                case "COUNT_INCUBATING":
                                  label = "Incubating"; bgcolor = theme.custom.status.info.bg; color = theme.custom.status.info.text; border = `1px solid ${theme.custom.status.info.border}`; break;
                                case "READY_FOR_DOWNSTREAM":
                                  label = "Ready to Read"; bgcolor = theme.custom.status.purple.bg; color = theme.custom.status.purple.text; border = `1px solid ${theme.custom.status.purple.border}`; break;
                                case "AWAITING_RESULTS":
                                  label = "Enter Result"; bgcolor = theme.custom.status.inconclusive.bg; color = theme.custom.status.inconclusive.text; border = `1px solid ${theme.custom.status.inconclusive.border}`; break;
                                case "RESULTS_RECORDED":
                                  label = "Pending Review"; bgcolor = theme.custom.status.action.bg; color = theme.custom.status.action.text; border = `1px solid ${theme.custom.status.action.border}`; break;
                                default:
                                  label = "Pending"; bgcolor = theme.custom.status.detected.bg; color = theme.custom.status.detected.text; border = `1px solid ${theme.custom.status.detected.border}`; break;
                              }
                            }

                            return (
                              <Chip
                                label={label}
                                size="small"
                                sx={{
                                  fontWeight: 700,
                                  bgcolor,
                                  color,
                                  border,
                                  fontSize: 11
                                }}
                              />
                            );
                          })()
                        )}

                        {/* Expandable Derivation Breakdown */}
                        <CellDerivationDetail
                          cell={cell}
                          testCode={t.testCode}
                          testDisplayName={t.displayName}
                          expanded={isExpanded}
                        />
                      </TableCell>
                    );
                  })}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      </Paper>

      {/* Audit & Traceability Card */}
      <Card sx={{ borderRadius: 2, border: "1px solid", borderColor: "divider", bgcolor: theme.custom.status.purple.bg }}>
        <CardContent sx={{ p: 2.5 }}>
          <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 1 }}>
            <VerifiedUserOutlinedIcon sx={{ fontSize: 20, color: theme.palette.primary.main }} />
            <Typography sx={{ fontSize: 15, fontWeight: 800, color: "text.primary" }}>
              Audit & Traceability (ALCOA+ Compliance)
            </Typography>
          </Stack>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 2 }}>
            All primary observations, confirmatory media selections, incubations, and multi-media agreement evaluations are contemporaneously signed and logged.
          </Typography>

          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(4, 1fr)" },
              gap: 2
            }}
          >
            <Box>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: theme.custom.status.purple.text, textTransform: "uppercase" }}>
                Total Sampling Locations
              </Typography>
              <Typography sx={{ fontSize: 15, fontWeight: 800, color: "text.primary" }}>
                {session.locations.length}
              </Typography>
            </Box>

            <Box>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: theme.custom.status.purple.text, textTransform: "uppercase" }}>
                Results Verified
              </Typography>
              <Typography sx={{ fontSize: 15, fontWeight: 800, color: theme.custom.status.notDetected.text }}>
                {session.completedResultCount} / {session.requiredResultCount} (100%)
              </Typography>
            </Box>

            <Box>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: theme.custom.status.purple.text, textTransform: "uppercase" }}>
                Outcome Breakdown
              </Typography>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.primary" }}>
                {outcomeStats.detected} Detected · {outcomeStats.notDetected} Not Detected
                {outcomeStats.inconclusive > 0 ? ` · ${outcomeStats.inconclusive} Inconclusive` : ""}
              </Typography>
            </Box>

            <Box>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: theme.custom.status.purple.text, textTransform: "uppercase" }}>
                Session Status
              </Typography>
              <Typography
                sx={{
                  fontSize: 14,
                  fontWeight: 800,
                  color: isSessionCompleted ? theme.custom.status.notDetected.text : theme.custom.status.info.text
                }}
              >
                {isSessionCompleted ? "✓ Completed & Locked" : "Ready for Technical Review"}
              </Typography>
            </Box>
          </Box>
        </CardContent>
      </Card>
    </Stack>
  );
}
