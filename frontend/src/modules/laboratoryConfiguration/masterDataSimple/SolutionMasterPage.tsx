import { useState, useEffect, useMemo, useCallback, useRef } from "react";
import { useSearchParams } from "react-router-dom";
import {
  Box,
  Typography,
  Button,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Chip,
  Stack,
  Tooltip,
  Alert
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import WavesOutlinedIcon from "@mui/icons-material/WavesOutlined";
import OpacityOutlinedIcon from "@mui/icons-material/OpacityOutlined";
import ScaleOutlinedIcon from "@mui/icons-material/ScaleOutlined";
import { LabPage, FilterBar, RegisterTable, RegisterColumn } from "../../../components/lab";
import { StatusBadge } from "../../../components/StatusBadge";
import { toast } from "sonner";
import { useTranslation } from "../../../i18n";
import {
  SolutionMasterService,
  SolutionMaster,
  SolutionType
} from "./services/SolutionMasterService";
import {
  MaterialMasterService,
  MaterialMasterEntry
} from "./services/MaterialMasterService";
import { useLaboratorySections } from "../../../hooks/useLaboratorySections";
import { getMySections, LaboratorySection } from "../../../services/laboratorySectionService";
import { SolutionMasterDialog } from "./solutionMaster/SolutionMasterDialog";
import { ReasonDialog } from "./solutionMaster/ReasonDialog";
import { formatCount } from "../../../utils/formatCount";

export function SolutionMasterPage() {
  const { t } = useTranslation("masterData");
  const { sections, sectionName } = useLaboratorySections();

  const [solutions, setSolutions] = useState<SolutionMaster[]>([]);
  const [materialMasters, setMaterialMasters] = useState<MaterialMasterEntry[]>([]);
  const [mySections, setMySections] = useState<LaboratorySection[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [searchQuery, setSearchQuery] = useState("");
  const [typeFilter, setTypeFilter] = useState<"ALL" | SolutionType>("ALL");
  const [statusFilter, setStatusFilter] = useState<"ALL" | "ACTIVE" | "INACTIVE">("ALL");
  const [sectionFilter, setSectionFilter] = useState<string>("ALL");

  // Menu links from a laboratory area append ?lab=MICRO to preselect that lab's section;
  // it is only a starting filter, the person can change it.
  const [searchParams] = useSearchParams();
  const labParam = searchParams.get("lab");
  const labPreselected = useRef(false);
  useEffect(() => {
    if (!labParam || labPreselected.current || sections.length === 0) return;
    const found = sections.find((s) => s.sectionCode === labParam);
    if (found) setSectionFilter(String(found.sectionId));
    labPreselected.current = true;
  }, [labParam, sections]);

  // Create / Edit Dialog State
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingEntry, setEditingEntry] = useState<SolutionMaster | null>(null);

  // Activate / Deactivate Toggle Dialog with Reason
  const [solutionToToggle, setSolutionToToggle] = useState<SolutionMaster | null>(null);
  const [toggleReason, setToggleReason] = useState("");
  const [togglingActive, setTogglingActive] = useState(false);
  const [toggleError, setToggleError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [solutionData, materialData, mySecData] = await Promise.all([
        SolutionMasterService.getAll(),
        MaterialMasterService.getAll(undefined, true),
        getMySections().catch(() => [])
      ]);
      setSolutions(Array.isArray(solutionData) ? solutionData : []);
      setMaterialMasters(Array.isArray(materialData) ? materialData : []);
      setMySections(Array.isArray(mySecData) ? mySecData : []);
    } catch (err: unknown) {
      console.error("Failed to load solution masters:", err);
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      setError(errorObj.response?.data?.message ?? errorObj.message ?? t("solutionMasterPage.loadError"));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleOpenAdd = () => {
    setEditingEntry(null);
    setDialogOpen(true);
  };

  const handleOpenEdit = (entry: SolutionMaster) => {
    setEditingEntry(entry);
    setDialogOpen(true);
  };

  const handleOpenToggleActive = (solution: SolutionMaster) => {
    setSolutionToToggle(solution);
    setToggleReason("");
    setToggleError(null);
  };

  const handleConfirmToggleActive = async () => {
    if (!solutionToToggle) return;
    const trimmedReason = toggleReason.trim();
    if (!trimmedReason) {
      setToggleError(t("solutionMasterPage.toggleReasonRequired"));
      return;
    }

    const nextActive = !solutionToToggle.isActive;
    setTogglingActive(true);
    setToggleError(null);
    try {
      await SolutionMasterService.setActive(
        solutionToToggle.id,
        nextActive,
        trimmedReason,
        solutionToToggle.version
      );
      toast.success(
        nextActive
          ? t("solutionMasterPage.toggledToast_activated", { name: solutionToToggle.name })
          : t("solutionMasterPage.toggledToast_deactivated", { name: solutionToToggle.name })
      );
      setSolutionToToggle(null);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      setToggleError(errorObj.response?.data?.message ?? errorObj.message ?? t("solutionMasterPage.toggleError"));
    } finally {
      setTogglingActive(false);
    }
  };

  const filteredSolutions = useMemo(() => {
    return solutions.filter((sol) => {
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase().trim();
        const match =
          sol.name.toLowerCase().includes(q) ||
          (sol.instructions && sol.instructions.toLowerCase().includes(q)) ||
          (sol.storageCondition && sol.storageCondition.toLowerCase().includes(q)) ||
          sol.components.some(
            (c) =>
              (c.entryCode ?? "").toLowerCase().includes(q) ||
              (c.entryName ?? "").toLowerCase().includes(q) ||
              (c.sourceSolutionName ?? "").toLowerCase().includes(q) ||
              (c.text ?? "").toLowerCase().includes(q)
          );
        if (!match) return false;
      }
      if (typeFilter !== "ALL" && sol.type !== typeFilter) return false;
      if (statusFilter === "ACTIVE" && !sol.isActive) return false;
      if (statusFilter === "INACTIVE" && sol.isActive) return false;
      if (sectionFilter !== "ALL" && String(sol.sectionId) !== sectionFilter) return false;
      return true;
    });
  }, [solutions, searchQuery, typeFilter, statusFilter, sectionFilter]);

  const resolveSectionDisplay = (secId: number, secNameProp?: string) => {
    if (secNameProp) return secNameProp;
    const fromHook = sectionName(secId);
    if (fromHook) return fromHook;
    const foundSec = sections.find((s) => s.sectionId === secId);
    return foundSec?.sectionName ?? t("solutionMasterPage.sectionFallback", { id: secId });
  };

  const renderTypeChip = (type: SolutionType) => {
    switch (type) {
      case "MobilePhase":
        return <Chip icon={<WavesOutlinedIcon fontSize="small" />} label={t("solutionMasterPage.types.MobilePhase")} size="small" color="primary" variant="outlined" sx={{ fontSize: 12, fontWeight: 600 }} />;
      case "Diluent":
        return <Chip icon={<OpacityOutlinedIcon fontSize="small" />} label={t("solutionMasterPage.types.Diluent")} size="small" color="secondary" variant="outlined" sx={{ fontSize: 12, fontWeight: 600 }} />;
      case "Titrant":
        return <Chip icon={<ScaleOutlinedIcon fontSize="small" />} label={t("solutionMasterPage.types.Titrant")} size="small" color="info" variant="outlined" sx={{ fontSize: 12, fontWeight: 600 }} />;
      case "Reagent":
      case "Neutralizer":
        return <Chip icon={<OpacityOutlinedIcon fontSize="small" />} label={t(`solutionMasterPage.types.${type}`)} size="small" color="secondary" variant="outlined" sx={{ fontSize: 12, fontWeight: 600 }} />;
      default:
        return <Chip label={type} size="small" variant="outlined" sx={{ fontSize: 12 }} />;
    }
  };

  const addButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={handleOpenAdd} sx={{ fontWeight: 600, textTransform: "none" }}>
      {t("solutionMasterPage.addSolution")}
    </Button>
  );

  const tableColumns: RegisterColumn<SolutionMaster>[] = [
    {
      key: "name",
      label: t("solutionMasterPage.columns.name"),
      sortable: true,
      render: (sol) => (
        <>
          <Typography sx={{ fontWeight: 600, fontSize: "0.875rem" }}>{sol.name}</Typography>
          <Stack useFlexGap direction="row" spacing={1} sx={{ mt: 0.5, flexWrap: "wrap" }}>
            {sol.phTarget != null && (
              <Typography variant="caption" color="text.secondary">
                {sol.phTolerance != null
                  ? t("solutionMasterPage.phSpecTolerance", { target: sol.phTarget, tolerance: sol.phTolerance })
                  : t("solutionMasterPage.phSpec", { target: sol.phTarget })}
              </Typography>
            )}
            {sol.type === "Titrant" && sol.nominalStrength != null && (
              <Typography variant="caption" color="text.secondary">
                • {sol.nominalStrength} {sol.strengthUnit === "Molar" ? "M" : sol.strengthUnit === "MgWaterPerMl" ? "mg H2O/mL" : "N"} ({sol.standardizationMode === "PrimaryStandard" ? t("solutionMasterPage.primaryStandard") : t("solutionMasterPage.vsVolumetricSolution")})
              </Typography>
            )}
          </Stack>
          {(sol.sterilizationRequired || sol.releaseRequired) && (
            <Stack useFlexGap direction="row" spacing={0.5} sx={{ mt: 0.5, flexWrap: "wrap" }}>
              {sol.sterilizationRequired && (
                <Chip label={t("solutionMasterPage.autoclaveBadge")} size="small" variant="outlined" sx={{ fontSize: 11, height: 20 }} />
              )}
              {sol.releaseRequired && (
                <Chip label={t("solutionMasterPage.releaseBadge")} size="small" variant="outlined" sx={{ fontSize: 11, height: 20 }} />
              )}
            </Stack>
          )}
        </>
      )
    },
    { key: "type", label: t("solutionMasterPage.columns.type"), sortable: true, render: (sol) => renderTypeChip(sol.type) },
    {
      key: "shelfLife",
      label: t("solutionMasterPage.columns.shelfLife"),
      render: (sol) => (
        <Typography variant="body2" sx={{ fontWeight: 500 }}>
          {sol.shelfLifeValue} {sol.shelfLifeUnit}
        </Typography>
      )
    },
    {
      key: "finalVolumeMl",
      label: t("solutionMasterPage.columns.finalVolume"),
      sortable: true,
      sortValue: (sol) => Number(sol.finalVolumeMl),
      render: (sol) => (
        <Typography variant="body2" sx={{ fontWeight: 500 }}>
          {t("solutionMasterPage.finalVolumeValue", { value: formatCount(Number(sol.finalVolumeMl)) })}
        </Typography>
      )
    },
    {
      key: "components",
      label: t("solutionMasterPage.columns.components"),
      sortValue: (sol) => sol.components.length,
      render: (sol) => (
        <Tooltip
          arrow
          title={
            <Box sx={{ p: 0.5 }}>
              <Typography variant="caption" sx={{ fontWeight: 700, display: "block", mb: 0.5 }}>
                {t("solutionMasterPage.recipeBreakdown")}
              </Typography>
              {sol.components.map((c, i) => (
                <Typography key={c.id ?? i} variant="caption" sx={{ display: "block" }}>
                  {c.kind === "Text"
                    ? `• ${c.text}`
                    : c.kind === "PreparedSolution"
                      ? t("solutionMasterPage.preparedComponent", { name: c.sourceSolutionName, quantity: c.quantity, unit: c.unit })
                      : `• ${c.entryCode} - ${c.entryName}: ${c.quantity} ${c.unit}`}
                </Typography>
              ))}
            </Box>
          }
        >
          <Chip
            label={t("solutionMasterPage.componentsCount", { count: sol.components.length })}
            size="small"
            variant="outlined"
            sx={{ fontSize: 12, cursor: "pointer" }}
          />
        </Tooltip>
      )
    },
    {
      key: "section",
      label: t("solutionMasterPage.columns.section"),
      sortable: true,
      sortValue: (sol) => resolveSectionDisplay(sol.sectionId, sol.sectionName),
      render: (sol) => (
        <Chip label={resolveSectionDisplay(sol.sectionId, sol.sectionName)} size="small" variant="outlined" sx={{ fontSize: 12 }} />
      )
    },
    {
      key: "isActive",
      label: t("solutionMasterPage.columns.status"),
      sortable: true,
      sortValue: (sol) => (sol.isActive ? 0 : 1),
      render: (sol) => <StatusBadge status={sol.isActive ? "Active" : "Inactive"} label={sol.isActive ? t("common.active") : t("common.inactive")} />
    }
  ];

  const filtersActive =
    Boolean(searchQuery.trim()) || typeFilter !== "ALL" || statusFilter !== "ALL" || sectionFilter !== "ALL";

  return (
    <LabPage
      title={t("solutionMasterPage.title")}
      subtitle={t("solutionMasterPage.subtitle")}
      actions={addButton}
      filters={
        <FilterBar
          search={searchQuery}
          onSearch={setSearchQuery}
          placeholder={t("solutionMasterPage.searchPlaceholder")}
          resultCount={filteredSolutions.length}
          onRefresh={loadData}
          refreshing={loading}
        >
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="solution-type-filter-label">{t("solutionMasterPage.typeLabel")}</InputLabel>
            <Select
              labelId="solution-type-filter-label"
              label={t("solutionMasterPage.typeLabel")}
              value={typeFilter}
              onChange={(e) => setTypeFilter(e.target.value as "ALL" | SolutionType)}
            >
              <MenuItem value="ALL">{t("solutionMasterPage.allTypes")}</MenuItem>
              <MenuItem value="MobilePhase">{t("solutionMasterPage.types.MobilePhase")}</MenuItem>
              <MenuItem value="Diluent">{t("solutionMasterPage.types.Diluent")}</MenuItem>
              <MenuItem value="Reagent">{t("solutionMasterPage.types.Reagent")}</MenuItem>
              <MenuItem value="Neutralizer">{t("solutionMasterPage.types.Neutralizer")}</MenuItem>
              <MenuItem value="Titrant">{t("solutionMasterPage.types.Titrant")}</MenuItem>
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 150 }}>
            <InputLabel id="solution-status-filter-label">{t("common.status")}</InputLabel>
            <Select
              labelId="solution-status-filter-label"
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
            <InputLabel id="solution-section-filter-label">{t("common.section")}</InputLabel>
            <Select
              labelId="solution-section-filter-label"
              label={t("common.section")}
              value={sectionFilter}
              onChange={(e) => setSectionFilter(e.target.value)}
            >
              <MenuItem value="ALL">{t("common.allSections")}</MenuItem>
              {sections.map((sec) => (
                <MenuItem key={sec.sectionId} value={String(sec.sectionId)}>
                  {sec.sectionName}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        </FilterBar>
      }
    >
      {error && <Alert severity="error">{error}</Alert>}

      <RegisterTable
        columns={tableColumns}
        rows={filteredSolutions}
        getRowId={(sol) => sol.id}
        loading={loading}
        onRowClick={handleOpenEdit}
        rowActions={(sol) => [
          { label: t("common.edit"), onClick: () => handleOpenEdit(sol) },
          sol.isActive
            ? { label: t("common.deactivate"), onClick: () => handleOpenToggleActive(sol), danger: true }
            : { label: t("common.activate"), onClick: () => handleOpenToggleActive(sol) }
        ]}
        empty={
          filtersActive
            ? { title: t("solutionMasterPage.emptyFilteredTitle"), description: t("solutionMasterPage.emptyFilteredDesc") }
            : {
                title: t("solutionMasterPage.emptyTitle"),
                description: t("solutionMasterPage.emptyDesc"),
                action: addButton
              }
        }
      />

      {/* Add / Edit Floating Dialog */}
      <SolutionMasterDialog
        open={dialogOpen}
        editingEntry={editingEntry}
        solutions={solutions}
        materialMasters={materialMasters}
        sections={sections}
        mySections={mySections}
        onClose={() => setDialogOpen(false)}
        onSuccess={loadData}
        resolveSectionDisplay={resolveSectionDisplay}
      />

      {/* Activate / Deactivate Confirmation Dialog with Reason */}
      <ReasonDialog
        open={Boolean(solutionToToggle)}
        title={solutionToToggle?.isActive ? t("solutionMasterPage.deactivateTitle") : t("solutionMasterPage.activateTitle")}
        onClose={() => { if (!togglingActive) setSolutionToToggle(null); }}
        onConfirm={handleConfirmToggleActive}
        confirmText={solutionToToggle?.isActive ? t("solutionMasterPage.deactivateConfirm") : t("solutionMasterPage.activateConfirm")}
        confirmColor={solutionToToggle?.isActive ? "error" : "success"}
        loading={togglingActive}
        loadingText={t("common.updating")}
        error={toggleError}
        reason={toggleReason}
        onReasonChange={setToggleReason}
        label={t("solutionMasterPage.reasonLabel")}
        placeholder={solutionToToggle?.isActive ? t("solutionMasterPage.deactivatePlaceholder") : t("solutionMasterPage.activatePlaceholder")}
        disabled={!toggleReason.trim() || togglingActive}
      >
        <Alert severity={solutionToToggle?.isActive ? "warning" : "info"} sx={{ fontSize: 13 }}>
          {solutionToToggle?.isActive
            ? t("solutionMasterPage.deactivateMessage", { name: solutionToToggle?.name ?? "" })
            : t("solutionMasterPage.activateMessage", { name: solutionToToggle?.name ?? "" })}
        </Alert>
      </ReasonDialog>
    </LabPage>
  );
}
