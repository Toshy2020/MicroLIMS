import React, { useState, useEffect, useCallback, useMemo } from "react";
import {
  Box,
  Typography,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  Button,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  TextField,
  InputAdornment,
  IconButton,
  Tooltip,
  Drawer,
  Divider,
  CircularProgress,
  Alert,
  Stack,
  Card,
  CardContent,
  Grid
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import RefreshIcon from "@mui/icons-material/Refresh";
import FilterListIcon from "@mui/icons-material/FilterList";
import ClearIcon from "@mui/icons-material/Clear";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ScheduleIcon from "@mui/icons-material/Schedule";
import ErrorIcon from "@mui/icons-material/Error";
import WarningIcon from "@mui/icons-material/Warning";
import RemoveIcon from "@mui/icons-material/Remove";
import CloseIcon from "@mui/icons-material/Close";
import PersonIcon from "@mui/icons-material/Person";
import AssessmentOutlinedIcon from "@mui/icons-material/AssessmentOutlined";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";

import { PageHeader } from "../../../components/PageHeader";
import { trainingMatrixService } from "../services/trainingMatrixService";
import { documentControlService } from "../services/documentControlService";
import type {
  TrainingMatrixGridDto,
  TrainingMatrixCellDto,
  TrainingMatrixUserHeaderDto,
  TrainingMatrixDocumentHeaderDto,
  UserComplianceDetailDto,
  DocumentComplianceDetailDto
} from "../types/trainingMatrixTypes";
import type { DocumentDepartmentDto } from "../types/documentControlTypes";
import { compactChipStrongSx, compactChipSx } from "../documentControlStyles";
import { formatLabCalendarDate } from "../../../utils/formatDate";

export function TrainingMatrixPage() {
  const { t } = useTranslation("documentControl");
  const navigate = useNavigate();

  // Grid State
  const [gridData, setGridData] = useState<TrainingMatrixGridDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [departmentId, setDepartmentId] = useState<string>("ALL");
  const [complianceStatus, setComplianceStatus] = useState<string>("ALL");
  const [searchQuery, setSearchQuery] = useState("");
  const [departments, setDepartments] = useState<DocumentDepartmentDto[]>([]);

  // Drilldown Drawers
  const [selectedCell, setSelectedCell] = useState<TrainingMatrixCellDto | null>(null);
  const [selectedUser, setSelectedUser] = useState<TrainingMatrixUserHeaderDto | null>(null);
  const [userCompliance, setUserCompliance] = useState<UserComplianceDetailDto | null>(null);
  const [selectedDoc, setSelectedDoc] = useState<TrainingMatrixDocumentHeaderDto | null>(null);
  const [docCompliance, setDocCompliance] = useState<DocumentComplianceDetailDto | null>(null);
  const [drawerLoading, setDrawerLoading] = useState(false);

  // Load Departments
  useEffect(() => {
    documentControlService.getDepartments()
      .then(setDepartments)
      .catch((err) => console.error("Failed to load departments:", err));
  }, []);

  // Fetch Matrix Grid
  const loadMatrix = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await trainingMatrixService.getMatrixGrid({
        departmentId: departmentId === "ALL" ? undefined : parseInt(departmentId, 10),
        complianceStatus: complianceStatus === "ALL" ? undefined : complianceStatus
      });
      setGridData(data);
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("trainingMatrix.loadError")));
    } finally {
      setLoading(false);
    }
  }, [departmentId, complianceStatus, t]);

  useEffect(() => {
    loadMatrix();
  }, [loadMatrix]);

  // Filtered Users (row search)
  const filteredUsers = useMemo(() => {
    if (!gridData) return [];
    if (!searchQuery.trim()) return gridData.users;
    const q = searchQuery.toLowerCase();
    return gridData.users.filter(
      (u) =>
        u.fullName.toLowerCase().includes(q) ||
        u.username.toLowerCase().includes(q) ||
        u.roleName.toLowerCase().includes(q)
    );
  }, [gridData, searchQuery]);

  // Lookup map: (UserId, DocumentMasterId) -> Cell
  const cellMap = useMemo(() => {
    const map = new Map<string, TrainingMatrixCellDto>();
    if (gridData) {
      for (const cell of gridData.cells) {
        map.set(`${cell.userId}_${cell.documentMasterId}`, cell);
      }
    }
    return map;
  }, [gridData]);

  // Open User Drilldown
  const handleUserClick = async (user: TrainingMatrixUserHeaderDto) => {
    setSelectedUser(user);
    setSelectedDoc(null);
    setSelectedCell(null);
    setDrawerLoading(true);
    try {
      const detail = await trainingMatrixService.getUserCompliance(user.userId);
      setUserCompliance(detail);
    } catch (err) {
      console.error(err);
    } finally {
      setDrawerLoading(false);
    }
  };

  // Open Document Drilldown
  const handleDocClick = async (doc: TrainingMatrixDocumentHeaderDto) => {
    setSelectedDoc(doc);
    setSelectedUser(null);
    setSelectedCell(null);
    setDrawerLoading(true);
    try {
      const detail = await trainingMatrixService.getDocumentCompliance(doc.documentMasterId);
      setDocCompliance(detail);
    } catch (err) {
      console.error(err);
    } finally {
      setDrawerLoading(false);
    }
  };

  // Cell status renderer helper
  const renderCellBadge = (cell?: TrainingMatrixCellDto) => {
    if (!cell || cell.cellStatus === "NotAssigned") {
      return (
        <Tooltip title={t("trainingMatrix.statuses.notAssignedTooltip")}>
          <Box sx={{ display: "flex", justifyContent: "center", color: "text.disabled" }}>
            <RemoveIcon fontSize="small" />
          </Box>
        </Tooltip>
      );
    }

    switch (cell.cellStatus) {
      case "Qualified":
        return (
          <Tooltip title={t("trainingMatrix.statuses.qualifiedTooltip", { rev: cell.assignedRevisionNumber || cell.currentEffectiveRevisionNumber })}>
            <Chip
              icon={<CheckCircleIcon sx={{ fontSize: "14px !important" }} />}
              label={t("trainingMatrix.statuses.qualified")}
              size="small"
              color="success"
              sx={compactChipStrongSx}
              onClick={() => setSelectedCell(cell)}
            />
          </Tooltip>
        );
      case "Pending":
        return (
          <Tooltip title={t("trainingMatrix.statuses.pendingTooltip", { date: cell.dueDateUtc ? formatLabCalendarDate(cell.dueDateUtc) : t("common.tbd") })}>
            <Chip
              icon={<ScheduleIcon sx={{ fontSize: "14px !important" }} />}
              label={t("trainingMatrix.statuses.pending")}
              size="small"
              color="warning"
              sx={compactChipStrongSx}
              onClick={() => setSelectedCell(cell)}
            />
          </Tooltip>
        );
      case "Overdue":
        return (
          <Tooltip title={t("trainingMatrix.statuses.overdueTooltip", { days: cell.daysRemainingOrOverdue ? Math.abs(cell.daysRemainingOrOverdue) : "" })}>
            <Chip
              icon={<ErrorIcon sx={{ fontSize: "14px !important" }} />}
              label={t("trainingMatrix.statuses.overdue")}
              size="small"
              color="error"
              sx={compactChipStrongSx}
              onClick={() => setSelectedCell(cell)}
            />
          </Tooltip>
        );
      case "TrainedOnSupersededOnly":
        return (
          <Tooltip title={t("trainingMatrix.statuses.trainedOnSupersededOnlyTooltip")}>
            <Chip
              icon={<WarningIcon sx={{ fontSize: "14px !important" }} />}
              label={t("trainingMatrix.statuses.trainedOnSupersededOnly")}
              size="small"
              color="warning"
              sx={compactChipStrongSx}
              onClick={() => setSelectedCell(cell)}
            />
          </Tooltip>
        );
      default:
        return (
          <Chip
            label={cell.cellStatus}
            size="small"
            variant="outlined"
            sx={compactChipSx}
            onClick={() => setSelectedCell(cell)}
          />
        );
    }
  };

  return (
    <Box sx={{ pb: 4 }}>
      <PageHeader
        title={t("trainingMatrix.title")}
        subtitle={t("trainingMatrix.subtitle")}
      >
        <Button
          variant="outlined"
          startIcon={<AssessmentOutlinedIcon />}
          onClick={() => navigate("/document-control/compliance-dashboard")}
        >
          {t("trainingMatrix.complianceDashboard")}
        </Button>
      </PageHeader>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {/* KPI Summary Bar */}
      {gridData && (
        <Grid container spacing={2} sx={{ mb: 3 }}>
          <Grid
            size={{
              xs: 12,
              sm: 6,
              md: 2.4
            }}>
            <Card variant="outlined">
              <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
                <Typography
                  variant="caption"
                  sx={{
                    color: "text.secondary",
                    fontWeight: "bold"
                  }}>
                  {t("trainingMatrix.kpis.personnelTracked")}
                </Typography>
                <Typography variant="h5" sx={{
                  fontWeight: "bold"
                }}>
                  {gridData.totalUsers}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 6,
              md: 2.4
            }}>
            <Card variant="outlined" sx={{ bgcolor: "success.50" }}>
              <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
                <Typography
                  variant="caption"
                  sx={{
                    color: "success.main",
                    fontWeight: "bold"
                  }}>
                  {t("trainingMatrix.kpis.qualifiedCells")}
                </Typography>
                <Typography
                  variant="h5"
                  sx={{
                    fontWeight: "bold",
                    color: "success.main"
                  }}>
                  {gridData.totalCompliantCells}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 6,
              md: 2.4
            }}>
            <Card variant="outlined" sx={{ bgcolor: "warning.50" }}>
              <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
                <Typography
                  variant="caption"
                  sx={{
                    color: "warning.main",
                    fontWeight: "bold"
                  }}>
                  {t("trainingMatrix.kpis.pendingCells")}
                </Typography>
                <Typography
                  variant="h5"
                  sx={{
                    fontWeight: "bold",
                    color: "warning.main"
                  }}>
                  {gridData.totalPendingCells}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 6,
              md: 2.4
            }}>
            <Card variant="outlined" sx={{ bgcolor: "error.50" }}>
              <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
                <Typography
                  variant="caption"
                  sx={{
                    color: "error.main",
                    fontWeight: "bold"
                  }}>
                  {t("trainingMatrix.kpis.overdueCells")}
                </Typography>
                <Typography
                  variant="h5"
                  sx={{
                    fontWeight: "bold",
                    color: "error.main"
                  }}>
                  {gridData.totalOverdueCells}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid
            size={{
              xs: 12,
              sm: 6,
              md: 2.4
            }}>
            <Card variant="outlined" sx={{ bgcolor: (t) => t.custom.status.action.bg}}>
              <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
                <Typography variant="caption" sx={{ color: (t) => t.palette.mode === "dark" ? "warning.light" : "warning.dark", fontWeight: "bold" }}>
                  {t("trainingMatrix.kpis.supersededGaps")}
                </Typography>
                <Typography
                  variant="h5"
                  sx={{
                    fontWeight: "bold",
                    color: (t) => t.palette.mode === "dark" ? "warning.light" : "warning.dark"
                  }}>
                  {gridData.totalSupersededGapCells}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        </Grid>
      )}

      {/* Filter Toolbar */}
      <Paper variant="outlined" sx={{ p: 2, mb: 3 }}>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{
            alignItems: "center",
            justifyContent: "space-between"
          }}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={2}
            sx={{
              alignItems: "center",
              width: "100%",
              maxWidth: 850
            }}>
            <TextField
              size="small"
              placeholder={t("trainingMatrix.filters.searchPlaceholder")}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              sx={{ minWidth: 320 }}
              slotProps={{
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <SearchIcon fontSize="small" color="action" />
                    </InputAdornment>
                  ),
                  endAdornment: searchQuery ? (
                    <InputAdornment position="end">
                      <IconButton aria-label={t("trainingMatrix.filters.clearSearchAria")} size="small" onClick={() => setSearchQuery("")}>
                        <ClearIcon fontSize="small" />
                      </IconButton>
                    </InputAdornment>
                  ) : null
                }
              }}
            />

            <FormControl size="small" sx={{ minWidth: 180 }}>
              <InputLabel id="dept-filter-label">{t("trainingMatrix.filters.department")}</InputLabel>
              <Select
                labelId="dept-filter-label"
                value={departmentId}
                label={t("trainingMatrix.filters.department")}
                onChange={(e) => setDepartmentId(e.target.value)}
              >
                <MenuItem value="ALL">{t("trainingMatrix.filters.allDepartments")}</MenuItem>
                {departments.map((d) => (
                  <MenuItem key={d.id} value={d.id.toString()}>{d.name}</MenuItem>
                ))}
              </Select>
            </FormControl>

            <FormControl size="small" sx={{ minWidth: 180 }}>
              <InputLabel id="status-filter-label">{t("trainingMatrix.filters.qualificationStatus")}</InputLabel>
              <Select
                labelId="status-filter-label"
                value={complianceStatus}
                label={t("trainingMatrix.filters.qualificationStatus")}
                onChange={(e) => setComplianceStatus(e.target.value)}
                startAdornment={
                  <InputAdornment position="start">
                    <FilterListIcon fontSize="small" color="action" />
                  </InputAdornment>
                }
              >
                <MenuItem value="ALL">{t("trainingMatrix.filters.allStates")}</MenuItem>
                <MenuItem value="Qualified">{t("trainingMatrix.statuses.qualified")}</MenuItem>
                <MenuItem value="Pending">{t("trainingMatrix.statuses.pending")}</MenuItem>
                <MenuItem value="Overdue">{t("trainingMatrix.statuses.overdue")}</MenuItem>
                <MenuItem value="TrainedOnSupersededOnly">{t("trainingMatrix.statuses.trainedOnSupersededOnly")}</MenuItem>
              </Select>
            </FormControl>
          </Stack>

          <Button
            size="small"
            variant="outlined"
            startIcon={<RefreshIcon />}
            onClick={() => loadMatrix()}
            disabled={loading}
          >
            {t("trainingMatrix.filters.refreshMatrix")}
          </Button>
        </Stack>
      </Paper>

      {/* Multi-Axis Matrix Table */}
      <TableContainer component={Paper} variant="outlined" sx={{ maxHeight: 750 }}>
        <Table stickyHeader size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={{ minWidth: 220, bgcolor: (t) => t.palette.mode === "dark" ? "background.paper" : "grey.100", fontWeight: 700, zIndex: 3 }}>
                {t("trainingMatrix.table.personnelRole")}
              </TableCell>
              {gridData?.documents.map((doc) => (
                <TableCell
                  key={doc.documentMasterId}
                  align="center"
                  sx={{
                    minWidth: 140,
                    maxWidth: 180,
                    bgcolor: (t) => t.palette.mode === "dark" ? "background.paper" : "grey.100",
                    fontWeight: 700,
                    cursor: "pointer",
                    "&:hover": { bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.200" }
                  }}
                  onClick={() => handleDocClick(doc)}
                >
                  <Tooltip title={t("trainingMatrix.table.docHeaderTooltip", { code: doc.companyDocumentCode, title: doc.title, rev: doc.currentEffectiveRevisionNumber || t("common.none") })}>
                    <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center" }}>
                      <Typography
                        variant="caption"
                        noWrap
                        sx={{
                          fontWeight: "bold",
                          color: "primary.main",
                          maxWidth: 160
                        }}>
                        {doc.companyDocumentCode}
                      </Typography>
                      <Chip
                        label={t("common.rev", { number: doc.currentEffectiveRevisionNumber || t("common.na") })}
                        size="small"
                        sx={{ ...compactChipSx, mt: 0.2 }}
                      />
                    </Box>
                  </Tooltip>
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={(gridData?.documents.length || 0) + 1} align="center" sx={{ py: 8 }}>
                  <CircularProgress size={36} sx={{ mb: 1 }} />
                  <Typography variant="body2" sx={{
                    color: "text.secondary"
                  }}>
                    {t("trainingMatrix.table.loading")}
                  </Typography>
                </TableCell>
              </TableRow>
            ) : filteredUsers.length === 0 ? (
              <TableRow>
                <TableCell colSpan={(gridData?.documents.length || 0) + 1} align="center" sx={{ py: 6 }}>
                  <Typography variant="body2" sx={{
                    color: "text.secondary"
                  }}>
                    {t("trainingMatrix.table.empty")}
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              filteredUsers.map((user) => (
                <TableRow key={user.userId} hover>
                  <TableCell
                    sx={{
                      position: "sticky",
                      left: 0,
                      bgcolor: "background.paper",
                      zIndex: 1,
                      cursor: "pointer",
                      "&:hover": { bgcolor: "action.hover" }
                    }}
                    onClick={() => handleUserClick(user)}
                  >
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                      <PersonIcon fontSize="small" color="action" />
                      <Box>
                        <Typography variant="body2" sx={{
                          fontWeight: "bold"
                        }}>
                          {user.fullName}
                        </Typography>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>
                          {user.roleName}
                        </Typography>
                      </Box>
                    </Box>
                  </TableCell>

                  {gridData?.documents.map((doc) => {
                    const cell = cellMap.get(`${user.userId}_${doc.documentMasterId}`);
                    return (
                      <TableCell
                        key={doc.documentMasterId}
                        align="center"
                        sx={{
                          p: 1,
                          borderLeft: "1px solid",
                          borderColor: "divider",
                          bgcolor:
                            cell?.cellStatus === "Qualified"
                              ? "rgba(46, 125, 50, 0.03)"
                              : cell?.cellStatus === "Overdue"
                              ? "rgba(211, 47, 47, 0.05)"
                              : cell?.cellStatus === "TrainedOnSupersededOnly"
                              ? "rgba(237, 108, 2, 0.05)"
                              : "inherit"
                        }}
                      >
                        {renderCellBadge(cell)}
                      </TableCell>
                    );
                  })}
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Drilldown Drawer for User Compliance */}
      <Drawer
        anchor="right"
        open={Boolean(selectedUser)}
        onClose={() => setSelectedUser(null)}
        slotProps={{
          paper: { sx: { width: { xs: "100%", sm: 500 }, p: 3 } }
        }}
      >
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
          <Box>
            <Typography variant="h6" sx={{
              fontWeight: "bold"
            }}>
              {t("trainingMatrix.userDrawer.title")}
            </Typography>
            <Typography variant="body2" sx={{
              color: "text.secondary"
            }}>
              {selectedUser?.fullName} ({selectedUser?.roleName})
            </Typography>
          </Box>
          <IconButton aria-label={t("trainingMatrix.userDrawer.closeAria")} size="small" onClick={() => setSelectedUser(null)}>
            <CloseIcon />
          </IconButton>
        </Box>
        <Divider sx={{ mb: 2 }} />

        {drawerLoading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}>
            <CircularProgress size={32} />
          </Box>
        ) : userCompliance && (
          <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
            <Paper variant="outlined" sx={{ p: 2, bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50" }}>
              <Typography
                variant="caption"
                sx={{
                  color: "text.secondary",
                  fontWeight: "bold"
                }}>
                {t("trainingMatrix.userDrawer.complianceScore")}
              </Typography>
              <Typography variant="h4" color={userCompliance.complianceRatePercentage >= 90 ? "success.main" : "warning.main"} sx={{
                fontWeight: "bold"
              }}>
                {userCompliance.complianceRatePercentage}%
              </Typography>
              <Typography variant="caption" sx={{
                color: "text.secondary"
              }}>
                {t("trainingMatrix.userDrawer.documentsQualified", { completed: userCompliance.completedDocuments, total: userCompliance.totalRequiredDocuments })}
              </Typography>
            </Paper>

            <Typography variant="subtitle2" sx={{
              fontWeight: "bold"
            }}>
              {t("trainingMatrix.userDrawer.breakdownTitle")}
            </Typography>
            <Stack spacing={1}>
              {userCompliance.documentStatuses.map((ds) => (
                <Paper key={ds.documentMasterId} variant="outlined" sx={{ p: 1.5 }}>
                  <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                    <Box>
                      <Typography
                        variant="body2"
                        sx={{
                          fontWeight: "bold",
                          color: "primary.main"
                        }}>
                        {ds.companyDocumentCode}
                      </Typography>
                      <Typography
                        variant="caption"
                        sx={{
                          color: "text.secondary",
                          display: "block"
                        }}>
                        {ds.documentTitle}
                      </Typography>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>
                        {t("trainingMatrix.userDrawer.currentRev", { rev: ds.currentEffectiveRevisionNumber || t("common.na") })}
                        {ds.assignedRevisionNumber ? t("trainingMatrix.userDrawer.trainedRev", { rev: ds.assignedRevisionNumber }) : ""}
                      </Typography>
                    </Box>
                    {renderCellBadge(ds)}
                  </Box>
                </Paper>
              ))}
            </Stack>
          </Box>
        )}
      </Drawer>

      {/* Drilldown Drawer for Document Compliance */}
      <Drawer
        anchor="right"
        open={Boolean(selectedDoc)}
        onClose={() => setSelectedDoc(null)}
        slotProps={{
          paper: { sx: { width: { xs: "100%", sm: 500 }, p: 3 } }
        }}
      >
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
          <Box>
            <Typography variant="h6" sx={{
              fontWeight: "bold"
            }}>
              {t("trainingMatrix.docDrawer.title")}
            </Typography>
            <Typography variant="body2" sx={{
              color: "text.secondary"
            }}>
              {selectedDoc?.companyDocumentCode}: {selectedDoc?.title}
            </Typography>
          </Box>
          <IconButton aria-label={t("trainingMatrix.docDrawer.closeAria")} size="small" onClick={() => setSelectedDoc(null)}>
            <CloseIcon />
          </IconButton>
        </Box>
        <Divider sx={{ mb: 2 }} />

        {drawerLoading ? (
          <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}>
            <CircularProgress size={32} />
          </Box>
        ) : docCompliance && (
          <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
            <Grid container spacing={1.5}>
              <Grid size={6}>
                <Paper variant="outlined" sx={{ p: 1.5, textAlign: "center", bgcolor: (t) => t.palette.mode === "dark" ? "rgba(46, 125, 50, 0.15)" : "success.50" }}>
                  <Typography
                    variant="caption"
                    sx={{
                      color: "success.main",
                      fontWeight: "bold"
                    }}>{t("trainingMatrix.docDrawer.qualified")}</Typography>
                  <Typography
                    variant="h5"
                    sx={{
                      fontWeight: "bold",
                      color: "success.main"
                    }}>{docCompliance.completedUserCount}</Typography>
                </Paper>
              </Grid>
              <Grid size={6}>
                <Paper variant="outlined" sx={{ p: 1.5, textAlign: "center", bgcolor: (t) => t.palette.mode === "dark" ? "rgba(211, 47, 47, 0.15)" : "error.50" }}>
                  <Typography
                    variant="caption"
                    sx={{
                      color: "error.main",
                      fontWeight: "bold"
                    }}>{t("trainingMatrix.docDrawer.overdue")}</Typography>
                  <Typography
                    variant="h5"
                    sx={{
                      fontWeight: "bold",
                      color: "error.main"
                    }}>{docCompliance.overdueUserCount}</Typography>
                </Paper>
              </Grid>
            </Grid>

            <Typography variant="subtitle2" sx={{
              fontWeight: "bold"
            }}>
              {t("trainingMatrix.docDrawer.personnelStatuses")}
            </Typography>
            <Stack spacing={1}>
              {docCompliance.userStatuses.map((us) => (
                <Paper key={us.userId} variant="outlined" sx={{ p: 1.5 }}>
                  <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                    <Box>
                      <Typography variant="body2" sx={{
                        fontWeight: "bold"
                      }}>
                        {us.userName}
                      </Typography>
                      {us.dueDateUtc && (
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>
                          {t("trainingMatrix.docDrawer.due", { date: formatLabCalendarDate(us.dueDateUtc) })}
                        </Typography>
                      )}
                    </Box>
                    {renderCellBadge(us)}
                  </Box>
                </Paper>
              ))}
            </Stack>
          </Box>
        )}
      </Drawer>
    </Box>
  );
}
