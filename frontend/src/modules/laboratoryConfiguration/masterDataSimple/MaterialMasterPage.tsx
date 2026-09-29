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
  InputLabel,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TableContainer,
  Chip,
  Stack,
  IconButton,
  Tooltip,
  CircularProgress,
  Alert,
  Checkbox,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import BlockIcon from "@mui/icons-material/Block";
import RefreshIcon from "@mui/icons-material/Refresh";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import ColorLensOutlinedIcon from "@mui/icons-material/ColorLensOutlined";
import VerifiedOutlinedIcon from "@mui/icons-material/VerifiedOutlined";
import { PageHeader } from "../../../components/PageHeader";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { tableHeadSx } from "../../../theme";
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
  { value: "ReferenceStandard", label: "Reference Standard" }
];

export function MaterialMasterPage() {
  const theme = useTheme();
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
    setDialogOpen(true);
  };

  // Save Entry
  const handleSave = async () => {
    const trimmedCode = formCode.trim().toUpperCase();
    const trimmedName = formName.trim();
    const trimmedGrade = formGrade.trim();
    const trimmedSource = formSource.trim();

    if (!trimmedCode) {
      setDialogError("Code is required.");
      return;
    }
    if (!trimmedName) {
      setDialogError("Name is required.");
      return;
    }
    if (!formBaseUnit) {
      setDialogError("Base Unit is required.");
      return;
    }
    if (!editingEntry && mySections.length > 1 && !formSectionId) {
      setDialogError("Laboratory Section is required.");
      return;
    }

    if (formCategory === "Indicator") {
      const fromVal = formTransitionRangeFrom !== "" ? Number(formTransitionRangeFrom) : null;
      const toVal = formTransitionRangeTo !== "" ? Number(formTransitionRangeTo) : null;
      if (fromVal !== null && toVal !== null && fromVal > toVal) {
        setDialogError("Transition range 'from' must not be above 'to'.");
        return;
      }
    }

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
            sx={{ fontSize: 11, fontWeight: 600 }}
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
            sx={{ fontSize: 11, fontWeight: 600 }}
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
            sx={{ fontSize: 11, fontWeight: 600 }}
          />
        );
      default:
        return <Chip label={category} size="small" variant="outlined" sx={{ fontSize: 11 }} />;
    }
  };

  return (
    <Box sx={{ p: 3 }}>
      <PageHeader
        title="Reagents & Reference Standards"
        subtitle="Manage master definitions for chemical reagents, indicators, and reference standards across laboratory sections."
      />

      {error && (
        <Alert severity="error" sx={{ mb: 3 }}>
          {error}
        </Alert>
      )}

      {/* Filter & Action Toolbar */}
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{
            alignItems: { sm: "center" },
            justifyContent: "space-between",
            flexWrap: "wrap"
          }}
        >
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ flexWrap: "wrap", flex: 1 }}>
            <TextField
              size="small"
              placeholder="Search code, name, grade, source..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              sx={{ minWidth: 260 }}
            />

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

            <Tooltip title="Refresh">
              <IconButton onClick={loadData} size="small" sx={{ alignSelf: "center" }}>
                <RefreshIcon />
              </IconButton>
            </Tooltip>
          </Stack>

          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={handleOpenAdd}
            sx={{ fontWeight: 600, textTransform: "none", height: 40 }}
          >
            Add Entry
          </Button>
        </Stack>
      </Paper>

      {/* Material Master Table */}
      <TableContainer
        component={Paper}
        elevation={0}
        sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2 }}
      >
        <Table size="small">
          <TableHead>
            <TableRow sx={tableHeadSx(theme)}>
              <TableCell sx={{ fontWeight: 600 }}>Code</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Name</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Category</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Grade</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Source</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Base Unit</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Section</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
              <TableCell align="right" sx={{ fontWeight: 600 }}>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={9} align="center" sx={{ py: 6 }}>
                  <CircularProgress size={32} />
                  <Typography variant="body2" sx={{ mt: 1, color: "text.secondary" }}>
                    Loading reagents &amp; standards...
                  </Typography>
                </TableCell>
              </TableRow>
            ) : filteredEntries.length === 0 ? (
              <TableRow>
                <TableCell colSpan={9} align="center" sx={{ py: 6 }}>
                  <ScienceOutlinedIcon sx={{ fontSize: 40, color: "text.disabled", mb: 1 }} />
                  <Typography variant="body1" sx={{ color: "text.secondary", fontWeight: 500 }}>
                    No material master entries found
                  </Typography>
                  <Typography variant="body2" sx={{ color: "text.disabled", mt: 0.5 }}>
                    {searchQuery || categoryFilter !== "ALL" || statusFilter !== "ALL" || sectionFilter !== "ALL"
                      ? "Try adjusting your search or filters."
                      : "Click 'Add Entry' to register your first reagent, indicator, or reference standard."}
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              filteredEntries.map((entry) => (
                <TableRow key={entry.id} hover>
                  <TableCell sx={{ fontFamily: "monospace", fontWeight: 600, fontSize: "0.875rem" }}>
                    {entry.code}
                  </TableCell>
                  <TableCell sx={{ fontWeight: 500 }}>
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
                  </TableCell>
                  <TableCell>{renderCategoryChip(entry.category)}</TableCell>
                  <TableCell>{entry.grade || <Typography variant="body2" color="text.secondary">—</Typography>}</TableCell>
                  <TableCell>{entry.source || <Typography variant="body2" color="text.secondary">—</Typography>}</TableCell>
                  <TableCell>
                    <Chip label={entry.baseUnit} size="small" variant="outlined" sx={{ fontSize: 11 }} />
                  </TableCell>
                  <TableCell>
                    <Chip
                      label={resolveSectionDisplay(entry.sectionId, entry.sectionName)}
                      size="small"
                      variant="outlined"
                      sx={{ fontSize: 12 }}
                    />
                  </TableCell>
                  <TableCell>
                    <Chip
                      icon={entry.isActive ? <CheckCircleOutlineIcon fontSize="small" /> : <BlockIcon fontSize="small" />}
                      label={entry.isActive ? "Active" : "Inactive"}
                      size="small"
                      color={entry.isActive ? "success" : "default"}
                      sx={{ fontSize: 11, fontWeight: 600 }}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
                      <Tooltip title="Edit Entry">
                        <IconButton size="small" onClick={() => handleOpenEdit(entry)}>
                          <EditIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title={entry.isActive ? "Deactivate Entry" : "Activate Entry"}>
                        <IconButton
                          size="small"
                          color={entry.isActive ? "error" : "success"}
                          onClick={() => setEntryToToggle(entry)}
                        >
                          {entry.isActive ? <BlockIcon fontSize="small" /> : <CheckCircleOutlineIcon fontSize="small" />}
                        </IconButton>
                      </Tooltip>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Create / Edit Floating Dialog */}
      <FloatingDialog
        open={dialogOpen}
        title={editingEntry ? `Edit Master Entry: ${editingEntry.code}` : "Add Reagent or Standard Master"}
        onClose={() => setDialogOpen(false)}
        maxWidth="md"
        actions={
          <>
            <Button onClick={() => setDialogOpen(false)} disabled={saving} sx={{ textTransform: "none" }}>
              Cancel
            </Button>
            <Button
              variant="contained"
              onClick={handleSave}
              disabled={saving}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {saving ? "Saving..." : editingEntry ? "Save Changes" : "Create Entry"}
            </Button>
          </>
        }
      >
        <Stack spacing={2.5} sx={{ pt: 1 }}>
          {dialogError && (
            <Alert severity="error" onClose={() => setDialogError(null)}>
              {dialogError}
            </Alert>
          )}

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
            <TextField
              label="Code"
              value={formCode}
              onChange={(e) => setFormCode(e.target.value.toUpperCase())}
              required
              fullWidth
              size="small"
              placeholder="e.g. NAOH-01, USP-RS-ASP"
              helperText="Unique code within laboratory section (e.g. NAOH-01)"
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
              onChange={(e) => setFormName(e.target.value)}
              required
              fullWidth
              size="small"
              placeholder="e.g. Sodium Hydroxide Pellets"
              helperText="Descriptive chemical or standard name"
              sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
            />

            <FormControl fullWidth size="small" required>
              <InputLabel id="entry-base-unit-select-label">Base Unit</InputLabel>
              <Select
                labelId="entry-base-unit-select-label"
                label="Base Unit"
                value={formBaseUnit}
                onChange={(e) => setFormBaseUnit(e.target.value as MaterialUnit)}
              >
                {MATERIAL_UNITS.map((u) => (
                  <MenuItem key={u} value={u}>
                    {u}
                  </MenuItem>
                ))}
              </Select>
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
              <FormControl fullWidth size="small" required={!editingEntry && mySections.length > 1}>
                <InputLabel id="entry-form-section-label">Laboratory Section</InputLabel>
                <Select
                  labelId="entry-form-section-label"
                  label="Laboratory Section"
                  value={formSectionId}
                  onChange={(e) => setFormSectionId(e.target.value)}
                >
                  {(!editingEntry && mySections.length > 1 ? mySections : sections).map((sec) => (
                    <MenuItem key={sec.sectionId} value={sec.sectionId}>
                      {sec.sectionName}
                    </MenuItem>
                  ))}
                </Select>
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
                  onChange={(e) => setFormTransitionRangeFrom(e.target.value)}
                  size="small"
                  fullWidth
                  placeholder="e.g. 8.2"
                  slotProps={{ htmlInput: { step: "0.01" } }}
                  helperText="Lower pH limit"
                />
                <TextField
                  label="Transition Range (To)"
                  type="number"
                  value={formTransitionRangeTo}
                  onChange={(e) => setFormTransitionRangeTo(e.target.value)}
                  size="small"
                  fullWidth
                  placeholder="e.g. 10.0"
                  slotProps={{ htmlInput: { step: "0.01" } }}
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
        </Stack>
      </FloatingDialog>

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
    </Box>
  );
}
