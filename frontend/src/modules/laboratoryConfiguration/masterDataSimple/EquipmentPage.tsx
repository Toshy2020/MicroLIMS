import { useEffect, useMemo, useState } from "react";
import type { TFunction } from "i18next";
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
  Alert,
  IconButton,
  Tooltip,
  Divider,
  Stack,
  CircularProgress,
  useTheme,
  TableContainer
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import { FieldHint, hintDescribedBy } from "../../../components/FieldHint";
import { getEquipmentHints } from "./hints";
import { i18n, useTranslation } from "../../../i18n";
import HistoryIcon from "@mui/icons-material/History";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import AddIcon from "@mui/icons-material/Add";
import ContentPasteIcon from "@mui/icons-material/ContentPaste";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import ThermostatIcon from "@mui/icons-material/Thermostat";
import RefreshIcon from "@mui/icons-material/Refresh";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { tableHeadSx } from "../../../theme";
import { toast } from "sonner";
import { usePersistedTab } from "../../../hooks/usePersistedTab";
import { Link } from "react-router-dom";
import {
  EquipmentConfigurationService,
  ConfiguredEquipmentSummary,
  IncubatorSetPointHistory,
  AutoclaveProgram,
  AutoclaveProgramHistory,
  SaveAutoclaveProgramRequest
} from "./services/EquipmentConfigurationService";
import { EquipmentInventoryService } from "../../inventory/equipment/services/EquipmentInventoryService";
import type { EquipmentItem } from "../../inventory/equipment/types/equipmentTypes";
import { useLaboratorySections } from "../../../hooks/useLaboratorySections";
import { getMySections, LaboratorySection } from "../../../services/laboratorySectionService";
import { clickable } from "../../../utils/clickable";
import { EquipmentPasteSection } from "./EquipmentPasteSection";
import {
  EquipmentFormModel,
  normalizeEquipmentTypeValue
} from "./equipmentWizard/equipmentFormModel";
import { EquipmentWizard } from "./equipmentWizard/EquipmentWizard";
import { FP_ONLY_EQUIPMENT_TYPES, MICRO_EQUIPMENT_TYPES } from "./equipmentWizard/equipmentOptions";
import { formatDateTimeWithTimeZone, formatLabCalendarDate } from "../../../utils/formatDate";

interface ApiErrorShape {
  response?: { data?: { error?: string; message?: string } };
  message?: string;
}

const asApiError = (err: unknown): ApiErrorShape => (err ?? {}) as ApiErrorShape;

export {
  EQUIPMENT_TYPES,
  FP_ONLY_EQUIPMENT_TYPES,
  MICRO_EQUIPMENT_TYPES,
  CDS_SOFTWARE_OPTIONS
} from "./equipmentWizard/equipmentOptions";

const defaultT = (): TFunction => i18n.getFixedT("en", "masterData");

export const formatEquipmentType = (type: string | number, t: TFunction = defaultT()): string => {
  const keyMap: Record<string, string> = {
    Incubator: "Incubator",
    "0": "Incubator",
    Autoclave: "Autoclave",
    "1": "Autoclave",
    LafCabinet: "LafCabinet",
    "2": "LafCabinet",
    BiologicalSafetyCabinet: "BiologicalSafetyCabinet",
    "3": "BiologicalSafetyCabinet",
    WaterBath: "WaterBath",
    "4": "WaterBath",
    Other: "Other",
    "5": "Other",
    Hplc: "Hplc",
    "6": "Hplc",
    PhMeter: "PhMeter",
    "7": "PhMeter",
    Balance: "Balance",
    "8": "Balance",
    IcpOes: "IcpOes",
    "9": "IcpOes"
  };
  const key = keyMap[String(type)];
  return key ? t(`equipmentTypes.${key}`) : String(type);
};

export { normalizeEquipmentTypeValue };

export const formatCdsSoftware = (cds?: string | number | null, t: TFunction = defaultT()): string => {
  if (!cds) return t("common.dash");
  const map: Record<string, string> = {
    ShimadzuLabSolutions: "Shimadzu LabSolutions",
    "0": "Shimadzu LabSolutions",
    AgilentOpenLab: "Agilent OpenLab",
    "1": "Agilent OpenLab",
    WatersEmpower3: "Waters Empower 3",
    "2": "Waters Empower 3",
    PerkinElmerSyngistix: "PerkinElmer Syngistix",
    "3": "PerkinElmer Syngistix"
  };
  return map[String(cds)] || String(cds);
};

const EQUIPMENT_TABS = ["configured", "programs", "history"] as const;

