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
  useTheme
} from "@mui/material";
import RateReviewOutlinedIcon from "@mui/icons-material/RateReviewOutlined";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import AccessTimeOutlinedIcon from "@mui/icons-material/AccessTimeOutlined";
import UndoOutlinedIcon from "@mui/icons-material/UndoOutlined";
import ArrowForwardOutlinedIcon from "@mui/icons-material/ArrowForwardOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import HistoryOutlinedIcon from "@mui/icons-material/HistoryOutlined";
import { Link } from "react-router-dom";
import { PageHeader } from "../../components/PageHeader";
import { SummaryTiles } from "../../components/configHierarchy/SummaryTiles";
import RefreshIcon from "@mui/icons-material/Refresh";
import { DashboardStateGate } from "./components/DashboardStateGate";
import { DashboardService } from "./services/DashboardService";
import { ReviewerDashboard } from "./types/dashboard";
import { brandColors, tableHeadSx } from "../../theme";
import { useDashboardLab } from "./DashboardLabContext";
import { useTranslation } from "../../i18n";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";

// Kept as a key so the translated text is looked up at render, not stored in the effect.
const SERVICE_ERROR_KEY = "reviewer.serviceError";

function formatAge(minutes: number): string {
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  const remainingMins = minutes % 60;
  if (hours < 24) return `${hours}h ${remainingMins}m`;
  const days = Math.floor(hours / 24);
  return `${days}d ${hours % 24}h`;
}

