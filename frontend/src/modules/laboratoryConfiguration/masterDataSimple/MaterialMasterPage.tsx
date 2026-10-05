import { useState, useEffect, useMemo, useCallback } from "react";
import {
  Box,
  Paper,
  Typography,
  Button,
  TextField,
  Select,
  MenuItem,
  FormControl,
  FormHelperText,
  InputLabel,
  Chip,
  Stack,
  Alert,
  Checkbox
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import ColorLensOutlinedIcon from "@mui/icons-material/ColorLensOutlined";
import VerifiedOutlinedIcon from "@mui/icons-material/VerifiedOutlined";
import { LabPage, FilterBar, RegisterTable, RegisterColumn, FormDialog } from "../../../components/lab";
import { StatusBadge } from "../../../components/StatusBadge";
import { monospaceFontFamily } from "../../../theme/palette";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { toast } from "sonner";
import {
  MaterialMasterService,
  MaterialMasterEntry,
  MaterialMasterCategory,
  SaveMaterialMasterEntryRequest
} from "./services/MaterialMasterService";
import type { MaterialUnit } from "../../inventory/materials/types/materialTypes";
import { useLaboratorySections } from "../../../hooks/useLaboratorySections";
import { getMySections, LaboratorySection } from "../../../services/laboratorySectionService";

const MATERIAL_UNITS: MaterialUnit[] = [
  "Gram",
  "Kilogram",
  "Milliliter",
  "Liter",
  "Disc",
  "Vial",
  "Kit",
  "Piece",
  "Bottle",
  "Pack"
];

const CATEGORY_OPTIONS: Array<{ value: MaterialMasterCategory; label: string }> = [
  { value: "Reagent", label: "Reagent" },
  { value: "Indicator", label: "Indicator" },
  { value: "ReferenceStandard", label: "Reference Standard" },
  { value: "PrimaryStandard", label: "Primary Standard" }
];

export function MaterialMasterPage() {
  const { sections, sectionName } = useLaboratorySections();

  const [entries, setEntries] = useState<MaterialMasterEntry[]>([]);
  const [mySections, setMySections] = useState<LaboratorySection[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [searchQuery, setSearchQuery] = useState("");
  const [categoryFilter, setCategoryFilter] = useState<"ALL" | MaterialMasterCategory>("ALL");
  const [statusFilter, setStatusFilter] = useState<"ALL" | "ACTIVE" | "INACTIVE">("ALL");
  const [sectionFilter, setSectionFilter] = useState<string>("ALL");

  // Create / Edit Dialog
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingEntry, setEditingEntry] = useState<MaterialMasterEntry | null>(null);
  const [formCode, setFormCode] = useState("");
  const [formName, setFormName] = useState("");
  const [formCategory, setFormCategory] = useState<MaterialMasterCategory>("Reagent");
  const [formGrade, setFormGrade] = useState("");
  const [formSource, setFormSource] = useState("");
  const [formBaseUnit, setFormBaseUnit] = useState<MaterialUnit>("Gram");
  const [formSectionId, setFormSectionId] = useState<string | number>("");
  const [formIsActive, setFormIsActive] = useState(true);

  // Indicator specific fields
  const [formWorkingConcentration, setFormWorkingConcentration] = useState("");
  const [formSolvent, setFormSolvent] = useState("");
  const [formTransitionRangeFrom, setFormTransitionRangeFrom] = useState<string | number>("");
  const [formTransitionRangeTo, setFormTransitionRangeTo] = useState<string | number>("");
  const [formColourChange, setFormColourChange] = useState("");
  const [formIndicatorUse, setFormIndicatorUse] = useState("");

  const [dialogError, setDialogError] = useState<string | null>(null);
  // Client-side validation errors, shown on the field instead of the top alert.
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<"code" | "name" | "unit" | "section" | "range", string>>>({});
  const [saving, setSaving] = useState(false);

  // Toggle Active (Activate / Deactivate) Confirmation Dialog
  const [entryToToggle, setEntryToToggle] = useState<MaterialMasterEntry | null>(null);
  const [togglingActive, setTogglingActive] = useState(false);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [entriesData, mySecData] = await Promise.all([
        MaterialMasterService.getAll(),
        getMySections().catch(() => [])
      ]);
      setEntries(Array.isArray(entriesData) ? entriesData : []);
      setMySections(Array.isArray(mySecData) ? mySecData : []);
    } catch (err: unknown) {
      console.error("Failed to load material master entries:", err);
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      setError(errorObj.response?.data?.message ?? errorObj.message ?? "Could not load material master entries.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Open Add Dialog
  const handleOpenAdd = () => {
    setEditingEntry(null);
    setFormCode("");
    setFormName("");
    setFormCategory("Reagent");
    setFormGrade("");
    setFormSource("");
    setFormBaseUnit("Gram");
    const defaultSecId = mySections.length === 1 ? mySections[0].sectionId : "";
    setFormSectionId(defaultSecId);
    setFormIsActive(true);
    setFormWorkingConcentration("");
    setFormSolvent("");
    setFormTransitionRangeFrom("");
    setFormTransitionRangeTo("");
    setFormColourChange("");
    setFormIndicatorUse("");
    setDialogError(null);
    setFieldErrors({});
    setDialogOpen(true);
  };

  // Open Edit Dialog
  const handleOpenEdit = (entry: MaterialMasterEntry) => {
    setEditingEntry(entry);
    setFormCode(entry.code);
    setFormName(entry.name);
    setFormCategory(entry.category);
    setFormGrade(entry.grade ?? "");
    setFormSource(entry.source ?? "");
    setFormBaseUnit(entry.baseUnit);
    setFormSectionId(entry.sectionId ?? "");
    setFormIsActive(entry.isActive);
    setFormWorkingConcentration(entry.workingConcentration ?? "");
    setFormSolvent(entry.solvent ?? "");
    setFormTransitionRangeFrom(entry.transitionRangeFrom ?? "");
    setFormTransitionRangeTo(entry.transitionRangeTo ?? "");
    setFormColourChange(entry.colourChange ?? "");
    setFormIndicatorUse(entry.indicatorUse ?? "");
    setDialogError(null);
    setFieldErrors({});
    setDialogOpen(true);
  };

  // Save Entry
  const handleSave = async () => {
    const trimmedCode = formCode.trim().toUpperCase();
    const trimmedName = formName.trim();
    const trimmedGrade = formGrade.trim();
    const trimmedSource = formSource.trim();

    const errors: typeof fieldErrors = {};
    if (!trimmedCode) errors.code = "Code is required.";
    if (!trimmedName) errors.name = "Name is required.";
    if (!formBaseUnit) errors.unit = "Base Unit is required.";
    if (!editingEntry && mySections.length > 1 && !formSectionId) errors.section = "Laboratory Section is required.";

    if (formCategory === "Indicator") {
      const fromVal = formTransitionRangeFrom !== "" ? Number(formTransitionRangeFrom) : null;
      const toVal = formTransitionRangeTo !== "" ? Number(formTransitionRangeTo) : null;
      if (fromVal !== null && toVal !== null && fromVal > toVal) {
        errors.range = "Transition range 'from' must not be above 'to'.";
      }
    }
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    const payload: SaveMaterialMasterEntryRequest = {
      code: trimmedCode,
      name: trimmedName,
      category: formCategory,
      grade: trimmedGrade || null,
      source: trimmedSource || null,
      baseUnit: formBaseUnit,
      sectionId: formSectionId ? Number(formSectionId) : null,
      workingConcentration: formCategory === "Indicator" && formWorkingConcentration.trim() ? formWorkingConcentration.trim() : null,
      solvent: formCategory === "Indicator" && formSolvent.trim() ? formSolvent.trim() : null,
      transitionRangeFrom: formCategory === "Indicator" && formTransitionRangeFrom !== "" ? Number(formTransitionRangeFrom) : null,
      transitionRangeTo: formCategory === "Indicator" && formTransitionRangeTo !== "" ? Number(formTransitionRangeTo) : null,
      colourChange: formCategory === "Indicator" && formColourChange.trim() ? formColourChange.trim() : null,
      indicatorUse: formCategory === "Indicator" && formIndicatorUse.trim() ? formIndicatorUse.trim() : null
    };

    setSaving(true);
    setDialogError(null);
    try {
      if (editingEntry) {
        await MaterialMasterService.update(editingEntry.id, payload, editingEntry.version);
        if (editingEntry.isActive !== formIsActive) {
          await MaterialMasterService.setActive(editingEntry.id, formIsActive);
        }
        toast.success(`Material master entry "${trimmedCode}" updated.`);
      } else {
        await MaterialMasterService.create(payload);
        toast.success(`Material master entry "${trimmedCode}" created.`);
      }
      setDialogOpen(false);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      const msg = errorObj.response?.data?.message ?? errorObj.message ?? "Could not save material master entry.";
      setDialogError(msg);
    } finally {
      setSaving(false);
    }
  };

  // Confirm Activate / Deactivate Toggle
  const handleConfirmToggleActive = async () => {
    if (!entryToToggle) return;
    const nextActive = !entryToToggle.isActive;
    setTogglingActive(true);
    try {
      await MaterialMasterService.setActive(entryToToggle.id, nextActive);
      toast.success(`Entry "${entryToToggle.code}" ${nextActive ? "activated" : "deactivated"}.`);
      setEntryToToggle(null);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      toast.error(errorObj.response?.data?.message ?? errorObj.message ?? "Could not update entry status.");
    } finally {
      setTogglingActive(false);
    }
  };

  // Filtered list
  const filteredEntries = useMemo(() => {
    return entries.filter((entry) => {
      // Search
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase().trim();
        const codeMatch = entry.code.toLowerCase().includes(q);
        const nameMatch = entry.name.toLowerCase().includes(q);
        const gradeMatch = entry.grade ? entry.grade.toLowerCase().includes(q) : false;
        const sourceMatch = entry.source ? entry.source.toLowerCase().includes(q) : false;
        if (!codeMatch && !nameMatch && !gradeMatch && !sourceMatch) return false;
      }

      // Category
      if (categoryFilter !== "ALL" && entry.category !== categoryFilter) {
        return false;
      }

      // Status
      if (statusFilter === "ACTIVE" && !entry.isActive) return false;
      if (statusFilter === "INACTIVE" && entry.isActive) return false;

      // Section
      if (sectionFilter !== "ALL" && String(entry.sectionId) !== sectionFilter) {
        return false;
      }

      return true;
    });
  }, [entries, searchQuery, categoryFilter, statusFilter, sectionFilter]);

  const resolveSectionDisplay = (secId: number, secNameProp?: string) => {
    if (secNameProp) return secNameProp;
    const fromHook = sectionName(secId);
    if (fromHook) return fromHook;
    const foundSec = sections.find((s) => s.sectionId === secId);
    if (foundSec?.sectionName) return foundSec.sectionName;
    return `Section #${secId}`;
  };

  const renderCategoryChip = (category: MaterialMasterCategory) => {
    switch (category) {
      case "Reagent":
        return (
          <Chip
            icon={<ScienceOutlinedIcon fontSize="small" />}
            label="Reagent"
            size="small"
            color="primary"
            variant="outlined"
            sx={{ fontSize: 12, fontWeight: 600 }}
          />
        );
      case "Indicator":
        return (
          <Chip
            icon={<ColorLensOutlinedIcon fontSize="small" />}
            label="Indicator"
            size="small"
            color="secondary"
            variant="outlined"
            sx={{ fontSize: 12, fontWeight: 600 }}
          />
        );
      case "ReferenceStandard":
        return (
          <Chip
            icon={<VerifiedOutlinedIcon fontSize="small" />}
            label="Reference Standard"
            size="small"
            color="info"
            variant="outlined"
            sx={{ fontSize: 12, fontWeight: 600 }}
          />
        );
      case "PrimaryStandard":
        return (
          <Chip
            icon={<VerifiedOutlinedIcon fontSize="small" />}
            label="Primary Standard"
            size="small"
            color="success"
            variant="outlined"
            sx={{ fontSize: 12, fontWeight: 600 }}
          />
        );
      default:
        return <Chip label={category} size="small" variant="outlined" sx={{ fontSize: 12 }} />;
    }
  };

  const addButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={handleOpenAdd} sx={{ fontWeight: 600, textTransform: "none" }}>
      Add Entry
    </Button>
  );

  const tableColumns: RegisterColumn<MaterialMasterEntry>[] = [
    {
      key: "code",
      label: "Code",
      sortable: true,
      render: (entry) => (
        <Typography component="span" sx={{ fontFamily: monospaceFontFamily, fontWeight: 600, fontSize: "0.875rem" }}>{entry.code}</Typography>
      )
    },
    {
      key: "name",
      label: "Name",
      sortable: true,
      render: (entry) => (
        <>
          <div>{entry.name}</div>
          {entry.category === "Indicator" && (entry.colourChange || entry.transitionRangeFrom != null || entry.indicatorUse) && (
            <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
              {[
                entry.transitionRangeFrom != null || entry.transitionRangeTo != null
                  ? `pH ${entry.transitionRangeFrom ?? "?"}–${entry.transitionRangeTo ?? "?"}`
                  : null,
                entry.colourChange,
                entry.indicatorUse
              ].filter(Boolean).join(" · ")}
            </Typography>
          )}
        </>
      )
    },
    { key: "category", label: "Category", sortable: true, render: (entry) => renderCategoryChip(entry.category) },
    {
      key: "grade",
      label: "Grade",
      sortable: true,
      render: (entry) => entry.grade || <Typography variant="body2" color="text.secondary">—</Typography>
    },
    {
      key: "source",
      label: "Source",
      sortable: true,
      render: (entry) => entry.source || <Typography variant="body2" color="text.secondary">—</Typography>
    },
    {
      key: "baseUnit",
      label: "Base Unit",
      sortable: true,
      render: (entry) => <Chip label={entry.baseUnit} size="small" variant="outlined" sx={{ fontSize: 12 }} />
    },
    {
      key: "section",
      label: "Section",
      sortable: true,
      sortValue: (entry) => resolveSectionDisplay(entry.sectionId, entry.sectionName),
      render: (entry) => (
        <Chip label={resolveSectionDisplay(entry.sectionId, entry.sectionName)} size="small" variant="outlined" sx={{ fontSize: 12 }} />
      )
    },
    {
      key: "isActive",
      label: "Status",
      sortable: true,
      sortValue: (entry) => (entry.isActive ? 0 : 1),
      render: (entry) => <StatusBadge status={entry.isActive ? "Active" : "Inactive"} />
    }
  ];

  const filtersActive =
    Boolean(searchQuery.trim()) || categoryFilter !== "ALL" || statusFilter !== "ALL" || sectionFilter !== "ALL";

  return (
    <LabPage
      title="Reagents & Reference Standards"
      subtitle="Manage master definitions for chemical reagents, indicators, and reference standards across laboratory sections."
      actions={addButton}
      filters={
        <FilterBar
          search={searchQuery}
          onSearch={setSearchQuery}
          placeholder="Search code, name, grade, source..."
          resultCount={filteredEntries.length}
          onRefresh={loadData}
          refreshing={loading}
        >
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="entry-category-filter-label">Category</InputLabel>
            <Select
              labelId="entry-category-filter-label"
              label="Category"
              value={categoryFilter}
              onChange={(e) => setCategoryFilter(e.target.value as "ALL" | MaterialMasterCategory)}
            >
              <MenuItem value="ALL">All Categories</MenuItem>
              <MenuItem value="Reagent">Reagent</MenuItem>
              <MenuItem value="Indicator">Indicator</MenuItem>
              <MenuItem value="ReferenceStandard">Reference Standard</MenuItem>
            </Select>
          </FormControl>

          <FormControl size="small" sx={{ minWidth: 150 }}>
            <InputLabel id="entry-status-filter-label">Status</InputLabel>
            <Select
              labelId="entry-status-filter-label"
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
            <InputLabel id="entry-section-filter-label">Section</InputLabel>
            <Select
              labelId="entry-section-filter-label"
              label="Section"
              value={sectionFilter}
              onChange={(e) => setSectionFilter(e.target.value)}
            >
              <MenuItem value="ALL">All Sections</MenuItem>
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
        rows={filteredEntries}
        getRowId={(entry) => entry.id}
        loading={loading}
        onRowClick={handleOpenEdit}
        rowActions={(entry) => [
          { label: "Edit", onClick: () => handleOpenEdit(entry) },
          entry.isActive
            ? { label: "Deactivate", onClick: () => setEntryToToggle(entry), danger: true }
            : { label: "Activate", onClick: () => setEntryToToggle(entry) }
        ]}
        empty={
          filtersActive
            ? { title: "No material master entries found", description: "Try adjusting your search or filters." }
            : {
                title: "No material master entries found",
                description: "Register your first reagent, indicator, or reference standard.",
                action: addButton
              }
        }
      />

      {/* Create / Edit Floating Dialog */}
      <FormDialog
        open={dialogOpen}
        title={editingEntry ? `Edit Master Entry: ${editingEntry.code}` : "Add Reagent or Standard Master"}
        onClose={() => setDialogOpen(false)}
        onSubmit={handleSave}
        submitLabel={saving ? "Saving..." : editingEntry ? "Save Changes" : "Create Entry"}
        submitting={saving}
        error={dialogError}
        maxWidth="md"
      >
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
            <TextField
              label="Code"
              value={formCode}
              onChange={(e) => { setFieldErrors((fe) => ({ ...fe, code: undefined })); setFormCode(e.target.value.toUpperCase()); }}
              required
              fullWidth
              size="small"
              placeholder="e.g. NAOH-01, USP-RS-ASP"
              error={!!fieldErrors.code}
              helperText={fieldErrors.code ?? "Unique code within laboratory section (e.g. NAOH-01)"}
            />

            <FormControl fullWidth size="small" required>
              <InputLabel id="entry-category-select-label">Category</InputLabel>
              <Select
                labelId="entry-category-select-label"
                label="Category"
                value={formCategory}
                onChange={(e) => setFormCategory(e.target.value as MaterialMasterCategory)}
              >
                {CATEGORY_OPTIONS.map((c) => (
                  <MenuItem key={c.value} value={c.value}>
                    {c.label}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <TextField
              label="Name"
              value={formName}
              onChange={(e) => { setFieldErrors((fe) => ({ ...fe, name: undefined })); setFormName(e.target.value); }}
              required
              fullWidth
              size="small"
              placeholder="e.g. Sodium Hydroxide Pellets"
              error={!!fieldErrors.name}
              helperText={fieldErrors.name ?? "Descriptive chemical or standard name"}
              sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
            />

            <FormControl fullWidth size="small" required error={!!fieldErrors.unit}>
              <InputLabel id="entry-base-unit-select-label">Base Unit</InputLabel>
              <Select
                labelId="entry-base-unit-select-label"
                label="Base Unit"
                value={formBaseUnit}
                onChange={(e) => { setFieldErrors((fe) => ({ ...fe, unit: undefined })); setFormBaseUnit(e.target.value as MaterialUnit); }}
              >
                {MATERIAL_UNITS.map((u) => (
                  <MenuItem key={u} value={u}>
                    {u}
                  </MenuItem>
                ))}
              </Select>
              {fieldErrors.unit && <FormHelperText>{fieldErrors.unit}</FormHelperText>}
            </FormControl>

            <TextField
              label="Grade"
              value={formGrade}
              onChange={(e) => setFormGrade(e.target.value)}
              fullWidth
              size="small"
              placeholder="e.g. AR, HPLC Grade, ACS"
              helperText="Chemical purity or grade"
            />

            <TextField
              label="Source / Pharmacopoeia"
              value={formSource}
              onChange={(e) => setFormSource(e.target.value)}
              fullWidth
              size="small"
              placeholder="e.g. USP, EP, BP, In-house"
              helperText="Standard source or manufacturer"
            />

            {/* Laboratory Section Select */}
            {(!editingEntry && mySections.length > 1) || (editingEntry && sections.length > 0) ? (
              <FormControl fullWidth size="small" required={!editingEntry && mySections.length > 1} error={!!fieldErrors.section}>
                <InputLabel id="entry-form-section-label">Laboratory Section</InputLabel>
                <Select
                  labelId="entry-form-section-label"
                  label="Laboratory Section"
                  value={formSectionId}
                  onChange={(e) => { setFieldErrors((fe) => ({ ...fe, section: undefined })); setFormSectionId(e.target.value); }}
                >
                  {(!editingEntry && mySections.length > 1 ? mySections : sections).map((sec) => (
                    <MenuItem key={sec.sectionId} value={sec.sectionId}>
                      {sec.sectionName}
                    </MenuItem>
                  ))}
                </Select>
                {fieldErrors.section && <FormHelperText>{fieldErrors.section}</FormHelperText>}
              </FormControl>
            ) : null}
          </Box>

          {/* Indicator specific fields */}
          {formCategory === "Indicator" && (
            <Paper variant="outlined" sx={{ p: 2, bgcolor: "background.default", borderRadius: 1.5 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.primary" }}>
                Indicator Specifications
              </Typography>
              <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
                <TextField
                  label="Working Concentration"
                  value={formWorkingConcentration}
                  onChange={(e) => setFormWorkingConcentration(e.target.value)}
                  size="small"
                  fullWidth
                  placeholder="e.g. 0.1% w/v, 1% in ethanol"
                />
                <TextField
                  label="Solvent"
                  value={formSolvent}
                  onChange={(e) => setFormSolvent(e.target.value)}
                  size="small"
                  fullWidth
                  placeholder="e.g. Ethanol 96%, Purified Water"
                />
                <TextField
                  label="Transition Range (From)"
                  type="number"
                  value={formTransitionRangeFrom}
                  onChange={(e) => { setFieldErrors((fe) => ({ ...fe, range: undefined })); setFormTransitionRangeFrom(e.target.value); }}
                  size="small"
                  fullWidth
                  placeholder="e.g. 8.2"
                  slotProps={{ htmlInput: { step: "0.01" } }}
                  error={!!fieldErrors.range}
                  helperText={fieldErrors.range ?? "Lower pH limit"}
                />
                <TextField
                  label="Transition Range (To)"
                  type="number"
                  value={formTransitionRangeTo}
                  onChange={(e) => { setFieldErrors((fe) => ({ ...fe, range: undefined })); setFormTransitionRangeTo(e.target.value); }}
                  size="small"
                  fullWidth
                  placeholder="e.g. 10.0"
                  slotProps={{ htmlInput: { step: "0.01" } }}
                  error={!!fieldErrors.range}
                  helperText="Upper pH limit"
                />
                <TextField
                  label="Colour Change"
                  value={formColourChange}
                  onChange={(e) => setFormColourChange(e.target.value)}
                  size="small"
                  fullWidth
                  placeholder="e.g. Colourless to Pink"
                />
                <TextField
                  label="Indicator Use"
                  value={formIndicatorUse}
                  onChange={(e) => setFormIndicatorUse(e.target.value)}
                  size="small"
                  fullWidth
                  placeholder="e.g. Acid-Base Titration"
                />
              </Box>
            </Paper>
          )}

          {editingEntry && (
            <FormControl size="small">
              <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                <Checkbox
                  checked={formIsActive}
                  onChange={(e) => setFormIsActive(e.target.checked)}
                  id="entry-is-active-check"
                />
                <Typography
                  component="label"
                  htmlFor="entry-is-active-check"
                  variant="body2"
                  sx={{ cursor: "pointer", fontWeight: 500 }}
                >
                  Entry is Active
                </Typography>
              </Stack>
            </FormControl>
          )}
      </FormDialog>

      {/* Confirmation Dialog for Activate / Deactivate */}
      <ConfirmationDialog
        open={Boolean(entryToToggle)}
        title={entryToToggle?.isActive ? "Deactivate Material Master Entry" : "Activate Material Master Entry"}
        message={
          entryToToggle?.isActive
            ? `Are you sure you want to deactivate "${entryToToggle.code} - ${entryToToggle.name}"? Inactive entries cannot be selected for new stock lots.`
            : `Are you sure you want to activate "${entryToToggle?.code} - ${entryToToggle?.name}"?`
        }
        destructive={entryToToggle?.isActive}
        confirmText={togglingActive ? "Saving..." : entryToToggle?.isActive ? "Deactivate" : "Activate"}
        onConfirm={handleConfirmToggleActive}
        onCancel={() => setEntryToToggle(null)}
      />
    </LabPage>
  );
}
