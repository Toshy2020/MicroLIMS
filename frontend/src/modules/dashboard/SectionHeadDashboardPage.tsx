import { useEffect, useState } from "react";
import {
  Box,
  Grid,
  Paper,
  Typography,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Button,
  Chip,
  LinearProgress,
  useTheme
} from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import ThermostatOutlinedIcon from "@mui/icons-material/ThermostatOutlined";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import RateReviewOutlinedIcon from "@mui/icons-material/RateReviewOutlined";
import VerifiedUserOutlinedIcon from "@mui/icons-material/VerifiedUserOutlined";
import AccessTimeOutlinedIcon from "@mui/icons-material/AccessTimeOutlined";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import ArrowForwardOutlinedIcon from "@mui/icons-material/ArrowForwardOutlined";
import PeopleAltOutlinedIcon from "@mui/icons-material/PeopleAltOutlined";
import { Link } from "react-router-dom";
import { PageHeader } from "../../components/PageHeader";
import RefreshIcon from "@mui/icons-material/Refresh";
import { DashboardStateGate } from "./components/DashboardStateGate";
import { DashboardService } from "./services/DashboardService";
import { SectionHeadDashboard, MonthlyTrendPoint, DistributionSlice } from "./types/dashboard";
import { SamplesTrendChart } from "./components/SamplesTrendChart";
import { TestOrderStatusDonut } from "./components/TestOrderStatusDonut";
import { tableHeadSx } from "../../theme";
import { SummaryTiles } from "../../components/configHierarchy/SummaryTiles";
import { PartialOutageBanner } from "../../components/lab";
import { SectionSelfAssignmentCard } from "./components/SectionSelfAssignmentCard";
import { useDashboardLab } from "./DashboardLabContext";
import { useAuth } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import {
  qualityDashboardService,
  type QualityDashboardDto
} from "../../services/qualityDashboardService";
import { useTranslation } from "../../i18n";

// Kept as a key so the translated text is looked up at render, not stored in the effect.
const SERVICE_ERROR_KEY = "sectionHead.serviceError";

