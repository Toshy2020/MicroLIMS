import { useEffect, useState } from "react";
import {
  Box, Paper, Stack, Typography, TextField, Button, Alert,
  Grid, CircularProgress, useTheme
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import PlaceIcon from "@mui/icons-material/Place";
import HistoryIcon from "@mui/icons-material/History";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import { useNavigate } from "react-router-dom";
import { RegisterTable } from "../../../../components/lab";
import type { RegisterColumn } from "../../../../components/lab";
import { StatusBadge } from "../../../../components/StatusBadge";
import { monospaceFontFamily } from "../../../../theme/palette";

import {
  EquipmentInventoryService, ActiveEquipmentDto, EquipmentActivityDto, WhereIsItResultDto
} from "../services/EquipmentInventoryService";
import { formatLabDate, formatLabDateTime } from "../../../../utils/formatDate";
import { clickable } from "../../../../utils/clickable";

interface ActiveEquipmentViewProps {
  onOpenDetails: (equipmentId: number) => void;
}

export function ActiveEquipmentView({ onOpenDetails }: ActiveEquipmentViewProps) {
  const navigate = useNavigate();
  const theme = useTheme();

  // Active Equipment state
  const [activeEquipment, setActiveEquipment] = useState<ActiveEquipmentDto[]>([]);
  const [selectedEqId, setSelectedEqId] = useState<number | null>(null);
  const [loadingActive, setLoadingActive] = useState(true);

  // Selected Equipment Activities state
  const [activities, setActivities] = useState<EquipmentActivityDto[]>([]);
  const [loadingActivities, setLoadingActivities] = useState(false);

  // Activity History state
  const [historyItemCode, setHistoryItemCode] = useState("");
  const [historyFromDate, setHistoryFromDate] = useState("");
  const [historyToDate, setHistoryToDate] = useState("");
  const [historyResults, setHistoryResults] = useState<EquipmentActivityDto[] | null>(null);
  const [loadingHistory, setLoadingHistory] = useState(false);

  // "Where is it?" search state
  const [whereQuery, setWhereQuery] = useState("");
  const [whereResult, setWhereResult] = useState<WhereIsItResultDto | null>(null);
  const [loadingWhere, setLoadingWhere] = useState(false);

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
      loadHistoryForEquipment(selectedEqId);
    }
  };

  const handleWhereIsItSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!whereQuery.trim()) return;
    try {
      setLoadingWhere(true);
      const res = await EquipmentInventoryService.whereIsIt(whereQuery);
      setWhereResult(res);
    } catch {
      setWhereResult(null);
    } finally {
      setLoadingWhere(false);
    }
  };

  const activityRoute = (act: EquipmentActivityDto): string | null =>
    act.entityType === "Sample"
      ? "/receiving-testing"
      : act.entityType === "Media"
      ? "/laboratory-configuration/media"
      : act.entityType === "Cryovial"
      ? "/laboratory-configuration/cryovials"
      : null;

  const whereColumns: RegisterColumn<WhereIsItResultDto["history"][number]>[] = [
    { key: "equipmentCode", label: "Equipment", sortable: true, render: (h) => <Box component="span" sx={{ fontWeight: 600, fontFamily: monospaceFontFamily }}>{h.equipmentCode} ({h.equipmentName})</Box> },
    { key: "activityType", label: "Activity", sortable: true },
    { key: "startedOn", label: "Started On", sortable: true, render: (h) => formatLabDateTime(h.startedOn) },
    { key: "performedBy", label: "Started By", sortable: true },
    { key: "completedOn", label: "Ended On", sortable: true, render: (h) => (h.completedOn ? formatLabDateTime(h.completedOn) : "Active") },
    { key: "completedBy", label: "Ended By", sortable: true, render: (h) => h.completedBy ?? "—" }
  ];

  const activityColumns: RegisterColumn<EquipmentActivityDto>[] = [
    { key: "itemName", label: "Item / Activity", sortable: true, render: (a) => <Box component="span" sx={{ fontWeight: 600 }}>{a.itemName}</Box> },
    { key: "itemCode", label: "Item Code", sortable: true, render: (a) => <Box component="span" sx={{ fontWeight: 700, fontFamily: monospaceFontFamily }}>{a.itemCode}</Box> },
    { key: "activityType", label: "Activity Type", sortable: true },
    { key: "mediaDescription", label: "Media / Description" },
    { key: "startedOn", label: "Started On", sortable: true, render: (a) => formatLabDateTime(a.startedOn) },
    { key: "startedBy", label: "Started By", sortable: true },
    { key: "expectedCompletion", label: "Expected Completion", sortable: true, render: (a) => (a.expectedCompletion ? formatLabDateTime(a.expectedCompletion) : "N/A") }
  ];

  const historyColumns: RegisterColumn<EquipmentActivityDto>[] = [
    { key: "itemName", label: "Item / Activity", sortable: true, render: (h) => <Box component="span" sx={{ fontWeight: 600 }}>{h.itemName}</Box> },
    { key: "itemCode", label: "Item Code", sortable: true, render: (h) => <Box component="span" sx={{ fontWeight: 700, fontFamily: monospaceFontFamily }}>{h.itemCode}</Box> },
    { key: "activityType", label: "Activity Type", sortable: true },
    { key: "mediaDescription", label: "Media / Description" },
    { key: "startedOn", label: "Started On", sortable: true, render: (h) => formatLabDateTime(h.startedOn) },
    { key: "startedBy", label: "Started By", sortable: true },
    { key: "completedOn", label: "Ended On", sortable: true, render: (h) => (h.completedOn ? formatLabDateTime(h.completedOn) : <StatusBadge status="Active" />) },
    { key: "completedBy", label: "Ended By", sortable: true, render: (h) => h.completedBy ?? "—" }
  ];

  const selectedEquipment = activeEquipment.find((e) => e.id === selectedEqId);

  return (
    <Stack spacing={3}>
      {/* 1. Global "Where is it?" Traceability Search Bar */}
      <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
        <Stack spacing={2}>
          <Stack direction="row" spacing={1} sx={{
            alignItems: "center"
          }}>
            <PlaceIcon color="primary" />
            <Typography variant="subtitle1" sx={{
              fontWeight: 700
            }}>
              Where is it? Global Traceability Search
            </Typography>
          </Stack>
          <Box component="form" onSubmit={handleWhereIsItSearch} sx={{ display: "flex", gap: 1.5 }}>
            <TextField
              size="small"
              fullWidth
              placeholder="Search by item code, sample reference, or media lot (e.g. TSB/08/26, PT-0021)..."
              value={whereQuery}
              onChange={(e) => setWhereQuery(e.target.value)}
              slotProps={{ htmlInput: { "aria-label": "Search by item code, sample reference, or media lot" } }}
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
              <Typography
                variant="subtitle2"
                sx={{
                  fontWeight: 700,
                  mb: 1.5
                }}>
                Traceability Results for "{whereResult.searchTerm}":
              </Typography>

              {whereResult.currentActivity ? (
                <Box sx={{ mb: 2, p: 2, bgcolor: theme.custom.status.notDetected.bg, border: "1px solid", borderColor: theme.custom.status.notDetected.border, borderRadius: 1.5 }}>
                  <Stack
                    direction="row"
                    sx={{
                      justifyContent: "space-between",
                      alignItems: "center"
                    }}>
                    <Box>
                      <Typography
                        variant="body2"
                        sx={{
                          fontWeight: 700,
                          color: "success.dark"
                        }}>
                        CURRENT LOCATION: {whereResult.currentEquipmentCode}, {whereResult.currentEquipmentName}
                      </Typography>
                      <Typography variant="body2">
                        Activity: <strong>{whereResult.currentActivity.activityType}</strong> | Item: <strong>{whereResult.currentActivity.itemName} (@{whereResult.currentActivity.itemCode})</strong>
                      </Typography>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>
                        Started: {formatLabDateTime(whereResult.currentActivity.startedOn)} | Analyst: {whereResult.currentActivity.startedBy}
                      </Typography>
                    </Box>
                    <StatusBadge status="Active" label="Active / Current Location" />
                  </Stack>
                </Box>
              ) : (
                <Alert severity="info" sx={{ mb: 2, fontSize: 13 }}>
                  Item is not currently in an active equipment location. Viewing location history below:
                </Alert>
              )}

              {whereResult.history.length > 0 ? (
                <>
                  <RegisterTable
                    columns={whereColumns}
                    rows={whereResult.history}
                    getRowId={(h) => `${h.equipmentCode}-${h.startedOn}-${h.activityType}`}
                    pageSize={25}
                    empty={{ title: "No location history records found matching this query." }}
                  />
                </>
              ) : (
                <Typography
                  variant="body2"
                  align="center"
                  sx={{
                    color: "text.secondary",
                    py: 1
                  }}>
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
        <Grid
          size={{
            xs: 12,
            md: 4
          }}>
          <Paper sx={{ p: 2, borderRadius: 2, border: "1px solid", borderColor: "divider", minHeight: 500 }}>
            <Typography
              variant="subtitle1"
              sx={{
                fontWeight: 700,
                mb: 0.25
              }}>
              Equipment ({activeEquipment.length})
            </Typography>
            <Typography
              variant="caption"
              sx={{
                color: "text.secondary",
                display: "block",
                mb: 2
              }}>
              {activeEquipment.filter((e) => e.activeItemCount > 0).length} currently in use. Select any equipment to view its traceability history
            </Typography>

            {loadingActive ? (
              <Box
                sx={{
                  textAlign: "center",
                  py: 4
                }}><CircularProgress size={32} /></Box>
            ) : activeEquipment.length === 0 ? (
              <Alert severity="info" sx={{ fontSize: 13 }}>
                No equipment records found.
              </Alert>
            ) : (
              <Stack spacing={1.5}>
                {activeEquipment.map((eq) => {
                  const isSelected = eq.id === selectedEqId;
                  const isInUse = eq.activeItemCount > 0;
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
                        opacity: isInUse ? 1 : 0.75,
                        transition: "all 0.15s ease-in-out",
                        "&:hover": { borderColor: "primary.main", bgcolor: isSelected ? theme.custom.status.purple.bg : "action.hover", opacity: 1 }
                      }}
                      {...clickable(() => setSelectedEqId(eq.id), { pressed: selectedEqId === eq.id })}
                    >
                      <Stack
                        direction="row"
                        sx={{
                          justifyContent: "space-between",
                          alignItems: "flex-start",
                          mb: 1
                        }}>
                        <Typography sx={{ fontFamily: "monospace", fontWeight: 700, fontSize: 14, color: "primary.main" }}>
                          {eq.code}
                        </Typography>
                        <StatusBadge
                          status={isInUse ? "InUse" : "Idle"}
                          label={isInUse ? `${eq.activeItemCount} ${eq.activeItemCount === 1 ? "item" : "items"}` : "Idle"}
                        />
                      </Stack>
                      <Typography sx={{ fontWeight: 600, fontSize: 13 }}>{eq.instrumentType}</Typography>
                      <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.5 }}>{eq.location}</Typography>
                      <Typography sx={{ fontSize: 11, color: "text.secondary", mt: 1 }}>{eq.primaryActivityCategory}</Typography>
                    </Paper>
                  );
                })}
              </Stack>
            )}
          </Paper>
        </Grid>

        {/* RIGHT MAIN PANEL: Selected Equipment Details & Activities */}
        <Grid
          size={{
            xs: 12,
            md: 8
          }}>
          {selectedEquipment ? (
            <Stack spacing={3}>
              {/* Equipment Info Header Card */}
              <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
                <Stack
                  direction="row"
                  sx={{
                    justifyContent: "space-between",
                    alignItems: "flex-start",
                    mb: 2
                  }}>
                  <Box>
                    <Typography variant="h6" sx={{
                      fontWeight: 700
                    }}>
                      {selectedEquipment.code}: {selectedEquipment.instrumentType}
                    </Typography>
                    <Typography variant="body2" sx={{
                      color: "text.secondary"
                    }}>
                      Location: {selectedEquipment.location}
                    </Typography>
                  </Box>
                  <Stack direction="row" spacing={1} sx={{
                    alignItems: "center"
                  }}>
                    <StatusBadge status={selectedEquipment.activeItemCount > 0 ? "InUse" : "Idle"} label={selectedEquipment.activeItemCount > 0 ? "In Use" : "Idle"} />
                    <Button size="small" variant="outlined" startIcon={<InfoOutlinedIcon />} onClick={() => onOpenDetails(selectedEquipment.id)}>
                      Equipment Details
                    </Button>
                  </Stack>
                </Stack>

                <Grid container spacing={2} sx={{ pt: 1, borderTop: "1px solid", borderTopColor: "divider" }}>
                  <Grid
                    size={{
                      xs: 6,
                      sm: 3
                    }}>
                    <Typography
                      variant="caption"
                      sx={{
                        color: "text.secondary",
                        display: "block"
                      }}>Manufacturer</Typography>
                    <Typography variant="body2" sx={{
                      fontWeight: 600
                    }}>{selectedEquipment.manufacturerName || "—"}</Typography>
                  </Grid>
                  <Grid
                    size={{
                      xs: 6,
                      sm: 3
                    }}>
                    <Typography
                      variant="caption"
                      sx={{
                        color: "text.secondary",
                        display: "block"
                      }}>Set Temperature</Typography>
                    <Typography variant="body2" sx={{
                      fontWeight: 600
                    }}>
                      {selectedEquipment.setPointTemperature ? `${selectedEquipment.setPointTemperature} °C` : "N/A"}
                    </Typography>
                  </Grid>
                  <Grid
                    size={{
                      xs: 6,
                      sm: 3
                    }}>
                    <Typography
                      variant="caption"
                      sx={{
                        color: "text.secondary",
                        display: "block"
                      }}>Calibration Due</Typography>
                    <Typography variant="body2" sx={{
                      fontWeight: 600
                    }}>
                      {selectedEquipment.calibrationDueDate ? formatLabDate(selectedEquipment.calibrationDueDate) : "—"}
                    </Typography>
                  </Grid>
                  <Grid
                    size={{
                      xs: 6,
                      sm: 3
                    }}>
                    <Typography
                      variant="caption"
                      sx={{
                        color: "text.secondary",
                        display: "block"
                      }}>Current Active Items</Typography>
                    <Typography
                      variant="body2"
                      sx={{
                        fontWeight: 700,
                        color: "primary.main"
                      }}>{selectedEquipment.activeItemCount}</Typography>
                  </Grid>
                </Grid>
              </Paper>

              {/* Current Activities / Items Table */}
              <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
                <Typography
                  variant="subtitle1"
                  sx={{
                    fontWeight: 700,
                    mb: 1.5
                  }}>
                  Current Activities / Items ({activities.length})
                </Typography>

                {loadingActivities ? (
                  <Box
                    sx={{
                      textAlign: "center",
                      py: 3
                    }}><CircularProgress size={28} /></Box>
                ) : activities.length === 0 ? (
                  <Alert severity="info" sx={{ fontSize: 13 }}>No active activities currently running in this equipment.</Alert>
                ) : (
                  <>
                    <RegisterTable
                      columns={activityColumns}
                      rows={activities}
                      getRowId={(act) => act.activityId}
                      pageSize={25}
                      onRowClick={(act) => { const route = activityRoute(act); if (route) navigate(route); }}
                      rowActions={(act) => {
                        const route = activityRoute(act);
                        return route ? [{ label: "View activity / test workspace", onClick: () => navigate(route) }] : [];
                      }}
                      empty={{ title: "No active activities currently running in this equipment." }}
                    />
                  </>
                )}
              </Paper>

              {/* Date-to-Date Activity History Search */}
              <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
                <Stack spacing={2}>
                  <Stack direction="row" spacing={1} sx={{
                    alignItems: "center"
                  }}>
                    <HistoryIcon color="action" />
                    <Typography variant="subtitle1" sx={{
                      fontWeight: 700
                    }}>
                      Search Activity History: {selectedEquipment.code}
                    </Typography>
                  </Stack>

                  <Box component="form" onSubmit={handleHistorySearch}>
                    <Grid container spacing={2} sx={{
                      alignItems: "center"
                    }}>
                      <Grid
                        size={{
                          xs: 12,
                          sm: 4
                        }}>
                        <TextField
                          size="small"
                          label="Item / Code Filter"
                          placeholder="e.g. PT-0021"
                          value={historyItemCode}
                          onChange={(e) => setHistoryItemCode(e.target.value)}
                          fullWidth
                        />
                      </Grid>
                      <Grid
                        size={{
                          xs: 6,
                          sm: 3
                        }}>
                        <TextField
                          size="small"
                          label="From Date"
                          type="date"
                          value={historyFromDate}
                          onChange={(e) => setHistoryFromDate(e.target.value)}
                          fullWidth
                          slotProps={{
                            inputLabel: { shrink: true }
                          }}
                        />
                      </Grid>
                      <Grid
                        size={{
                          xs: 6,
                          sm: 3
                        }}>
                        <TextField
                          size="small"
                          label="To Date"
                          type="date"
                          value={historyToDate}
                          onChange={(e) => setHistoryToDate(e.target.value)}
                          fullWidth
                          slotProps={{
                            inputLabel: { shrink: true }
                          }}
                        />
                      </Grid>
                      <Grid
                        size={{
                          xs: 12,
                          sm: 2
                        }}>
                        <Button type="submit" variant="outlined" color="primary" fullWidth disabled={loadingHistory}>
                          {loadingHistory ? "Searching..." : "Search"}
                        </Button>
                      </Grid>
                    </Grid>
                  </Box>

                  {historyResults && (
                    <Box sx={{ mt: 1 }}>
                      <RegisterTable
                        columns={historyColumns}
                        rows={historyResults}
                        getRowId={(h) => h.activityId}
                        pageSize={25}
                        empty={{ title: "No historical activities found matching the selected search criteria." }}
                      />
                    </Box>
                  )}
                </Stack>
              </Paper>
            </Stack>
          ) : (
            <Paper sx={{ p: 4, textAlign: "center", borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
              <Typography variant="body1" sx={{
                color: "text.secondary"
              }}>
                Select an active equipment record from the left panel to inspect its current activities and traceability history.
              </Typography>
            </Paper>
          )}
        </Grid>
      </Grid>
    </Stack>
  );
}
