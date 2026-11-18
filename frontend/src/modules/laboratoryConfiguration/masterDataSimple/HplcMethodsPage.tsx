import { useState, useEffect, useMemo, useCallback } from "react";
import {
  Button,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Alert
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { LabPage, FilterBar } from "../../../components/lab";
import { toast } from "sonner";
import { useTranslation } from "../../../i18n";
import { HplcMethodTable } from "./hplcMethod/HplcMethodTable";
import {
  HplcMethodService,
  HplcMethodListItem
} from "./services/HplcMethodService";
import {
  SolutionMasterService,
  SolutionMaster
} from "./services/SolutionMasterService";
import {
  MaterialMasterService,
  MaterialMasterEntry
} from "./services/MaterialMasterService";
import { useLaboratorySections } from "../../../hooks/useLaboratorySections";
import { getMySections, LaboratorySection } from "../../../services/laboratorySectionService";
import { HplcMethodDialog } from "./hplcMethod/HplcMethodDialog";
import { HplcMethodHistoryDialog } from "./hplcMethod/HplcMethodHistoryDialog";
import { ReasonDialog } from "./solutionMaster/ReasonDialog";

export function HplcMethodsPage() {
  const { t } = useTranslation("masterData");
  const { sections } = useLaboratorySections();

  const [methods, setMethods] = useState<HplcMethodListItem[]>([]);
  const [solutions, setSolutions] = useState<SolutionMaster[]>([]);
  const [materialMasters, setMaterialMasters] = useState<MaterialMasterEntry[]>([]);
  const [mySections, setMySections] = useState<LaboratorySection[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [searchQuery, setSearchQuery] = useState("");
  const [techniqueFilter, setTechniqueFilter] = useState<"ALL" | "Hplc" | "Gc">("ALL");
  const [statusFilter, setStatusFilter] = useState<"ALL" | "ACTIVE" | "INACTIVE">("ALL");
  const [sectionFilter, setSectionFilter] = useState<string>("ALL");

  // Create / Edit Dialog State
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);

  // Audit History Dialog State
  const [historyMethod, setHistoryMethod] = useState<HplcMethodListItem | null>(null);

  // Activate / Deactivate Toggle Dialog with Reason
  const [methodToToggle, setMethodToToggle] = useState<HplcMethodListItem | null>(null);
  const [toggleReason, setToggleReason] = useState("");
  const [togglingActive, setTogglingActive] = useState(false);
  const [toggleError, setToggleError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [methodData, solutionData, materialData, mySecData] = await Promise.all([
        HplcMethodService.getAll(),
        SolutionMasterService.getAll(),
        MaterialMasterService.getAll(undefined, true),
        getMySections().catch(() => [])
      ]);
      setMethods(Array.isArray(methodData) ? methodData : []);
      setSolutions(Array.isArray(solutionData) ? solutionData : []);
      setMaterialMasters(Array.isArray(materialData) ? materialData : []);
      setMySections(Array.isArray(mySecData) ? mySecData : []);
    } catch (err: unknown) {
      console.error("Failed to load HPLC methods:", err);
      const errObj = err as { response?: { data?: { message?: string } }; message?: string };
      setError(errObj.response?.data?.message ?? errObj.message ?? t("hplcMethods.loadError"));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Solutions and reference standards are often created in another tab
  // while this page is open; re-read them so the dialog's pickers include
  // anything added since the page loaded.
  const refreshPickerData = useCallback(async () => {
    try {
      const [solutionData, materialData] = await Promise.all([
        SolutionMasterService.getAll(),
        MaterialMasterService.getAll(undefined, true)
      ]);
      setSolutions(Array.isArray(solutionData) ? solutionData : []);
      setMaterialMasters(Array.isArray(materialData) ? materialData : []);
    } catch (err: unknown) {
      console.error("Failed to refresh solutions / reference standards:", err);
    }
  }, []);

  const handleOpenAdd = () => {
    setEditingId(null);
    setDialogOpen(true);
    void refreshPickerData();
  };

  const handleOpenEdit = (m: HplcMethodListItem) => {
    setEditingId(m.id);
    setDialogOpen(true);
    void refreshPickerData();
  };

  const handleOpenToggleActive = (m: HplcMethodListItem) => {
    setMethodToToggle(m);
    setToggleReason("");
    setToggleError(null);
  };

  const handleConfirmToggleActive = async () => {
    if (!methodToToggle) return;
    const trimmedReason = toggleReason.trim();
    if (!trimmedReason) {
      setToggleError(t("hplcMethods.toggleReasonRequired"));
      return;
    }

    const nextActive = !methodToToggle.isActive;
    setTogglingActive(true);
    setToggleError(null);
    try {
      await HplcMethodService.setActive(methodToToggle.id, nextActive, trimmedReason);
      toast.success(
        nextActive
          ? t("hplcMethods.toggledToast_activated", { name: methodToToggle.name })
          : t("hplcMethods.toggledToast_deactivated", { name: methodToToggle.name })
      );
      setMethodToToggle(null);
      await loadData();
    } catch (err: unknown) {
      const errObj = err as { response?: { data?: { message?: string } }; message?: string };
      setToggleError(errObj.response?.data?.message ?? errObj.message ?? t("hplcMethods.toggleError"));
    } finally {
      setTogglingActive(false);
    }
  };

  const filteredMethods = useMemo(() => {
    return methods.filter((m) => {
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase().trim();
        const match =
          m.name.toLowerCase().includes(q) ||
          m.abbreviation.toLowerCase().includes(q) ||
          m.sectionName.toLowerCase().includes(q);
        if (!match) return false;
      }
      if (techniqueFilter !== "ALL" && (m.technique ?? "Hplc") !== techniqueFilter) return false;
      if (statusFilter === "ACTIVE" && !m.isActive) return false;
      if (statusFilter === "INACTIVE" && m.isActive) return false;
      if (sectionFilter !== "ALL" && m.sectionName !== sectionFilter) return false;
      return true;
    });
  }, [methods, searchQuery, techniqueFilter, statusFilter, sectionFilter]);

  const resolveSectionDisplay = (secName?: string) => {
    if (secName) return secName;
    return t("hplcMethods.sectionFallback");
  };

  const addButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={handleOpenAdd} sx={{ fontWeight: 600, textTransform: "none" }}>
      {t("hplcMethods.addMethod")}
    </Button>
  );

  return (
    <LabPage
      title={t("hplcMethods.title")}
      subtitle={t("hplcMethods.subtitle")}
      actions={addButton}
      filters={
        <FilterBar
          search={searchQuery}
          onSearch={setSearchQuery}
          placeholder={t("hplcMethods.searchPlaceholder")}
          resultCount={filteredMethods.length}
          onRefresh={loadData}
          refreshing={loading}
        >
          <FormControl size="small" sx={{ minWidth: 140 }}>
            <InputLabel id="method-technique-filter-label">{t("hplcMethods.techniqueLabel")}</InputLabel>
            <Select
              labelId="method-technique-filter-label"
              label={t("hplcMethods.techniqueLabel")}
              value={techniqueFilter}
              onChange={(e) => setTechniqueFilter(e.target.value as "ALL" | "Hplc" | "Gc")}
            >
              <MenuItem value="ALL">{t("hplcMethods.allTechniques")}</MenuItem>
              <MenuItem value="Hplc">{t("hplcMethods.hplc")}</MenuItem>
              <MenuItem value="Gc">{t("hplcMethods.gc")}</MenuItem>
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 150 }}>
            <InputLabel id="method-status-filter-label">{t("common.status")}</InputLabel>
            <Select
              labelId="method-status-filter-label"
              label={t("common.status")}
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as "ALL" | "ACTIVE" | "INACTIVE")}
            >
              <MenuItem value="ALL">{t("common.allStatuses")}</MenuItem>
              <MenuItem value="ACTIVE">{t("common.activeOnly")}</MenuItem>
              <MenuItem value="INACTIVE">{t("common.inactiveOnly")}</MenuItem>
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 180 }}>
            <InputLabel id="method-section-filter-label">{t("common.section")}</InputLabel>
            <Select
              labelId="method-section-filter-label"
              label={t("common.section")}
              value={sectionFilter}
              onChange={(e) => setSectionFilter(e.target.value)}
            >
              <MenuItem value="ALL">{t("common.allSections")}</MenuItem>
              {sections.map((sec) => (
                <MenuItem key={sec.sectionId} value={sec.sectionName}>
                  {sec.sectionName}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        </FilterBar>
      }
    >
      {error && <Alert severity="error">{error}</Alert>}

      <HplcMethodTable
        methods={filteredMethods}
        loading={loading}
        searchQuery={searchQuery}
        techniqueFilter={techniqueFilter}
        statusFilter={statusFilter}
        sectionFilter={sectionFilter}
        resolveSectionDisplay={resolveSectionDisplay}
        onViewHistory={(m) => setHistoryMethod(m)}
        onEdit={handleOpenEdit}
        onToggleActive={handleOpenToggleActive}
        emptyAction={addButton}
      />

      {/* Add / Edit Floating Dialog */}
      <HplcMethodDialog
        open={dialogOpen}
        editingId={editingId}
        solutions={solutions}
        materialMasters={materialMasters}
        mySections={mySections}
        onClose={() => setDialogOpen(false)}
        onSuccess={loadData}
      />

      {/* Audit History Dialog */}
      <HplcMethodHistoryDialog
        open={Boolean(historyMethod)}
        method={historyMethod}
        onClose={() => setHistoryMethod(null)}
      />

      {/* Activate / Deactivate Confirmation Dialog with Reason */}
      <ReasonDialog
        open={Boolean(methodToToggle)}
        title={methodToToggle?.isActive ? t("hplcMethods.deactivateTitle") : t("hplcMethods.activateTitle")}
        onClose={() => { if (!togglingActive) setMethodToToggle(null); }}
        onConfirm={handleConfirmToggleActive}
        confirmText={methodToToggle?.isActive ? t("hplcMethods.deactivateConfirm") : t("hplcMethods.activateConfirm")}
        confirmColor={methodToToggle?.isActive ? "error" : "success"}
        loading={togglingActive}
        loadingText={t("common.updating")}
        error={toggleError}
        reason={toggleReason}
        onReasonChange={setToggleReason}
        label={t("hplcMethods.reasonLabel")}
        placeholder={methodToToggle?.isActive ? t("hplcMethods.deactivatePlaceholder") : t("hplcMethods.activatePlaceholder")}
        disabled={!toggleReason.trim() || togglingActive}
      >
        <Alert severity={methodToToggle?.isActive ? "warning" : "info"} sx={{ fontSize: 13 }}>
          {methodToToggle?.isActive
            ? t("hplcMethods.deactivateMessage", { name: methodToToggle?.name ?? "" })
            : t("hplcMethods.activateMessage", { name: methodToToggle?.name ?? "" })}
        </Alert>
      </ReasonDialog>
    </LabPage>
  );
}