export function ReviewerDashboardPage() {
  const { t } = useTranslation("dashboard");
  const theme = useTheme();
  const lab = useDashboardLab();

  const [data, setData] = useState<ReviewerDashboard | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);
  const reload = () => setReloadKey((k) => k + 1);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    DashboardService.getReviewerDashboard(lab.code)
      .then((res) => {
        if (!cancelled) setData(res);
      })
      .catch((err) => {
        console.error("Failed to load reviewer dashboard:", err);
        if (!cancelled) setError(SERVICE_ERROR_KEY);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, [reloadKey, lab.code]);

  if (!data) {
    return (
      <DashboardStateGate loading={loading} error={error === SERVICE_ERROR_KEY ? t(SERVICE_ERROR_KEY) : error} hasData={false} onRetry={reload}>
        {null}
      </DashboardStateGate>
    );
  }

  return (
    <>
      <PageHeader
        title={t("reviewer.title")}
        subtitle={lab.code
          ? t("reviewer.subtitleLab", { lab: t(`labName.${lab.code}`) })
          : t("reviewer.subtitle")}
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
          {t("reviewer.workspace")}
        </Button>
      </PageHeader>

      {/* Tier 1: Summary Cards */}
      <SummaryTiles
        tiles={[
          {
            label: t("reviewer.tiles.pendingReview"),
            value: data.pendingReviewCount,
            tone: "action",
            icon: <RateReviewOutlinedIcon />,
            caption: t("common.samples"),
            hint: t("reviewer.tiles.pendingReviewHint"),
            to: lab.workspace("?status=UnderReview")
          },
          {
            label: t("reviewer.tiles.overdueReview"),
            value: data.overdueReviewCount,
            tone: "detected",
            icon: <AccessTimeOutlinedIcon />,
            caption: t("reviewer.tiles.overdueCaption"),
            hint: t("reviewer.tiles.overdueHint"),
            to: lab.workspace("?status=UnderReview&workload=reviewOverdue")
          },
          {
            label: t("reviewer.tiles.dueToday"),
            value: data.dueTodayCount,
            tone: "action",
            icon: <AccessTimeOutlinedIcon />,
            caption: t("reviewer.tiles.dueTodayCaption"),
            hint: t("reviewer.tiles.dueTodayHint"),
            to: lab.workspace("?status=UnderReview")
          },
          {
            label: t("reviewer.tiles.retests"),
            value: data.retestsInProgressCount,
            tone: "info",
            icon: <UndoOutlinedIcon />,
            caption: t("common.samples"),
            hint: t("reviewer.tiles.retestsHint"),
            to: lab.workspace("?workload=retestInProgress")
          },
          {
            label: t("reviewer.tiles.completedToday"),
            value: data.completedTodayCount,
            tone: "notDetected",
            icon: <CheckCircleOutlineIcon />,
            caption: t("reviewer.tiles.completedCaption"),
            hint: t("reviewer.tiles.completedHint")
          }
        ]}
      />

      {/* Tier 2: Attention Items (if any) */}
      {data.attentionItems.length > 0 && (
        <Paper sx={{ p: 2, mb: 2.5, borderLeft: `4px solid ${brandColors.err}`, bgcolor: "background.paper" }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1.5 }}>
            <WarningAmberOutlinedIcon sx={{ color: brandColors.err }} />
            <Typography sx={{ fontSize: 14, fontWeight: 700, color: brandColors.err }}>
              {t("reviewer.attention", { n: data.attentionItems.length })}
            </Typography>
          </Box>
          <Grid container spacing={1.5}>
            {data.attentionItems.map((item) => (
              <Grid
                key={item.sampleId}
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
                    gap: 1,
                    borderColor: "divider",
                    "&:hover": { bgcolor: "action.hover" }
                  }}
                >
                  <Box>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
                      <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
                        {item.referenceNumber}: {item.subjectName}
                      </Typography>
                      {item.testCodes.map((code) => (
                        <Chip key={code} label={code} size="small" sx={{ fontSize: 12, height: 20 }} />
                      ))}
                    </Box>
                    <Typography sx={{ fontSize: 12, color: brandColors.err, mt: 0.25 }}>
                      {item.reason}
                    </Typography>
                  </Box>
                  <Button
                    component={Link}
                    to={lab.workspace(`?sampleId=${item.sampleId}&openSummary=true`)}
                    variant="outlined"
                    color="error"
                    size="small"
                    endIcon={<ArrowForwardOutlinedIcon />}
                    sx={{ textTransform: "none", fontSize: 12, fontWeight: 600, flexShrink: 0 }}
                  >
                    {t("reviewer.reviewNow")}
                  </Button>
                </Paper>
              </Grid>
            ))}
          </Grid>
        </Paper>
      )}

      {/* Tier 3: Central Review Queue Table */}
      <Grid container spacing={2.5}>
        <Grid
          size={{
            xs: 12,
            lg: 8.5
          }}>
          <Paper sx={{ p: 2, mb: 2 }}>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
              <Box>
                <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main }}>
                  {t("reviewer.queue.title", { n: data.reviewQueue.length })}
                </Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  {t("reviewer.queue.intro")}
                </Typography>
              </Box>
              <Button
                component={Link}
                to={lab.workspace("?status=UnderReview")}
                variant="text"
                size="small"
                sx={{ textTransform: "none", fontWeight: 600 }}
              >
                {t("reviewer.queue.viewAll")}
              </Button>
            </Box>

            {data.reviewQueue.length === 0 ? (
              <Box sx={{ py: 6, textAlign: "center" }}>
                <CheckCircleOutlineIcon sx={{ color: brandColors.ok, fontSize: 48, mb: 1 }} />
                <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{t("reviewer.queue.clear")}</Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  {t("reviewer.queue.clearBody")}
                </Typography>
              </Box>
            ) : (
              <Box sx={{ overflowX: "auto" }}>
                <Table size="small">
                  <TableHead>
                    <TableRow sx={tableHeadSx}>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("reviewer.columns.sampleRef")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("reviewer.columns.itemLocation")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("reviewer.columns.tests")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("reviewer.columns.analysts")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("reviewer.columns.worstResult")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("reviewer.columns.inReview")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>{t("reviewer.columns.priority")}</TableCell>
                      <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>{t("reviewer.columns.action")}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.reviewQueue.map((row) => (
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
                              sx={{ fontSize: 12, height: 20, mt: 0.5, display: "inline-flex" }}
                            />
                          )}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          {row.subjectName}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          <Box sx={{ display: "flex", gap: 0.5, flexWrap: "wrap" }}>
                            {row.tests.map((t) => (
                              <Chip key={t.testOrderId} label={t.testCode} size="small" sx={{ fontSize: 12, height: 22, fontWeight: 600 }} />
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
                                    ? brandColors.err + "22"
                                    : row.worstResultLevel === "ActionLevel" || row.worstResultLevel === "AlertLevel"
                                    ? brandColors.warn + "22"
                                    : brandColors.ok + "22",
                                color:
                                  row.worstResultLevel === "OutOfSpecification"
                                    ? brandColors.err
                                    : row.worstResultLevel === "ActionLevel" || row.worstResultLevel === "AlertLevel"
                                    ? brandColors.warn
                                    : brandColors.ok,
                                fontWeight: 700
                              }}
                            />
                          ) : (
                            "—"
                          )}
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          <Typography
                            sx={{
                              fontSize: 12,
                              fontWeight: row.ageMinutes > 1440 ? 700 : 400,
                              color: row.ageMinutes > 1440 ? brandColors.err : "text.primary"
                            }}
                          >
                            {formatAge(row.ageMinutes)}
                          </Typography>
                        </TableCell>
                        <TableCell sx={{ fontSize: 12 }}>
                          <Chip
                            label={row.priority}
                            size="small"
                            sx={{
                              fontSize: 12,
                              height: 20,
                              fontWeight: 700,
                              bgcolor:
                                row.priority === "High"
                                  ? brandColors.err + "22"
                                  : row.priority === "Medium"
                                  ? brandColors.warn + "22"
                                  : "action.selected",
                              color:
                                row.priority === "High"
                                  ? brandColors.err
                                  : row.priority === "Medium"
                                  ? brandColors.warn
                                  : "text.secondary"
                            }}
                          />
                        </TableCell>
                        <TableCell sx={{ textAlign: "right" }}>
                          <Button
                            component={Link}
                            to={lab.workspace(`?sampleId=${row.sampleId}&openSummary=true`)}
                            variant="contained"
                            size="small"
                            startIcon={<RateReviewOutlinedIcon />}
                            sx={{ textTransform: "none", fontSize: 12, fontWeight: 700, py: 0.3 }}
                          >
                            {t("reviewer.review")}
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

        {/* Tier 4: Recently Reviewed Panel */}
        <Grid
          size={{
            xs: 12,
            lg: 3.5
          }}>
          <Paper sx={{ p: 2 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 1.5 }}>
              <HistoryOutlinedIcon sx={{ color: theme.palette.primary.main }} />
              <Typography sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}>
                {t("reviewer.recent.title")}
              </Typography>
            </Box>
            <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 2 }}>
              {t("reviewer.recent.intro")}
            </Typography>

            {data.recentlyReviewed.length === 0 ? (
              <Typography sx={{ fontSize: 12, color: "text.secondary", py: 3, textAlign: "center" }}>
                {t("reviewer.recent.empty")}
              </Typography>
            ) : (
              <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
                {data.recentlyReviewed.map((rec) => (
                  <Paper
                    key={rec.sampleId}
                    component={Link}
                    to={lab.workspace(`?sampleId=${rec.sampleId}`)}
                    variant="outlined"
                    sx={{
                      p: 1.5,
                      cursor: "pointer",
                      display: "block",
                      textDecoration: "none",
                      color: "inherit",
                      "&:hover": { bgcolor: "action.hover" }
                    }}
                  >
                    <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                      <Box>
                        <Typography sx={{ fontSize: 12, fontWeight: 700 }}>
                          {rec.referenceNumber}
                        </Typography>
                        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                          {rec.subjectName}
                        </Typography>
                      </Box>
                      <Chip
                        label={rec.status}
                        size="small"
                        sx={{ fontSize: 12, height: 20, fontWeight: 700 }}
                      />
                    </Box>
                    <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 1 }}>
                      <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                        {t("reviewer.recent.tests", { code: rec.testCode })}
                      </Typography>
                      <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                        {formatDateTimeWithTimeZone(rec.reviewedAt)}
                      </Typography>
                    </Box>
                  </Paper>
                ))}
              </Box>
            )}
          </Paper>
        </Grid>
      </Grid>
    </>
  );
}
