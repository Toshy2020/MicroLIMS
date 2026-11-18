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
  Card,
  CardContent,
  Tooltip,
  useTheme
} from "@mui/material";
import VerifiedUserOutlinedIcon from "@mui/icons-material/VerifiedUserOutlined";
import TaskAltIcon from "@mui/icons-material/TaskAlt";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import { Trans } from "react-i18next";
import {
  PathogenTestingSessionDto,
  MatrixCellResultDto
} from "../types/pathogenSessionTypes";
import { PathogenSessionService } from "../services/PathogenSessionService";
import { SharedTsbStatusCard } from "./SharedTsbStatusCard";
import { brandColors } from "../../../theme";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { useTranslation } from "../../../i18n";
import { Ltr } from "../../../components/Ltr";

interface Props {
  session: PathogenTestingSessionDto;
  onSessionCompleted: (updatedSession: PathogenTestingSessionDto) => void;
  onBackToMatrix: () => void;
}

function getErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === "object") {
    const apiErr = err as { response?: { data?: { message?: string } }; message?: string };
    return apiErr.response?.data?.message ?? apiErr.message ?? fallback;
  }
  return fallback;
}

const formatDate = (d: string | null | undefined) =>
  d
    ? formatDateTimeWithTimeZone(d)
    : "—";

interface CellDerivationProps {
  cell: MatrixCellResultDto | undefined;
  testCode: string;
  testDisplayName: string;
  expanded: boolean;
}

function CellDerivationDetail({ cell, expanded }: CellDerivationProps) {
  const { t } = useTranslation("testingWorkspace");
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
        border: "1px solid",
        borderColor: "divider",
        borderInlineStart: `3px solid ${brandColors.sectionTitle}`,
        fontSize: 12,
        textAlign: "start",
        color: "text.secondary"
      }}
    >
      <Typography sx={{ fontSize: 12, fontWeight: 800, color: theme.palette.primary.main, mb: 0.5 }}>
        {t("pathogenSession.review.evidenceChainTitle")}
      </Typography>

      {!isQuant ? (
        <Stack spacing={0.75}>
          {/* Primary Observation */}
          <Box>
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.primary" }}>
              {t("pathogenSession.review.primaryObsStepTitle")}
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary", ps: 1 }}>
              {t("pathogenSession.review.observationPrefix")} <strong>{cell.primaryObservation || t("pathogenSession.review.noGrowthRecorded")}</strong>
            </Typography>
          </Box>

          {/* Confirmatory Plate Details */}
          {cell.confirmatoryPlates && cell.confirmatoryPlates.length > 0 ? (
            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.primary" }}>
                <Trans
                  i18nKey="pathogenSession.review.confirmatoryPlatingTitle"
                  ns="testingWorkspace"
                  values={{ count: cell.confirmatoryPlates.length }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
              {cell.confirmatoryPlates.map((plate) => (
                <Box key={plate.id} sx={{ ps: 1, mt: 0.25 }}>
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                    <Trans
                      i18nKey="pathogenSession.review.mediumItemLabel"
                      ns="testingWorkspace"
                      values={{ index: plate.mediumIndex + 1, name: plate.mediumName ?? t("pathogenSession.review.selectiveAgarFallback") }}
                      components={{ ltr: <Ltr>{null}</Ltr> }}
                    />{" "}
                    <strong>{plate.observation}</strong>
                  </Typography>
                  {plate.expectedAppearanceSnapshot && (
                    <Typography sx={{ fontSize: 12, color: "text.secondary", ps: 1.25 }}>
                      {t("pathogenSession.review.expectedPrefix")} <em>{plate.expectedAppearanceSnapshot}</em>
                    </Typography>
                  )}
                  <Typography sx={{ fontSize: 12, color: "text.secondary", ps: 1.25 }}>
                    <Trans
                      i18nKey="pathogenSession.review.readAtBy"
                      ns="testingWorkspace"
                      values={{
                        date: formatDate(plate.recordedAtUtc),
                        user: plate.recordedByUserName ?? t("pathogenSession.review.analystFallback")
                      }}
                      components={{ ltr: <Ltr>{null}</Ltr> }}
                    />
                  </Typography>
                </Box>
              ))}
            </Box>
          ) : (
            <Box>
              <Typography sx={{ fontSize: 12, color: "text.secondary", ps: 1 }}>
                <Trans
                  i18nKey="pathogenSession.review.confirmatoryNotRequired"
                  ns="testingWorkspace"
                  components={{ em: <em /> }}
                />
              </Typography>
            </Box>
          )}

          {/* Agreement Evaluation */}
          <Box>
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.primary" }}>
              {t("pathogenSession.review.agreementEvalTitle")}
            </Typography>
            <Typography sx={{ fontSize: 12, color: theme.custom.status.notDetected.text, ps: 1, fontWeight: 600 }}>
              {cell.resultDisplay?.includes("Detected (+)")
                ? t("pathogenSession.review.agreementDetected")
                : cell.resultDisplay?.includes("Not Detected (-)")
                ? t("pathogenSession.review.agreementNotDetected")
                : cell.resultDisplay?.includes("Inconclusive")
                ? t("pathogenSession.review.agreementInconclusive")
                : cell.resultDisplay ?? "—"}
            </Typography>
          </Box>
        </Stack>
      ) : (
        <Box>
          <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.primary" }}>
            {t("pathogenSession.review.plateColonyCountTitle")}
          </Typography>
          <Typography sx={{ fontSize: 12, color: theme.custom.status.info.text, ps: 1, fontWeight: 700 }}>
            {cell.numericValue !== null ? (
              <Trans
                i18nKey="pathogenSession.review.cfuValue"
                ns="testingWorkspace"
                values={{ value: cell.numericValue }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            ) : cell.resultDisplay}
          </Typography>
        </Box>
      )}

      <Divider sx={{ my: 0.75, borderColor: "divider" }} />

      {/* Audit Stamp */}
      <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
        <Trans
          i18nKey="pathogenSession.review.enteredByAt"
          ns="testingWorkspace"
          values={{
            user: cell.enteredByUserName ?? t("pathogenSession.review.analystFallback"),
            date: formatDate(cell.enteredAt)
          }}
          components={{ strong: <strong />, ltr: <Ltr>{null}</Ltr> }}
        />
      </Typography>
    </Box>
  );
}

