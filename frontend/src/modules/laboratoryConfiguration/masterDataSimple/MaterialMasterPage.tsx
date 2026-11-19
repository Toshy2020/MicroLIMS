import { useState, useEffect, useMemo, useCallback } from "react";
import type { ClipboardEvent as ReactClipboardEvent } from "react";
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
import CategoryOutlinedIcon from "@mui/icons-material/CategoryOutlined";
import ContentPasteIcon from "@mui/icons-material/ContentPaste";
import Inventory2OutlinedIcon from "@mui/icons-material/Inventory2Outlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import ColorLensOutlinedIcon from "@mui/icons-material/ColorLensOutlined";
import VerifiedOutlinedIcon from "@mui/icons-material/VerifiedOutlined";
import { LabPage, FilterBar, RegisterTable, RegisterColumn, FormDialog } from "../../../components/lab";
import { StatusBadge } from "../../../components/StatusBadge";
import { monospaceFontFamily } from "../../../theme/palette";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { parseClipboardTable } from "../../../components/entry/parseClipboardTable";
import { PastePreviewDialog, type PasteAcceptedRow } from "../../../components/entry/PastePreviewDialog";
import { PendingBatchList, type PendingBatchColumn } from "../../../components/entry/PendingBatchList";
import { extractBatchSaveErrors } from "../../../utils/fieldErrors";
import { toast } from "sonner";
import { useTranslation } from "../../../i18n";
import { RecordChangedBanner } from "../../../components/RecordChangedBanner";
import { noteOwnSave } from "../../../hooks/useRecordChanged";
import {
  MaterialMasterService,
  MaterialMasterEntry,
  MaterialMasterCategory,
  SaveMaterialMasterEntryRequest,
  MATERIAL_MASTER_CODE_PREFIX
} from "./services/MaterialMasterService";
import { MaterialCategoryService, MaterialCategory } from "./services/MaterialCategoryService";
import { ChangeMaterialMasterCodeDialog } from "./ChangeMaterialMasterCodeDialog";
import { MaterialCategoriesDialog } from "./MaterialCategoriesDialog";
import {
  buildMaterialMasterPastePlan,
  toPendingMaterialMasterEntries,
  toSaveMaterialMasterEntryPayload,
  type PendingMaterialMasterEntry
} from "./materialMasterPaste";
import type { MaterialUnit } from "../../inventory/materials/types/materialTypes";
import { useLaboratorySections } from "../../../hooks/useLaboratorySections";
import { getMySections, LaboratorySection } from "../../../services/laboratorySectionService";

