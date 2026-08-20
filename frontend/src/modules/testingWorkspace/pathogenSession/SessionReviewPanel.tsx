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
  Tooltip
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
  if (!cell || !expanded) return null;

  const isQuant = cell.resultType === "Quantitative";

  return (
    <Box
      sx={{
        mt: 1,
        p: 1.5,
        bgcolor: "#f8fafc",
        borderRadius: 1.5,
        borderLeft: `3px solid ${brandColors.sectionTitle}`,
        border: "1px solid #e2e8f0",
        borderLeftWidth: 3,
        fontSize: 11,
        textAlign: "left",
        color: "#334155"
      }}
    >
      <Typography sx={{ fontSize: 11, fontWeight: 800, color: brandColors.sectionTitle, mb: 0.5 }}>
        Evidence Chain & Derivation:
      </Typography>

      {!isQuant ? (
        <Stack spacing={0.75}>
          {/* Primary Observation */}
          <Box>
            <Typography sx={{ fontSize: 11, fontWeight: 700, color: "#1e293b" }}>
              1. Primary Plate Observation:
            </Typography>
            <Typography sx={{ fontSize: 11, color: "#475569", pl: 1 }}>
              Observation: <strong>{cell.primaryObservation || "No Growth recorded"}</strong>
            </Typography>
          </Box>

          {/* Confirmatory Plate Details */}
          {cell.confirmatoryPlates && cell.confirmatoryPlates.length > 0 ? (
            <Box>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: "#1e293b" }}>
                2. Confirmatory Plating ({cell.confirmatoryPlates.length} Media):
              </Typography>
              {cell.confirmatoryPlates.map((plate) => (
                <Box key={plate.id} sx={{ pl: 1, mt: 0.25 }}>
                  <Typography sx={{ fontSize: 10.5, color: "#475569" }}>
                    • Medium #{plate.mediumIndex + 1} ({plate.mediumName ?? "Selective Agar"}):{" "}
                    <strong>{plate.observation}</strong>
                  </Typography>
                  {plate.expectedAppearanceSnapshot && (
                    <Typography sx={{ fontSize: 10, color: "#64748b", pl: 1.25 }}>
                      Expected: <em>{plate.expectedAppearanceSnapshot}</em>
                    </Typography>
                  )}
                  <Typography sx={{ fontSize: 10, color: "#64748b", pl: 1.25 }}>
                    Read at {formatDate(plate.recordedAtUtc)} by {plate.recordedByUserName ?? "Analyst"}
                  </Typography>
                </Box>
              ))}
            </Box>
          ) : (
            <Box>
              <Typography sx={{ fontSize: 10.5, color: "#64748b", pl: 1 }}>
                2. Confirmatory Plating: <em>Not required (resolved at primary plate)</em>
              </Typography>
            </Box>
          )}

          {/* Agreement Evaluation */}
          <Box>
            <Typography sx={{ fontSize: 11, fontWeight: 700, color: "#1e293b" }}>
              3. Agreement Evaluation:
            </Typography>
            <Typography sx={{ fontSize: 11, color: "#065f46", pl: 1, fontWeight: 600 }}>
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
          <Typography sx={{ fontSize: 11, fontWeight: 700, color: "#1e293b" }}>
            Plate Colony Count:
          </Typography>
          <Typography sx={{ fontSize: 11, color: "#1e40af", pl: 1, fontWeight: 700 }}>
            {cell.numericValue !== null ? `${cell.numericValue} CFU` : cell.resultDisplay}
          </Typography>
        </Box>
      )}

      <Divider sx={{ my: 0.75, borderColor: "#e2e8f0" }} />

      {/* Audit Stamp */}
      <Typography sx={{ fontSize: 10, color: "#64748b" }}>
        Entered by: <strong>{cell.enteredByUserName ?? "Analyst"}</strong> at {formatDate(cell.enteredAt)}
      </Typography>
    </Box>
  );
}

