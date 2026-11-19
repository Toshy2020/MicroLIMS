import React, { useState, useEffect, useCallback } from "react";
import {
  Box,
  Typography,
  Paper,
  Grid,
  Card,
  CardContent,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  LinearProgress,
  Chip,
  Alert,
  CircularProgress,
  Stack,
  useTheme
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import GridOnIcon from "@mui/icons-material/GridOn";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";

import { PageHeader } from "../../../components/PageHeader";
import { tableHeadSx } from "../../../theme";
import { trainingMatrixService } from "../services/trainingMatrixService";
import type { ComplianceKpiSummaryDto } from "../types/trainingMatrixTypes";
import { compactChipStrongSx, compactChipSx } from "../documentControlStyles";

export function ComplianceDashboardPage() {
  const { t } = useTranslation("documentControl");
  const theme = useTheme();
  const navigate = useNavigate();

  const [kpis, setKpis] = useState<ComplianceKpiSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadKpis = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await trainingMatrixService.getComplianceKpis();
      setKpis(data);
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("compliance.loadError")));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    loadKpis();
  }, [loadKpis]);

  return (
    <Box sx={{ pb: 4 }}>
      <PageHeader
        title={t("compliance.title")}
        subtitle={t("compliance.subtitle")}
      >
        <Stack direction="row" spacing={1.5}>
          <Button
            variant="outlined"
            startIcon={<RefreshIcon />}
            onClick={() => loadKpis()}
            disabled={loading}
          >
            {t("compliance.refresh")}
          </Button>
          <Button
            variant="contained"
            color="primary"
            startIcon={<GridOnIcon />}
            onClick={() => navigate("/document-control/training-matrix")}
          >
            {t("compliance.viewFullMatrix")}
          </Button>
        </Stack>
      </PageHeader>

      {error && (
        <Alert severity="error" sx={{ mb: 3 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", py: 10 }}>
          <CircularProgress size={40} sx={{ mb: 2 }} />
          <Typography variant="body2" sx={{
            color: "text.secondary"
          }}>
            {t("compliance.loading")}
          </Typography>
        </Box>
      ) : kpis && (
        <>
          {/* Top KPI Cards */}
          <Grid container spacing={2.5} sx={{ mb: 4 }}>
            <Grid
              size={{
                xs: 12,
                sm: 6,
                md: 3
              }}>
              <Card variant="outlined" sx={{ borderTop: "4px solid", borderColor: "primary.main" }}>
                <CardContent>
                  <Typography
                    variant="caption"
                    sx={{
                      color: "text.secondary",
                      fontWeight: "bold"
                    }}>
                    {t("compliance.kpis.overallRate")}
                  </Typography>
                  <Typography
                    variant="h3"
                    sx={{
                      fontWeight: "bold",
                      color: "primary.main",
                      my: 0.5
                    }}>
                    {kpis.overallComplianceRatePercentage}%
                  </Typography>
                  <Typography variant="caption" sx={{
                    color: "text.secondary"
                  }}>
                    {t("compliance.kpis.requiredAssignments", { completed: kpis.completedAssignments, total: kpis.totalRequiredAssignments })}
                  </Typography>
                  <LinearProgress
                    variant="determinate"
                    value={kpis.overallComplianceRatePercentage}
                    color={kpis.overallComplianceRatePercentage >= 90 ? "success" : "warning"}
                    sx={{ height: 6, borderRadius: 1, mt: 1.5 }}
                  />
                </CardContent>
              </Card>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 6,
                md: 3
              }}>
              <Card variant="outlined" sx={{ borderTop: "4px solid", borderColor: "success.main" }}>
                <CardContent>
                  <Typography
                    variant="caption"
                    sx={{
                      color: "text.secondary",
                      fontWeight: "bold"
                    }}>
                    {t("compliance.kpis.qualifiedCompleted")}
                  </Typography>
                  <Typography
                    variant="h3"
                    sx={{
                      fontWeight: "bold",
                      color: "success.main",
                      my: 0.5
                    }}>
                    {kpis.completedAssignments}
                  </Typography>
                  <Typography variant="caption" sx={{
                    color: "text.secondary"
                  }}>
                    {t("compliance.kpis.acrossUsersAndDocs", { users: kpis.totalTrackedUsers, docs: kpis.totalEffectiveDocuments })}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 6,
                md: 3
              }}>
              <Card
                variant="outlined"
                sx={{
                  borderTop: "4px solid",
                  borderColor: "error.main",
                  cursor: "pointer",
                  "&:hover": { bgcolor: "error.50" }
                }}
                onClick={() => navigate("/document-control/training-matrix")}
              >
                <CardContent>
                  <Typography
                    variant="caption"
                    sx={{
                      color: "text.secondary",
                      fontWeight: "bold"
                    }}>
                    {t("compliance.kpis.overdueExceptions")}
                  </Typography>
                  <Typography
                    variant="h3"
                    sx={{
                      fontWeight: "bold",
                      color: "error.main",
                      my: 0.5
                    }}>
                    {kpis.overdueAssignments}
                  </Typography>
                  <Typography
                    variant="caption"
                    sx={{
                      color: "error.main",
                      display: "flex",
                      alignItems: "center",
                      gap: 0.5
                    }}>
                    <ErrorOutlineIcon fontSize="inherit" />
                    {t("compliance.kpis.actionRequiredFilter")}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>

            <Grid
              size={{
                xs: 12,
                sm: 6,
                md: 3
              }}>
              <Card variant="outlined" sx={{ borderTop: "4px solid", borderColor: "warning.main" }}>
                <CardContent>
                  <Typography
                    variant="caption"
                    sx={{
                      color: "text.secondary",
                      fontWeight: "bold"
                    }}>
                    {t("compliance.kpis.supersededGaps")}
                  </Typography>
                  <Typography
                    variant="h3"
                    sx={{
                      fontWeight: "bold",
                      color: (t) => t.palette.mode === "dark" ? "warning.light" : "warning.dark",
                      my: 0.5
                    }}>
                    {kpis.supersededGapCount}
                  </Typography>
                  <Typography
                    variant="caption"
                    sx={{
                      color: "text.secondary",
                      display: "flex",
                      alignItems: "center",
                      gap: 0.5
                    }}>
                    <WarningAmberIcon fontSize="inherit" />
                    {t("compliance.kpis.priorRevisionOnly")}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
          </Grid>

          {/* Department Rankings & Top Overdue Documents */}
          <Grid container spacing={3}>
            {/* Department Ranking */}
            <Grid
              size={{
                xs: 12,
                md: 7
              }}>
              <Paper variant="outlined" sx={{ p: 2.5 }}>
                <Typography variant="h6" gutterBottom sx={{
                  fontWeight: "bold"
                }}>
                  {t("compliance.deptRanking.title")}
                </Typography>
                <Typography
                  variant="body2"
                  sx={{
                    color: "text.secondary",
                    mb: 2
                  }}>
                  {t("compliance.deptRanking.subtitle")}
                </Typography>

                <TableContainer>
                  <Table size="small">
                    <TableHead sx={tableHeadSx(theme)}>
                      <TableRow>
                        <TableCell sx={{ fontWeight: 700 }}>{t("compliance.deptRanking.columns.department")}</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700 }}>{t("compliance.deptRanking.columns.required")}</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700 }}>{t("compliance.deptRanking.columns.qualified")}</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700 }}>{t("compliance.deptRanking.columns.overdue")}</TableCell>
                        <TableCell sx={{ fontWeight: 700, minWidth: 150 }}>{t("compliance.deptRanking.columns.complianceRate")}</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {kpis.departmentCompliance.length === 0 ? (
                        <TableRow>
                          <TableCell colSpan={5} align="center" sx={{ py: 3 }}>
                            {t("compliance.deptRanking.empty")}
                          </TableCell>
                        </TableRow>
                      ) : (
                        kpis.departmentCompliance.map((dept) => (
                          <TableRow key={dept.departmentId} hover>
                            <TableCell sx={{ fontWeight: 600 }}>{dept.departmentName}</TableCell>
                            <TableCell align="right">{dept.requiredCount}</TableCell>
                            <TableCell align="right">{dept.completedCount}</TableCell>
                            <TableCell align="right">
                              {dept.overdueCount > 0 ? (
                                <Chip label={dept.overdueCount} size="small" color="error" sx={compactChipSx} />
                              ) : (
                                "0"
                              )}
                            </TableCell>
                            <TableCell>
                              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                                <LinearProgress
                                  variant="determinate"
                                  value={dept.complianceRatePercentage}
                                  color={dept.complianceRatePercentage >= 90 ? "success" : "warning"}
                                  sx={{ flexGrow: 1, height: 6, borderRadius: 1 }}
                                />
                                <Typography
                                  variant="caption"
                                  sx={{
                                    fontWeight: "bold",
                                    minWidth: 40
                                  }}>
                                  {dept.complianceRatePercentage}%
                                </Typography>
                              </Box>
                            </TableCell>
                          </TableRow>
                        ))
                      )}
                    </TableBody>
                  </Table>
                </TableContainer>
              </Paper>
            </Grid>

            {/* Top Overdue Documents */}
            <Grid
              size={{
                xs: 12,
                md: 5
              }}>
              <Paper variant="outlined" sx={{ p: 2.5 }}>
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                  <Typography variant="h6" sx={{
                    fontWeight: "bold"
                  }}>
                    {t("compliance.topOverdue.title")}
                  </Typography>
                  <Button
                    size="small"
                    endIcon={<ArrowForwardIcon />}
                    onClick={() => navigate("/document-control/training-matrix")}
                  >
                    {t("compliance.topOverdue.viewAll")}
                  </Button>
                </Box>
                <Typography
                  variant="body2"
                  sx={{
                    color: "text.secondary",
                    mb: 2
                  }}>
                  {t("compliance.topOverdue.subtitle")}
                </Typography>

                <TableContainer>
                  <Table size="small">
                    <TableHead sx={tableHeadSx(theme)}>
                      <TableRow>
                        <TableCell sx={{ fontWeight: 700 }}>{t("compliance.topOverdue.columns.codeAndTitle")}</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700 }}>{t("compliance.topOverdue.columns.overdue")}</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {kpis.topOverdueDocuments.length === 0 ? (
                        <TableRow>
                          <TableCell colSpan={2} align="center" sx={{ py: 3 }}>
                            <CheckCircleOutlineIcon color="success" sx={{ mb: 0.5 }} />
                            <Typography variant="body2" sx={{
                              color: "text.secondary"
                            }}>
                              {t("compliance.topOverdue.empty")}
                            </Typography>
                          </TableCell>
                        </TableRow>
                      ) : (
                        kpis.topOverdueDocuments.map((doc) => (
                          <TableRow key={doc.documentMasterId} hover>
                            <TableCell>
                              <Typography
                                variant="subtitle2"
                                sx={{
                                  fontWeight: "bold",
                                  color: "primary.main"
                                }}>
                                {doc.companyDocumentCode}
                              </Typography>
                              <Typography variant="caption" sx={{
                                color: "text.secondary"
                              }}>
                                {doc.title}
                              </Typography>
                            </TableCell>
                            <TableCell align="right">
                              <Chip
                                label={t("compliance.topOverdue.overdueCount", { count: doc.overdueCount })}
                                size="small"
                                color="error"
                                sx={compactChipStrongSx}
                              />
                            </TableCell>
                          </TableRow>
                        ))
                      )}
                    </TableBody>
                  </Table>
                </TableContainer>
              </Paper>
            </Grid>
          </Grid>
        </>
      )}
    </Box>
  );
}