export const MATERIAL_UNITS: MaterialUnit[] = [
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

export const CATEGORY_OPTIONS: Array<{ value: MaterialMasterCategory; label: string }> = [
  { value: "Reagent", label: "Reagent" },
  { value: "Indicator", label: "Indicator" },
  { value: "ReferenceStandard", label: "Reference Standard" },
  { value: "PrimaryStandard", label: "Primary Standard" },
  { value: "Other", label: "Other material" }
];

const getPendingColumnByKey = (
  t: ReturnType<typeof useTranslation>["t"]
): Record<string, PendingBatchColumn<PendingMaterialMasterEntry>> => ({
  code: {
    key: "code",
    label: t("materialMasterPaste.columns.code"),
    sx: { fontFamily: monospaceFontFamily, fontWeight: 600 },
    render: (r) => r.code
  },
  category: { key: "category", label: t("materialMasterPaste.columns.category"), render: (r) => r.categoryLabel },
  materialCategory: {
    key: "materialCategory",
    label: t("materialMasterPaste.columns.materialCategory"),
    render: (r) => r.materialCategoryName || t("common.dash")
  },
  name: { key: "name", label: t("materialMasterPaste.columns.name"), render: (r) => r.name },
  baseUnit: { key: "baseUnit", label: t("materialMasterPaste.columns.baseUnit"), render: (r) => r.baseUnit },
  grade: { key: "grade", label: t("materialMasterPaste.columns.grade"), render: (r) => r.grade || t("common.dash") },
  source: { key: "source", label: t("materialMasterPaste.columns.source"), render: (r) => r.source || t("common.dash") },
  section: { key: "section", label: t("materialMasterPaste.columns.section"), render: (r) => r.sectionName || t("common.dash") },
  workingConcentration: {
    key: "workingConcentration",
    label: t("materialMasterPaste.columns.workingConcentration"),
    render: (r) => r.workingConcentration || t("common.dash")
  },
  solvent: { key: "solvent", label: t("materialMasterPaste.columns.solvent"), render: (r) => r.solvent || t("common.dash") },
  transitionRangeFrom: {
    key: "transitionRangeFrom",
    label: t("materialMasterPaste.columns.transitionRangeFrom"),
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => (r.transitionRangeFrom !== null ? String(r.transitionRangeFrom) : t("common.dash"))
  },
  transitionRangeTo: {
    key: "transitionRangeTo",
    label: t("materialMasterPaste.columns.transitionRangeTo"),
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => (r.transitionRangeTo !== null ? String(r.transitionRangeTo) : t("common.dash"))
  },
  colourChange: { key: "colourChange", label: t("materialMasterPaste.columns.colourChange"), render: (r) => r.colourChange || t("common.dash") },
  indicatorUse: { key: "indicatorUse", label: t("materialMasterPaste.columns.indicatorUse"), render: (r) => r.indicatorUse || t("common.dash") }
});

// Other materials take the prefix of their material category; the rest have a fixed one.
function codePrefixFor(category: MaterialMasterCategory, materialCategory: MaterialCategory | undefined): string | null {
  if (category === "Other") return materialCategory ? `${materialCategory.codePrefix}-` : null;
  return MATERIAL_MASTER_CODE_PREFIX[category];
}

interface MaterialMasterPageProps {
  // Limits the page to these categories (a Master Testing Materials tab); all categories when omitted.
  categories?: MaterialMasterCategory[];
  // Hosted in the Master Testing Materials page, which has the title.
  embedded?: boolean;
}

export function MaterialMasterPage({ categories, embedded }: MaterialMasterPageProps = {}) {
  const { t } = useTranslation("masterData");
  const { sections, sectionName } = useLaboratorySections();
  const categoryOptions = useMemo(
    () =>
      (categories ? CATEGORY_OPTIONS.filter((c) => categories.includes(c.value)) : CATEGORY_OPTIONS).map((c) => ({
        value: c.value,
        label: t(`materialMaster.categories.${c.value}`, { defaultValue: c.label })
      })),
    [categories, t]
  );
  const defaultCategory = categoryOptions[0]?.value ?? "Reagent";

  const [entries, setEntries] = useState<MaterialMasterEntry[]>([]);
  const [mySections, setMySections] = useState<LaboratorySection[]>([]);
  const [materialCategories, setMaterialCategories] = useState<MaterialCategory[]>([]);
  const [categoriesOpen, setCategoriesOpen] = useState(false);
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
  const [formMaterialCategoryId, setFormMaterialCategoryId] = useState<number | "">("");
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
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<"code" | "name" | "unit" | "section" | "range" | "materialCategory", string>>>({});
  const [saving, setSaving] = useState(false);

  // Toggle Active (Activate / Deactivate) Confirmation Dialog
  const [entryToToggle, setEntryToToggle] = useState<MaterialMasterEntry | null>(null);
  const [togglingActive, setTogglingActive] = useState(false);

  // Signed code change (entries with stock)
  const [entryToRecode, setEntryToRecode] = useState<MaterialMasterEntry | null>(null);

  // Paste batch state
  const [pasteBarOpen, setPasteBarOpen] = useState(false);
  const [pastePreviewCells, setPastePreviewCells] = useState<string[][] | null>(null);
  const [pendingEntries, setPendingEntries] = useState<PendingMaterialMasterEntry[]>([]);
  const [savingBatch, setSavingBatch] = useState(false);
  const [batchError, setBatchError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [entriesData, mySecData, categoriesData] = await Promise.all([
        MaterialMasterService.getAll(),
        getMySections().catch(() => []),
        MaterialCategoryService.getAll().catch(() => [] as MaterialCategory[])
      ]);
      setEntries(Array.isArray(entriesData) ? entriesData : []);
      setMaterialCategories(categoriesData);
      setMySections(Array.isArray(mySecData) ? mySecData : []);
    } catch (err: unknown) {
      console.error("Failed to load material master entries:", err);
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      setError(errorObj.response?.data?.message ?? errorObj.message ?? t("materialMaster.loadError"));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const selectedMaterialCategory = materialCategories.find((c) => c.id === formMaterialCategoryId);
  const formPrefix = codePrefixFor(formCategory, selectedMaterialCategory);

  // Active categories offered to the entry's owner lab (a category with no sections is offered to every lab);
  // an entry being edited keeps showing its current category even if it has since been deactivated.
  const ownerSectionId = editingEntry ? editingEntry.sectionId : formSectionId ? Number(formSectionId) : null;
  const pickableCategories = materialCategories.filter(
    (c) =>
      c.id === formMaterialCategoryId ||
      (c.isActive && (ownerSectionId == null || c.sectionIds.length === 0 || c.sectionIds.includes(ownerSectionId)))
  );

  // Open Add Dialog
  const handleOpenAdd = () => {
    setEditingEntry(null);
    setFormCode("");
    setFormName("");
    setFormCategory(defaultCategory);
    setFormMaterialCategoryId("");
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

  const entryFormValues = (entry: MaterialMasterEntry) => [
    entry.code,
    entry.name,
    entry.category,
    entry.materialCategoryId ?? "",
    entry.grade ?? "",
    entry.source ?? "",
    entry.baseUnit,
    entry.sectionId ?? "",
    entry.isActive,
    entry.workingConcentration ?? "",
    entry.solvent ?? "",
    entry.transitionRangeFrom ?? "",
    entry.transitionRangeTo ?? "",
    entry.colourChange ?? "",
    entry.indicatorUse ?? ""
  ];
  const editFormIsDirty = editingEntry
    ? JSON.stringify([
        formCode, formName, formCategory, formMaterialCategoryId, formGrade, formSource, formBaseUnit, formSectionId,
        formIsActive, formWorkingConcentration, formSolvent, formTransitionRangeFrom, formTransitionRangeTo,
        formColourChange, formIndicatorUse
      ]) !== JSON.stringify(entryFormValues(editingEntry))
    : false;

  // Reload from the "changed by someone else" banner: replaces the record, its form values and the
  // version the next save sends.
  const reloadEditingEntry = async () => {
    if (!editingEntry) return;
    try {
      const fresh = await MaterialMasterService.getById(editingEntry.id);
      fillFormFromEntry(fresh);
      setEditingEntry(fresh);
      setDialogError(null);
      setFieldErrors({});
    } catch {
      toast.error(t("materialMaster.reloadError"));
    }
  };

  const fillFormFromEntry = (entry: MaterialMasterEntry) => {
    setFormCode(entry.code);
    setFormName(entry.name);
    setFormCategory(entry.category);
    setFormMaterialCategoryId(entry.materialCategoryId ?? "");
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
  };

  // Open Edit Dialog
  const handleOpenEdit = (entry: MaterialMasterEntry) => {
    setEditingEntry(entry);
    fillFormFromEntry(entry);
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
    if (formCategory === "Other" && !formMaterialCategoryId) errors.materialCategory = t("materialMaster.form.materialCategoryRequired");
    const prefix = codePrefixFor(formCategory, selectedMaterialCategory);
    if (!trimmedCode) errors.code = t("materialMaster.form.codeRequired");
    // Prefix applies to new codes and changed codes; an unchanged older code is grandfathered.
    else if (prefix && (!editingEntry || trimmedCode !== editingEntry.code) && !trimmedCode.startsWith(prefix)) {
      errors.code = t("materialMaster.form.codePrefixError", {
        category: categoryOptions.find((c) => c.value === formCategory)?.label ?? formCategory,
        prefix
      });
    }
    if (!trimmedName) errors.name = t("materialMaster.form.nameRequired");
    if (!formBaseUnit) errors.unit = t("materialMaster.form.baseUnitRequired");
    if (!editingEntry && mySections.length > 1 && !formSectionId) errors.section = t("materialMaster.form.ownerLabRequired");

    if (formCategory === "Indicator") {
      const fromVal = formTransitionRangeFrom !== "" ? Number(formTransitionRangeFrom) : null;
      const toVal = formTransitionRangeTo !== "" ? Number(formTransitionRangeTo) : null;
      if (fromVal !== null && toVal !== null && fromVal > toVal) {
        errors.range = t("materialMaster.form.transitionRangeError");
      }
    }
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    const payload: SaveMaterialMasterEntryRequest = {
      code: trimmedCode,
      name: trimmedName,
      category: formCategory,
      materialCategoryId: formCategory === "Other" ? Number(formMaterialCategoryId) : null,
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
        noteOwnSave("materialMasterEntry", editingEntry.id);
        if (editingEntry.isActive !== formIsActive) {
          await MaterialMasterService.setActive(editingEntry.id, formIsActive);
        }
        toast.success(t("materialMaster.updatedToast", { code: trimmedCode }));
      } else {
        await MaterialMasterService.create(payload);
        toast.success(t("materialMaster.createdToast", { code: trimmedCode }));
      }
      setDialogOpen(false);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      const msg = errorObj.response?.data?.message ?? errorObj.message ?? t("materialMaster.saveError");
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
      toast.success(
        nextActive
          ? t("materialMaster.toggledToast_activated", { code: entryToToggle.code })
          : t("materialMaster.toggledToast_deactivated", { code: entryToToggle.code })
      );
      setEntryToToggle(null);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      toast.error(errorObj.response?.data?.message ?? errorObj.message ?? t("materialMaster.toggleError"));
    } finally {
      setTogglingActive(false);
    }
  };

  // Filtered list
  const filteredEntries = useMemo(() => {
    return entries.filter((entry) => {
      if (categories && !categories.includes(entry.category)) return false;

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
  }, [entries, categories, searchQuery, categoryFilter, statusFilter, sectionFilter]);

  const resolveSectionDisplay = (secId: number, secNameProp?: string) => {
    if (secNameProp) return secNameProp;
    const fromHook = sectionName(secId);
    if (fromHook) return fromHook;
    const foundSec = sections.find((s) => s.sectionId === secId);
    if (foundSec?.sectionName) return foundSec.sectionName;
    return t("materialMaster.sectionFallback", { id: secId });
  };

  const renderCategoryChip = (category: MaterialMasterCategory) => {
    const label = t(`materialMaster.categories.${category}`, { defaultValue: category });
    switch (category) {
      case "Reagent":
        return (
          <Chip
            icon={<ScienceOutlinedIcon fontSize="small" />}
            label={label}
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
            label={label}
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
            label={label}
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
            label={label}
            size="small"
            color="success"
            variant="outlined"
            sx={{ fontSize: 12, fontWeight: 600 }}
          />
        );
      case "Other":
        return (
          <Chip
            icon={<Inventory2OutlinedIcon fontSize="small" />}
            label={label}
            size="small"
            color="warning"
            variant="outlined"
            sx={{ fontSize: 12, fontWeight: 600 }}
          />
        );
      default:
        return <Chip label={label} size="small" variant="outlined" sx={{ fontSize: 12 }} />;
    }
  };

  const pastePlan = useMemo(
    () =>
      buildMaterialMasterPastePlan({
        entries,
        categoryOptions,
        categories,
        materialCategories,
        mySections,
        t
      }),
    [entries, categoryOptions, categories, materialCategories, mySections, t]
  );

  const pendingColumnByKey = useMemo(() => getPendingColumnByKey(t), [t]);
  const pendingColumns = useMemo(
    () =>
      pastePlan.columns
        .map((c) => pendingColumnByKey[c.key])
        .filter((c): c is PendingBatchColumn<PendingMaterialMasterEntry> => Boolean(c)),
    [pastePlan.columns, pendingColumnByKey]
  );

  const anyModalOpen =
    dialogOpen ||
    categoriesOpen ||
    Boolean(entryToToggle) ||
    Boolean(entryToRecode) ||
    pastePreviewCells !== null;

  useEffect(() => {
    if (anyModalOpen || !pasteBarOpen) return;
    const handleDocumentPaste = (e: ClipboardEvent) => {
      if (e.defaultPrevented) return;
      const target = e.target as HTMLElement | null;
      if (
        target &&
        (target.tagName === "INPUT" ||
          target.tagName === "TEXTAREA" ||
          target.tagName === "SELECT" ||
          target.isContentEditable)
      ) {
        return;
      }
      const text = e.clipboardData?.getData("text/plain") ?? "";
      const table = parseClipboardTable(text);
      if (table.length === 0) return;
      e.preventDefault();
      setPastePreviewCells(table);
    };
    document.addEventListener("paste", handleDocumentPaste);
    return () => document.removeEventListener("paste", handleDocumentPaste);
  }, [anyModalOpen, pasteBarOpen]);

  const handlePasteInput = (e: ReactClipboardEvent<HTMLElement>) => {
    if (!pasteBarOpen) return;
    const table = parseClipboardTable(e.clipboardData.getData("text/plain"));
    if (table.length === 0) return;
    e.preventDefault();
    e.stopPropagation();
    setPastePreviewCells(table);
  };

  const handleAcceptPaste = (rows: PasteAcceptedRow[]) => {
    setPendingEntries(
      toPendingMaterialMasterEntries(rows, { categoryOptions, materialCategories, mySections, t })
    );
    setBatchError(null);
    setPastePreviewCells(null);
    setPasteBarOpen(false);
  };

  const saveEntriesBatch = async () => {
    if (pendingEntries.length === 0) return;
    setSavingBatch(true);
    setBatchError(null);
    try {
      const payload = pendingEntries.map(toSaveMaterialMasterEntryPayload);
      await MaterialMasterService.createBatch(payload);
      const count = pendingEntries.length;
      setPendingEntries([]);
      toast.success(t("materialMaster.batchAddedToast", { count }));
      await loadData();
    } catch (e: unknown) {
      const { summary, rowErrors } = extractBatchSaveErrors(
        e,
        t("materialMaster.batchSaveError")
      );
      if (rowErrors) {
        setPendingEntries((prev) => prev.map((row, idx) => ({ ...row, errors: rowErrors[idx] })));
      }
      setBatchError(summary);
    } finally {
      setSavingBatch(false);
    }
  };

  const addButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={handleOpenAdd} sx={{ fontWeight: 600, textTransform: "none" }}>
      {t("materialMaster.addEntry")}
    </Button>
  );

  const pasteButton = (
    <Button
      variant="outlined"
      startIcon={<ContentPasteIcon />}
      aria-expanded={pasteBarOpen}
      onClick={() => setPasteBarOpen((v) => !v)}
      sx={{ fontWeight: 600, textTransform: "none" }}
    >
      {t("materialMaster.pasteEntries")}
    </Button>
  );

  const pageActions = (
    <>
      {(!categories || categories.includes("Other")) && (
        <Button variant="outlined" startIcon={<CategoryOutlinedIcon />} onClick={() => setCategoriesOpen(true)} sx={{ fontWeight: 600, textTransform: "none" }}>
          {t("materialMaster.materialCategoriesButton")}
        </Button>
      )}
      {pasteButton}
      {addButton}
    </>
  );

  const columnLabels = pastePlan.columns.map((c) => c.label).join(", ");
  const ownerNote = mySections.length === 1
    ? t("materialMaster.pasteHelperOwnerNote", { sectionName: mySections[0].sectionName })
    : "";
  const pasteHelperText = t("materialMaster.pasteHelperBase", { columns: columnLabels, ownerNote });

  const tableColumns: RegisterColumn<MaterialMasterEntry>[] = [
    {
      key: "code",
      label: t("materialMaster.columns.code"),
      sortable: true,
      render: (entry) => (
        <Typography component="span" sx={{ fontFamily: monospaceFontFamily, fontWeight: 600, fontSize: "0.875rem" }}>{entry.code}</Typography>
      )
    },
    {
      key: "name",
      label: t("materialMaster.columns.name"),
      sortable: true,
      render: (entry) => (
        <>
          <div>{entry.name}</div>
          {entry.category === "Indicator" && (entry.colourChange || entry.transitionRangeFrom != null || entry.indicatorUse) && (
            <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
              {[
                entry.transitionRangeFrom != null || entry.transitionRangeTo != null
                  ? t("materialMaster.phRange", { from: entry.transitionRangeFrom ?? "?", to: entry.transitionRangeTo ?? "?" })
                  : null,
                entry.colourChange,
                entry.indicatorUse
              ].filter(Boolean).join(" · ")}
            </Typography>
          )}
        </>
      )
    },
    { key: "category", label: t("materialMaster.columns.category"), sortable: true, render: (entry) => renderCategoryChip(entry.category) },
    {
      key: "materialCategory",
      label: t("materialMaster.columns.materialCategory"),
      sortable: true,
      sortValue: (entry) => entry.materialCategoryName ?? "",
      render: (entry) => entry.materialCategoryName || <Typography variant="body2" color="text.secondary">{t("common.dash")}</Typography>
    },
    {
      key: "grade",
      label: t("materialMaster.columns.grade"),
      sortable: true,
      render: (entry) => entry.grade || <Typography variant="body2" color="text.secondary">{t("common.dash")}</Typography>
    },
    {
      key: "source",
      label: t("materialMaster.columns.source"),
      sortable: true,
      render: (entry) => entry.source || <Typography variant="body2" color="text.secondary">{t("common.dash")}</Typography>
    },
    {
      key: "baseUnit",
      label: t("materialMaster.columns.baseUnit"),
      sortable: true,
      render: (entry) => <Chip label={entry.baseUnit} size="small" variant="outlined" sx={{ fontSize: 12 }} />
    },
    {
      key: "section",
      label: t("materialMaster.columns.ownerLab"),
      sortable: true,
      sortValue: (entry) => resolveSectionDisplay(entry.sectionId, entry.sectionName),
      render: (entry) => (
        <Chip label={resolveSectionDisplay(entry.sectionId, entry.sectionName)} size="small" variant="outlined" sx={{ fontSize: 12 }} />
      )
    },
    {
      key: "isActive",
      label: t("materialMaster.columns.status"),
      sortable: true,
      sortValue: (entry) => (entry.isActive ? 0 : 1),
      render: (entry) => <StatusBadge status={entry.isActive ? "Active" : "Inactive"} label={entry.isActive ? t("common.active") : t("common.inactive")} />
    }
  ];

  const filtersActive =
    Boolean(searchQuery.trim()) || categoryFilter !== "ALL" || statusFilter !== "ALL" || sectionFilter !== "ALL";

  return (
    <LabPage
      title={t("materialMaster.title")}
      subtitle={t("materialMaster.subtitle")}
      actions={pageActions}
      embedded={embedded}
      filters={
        <FilterBar
          search={searchQuery}
          onSearch={setSearchQuery}
          placeholder={t("materialMaster.searchPlaceholder")}
          resultCount={filteredEntries.length}
          onRefresh={loadData}
          refreshing={loading}
        >
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="entry-category-filter-label">{t("materialMaster.categoryFilterLabel")}</InputLabel>
            <Select
              labelId="entry-category-filter-label"
              label={t("materialMaster.categoryFilterLabel")}
              value={categoryFilter}
              onChange={(e) => setCategoryFilter(e.target.value as "ALL" | MaterialMasterCategory)}
            >
              <MenuItem value="ALL">{t("materialMaster.allCategories")}</MenuItem>
              {categoryOptions.map((c) => (
                <MenuItem key={c.value} value={c.value}>{c.label}</MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl size="small" sx={{ minWidth: 150 }}>
            <InputLabel id="entry-status-filter-label">{t("common.status")}</InputLabel>
            <Select
              labelId="entry-status-filter-label"
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
            <InputLabel id="entry-section-filter-label">{t("materialMaster.ownerLabLabel")}</InputLabel>
            <Select
              labelId="entry-section-filter-label"
              label={t("materialMaster.ownerLabLabel")}
              value={sectionFilter}
              onChange={(e) => setSectionFilter(e.target.value)}
            >
              <MenuItem value="ALL">{t("materialMaster.allLabs")}</MenuItem>
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

      {pasteBarOpen && (
        <Paper variant="outlined" sx={{ p: 2, borderRadius: 2 }}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: { sm: "flex-start" } }}>
            <TextField
              autoFocus
              size="small"
              fullWidth
              label={t("materialMaster.pasteLabel")}
              placeholder={t("materialMaster.pastePlaceholder", { columns: columnLabels })}
              value=""
              onChange={() => {}}
              onPaste={handlePasteInput}
              helperText={pasteHelperText}
            />
            <Button onClick={() => setPasteBarOpen(false)} sx={{ textTransform: "none", flexShrink: 0 }}>
              {t("common.close")}
            </Button>
          </Stack>
        </Paper>
      )}

      <PendingBatchList
        title={t("materialMaster.pendingTitle", { count: pendingEntries.length })}
        subtitle={t("materialMaster.pendingSubtitle", { count: pendingEntries.length })}
        ariaLabel={t("materialMaster.pendingAria")}
        columns={pendingColumns}
        rows={pendingEntries}
        getRowKey={(r, i) => `${r.code}-${i}`}
        getRowLabel={(r) => (r.name ? `${r.code} (${r.name})` : r.code)}
        saving={savingBatch}
        error={batchError}
        onSave={saveEntriesBatch}
        onDiscard={() => {
          setPendingEntries([]);
          setBatchError(null);
        }}
        onRemoveRow={(index) => {
          setPendingEntries((prev) => prev.filter((_, i) => i !== index));
          setBatchError(null);
        }}
      />

      <RegisterTable
        columns={tableColumns}
        rows={filteredEntries}
        getRowId={(entry) => entry.id}
        loading={loading}
        onRowClick={(entry) => { if (entry.canEdit) handleOpenEdit(entry); }}
        rowActions={(entry) => [
          { label: t("common.edit"), onClick: () => handleOpenEdit(entry), disabled: !entry.canEdit },
          ...(entry.hasStock ? [{ label: t("materialMaster.changeCodeAction"), onClick: () => setEntryToRecode(entry), disabled: !entry.canEdit }] : []),
          entry.isActive
            ? { label: t("common.deactivate"), onClick: () => setEntryToToggle(entry), danger: true, disabled: !entry.canEdit }
            : { label: t("common.activate"), onClick: () => setEntryToToggle(entry), disabled: !entry.canEdit }
        ]}
        empty={
          filtersActive
            ? { title: t("materialMaster.emptyFilteredTitle"), description: t("materialMaster.emptyFilteredDesc") }
            : {
                title: t("materialMaster.emptyTitle"),
                description: t("materialMaster.emptyDesc"),
                action: addButton
              }
        }
      />

      {/* Create / Edit Floating Dialog */}
      <FormDialog
        open={dialogOpen}
        title={editingEntry ? t("materialMaster.editDialogTitle", { code: editingEntry.code }) : t("materialMaster.addDialogTitle")}
        onClose={() => setDialogOpen(false)}
        onSubmit={handleSave}
        submitLabel={saving ? t("materialMaster.saving") : editingEntry ? t("common.saveChanges") : t("materialMaster.createEntry")}
        submitting={saving}
        error={dialogError}
        maxWidth="md"
      >
          {dialogOpen && editingEntry && (
            <RecordChangedBanner
              type="materialMasterEntry"
              id={editingEntry.id}
              dirty={editFormIsDirty}
              onReload={() => void reloadEditingEntry()}
            />
          )}
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
            <TextField
              label={t("materialMaster.form.codeLabel")}
              value={formCode}
              onChange={(e) => { setFieldErrors((fe) => ({ ...fe, code: undefined })); setFormCode(e.target.value.toUpperCase()); }}
              required
              fullWidth
              size="small"
              placeholder={formPrefix ? t("materialMaster.form.codePlaceholderPrefix", { prefix: formPrefix }) : t("materialMaster.form.codePlaceholderFirst")}
              disabled={Boolean(editingEntry?.hasStock)}
              error={!!fieldErrors.code}
              helperText={
                fieldErrors.code ??
                (editingEntry?.hasStock
                  ? t("materialMaster.form.codeFrozenHelper")
                  : formPrefix
                  ? t("materialMaster.form.codePrefixHelper", { prefix: formPrefix })
                  : t("materialMaster.form.codeCategoryHelper"))
              }
            />

            <FormControl fullWidth size="small" required>
              <InputLabel id="entry-category-select-label">{t("materialMaster.form.categoryLabel")}</InputLabel>
              <Select
                labelId="entry-category-select-label"
                label={t("materialMaster.form.categoryLabel")}
                value={formCategory}
                onChange={(e) => {
                  setFormCategory(e.target.value as MaterialMasterCategory);
                  setFormMaterialCategoryId("");
                  setFieldErrors((fe) => ({ ...fe, materialCategory: undefined }));
                }}
              >
                {categoryOptions.map((c) => (
                  <MenuItem key={c.value} value={c.value}>
                    {c.label}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            {formCategory === "Other" && (
              <FormControl fullWidth size="small" required error={!!fieldErrors.materialCategory} sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
                <InputLabel id="entry-material-category-label">{t("materialMaster.form.materialCategoryLabel")}</InputLabel>
                <Select<number | "">
                  labelId="entry-material-category-label"
                  label={t("materialMaster.form.materialCategoryLabel")}
                  value={formMaterialCategoryId}
                  onChange={(e) => {
                    const id = e.target.value === "" ? "" : Number(e.target.value);
                    setFieldErrors((fe) => ({ ...fe, materialCategory: undefined }));
                    setFormMaterialCategoryId(id);
                    // A new entry starts on the category's default unit; the unit stays editable.
                    const picked = materialCategories.find((c) => c.id === id);
                    if (picked && !editingEntry) setFormBaseUnit(picked.defaultUnit);
                  }}
                >
                  {pickableCategories.map((c) => (
                    <MenuItem key={c.id} value={c.id}>
                      {c.name} ({c.codePrefix}-){c.isActive ? "" : t("materialMaster.form.materialCategoryInactive")}
                    </MenuItem>
                  ))}
                </Select>
                <FormHelperText>
                  {fieldErrors.materialCategory ??
                    (pickableCategories.length === 0
                      ? t("materialMaster.form.materialCategoryNone")
                      : t("materialMaster.form.materialCategoryHelper"))}
                </FormHelperText>
              </FormControl>
            )}

            <TextField
              label={t("materialMaster.form.nameLabel")}
              value={formName}
              onChange={(e) => { setFieldErrors((fe) => ({ ...fe, name: undefined })); setFormName(e.target.value); }}
              required
              fullWidth
              size="small"
              placeholder={t("materialMaster.form.namePlaceholder")}
              error={!!fieldErrors.name}
              helperText={fieldErrors.name ?? t("materialMaster.form.nameHelper")}
              sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
            />

            <FormControl fullWidth size="small" required error={!!fieldErrors.unit}>
              <InputLabel id="entry-base-unit-select-label">{t("materialMaster.form.baseUnitLabel")}</InputLabel>
              <Select
                labelId="entry-base-unit-select-label"
                label={t("materialMaster.form.baseUnitLabel")}
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
              label={t("materialMaster.form.gradeLabel")}
              value={formGrade}
              onChange={(e) => setFormGrade(e.target.value)}
              fullWidth
              size="small"
              placeholder={t("materialMaster.form.gradePlaceholder")}
              helperText={t("materialMaster.form.gradeHelper")}
            />

            <TextField
              label={t("materialMaster.form.sourceLabel")}
              value={formSource}
              onChange={(e) => setFormSource(e.target.value)}
              fullWidth
              size="small"
              placeholder={t("materialMaster.form.sourcePlaceholder")}
              helperText={t("materialMaster.form.sourceHelper")}
            />

            {/* Laboratory Section Select */}
            {(!editingEntry && mySections.length > 1) || (editingEntry && sections.length > 0) ? (
              <FormControl fullWidth size="small" required={!editingEntry && mySections.length > 1} error={!!fieldErrors.section}>
                <InputLabel id="entry-form-section-label">{t("materialMaster.form.ownerLabLabel")}</InputLabel>
                <Select
                  labelId="entry-form-section-label"
                  label={t("materialMaster.form.ownerLabLabel")}
                  value={formSectionId}
                  disabled={Boolean(editingEntry)}
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
                {t("materialMaster.form.indicatorSectionTitle")}
              </Typography>
              <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
                <TextField
                  label={t("materialMaster.form.workingConcentrationLabel")}
                  value={formWorkingConcentration}
                  onChange={(e) => setFormWorkingConcentration(e.target.value)}
                  size="small"
                  fullWidth
                  placeholder={t("materialMaster.form.workingConcentrationPlaceholder")}
                />
                <TextField
                  label={t("materialMaster.form.solventLabel")}
                  value={formSolvent}
                  onChange={(e) => setFormSolvent(e.target.value)}
                  size="small"
                  fullWidth
                  placeholder={t("materialMaster.form.solventPlaceholder")}
                />
                <TextField
                  label={t("materialMaster.form.transitionFromLabel")}
                  type="number"
                  value={formTransitionRangeFrom}
                  onChange={(e) => { setFieldErrors((fe) => ({ ...fe, range: undefined })); setFormTransitionRangeFrom(e.target.value); }}
                  size="small"
                  fullWidth
                  placeholder={t("materialMaster.form.transitionFromPlaceholder")}
                  slotProps={{ htmlInput: { step: "0.01" } }}
                  error={!!fieldErrors.range}
                  helperText={fieldErrors.range ?? t("materialMaster.form.transitionFromHelper")}
                />
                <TextField
                  label={t("materialMaster.form.transitionToLabel")}
                  type="number"
                  value={formTransitionRangeTo}
                  onChange={(e) => { setFieldErrors((fe) => ({ ...fe, range: undefined })); setFormTransitionRangeTo(e.target.value); }}
                  size="small"
                  fullWidth
                  placeholder={t("materialMaster.form.transitionToPlaceholder")}
                  slotProps={{ htmlInput: { step: "0.01" } }}
                  error={!!fieldErrors.range}
                  helperText={t("materialMaster.form.transitionToHelper")}
                />
                <TextField
                  label={t("materialMaster.form.colourChangeLabel")}
                  value={formColourChange}
                  onChange={(e) => setFormColourChange(e.target.value)}
                  size="small"
                  fullWidth
                  placeholder={t("materialMaster.form.colourChangePlaceholder")}
                />
                <TextField
                  label={t("materialMaster.form.indicatorUseLabel")}
                  value={formIndicatorUse}
                  onChange={(e) => setFormIndicatorUse(e.target.value)}
                  size="small"
                  fullWidth
                  placeholder={t("materialMaster.form.indicatorUsePlaceholder")}
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
                  {t("materialMaster.form.isActiveLabel")}
                </Typography>
              </Stack>
            </FormControl>
          )}
      </FormDialog>

      <ChangeMaterialMasterCodeDialog
        open={Boolean(entryToRecode)}
        entry={entryToRecode}
        categoryPrefix={materialCategories.find((c) => c.id === entryToRecode?.materialCategoryId)?.codePrefix}
        onClose={() => setEntryToRecode(null)}
        onSuccess={() => { toast.success(t("materialMaster.codeChangedToast")); loadData(); }}
      />

      <MaterialCategoriesDialog
        open={categoriesOpen}
        sections={sections}
        onClose={() => setCategoriesOpen(false)}
        onChanged={loadData}
      />

      {/* Confirmation Dialog for Activate / Deactivate */}
      <ConfirmationDialog
        open={Boolean(entryToToggle)}
        title={entryToToggle?.isActive ? t("materialMaster.deactivateTitle") : t("materialMaster.activateTitle")}
        message={
          entryToToggle?.isActive
            ? t("materialMaster.deactivateMessage", { code: entryToToggle.code, name: entryToToggle.name })
            : t("materialMaster.activateMessage", { code: entryToToggle?.code ?? "", name: entryToToggle?.name ?? "" })
        }
        destructive={entryToToggle?.isActive}
        confirmText={togglingActive ? t("materialMaster.saving") : entryToToggle?.isActive ? t("common.deactivate") : t("common.activate")}
        onConfirm={handleConfirmToggleActive}
        onCancel={() => setEntryToToggle(null)}
      />

      {pastePreviewCells && (
        <PastePreviewDialog
          open
          plan={pastePlan}
          cells={pastePreviewCells}
          onAccept={handleAcceptPaste}
          onCancel={() => setPastePreviewCells(null)}
        />
      )}
    </LabPage>
  );
}