export function SessionReviewPanel({ session, onSessionCompleted, onBackToMatrix }: Props) {
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
      <Paper sx={{ p: 3, borderRadius: 2, border: "1px solid #e5e7eb", bgcolor: "#faf5ff" }}>
        <Stack direction={{ xs: "column", md: "row" }} justifyContent="space-between" alignItems={{ md: "center" }} spacing={2}>
          <Box>
            <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 1 }}>
              <Typography sx={{ fontSize: 18, fontWeight: 800, color: "#1f2937" }}>
                Testing Session Review — {session.sampleReferenceNumber}
              </Typography>
              <Chip
                label={session.overallSessionStatusDisplay ?? session.overallSessionStatus}
                size="small"
                sx={{ bgcolor: brandColors.sectionTitle, color: "#ffffff", fontWeight: 700 }}
              />
            </Stack>
            <Typography sx={{ fontSize: 13, color: "#4b5563" }}>
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
      <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid #e5e7eb" }}>
        <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
          <Box>
            <Typography sx={{ fontSize: 16, fontWeight: 800, color: "#1f2937" }}>
              Final Analytical Results Summary
            </Typography>
            <Typography sx={{ fontSize: 12, color: "#6b7280" }}>
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
                <TableCell sx={{ position: "sticky", left: 0, zIndex: 3, bgcolor: "#f8fafc", fontWeight: 800, minWidth: 220, borderRight: "2px solid #e2e8f0" }}>
                  Sampling Location ({session.locations.length})
                </TableCell>
                {session.assignedTests.map((t) => (
                  <TableCell key={t.testCode} align="center" sx={{ bgcolor: "#f8fafc", fontWeight: 800, minWidth: 180, borderRight: "1px solid #f1f5f9" }}>
                    <Typography sx={{ fontSize: 13, fontWeight: 800 }}>
                      {t.testCode}
                    </Typography>
                    <Typography sx={{ fontSize: 11, color: "#64748b" }}>
                      {t.displayName}
                    </Typography>
                  </TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {session.locations.map((loc, idx) => (
                <TableRow key={loc.id} hover sx={{ bgcolor: idx % 2 === 0 ? "#ffffff" : "#fafafa" }}>
                  {/* Sticky Location Column */}
                  <TableCell
                    sx={{
                      position: "sticky",
                      left: 0,
                      zIndex: 2,
                      bgcolor: idx % 2 === 0 ? "#ffffff" : "#fafafa",
                      fontWeight: 700,
                      fontSize: 13,
                      borderRight: "2px solid #e2e8f0",
                      verticalAlign: "top",
                      py: 1.5
                    }}
                  >
                    <Typography sx={{ fontSize: 13, fontWeight: 700, color: "#1f2937" }}>
                      {loc.locationName}
                    </Typography>
                    <Typography sx={{ fontSize: 11, color: "#6b7280" }}>
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
                          borderRight: "1px solid #f1f5f9",
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
                              bgcolor: "#fee2e2",
                              color: "#991b1b",
                              fontSize: 11,
                              cursor: "pointer",
                              "&:hover": { bgcolor: "#fecaca" }
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
                              bgcolor: "#ecfdf5",
                              color: "#065f46",
                              fontSize: 11,
                              cursor: "pointer",
                              "&:hover": { bgcolor: "#d1fae5" }
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
                                bgcolor: "#ffedd5",
                                color: "#9a3412",
                                fontSize: 11,
                                cursor: "pointer",
                                "&:hover": { bgcolor: "#fed7aa" }
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
                              bgcolor: "#eff6ff",
                              color: "#1e40af",
                              fontSize: 11,
                              cursor: "pointer",
                              "&:hover": { bgcolor: "#dbeafe" }
                            }}
                          />
                        ) : (
                          (() => {
                            let label = "Pending";
                            let bgcolor = "#f9fafb";
                            let color = "#ef4444";
                            let border = "1px solid #fca5a5";

                            if (cell?.cellState === "LOCKED_PREREQUISITE") {
                              label = "Locked";
                              bgcolor = "#f3f4f6";
                              color = "#6b7280";
                              border = "1px solid #e5e7eb";
                            } else {
                              switch (t.testSessionState) {
                                case "TSB_INCUBATING":
                                  label = "TSB Incubating"; bgcolor = "#eff6ff"; color = "#1e40af"; border = "1px solid #bfdbfe"; break;
                                case "DOWNSTREAM_INCUBATING":
                                  label = "Plating In Progress"; bgcolor = "#eff6ff"; color = "#1e40af"; border = "1px solid #bfdbfe"; break;
                                case "COUNT_INCUBATING":
                                  label = "Incubating"; bgcolor = "#eff6ff"; color = "#1e40af"; border = "1px solid #bfdbfe"; break;
                                case "READY_FOR_DOWNSTREAM":
                                  label = "Ready to Read"; bgcolor = "#f5f3ff"; color = "#7c3aed"; border = "1px solid #ddd6fe"; break;
                                case "AWAITING_RESULTS":
                                  label = "Enter Result"; bgcolor = "#fefce8"; color = "#ca8a04"; border = "1px solid #fef08a"; break;
                                case "RESULTS_RECORDED":
                                  label = "Pending Review"; bgcolor = "#fff7ed"; color = "#d97706"; border = "1px solid #fed7aa"; break;
                                default:
                                  label = "Pending"; bgcolor = "#f9fafb"; color = "#ef4444"; border = "1px solid #fca5a5"; break;
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
      <Card sx={{ borderRadius: 2, border: "1px solid #e5e7eb", bgcolor: "#fcfaff" }}>
        <CardContent sx={{ p: 2.5 }}>
          <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 1 }}>
            <VerifiedUserOutlinedIcon sx={{ fontSize: 20, color: brandColors.sectionTitle }} />
            <Typography sx={{ fontSize: 15, fontWeight: 800, color: "#1f2937" }}>
              Audit & Traceability (ALCOA+ Compliance)
            </Typography>
          </Stack>
          <Typography sx={{ fontSize: 12, color: "#64748b", mb: 2 }}>
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
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: "#7c3aed", textTransform: "uppercase" }}>
                Total Sampling Locations
              </Typography>
              <Typography sx={{ fontSize: 15, fontWeight: 800, color: "#1f2937" }}>
                {session.locations.length}
              </Typography>
            </Box>

            <Box>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: "#7c3aed", textTransform: "uppercase" }}>
                Results Verified
              </Typography>
              <Typography sx={{ fontSize: 15, fontWeight: 800, color: "#065f46" }}>
                {session.completedResultCount} / {session.requiredResultCount} (100%)
              </Typography>
            </Box>

            <Box>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: "#7c3aed", textTransform: "uppercase" }}>
                Outcome Breakdown
              </Typography>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "#1f2937" }}>
                {outcomeStats.detected} Detected · {outcomeStats.notDetected} Not Detected
                {outcomeStats.inconclusive > 0 ? ` · ${outcomeStats.inconclusive} Inconclusive` : ""}
              </Typography>
            </Box>

            <Box>
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: "#7c3aed", textTransform: "uppercase" }}>
                Session Status
              </Typography>
              <Typography
                sx={{
                  fontSize: 14,
                  fontWeight: 800,
                  color: isSessionCompleted ? "#065f46" : "#1e40af"
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