export function EquipmentPage() {
  const { t } = useTranslation("masterData");
  const hints = useMemo(() => getEquipmentHints(t), [t]);
  const theme = useTheme();
  const { sectionName } = useLaboratorySections();
  // The tab used last time reopens (state key nav.tab.equipmentConfiguration).
  const [tabKey, setTabKey] = usePersistedTab("equipmentConfiguration", EQUIPMENT_TABS);
  const activeTab = EQUIPMENT_TABS.indexOf(tabKey);
  const [summaryList, setSummaryList] = useState<ConfiguredEquipmentSummary[]>([]);
  const [inventoryList, setInventoryList] = useState<EquipmentItem[]>([]);
  const [allPrograms, setAllPrograms] = useState<AutoclaveProgram[]>([]);
  const [selectedEqId, setSelectedEqId] = useState<number | null>(null);

  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("All");

  // Create / Edit Master Equipment Dialog State
  const [wizard, setWizard] = useState<
    { kind: "create"; prefill?: Partial<EquipmentFormModel> } | { kind: "edit" } | { kind: "copy" } | null
  >(null);
  const [mySections, setMySections] = useState<LaboratorySection[]>([]);

  // Incubator Edit State
  const [editSetPointDialogOpen, setEditSetPointDialogOpen] = useState(false);
  const [newSetPoint, setNewSetPoint] = useState("");
  const [setPointReason, setSetPointReason] = useState("");
  const [setPointHistory, setSetPointHistory] = useState<IncubatorSetPointHistory[]>([]);
  // Kept apart from an empty history: "no set-point changes" is a GMP claim.
  const [setPointHistoryFailed, setSetPointHistoryFailed] = useState(false);

  // Autoclave Program Edit State
  const [programDialogOpen, setProgramDialogOpen] = useState(false);
  const [programForm, setProgramForm] = useState<Partial<SaveAutoclaveProgramRequest>>({});
  const [programHistoryDialogOpen, setProgramHistoryDialogOpen] = useState(false);
  const [programHistory, setProgramHistory] = useState<AutoclaveProgramHistory[]>([]);
  const [selectedProgramCode, setSelectedProgramCode] = useState("");

  // Select from Inventory Dialog
  const [inventoryDialogOpen, setInventoryDialogOpen] = useState(false);

  // Paste batch state
  const [pasteBarOpen, setPasteBarOpen] = useState(false);

  // General Loading & Error State
  const [loading, setLoading] = useState(true);
  const [pageError, setPageError] = useState<string | null>(null);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const loadData = async (signal?: AbortSignal) => {
    setLoading(true);
    setPageError(null);
    try {
      const [summaryData, masterData, invData, progData, mySecData] = await Promise.all([
        EquipmentConfigurationService.getConfiguredSummary(),
        EquipmentConfigurationService.getEquipmentList(),
        EquipmentInventoryService.getAll(signal),
        EquipmentConfigurationService.getAutoclavePrograms(),
        getMySections(signal).catch(() => [])
      ]);

      if (signal?.aborted) {
        return;
      }

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
          existing.equipmentInventoryId = m.equipmentInventoryId ?? existing.equipmentInventoryId ?? null;
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
            equipmentInventoryId: m.equipmentInventoryId ?? null,
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
      setMySections(Array.isArray(mySecData) ? mySecData : []);

      if (mergedList.length > 0 && selectedEqId === null) {
        setSelectedEqId(mergedList[0].id);
      }
    } catch (err: unknown) {
      if (signal?.aborted) {
        return;
      }
      console.error("Failed to load laboratory equipment configuration:", err);
      const message = err instanceof Error ? err.message : undefined;
      setPageError(message || t("equipmentPage.loadError"));
      setSummaryList([]);
      setInventoryList([]);
      setAllPrograms([]);
    } finally {
      if (!signal?.aborted) {
        setLoading(false);
      }
    }
  };

  useEffect(() => {
    const controller = new AbortController();
    void loadData(controller.signal);
    return () => controller.abort();
  }, []);

  const safeSummaryList = Array.isArray(summaryList) ? summaryList : [];
  const selectedEquipment = safeSummaryList.find((e) => e.id === selectedEqId);

  // Load history when selected equipment changes
  useEffect(() => {
    setSetPointHistoryFailed(false);
    if (selectedEquipment) {
      if (selectedEquipment.type === "Incubator" || selectedEquipment.type === 0) {
        EquipmentConfigurationService.getSetPointHistory(selectedEquipment.id)
          .then((history) => setSetPointHistory(Array.isArray(history) ? history : []))
          .catch(() => {
            setSetPointHistory([]);
            setSetPointHistoryFailed(true);
          });
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
      setDialogError(t("equipmentPage.incubator.invalidSetPoint"));
      return;
    }
    if (!setPointReason.trim()) {
      setDialogError(t("equipmentPage.incubator.reasonRequired"));
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
    } catch (err: unknown) {
      setDialogError(asApiError(err).response?.data?.message ?? asApiError(err).message ?? t("equipmentPage.incubator.updateError"));
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
      comment: i18n.getFixedT("en", "masterData")("equipmentPage.autoclave.initialComment")
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
      setDialogError(t("equipmentPage.autoclave.requiredFields"));
      return;
    }

    setSaving(true);
    setDialogError(null);
    try {
      await EquipmentConfigurationService.saveAutoclaveProgram(programForm.equipmentId as number, {
        id: programForm.id,
        equipmentId: programForm.equipmentId as number,
        programCode: programForm.programCode,
        programName: programForm.programName,
        loadType: programForm.loadType,
        temperature: Number(programForm.temperature),
        cycleTimeMinutes: Number(programForm.cycleTimeMinutes),
        isActive: Boolean(programForm.isActive),
        comment: programForm.comment
      }, programForm.id ? safeAllPrograms.find((p) => p.id === programForm.id)?.version : undefined);
      setProgramDialogOpen(false);
      await loadData();
    } catch (err: unknown) {
      setDialogError(asApiError(err).response?.data?.message ?? asApiError(err).message ?? t("equipmentPage.autoclave.saveError"));
    } finally {
      setSaving(false);
    }
  };

  const handleToggleProgramStatus = async (prog: AutoclaveProgram) => {
    const newStatus = !prog.isActive;
    const enT = i18n.getFixedT("en", "masterData");
    const comment = newStatus
      ? enT("equipmentPage.autoclave.activatedComment")
      : enT("equipmentPage.autoclave.deactivatedComment");
    try {
      await EquipmentConfigurationService.setAutoclaveProgramStatus(prog.id, newStatus, comment);
      await loadData();
      toast.success(newStatus ? t("equipmentPage.autoclave.activatedToast") : t("equipmentPage.autoclave.deactivatedToast"));
    } catch (err: unknown) {
      toast.error(asApiError(err).response?.data?.message ?? asApiError(err).message ?? t("equipmentPage.autoclave.statusUpdateFailed"));
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
  // needs a type and a laboratory section, so
  // "Configure for Lab" opens the equipment form pre-filled from inventory
  // instead of guessing those.
  const handleConfigureInventoryEquipment = async (inv: EquipmentItem) => {
    const text = `${inv.instrumentType ?? ""}`.toLowerCase();
    const guessedType =
      text.includes("incubator") ? "Incubator"
      : text.includes("autoclave") ? "Autoclave"
      : text.includes("cabinet") ? "LafCabinet"
      : "Other";
    await handleOpenAddEquipment({
      name: inv.instrumentType ?? "",
      code: inv.code ?? "",
      type: guessedType,
      location: inv.location ?? "",
      vendor: inv.manufacturerName ?? "",
      calibrationDueDate: inv.calibrationDueDate ? String(inv.calibrationDueDate).slice(0, 10) : "",
      equipmentInventoryId: inv.id
    });
    setInventoryDialogOpen(false);
  };

  const handleOpenAddEquipment = async (prefill?: Partial<EquipmentFormModel>) => {
    try {
      const secs = await getMySections();
      setMySections(Array.isArray(secs) ? secs : []);
    } catch {
      setMySections([]);
    }
    setWizard({ kind: "create", prefill });
  };

  const handleOpenEditEquipment = () => setWizard({ kind: "edit" });
  const handleOpenCopyEquipment = () => setWizard({ kind: "copy" });

  // Filters
  const filteredSummary = safeSummaryList.filter((e) => {
    if (FP_ONLY_EQUIPMENT_TYPES.includes(normalizeEquipmentTypeValue(e.type))) return false;
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
          title={t("equipmentPage.title")}
          subtitle={t("equipmentPage.subtitle")}
        />
        <Stack direction="row" spacing={1}>
          <IconButton aria-label={t("equipmentPage.refreshAria")} onClick={() => void loadData()} title={t("equipmentPage.refreshAria")}>
            <RefreshIcon />
          </IconButton>
          <Button
            variant="outlined"
            startIcon={<ContentPasteIcon />}
            aria-expanded={pasteBarOpen}
            onClick={() => setPasteBarOpen((v) => !v)}
          >
            {t("equipmentPage.pasteEquipment")}
          </Button>
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={() => handleOpenAddEquipment()}
            color="primary"
          >
            {t("equipmentPage.addEquipment")}
          </Button>
          <Button
            variant="outlined"
            onClick={() => setInventoryDialogOpen(true)}
          >
            {t("equipmentPage.selectFromInventory")}
          </Button>
        </Stack>
      </Box>

      {pageError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {pageError}
        </Alert>
      )}

      <EquipmentPasteSection
        pasteBarOpen={pasteBarOpen}
        onClosePasteBar={() => setPasteBarOpen(false)}
        modalOpen={wizard !== null || editSetPointDialogOpen || programDialogOpen || programHistoryDialogOpen || inventoryDialogOpen}
        existingEquipment={safeSummaryList}
        mySections={mySections}
        inventoryList={inventoryList}
        onCreated={async () => {
          await loadData();
        }}
      />

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", alignItems: "center", py: 8 }}>
          <CircularProgress />
        </Box>
      ) : (
        <>
          <Paper sx={{ mb: 3 }}>
            <Tabs
              value={activeTab}
              onChange={(_, v: number) => setTabKey(EQUIPMENT_TABS[v])}
              indicatorColor="primary"
              textColor="primary"
              sx={{ borderBottom: 1, borderColor: "divider" }}
            >
              <Tab label={t("equipmentPage.tabs.configured")} />
              <Tab label={t("equipmentPage.tabs.programs", { count: safeAllPrograms.length })} />
              <Tab label={t("equipmentPage.tabs.history")} />
            </Tabs>
          </Paper>

          {/* TAB 0: CONFIGURED EQUIPMENT */}
          {activeTab === 0 && (
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "340px 1fr" }, gap: 3 }}>
              {/* Left Panel: Search & Equipment List */}
              <Paper sx={{ p: 2, height: "fit-content" }}>
                <SectionTitle>{t("equipmentPage.configured.title")}</SectionTitle>
                <Stack spacing={1.5} sx={{ mb: 2 }}>
                  <TextField
                    size="small"
                    placeholder={t("equipmentPage.configured.searchPlaceholder")}
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    slotProps={{ htmlInput: { "aria-label": t("equipmentPage.configured.searchAria") } }}
                  />
                  <Select size="small" value={typeFilter} onChange={(e) => setTypeFilter(e.target.value)} inputProps={{ "aria-label": t("equipmentPage.configured.typeFilterAria") }}>
                    <MenuItem value="All">{t("equipmentPage.configured.allTypes")}</MenuItem>
                    {MICRO_EQUIPMENT_TYPES.map((mt) => (
                      <MenuItem key={mt.value} value={mt.value}>
                        {t(`equipmentTypes.${mt.value}`, { defaultValue: mt.label })}
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
                    const typeLabel = formatEquipmentType(eq.type, t);
                    const sectionLabel = eq.section?.name || eq.section?.sectionName || sectionName(eq.sectionId) || t("common.dash");

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
                        {...clickable(() => setSelectedEqId(eq.id), { pressed: selectedEqId === eq.id })}
                      >
                        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 0.5 }}>
                          <Typography sx={{ fontWeight: 700, fontSize: 14 }}>{eq.code}</Typography>
                          <Chip
                            size="small"
                            label={typeLabel}
                            color={isIncubator ? "primary" : isAutoclave ? "secondary" : isHplc ? "info" : "default"}
                            variant="outlined"
                            sx={{ height: 20, fontSize: 12 }}
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
                            fontSize: 12,
                            mt: 0.5
                          }}>
                          {t("equipmentPage.configured.sectionLine", { section: sectionLabel })}
                        </Typography>
                        {eq.vendor && (
                          <Typography
                            variant="caption"
                            sx={{
                              color: "text.secondary",
                              display: "block",
                              fontSize: 12
                            }}>
                            {t("equipmentPage.configured.vendorLine", { vendor: eq.vendor })}
                          </Typography>
                        )}

                        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 1 }}>
                          {isIncubator && (
                            <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main }}>
                              {t("equipmentPage.configured.setPointLine", {
                                value: eq.setPointTemperature ? t("equipmentPage.tempValue", { value: eq.setPointTemperature }) : t("common.dash")
                              })}
                            </Typography>
                          )}
                          {isAutoclave && (
                            <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary" }}>
                              {t("equipmentPage.configured.programsCount", { count: eq.configuredProgramCount })}
                            </Typography>
                          )}
                          <Chip
                            size="small"
                            label={eq.inventoryStatus ?? t("common.inService")}
                            color={eq.inventoryStatus === "OutOfService" ? "error" : "success"}
                            sx={{ height: 20, fontSize: 12 }}
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
                      {t("equipmentPage.configured.noEquipment")}
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
                      {t("equipmentPage.configured.noMatch")}
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
                          {selectedEquipment.code}: {selectedEquipment.name}
                        </Typography>
                        <Typography variant="body2" sx={{
                          color: "text.secondary"
                        }}>
                          {t("equipmentPage.details.subtitle")}
                        </Typography>
                      </Box>
                      <Stack direction="row" spacing={1}>
                        <Button
                          variant="contained"
                          size="small"
                          startIcon={<EditIcon />}
                          onClick={handleOpenEditEquipment}
                          color="primary"
                        >
                          {t("equipmentPage.details.editEquipment")}
                        </Button>
                        <Button
                          variant="outlined"
                          size="small"
                          startIcon={<ContentCopyIcon />}
                          onClick={handleOpenCopyEquipment}
                        >
                          {t("equipmentPage.details.newFromExisting")}
                        </Button>
                        <Button
                          component={Link}
                          to="/inventory/equipment"
                          variant="outlined"
                          size="small"
                          startIcon={<OpenInNewIcon />}
                        >
                          {t("equipmentPage.details.viewInInventory")}
                        </Button>
                      </Stack>
                    </Box>

                    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(3, 1fr)", md: "repeat(4, 1fr)" }, gap: 2 }}>
                      <Box>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{t("equipmentPage.details.equipmentType")}</Typography>
                        <Typography variant="body2" sx={{
                          fontWeight: 600
                        }}>{formatEquipmentType(selectedEquipment.type, t)}</Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{t("equipmentPage.details.laboratorySection")}</Typography>
                        <Typography variant="body2" sx={{
                          fontWeight: 600
                        }}>
                          {selectedEquipment.section?.name || selectedEquipment.section?.sectionName || sectionName(selectedEquipment.sectionId) || t("common.dash")}
                        </Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{t("equipmentPage.details.vendorManufacturer")}</Typography>
                        <Typography variant="body2" sx={{
                          fontWeight: 600
                        }}>
                          {selectedEquipment.vendor || selectedEquipment.manufacturerName || t("common.dash")}
                        </Typography>
                      </Box>
                      {normalizeEquipmentTypeValue(selectedEquipment.type) === "Hplc" && (
                        <Box>
                          <Typography variant="caption" sx={{
                            color: "text.secondary"
                          }}>{t("equipmentPage.details.cdsSoftware")}</Typography>
                          <Typography variant="body2" sx={{
                            fontWeight: 700,
                            color: "primary.main"
                          }}>
                            {formatCdsSoftware(selectedEquipment.cdsSoftware, t)}
                          </Typography>
                        </Box>
                      )}
                      <Box>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{t("equipmentPage.details.serialNumber")}</Typography>
                        <Typography
                          variant="body2"
                          sx={{
                            fontWeight: 700,
                            color: theme.palette.primary.main
                          }}>
                          {selectedEquipment.serialNumber || t("common.dash")}
                        </Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{t("equipmentPage.details.location")}</Typography>
                        <Typography variant="body2" sx={{
                          fontWeight: 600
                        }}>{selectedEquipment.inventoryLocation || selectedEquipment.location || t("common.dash")}</Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{t("equipmentPage.details.calibrationDueDate")}</Typography>
                        <Typography variant="body2" sx={{
                          fontWeight: 600
                        }}>
                          {selectedEquipment.calibrationDueDate ? formatLabCalendarDate(selectedEquipment.calibrationDueDate) : t("common.dash")}
                        </Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{t("equipmentPage.details.operationalStatus")}</Typography>
                        <Typography variant="body2" sx={{
                          fontWeight: 600
                        }}>{selectedEquipment.inventoryStatus || t("common.inService")}</Typography>
                      </Box>
                    </Box>
                  </Paper>

                  {/* Card 2: Configuration Specifics (Incubator vs Autoclave) */}
                  {(selectedEquipment.type === "Incubator" || selectedEquipment.type === 0) && (
                    <Paper sx={{ p: 2.5 }}>
                      <SectionTitle>{t("equipmentPage.incubator.title")}</SectionTitle>

                      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", p: 2, bgcolor: "action.hover", borderRadius: 2, mb: 3 }}>
                        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
                          <ThermostatIcon sx={{ fontSize: 36, color: theme.palette.primary.main }} />
                          <Box>
                            <Typography variant="caption" sx={{
                              color: "text.secondary"
                            }}>{t("equipmentPage.incubator.currentSetPoint")}</Typography>
                            <Typography sx={{ fontSize: 26, fontWeight: 800, color: theme.palette.primary.main }}>
                              {selectedEquipment.setPointTemperature ? t("equipmentPage.tempValue", { value: selectedEquipment.setPointTemperature }) : t("equipmentPage.incubator.notConfigured")}
                            </Typography>
                          </Box>
                        </Box>
                        <Button
                          variant="contained"
                          startIcon={<EditIcon />}
                          onClick={handleOpenEditSetPoint}
                          color="primary"
                        >
                          {t("equipmentPage.incubator.editSetPoint")}
                        </Button>
                      </Box>

                      <Typography sx={{ fontSize: 14, fontWeight: 700, mb: 1.5 }}>
                        {t("equipmentPage.incubator.historyTitle")}
                      </Typography>

                      <TableContainer>
                        <Table size="small">
                          <TableHead>
                            <TableRow sx={tableHeadSx}>
                              <TableCell>{t("equipmentPage.incubator.columns.effectiveOn")}</TableCell>
                              <TableCell align="right">{t("equipmentPage.incubator.columns.previous")}</TableCell>
                              <TableCell align="right">{t("equipmentPage.incubator.columns.new")}</TableCell>
                              <TableCell>{t("equipmentPage.incubator.columns.changedBy")}</TableCell>
                              <TableCell>{t("equipmentPage.incubator.columns.reason")}</TableCell>
                            </TableRow>
                          </TableHead>
                          <TableBody>
                            {safeSetPointHistory.map((h) => (
                              <TableRow key={h.id}>
                                <TableCell>{formatDateTimeWithTimeZone(h.changedAt)}</TableCell>
                                <TableCell align="right">{t("equipmentPage.tempValue", { value: h.previousSetPoint })}</TableCell>
                                <TableCell align="right" sx={{ fontWeight: 700, color: theme.palette.primary.main }}>{t("equipmentPage.tempValue", { value: h.newSetPoint })}</TableCell>
                                <TableCell>{h.changedByName}</TableCell>
                                <TableCell>{h.reason}</TableCell>
                              </TableRow>
                            ))}
                            {safeSetPointHistory.length === 0 && (
                              <TableRow>
                                <TableCell colSpan={5} align="center" sx={{ color: setPointHistoryFailed ? "error.main" : "text.secondary", py: 3 }}>
                                  {setPointHistoryFailed
                                    ? t("equipmentPage.incubator.historyLoadError")
                                    : t("equipmentPage.incubator.noHistory")}
                                </TableCell>
                              </TableRow>
                            )}
                          </TableBody>
                        </Table>
                      </TableContainer>
                    </Paper>
                  )}

                  {(selectedEquipment.type === "Autoclave" || selectedEquipment.type === 1) && (
                    <Paper sx={{ p: 2.5 }}>
                      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
                        <SectionTitle>{t("equipmentPage.autoclave.title")}</SectionTitle>
                        <Button
                          variant="contained"
                          size="small"
                          startIcon={<AddIcon />}
                          onClick={handleOpenAddProgram}
                          color="primary"
                        >
                          {t("equipmentPage.autoclave.addProgram")}
                        </Button>
                      </Box>

                      <TableContainer>
                        <Table size="small">
                          <TableHead>
                            <TableRow sx={tableHeadSx}>
                              <TableCell>{t("equipmentPage.autoclave.columns.program")}</TableCell>
                              <TableCell>{t("equipmentPage.autoclave.columns.programName")}</TableCell>
                              <TableCell>{t("equipmentPage.autoclave.columns.loadType")}</TableCell>
                              <TableCell align="right">{t("equipmentPage.autoclave.columns.temperature")}</TableCell>
                              <TableCell align="right">{t("equipmentPage.autoclave.columns.cycleTime")}</TableCell>
                              <TableCell>{t("equipmentPage.autoclave.columns.status")}</TableCell>
                              <TableCell align="center">{t("equipmentPage.autoclave.columns.actions")}</TableCell>
                            </TableRow>
                          </TableHead>
                          <TableBody>
                            {autoclaveProgramsForSelected.map((p) => (
                              <TableRow key={p.id}>
                                <TableCell sx={{ fontWeight: 700 }}>{p.programCode}</TableCell>
                                <TableCell>{p.programName}</TableCell>
                                <TableCell>{p.loadType}</TableCell>
                                <TableCell align="right">{t("equipmentPage.tempValue", { value: p.temperature })}</TableCell>
                                <TableCell align="right">{t("equipmentPage.autoclave.minUnit", { value: p.cycleTimeMinutes })}</TableCell>
                                <TableCell>
                                  <Chip
                                    size="small"
                                    label={p.isActive ? t("common.active") : t("common.inactive")}
                                    color={p.isActive ? "success" : "default"}
                                    sx={{ height: 20, fontSize: 12 }}
                                  />
                                </TableCell>
                                <TableCell align="center">
                                  <Tooltip title={t("equipmentPage.autoclave.editProgramTooltip")}>
                                    <IconButton size="small" onClick={() => handleOpenEditProgram(p)}>
                                      <EditIcon fontSize="small" />
                                    </IconButton>
                                  </Tooltip>
                                  <Button
                                    size="small"
                                    sx={{ fontSize: 12, minWidth: 60 }}
                                    color={p.isActive ? "warning" : "success"}
                                    onClick={() => handleToggleProgramStatus(p)}
                                  >
                                    {p.isActive ? t("common.deactivate") : t("common.activate")}
                                  </Button>
                                  <Tooltip title={t("equipmentPage.autoclave.viewHistoryTooltip")}>
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
                                  {t("equipmentPage.autoclave.noPrograms")}
                                </TableCell>
                              </TableRow>
                            )}
                          </TableBody>
                        </Table>
                      </TableContainer>
                    </Paper>
                  )}

                  {normalizeEquipmentTypeValue(selectedEquipment.type) === "Hplc" && (
                    <Paper sx={{ p: 2.5 }}>
                      <SectionTitle>{t("equipmentPage.hplcSection.title")}</SectionTitle>
                      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" }, gap: 2, mt: 1 }}>
                        <Box>
                          <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("equipmentPage.hplcSection.cdsPackage")}</Typography>
                          <Typography variant="body1" sx={{ fontWeight: 700, color: "primary.main" }}>
                            {formatCdsSoftware(selectedEquipment.cdsSoftware, t)}
                          </Typography>
                        </Box>
                        <Box>
                          <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("equipmentPage.hplcSection.vendorModel")}</Typography>
                          <Typography variant="body1" sx={{ fontWeight: 600 }}>
                            {selectedEquipment.vendor || selectedEquipment.manufacturerName || t("common.dash")}
                          </Typography>
                        </Box>
                        <Box>
                          <Typography variant="caption" sx={{ color: "text.secondary" }}>{t("equipmentPage.hplcSection.connectionSettings")}</Typography>
                          <Typography variant="body1" sx={{ fontWeight: 600, fontFamily: "monospace" }}>
                            {selectedEquipment.connectionSettings || t("common.dash")}
                          </Typography>
                        </Box>
                      </Box>
                    </Paper>
                  )}

                  {selectedEquipment.type !== "Incubator" && selectedEquipment.type !== 0 && selectedEquipment.type !== "Autoclave" && selectedEquipment.type !== 1 && normalizeEquipmentTypeValue(selectedEquipment.type) !== "Hplc" && (
                    <Paper sx={{ p: 2.5 }}>
                      <SectionTitle>{t("equipmentPage.generalSection.title")}</SectionTitle>
                      <Typography variant="body2" sx={{
                        color: "text.secondary"
                      }}>
                        {t("equipmentPage.generalSection.description")}
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
                      ? t("equipmentPage.configured.noEquipment")
                      : t("equipmentPage.configured.selectPrompt")}
                  </Typography>
                </Paper>
              )}
            </Box>
          )}

          {/* TAB 1: AUTOCLAVE PROGRAMS / LOADS OVERVIEW */}
          {activeTab === 1 && (
            <Paper sx={{ p: 2.5 }}>
              <SectionTitle>{t("equipmentPage.autoclave.allProgramsTitle")}</SectionTitle>
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow sx={tableHeadSx}>
                      <TableCell>{t("equipmentPage.autoclave.allColumns.autoclave")}</TableCell>
                      <TableCell>{t("equipmentPage.autoclave.allColumns.programCode")}</TableCell>
                      <TableCell>{t("equipmentPage.autoclave.columns.programName")}</TableCell>
                      <TableCell>{t("equipmentPage.autoclave.columns.loadType")}</TableCell>
                      <TableCell align="right">{t("equipmentPage.autoclave.columns.temperature")}</TableCell>
                      <TableCell align="right">{t("equipmentPage.autoclave.columns.cycleTime")}</TableCell>
                      <TableCell>{t("equipmentPage.autoclave.columns.status")}</TableCell>
                      <TableCell align="center">{t("equipmentPage.autoclave.columns.actions")}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {safeAllPrograms.map((p) => (
                      <TableRow key={p.id}>
                        <TableCell sx={{ fontWeight: 700 }}>{p.autoclaveCode} ({p.autoclaveName})</TableCell>
                        <TableCell>{p.programCode}</TableCell>
                        <TableCell>{p.programName}</TableCell>
                        <TableCell>{p.loadType}</TableCell>
                        <TableCell align="right">{t("equipmentPage.tempValue", { value: p.temperature })}</TableCell>
                        <TableCell align="right">{t("equipmentPage.autoclave.minUnit", { value: p.cycleTimeMinutes })}</TableCell>
                        <TableCell>
                          <Chip
                            size="small"
                            label={p.isActive ? t("common.active") : t("common.inactive")}
                            color={p.isActive ? "success" : "default"}
                          />
                        </TableCell>
                        <TableCell align="center">
                          <Tooltip title={t("equipmentPage.autoclave.editProgramTooltip")}>
                            <IconButton size="small" onClick={() => handleOpenEditProgram(p)}>
                              <EditIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                          <Tooltip title={t("equipmentPage.autoclave.viewHistoryTooltip")}>
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
                          {t("equipmentPage.autoclave.noProgramsAll")}
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </TableContainer>
            </Paper>
          )}

          {/* TAB 2: CONFIGURATION HISTORY */}
          {activeTab === 2 && (
            <Paper sx={{ p: 2.5 }}>
              <SectionTitle>{t("equipmentPage.historyTab.title")}</SectionTitle>
              <Typography
                variant="body2"
                sx={{
                  color: "text.secondary",
                  mb: 2
                }}>
                {t("equipmentPage.historyTab.subtitle")}
              </Typography>
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow sx={tableHeadSx}>
                      <TableCell>{t("equipmentPage.historyTab.columns.effectiveOn")}</TableCell>
                      <TableCell>{t("equipmentPage.historyTab.columns.equipmentCode")}</TableCell>
                      <TableCell>{t("equipmentPage.historyTab.columns.category")}</TableCell>
                      <TableCell>{t("equipmentPage.historyTab.columns.detailsChange")}</TableCell>
                      <TableCell>{t("equipmentPage.historyTab.columns.changedBy")}</TableCell>
                      <TableCell>{t("equipmentPage.historyTab.columns.reasonComment")}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {safeSetPointHistory.map((h) => (
                      <TableRow key={`sp-${h.id}`}>
                        <TableCell>{formatDateTimeWithTimeZone(h.changedAt)}</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{selectedEquipment?.code ?? t("equipmentPage.historyTab.incubatorFallback")}</TableCell>
                        <TableCell><Chip size="small" label={t("equipmentPage.historyTab.setPointChip")} color="primary" variant="outlined" /></TableCell>
                        <TableCell>{t("equipmentPage.historyTab.setPointChange", { prev: h.previousSetPoint, next: h.newSetPoint })}</TableCell>
                        <TableCell>{h.changedByName}</TableCell>
                        <TableCell>{h.reason}</TableCell>
                      </TableRow>
                    ))}
                    {safeSetPointHistory.length === 0 && (
                      <TableRow>
                        <TableCell colSpan={6} align="center" sx={{ py: 3, color: setPointHistoryFailed ? "error.main" : "text.secondary" }}>
                          {setPointHistoryFailed
                            ? t("equipmentPage.incubator.historyLoadError")
                            : t("equipmentPage.historyTab.selectIncubatorPrompt")}
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </TableContainer>
            </Paper>
          )}
        </>
      )}

      {/* ADD / EDIT / COPY EQUIPMENT WIZARD */}
      <EquipmentWizard
        open={wizard !== null}
        editing={wizard?.kind === "edit" ? selectedEquipment ?? null : null}
        copyFrom={wizard?.kind === "copy" ? selectedEquipment ?? null : null}
        prefill={wizard?.kind === "create" ? wizard.prefill ?? null : null}
        mySections={mySections}
        inventory={inventoryList}
        onSaved={async (createdId) => {
          if (createdId) setSelectedEqId(createdId);
          await loadData();
        }}
        onClose={() => setWizard(null)}
      />

      {/* DIALOG 1: EDIT INCUBATOR SET POINT */}
      <FloatingDialog
        open={editSetPointDialogOpen}
        onClose={() => setEditSetPointDialogOpen(false)}
        maxWidth="sm"
        titleSx={{ fontWeight: 700 }}
        title={t("equipmentPage.incubator.editDialogTitle")}
        actions={
          <>
            <Button onClick={() => setEditSetPointDialogOpen(false)} variant="outlined" disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button
              onClick={handleSaveSetPoint}
              variant="contained"
              disabled={saving}
              color="primary"
            >
              {saving ? t("common.saving") : t("common.saveChanges")}
            </Button>
          </>
        }
      >
        {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}
        <Stack spacing={2}>
          <Box>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>{t("equipmentPage.incubator.equipmentLabel")}</Typography>
            <Typography variant="body1" sx={{
              fontWeight: 700
            }}>
              {selectedEquipment?.code}: {selectedEquipment?.name}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>{t("equipmentPage.incubator.currentSetPointLabel")}</Typography>
            <Typography
              variant="body1"
              sx={{
                fontWeight: 700,
                color: "primary.main"
              }}>
              {selectedEquipment?.setPointTemperature ? t("equipmentPage.tempValue", { value: selectedEquipment.setPointTemperature }) : t("equipmentPage.incubator.notConfigured")}
            </Typography>
          </Box>
          <TextField
            label={t("equipmentPage.incubator.newSetPointLabel")}
            type="number"
            value={newSetPoint}
            onChange={(e) => setNewSetPoint(e.target.value)}
            fullWidth
            slotProps={{
              htmlInput: { step: "0.1" }
            }}
          />
          <Stack direction="row" spacing={0.5} sx={{ alignItems: "flex-start" }}>
            <TextField
              label={t("equipmentPage.incubator.reasonLabel")}
              placeholder={t("equipmentPage.incubator.reasonPlaceholder")}
              value={setPointReason}
              onChange={(e) => setSetPointReason(e.target.value)}
              multiline
              rows={2}
              fullWidth
              slotProps={{ input: { "aria-describedby": hintDescribedBy("equipment-reason-for-change") } }}
            />
            <FieldHint id="equipment-reason-for-change" label={t("equipmentPage.incubator.reasonHintLabel")} hint={hints.reasonForChange} />
          </Stack>
        </Stack>
      </FloatingDialog>

      {/* DIALOG 2: ADD / EDIT AUTOCLAVE PROGRAM */}
      <FloatingDialog
        open={programDialogOpen}
        onClose={() => setProgramDialogOpen(false)}
        maxWidth="sm"
        titleSx={{ fontWeight: 700 }}
        title={programForm.id ? t("equipmentPage.autoclave.editDialogTitle") : t("equipmentPage.autoclave.addDialogTitle")}
        actions={
          <>
            <Button onClick={() => setProgramDialogOpen(false)} variant="outlined" disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button
              onClick={handleSaveProgram}
              variant="contained"
              disabled={saving}
              color="primary"
            >
              {saving ? t("common.saving") : t("equipmentPage.autoclave.saveProgram")}
            </Button>
          </>
        }
      >
        {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}
        <Stack spacing={2}>
          <Box>
            <Typography variant="caption" sx={{
              color: "text.secondary"
            }}>{t("equipmentPage.autoclave.autoclaveEquipment")}</Typography>
            <Typography variant="body1" sx={{
              fontWeight: 700
            }}>
              {selectedEquipment?.code}: {selectedEquipment?.name}
            </Typography>
          </Box>
          <TextField
            label={t("equipmentPage.autoclave.programCodeLabel")}
            placeholder={t("equipmentPage.autoclave.programCodePlaceholder")}
            value={programForm.programCode ?? ""}
            onChange={(e) => setProgramForm({ ...programForm, programCode: e.target.value })}
            fullWidth
          />
          <TextField
            label={t("equipmentPage.autoclave.programNameLabel")}
            placeholder={t("equipmentPage.autoclave.programNamePlaceholder")}
            value={programForm.programName ?? ""}
            onChange={(e) => setProgramForm({ ...programForm, programName: e.target.value })}
            fullWidth
          />
          <TextField
            label={t("equipmentPage.autoclave.loadTypeLabel")}
            placeholder={t("equipmentPage.autoclave.loadTypePlaceholder")}
            value={programForm.loadType ?? ""}
            onChange={(e) => setProgramForm({ ...programForm, loadType: e.target.value })}
            fullWidth
          />
          <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2 }}>
            <TextField
              label={t("equipmentPage.autoclave.temperatureLabel")}
              type="number"
              value={programForm.temperature ?? 121}
              onChange={(e) => setProgramForm({ ...programForm, temperature: Number(e.target.value) })}
            />
            <TextField
              label={t("equipmentPage.autoclave.cycleTimeLabel")}
              type="number"
              value={programForm.cycleTimeMinutes ?? 15}
              onChange={(e) => setProgramForm({ ...programForm, cycleTimeMinutes: Number(e.target.value) })}
            />
          </Box>
          <Select
            value={programForm.isActive ? "Active" : "Inactive"}
            onChange={(e) => setProgramForm({ ...programForm, isActive: e.target.value === "Active" })}
            fullWidth
            inputProps={{ "aria-label": t("equipmentPage.autoclave.programStatusAria") }}
          >
            <MenuItem value="Active">{t("common.active")}</MenuItem>
            <MenuItem value="Inactive">{t("common.inactive")}</MenuItem>
          </Select>
          <TextField
            label={t("equipmentPage.autoclave.commentLabel")}
            placeholder={t("equipmentPage.autoclave.commentPlaceholder")}
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
        title={t("equipmentPage.autoclave.historyDialogTitle", { code: selectedProgramCode })}
        actions={
          <Button onClick={() => setProgramHistoryDialogOpen(false)} variant="contained">
            {t("common.close")}
          </Button>
        }
      >
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow sx={tableHeadSx}>
                  <TableCell>{t("equipmentPage.autoclave.historyColumns.dateTime")}</TableCell>
                  <TableCell>{t("equipmentPage.autoclave.historyColumns.action")}</TableCell>
                  <TableCell>{t("equipmentPage.autoclave.historyColumns.programName")}</TableCell>
                  <TableCell>{t("equipmentPage.autoclave.historyColumns.loadType")}</TableCell>
                  <TableCell align="right">{t("equipmentPage.autoclave.historyColumns.temperature")}</TableCell>
                  <TableCell align="right">{t("equipmentPage.autoclave.historyColumns.cycleTime")}</TableCell>
                  <TableCell>{t("equipmentPage.autoclave.historyColumns.status")}</TableCell>
                  <TableCell>{t("equipmentPage.autoclave.historyColumns.changedBy")}</TableCell>
                  <TableCell>{t("equipmentPage.autoclave.historyColumns.comment")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {Array.isArray(programHistory) && programHistory.map((h) => (
                  <TableRow key={h.id}>
                    <TableCell>{formatDateTimeWithTimeZone(h.changedAt)}</TableCell>
                    <TableCell><Chip size="small" label={h.action} color={h.action === "Created" ? "success" : "info"} /></TableCell>
                    <TableCell>{h.newProgramName}</TableCell>
                    <TableCell>{h.newLoadType}</TableCell>
                    <TableCell align="right">{t("equipmentPage.tempValue", { value: h.newTemperature })}</TableCell>
                    <TableCell align="right">{t("equipmentPage.autoclave.minUnit", { value: h.newCycleTimeMinutes })}</TableCell>
                    <TableCell>{h.newIsActive ? t("common.active") : t("common.inactive")}</TableCell>
                    <TableCell>{h.changedByName}</TableCell>
                    <TableCell>{h.comment}</TableCell>
                  </TableRow>
                ))}
                {(!Array.isArray(programHistory) || programHistory.length === 0) && (
                  <TableRow>
                    <TableCell colSpan={9} align="center" sx={{ py: 3, color: "text.secondary" }}>
                      {t("equipmentPage.autoclave.noProgramHistory")}
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>
      </FloatingDialog>

      {/* DIALOG 4: SELECT FROM INVENTORY */}
      <FloatingDialog
        open={inventoryDialogOpen}
        onClose={() => setInventoryDialogOpen(false)}
        maxWidth="md"
        titleSx={{ fontWeight: 700 }}
        title={t("equipmentPage.inventoryDialog.title")}
        actions={
          <Button onClick={() => setInventoryDialogOpen(false)} variant="outlined">
            {t("common.close")}
          </Button>
        }
      >
          <Typography
            variant="body2"
            sx={{
              color: "text.secondary",
              mb: 2
            }}>
            {t("equipmentPage.inventoryDialog.description")}
          </Typography>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow sx={tableHeadSx}>
                  <TableCell>{t("equipmentPage.inventoryDialog.columns.equipmentCode")}</TableCell>
                  <TableCell>{t("equipmentPage.inventoryDialog.columns.instrumentType")}</TableCell>
                  <TableCell>{t("equipmentPage.inventoryDialog.columns.manufacturer")}</TableCell>
                  <TableCell>{t("equipmentPage.inventoryDialog.columns.location")}</TableCell>
                  <TableCell>{t("equipmentPage.inventoryDialog.columns.status")}</TableCell>
                  <TableCell align="center">{t("equipmentPage.inventoryDialog.columns.action")}</TableCell>
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
                          {isAlreadyLinked ? t("equipmentPage.inventoryDialog.configured") : t("equipmentPage.inventoryDialog.configureForLab")}
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
      </FloatingDialog>
    </>
  );
}