export function SessionReviewPanel({ session, onSessionCompleted, onBackToMatrix }: Props) {
  const { t } = useTranslation("testingWorkspace");
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
      setError(t("pathogenSession.review.missingResultsError", { count: session.pendingResultCount }));
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      const completed = await PathogenSessionService.completeSession(session.sampleId);
      onSessionCompleted(completed);
    } catch (e: unknown) {
      setError(getErrorMessage(e, t("pathogenSession.review.completeSessionError")));
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
        <Stack
          direction={{ xs: "column", md: "row" }}
          spacing={2}
          sx={{
            justifyContent: "space-between",
            alignItems: { md: "center" }
          }}>
          <Box>
            <Stack
              direction="row"
              spacing={1.5}
              sx={{
                alignItems: "center",
                mb: 1
              }}>
              <Typography sx={{ fontSize: 18, fontWeight: 800, color: "text.primary" }}>
                <Trans
                  i18nKey="pathogenSession.review.title"
                  ns="testingWorkspace"
                  values={{ sampleRef: session.sampleReferenceNumber }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
              <Chip
                label={session.overallSessionStatusDisplay ?? session.overallSessionStatus}
                size="small"
                sx={{ bgcolor: brandColors.sectionTitle, color: "common.white", fontWeight: 700 }}
              />
            </Stack>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              <Trans
                i18nKey="pathogenSession.review.metadataSubtitle"
                ns="testingWorkspace"
                values={{
                  program: session.programName,
                  area: session.departmentOrAreaName,
                  control: session.controlNumber
                }}
                components={{ strong: <strong />, ltr: <Ltr>{null}</Ltr> }}
              />
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
            {isSessionCompleted ? t("pathogenSession.review.sessionCompletedButton") : t("pathogenSession.review.completeSessionButton")}
          </Button>
        </Stack>
      </Paper>

      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}

      {!isAllComplete && (
        <Alert
          severity="warning"
          action={
            <Button color="inherit" size="small" onClick={onBackToMatrix} disabled={isSessionCompleted}>
              {t("pathogenSession.review.editMatrix")}
            </Button>
          }
        >
          <Trans
            i18nKey="pathogenSession.review.missingResultsAlert"
            ns="testingWorkspace"
            values={{ count: session.pendingResultCount }}
            components={{ ltr: <Ltr>{null}</Ltr> }}
          />
        </Alert>
      )}

      {outcomeStats.inconclusive > 0 && (
        <Alert severity="warning" icon={<WarningAmberOutlinedIcon />} sx={{ fontWeight: 600 }}>
          <Trans
            i18nKey="pathogenSession.review.inconclusiveAlert"
            ns="testingWorkspace"
            values={{ count: outcomeStats.inconclusive }}
            components={{ strong: <strong />, ltr: <Ltr>{null}</Ltr> }}
          />
        </Alert>
      )}

      {/* Shared TSB Record if applicable */}
      {session.sharedTsb.isStarted && (
        <SharedTsbStatusCard sharedTsb={session.sharedTsb} />
      )}

      {/* Results Matrix Review Table with Evidence Expansion */}
      <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
        <Stack
          direction="row"
          sx={{
            justifyContent: "space-between",
            alignItems: "center",
            mb: 2
          }}>
          <Box>
            <Typography sx={{ fontSize: 16, fontWeight: 800, color: "text.primary" }}>
              {t("pathogenSession.review.resultsSummaryTitle")}
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              <Trans
                i18nKey="pathogenSession.review.resultsSummarySubtitle"
                ns="testingWorkspace"
                values={{
                  completed: session.completedResultCount,
                  required: session.requiredResultCount,
                  locations: session.locations.length,
                  testCount: session.assignedTests.length
                }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
          </Box>

          <Stack direction="row" spacing={1.5} sx={{
            alignItems: "center"
          }}>
            <Button
              variant="outlined"
              size="small"
              onClick={onBackToMatrix}
              disabled={isSessionCompleted}
              sx={{ fontWeight: 600 }}
            >
              {t("pathogenSession.review.modifyMatrix")}
            </Button>
          </Stack>
        </Stack>

        <Box sx={{ maxHeight: 520, overflow: "auto" }}>
          <Table size="small" stickyHeader>
            <TableHead>
              <TableRow>
                <TableCell sx={{ position: "sticky", insetInlineStart: 0, zIndex: 3, bgcolor: theme.custom.chrome.tableHeaderBg, color: theme.custom.chrome.tableHeaderText, fontWeight: 800, minWidth: 220, borderInlineEnd: "2px solid", borderColor: "divider" }}>
                  <Trans
                    i18nKey="pathogenSession.review.samplingLocationHeader"
                    ns="testingWorkspace"
                    values={{ count: session.locations.length }}
                    components={{ ltr: <Ltr>{null}</Ltr> }}
                  />
                </TableCell>
                {session.assignedTests.map((testItem) => (
                  <TableCell key={testItem.testCode} align="center" sx={{ bgcolor: "background.default", fontWeight: 800, minWidth: 180, borderInlineEnd: "1px solid", borderColor: "divider" }}>
                    <Typography sx={{ fontSize: 13, fontWeight: 800 }}>
                      <Ltr>{testItem.testCode}</Ltr>
                    </Typography>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {testItem.displayName}
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
                      insetInlineStart: 0,
                      zIndex: 2,
                      bgcolor: idx % 2 === 0 ? "background.paper" : "background.default",
                      fontWeight: 700,
                      fontSize: 13,
                      borderInlineEnd: "2px solid",
                      borderColor: "divider",
                      verticalAlign: "top",
                      py: 1.5
                    }}
                  >
                    <Typography sx={{ fontSize: 13, fontWeight: 700, color: "text.primary" }}>
                      {loc.locationName}
                    </Typography>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {loc.locationType} {loc.gradeClassification ? t("pathogenSession.review.gradeSuffix", { grade: loc.gradeClassification }) : ""}
                    </Typography>
                  </TableCell>

                  {/* Test Cells with Clickable Derivation Expansion */}
                  {session.assignedTests.map((testItem) => {
                    const slocId = loc.testLocationMap[testItem.testCode] ?? loc.primarySampleLocationId;
                    const cell = session.resultMatrix.find(
                      (c) => c.sampleLocationId === slocId && c.testCode === testItem.testCode
                    );

                    const key = `${slocId}_${testItem.testCode}`;
                    const isExpanded = expandedCells.has(key);

                    const isDetected = cell?.resultCode === "DETECTED" || cell?.resultDisplay?.includes("Detected (+)");
                    const isNotDetected = cell?.resultCode === "NOT_DETECTED" || cell?.resultDisplay?.includes("Not Detected (-)");
                    const isInconclusive = cell?.resultDisplay?.includes("Inconclusive");
                    const isCfu = cell?.resultType === "Quantitative";

                    return (
                      <TableCell
                        key={testItem.testCode}
                        align="center"
                        sx={{
                          borderInlineEnd: "1px solid",
                          borderColor: "divider",
                          verticalAlign: "top",
                          p: 1.25
                        }}
                      >
                        {/* Interactive Result Chip */}
                        {isDetected ? (
                          <Chip
                            icon={isExpanded ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
                            label={t("pathogenSession.review.detectedBadge")}
                            size="small"
                            onClick={() => toggleExpanded(slocId, testItem.testCode)}
                            sx={{
                              fontWeight: 800,
                              bgcolor: theme.custom.status.detected.bg,
                              color: theme.custom.status.detected.text,
                              fontSize: 12,
                              cursor: "pointer",
                              "&:hover": { bgcolor: theme.custom.status.detected.border }
                            }}
                          />
                        ) : isNotDetected ? (
                          <Chip
                            icon={isExpanded ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
                            label={t("pathogenSession.review.notDetectedBadge")}
                            size="small"
                            onClick={() => toggleExpanded(slocId, testItem.testCode)}
                            sx={{
                              fontWeight: 700,
                              bgcolor: theme.custom.status.notDetected.bg,
                              color: theme.custom.status.notDetected.text,
                              fontSize: 12,
                              cursor: "pointer",
                              "&:hover": { bgcolor: theme.custom.status.notDetected.border }
                            }}
                          />
                        ) : isInconclusive ? (
                          <Tooltip title={t("pathogenSession.review.inconclusiveTooltip")}>
                            <Chip
                              icon={isExpanded ? <KeyboardArrowUpIcon /> : <WarningAmberOutlinedIcon sx={{ fontSize: 13 }} />}
                              label={t("pathogenSession.review.inconclusiveBadge")}
                              size="small"
                              onClick={() => toggleExpanded(slocId, testItem.testCode)}
                              sx={{
                                fontWeight: 800,
                                bgcolor: theme.custom.status.action.bg,
                                color: theme.custom.status.action.text,
                                fontSize: 12,
                                cursor: "pointer",
                                "&:hover": { bgcolor: theme.custom.status.action.border }
                              }}
                            />
                          </Tooltip>
                        ) : isCfu && cell?.numericValue !== null && cell?.numericValue !== undefined ? (
                          <Chip
                            icon={isExpanded ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
                            label={
                              <Trans
                                i18nKey="pathogenSession.review.cfuChip"
                                ns="testingWorkspace"
                                values={{ value: cell.numericValue }}
                                components={{ ltr: <Ltr>{null}</Ltr> }}
                              />
                            }
                            size="small"
                            onClick={() => toggleExpanded(slocId, testItem.testCode)}
                            sx={{
                              fontWeight: 700,
                              bgcolor: theme.custom.status.info.bg,
                              color: theme.custom.status.info.text,
                              fontSize: 12,
                              cursor: "pointer",
                              "&:hover": { bgcolor: theme.custom.status.info.border }
                            }}
                          />
                        ) : (
                          (() => {
                            let label = t("pathogenSession.review.status.pending");
                            let bgcolor: string = theme.custom.status.detected.bg;
                            let color: string = theme.custom.status.detected.text;
                            let border = `1px solid ${theme.custom.status.detected.border}`;

                            if (cell?.cellState === "LOCKED_PREREQUISITE") {
                              label = t("pathogenSession.review.status.locked");
                              bgcolor = theme.custom.status.pending.bg;
                              color = theme.custom.status.pending.text;
                              border = `1px solid ${theme.custom.status.pending.border}`;
                            } else {
                              switch (testItem.testSessionState) {
                                case "WINDOW_NOT_CONFIGURED":
                                  label = t("pathogenSession.review.status.windowNotConfigured"); bgcolor = theme.custom.status.detected.bg; color = theme.custom.status.detected.text; border = `1px solid ${theme.custom.status.detected.border}`; break;
                                case "TSB_INCUBATING":
                                  label = t("pathogenSession.review.status.tsbIncubating"); bgcolor = theme.custom.status.info.bg; color = theme.custom.status.info.text; border = `1px solid ${theme.custom.status.info.border}`; break;
                                case "DOWNSTREAM_INCUBATING":
                                  label = t("pathogenSession.review.status.platingInProgress"); bgcolor = theme.custom.status.info.bg; color = theme.custom.status.info.text; border = `1px solid ${theme.custom.status.info.border}`; break;
                                case "COUNT_INCUBATING":
                                  label = t("pathogenSession.review.status.incubating"); bgcolor = theme.custom.status.info.bg; color = theme.custom.status.info.text; border = `1px solid ${theme.custom.status.info.border}`; break;
                                case "READY_FOR_DOWNSTREAM":
                                  label = t("pathogenSession.review.status.readyToRead"); bgcolor = theme.custom.status.purple.bg; color = theme.custom.status.purple.text; border = `1px solid ${theme.custom.status.purple.border}`; break;
                                case "AWAITING_RESULTS":
                                  label = t("pathogenSession.review.status.enterResult"); bgcolor = theme.custom.status.inconclusive.bg; color = theme.custom.status.inconclusive.text; border = `1px solid ${theme.custom.status.inconclusive.border}`; break;
                                case "RESULTS_RECORDED":
                                  label = t("pathogenSession.review.status.pendingReview"); bgcolor = theme.custom.status.action.bg; color = theme.custom.status.action.text; border = `1px solid ${theme.custom.status.action.border}`; break;
                                case "REVIEWED":
                                  label = t("pathogenSession.review.status.reviewed"); bgcolor = theme.custom.status.action.bg; color = theme.custom.status.action.text; border = `1px solid ${theme.custom.status.action.border}`; break;
                                case "APPROVED":
                                  label = t("pathogenSession.review.status.approved"); bgcolor = theme.custom.status.notDetected.bg; color = theme.custom.status.notDetected.text; border = `1px solid ${theme.custom.status.notDetected.border}`; break;
                                case "REJECTED":
                                case "VOIDED":
                                case "CANCELLED":
                                  label = testItem.testSessionState === "REJECTED"
                                    ? t("pathogenSession.review.status.rejected")
                                    : testItem.testSessionState === "VOIDED"
                                    ? t("pathogenSession.review.status.voided")
                                    : t("pathogenSession.review.status.cancelled");
                                  bgcolor = theme.custom.status.detected.bg; color = theme.custom.status.detected.text; border = `1px solid ${theme.custom.status.detected.border}`; break;
                                case "SUPERSEDED":
                                case "ON_HOLD":
                                  label = testItem.testSessionState === "SUPERSEDED"
                                    ? t("pathogenSession.review.status.superseded")
                                    : t("pathogenSession.review.status.onHold");
                                  bgcolor = theme.custom.status.inconclusive.bg; color = theme.custom.status.inconclusive.text; border = `1px solid ${theme.custom.status.inconclusive.border}`; break;
                                default:
                                  label = t("pathogenSession.review.status.pending"); bgcolor = theme.custom.status.detected.bg; color = theme.custom.status.detected.text; border = `1px solid ${theme.custom.status.detected.border}`; break;
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
                                  fontSize: 12
                                }}
                              />
                            );
                          })()
                        )}

                        {/* Expandable Derivation Breakdown */}
                        <CellDerivationDetail
                          cell={cell}
                          testCode={testItem.testCode}
                          testDisplayName={testItem.displayName}
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
          <Stack
            direction="row"
            spacing={1.5}
            sx={{
              alignItems: "center",
              mb: 1
            }}>
            <VerifiedUserOutlinedIcon sx={{ fontSize: 20, color: theme.palette.primary.main }} />
            <Typography sx={{ fontSize: 15, fontWeight: 800, color: "text.primary" }}>
              {t("pathogenSession.review.auditCardTitle")}
            </Typography>
          </Stack>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 2 }}>
            {t("pathogenSession.review.auditCardDesc")}
          </Typography>

          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(4, 1fr)" },
              gap: 2
            }}
          >
            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.custom.status.purple.text, textTransform: "uppercase" }}>
                {t("pathogenSession.review.totalSamplingLocations")}
              </Typography>
              <Typography sx={{ fontSize: 15, fontWeight: 800, color: "text.primary" }}>
                <Ltr>{session.locations.length}</Ltr>
              </Typography>
            </Box>

            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.custom.status.purple.text, textTransform: "uppercase" }}>
                {t("pathogenSession.review.resultsVerifiedTitle")}
              </Typography>
              <Typography sx={{ fontSize: 15, fontWeight: 800, color: theme.custom.status.notDetected.text }}>
                <Trans
                  i18nKey="pathogenSession.review.resultsVerifiedValue"
                  ns="testingWorkspace"
                  values={{ completed: session.completedResultCount, required: session.requiredResultCount }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
            </Box>

            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.custom.status.purple.text, textTransform: "uppercase" }}>
                {t("pathogenSession.review.outcomeBreakdownTitle")}
              </Typography>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.primary" }}>
                <Trans
                  i18nKey="pathogenSession.review.outcomeSummary"
                  ns="testingWorkspace"
                  values={{ detected: outcomeStats.detected, notDetected: outcomeStats.notDetected }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
                {outcomeStats.inconclusive > 0 && (
                  <Trans
                    i18nKey="pathogenSession.review.outcomeInconclusivePart"
                    ns="testingWorkspace"
                    values={{ count: outcomeStats.inconclusive }}
                    components={{ ltr: <Ltr>{null}</Ltr> }}
                  />
                )}
              </Typography>
            </Box>

            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.custom.status.purple.text, textTransform: "uppercase" }}>
                {t("pathogenSession.review.sessionStatusTitle")}
              </Typography>
              <Typography
                sx={{
                  fontSize: 14,
                  fontWeight: 800,
                  color: isSessionCompleted ? theme.custom.status.notDetected.text : theme.custom.status.info.text
                }}
              >
                {isSessionCompleted ? t("pathogenSession.review.sessionCompletedStatus") : t("pathogenSession.review.sessionReadyForReviewStatus")}
              </Typography>
            </Box>
          </Box>
        </CardContent>
      </Card>
    </Stack>
  );
}
