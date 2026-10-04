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
import { IcpMethodTable } from "./icpMethod/IcpMethodTable";
import {
  IcpMethodService,
  IcpMethodListItem
} from "./services/IcpMethodService";
import {
  MaterialMasterService,
  MaterialMasterEntry
} from "./services/MaterialMasterService";
import { useLaboratorySections } from "../../../hooks/useLaboratorySections";
import { getMySections, LaboratorySection } from "../../../services/laboratorySectionService";
import { IcpMethodDialog } from "./icpMethod/IcpMethodDialog";
import { IcpMethodHistoryDialog } from "./icpMethod/IcpMethodHistoryDialog";
import { ReasonDialog } from "./solutionMaster/ReasonDialog";

export function IcpMethodsPage() {
  const { sections } = useLaboratorySections();

  const [methods, setMethods] = useState<IcpMethodListItem[]>([]);
  const [materialMasters, setMaterialMasters] = useState<MaterialMasterEntry[]>([]);
  const [mySections, setMySections] = useState<LaboratorySection[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [searchQuery, setSearchQuery] = useState("");
  const [modeFilter, setModeFilter] = useState<"ALL" | "MineralAssay" | "ElementalImpurities">("ALL");
  const [statusFilter, setStatusFilter] = useState<"ALL" | "ACTIVE" | "INACTIVE">("ALL");
  const [sectionFilter, setSectionFilter] = useState<string>("ALL");

  // Create / Edit Dialog State
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);

  // Audit History Dialog State
  const [historyMethod, setHistoryMethod] = useState<IcpMethodListItem | null>(null);

  // Activate / Deactivate Toggle Dialog with Reason
  const [methodToToggle, setMethodToToggle] = useState<IcpMethodListItem | null>(null);
  const [toggleReason, setToggleReason] = useState("");
  const [togglingActive, setTogglingActive] = useState(false);
  const [toggleError, setToggleError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [methodData, materialData, mySecData] = await Promise.all([
        IcpMethodService.getAll(),
        MaterialMasterService.getAll(undefined, true),
        getMySections().catch(() => [])
      ]);
      setMethods(Array.isArray(methodData) ? methodData : []);
      setMaterialMasters(Array.isArray(materialData) ? materialData : []);
      setMySections(Array.isArray(mySecData) ? mySecData : []);
    } catch (err: unknown) {
      console.error("Failed to load ICP methods:", err);
      const errObj = err as { response?: { data?: { message?: string } }; message?: string };
      setError(errObj.response?.data?.message ?? errObj.message ?? "Could not load ICP methods.");
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

  const handleOpenEdit = (m: IcpMethodListItem) => {
    setEditingId(m.id);
    setDialogOpen(true);
  };

  const handleOpenToggleActive = (m: IcpMethodListItem) => {
    setMethodToToggle(m);
    setToggleReason("");
    setToggleError(null);
  };

  const handleConfirmToggleActive = async () => {
    if (!methodToToggle) return;
    const trimmedReason = toggleReason.trim();
    if (!trimmedReason) {
      setToggleError("A reason is required to activate or deactivate an ICP method.");
      return;
    }

    const nextActive = !methodToToggle.isActive;
    setTogglingActive(true);
    setToggleError(null);
    try {
      await IcpMethodService.setActive(methodToToggle.id, nextActive, trimmedReason);
      toast.success(`ICP method "${methodToToggle.name}" ${nextActive ? "activated" : "deactivated"}.`);
      setMethodToToggle(null);
      await loadData();
    } catch (err: unknown) {
      const errObj = err as { response?: { data?: { message?: string } }; message?: string };
      setToggleError(errObj.response?.data?.message ?? errObj.message ?? "Could not update ICP method status.");
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
      if (modeFilter !== "ALL" && m.mode !== modeFilter) return false;
      if (statusFilter === "ACTIVE" && !m.isActive) return false;
      if (statusFilter === "INACTIVE" && m.isActive) return false;
      if (sectionFilter !== "ALL" && m.sectionName !== sectionFilter) return false;
      return true;
    });
  }, [methods, searchQuery, modeFilter, statusFilter, sectionFilter]);

  const resolveSectionDisplay = (secName?: string) => {
    if (secName) return secName;
    return "Section";
  };

  const addButton = (
    <Button
      variant="contained"
      startIcon={<AddIcon />}
      onClick={handleOpenAdd}
      sx={{ fontWeight: 600, textTransform: "none" }}
    >
      Add Method
    </Button>
  );

  return (
    <LabPage
      title="ICP Methods Master"
      subtitle="Manage ICP-OES methods, calibration standard curves, quality control checks, sample preparation defaults, and element emission lines."
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
          <FormControl size="small" sx={{ minWidth: 170 }}>
            <InputLabel id="icp-mode-filter-label">Mode</InputLabel>
            <Select
              labelId="icp-mode-filter-label"
              label="Mode"
              value={modeFilter}
              onChange={(e) => setModeFilter(e.target.value as "ALL" | "MineralAssay" | "ElementalImpurities")}
            >
              <MenuItem value="ALL">All Modes</MenuItem>
              <MenuItem value="MineralAssay">Mineral Assay</MenuItem>
              <MenuItem value="ElementalImpurities">Elemental Impurities</MenuItem>
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 150 }}>
            <InputLabel id="icp-status-filter-label">Status</InputLabel>
            <Select
              labelId="icp-status-filter-label"
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
            <InputLabel id="icp-section-filter-label">Section</InputLabel>
            <Select
              labelId="icp-section-filter-label"
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

      <IcpMethodTable
        methods={filteredMethods}
        loading={loading}
        searchQuery={searchQuery}
        modeFilter={modeFilter}
        statusFilter={statusFilter}
        sectionFilter={sectionFilter}
        resolveSectionDisplay={resolveSectionDisplay}
        onViewHistory={(m) => setHistoryMethod(m)}
        onEdit={handleOpenEdit}
        onToggleActive={handleOpenToggleActive}
        emptyAction={addButton}
      />

      {/* Add / Edit Floating Dialog */}
      <IcpMethodDialog
        open={dialogOpen}
        editingId={editingId}
        materialMasters={materialMasters}
        sections={sections}
        mySections={mySections}
        onClose={() => setDialogOpen(false)}
        onSuccess={loadData}
      />

      {/* Audit History Dialog */}
      <IcpMethodHistoryDialog
        open={Boolean(historyMethod)}
        method={historyMethod}
        onClose={() => setHistoryMethod(null)}
      />

      {/* Activate / Deactivate Confirmation Dialog with Reason */}
      <ReasonDialog
        open={Boolean(methodToToggle)}
        title={methodToToggle?.isActive ? "Deactivate ICP Method" : "Activate ICP Method"}
        onClose={() => {
          if (!togglingActive) setMethodToToggle(null);
        }}
        onConfirm={handleConfirmToggleActive}
        confirmText={methodToToggle?.isActive ? "Deactivate Method" : "Activate Method"}
        confirmColor={methodToToggle?.isActive ? "error" : "success"}
        loading={togglingActive}
        loadingText="Updating..."
        error={toggleError}
        reason={toggleReason}
        onReasonChange={setToggleReason}
        label="Reason *"
        placeholder={
          methodToToggle?.isActive
            ? "State the operational reason for deactivating this ICP method..."
            : "State the operational reason for activating this ICP method..."
        }
        disabled={!toggleReason.trim() || togglingActive}
      >
        <Alert severity={methodToToggle?.isActive ? "warning" : "info"} sx={{ fontSize: 13 }}>
          {methodToToggle?.isActive
            ? `Are you sure you want to deactivate "${methodToToggle?.name}"? Inactive ICP methods cannot be selected for new test definitions.`
            : `Are you sure you want to activate "${methodToToggle?.name}"?`}
        </Alert>
      </ReasonDialog>
    </LabPage>
  );
}
