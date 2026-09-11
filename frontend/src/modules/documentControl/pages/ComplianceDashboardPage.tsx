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
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import { useNavigate } from "react-router-dom";

import { PageHeader } from "../../../components/PageHeader";
import { tableHeadSx } from "../../../theme";
import { trainingMatrixService } from "../services/trainingMatrixService";
import type { ComplianceKpiSummaryDto } from "../types/trainingMatrixTypes";

export function ComplianceDashboardPage() {
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
    } catch (err: any) {
      setError(err?.response?.data?.message || err?.message || "Failed to load compliance KPIs.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadKpis();
  }, [loadKpis]);

  return (
    <Box sx={{ pb: 4 }}>
      <PageHeader
        title="Training Compliance Dashboard"
        subtitle="Executive & operational compliance metrics, departmental ranking, and overdue exceptions."
      >
        <Stack direction="row" spacing={1.5}>
          <Button
            variant="outlined"
            startIcon={<RefreshIcon />}
            onClick={() => loadKpis()}
            disabled={loading}
          >
            Refresh
          </Button>
          <Button
            variant="contained"
            color="primary"
            startIcon={<GridOnIcon />}
            onClick={() => navigate("/document-control/training-matrix")}
          >
            View Full Matrix
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
          <Typography variant="body2" color="text.secondary">
            Aggregating organizational training compliance metrics...
          </Typography>
        </Box>
      ) : kpis && (
        <>
          {/* Top KPI Cards */}
          <Grid container spacing={2.5} sx={{ mb: 4 }}>
            <Grid item xs={12} sm={6} md={3}>
              <Card variant="outlined" sx={{ borderTop: "4px solid", borderColor: "primary.main" }}>
                <CardContent>
                  <Typography variant="caption" color="text.secondary" fontWeight="bold">
                    OVERALL COMPLIANCE RATE
                  </Typography>
                  <Typography variant="h3" fontWeight="bold" color="primary.main" sx={{ my: 0.5 }}>
                    {kpis.overallComplianceRatePercentage}%
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {kpis.completedAssignments} / {kpis.totalRequiredAssignments} required training assignments
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

            <Grid item xs={12} sm={6} md={3}>
              <Card variant="outlined" sx={{ borderTop: "4px solid", borderColor: "success.main" }}>
                <CardContent>
                  <Typography variant="caption" color="text.secondary" fontWeight="bold">
                    QUALIFIED & COMPLETED
                  </Typography>
                  <Typography variant="h3" fontWeight="bold" color="success.main" sx={{ my: 0.5 }}>
                    {kpis.completedAssignments}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    Across {kpis.totalTrackedUsers} personnel & {kpis.totalEffectiveDocuments} effective documents
                  </Typography>
                </CardContent>
              </Card>
            </Grid>

            <Grid item xs={12} sm={6} md={3}>
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
                  <Typography variant="caption" color="text.secondary" fontWeight="bold">
                    OVERDUE EXCEPTIONS
                  </Typography>
                  <Typography variant="h3" fontWeight="bold" color="error.main" sx={{ my: 0.5 }}>
                    {kpis.overdueAssignments}
                  </Typography>
                  <Typography variant="caption" color="error.main" sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                    <ErrorOutlineIcon fontSize="inherit" />
                    Action required — click to filter matrix
                  </Typography>
                </CardContent>
              </Card>
            </Grid>

            <Grid item xs={12} sm={6} md={3}>
              <Card variant="outlined" sx={{ borderTop: "4px solid", borderColor: "warning.main" }}>
                <CardContent>
                  <Typography variant="caption" color="text.secondary" fontWeight="bold">
                    SUPERSEDED GAPS
                  </Typography>
                  <Typography variant="h3" fontWeight="bold" sx={{ color: (t) => t.palette.mode === "dark" ? "warning.light" : "#e65100", my: 0.5 }}>
                    {kpis.supersededGapCount}
                  </Typography>
                  <Typography variant="caption" color="text.secondary" sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                    <WarningAmberIcon fontSize="inherit" />
                    Qualified on prior revision only
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
          </Grid>

          {/* Department Rankings & Top Overdue Documents */}
          <Grid container spacing={3}>
            {/* Department Ranking */}
            <Grid item xs={12} md={7}>
              <Paper variant="outlined" sx={{ p: 2.5 }}>
                <Typography variant="h6" fontWeight="bold" gutterBottom>
                  Departmental Compliance Ranking
                </Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                  Organizational compliance breakdown categorized by department.
                </Typography>

                <TableContainer>
                  <Table size="small">
                    <TableHead sx={tableHeadSx(theme)}>
                      <TableRow>
                        <TableCell sx={{ fontWeight: 700 }}>Department</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700 }}>Required</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700 }}>Qualified</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700 }}>Overdue</TableCell>
                        <TableCell sx={{ fontWeight: 700, minWidth: 150 }}>Compliance Rate</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {kpis.departmentCompliance.length === 0 ? (
                        <TableRow>
                          <TableCell colSpan={5} align="center" sx={{ py: 3 }}>
                            No department training data recorded.
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
                                <Chip label={dept.overdueCount} size="small" color="error" sx={{ height: 20, fontSize: 10 }} />
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
                                <Typography variant="caption" fontWeight="bold" sx={{ minWidth: 40 }}>
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
            <Grid item xs={12} md={5}>
              <Paper variant="outlined" sx={{ p: 2.5 }}>
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                  <Typography variant="h6" fontWeight="bold">
                    Top Overdue Documents
                  </Typography>
                  <Button
                    size="small"
                    endIcon={<ArrowForwardIcon />}
                    onClick={() => navigate("/document-control/training-matrix")}
                  >
                    View All
                  </Button>
                </Box>
                <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                  Controlled documents with the highest volume of overdue reading assignments.
                </Typography>

                <TableContainer>
                  <Table size="small">
                    <TableHead sx={tableHeadSx(theme)}>
                      <TableRow>
                        <TableCell sx={{ fontWeight: 700 }}>Document Code & Title</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700 }}>Overdue</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {kpis.topOverdueDocuments.length === 0 ? (
                        <TableRow>
                          <TableCell colSpan={2} align="center" sx={{ py: 3 }}>
                            <CheckCircleOutlineIcon color="success" sx={{ mb: 0.5 }} />
                            <Typography variant="body2" color="text.secondary">
                              Zero overdue documents! All assignments are on schedule.
                            </Typography>
                          </TableCell>
                        </TableRow>
                      ) : (
                        kpis.topOverdueDocuments.map((doc) => (
                          <TableRow key={doc.documentMasterId} hover>
                            <TableCell>
                              <Typography variant="subtitle2" fontWeight="bold" color="primary.main">
                                {doc.companyDocumentCode}
                              </Typography>
                              <Typography variant="caption" color="text.secondary">
                                {doc.title}
                              </Typography>
                            </TableCell>
                            <TableCell align="right">
                              <Chip
                                label={`${doc.overdueCount} Overdue`}
                                size="small"
                                color="error"
                                sx={{ height: 20, fontSize: 10, fontWeight: 700 }}
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
