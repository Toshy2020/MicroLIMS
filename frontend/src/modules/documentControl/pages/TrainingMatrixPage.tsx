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
import DescriptionIcon from "@mui/icons-material/Description";
import AssessmentOutlinedIcon from "@mui/icons-material/AssessmentOutlined";
import { useNavigate } from "react-router-dom";

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

export function TrainingMatrixPage() {
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
    } catch (err: any) {
      setError(err?.response?.data?.message || err?.message || "Failed to load Training Matrix.");
    } finally {
      setLoading(false);
    }
  }, [departmentId, complianceStatus]);

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
        <Tooltip title="Not Assigned / Not in Curriculum">
          <Box sx={{ display: "flex", justifyContent: "center", color: "text.disabled" }}>
            <RemoveIcon fontSize="small" />
          </Box>
        </Tooltip>
      );
    }

    switch (cell.cellStatus) {
      case "Qualified":
        return (
          <Tooltip title={`Qualified (Rev ${cell.assignedRevisionNumber || cell.currentEffectiveRevisionNumber})`}>
            <Chip
              icon={<CheckCircleIcon sx={{ fontSize: "14px !important" }} />}
              label="Qualified"
              size="small"
              color="success"
              sx={{ height: 20, fontSize: 10, fontWeight: 700 }}
              onClick={() => setSelectedCell(cell)}
            />
          </Tooltip>
        );
      case "Pending":
        return (
          <Tooltip title={`Pending Reading (Due ${cell.dueDateUtc ? new Date(cell.dueDateUtc).toLocaleDateString() : "TBD"})`}>
            <Chip
              icon={<ScheduleIcon sx={{ fontSize: "14px !important" }} />}
              label="Pending"
              size="small"
              color="warning"
              sx={{ height: 20, fontSize: 10, fontWeight: 700 }}
              onClick={() => setSelectedCell(cell)}
            />
          </Tooltip>
        );
      case "Overdue":
        return (
          <Tooltip title={`OVERDUE by ${cell.daysRemainingOrOverdue ? Math.abs(cell.daysRemainingOrOverdue) : ""}d`}>
            <Chip
              icon={<ErrorIcon sx={{ fontSize: "14px !important" }} />}
              label="Overdue"
              size="small"
              color="error"
              sx={{ height: 20, fontSize: 10, fontWeight: 700 }}
              onClick={() => setSelectedCell(cell)}
            />
          </Tooltip>
        );
      case "TrainedOnSupersededOnly":
        return (
          <Tooltip title="Qualified on Prior Revision Only — Retraining Gap">
            <Chip
              icon={<WarningIcon sx={{ fontSize: "14px !important" }} />}
              label="Superseded Gap"
              size="small"
              sx={{ height: 20, fontSize: 10, fontWeight: 700, bgcolor: "#ff9800", color: "#fff" }}
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
            sx={{ height: 20, fontSize: 10 }}
            onClick={() => setSelectedCell(cell)}
          />
        );
    }
  };

  return (
    <Box sx={{ p: 3, maxWidth: 1600, margin: "0 auto" }}>
      <PageHeader
        title="Training Matrix"
        subtitle="Multi-axis qualification matrix mapping personnel to controlled document training requirements."
      >
        <Button
          variant="outlined"
          startIcon={<AssessmentOutlinedIcon />}
          onClick={() => navigate("/document-control/compliance-dashboard")}
        >
          Compliance Dashboard
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
          <Grid item xs={12} sm={6} md={2.4}>
            <Card variant="outlined">
              <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
                <Typography variant="caption" color="text.secondary" fontWeight="bold">
                  PERSONNEL TRACKED
                </Typography>
                <Typography variant="h5" fontWeight="bold">
                  {gridData.totalUsers}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} sm={6} md={2.4}>
            <Card variant="outlined" sx={{ bgcolor: "success.50" }}>
              <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
                <Typography variant="caption" color="success.main" fontWeight="bold">
                  QUALIFIED CELLS
                </Typography>
                <Typography variant="h5" fontWeight="bold" color="success.main">
                  {gridData.totalCompliantCells}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} sm={6} md={2.4}>
            <Card variant="outlined" sx={{ bgcolor: "warning.50" }}>
              <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
                <Typography variant="caption" color="warning.main" fontWeight="bold">
                  PENDING CELLS
                </Typography>
                <Typography variant="h5" fontWeight="bold" color="warning.main">
                  {gridData.totalPendingCells}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} sm={6} md={2.4}>
            <Card variant="outlined" sx={{ bgcolor: "error.50" }}>
              <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
                <Typography variant="caption" color="error.main" fontWeight="bold">
                  OVERDUE CELLS
                </Typography>
                <Typography variant="h5" fontWeight="bold" color="error.main">
                  {gridData.totalOverdueCells}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} sm={6} md={2.4}>
            <Card variant="outlined" sx={{ bgcolor: "#fff3e0" }}>
              <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
                <Typography variant="caption" sx={{ color: "#e65100", fontWeight: "bold" }}>
                  SUPERSEDED GAPS
                </Typography>
                <Typography variant="h5" fontWeight="bold" sx={{ color: "#e65100" }}>
                  {gridData.totalSupersededGapCells}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        </Grid>
      )}

      {/* Filter Toolbar */}
      <Paper variant="outlined" sx={{ p: 2, mb: 3 }}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} alignItems="center" justifyContent="space-between">
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2} alignItems="center" sx={{ width: "100%", maxWidth: 850 }}>
            <TextField
              size="small"
              placeholder="Search personnel name, username, or role..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              sx={{ minWidth: 320 }}
              InputProps={{
                startAdornment: (
                  <InputAdornment position="start">
                    <SearchIcon fontSize="small" color="action" />
                  </InputAdornment>
                ),
                endAdornment: searchQuery ? (
                  <InputAdornment position="end">
                    <IconButton size="small" onClick={() => setSearchQuery("")}>
                      <ClearIcon fontSize="small" />
                    </IconButton>
                  </InputAdornment>
                ) : null
              }}
            />

            <FormControl size="small" sx={{ minWidth: 180 }}>
              <InputLabel id="dept-filter-label">Department</InputLabel>
              <Select
                labelId="dept-filter-label"
                value={departmentId}
                label="Department"
                onChange={(e) => setDepartmentId(e.target.value)}
              >
                <MenuItem value="ALL">All Departments</MenuItem>
                {departments.map((d) => (
                  <MenuItem key={d.id} value={d.id.toString()}>{d.name}</MenuItem>
                ))}
              </Select>
            </FormControl>

            <FormControl size="small" sx={{ minWidth: 180 }}>
              <InputLabel id="status-filter-label">Qualification Status</InputLabel>
              <Select
                labelId="status-filter-label"
                value={complianceStatus}
                label="Qualification Status"
                onChange={(e) => setComplianceStatus(e.target.value)}
                startAdornment={
                  <InputAdornment position="start">
                    <FilterListIcon fontSize="small" color="action" />
                  </InputAdornment>
                }
              >
                <MenuItem value="ALL">All States</MenuItem>
                <MenuItem value="Qualified">Qualified</MenuItem>
                <MenuItem value="Pending">Pending</MenuItem>
                <MenuItem value="Overdue">Overdue</MenuItem>
                <MenuItem value="TrainedOnSupersededOnly">Superseded Gap</MenuItem>
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
            Refresh Matrix
          </Button>
        </Stack>
      </Paper>

      {/* Multi-Axis Matrix Table */}
      <TableContainer component={Paper} variant="outlined" sx={{ maxHeight: 750 }}>
        <Table stickyHeader size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={{ minWidth: 220, bgcolor: "grey.100", fontWeight: 700, zIndex: 3 }}>
                Personnel / Role
              </TableCell>
              {gridData?.documents.map((doc) => (
                <TableCell
                  key={doc.documentMasterId}
                  align="center"
                  sx={{
                    minWidth: 140,
                    maxWidth: 180,
                    bgcolor: "grey.100",
                    fontWeight: 700,
                    cursor: "pointer",
                    "&:hover": { bgcolor: "grey.200" }
                  }}
                  onClick={() => handleDocClick(doc)}
                >
                  <Tooltip title={`${doc.companyDocumentCode} — ${doc.title} (Current Rev: ${doc.currentEffectiveRevisionNumber || "None"})`}>
                    <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center" }}>
                      <Typography variant="caption" fontWeight="bold" color="primary.main" noWrap sx={{ maxWidth: 160 }}>
                        {doc.companyDocumentCode}
                      </Typography>
                      <Chip
                        label={`Rev ${doc.currentEffectiveRevisionNumber || "N/A"}`}
                        size="small"
                        sx={{ height: 16, fontSize: 9, mt: 0.2 }}
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
                  <Typography variant="body2" color="text.secondary">
                    Computing real-time training qualification matrix...
                  </Typography>
                </TableCell>
              </TableRow>
            ) : filteredUsers.length === 0 ? (
              <TableRow>
                <TableCell colSpan={(gridData?.documents.length || 0) + 1} align="center" sx={{ py: 6 }}>
                  <Typography variant="body2" color="text.secondary">
                    No personnel records matched your filter criteria.
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
                        <Typography variant="body2" fontWeight="bold">
                          {user.fullName}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
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
        PaperProps={{ sx: { width: { xs: "100%", sm: 500 }, p: 3 } }}
      >
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
          <Box>
            <Typography variant="h6" fontWeight="bold">
              User Compliance Profile
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {selectedUser?.fullName} ({selectedUser?.roleName})
            </Typography>
          </Box>
          <IconButton size="small" onClick={() => setSelectedUser(null)}>
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
            <Paper variant="outlined" sx={{ p: 2, bgcolor: "grey.50" }}>
              <Typography variant="caption" color="text.secondary" fontWeight="bold">
                COMPLIANCE SCORE
              </Typography>
              <Typography variant="h4" fontWeight="bold" color={userCompliance.complianceRatePercentage >= 90 ? "success.main" : "warning.main"}>
                {userCompliance.complianceRatePercentage}%
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {userCompliance.completedDocuments} of {userCompliance.totalRequiredDocuments} documents qualified
              </Typography>
            </Paper>

            <Typography variant="subtitle2" fontWeight="bold">
              Document Training Breakdown
            </Typography>
            <Stack spacing={1}>
              {userCompliance.documentStatuses.map((ds) => (
                <Paper key={ds.documentMasterId} variant="outlined" sx={{ p: 1.5 }}>
                  <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                    <Box>
                      <Typography variant="body2" fontWeight="bold" color="primary.main">
                        {ds.companyDocumentCode}
                      </Typography>
                      <Typography variant="caption" color="text.secondary" display="block">
                        {ds.documentTitle}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        Current Rev: {ds.currentEffectiveRevisionNumber || "N/A"}
                        {ds.assignedRevisionNumber ? ` | Trained: Rev ${ds.assignedRevisionNumber}` : ""}
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
        PaperProps={{ sx: { width: { xs: "100%", sm: 500 }, p: 3 } }}
      >
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
          <Box>
            <Typography variant="h6" fontWeight="bold">
              Document Compliance Profile
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {selectedDoc?.companyDocumentCode} — {selectedDoc?.title}
            </Typography>
          </Box>
          <IconButton size="small" onClick={() => setSelectedDoc(null)}>
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
              <Grid item xs={6}>
                <Paper variant="outlined" sx={{ p: 1.5, textAlign: "center", bgcolor: "success.50" }}>
                  <Typography variant="caption" color="success.main" fontWeight="bold">QUALIFIED</Typography>
                  <Typography variant="h5" fontWeight="bold" color="success.main">{docCompliance.completedUserCount}</Typography>
                </Paper>
              </Grid>
              <Grid item xs={6}>
                <Paper variant="outlined" sx={{ p: 1.5, textAlign: "center", bgcolor: "error.50" }}>
                  <Typography variant="caption" color="error.main" fontWeight="bold">OVERDUE</Typography>
                  <Typography variant="h5" fontWeight="bold" color="error.main">{docCompliance.overdueUserCount}</Typography>
                </Paper>
              </Grid>
            </Grid>

            <Typography variant="subtitle2" fontWeight="bold">
              Personnel Training Statuses
            </Typography>
            <Stack spacing={1}>
              {docCompliance.userStatuses.map((us) => (
                <Paper key={us.userId} variant="outlined" sx={{ p: 1.5 }}>
                  <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                    <Box>
                      <Typography variant="body2" fontWeight="bold">
                        {us.userName}
                      </Typography>
                      {us.dueDateUtc && (
                        <Typography variant="caption" color="text.secondary">
                          Due: {new Date(us.dueDateUtc).toLocaleDateString()}
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
