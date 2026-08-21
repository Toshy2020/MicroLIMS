import { useEffect, useState } from "react";
import {
  Paper,
  TextField,
  Button,
  Select,
  MenuItem,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Box,
  Typography,
  Chip,
  Tabs,
  Tab,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Alert,
  IconButton,
  Tooltip,
  Divider,
  Stack,
  CircularProgress,
  useTheme
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import HistoryIcon from "@mui/icons-material/History";
import AddIcon from "@mui/icons-material/Add";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import ThermostatIcon from "@mui/icons-material/Thermostat";
import RefreshIcon from "@mui/icons-material/Refresh";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { brandColors } from "../../../theme";
import { useNavigate } from "react-router-dom";
import {
  EquipmentConfigurationService,
  ConfiguredEquipmentSummary,
  IncubatorSetPointHistory,
  AutoclaveProgram,
  AutoclaveProgramHistory
} from "./services/EquipmentConfigurationService";
import { EquipmentInventoryService } from "../../inventory/equipment/services/EquipmentInventoryService";

export function EquipmentPage() {
  const theme = useTheme();
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState(0);
  const [summaryList, setSummaryList] = useState<ConfiguredEquipmentSummary[]>([]);
  const [inventoryList, setInventoryList] = useState<any[]>([]);
  const [allPrograms, setAllPrograms] = useState<AutoclaveProgram[]>([]);
  const [selectedEqId, setSelectedEqId] = useState<number | null>(null);

  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("All");

  // Incubator Edit State
  const [editSetPointDialogOpen, setEditSetPointDialogOpen] = useState(false);
  const [newSetPoint, setNewSetPoint] = useState("");
  const [setPointReason, setSetPointReason] = useState("");
  const [setPointHistory, setSetPointHistory] = useState<IncubatorSetPointHistory[]>([]);

  // Autoclave Program Edit State
  const [programDialogOpen, setProgramDialogOpen] = useState(false);
  const [programForm, setProgramForm] = useState<Record<string, any>>({});
  const [programHistoryDialogOpen, setProgramHistoryDialogOpen] = useState(false);
  const [programHistory, setProgramHistory] = useState<AutoclaveProgramHistory[]>([]);
  const [selectedProgramCode, setSelectedProgramCode] = useState("");

  // Select from Inventory Dialog
  const [inventoryDialogOpen, setInventoryDialogOpen] = useState(false);

  // General Loading & Error State
  const [loading, setLoading] = useState(true);
  const [pageError, setPageError] = useState<string | null>(null);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const loadData = async () => {
    setLoading(true);
    setPageError(null);
    try {
      const summaryData = await EquipmentConfigurationService.getConfiguredSummary();
      const normalizedSummary = Array.isArray(summaryData) ? summaryData : [];
      setSummaryList(normalizedSummary);

      const invData = await EquipmentInventoryService.getAll();
      setInventoryList(Array.isArray(invData) ? invData : []);

      const progData = await EquipmentConfigurationService.getAutoclavePrograms();
      setAllPrograms(Array.isArray(progData) ? progData : []);

      if (normalizedSummary.length > 0 && selectedEqId === null) {
        setSelectedEqId(normalizedSummary[0].id);
      }
    } catch (err: any) {
      console.error("Failed to load laboratory equipment configuration:", err);
      setPageError(err?.message || "Could not load laboratory equipment configuration.");
      setSummaryList([]);
      setInventoryList([]);
      setAllPrograms([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const safeSummaryList = Array.isArray(summaryList) ? summaryList : [];
  const selectedEquipment = safeSummaryList.find((e) => e.id === selectedEqId);

  // Load history when selected equipment changes
  useEffect(() => {
    if (selectedEquipment) {
      if (selectedEquipment.type === "Incubator" || selectedEquipment.type === 0) {
        EquipmentConfigurationService.getSetPointHistory(selectedEquipment.id)
          .then((history) => setSetPointHistory(Array.isArray(history) ? history : []))
          .catch(() => setSetPointHistory([]));
      }
    } else {
      setSetPointHistory([]);
    }
  }, [selectedEqId, selectedEquipment]);

  // Handlers
  const handleOpenEditSetPoint = () => {
    if (!selectedEquipment) return;
    setNewSetPoint(selectedEquipment.setPointTemperature?.toString() ?? "32.5");
    setSetPointReason("");
    setDialogError(null);
    setEditSetPointDialogOpen(true);
  };

  const handleSaveSetPoint = async () => {
    if (!selectedEquipment) return;
    if (!newSetPoint || isNaN(Number(newSetPoint))) {
      setDialogError("Please enter a valid numeric set point temperature.");
      return;
    }
    if (!setPointReason.trim()) {
      setDialogError("Reason for Change is required for audit and ALCOA+ compliance.");
      return;
    }

    setSaving(true);
    setDialogError(null);
    try {
      await EquipmentConfigurationService.updateSetPoint(selectedEquipment.id, {
        newSetPoint: Number(newSetPoint),
        reason: setPointReason.trim()
      });
      setEditSetPointDialogOpen(false);
      await loadData();
      const updatedHistory = await EquipmentConfigurationService.getSetPointHistory(selectedEquipment.id);
      setSetPointHistory(Array.isArray(updatedHistory) ? updatedHistory : []);
    } catch (err: any) {
      setDialogError(err?.response?.data?.message ?? err?.message ?? "Could not update set point.");
    } finally {
      setSaving(false);
    }
  };

  const handleOpenAddProgram = () => {
    if (!selectedEquipment) return;
    setProgramForm({
      equipmentId: selectedEquipment.id,
      programCode: "",
      programName: "",
      loadType: "Media",
      temperature: 121,
      cycleTimeMinutes: 15,
      isActive: true,
      comment: "Initial program configuration"
    });
    setDialogError(null);
    setProgramDialogOpen(true);
  };

  const handleOpenEditProgram = (prog: AutoclaveProgram) => {
    setProgramForm({
      id: prog.id,
      equipmentId: prog.equipmentId,
      programCode: prog.programCode,
      programName: prog.programName,
      loadType: prog.loadType,
      temperature: prog.temperature,
      cycleTimeMinutes: prog.cycleTimeMinutes,
      isActive: prog.isActive,
      comment: ""
    });
    setDialogError(null);
    setProgramDialogOpen(true);
  };

  const handleSaveProgram = async () => {
    if (!programForm.programCode || !programForm.programName || !programForm.loadType) {
      setDialogError("Please fill out Program Code, Program Name, and Load Type.");
      return;
    }

    setSaving(true);
    setDialogError(null);
    try {
      await EquipmentConfigurationService.saveAutoclaveProgram(programForm.equipmentId, {
        id: programForm.id,
        equipmentId: programForm.equipmentId,
        programCode: programForm.programCode,
        programName: programForm.programName,
        loadType: programForm.loadType,
        temperature: Number(programForm.temperature),
        cycleTimeMinutes: Number(programForm.cycleTimeMinutes),
        isActive: Boolean(programForm.isActive),
        comment: programForm.comment
      });
      setProgramDialogOpen(false);
      await loadData();
    } catch (err: any) {
      setDialogError(err?.response?.data?.message ?? err?.message ?? "Could not save program.");
    } finally {
      setSaving(false);
    }
  };

  const handleToggleProgramStatus = async (prog: AutoclaveProgram) => {
    const newStatus = !prog.isActive;
    const comment = newStatus ? "Activated program" : "Deactivated program";
    try {
      await EquipmentConfigurationService.setAutoclaveProgramStatus(prog.id, newStatus, comment);
      await loadData();
    } catch (err: any) {
      alert(err?.response?.data?.message ?? err?.message ?? "Status update failed.");
    }
  };

  const handleViewProgramHistory = async (prog: AutoclaveProgram) => {
    setSelectedProgramCode(prog.programCode);
    try {
      const history = await EquipmentConfigurationService.getAutoclaveProgramHistory(prog.id);
      setProgramHistory(Array.isArray(history) ? history : []);
      setProgramHistoryDialogOpen(true);
    } catch (err) {
      setProgramHistory([]);
    }
  };

  const handleLinkInventoryEquipment = async (invId: number) => {
    try {
      await EquipmentConfigurationService.linkInventory(invId);
      setInventoryDialogOpen(false);
      await loadData();
    } catch (err: any) {
      alert(err?.response?.data?.message ?? err?.message ?? "Could not link equipment.");
    }
  };

  // Filters
  const filteredSummary = safeSummaryList.filter((e) => {
    const matchType =
      typeFilter === "All"
        ? true
        : typeFilter === "Incubator"
        ? e.type === "Incubator" || e.type === 0
        : typeFilter === "Autoclave"
        ? e.type === "Autoclave" || e.type === 1
        : typeFilter === "LafCabinet"
        ? e.type === "LafCabinet" || e.type === 2
        : true;
    const matchSearch =
      search === "" ||
      (e.code && e.code.toLowerCase().includes(search.toLowerCase())) ||
      (e.name && e.name.toLowerCase().includes(search.toLowerCase()));
    return matchType && matchSearch;
  });

  const safeAllPrograms = Array.isArray(allPrograms) ? allPrograms : [];
  const autoclaveProgramsForSelected = selectedEqId !== null
    ? safeAllPrograms.filter((p) => p.equipmentId === selectedEqId)
    : [];

  const safeSetPointHistory = Array.isArray(setPointHistory) ? setPointHistory : [];

  return (
    <>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
        <PageHeader
          title="Laboratory Configuration — Equipment"
          subtitle="Configure equipment used by this laboratory, set points, and autoclave programs."
        />
        <Stack direction="row" spacing={1}>
          <IconButton onClick={loadData} title="Refresh data">
            <RefreshIcon />
          </IconButton>
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={() => setInventoryDialogOpen(true)}
            sx={{ bgcolor: brandColors.sectionTitle, "&:hover": { bgcolor: "#632273" } }}
          >
            Select from Inventory
          </Button>
        </Stack>
      </Box>

      {pageError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {pageError}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", alignItems: "center", py: 8 }}>
          <CircularProgress />
        </Box>
      ) : (
        <>
          <Paper sx={{ mb: 3 }}>
            <Tabs
              value={activeTab}
              onChange={(_, v) => setActiveTab(v)}
              indicatorColor="primary"
              textColor="primary"
              sx={{ borderBottom: 1, borderColor: "divider" }}
            >
              <Tab label="Configured Equipment" />
              <Tab label={`Autoclave Programs / Loads (${safeAllPrograms.length})`} />
              <Tab label="Configuration History" />
            </Tabs>
          </Paper>

          {/* TAB 0: CONFIGURED EQUIPMENT */}
          {activeTab === 0 && (
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "340px 1fr" }, gap: 3 }}>
              {/* Left Panel: Search & Equipment List */}
              <Paper sx={{ p: 2, height: "fit-content" }}>
                <SectionTitle>Configured Equipment</SectionTitle>
                <Stack spacing={1.5} sx={{ mb: 2 }}>
                  <TextField
                    size="small"
                    placeholder="Search by code or name…"
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                  />
                  <Select size="small" value={typeFilter} onChange={(e) => setTypeFilter(e.target.value)}>
                    <MenuItem value="All">All Types</MenuItem>
                    <MenuItem value="Incubator">Incubators</MenuItem>
                    <MenuItem value="Autoclave">Autoclaves</MenuItem>
                    <MenuItem value="LafCabinet">LAF Cabinets</MenuItem>
                  </Select>
                </Stack>

                <Divider sx={{ mb: 2 }} />

                <Stack spacing={1} sx={{ maxHeight: 600, overflowY: "auto" }}>
                  {filteredSummary.map((eq) => {
                    const isSelected = eq.id === selectedEqId;
                    const isIncubator = eq.type === "Incubator" || eq.type === 0;
                    const isAutoclave = eq.type === "Autoclave" || eq.type === 1;

                    return (
                      <Box
                        key={eq.id}
                        onClick={() => setSelectedEqId(eq.id)}
                        sx={{
                          p: 1.5,
                          borderRadius: 1.5,
                          border: 1,
                          borderColor: isSelected ? brandColors.sectionTitle : "divider",
                          bgcolor: isSelected ? "action.selected" : "background.paper",
                          cursor: "pointer",
                          transition: "all 0.15s ease",
                          "&:hover": { borderColor: theme.palette.primary.main }
                        }}
                      >
                        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 0.5 }}>
                          <Typography sx={{ fontWeight: 700, fontSize: 14 }}>{eq.code}</Typography>
                          <Chip
                            size="small"
                            label={isIncubator ? "Incubator" : isAutoclave ? "Autoclave" : "Equipment"}
                            color={isIncubator ? "primary" : isAutoclave ? "secondary" : "default"}
                            variant="outlined"
                            sx={{ height: 20, fontSize: 11 }}
                          />
                        </Box>
                        <Typography variant="body2" color="text.secondary" sx={{ fontSize: 12 }}>
                          {eq.name}
                        </Typography>

                        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 1 }}>
                          {isIncubator && (
                            <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main }}>
                              Set Point: {eq.setPointTemperature ? `${eq.setPointTemperature} °C` : "—"}
                            </Typography>
                          )}
                          {isAutoclave && (
                            <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary" }}>
                              {eq.configuredProgramCount} Programs
                            </Typography>
                          )}
                          <Chip
                            size="small"
                            label={eq.inventoryStatus ?? "In Service"}
                            color={eq.inventoryStatus === "OutOfService" ? "error" : "success"}
                            sx={{ height: 18, fontSize: 10 }}
                          />
                        </Box>
                      </Box>
                    );
                  })}
                  {safeSummaryList.length === 0 && (
                    <Typography variant="body2" color="text.secondary" sx={{ textAlign: "center", py: 4 }}>
                      No equipment configured for this laboratory.
                    </Typography>
                  )}
                  {safeSummaryList.length > 0 && filteredSummary.length === 0 && (
                    <Typography variant="body2" color="text.secondary" sx={{ textAlign: "center", py: 4 }}>
                      No equipment matching search filter.
                    </Typography>
                  )}
                </Stack>
              </Paper>

              {/* Right Panel: Selected Equipment Details & Configuration */}
              {selectedEquipment ? (
                <Stack spacing={3}>
                  {/* Card 1: Read-Only Inventory Information */}
                  <Paper sx={{ p: 2.5 }}>
                    <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
                      <Box>
                        <Typography sx={{ fontSize: 20, fontWeight: 700, color: theme.palette.primary.main }}>
                          {selectedEquipment.code} — {selectedEquipment.name}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                          Master Inventory Information (Read-Only)
                        </Typography>
                      </Box>
                      <Button
                        variant="outlined"
                        size="small"
                        startIcon={<OpenInNewIcon />}
                        onClick={() => navigate("/inventory/equipment")}
                      >
                        View in Inventory
                      </Button>
                    </Box>

                    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(5, 1fr)" }, gap: 2 }}>
                      <Box>
                        <Typography variant="caption" color="text.secondary">Equipment Type</Typography>
                        <Typography variant="body2" fontWeight={600}>{selectedEquipment.type}</Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" color="text.secondary">Serial Number</Typography>
                        <Typography variant="body2" fontWeight={700} sx={{ color: theme.palette.primary.main }}>
                          {selectedEquipment.serialNumber || "—"}
                        </Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" color="text.secondary">Location</Typography>
                        <Typography variant="body2" fontWeight={600}>{selectedEquipment.inventoryLocation || selectedEquipment.location || "—"}</Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" color="text.secondary">Calibration Due Date</Typography>
                        <Typography variant="body2" fontWeight={600}>
                          {selectedEquipment.calibrationDueDate ? new Date(selectedEquipment.calibrationDueDate).toLocaleDateString() : "—"}
                        </Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" color="text.secondary">Operational Status</Typography>
                        <Typography variant="body2" fontWeight={600}>{selectedEquipment.inventoryStatus || "In Service"}</Typography>
                      </Box>
                    </Box>
                  </Paper>

                  {/* Card 2: Configuration Specifics (Incubator vs Autoclave) */}
                  {(selectedEquipment.type === "Incubator" || selectedEquipment.type === 0) && (
                    <Paper sx={{ p: 2.5 }}>
                      <SectionTitle>Incubator Configuration</SectionTitle>

                      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", p: 2, bgcolor: "action.hover", borderRadius: 2, mb: 3 }}>
                        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
                          <ThermostatIcon sx={{ fontSize: 36, color: theme.palette.primary.main }} />
                          <Box>
                            <Typography variant="caption" color="text.secondary">Current Set Point Temperature</Typography>
                            <Typography sx={{ fontSize: 26, fontWeight: 800, color: theme.palette.primary.main }}>
                              {selectedEquipment.setPointTemperature ? `${selectedEquipment.setPointTemperature} °C` : "Not Configured"}
                            </Typography>
                          </Box>
                        </Box>
                        <Button
                          variant="contained"
                          startIcon={<EditIcon />}
                          onClick={handleOpenEditSetPoint}
                          sx={{ bgcolor: brandColors.sectionTitle, "&:hover": { bgcolor: "#632273" } }}
                        >
                          Edit Set Point
                        </Button>
                      </Box>

                      <Typography sx={{ fontSize: 14, fontWeight: 700, mb: 1.5 }}>
                        Set Point Change History
                      </Typography>

                      <Table size="small">
                        <TableHead>
                          <TableRow>
                            <TableCell>Effective On</TableCell>
                            <TableCell align="right">Previous</TableCell>
                            <TableCell align="right">New</TableCell>
                            <TableCell>Changed By</TableCell>
                            <TableCell>Reason for Change</TableCell>
                          </TableRow>
                        </TableHead>
                        <TableBody>
                          {safeSetPointHistory.map((h) => (
                            <TableRow key={h.id}>
                              <TableCell>{new Date(h.changedAt).toLocaleString()}</TableCell>
                              <TableCell align="right">{h.previousSetPoint} °C</TableCell>
                              <TableCell align="right" sx={{ fontWeight: 700, color: theme.palette.primary.main }}>{h.newSetPoint} °C</TableCell>
                              <TableCell>{h.changedByName}</TableCell>
                              <TableCell>{h.reason}</TableCell>
                            </TableRow>
                          ))}
                          {safeSetPointHistory.length === 0 && (
                            <TableRow>
                              <TableCell colSpan={5} align="center" sx={{ color: "text.secondary", py: 3 }}>
                                No set point changes recorded yet.
                              </TableCell>
                            </TableRow>
                          )}
                        </TableBody>
                      </Table>
                    </Paper>
                  )}

                  {(selectedEquipment.type === "Autoclave" || selectedEquipment.type === 1) && (
                    <Paper sx={{ p: 2.5 }}>
                      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
                        <SectionTitle>Configured Programs / Loads</SectionTitle>
                        <Button
                          variant="contained"
                          size="small"
                          startIcon={<AddIcon />}
                          onClick={handleOpenAddProgram}
                          sx={{ bgcolor: brandColors.sectionTitle, "&:hover": { bgcolor: "#632273" } }}
                        >
                          Add Program / Load
                        </Button>
                      </Box>

                      <Table size="small">
                        <TableHead>
                          <TableRow>
                            <TableCell>Program</TableCell>
                            <TableCell>Program / Load Name</TableCell>
                            <TableCell>Load Type</TableCell>
                            <TableCell align="right">Temperature</TableCell>
                            <TableCell align="right">Cycle Time</TableCell>
                            <TableCell>Status</TableCell>
                            <TableCell align="center">Actions</TableCell>
                          </TableRow>
                        </TableHead>
                        <TableBody>
                          {autoclaveProgramsForSelected.map((p) => (
                            <TableRow key={p.id}>
                              <TableCell sx={{ fontWeight: 700 }}>{p.programCode}</TableCell>
                              <TableCell>{p.programName}</TableCell>
                              <TableCell>{p.loadType}</TableCell>
                              <TableCell align="right">{p.temperature} °C</TableCell>
                              <TableCell align="right">{p.cycleTimeMinutes} min</TableCell>
                              <TableCell>
                                <Chip
                                  size="small"
                                  label={p.isActive ? "Active" : "Inactive"}
                                  color={p.isActive ? "success" : "default"}
                                  sx={{ height: 20, fontSize: 11 }}
                                />
                              </TableCell>
                              <TableCell align="center">
                                <Tooltip title="Edit Program">
                                  <IconButton size="small" onClick={() => handleOpenEditProgram(p)}>
                                    <EditIcon fontSize="small" />
                                  </IconButton>
                                </Tooltip>
                                <Button
                                  size="small"
                                  sx={{ fontSize: 11, minWidth: 60 }}
                                  color={p.isActive ? "warning" : "success"}
                                  onClick={() => handleToggleProgramStatus(p)}
                                >
                                  {p.isActive ? "Deactivate" : "Activate"}
                                </Button>
                                <Tooltip title="View History">
                                  <IconButton size="small" onClick={() => handleViewProgramHistory(p)}>
                                    <HistoryIcon fontSize="small" />
                                  </IconButton>
                                </Tooltip>
                              </TableCell>
                            </TableRow>
                          ))}
                          {autoclaveProgramsForSelected.length === 0 && (
                            <TableRow>
                              <TableCell colSpan={7} align="center" sx={{ color: "text.secondary", py: 3 }}>
                                No autoclave programs configured. Click "+ Add Program / Load" to configure one.
                              </TableCell>
                            </TableRow>
                          )}
                        </TableBody>
                      </Table>
                    </Paper>
                  )}

                  {selectedEquipment.type !== "Incubator" && selectedEquipment.type !== 0 && selectedEquipment.type !== "Autoclave" && selectedEquipment.type !== 1 && (
                    <Paper sx={{ p: 2.5 }}>
                      <SectionTitle>Equipment Configuration</SectionTitle>
                      <Typography variant="body2" color="text.secondary">
                        This equipment item is configured for general laboratory usage. Additional specific operational parameters may be set under Master Data.
                      </Typography>
                    </Paper>
                  )}
                </Stack>
              ) : (
                <Paper sx={{ p: 4, textAlign: "center" }}>
                  <Typography color="text.secondary">
                    {safeSummaryList.length === 0
                      ? "No equipment configured for this laboratory."
                      : "Select an equipment item from the list to view its configuration."}
                  </Typography>
                </Paper>
              )}
            </Box>
          )}

          {/* TAB 1: AUTOCLAVE PROGRAMS / LOADS OVERVIEW */}
          {activeTab === 1 && (
            <Paper sx={{ p: 2.5 }}>
              <SectionTitle>All Configured Autoclave Programs / Loads</SectionTitle>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Autoclave</TableCell>
                    <TableCell>Program Code</TableCell>
                    <TableCell>Program / Load Name</TableCell>
                    <TableCell>Load Type</TableCell>
                    <TableCell align="right">Temperature</TableCell>
                    <TableCell align="right">Cycle Time</TableCell>
                    <TableCell>Status</TableCell>
                    <TableCell align="center">Actions</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {safeAllPrograms.map((p) => (
                    <TableRow key={p.id}>
                      <TableCell sx={{ fontWeight: 700 }}>{p.autoclaveCode} ({p.autoclaveName})</TableCell>
                      <TableCell>{p.programCode}</TableCell>
                      <TableCell>{p.programName}</TableCell>
                      <TableCell>{p.loadType}</TableCell>
                      <TableCell align="right">{p.temperature} °C</TableCell>
                      <TableCell align="right">{p.cycleTimeMinutes} min</TableCell>
                      <TableCell>
                        <Chip
                          size="small"
                          label={p.isActive ? "Active" : "Inactive"}
                          color={p.isActive ? "success" : "default"}
                        />
                      </TableCell>
                      <TableCell align="center">
                        <Tooltip title="Edit Program">
                          <IconButton size="small" onClick={() => handleOpenEditProgram(p)}>
                            <EditIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                        <Tooltip title="View History">
                          <IconButton size="small" onClick={() => handleViewProgramHistory(p)}>
                            <HistoryIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      </TableCell>
                    </TableRow>
                  ))}
                  {safeAllPrograms.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={8} align="center" sx={{ py: 3, color: "text.secondary" }}>
                        No autoclave programs configured across any autoclaves.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </Paper>
          )}

          {/* TAB 2: CONFIGURATION HISTORY */}
          {activeTab === 2 && (
            <Paper sx={{ p: 2.5 }}>
              <SectionTitle>Laboratory Configuration Change History</SectionTitle>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                Complete audit trail of incubator set point changes and autoclave program configuration events.
              </Typography>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Effective On</TableCell>
                    <TableCell>Equipment Code</TableCell>
                    <TableCell>Category</TableCell>
                    <TableCell>Details / Change</TableCell>
                    <TableCell>Changed By</TableCell>
                    <TableCell>Reason / Comment</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {safeSetPointHistory.map((h) => (
                    <TableRow key={`sp-${h.id}`}>
                      <TableCell>{new Date(h.changedAt).toLocaleString()}</TableCell>
                      <TableCell sx={{ fontWeight: 700 }}>{selectedEquipment?.code ?? "Incubator"}</TableCell>
                      <TableCell><Chip size="small" label="Set Point" color="primary" variant="outlined" /></TableCell>
                      <TableCell>Set Point: {h.previousSetPoint} °C → {h.newSetPoint} °C</TableCell>
                      <TableCell>{h.changedByName}</TableCell>
                      <TableCell>{h.reason}</TableCell>
                    </TableRow>
                  ))}
                  {safeSetPointHistory.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={6} align="center" sx={{ py: 3, color: "text.secondary" }}>
                        Select an incubator equipment item under Configured Equipment to view its configuration history.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </Paper>
          )}
        </>
      )}

      {/* DIALOG 1: EDIT INCUBATOR SET POINT */}
      <Dialog open={editSetPointDialogOpen} onClose={() => setEditSetPointDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle sx={{ fontWeight: 700 }}>Edit Incubator Set Point</DialogTitle>
        <DialogContent dividers>
          {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}
          <Stack spacing={2}>
            <Box>
              <Typography variant="caption" color="text.secondary">Equipment</Typography>
              <Typography variant="body1" fontWeight={700}>
                {selectedEquipment?.code} — {selectedEquipment?.name}
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">Current Set Point</Typography>
              <Typography variant="body1" fontWeight={700} color={brandColors.sectionTitle}>
                {selectedEquipment?.setPointTemperature ? `${selectedEquipment.setPointTemperature} °C` : "Not Configured"}
              </Typography>
            </Box>
            <TextField
              label="New Set Point (°C) *"
              type="number"
              inputProps={{ step: "0.1" }}
              value={newSetPoint}
              onChange={(e) => setNewSetPoint(e.target.value)}
              fullWidth
            />
            <TextField
              label="Reason for Change *"
              placeholder="e.g. Seasonal adjustment / Routine calibration adjustment"
              value={setPointReason}
              onChange={(e) => setSetPointReason(e.target.value)}
              multiline
              rows={2}
              fullWidth
            />
          </Stack>
        </DialogContent>
        <DialogActions sx={{ p: 2 }}>
          <Button onClick={() => setEditSetPointDialogOpen(false)} variant="outlined" disabled={saving}>
            Cancel
          </Button>
          <Button
            onClick={handleSaveSetPoint}
            variant="contained"
            disabled={saving}
            sx={{ bgcolor: brandColors.sectionTitle, "&:hover": { bgcolor: "#632273" } }}
          >
            {saving ? "Saving…" : "Save Changes"}
          </Button>
        </DialogActions>
      </Dialog>

      {/* DIALOG 2: ADD / EDIT AUTOCLAVE PROGRAM */}
      <Dialog open={programDialogOpen} onClose={() => setProgramDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle sx={{ fontWeight: 700 }}>
          {programForm.id ? "Edit Autoclave Program / Load" : "Add Autoclave Program / Load"}
        </DialogTitle>
        <DialogContent dividers>
          {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}
          <Stack spacing={2}>
            <Box>
              <Typography variant="caption" color="text.secondary">Autoclave Equipment</Typography>
              <Typography variant="body1" fontWeight={700}>
                {selectedEquipment?.code} — {selectedEquipment?.name}
              </Typography>
            </Box>
            <TextField
              label="Program / Load Code *"
              placeholder="e.g. P01"
              value={programForm.programCode ?? ""}
              onChange={(e) => setProgramForm({ ...programForm, programCode: e.target.value })}
              fullWidth
            />
            <TextField
              label="Program / Load Name *"
              placeholder="e.g. Prepared Media"
              value={programForm.programName ?? ""}
              onChange={(e) => setProgramForm({ ...programForm, programName: e.target.value })}
              fullWidth
            />
            <TextField
              label="Load Type *"
              placeholder="e.g. Media / Glassware / Biohazard Waste"
              value={programForm.loadType ?? ""}
              onChange={(e) => setProgramForm({ ...programForm, loadType: e.target.value })}
              fullWidth
            />
            <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2 }}>
              <TextField
                label="Temperature (°C) *"
                type="number"
                value={programForm.temperature ?? 121}
                onChange={(e) => setProgramForm({ ...programForm, temperature: Number(e.target.value) })}
              />
              <TextField
                label="Cycle Time (min) *"
                type="number"
                value={programForm.cycleTimeMinutes ?? 15}
                onChange={(e) => setProgramForm({ ...programForm, cycleTimeMinutes: Number(e.target.value) })}
              />
            </Box>
            <Select
              value={programForm.isActive ? "Active" : "Inactive"}
              onChange={(e) => setProgramForm({ ...programForm, isActive: e.target.value === "Active" })}
              fullWidth
            >
              <MenuItem value="Active">Active</MenuItem>
              <MenuItem value="Inactive">Inactive</MenuItem>
            </Select>
            <TextField
              label="Audit Comment / Reason *"
              placeholder="Explain the reason for creating or modifying this program configuration"
              value={programForm.comment ?? ""}
              onChange={(e) => setProgramForm({ ...programForm, comment: e.target.value })}
              multiline
              rows={2}
              fullWidth
            />
          </Stack>
        </DialogContent>
        <DialogActions sx={{ p: 2 }}>
          <Button onClick={() => setProgramDialogOpen(false)} variant="outlined" disabled={saving}>
            Cancel
          </Button>
          <Button
            onClick={handleSaveProgram}
            variant="contained"
            disabled={saving}
            sx={{ bgcolor: brandColors.sectionTitle, "&:hover": { bgcolor: "#632273" } }}
          >
            {saving ? "Saving…" : "Save Program"}
          </Button>
        </DialogActions>
      </Dialog>

      {/* DIALOG 3: AUTOCLAVE PROGRAM HISTORY */}
      <Dialog open={programHistoryDialogOpen} onClose={() => setProgramHistoryDialogOpen(false)} fullWidth maxWidth="md">
        <DialogTitle sx={{ fontWeight: 700 }}>
          Program History — {selectedProgramCode}
        </DialogTitle>
        <DialogContent dividers>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Date / Time</TableCell>
                <TableCell>Action</TableCell>
                <TableCell>Program Name</TableCell>
                <TableCell>Load Type</TableCell>
                <TableCell align="right">Temperature</TableCell>
                <TableCell align="right">Cycle Time</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Changed By</TableCell>
                <TableCell>Comment</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {Array.isArray(programHistory) && programHistory.map((h) => (
                <TableRow key={h.id}>
                  <TableCell>{new Date(h.changedAt).toLocaleString()}</TableCell>
                  <TableCell><Chip size="small" label={h.action} color={h.action === "Created" ? "success" : "info"} /></TableCell>
                  <TableCell>{h.newProgramName}</TableCell>
                  <TableCell>{h.newLoadType}</TableCell>
                  <TableCell align="right">{h.newTemperature} °C</TableCell>
                  <TableCell align="right">{h.newCycleTimeMinutes} min</TableCell>
                  <TableCell>{h.newIsActive ? "Active" : "Inactive"}</TableCell>
                  <TableCell>{h.changedByName}</TableCell>
                  <TableCell>{h.comment}</TableCell>
                </TableRow>
              ))}
              {(!Array.isArray(programHistory) || programHistory.length === 0) && (
                <TableRow>
                  <TableCell colSpan={9} align="center" sx={{ py: 3, color: "text.secondary" }}>
                    No historical changes logged for this program.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </DialogContent>
        <DialogActions sx={{ p: 2 }}>
          <Button onClick={() => setProgramHistoryDialogOpen(false)} variant="contained">
            Close
          </Button>
        </DialogActions>
      </Dialog>

      {/* DIALOG 4: SELECT FROM INVENTORY */}
      <Dialog open={inventoryDialogOpen} onClose={() => setInventoryDialogOpen(false)} fullWidth maxWidth="md">
        <DialogTitle sx={{ fontWeight: 700 }}>
          Select Equipment from Master Inventory
        </DialogTitle>
        <DialogContent dividers>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Register physical equipment from Inventory into Laboratory Configuration. Master equipment identity and calibration remain managed by Inventory.
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Equipment Code</TableCell>
                <TableCell>Instrument Type</TableCell>
                <TableCell>Manufacturer</TableCell>
                <TableCell>Location</TableCell>
                <TableCell>Status</TableCell>
                <TableCell align="center">Action</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {Array.isArray(inventoryList) && inventoryList.map((inv) => {
                const isAlreadyLinked = safeSummaryList.some(
                  (s) => (s.code && inv.code && s.code.toLowerCase() === inv.code.toLowerCase()) || s.equipmentInventoryId === inv.id
                );
                return (
                  <TableRow key={inv.id}>
                    <TableCell sx={{ fontWeight: 700 }}>{inv.code}</TableCell>
                    <TableCell>{inv.instrumentType}</TableCell>
                    <TableCell>{inv.manufacturerName}</TableCell>
                    <TableCell>{inv.location}</TableCell>
                    <TableCell>
                      <Chip size="small" label={inv.status} color={inv.status === "InService" ? "success" : "default"} />
                    </TableCell>
                    <TableCell align="center">
                      <Button
                        size="small"
                        variant={isAlreadyLinked ? "outlined" : "contained"}
                        disabled={isAlreadyLinked}
                        onClick={() => handleLinkInventoryEquipment(inv.id)}
                      >
                        {isAlreadyLinked ? "Configured" : "Configure for Lab"}
                      </Button>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </DialogContent>
        <DialogActions sx={{ p: 2 }}>
          <Button onClick={() => setInventoryDialogOpen(false)} variant="outlined">
            Close
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