export function SectionHeadDashboardPage() {
  const { t } = useTranslation("dashboard");
  const theme = useTheme();
  const lab = useDashboardLab();
  const { permissions } = useAuth();
  const canViewQuality = permissions.includes(PERMISSIONS.QUALITY_VIEW);

  const [data, setData] = useState<SectionHeadDashboard | null>(null);
  const [qualityDash, setQualityDash] = useState<QualityDashboardDto | null>(null);
  const [qualityOutage, setQualityOutage] = useState(false);
  const [months, setMonths] = useState(6);
  const [trend, setTrend] = useState<MonthlyTrendPoint[] | null>(null);
  const [statusDist, setStatusDist] = useState<DistributionSlice[] | null>(null);
  const [loading, setLoading] = useState(true);

  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);
  const reload = () => setReloadKey((k) => k + 1);

  // The dashboard body and the status donut do not depend on `months` - only
  // the trend does - so changing the period no longer refetches all three.
  useEffect(() => {
    const controller = new AbortController();
    let cancelled = false;
    setLoading(true);
    setError(null);
    setQualityOutage(false);

    const qualityPromise = canViewQuality
      ? qualityDashboardService
          .getDashboard(controller.signal)
          .then((res) => ({ ok: true as const, res }))
          .catch(() => ({ ok: false as const }))
      : Promise.resolve({ ok: true as const, res: null });

    Promise.all([
      DashboardService.getSectionHeadDashboard(lab.code),
      DashboardService.getStatusDistribution(lab.code).catch(() => null),
      qualityPromise
    ])
      .then(([dashData, distData, qualityResult]) => {
        if (cancelled) return;
        setData(dashData);
        setStatusDist(distData);
        if (qualityResult.ok) {
          setQualityDash(qualityResult.res);
        } else {
          setQualityDash(null);
          setQualityOutage(true);
        }
      })
      .catch(() => {
        if (!cancelled) setError(SERVICE_ERROR_KEY);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [reloadKey, lab.code, canViewQuality]);

  useEffect(() => {
    let cancelled = false;
    DashboardService.getMonthlyTrend(months, lab.code)
      .catch(() => null)
      .then((trendData) => {
        if (!cancelled) setTrend(trendData);
      });
    return () => { cancelled = true; };
  }, [months, reloadKey, lab.code]);

  if (!data) {
    return (
      <DashboardStateGate loading={loading} error={error === SERVICE_ERROR_KEY ? t(SERVICE_ERROR_KEY) : error} hasData={false} onRetry={reload}>
        {null}
      </DashboardStateGate>
    );
  }

  // Incubation and Ready to Read are Microbiology stages.
  const totalBottleneck =
    data.testingBottleneck +
    (lab.isPhyschem ? 0 : data.incubationBottleneck + data.readyToReadBottleneck) +
    data.reviewBottleneck +
    data.approvalBottleneck || 1;

  return (
    <>
      <PageHeader
        title={t("sectionHead.title")}
        subtitle={lab.code
          ? t("sectionHead.subtitleLab", { lab: t(`labName.${lab.code}`) })
          : t("sectionHead.subtitle")}
      >
        <Button
          variant="outlined"
          onClick={reload}
          disabled={loading}
          startIcon={<RefreshIcon />}
        >
          {t("common.refresh")}
        </Button>
        <Button
          component={Link}
          to={lab.workspace()}
          variant="contained"
          startIcon={<ScienceOutlinedIcon />}
        >
          {t("sectionHead.workspace")}
        </Button>
      </PageHeader>

      {qualityOutage && (
        <Box sx={{ mb: 2 }}>
          <PartialOutageBanner sources={[t("qualityTiles.source")]} onRetry={reload} />
        </Box>
      )}

      {/* Tier 1: Laboratory Overview KPI Strip */}
      <SummaryTiles
        tiles={[
          { label: t("tiles.activeTests"), value: data.activeTests, icon: <ScienceOutlinedIcon />, hint: t("tiles.activeTestsHint"), to: lab.workspace("?status=Active") },
          // Incubating and Ready to read are Microbiology only.
          ...(lab.isPhyschem
            ? []
            : [
                { label: t("tiles.incubating"), value: data.incubating, tone: "info" as const, icon: <ThermostatOutlinedIcon />, hint: t("tiles.incubatingHint"), to: lab.workspace("?view=kanban") },
                { label: t("tiles.readyToRead"), value: data.readyToRead, tone: "action" as const, icon: <VisibilityOutlinedIcon />, hint: t("tiles.readyToReadHint"), to: lab.workspace("?testStatus=ReadyToRead") }
              ]),
          { label: t("tiles.pendingReview"), value: data.pendingReview, tone: "action", icon: <RateReviewOutlinedIcon />, hint: t("tiles.pendingReviewHint"), to: lab.workspace("?status=UnderReview") },
          { label: t("tiles.pendingApproval"), value: data.pendingApproval, tone: "action", icon: <VerifiedUserOutlinedIcon />, hint: t("tiles.pendingApprovalHint"), to: lab.workspace("?status=UnderApproval") },
          { label: t("tiles.overdue"), value: data.overdue, tone: "detected", icon: <AccessTimeOutlinedIcon />, hint: t("tiles.overdueHint"), to: lab.workspace("?urgency=overdue") },
          {
            label: t("tiles.attentionItems"),
            value: data.attentionCount,
            tone: data.attentionCount > 0 ? "detected" : "notDetected",
            icon: <WarningAmberOutlinedIcon />,
            hint: t("tiles.attentionHint"),
            onClick: () => document.getElementById("attention-section")?.scrollIntoView({ behavior: "smooth" })
          },
          ...(canViewQuality
            ? [
                {
                  label: t("qualityTiles.labOpen"),
                  value: qualityDash
                    ? qualityDash.laboratoryOpenRecordsTotalCount +
                      qualityDash.laboratoryOpenActionsTotalCount
                    : "—",
                  tone: "info" as const,
                  icon: <ScienceOutlinedIcon />,
                  hint: t("qualityTiles.labOpenHint"),
                  to: "/quality"
                },
                {
                  label: t("qualityTiles.myOpen"),
                  value: qualityDash
                    ? qualityDash.myOpenRecordsTotalCount + qualityDash.myOpenActionsTotalCount
                    : "—",
                  tone: "info" as const,
                  icon: <RateReviewOutlinedIcon />,
                  hint: t("qualityTiles.myOpenHint"),
                  to: "/quality?owner=me"
                },
                {
                  label: t("qualityTiles.overdue"),
                  value: qualityDash
                    ? qualityDash.overdueRecordsTotalCount + qualityDash.overdueActionsTotalCount
                    : "—",
                  tone:
                    qualityDash &&
                    qualityDash.overdueRecordsTotalCount + qualityDash.overdueActionsTotalCount > 0
                      ? ("detected" as const)
                      : ("notDetected" as const),
                  icon: <AccessTimeOutlinedIcon />,
                  hint: t("qualityTiles.overdueHint"),
                  to: "/quality?overdue=true"
                }
              ]
            : [])
        ]}
      />

      {/* Tier 2: Workflow Bottlenecks Pipeline */}
      <Paper sx={{ p: 2, mb: 2.5 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
          <Box>
            <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
              {t("pipeline.title")}
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              {t("pipeline.intro")}
            </Typography>
          </Box>
        </Box>

        <Grid container spacing={1.5}>
          {/* These five are positions in one pipeline, not health states.
              They previously borrowed the status palette - which made stage 4
              permanently amber and stage 5 "good" regardless of the numbers,
              and painted stages 3 and 5 the identical green, so colour
              carried no stage identity at all. An ordered sequence wants an
              ordinal ramp: one hue deepening stage by stage. */}
          {[
            { label: lab.isPhyschem ? t("pipeline.testing") : t("pipeline.testingPreparation"), count: data.testingBottleneck, color: theme.custom.chartSequential[0], link: lab.workspace("?status=Active"), unit: t("pipeline.unitTests"), micro: false },
            { label: t("pipeline.incubation"), count: data.incubationBottleneck, color: theme.custom.chartSequential[1], link: lab.workspace(), unit: t("pipeline.unitTests"), micro: true },
            { label: t("pipeline.readyToRead"), count: data.readyToReadBottleneck, color: theme.custom.chartSequential[2], link: lab.workspace("?testStatus=ReadyToRead"), unit: t("pipeline.unitTests"), micro: true },
            { label: t("pipeline.scientificReview"), count: data.reviewBottleneck, color: theme.custom.chartSequential[3], link: lab.workspace("?status=UnderReview"), unit: t("pipeline.unitSamples"), micro: false },
            { label: t("pipeline.finalApproval"), count: data.approvalBottleneck, color: theme.custom.chartSequential[4], link: lab.workspace("?status=UnderApproval"), unit: t("pipeline.unitSamples"), micro: false }
          ]
            // Incubation stages are Microbiology only; numbering follows what is shown.
            .filter((stage) => !(lab.isPhyschem && stage.micro))
            .map((stage, idx, shown) => ({ ...stage, label: t("pipeline.numbered", { n: idx + 1, label: stage.label }), cols: 12 / shown.length }))
            .map((stage, idx) => (
            <Grid
              key={idx}
              size={{
                xs: 12,
                sm: 6,
                md: stage.cols
              }}>
              <Paper
                component={Link}
                to={stage.link}
                variant="outlined"
                sx={{
                  p: 1.5,
                  cursor: "pointer",
                  display: "block",
                  textDecoration: "none",
                  color: "inherit",
                  borderColor: "divider",
                  transition: "background-color 0.15s, border-color 0.15s",
                  "&:hover": { bgcolor: "action.hover", borderColor: stage.color }
                }}
              >
                <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>
                  {stage.label}
                </Typography>
                <Typography sx={{ fontSize: 22, fontWeight: 800, color: stage.color, my: 0.5 }}>
                  {stage.count}{" "}
                  <Box component="span" sx={{ fontSize: 12, fontWeight: 500, color: "text.secondary" }}>
                    {stage.unit}
                  </Box>
                </Typography>
                <LinearProgress
                  variant="determinate"
                  value={Math.min(100, Math.round((stage.count / totalBottleneck) * 100))}
                  sx={{
                    height: 6,
                    borderRadius: 3,
                    bgcolor: "action.selected",
                    "& .MuiLinearProgress-bar": { bgcolor: stage.color }
                  }}
                />
              </Paper>
            </Grid>
          ))}
        </Grid>
      </Paper>

      {/* Tier 3: Attention Required (Actionable Section) */}
      <Box id="attention-section" sx={{ mb: 2.5 }}>
        {data.attentionItems.length > 0 ? (
          <Paper sx={{ p: 2, borderLeft: `4px solid ${theme.custom.status.detected.text}` }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1.5 }}>
              <WarningAmberOutlinedIcon sx={{ color: theme.custom.status.detected.text }} />
              <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.custom.status.detected.text }}>
                {t("attention.title", { n: data.attentionItems.length })}
              </Typography>
            </Box>
            <Grid container spacing={1.5}>
              {data.attentionItems.map((item, idx) => {
                const itemLink = (item.actionType === "DelayedReview" || item.actionType === "DelayedApproval")
                  ? lab.workspace(`?sampleId=${item.sampleId}&openSummary=true`)
                  : lab.workspace(`?sampleId=${item.sampleId}`);

                return (
                  <Grid
                    key={`${item.actionType}-${item.sampleId}-${idx}`}
                    size={{
                      xs: 12,
                      md: 6
                    }}>
                    <Paper
                      variant="outlined"
                      sx={{
                        p: 1.5,
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                        gap: 1.5,
                        borderColor: "divider",
                        "&:hover": { bgcolor: "action.hover" }
                      }}
                    >
                      <Box>
                        <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
                          <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
                            {item.referenceNumber}: {item.subjectName}
                          </Typography>
                          <Box sx={{ display: "flex", gap: 0.5, flexWrap: "wrap" }}>
                            {item.testCodes.map((tc, tcIdx) => (
                              <Chip key={`${tcIdx}-${tc}`} label={tc} size="small" sx={{ fontSize: 12, height: 20 }} />
                            ))}
                          </Box>
                        </Box>
                        <Typography sx={{ fontSize: 12, color: theme.custom.status.detected.text, mt: 0.25 }}>
                          {item.reason}
                        </Typography>
                      </Box>
                      <Button
                        component={Link}
                        to={itemLink}
                        variant="outlined"
                        color="error"
                        size="small"
                        endIcon={<ArrowForwardOutlinedIcon />}
                        sx={{ textTransform: "none", fontSize: 12, fontWeight: 600, flexShrink: 0 }}
                      >
                        {t("attention.intervene")}
                      </Button>
                    </Paper>
                  </Grid>
                );
              })}
            </Grid>
          </Paper>
        ) : (
          <Paper sx={{ p: 2, display: "flex", alignItems: "center", gap: 1.5, borderLeft: `4px solid ${theme.custom.status.notDetected.text}` }}>
            <VerifiedUserOutlinedIcon sx={{ color: theme.custom.status.notDetected.text }} />
            <Box>
              <Typography sx={{ fontSize: 14, fontWeight: 700, color: theme.custom.status.notDetected.text }}>
                {t("attention.allClearTitle")}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                {t("attention.allClearBody")}
              </Typography>
            </Box>
          </Paper>
        )}
      </Box>

      {/* Tier 4: Review Queue & Approval Queue Summaries */}
      <Grid container spacing={2.5} sx={{ mb: 2.5 }}>
        {/* Review Queue Summary */}
        <Grid
          size={{
            xs: 12,
            lg: 6
          }}>
          <Paper sx={{ p: 2, height: "100%", display: "flex", flexDirection: "column" }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
              <Box>
                <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
                  {t("review.title", { n: data.reviewQueueCount })}
                </Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  {t("review.overdue")}{" "}
                  <Typography
                    component={Link}
                    to={lab.workspace("?status=UnderReview&workload=reviewOverdue")}
                    sx={{
                      fontSize: 12,
                      fontWeight: 700,
                      color: data.reviewQueueOverdueCount > 0 ? theme.custom.status.detected.text : "text.secondary",
                      textDecoration: "none",
                      "&:hover": { textDecoration: "underline" }
                    }}
                  >
                    {data.reviewQueueOverdueCount}
                  </Typography>
                  {" "}{t("review.oldest", { hours: data.reviewQueueOldestHours })}
                </Typography>
              </Box>
              <Button
                component={Link}
                to={lab.workspace("?status=UnderReview")}
                variant="text"
                size="small"
                sx={{ textTransform: "none", fontWeight: 600, fontSize: 12 }}
              >
                {t("review.viewAll")}
              </Button>
            </Box>

            {data.reviewQueueItems.length === 0 ? (
              <Typography sx={{ fontSize: 12, color: "text.secondary", py: 4, textAlign: "center" }}>
                {t("review.empty")}
              </Typography>
            ) : (
              <Box sx={{ overflowX: "auto", flex: 1 }}>
                <Table size="small">
                  <TableHead>
                    <TableRow sx={tableHeadSx}>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("columns.sampleRef")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("columns.tests")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("columns.analysts")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("columns.worstResult")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("columns.age")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>{t("columns.action")}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.reviewQueueItems.slice(0, 5).map((row) => (
                      <TableRow
                        key={`${row.sampleId}-${row.sectionId ?? "all"}`}
                        hover
                      >
                        <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>
                          <Typography
                            component={Link}
                            to={lab.workspace(`?sampleId=${row.sampleId}`)}
                            sx={{
                              fontSize: 12,
                              fontWeight: 700,
                              color: "text.primary",
                              textDecoration: "none",
                              "&:hover": { color: "primary.main", textDecoration: "underline" }
                            }}
                          >
                            {row.referenceNumber}
                          </Typography>
                          {row.sectionName && (
                            <Chip
                              label={row.sectionName}
                              size="small"
                              variant="outlined"
                              sx={{ fontSize: 12, height: 20, mt: 0.25, display: "inline-flex" }}
                            />
                          )}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          <Box sx={{ display: "flex", gap: 0.5, flexWrap: "wrap" }}>
                            {row.testCodes.map((code, cIdx) => (
                              <Chip key={`${cIdx}-${code}`} label={code} size="small" sx={{ fontSize: 12, height: 20 }} />
                            ))}
                          </Box>
                        </TableCell>
                        <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                          {row.analystNames.length > 0 ? row.analystNames.join(", ") : "—"}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          {row.worstResultLevel ? (
                            <Chip
                              label={row.worstResultLevel.replace(/([a-z])([A-Z])/g, "$1 $2")}
                              size="small"
                              sx={{
                                fontSize: 12,
                                height: 20,
                                bgcolor:
                                  row.worstResultLevel === "OutOfSpecification"
                                    ? theme.custom.status.detected.text + "22"
                                    : row.worstResultLevel === "ActionLevel" || row.worstResultLevel === "AlertLevel"
                                    ? theme.custom.status.inconclusive.text + "22"
                                    : theme.custom.status.notDetected.text + "22",
                                color:
                                  row.worstResultLevel === "OutOfSpecification"
                                    ? theme.custom.status.detected.text
                                    : row.worstResultLevel === "ActionLevel" || row.worstResultLevel === "AlertLevel"
                                    ? theme.custom.status.inconclusive.text
                                    : theme.custom.status.notDetected.text,
                                fontWeight: 700
                              }}
                            />
                          ) : (
                            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>—</Typography>
                          )}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12, color: row.ageHours >= 24 ? theme.custom.status.detected.text : "text.primary" }}>
                          {row.ageHours}h
                        </TableCell>
                        <TableCell sx={{ textAlign: "right" }}>
                          <Button
                            component={Link}
                            to={lab.workspace(`?sampleId=${row.sampleId}&openSummary=true`)}
                            variant="outlined"
                            size="small"
                            sx={{ textTransform: "none", fontSize: 12, py: 0.2, fontWeight: 600 }}
                          >
                            {t("review.action")}
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
            )}
          </Paper>
        </Grid>

        {/* Approval Queue Summary */}
        <Grid
          size={{
            xs: 12,
            lg: 6
          }}>
          <Paper sx={{ p: 2, height: "100%", display: "flex", flexDirection: "column" }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
              <Box>
                <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
                  {t("approval.title", { n: data.approvalQueueCount })}
                </Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  {t("review.overdue")}{" "}
                  <Typography
                    component={Link}
                    to={lab.workspace("?status=UnderApproval&workload=approvalOverdue")}
                    sx={{
                      fontSize: 12,
                      fontWeight: 700,
                      color: data.approvalQueueOverdueCount > 0 ? theme.custom.status.detected.text : "text.secondary",
                      textDecoration: "none",
                      "&:hover": { textDecoration: "underline" }
                    }}
                  >
                    {data.approvalQueueOverdueCount}
                  </Typography>
                  {" "}{t("approval.oldest", { hours: data.approvalQueueOldestHours })}
                </Typography>
              </Box>
              <Button
                component={Link}
                to={lab.workspace("?status=UnderApproval")}
                variant="text"
                size="small"
                sx={{ textTransform: "none", fontWeight: 600, fontSize: 12 }}
              >
                {t("approval.viewAll")}
              </Button>
            </Box>

            {data.approvalQueueItems.length === 0 ? (
              <Typography sx={{ fontSize: 12, color: "text.secondary", py: 4, textAlign: "center" }}>
                {t("approval.empty")}
              </Typography>
            ) : (
              <Box sx={{ overflowX: "auto", flex: 1 }}>
                <Table size="small">
                  <TableHead>
                    <TableRow sx={tableHeadSx}>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("columns.sampleRef")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("columns.tests")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("columns.reviewer")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("columns.age")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>{t("columns.action")}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.approvalQueueItems.slice(0, 5).map((row) => (
                      <TableRow
                        key={`${row.sampleId}-${row.sectionId ?? "all"}`}
                        hover
                      >
                        <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>
                          <Typography
                            component={Link}
                            to={lab.workspace(`?sampleId=${row.sampleId}`)}
                            sx={{
                              fontSize: 12,
                              fontWeight: 700,
                              color: "text.primary",
                              textDecoration: "none",
                              "&:hover": { color: "primary.main", textDecoration: "underline" }
                            }}
                          >
                            {row.referenceNumber}
                          </Typography>
                          {row.sectionName && (
                            <Chip
                              label={row.sectionName}
                              size="small"
                              variant="outlined"
                              sx={{ fontSize: 12, height: 20, mt: 0.25, display: "inline-flex" }}
                            />
                          )}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          <Box sx={{ display: "flex", gap: 0.5, flexWrap: "wrap" }}>
                            {row.testCodes.map((code, cIdx) => (
                              <Chip key={`${cIdx}-${code}`} label={code} size="small" sx={{ fontSize: 12, height: 20 }} />
                            ))}
                          </Box>
                        </TableCell>
                        <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>{row.reviewerName ?? "—"}</TableCell>
                        <TableCell sx={{ fontSize: 12, color: row.ageHours >= 24 ? theme.custom.status.detected.text : "text.primary" }}>{row.ageHours}h</TableCell>
                        <TableCell sx={{ textAlign: "right" }}>
                          <Button
                            component={Link}
                            to={lab.workspace(`?sampleId=${row.sampleId}&openSummary=true`)}
                            variant="outlined"
                            size="small"
                            color="success"
                            sx={{ textTransform: "none", fontSize: 12, py: 0.2, fontWeight: 600 }}
                          >
                            {t("approval.action")}
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
            )}
          </Paper>
        </Grid>
      </Grid>

      {/* Tier 5: Laboratory Workload & Incubation Status */}
      <Grid container spacing={2.5} sx={{ mb: 2.5 }}>
        {/* Analyst Workload Allocation */}
        <Grid
          size={{
            xs: 12,
            lg: lab.isPhyschem ? 12 : 7
          }}>
          <Paper sx={{ p: 2 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
              <PeopleAltOutlinedIcon sx={{ color: theme.palette.primary.main }} />
              <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
                {t("workload.title")}
              </Typography>
            </Box>
            <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 2 }}>
              {t("workload.intro")}
            </Typography>

            {data.analystWorkloads.length === 0 ? (
              <Typography sx={{ fontSize: 12, color: "text.secondary", py: 3, textAlign: "center" }}>
                {t("workload.empty")}
              </Typography>
            ) : (
              <Box sx={{ overflowX: "auto" }}>
                <Table size="small">
                  <TableHead>
                    <TableRow sx={tableHeadSx}>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("columns.analyst")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("workload.columns.activeAssigned")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("workload.columns.overdueTests")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("workload.columns.completedToday")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>{t("workload.columns.workspace")}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.analystWorkloads.map((a) => (
                      <TableRow
                        key={a.analystId}
                        hover
                      >
                        <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>
                          <Typography
                            component={Link}
                            to={lab.workspace(`?analystId=${a.analystId}`)}
                            sx={{
                              fontSize: 12,
                              fontWeight: 700,
                              color: "text.primary",
                              textDecoration: "none",
                              "&:hover": { color: "primary.main", textDecoration: "underline" }
                            }}
                          >
                            {a.analystName}
                          </Typography>
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          <Chip
                            label={t("workload.testsCount", { n: a.activeCount })}
                            size="small"
                            sx={{ fontSize: 12, fontWeight: 600, bgcolor: "action.selected" }}
                          />
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          {a.overdueCount > 0 ? (
                            <Chip
                              component={Link}
                              to={lab.workspace(`?analystId=${a.analystId}&urgency=overdue`)}
                              label={t("workload.overdueCount", { n: a.overdueCount })}
                              size="small"
                              clickable
                              sx={{ fontSize: 12, fontWeight: 700, bgcolor: theme.custom.status.detected.text + "22", color: theme.custom.status.detected.text }}
                            />
                          ) : (
                            <Typography sx={{ fontSize: 12, color: theme.custom.status.notDetected.text, fontWeight: 600 }}>0</Typography>
                          )}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12, fontWeight: 600, color: theme.custom.status.notDetected.text }}>
                          {a.completedTodayCount}
                        </TableCell>
                        <TableCell sx={{ textAlign: "right" }}>
                          <Button
                            component={Link}
                            to={lab.workspace(`?analystId=${a.analystId}`)}
                            variant="text"
                            size="small"
                            sx={{ textTransform: "none", fontSize: 12, fontWeight: 600 }}
                          >
                            {t("workload.filter")}
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
            )}
          </Paper>
        </Grid>

        {/* Incubation Summary */}
        {/* Incubation status - Microbiology only. */}
        {!lab.isPhyschem && (
          <Grid
            size={{
              xs: 12,
              lg: 5
            }}>
            <Paper sx={{ p: 2 }}>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1 }}>
                <ThermostatOutlinedIcon sx={{ color: theme.palette.primary.main }} />
                <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
                  {t("incubation.title")}
                </Typography>
              </Box>
              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 2 }}>
                {t("incubation.intro")}
              </Typography>

              {data.incubationSummary.length === 0 ? (
                <Typography sx={{ fontSize: 12, color: "text.secondary", py: 3, textAlign: "center" }}>
                  {t("incubation.empty")}
                </Typography>
              ) : (
                <Box sx={{ overflowX: "auto" }}>
                  <Table size="small">
                    <TableHead>
                      <TableRow sx={tableHeadSx}>
                        <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("incubation.columns.testType")}</TableCell>
                        <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("incubation.columns.readyToRead")}</TableCell>
                        <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("incubation.columns.stillIncubating")}</TableCell>
                        <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>{t("columns.action")}</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {data.incubationSummary.map((inc) => (
                        <TableRow
                          key={inc.testCode}
                          hover
                        >
                          <TableCell sx={{ fontSize: 12, fontWeight: 700 }}>
                            <Chip label={inc.testCode} size="small" sx={{ fontSize: 12, fontWeight: 600 }} />
                          </TableCell>
                          <TableCell sx={{ fontSize: 12 }}>
                            {inc.readyToRead > 0 ? (
                              <Chip
                                label={t("incubation.readyCount", { n: inc.readyToRead })}
                                size="small"
                                sx={{ fontSize: 12, fontWeight: 700, bgcolor: theme.custom.status.notDetected.text + "22", color: theme.custom.status.notDetected.text }}
                              />
                            ) : (
                              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>0</Typography>
                            )}
                          </TableCell>
                          <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                            {inc.incubating}
                          </TableCell>
                          <TableCell sx={{ textAlign: "right" }}>
                            <Button
                              component={Link}
                              to={lab.workspace(`?testStatus=${inc.readyToRead > 0 ? "ReadyToRead" : ""}`)}
                              variant="text"
                              size="small"
                              sx={{ textTransform: "none", fontSize: 12, fontWeight: 600 }}
                            >
                              {t("incubation.open")}
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </Box>
              )}
            </Paper>
          </Grid>
        )}
      </Grid>

      {/* Section Self-Assignment Control (Samples.AssignAnalyst only) */}
      <Box sx={{ mb: 2.5 }}>
        <SectionSelfAssignmentCard />
      </Box>

      {/* Tier 6: Laboratory Trends & Distribution */}
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid
          size={{
            xs: 12,
            md: 8
          }}>
          <SamplesTrendChart trend={trend} months={months} onMonthsChange={setMonths} />
        </Grid>
        <Grid
          size={{
            xs: 12,
            md: 4
          }}>
          <TestOrderStatusDonut statusDist={statusDist} />
        </Grid>
      </Grid>
    </>
  );
}
