import { useEffect, useState } from "react";
import {
  Paper,
  TextField,
  Button,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
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
import { FloatingDialog } from "../../../components/FloatingDialog";
import { tableHeadSx } from "../../../theme";
import { toast } from "sonner";
import { Link } from "react-router-dom";
import {
  EquipmentConfigurationService,
  ConfiguredEquipmentSummary,
  IncubatorSetPointHistory,
  AutoclaveProgram,
  AutoclaveProgramHistory
} from "./services/EquipmentConfigurationService";
import { EquipmentInventoryService } from "../../inventory/equipment/services/EquipmentInventoryService";
import { useLaboratorySections } from "../../../hooks/useLaboratorySections";
import { getMySections, LaboratorySection } from "../../../services/laboratorySectionService";

export const EQUIPMENT_TYPES = [
  { value: "Incubator", label: "Incubator" },
  { value: "Autoclave", label: "Autoclave" },
  { value: "LafCabinet", label: "LAF Cabinet" },
  { value: "BiologicalSafetyCabinet", label: "Biological Safety Cabinet" },
  { value: "WaterBath", label: "Water Bath" },
  { value: "Hplc", label: "HPLC" },
  { value: "PhMeter", label: "pH Meter" },
  { value: "Balance", label: "Balance" },
  { value: "Other", label: "Other" }
];

export const CDS_SOFTWARE_OPTIONS = [
  { value: "ShimadzuLabSolutions", label: "Shimadzu LabSolutions" },
  { value: "AgilentOpenLab", label: "Agilent OpenLab" },
  { value: "WatersEmpower3", label: "Waters Empower 3" }
];

export const formatEquipmentType = (type: string | number): string => {
  const map: Record<string, string> = {
    Incubator: "Incubator",
    "0": "Incubator",
    Autoclave: "Autoclave",
    "1": "Autoclave",
    LafCabinet: "LAF Cabinet",
    "2": "LAF Cabinet",
    BiologicalSafetyCabinet: "Biological Safety Cabinet",
    "3": "Biological Safety Cabinet",
    WaterBath: "Water Bath",
    "4": "Water Bath",
    Other: "Other",
    "5": "Other",
    Hplc: "HPLC",
    "6": "HPLC",
    PhMeter: "pH Meter",
    "7": "pH Meter",
    Balance: "Balance",
    "8": "Balance"
  };
  return map[String(type)] || String(type);
};

export const normalizeEquipmentTypeValue = (type: string | number): string => {
  const map: Record<string, string> = {
    "0": "Incubator",
    "1": "Autoclave",
    "2": "LafCabinet",
    "3": "BiologicalSafetyCabinet",
    "4": "WaterBath",
    "5": "Other",
    "6": "Hplc",
    "7": "PhMeter",
    "8": "Balance"
  };
  return map[String(type)] || String(type);
};

export const formatCdsSoftware = (cds?: string | number | null): string => {
  if (!cds) return "—";
  const map: Record<string, string> = {
    ShimadzuLabSolutions: "Shimadzu LabSolutions",
    "0": "Shimadzu LabSolutions",
    AgilentOpenLab: "Agilent OpenLab",
    "1": "Agilent OpenLab",
    WatersEmpower3: "Waters Empower 3",
    "2": "Waters Empower 3"
  };
  return map[String(cds)] || String(cds);
};

export function EquipmentPage() {
  const theme = useTheme();
  const { sectionName } = useLaboratorySections();
  const [activeTab, setActiveTab] = useState(0);
  const [summaryList, setSummaryList] = useState<ConfiguredEquipmentSummary[]>([]);
  const [inventoryList, setInventoryList] = useState<any[]>([]);
  const [allPrograms, setAllPrograms] = useState<AutoclaveProgram[]>([]);
  const [selectedEqId, setSelectedEqId] = useState<number | null>(null);

  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("All");

  // Create / Edit Master Equipment Dialog State
  const [equipmentDialogOpen, setEquipmentDialogOpen] = useState(false);
  const [isEditingEquipment, setIsEditingEquipment] = useState(false);
  const [equipmentForm, setEquipmentForm] = useState({
    name: "",
    code: "",
    type: "Hplc",
    location: "",
    vendor: "",
    cdsSoftware: "",
    connectionSettings: "",
    setPointTemperature: "",
    calibrationDueDate: ""
  });
  const [mySections, setMySections] = useState<LaboratorySection[]>([]);
  const [selectedSectionId, setSelectedSectionId] = useState<number | "">("");
  const [dialogEquipmentError, setDialogEquipmentError] = useState<string | null>(null);

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
      const [summaryData, masterData, invData, progData] = await Promise.all([
        EquipmentConfigurationService.getConfiguredSummary(),
        EquipmentConfigurationService.getEquipmentList(),
        EquipmentInventoryService.getAll(),
        EquipmentConfigurationService.getAutoclavePrograms()
      ]);

      const normalizedSummary = Array.isArray(summaryData) ? [...summaryData] : [];
      const masterList = Array.isArray(masterData) ? masterData : [];

      const summaryMap = new Map<number, ConfiguredEquipmentSummary>();
      for (const s of normalizedSummary) {
        summaryMap.set(s.id, { ...s });
      }

      for (const m of masterList) {
        const existing = summaryMap.get(m.id);
        if (existing) {
          existing.vendor = m.vendor ?? existing.vendor ?? null;
          existing.cdsSoftware = m.cdsSoftware ?? existing.cdsSoftware ?? null;
          existing.connectionSettings = m.connectionSettings ?? existing.connectionSettings ?? null;
          existing.sectionId = m.sectionId ?? existing.sectionId ?? null;
          existing.section = m.section ?? existing.section ?? null;
          existing.location = existing.location || m.location || null;
          summaryMap.set(m.id, existing);
        } else {
          summaryMap.set(m.id, {
            id: m.id,
            name: m.name,
            code: m.code,
            type: m.type,
            location: m.location ?? null,
            setPointTemperature: m.setPointTemperature ?? null,
            calibrationDueDate: m.calibrationDueDate ?? null,
            equipmentInventoryId: null,
            inventoryStatus: "InService",
            inventoryLocation: m.location ?? null,
            configuredProgramCount: 0,
            serialNumber: null,
            manufacturerName: m.vendor ?? null,
            vendor: m.vendor ?? null,
            cdsSoftware: m.cdsSoftware ?? null,
            connectionSettings: m.connectionSettings ?? null,
            sectionId: m.sectionId ?? null,
            section: m.section ?? null
          });
        }
      }

      const mergedList = Array.from(summaryMap.values());
      setSummaryList(mergedList);
      setInventoryList(Array.isArray(invData) ? invData : []);
      setAllPrograms(Array.isArray(progData) ? progData : []);

      if (mergedList.length > 0 && selectedEqId === null) {
        setSelectedEqId(mergedList[0].id);
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
      toast.success(newStatus ? "Program activated" : "Program deactivated");
    } catch (err: any) {
      toast.error(err?.response?.data?.message ?? err?.message ?? "Status update failed.");
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

  // Inventory holds the asset identity; the lab-configuration record also
  // needs a type, a laboratory section and (for HPLC) the CDS software, so
  // "Configure for Lab" opens the equipment form pre-filled from inventory
  // instead of guessing those.
  const handleConfigureInventoryEquipment = async (inv: any) => {
    const text = `${inv.instrumentType ?? ""}`.toLowerCase();
    const guessedType =
      /h[pb]lc|chromatograph/.test(text) ? "Hplc"
      : text.includes("incubator") ? "Incubator"
      : text.includes("autoclave") ? "Autoclave"
      : text.includes("cabinet") ? "LafCabinet"
      : text.includes("balance") ? "Balance"
      : /(^|[^a-z])ph([^a-z]|$)/.test(text) ? "PhMeter"
      : "Other";
    await handleOpenAddEquipment({
      name: inv.instrumentType ?? "",
      code: inv.code ?? "",
      type: guessedType,
      location: inv.location ?? "",
      vendor: inv.manufacturerName ?? "",
      calibrationDueDate: inv.calibrationDueDate ? String(inv.calibrationDueDate).slice(0, 10) : ""
    });
    setInventoryDialogOpen(false);
  };

  const handleOpenAddEquipment = async (prefill?: Partial<typeof equipmentForm>) => {
    setIsEditingEquipment(false);
    setEquipmentForm({
      name: "",
      code: "",
      type: "Hplc",
      location: "",
      vendor: "",
      cdsSoftware: "",
      connectionSettings: "",
      setPointTemperature: "",
      calibrationDueDate: "",
      ...prefill
    });
    setDialogEquipmentError(null);
    try {
      const secs = await getMySections();
      const validSecs = Array.isArray(secs) ? secs : [];
      setMySections(validSecs);
      if (validSecs.length === 1) {
        setSelectedSectionId(validSecs[0].sectionId);
      } else {
        setSelectedSectionId("");
      }
    } catch {
      setMySections([]);
      setSelectedSectionId("");
    }
    setEquipmentDialogOpen(true);
  };

  const handleOpenEditEquipment = (eq: ConfiguredEquipmentSummary) => {
    setIsEditingEquipment(true);
    setEquipmentForm({
      name: eq.name,
      code: eq.code,
      type: normalizeEquipmentTypeValue(eq.type),
      location: eq.location || eq.inventoryLocation || "",
      vendor: eq.vendor || eq.manufacturerName || "",
      cdsSoftware: eq.cdsSoftware ? String(eq.cdsSoftware) : "",
      connectionSettings: eq.connectionSettings || "",
      setPointTemperature: eq.setPointTemperature != null ? String(eq.setPointTemperature) : "",
      calibrationDueDate: eq.calibrationDueDate ? eq.calibrationDueDate.slice(0, 10) : ""
    });
    setSelectedSectionId(eq.sectionId ?? "");
    setDialogEquipmentError(null);
    setEquipmentDialogOpen(true);
  };

  const handleSaveEquipment = async () => {
    if (!equipmentForm.name.trim() || !equipmentForm.code.trim() || !equipmentForm.type) {
      setDialogEquipmentError("Name, Code, and Type are required.");
      return;
    }

    if (equipmentForm.type === "Hplc" && !equipmentForm.cdsSoftware) {
      setDialogEquipmentError("CDS Software is required for HPLC equipment.");
      return;
    }

    if (!isEditingEquipment && mySections.length > 1 && !selectedSectionId) {
      setDialogEquipmentError("Laboratory section is required.");
      return;
    }

    setSaving(true);
    setDialogEquipmentError(null);
    try {
      const payload: any = {
        name: equipmentForm.name.trim(),
        code: equipmentForm.code.trim(),
        type: equipmentForm.type,
        location: equipmentForm.location.trim() || null,
        vendor: equipmentForm.vendor.trim() || null,
        cdsSoftware: equipmentForm.type === "Hplc" && equipmentForm.cdsSoftware ? equipmentForm.cdsSoftware : null,
        connectionSettings: equipmentForm.connectionSettings.trim() || null,
        setPointTemperature: equipmentForm.setPointTemperature !== "" ? Number(equipmentForm.setPointTemperature) : null,
        calibrationDueDate: equipmentForm.calibrationDueDate || null,
        ...(!isEditingEquipment && mySections.length > 1 && selectedSectionId !== "" ? { sectionId: Number(selectedSectionId) } : {})
      };

      if (isEditingEquipment && selectedEquipment) {
        await EquipmentConfigurationService.updateEquipment(selectedEquipment.id, payload);
        toast.success("Equipment updated successfully.");
      } else {
        const created = await EquipmentConfigurationService.createEquipment(payload);
        toast.success("Equipment created successfully.");
        if (created?.id) {
          setSelectedEqId(created.id);
        }
      }
      setEquipmentDialogOpen(false);
      await loadData();
    } catch (err: any) {
      setDialogEquipmentError(err?.response?.data?.error ?? err?.response?.data?.message ?? err?.message ?? "Could not save equipment.");
    } finally {
      setSaving(false);
    }
  };

  // Filters
  const filteredSummary = safeSummaryList.filter((e) => {
    const matchType =
      typeFilter === "All"
        ? true
        : normalizeEquipmentTypeValue(e.type).toLowerCase() === typeFilter.toLowerCase();
    const matchSearch =
      search === "" ||
      (e.code && e.code.toLowerCase().includes(search.toLowerCase())) ||
      (e.name && e.name.toLowerCase().includes(search.toLowerCase())) ||
      (e.vendor && e.vendor.toLowerCase().includes(search.toLowerCase()));
    return matchType && matchSearch;
  });

  const safeAllPrograms = Array.isArray(allPrograms) ? allPrograms : [];
  const autoclaveProgramsForSelected = selectedEqId !== null
    ? safeAllPrograms.filter((p) => p.equipmentId === selectedEqId)
    : [];

  const safeSetPointHistory = Array.isArray(setPointHistory) ? setPointHistory : [];

  return (
    <>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2, flexWrap: "wrap", gap: 1 }}>
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
            onClick={() => handleOpenAddEquipment()}
            color="primary"
          >
            Add Equipment
          </Button>
          <Button
            variant="outlined"
            onClick={() => setInventoryDialogOpen(true)}
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
                    {EQUIPMENT_TYPES.map((t) => (
                      <MenuItem key={t.value} value={t.value}>
                        {t.label}
                      </MenuItem>
                    ))}
                  </Select>
                </Stack>

                <Divider sx={{ mb: 2 }} />

                <Stack spacing={1} sx={{ maxHeight: 600, overflowY: "auto" }}>
                  {filteredSummary.map((eq) => {
                    const isSelected = eq.id === selectedEqId;
                    const isIncubator = eq.type === "Incubator" || eq.type === 0;
                    const isAutoclave = eq.type === "Autoclave" || eq.type === 1;
                    const isHplc = normalizeEquipmentTypeValue(eq.type) === "Hplc";
                    const typeLabel = formatEquipmentType(eq.type);
                    const sectionLabel = eq.section?.name || eq.section?.sectionName || sectionName(eq.sectionId) || "—";

                    return (
                      <Box
                        key={eq.id}
                        onClick={() => setSelectedEqId(eq.id)}
                        sx={{
                          p: 1.5,
                          borderRadius: 1.5,
                          border: 1,
                          borderColor: isSelected ? "primary.main" : "divider",
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
                            label={typeLabel}
                            color={isIncubator ? "primary" : isAutoclave ? "secondary" : isHplc ? "info" : "default"}
                            variant="outlined"
                            sx={{ height: 20, fontSize: 11 }}
                          />
                        </Box>
                        <Typography
                          variant="body2"
                          sx={{
                            color: "text.secondary",
                            fontSize: 12
                          }}>
                          {eq.name}
                        </Typography>
                        <Typography
                          variant="caption"
                          sx={{
                            color: "text.secondary",
                            display: "block",
                            fontSize: 11,
                            mt: 0.5
                          }}>
                          Section: {sectionLabel}
                        </Typography>
                        {eq.vendor && (
                          <Typography
                            variant="caption"
                            sx={{
                              color: "text.secondary",
                              display: "block",
                              fontSize: 11
                            }}>
                            Vendor: {eq.vendor}
                          </Typography>
                        )}

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
                    <Typography
                      variant="body2"
                      sx={{
                        color: "text.secondary",
                        textAlign: "center",
                        py: 4
                      }}>
                      No equipment configured for this laboratory.
                    </Typography>
                  )}
                  {safeSummaryList.length > 0 && filteredSummary.length === 0 && (
                    <Typography
                      variant="body2"
                      sx={{
                        color: "text.secondary",
                        textAlign: "center",
                        py: 4
                      }}>
                      No equipment matching search filter.
                    </Typography>
                  )}
                </Stack>
              </Paper>

              {/* Right Panel: Selected Equipment Details & Configuration */}
              {selectedEquipment ? (
                <Stack spacing={3}>
                  {/* Card 1: Master Inventory Information & Configuration */}
                  <Paper sx={{ p: 2.5 }}>
                    <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2, flexWrap: "wrap", gap: 1 }}>
                      <Box>
                        <Typography sx={{ fontSize: 20, fontWeight: 700, color: theme.palette.primary.main }}>
                          {selectedEquipment.code} — {selectedEquipment.name}
                        </Typography>
                        <Typography variant="body2" sx={{
                          color: "text.secondary"
                        }}>
                          Equipment Configuration Details
                        </Typography>
                      </Box>
                      <Stack direction="row" spacing={1}>
                        <Button
                          variant="contained"
                          size="small"
                          startIcon={<EditIcon />}
                          onClick={() => handleOpenEditEquipment(selectedEquipment)}
                          color="primary"
                        >
                          Edit Equipment
                        </Button>
                        <Button
                          component={Link}
                          to="/inventory/equipment"
                          variant="outlined"
                          size="small"
                          startIcon={<OpenInNewIcon />}
                        >
                          View in Inventory
                        </Button>
                      </Stack>
                    </Box>

                    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(3, 1fr)", md: "repeat(4, 1fr)" }, gap: 2 }}>
                      <Box>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>Equipment Type</Typography>
                        <Typography variant="body2" sx={{
                          fontWeight: 600
                        }}>{formatEquipmentType(selectedEquipment.type)}</Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>Laboratory Section</Typography>
                        <Typography variant="body2" sx={{
                          fontWeight: 600
                        }}>
                          {selectedEquipment.section?.name || selectedEquipment.section?.sectionName || sectionName(selectedEquipment.sectionId) || "—"}
                        </Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>Vendor / Manufacturer</Typography>
                        <Typography variant="body2" sx={{
                          fontWeight: 600
                        }}>
                          {selectedEquipment.vendor || selectedEquipment.manufacturerName || "—"}
                        </Typography>
                      </Box>
                      {normalizeEquipmentTypeValue(selectedEquipment.type) === "Hplc" && (
                        <Box>
                          <Typography variant="caption" sx={{
                            color: "text.secondary"
                          }}>CDS Software</Typography>
                          <Typography variant="body2" sx={{
                            fontWeight: 700,
                            color: "primary.main"
                          }}>
                            {formatCdsSoftware(selectedEquipment.cdsSoftware)}
                          </Typography>
                        </Box>
                      )}
                      <Box>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>Serial Number</Typography>
                        <Typography
                          variant="body2"
                          sx={{
                            fontWeight: 700,
                            color: theme.palette.primary.main
                          }}>
                          {selectedEquipment.serialNumber || "—"}
                        </Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>Location</Typography>
                        <Typography variant="body2" sx={{
                          fontWeight: 600
                        }}>{selectedEquipment.inventoryLocation || selectedEquipment.location || "—"}</Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>Calibration Due Date</Typography>
                        <Typography variant="body2" sx={{
                          fontWeight: 600
                        }}>
                          {selectedEquipment.calibrationDueDate ? new Date(selectedEquipment.calibrationDueDate).toLocaleDateString() : "—"}
                        </Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>Operational Status</Typography>
                        <Typography variant="body2" sx={{
                          fontWeight: 600
                        }}>{selectedEquipment.inventoryStatus || "In Service"}</Typography>
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
                            <Typography variant="caption" sx={{
                              color: "text.secondary"
                            }}>Current Set Point Temperature</Typography>
                            <Typography sx={{ fontSize: 26, fontWeight: 800, color: theme.palette.primary.main }}>
                              {selectedEquipment.setPointTemperature ? `${selectedEquipment.setPointTemperature} °C` : "Not Configured"}
                            </Typography>
                          </Box>
                        </Box>
                        <Button
                          variant="contained"
                          startIcon={<EditIcon />}
                          onClick={handleOpenEditSetPoint}
                          color="primary"
                        >
                          Edit Set Point
                        </Button>
                      </Box>

                      <Typography sx={{ fontSize: 14, fontWeight: 700, mb: 1.5 }}>
                        Set Point Change History
                      </Typography>

                      <Table size="small">
                        <TableHead>
                          <TableRow sx={tableHeadSx}>
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
                          color="primary"
                        >
                          Add Program / Load
                        </Button>
                      </Box>

                      <Table size="small">
                        <TableHead>
                          <TableRow sx={tableHeadSx}>
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

                  {normalizeEquipmentTypeValue(selectedEquipment.type) === "Hplc" && (
                    <Paper sx={{ p: 2.5 }}>
                      <SectionTitle>HPLC Chromatography System Configuration</SectionTitle>
                      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" }, gap: 2, mt: 1 }}>
                        <Box>
                          <Typography variant="caption" sx={{ color: "text.secondary" }}>CDS Software Package</Typography>
                          <Typography variant="body1" sx={{ fontWeight: 700, color: "primary.main" }}>
                            {formatCdsSoftware(selectedEquipment.cdsSoftware)}
                          </Typography>
                        </Box>
                        <Box>
                          <Typography variant="caption" sx={{ color: "text.secondary" }}>Vendor / Instrument Model</Typography>
                          <Typography variant="body1" sx={{ fontWeight: 600 }}>
                            {selectedEquipment.vendor || selectedEquipment.manufacturerName || "—"}
                          </Typography>
                        </Box>
                        <Box>
                          <Typography variant="caption" sx={{ color: "text.secondary" }}>Connection Settings</Typography>
                          <Typography variant="body1" sx={{ fontWeight: 600, fontFamily: "monospace" }}>
                            {selectedEquipment.connectionSettings || "—"}
                          </Typography>
                        </Box>
                      </Box>
                    </Paper>
                  )}

                  {selectedEquipment.type !== "Incubator" && selectedEquipment.type !== 0 && selectedEquipment.type !== "Autoclave" && selectedEquipment.type !== 1 && normalizeEquipmentTypeValue(selectedEquipment.type) !== "Hplc" && (
                    <Paper sx={{ p: 2.5 }}>
                      <SectionTitle>Equipment Configuration</SectionTitle>
                      <Typography variant="body2" sx={{
                        color: "text.secondary"
                      }}>
                        This equipment item is configured for general laboratory usage. Additional specific operational parameters may be set under Master Data.
                      </Typography>
                    </Paper>
                  )}
                </Stack>
              ) : (
                <Paper sx={{ p: 4, textAlign: "center" }}>
                  <Typography sx={{
                    color: "text.secondary"
                  }}>
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
                  <TableRow sx={tableHeadSx}>
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
              <Typography
                variant="body2"
                sx={{
                  color: "text.secondary",
                  mb: 2
                }}>
                Complete audit trail of incubator set point changes and autoclave program configuration events.
              </Typography>
              <Table size="small">
                <TableHead>
                  <TableRow sx={tableHeadSx}>
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

      {/* DIALOG 0: ADD / EDIT MASTER EQUIPMENT */}
      <FloatingDialog
        open={equipmentDialogOpen}
        onClose={() => setEquipmentDialogOpen(false)}
        maxWidth="sm"
        titleSx={{ fontWeight: 700 }}
        title={isEditingEquipment ? "Edit Equipment" : "Add Equipment"}
        actions={
          <>
            <Button onClick={() => setEquipmentDialogOpen(false)} variant="outlined" disabled={saving}>
              Cancel
            </Button>
            <Button
              onClick={handleSaveEquipment}
              variant="contained"
              disabled={saving}
              color="primary"
            >
              {saving ? "Saving…" : isEditingEquipment ? "Save Changes" : "Create Equipment"}
            </Button>
          </>
        }
      >
        {dialogEquipmentError && <Alert severity="error" sx={{ mb: 2 }}>{dialogEquipmentError}</Alert>}
        <Stack spacing={2}>
          <TextField
            label="Equipment Name *"
            placeholder="e.g. HPLC System 01"
            value={equipmentForm.name}
            onChange={(e) => setEquipmentForm({ ...equipmentForm, name: e.target.value })}
            fullWidth
            required
          />
          <TextField
            label="Equipment Code *"
            placeholder="e.g. HPLC-001"
            value={equipmentForm.code}
            onChange={(e) => setEquipmentForm({ ...equipmentForm, code: e.target.value })}
            fullWidth
            required
          />
          <FormControl fullWidth required size="small">
            <InputLabel id="equipment-type-select-label">Equipment Type *</InputLabel>
            <Select
              labelId="equipment-type-select-label"
              label="Equipment Type *"
              value={equipmentForm.type}
              onChange={(e) => {
                const nextType = e.target.value;
                setEquipmentForm({
                  ...equipmentForm,
                  type: nextType,
                  cdsSoftware: nextType === "Hplc" ? equipmentForm.cdsSoftware : ""
                });
              }}
            >
              {EQUIPMENT_TYPES.map((t) => (
                <MenuItem key={t.value} value={t.value}>
                  {t.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          {equipmentForm.type === "Hplc" && (
            <FormControl fullWidth required size="small">
              <InputLabel id="cds-software-select-label">CDS Software *</InputLabel>
              <Select
                labelId="cds-software-select-label"
                label="CDS Software *"
                value={equipmentForm.cdsSoftware}
                onChange={(e) => setEquipmentForm({ ...equipmentForm, cdsSoftware: e.target.value })}
              >
                <MenuItem value="">
                  <em>Select CDS Software…</em>
                </MenuItem>
                {CDS_SOFTWARE_OPTIONS.map((opt) => (
                  <MenuItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}

          <TextField
            label="Vendor / Manufacturer"
            placeholder="e.g. Shimadzu, Agilent, Waters"
            value={equipmentForm.vendor}
            onChange={(e) => setEquipmentForm({ ...equipmentForm, vendor: e.target.value })}
            fullWidth
            slotProps={{
              htmlInput: { maxLength: 100 }
            }}
          />

          {!isEditingEquipment && mySections.length > 1 && (
            <FormControl size="small" fullWidth required>
              <InputLabel id="equipment-section-label">Laboratory Section *</InputLabel>
              <Select<number | "">
                labelId="equipment-section-label"
                label="Laboratory Section *"
                value={selectedSectionId}
                onChange={(e) => setSelectedSectionId(e.target.value === "" ? "" : Number(e.target.value))}
              >
                <MenuItem value="">
                  <em>Select Laboratory Section…</em>
                </MenuItem>
                {mySections.map((s) => (
                  <MenuItem key={s.sectionId} value={s.sectionId}>
                    {s.sectionName} ({s.departmentName})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}

          <TextField
            label="Location"
            placeholder="e.g. Instrumental Analysis Lab"
            value={equipmentForm.location}
            onChange={(e) => setEquipmentForm({ ...equipmentForm, location: e.target.value })}
            fullWidth
          />

          <TextField
            label="Connection Settings (optional)"
            placeholder="e.g. COM3 / 192.168.1.50"
            value={equipmentForm.connectionSettings}
            onChange={(e) => setEquipmentForm({ ...equipmentForm, connectionSettings: e.target.value })}
            fullWidth
          />

          <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2 }}>
            <TextField
              label="Set Point Temp (°C)"
              type="number"
              placeholder="e.g. 37.0"
              value={equipmentForm.setPointTemperature}
              onChange={(e) => setEquipmentForm({ ...equipmentForm, setPointTemperature: e.target.value })}
              slotProps={{ htmlInput: { step: "0.1" } }}
            />
            <TextField
              label="Calibration Due Date"
              type="date"
              value={equipmentForm.calibrationDueDate}
              onChange={(e) => setEquipmentForm({ ...equipmentForm, calibrationDueDate: e.target.value })}
              slotProps={{ inputLabel: { shrink: true } }}
            />
          </Box>
        </Stack>
      </FloatingDialog>

      {/* DIALOG 1: EDIT INCUBATOR SET POINT */}
      <FloatingDialog
        open={editSetPointDialogOpen}
        onClose={() => setEditSetPointDialogOpen(false)}
        maxWidth="sm"
        titleSx={{ fontWeight: 700 }}
        title="Edit Incubator Set Point"
        actions={
          <>
            <Button onClick={() => setEditSetPointDialogOpen(false)} variant="outlined" disabled={saving}>
              Cancel
            </Button>
            <Button
              onClick={handleSaveSetPoint}
              variant="contained"
              disabled={saving}
              color="primary"
            >
              {saving ? "Saving…" : "Save Changes"}
            </Button>
          </>
        }
      >
        {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}
        <Stack spacing={2}>
          <Box>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>Equipment</Typography>
            <Typography variant="body1" sx={{
              fontWeight: 700
            }}>
              {selectedEquipment?.code} — {selectedEquipment?.name}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>Current Set Point</Typography>
            <Typography
              variant="body1"
              sx={{
                fontWeight: 700,
                color: "primary.main"
              }}>
              {selectedEquipment?.setPointTemperature ? `${selectedEquipment.setPointTemperature} °C` : "Not Configured"}
            </Typography>
          </Box>
          <TextField
            label="New Set Point (°C) *"
            type="number"
            value={newSetPoint}
            onChange={(e) => setNewSetPoint(e.target.value)}
            fullWidth
            slotProps={{
              htmlInput: { step: "0.1" }
            }}
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
      </FloatingDialog>

      {/* DIALOG 2: ADD / EDIT AUTOCLAVE PROGRAM */}
      <FloatingDialog
        open={programDialogOpen}
        onClose={() => setProgramDialogOpen(false)}
        maxWidth="sm"
        titleSx={{ fontWeight: 700 }}
        title={programForm.id ? "Edit Autoclave Program / Load" : "Add Autoclave Program / Load"}
        actions={
          <>
            <Button onClick={() => setProgramDialogOpen(false)} variant="outlined" disabled={saving}>
              Cancel
            </Button>
            <Button
              onClick={handleSaveProgram}
              variant="contained"
              disabled={saving}
              color="primary"
            >
              {saving ? "Saving…" : "Save Program"}
            </Button>
          </>
        }
      >
        {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}
        <Stack spacing={2}>
          <Box>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>Autoclave Equipment</Typography>
            <Typography variant="body1" sx={{
              fontWeight: 700
            }}>
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
      </FloatingDialog>

      {/* DIALOG 3: AUTOCLAVE PROGRAM HISTORY */}
      <FloatingDialog
        open={programHistoryDialogOpen}
        onClose={() => setProgramHistoryDialogOpen(false)}
        maxWidth="md"
        titleSx={{ fontWeight: 700 }}
        title={`Program History — ${selectedProgramCode}`}
        actions={
          <Button onClick={() => setProgramHistoryDialogOpen(false)} variant="contained">
            Close
          </Button>
        }
      >
          <Table size="small">
            <TableHead>
              <TableRow sx={tableHeadSx}>
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
      </FloatingDialog>

      {/* DIALOG 4: SELECT FROM INVENTORY */}
      <FloatingDialog
        open={inventoryDialogOpen}
        onClose={() => setInventoryDialogOpen(false)}
        maxWidth="md"
        titleSx={{ fontWeight: 700 }}
        title="Select Equipment from Master Inventory"
        actions={
          <Button onClick={() => setInventoryDialogOpen(false)} variant="outlined">
            Close
          </Button>
        }
      >
          <Typography
            variant="body2"
            sx={{
              color: "text.secondary",
              mb: 2
            }}>
            Register physical equipment from Inventory into Laboratory Configuration. Master equipment identity and calibration remain managed by Inventory.
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow sx={tableHeadSx}>
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
                        onClick={() => handleConfigureInventoryEquipment(inv)}
                      >
                        {isAlreadyLinked ? "Configured" : "Configure for Lab"}
                      </Button>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
      </FloatingDialog>
    </>
  );
}
