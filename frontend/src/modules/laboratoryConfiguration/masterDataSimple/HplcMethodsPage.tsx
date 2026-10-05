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
      setError(errObj.response?.data?.message ?? errObj.message ?? "Could not load HPLC methods.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleOpenAdd = () => {
    setEditingId(null);
    setDialogOpen(true);
  };

  const handleOpenEdit = (m: HplcMethodListItem) => {
    setEditingId(m.id);
    setDialogOpen(true);
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
      setToggleError("A reason is required to activate or deactivate an HPLC method.");
      return;
    }

    const nextActive = !methodToToggle.isActive;
    setTogglingActive(true);
    setToggleError(null);
    try {
      await HplcMethodService.setActive(methodToToggle.id, nextActive, trimmedReason);
      toast.success(`HPLC method "${methodToToggle.name}" ${nextActive ? "activated" : "deactivated"}.`);
      setMethodToToggle(null);
      await loadData();
    } catch (err: unknown) {
      const errObj = err as { response?: { data?: { message?: string } }; message?: string };
      setToggleError(errObj.response?.data?.message ?? errObj.message ?? "Could not update HPLC method status.");
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
    return "Section";
  };

  const addButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={handleOpenAdd} sx={{ fontWeight: 600, textTransform: "none" }}>
      Add Method
    </Button>
  );

  return (
    <LabPage
      title="Chromatography Methods Master"
      subtitle="Manage HPLC and GC methods, column chemistry, carrier gas/oven parameters, mobile phases, and analyte theoretical constants with SST criteria."
      actions={addButton}
      filters={
        <FilterBar
          search={searchQuery}
          onSearch={setSearchQuery}
          placeholder="Search by name, abbreviation, section..."
          resultCount={filteredMethods.length}
          onRefresh={loadData}
          refreshing={loading}
        >
          <FormControl size="small" sx={{ minWidth: 140 }}>
            <InputLabel id="method-technique-filter-label">Technique</InputLabel>
            <Select
              labelId="method-technique-filter-label"
              label="Technique"
              value={techniqueFilter}
              onChange={(e) => setTechniqueFilter(e.target.value as "ALL" | "Hplc" | "Gc")}
            >
              <MenuItem value="ALL">All Techniques</MenuItem>
              <MenuItem value="Hplc">HPLC</MenuItem>
              <MenuItem value="Gc">GC</MenuItem>
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 150 }}>
            <InputLabel id="method-status-filter-label">Status</InputLabel>
            <Select
              labelId="method-status-filter-label"
              label="Status"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as "ALL" | "ACTIVE" | "INACTIVE")}
            >
              <MenuItem value="ALL">All Statuses</MenuItem>
              <MenuItem value="ACTIVE">Active Only</MenuItem>
              <MenuItem value="INACTIVE">Inactive Only</MenuItem>
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 180 }}>
            <InputLabel id="method-section-filter-label">Section</InputLabel>
            <Select
              labelId="method-section-filter-label"
              label="Section"
              value={sectionFilter}
              onChange={(e) => setSectionFilter(e.target.value)}
            >
              <MenuItem value="ALL">All Sections</MenuItem>
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
        sections={sections}
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
        title={methodToToggle?.isActive ? "Deactivate HPLC Method" : "Activate HPLC Method"}
        onClose={() => { if (!togglingActive) setMethodToToggle(null); }}
        onConfirm={handleConfirmToggleActive}
        confirmText={methodToToggle?.isActive ? "Deactivate Method" : "Activate Method"}
        confirmColor={methodToToggle?.isActive ? "error" : "success"}
        loading={togglingActive}
        loadingText="Updating..."
        error={toggleError}
        reason={toggleReason}
        onReasonChange={setToggleReason}
        label="Reason *"
        placeholder={methodToToggle?.isActive ? "State the operational reason for deactivating this HPLC method..." : "State the operational reason for activating this HPLC method..."}
        disabled={!toggleReason.trim() || togglingActive}
      >
        <Alert severity={methodToToggle?.isActive ? "warning" : "info"} sx={{ fontSize: 13 }}>
          {methodToToggle?.isActive
            ? `Are you sure you want to deactivate "${methodToToggle?.name}"? Inactive HPLC methods cannot be selected for new test definitions.`
            : `Are you sure you want to activate "${methodToToggle?.name}"?`}
        </Alert>
      </ReasonDialog>
    </LabPage>
  );
}
