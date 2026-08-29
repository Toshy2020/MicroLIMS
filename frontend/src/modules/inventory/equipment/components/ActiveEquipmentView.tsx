import { useEffect, useState } from "react";
import {
  Box, Paper, Stack, Typography, TextField, Button, Table, TableHead,
  TableRow, TableCell, TableBody, TableContainer, TablePagination, Chip, Alert, IconButton,
  Tooltip, Grid, CircularProgress, Divider, useTheme
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import VisibilityIcon from "@mui/icons-material/Visibility";
import PlaceIcon from "@mui/icons-material/Place";
import HistoryIcon from "@mui/icons-material/History";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import { useNavigate, Link } from "react-router-dom";

import {
  EquipmentInventoryService, ActiveEquipmentDto, EquipmentActivityDto, WhereIsItResultDto
} from "../services/EquipmentInventoryService";
import { formatLabDate, formatLabDateTime } from "../../../../utils/formatDate";
import { brandColors } from "../../../../theme";

interface ActiveEquipmentViewProps {
  onOpenDetails: (equipmentId: number) => void;
}

export function ActiveEquipmentView({ onOpenDetails }: ActiveEquipmentViewProps) {
  const navigate = useNavigate();
  const theme = useTheme();
  const headerBg = theme.palette.mode === "dark" ? "grey.800" : "grey.100";

  // Active Equipment state
  const [activeEquipment, setActiveEquipment] = useState<ActiveEquipmentDto[]>([]);
  const [selectedEqId, setSelectedEqId] = useState<number | null>(null);
  const [loadingActive, setLoadingActive] = useState(true);

  // Selected Equipment Activities state
  const [activities, setActivities] = useState<EquipmentActivityDto[]>([]);
  const [loadingActivities, setLoadingActivities] = useState(false);
  const [activitiesPage, setActivitiesPage] = useState(0);
  const [activitiesRowsPerPage, setActivitiesRowsPerPage] = useState(15);

  // Activity History state
  const [historyItemCode, setHistoryItemCode] = useState("");
  const [historyFromDate, setHistoryFromDate] = useState("");
  const [historyToDate, setHistoryToDate] = useState("");
  const [historyResults, setHistoryResults] = useState<EquipmentActivityDto[] | null>(null);
  const [loadingHistory, setLoadingHistory] = useState(false);
  const [historyPage, setHistoryPage] = useState(0);
  const [historyRowsPerPage, setHistoryRowsPerPage] = useState(15);

  // "Where is it?" search state
  const [whereQuery, setWhereQuery] = useState("");
  const [whereResult, setWhereResult] = useState<WhereIsItResultDto | null>(null);
  const [loadingWhere, setLoadingWhere] = useState(false);
  const [wherePage, setWherePage] = useState(0);
  const [whereRowsPerPage, setWhereRowsPerPage] = useState(15);

  // Load Active Equipment List on mount
  const loadActiveEquipment = async () => {
    try {
      setLoadingActive(true);
      const list = await EquipmentInventoryService.getActiveEquipment();
      setActiveEquipment(list);
      if (list.length > 0 && !selectedEqId) {
        setSelectedEqId(list[0].id);
      }
    } catch {
      // Error handled
    } finally {
      setLoadingActive(false);
    }
  };

  useEffect(() => {
    loadActiveEquipment();
  }, []);

  // Load active activities when selected equipment changes
  useEffect(() => {
    if (selectedEqId) {
      setActivitiesPage(0);
      setHistoryPage(0);
      loadActivitiesForEquipment(selectedEqId);
      loadHistoryForEquipment(selectedEqId);
    } else {
      setActivities([]);
      setHistoryResults(null);
    }
  }, [selectedEqId]);

  const loadActivitiesForEquipment = async (eqId: number) => {
    try {
      setLoadingActivities(true);
      const acts = await EquipmentInventoryService.getActiveActivities(eqId);
      setActivities(acts);
    } catch {
      setActivities([]);
    } finally {
      setLoadingActivities(false);
    }
  };

  const loadHistoryForEquipment = async (eqId: number) => {
    try {
      setLoadingHistory(true);
      const history = await EquipmentInventoryService.getHistory(eqId, {
        itemCode: historyItemCode || undefined,
        fromDate: historyFromDate || undefined,
        toDate: historyToDate || undefined,
      });
      setHistoryResults(history);
    } catch {
      setHistoryResults([]);
    } finally {
      setLoadingHistory(false);
    }
  };

  const handleHistorySearch = (e: React.FormEvent) => {
    e.preventDefault();
    if (selectedEqId) {
      setHistoryPage(0);
      loadHistoryForEquipment(selectedEqId);
    }
  };

  const handleWhereIsItSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!whereQuery.trim()) return;
    try {
      setLoadingWhere(true);
      setWherePage(0);
      const res = await EquipmentInventoryService.whereIsIt(whereQuery);
      setWhereResult(res);
    } catch {
      setWhereResult(null);
    } finally {
      setLoadingWhere(false);
    }
  };

  const selectedEquipment = activeEquipment.find((e) => e.id === selectedEqId);

  return (
    <Stack spacing={3}>
      {/* 1. Global "Where is it?" Traceability Search Bar */}
      <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
        <Stack spacing={2}>
          <Stack direction="row" alignItems="center" spacing={1}>
            <PlaceIcon color="primary" />
            <Typography variant="subtitle1" fontWeight={700}>
              Where is it? — Global Traceability Search
            </Typography>
          </Stack>
          <Box component="form" onSubmit={handleWhereIsItSearch} sx={{ display: "flex", gap: 1.5 }}>
            <TextField
              size="small"
              fullWidth
              placeholder="Search by item code, sample reference, or media lot (e.g. TSB/08/26, PT-0021)..."
              value={whereQuery}
              onChange={(e) => setWhereQuery(e.target.value)}
            />
            <Button
              type="submit"
              variant="contained"
              color="primary"
              startIcon={<SearchIcon />}
              disabled={loadingWhere}
              sx={{ whiteSpace: "nowrap", px: 3 }}
            >
              {loadingWhere ? "Searching..." : "Where is it?"}
            </Button>
          </Box>

          {/* Where is it? Search Results Display */}
          {whereResult && (
            <Paper variant="outlined" sx={{ p: 2, bgcolor: "background.paper", mt: 1 }}>
              <Typography variant="subtitle2" fontWeight={700} sx={{ mb: 1.5 }}>
                Traceability Results for "{whereResult.searchTerm}":
              </Typography>

              {whereResult.currentActivity ? (
                <Box sx={{ mb: 2, p: 2, bgcolor: theme.custom.status.notDetected.bg, border: "1px solid", borderColor: theme.custom.status.notDetected.border, borderRadius: 1.5 }}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Box>
                      <Typography variant="body2" fontWeight={700} color="success.dark">
                        CURRENT LOCATION: {whereResult.currentEquipmentCode} — {whereResult.currentEquipmentName}
                      </Typography>
                      <Typography variant="body2">
                        Activity: <strong>{whereResult.currentActivity.activityType}</strong> | Item: <strong>{whereResult.currentActivity.itemName} (@{whereResult.currentActivity.itemCode})</strong>
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        Started: {formatLabDateTime(whereResult.currentActivity.startedOn)} | Analyst: {whereResult.currentActivity.startedBy}
                      </Typography>
                    </Box>
                    <Chip label="Active / Current Location" color="success" size="small" />
                  </Stack>
                </Box>
              ) : (
                <Alert severity="info" sx={{ mb: 2, fontSize: 13 }}>
                  Item is not currently in an active equipment location. Viewing location history below:
                </Alert>
              )}

              {whereResult.history.length > 0 ? (
                <>
                  <TableContainer>
                    <Table size="small">
                      <TableHead sx={{ bgcolor: headerBg }}>
                        <TableRow>
                          <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Equipment</TableCell>
                          <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Activity</TableCell>
                          <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Started On</TableCell>
                          <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Completed On</TableCell>
                          <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Performed By</TableCell>
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {whereResult.history
                          .slice(wherePage * whereRowsPerPage, wherePage * whereRowsPerPage + whereRowsPerPage)
                          .map((h, idx) => (
                            <TableRow key={idx}>
                              <TableCell sx={{ fontSize: 12, fontWeight: 600, fontFamily: "monospace" }}>{h.equipmentCode} ({h.equipmentName})</TableCell>
                              <TableCell sx={{ fontSize: 12 }}>{h.activityType}</TableCell>
                              <TableCell sx={{ fontSize: 12 }}>{formatLabDateTime(h.startedOn)}</TableCell>
                              <TableCell sx={{ fontSize: 12 }}>{h.completedOn ? formatLabDateTime(h.completedOn) : "Active"}</TableCell>
                              <TableCell sx={{ fontSize: 12 }}>{h.performedBy}</TableCell>
                            </TableRow>
                          ))}
                      </TableBody>
                    </Table>
                  </TableContainer>
                  <TablePagination
                    component="div"
                    count={whereResult.history.length}
                    page={wherePage}
                    onPageChange={(_, newPage) => setWherePage(newPage)}
                    rowsPerPage={whereRowsPerPage}
                    onRowsPerPageChange={(e) => {
                      setWhereRowsPerPage(parseInt(e.target.value, 10));
                      setWherePage(0);
                    }}
                    rowsPerPageOptions={[15, 30, 50]}
                    sx={{ borderTop: "1px solid", borderColor: "divider" }}
                  />
                </>
              ) : (
                <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 1 }}>
                  No location history records found matching this query.
                </Typography>
              )}
            </Paper>
          )}
        </Stack>
      </Paper>

      {/* 2. Main Active Equipment Split Layout */}
      <Grid container spacing={3}>
        {/* LEFT PANEL: Active Equipment List */}
        <Grid item xs={12} md={4}>
          <Paper sx={{ p: 2, borderRadius: 2, border: "1px solid", borderColor: "divider", minHeight: 500 }}>
            <Typography variant="subtitle1" fontWeight={700} sx={{ mb: 2 }}>
              Active Equipment ({activeEquipment.length})
            </Typography>

            {loadingActive ? (
              <Box textAlign="center" py={4}><CircularProgress size={32} /></Box>
            ) : activeEquipment.length === 0 ? (
              <Alert severity="info" sx={{ fontSize: 13 }}>
                No equipment is currently in active use by laboratory activities.
              </Alert>
            ) : (
              <Stack spacing={1.5}>
                {activeEquipment.map((eq) => {
                  const isSelected = eq.id === selectedEqId;
                  return (
                    <Paper
                      key={eq.id}
                      elevation={isSelected ? 2 : 0}
                      onClick={() => setSelectedEqId(eq.id)}
                      sx={{
                        p: 2,
                        cursor: "pointer",
                        border: "1px solid",
                        borderColor: isSelected ? "primary.main" : "divider",
                        bgcolor: isSelected ? theme.custom.status.purple.bg : "background.paper",
                        transition: "all 0.15s ease-in-out",
                        "&:hover": { borderColor: "primary.main", bgcolor: isSelected ? theme.custom.status.purple.bg : "action.hover" }
                      }}
                    >
                      <Stack direction="row" justifyContent="space-between" alignItems="flex-start" sx={{ mb: 1 }}>
                        <Typography sx={{ fontFamily: "monospace", fontWeight: 700, fontSize: 14, color: "primary.main" }}>
                          {eq.code}
                        </Typography>
                        <Chip label={`${eq.activeItemCount} ${eq.activeItemCount === 1 ? "item" : "items"}`} size="small" color="primary" sx={{ height: 20, fontSize: 11 }} />
                      </Stack>
                      <Typography sx={{ fontWeight: 600, fontSize: 13 }}>{eq.instrumentType}</Typography>
                      <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.5 }}>{eq.location}</Typography>
                      <Chip label={eq.primaryActivityCategory} size="small" variant="outlined" sx={{ mt: 1, height: 20, fontSize: 10 }} />
                    </Paper>
                  );
                })}
              </Stack>
            )}
          </Paper>
        </Grid>

        {/* RIGHT MAIN PANEL: Selected Equipment Details & Activities */}
        <Grid item xs={12} md={8}>
          {selectedEquipment ? (
            <Stack spacing={3}>
              {/* Equipment Info Header Card */}
              <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
                <Stack direction="row" justifyContent="space-between" alignItems="flex-start" sx={{ mb: 2 }}>
                  <Box>
                    <Typography variant="h6" fontWeight={700}>
                      {selectedEquipment.code} — {selectedEquipment.instrumentType}
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                      Location: {selectedEquipment.location}
                    </Typography>
                  </Box>
                  <Stack direction="row" spacing={1} alignItems="center">
                    <Chip label="In Use" color="success" size="small" />
                    <Button size="small" variant="outlined" startIcon={<InfoOutlinedIcon />} onClick={() => onOpenDetails(selectedEquipment.id)}>
                      Equipment Details
                    </Button>
                  </Stack>
                </Stack>

                <Grid container spacing={2} sx={{ pt: 1, borderTop: "1px solid", borderTopColor: "divider" }}>
                  <Grid item xs={6} sm={3}>
                    <Typography variant="caption" color="text.secondary" display="block">Manufacturer</Typography>
                    <Typography variant="body2" fontWeight={600}>{selectedEquipment.manufacturerName || "—"}</Typography>
                  </Grid>
                  <Grid item xs={6} sm={3}>
                    <Typography variant="caption" color="text.secondary" display="block">Set Temperature</Typography>
                    <Typography variant="body2" fontWeight={600}>
                      {selectedEquipment.setPointTemperature ? `${selectedEquipment.setPointTemperature} °C` : "N/A"}
                    </Typography>
                  </Grid>
                  <Grid item xs={6} sm={3}>
                    <Typography variant="caption" color="text.secondary" display="block">Calibration Due</Typography>
                    <Typography variant="body2" fontWeight={600}>
                      {selectedEquipment.calibrationDueDate ? formatLabDate(selectedEquipment.calibrationDueDate) : "—"}
                    </Typography>
                  </Grid>
                  <Grid item xs={6} sm={3}>
                    <Typography variant="caption" color="text.secondary" display="block">Current Active Items</Typography>
                    <Typography variant="body2" fontWeight={700} color="primary.main">{selectedEquipment.activeItemCount}</Typography>
                  </Grid>
                </Grid>
              </Paper>

              {/* Current Activities / Items Table */}
              <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
                <Typography variant="subtitle1" fontWeight={700} sx={{ mb: 1.5 }}>
                  Current Activities / Items ({activities.length})
                </Typography>

                {loadingActivities ? (
                  <Box textAlign="center" py={3}><CircularProgress size={28} /></Box>
                ) : activities.length === 0 ? (
                  <Alert severity="info" sx={{ fontSize: 13 }}>No active activities currently running in this equipment.</Alert>
                ) : (
                  <>
                    <TableContainer>
                      <Table size="small">
                        <TableHead sx={{ bgcolor: headerBg }}>
                          <TableRow>
                            <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Item / Activity</TableCell>
                            <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Item Code</TableCell>
                            <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Activity Type</TableCell>
                            <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Media / Description</TableCell>
                            <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Started On</TableCell>
                            <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Started By</TableCell>
                            <TableCell sx={{ fontWeight: 700, fontSize: 11 }}>Expected Completion</TableCell>
                            <TableCell align="right" sx={{ fontWeight: 700, fontSize: 11 }}>Actions</TableCell>
                          </TableRow>
                        </TableHead>
                        <TableBody>
                          {activities
                            .slice(activitiesPage * activitiesRowsPerPage, activitiesPage * activitiesRowsPerPage + activitiesRowsPerPage)
                            .map((act) => (
                              <TableRow key={act.activityId} hover>
                                <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>{act.itemName}</TableCell>
                                <TableCell sx={{ fontSize: 12, fontFamily: "monospace", fontWeight: 700 }}>{act.itemCode}</TableCell>
                                <TableCell sx={{ fontSize: 12 }}>
                                  <Chip label={act.activityType} size="small" variant="outlined" color="primary" sx={{ height: 20, fontSize: 10 }} />
                                </TableCell>
                                <TableCell sx={{ fontSize: 12 }}>{act.mediaDescription}</TableCell>
                                <TableCell sx={{ fontSize: 12 }}>{formatLabDateTime(act.startedOn)}</TableCell>
                                <TableCell sx={{ fontSize: 12 }}>{act.startedBy}</TableCell>
                                <TableCell sx={{ fontSize: 12 }}>
                                  {act.expectedCompletion ? formatLabDateTime(act.expectedCompletion) : "N/A"}
                                </TableCell>
                                <TableCell align="right">
                                  {(() => {
                                    const targetRoute =
                                      act.entityType === "Sample"
                                        ? "/testing-workspace"
                                        : act.entityType === "Media"
                                        ? "/laboratory-configuration/media"
                                        : act.entityType === "Cryovial"
                                        ? "/laboratory-configuration/cryovials"
                                        : null;

                                    return (
                                      <Tooltip title="View Activity / Test Workspace">
                                        <IconButton
                                          {...(targetRoute ? { component: Link, to: targetRoute } : {})}
                                          size="small"
                                          color="primary"
                                        >
                                          <VisibilityIcon fontSize="small" />
                                        </IconButton>
                                      </Tooltip>
                                    );
                                  })()}
                                </TableCell>
                              </TableRow>
                            ))}
                        </TableBody>
                      </Table>
                    </TableContainer>
                    <TablePagination
                      component="div"
                      count={activities.length}
                      page={activitiesPage}
                      onPageChange={(_, newPage) => setActivitiesPage(newPage)}
                      rowsPerPage={activitiesRowsPerPage}
                      onRowsPerPageChange={(e) => {
                        setActivitiesRowsPerPage(parseInt(e.target.value, 10));
                        setActivitiesPage(0);
                      }}
                      rowsPerPageOptions={[15, 30, 50]}
                      sx={{ borderTop: "1px solid", borderColor: "divider" }}
                    />
                  </>
                )}
              </Paper>

              {/* Date-to-Date Activity History Search */}
              <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
                <Stack spacing={2}>
                  <Stack direction="row" alignItems="center" spacing={1}>
                    <HistoryIcon color="action" />
                    <Typography variant="subtitle1" fontWeight={700}>
                      Search Activity History — {selectedEquipment.code}
                    </Typography>
                  </Stack>

                  <Box component="form" onSubmit={handleHistorySearch}>
                    <Grid container spacing={2} alignItems="center">
                      <Grid item xs={12} sm={4}>
                        <TextField
                          size="small"
                          label="Item / Code Filter"
                          placeholder="e.g. PT-0021"
                          value={historyItemCode}
                          onChange={(e) => setHistoryItemCode(e.target.value)}
                          fullWidth
                        />
                      </Grid>
                      <Grid item xs={6} sm={3}>
                        <TextField
                          size="small"
                          label="From Date"
                          type="date"
                          InputLabelProps={{ shrink: true }}
                          value={historyFromDate}
                          onChange={(e) => setHistoryFromDate(e.target.value)}
                          fullWidth
                        />
                      </Grid>
                      <Grid item xs={6} sm={3}>
                        <TextField
                          size="small"
                          label="To Date"
                          type="date"
                          InputLabelProps={{ shrink: true }}
                          value={historyToDate}
                          onChange={(e) => setHistoryToDate(e.target.value)}
                          fullWidth
                        />
                      </Grid>
                      <Grid item xs={12} sm={2}>
                        <Button type="submit" variant="outlined" color="primary" fullWidth disabled={loadingHistory}>
                          {loadingHistory ? "Searching..." : "Search"}
                        </Button>
                      </Grid>
                    </Grid>
                  </Box>

                  {historyResults && (
                    <Box sx={{ mt: 1 }}>
                      <TableContainer>
                        <Table size="small">
                          <TableHead sx={{ bgcolor: headerBg }}>
                            <TableRow>
                              <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Item / Activity</TableCell>
                              <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Item Code</TableCell>
                              <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Activity Type</TableCell>
                              <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Media / Description</TableCell>
                              <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Started On</TableCell>
                              <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Completed On</TableCell>
                              <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Analyst</TableCell>
                            </TableRow>
                          </TableHead>
                          <TableBody>
                            {historyResults.length === 0 ? (
                              <TableRow>
                                <TableCell colSpan={7} align="center" sx={{ py: 3 }}>
                                  <Typography color="text.secondary" sx={{ fontSize: 13 }}>
                                    No historical activities found matching the selected search criteria.
                                  </Typography>
                                </TableCell>
                              </TableRow>
                            ) : (
                              historyResults
                                .slice(historyPage * historyRowsPerPage, historyPage * historyRowsPerPage + historyRowsPerPage)
                                .map((h) => (
                                  <TableRow key={h.activityId} hover>
                                    <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>{h.itemName}</TableCell>
                                    <TableCell sx={{ fontSize: 12, fontFamily: "monospace", fontWeight: 700 }}>{h.itemCode}</TableCell>
                                    <TableCell sx={{ fontSize: 12 }}>{h.activityType}</TableCell>
                                    <TableCell sx={{ fontSize: 12 }}>{h.mediaDescription}</TableCell>
                                    <TableCell sx={{ fontSize: 12 }}>{formatLabDateTime(h.startedOn)}</TableCell>
                                    <TableCell sx={{ fontSize: 12 }}>
                                      {h.completedOn ? formatLabDateTime(h.completedOn) : <Chip label="Active" size="small" color="success" sx={{ height: 18, fontSize: 10 }} />}
                                    </TableCell>
                                    <TableCell sx={{ fontSize: 12 }}>{h.startedBy}</TableCell>
                                  </TableRow>
                                ))
                            )}
                          </TableBody>
                        </Table>
                      </TableContainer>
                      {historyResults.length > 0 && (
                        <TablePagination
                          component="div"
                          count={historyResults.length}
                          page={historyPage}
                          onPageChange={(_, newPage) => setHistoryPage(newPage)}
                          rowsPerPage={historyRowsPerPage}
                          onRowsPerPageChange={(e) => {
                            setHistoryRowsPerPage(parseInt(e.target.value, 10));
                            setHistoryPage(0);
                          }}
                          rowsPerPageOptions={[15, 30, 50]}
                          sx={{ borderTop: "1px solid", borderColor: "divider" }}
                        />
                      )}
                    </Box>
                  )}
                </Stack>
              </Paper>
            </Stack>
          ) : (
            <Paper sx={{ p: 4, textAlign: "center", borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
              <Typography variant="body1" color="text.secondary">
                Select an active equipment record from the left panel to inspect its current activities and traceability history.
              </Typography>
            </Paper>
          )}
        </Grid>
      </Grid>
    </Stack>
  );
}
