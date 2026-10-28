import { useEffect, useMemo, useState, useCallback } from "react";
import {
  Button,
  Box,
  TextField,
  Select,
  MenuItem,
  ListSubheader,
  FormControl,
  FormHelperText,
  InputLabel,
  Typography,
  Divider,
  Alert,
  Paper,
  useTheme,
  Autocomplete
} from "@mui/material";
import { MediaProductPicker } from "../../../../components/MediaProductPicker";
import type { MediaProductOption } from "../../../../hooks/useMediaProducts";
import { MaterialService, SaveMaterialPayload } from "../services/MaterialService";
import { EquipmentInventoryService } from "../../equipment/services/EquipmentInventoryService";
import {
  MaterialFormState,
  MaterialItem,
  MaterialType,
  MaterialUnit,
  MATERIAL_UNITS,
  MATERIAL_TYPE_LABELS,
  LEGACY_MATERIAL_TYPES
} from "../types/materialTypes";
import { brandColors } from "../../../../theme";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { LoadFailuresAlert } from "../../../../components/LoadErrorAlert";
import { useLoadFailures } from "../../../../hooks/useLoadFailures";
import { getMySections, LaboratorySection } from "../../../../services/laboratorySectionService";
import {
  MaterialMasterService,
  MaterialMasterEntry,
  MaterialMasterCategory
} from "../../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService";
import {
  MaterialCategoryService,
  MaterialCategory
} from "../../../laboratoryConfiguration/masterDataSimple/services/MaterialCategoryService";
import { ReferenceOrganismService, ReferenceOrganism } from "../../../laboratoryConfiguration/masterDataSimple/services/ReferenceOrganismService";
import { DateField } from "../../../../components/DateField";

// Types a new lot can still be received under. Supplements, discs, kits and the like are now
// categories of Other materials; WorkingStandard lots come from qualifications only.
const NEW_LOT_TYPES = (Object.keys(MATERIAL_TYPE_LABELS) as MaterialType[]).filter(
  (t) => t !== "WorkingStandard" && !LEGACY_MATERIAL_TYPES.includes(t)
);

const typeOptionLabel = (type: MaterialType): string => (type === "Other" ? "Other material" : MATERIAL_TYPE_LABELS[type] ?? type);

const isMasterLinkedType = (type: MaterialType): boolean =>
  type === "Chemical" || type === "Indicator" || type === "ReferenceStandard" || type === "PrimaryStandard" || type === "Other";

const getMasterCategoryForType = (type: MaterialType): MaterialMasterCategory | null => {
  switch (type) {
    case "Chemical":
      return "Reagent";
    case "Indicator":
      return "Indicator";
    case "ReferenceStandard":
      return "ReferenceStandard";
    case "PrimaryStandard":
      return "PrimaryStandard";
    case "Other":
      return "Other";
    default:
      return null;
  }
};

interface AddMaterialDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: (message: string) => void;
  editingItem: MaterialItem | null;
}

interface StorageEquipmentOption {
  id: number;
  code: string;
  instrumentType?: string | null;
  manufacturerName?: string | null;
  location?: string | null;
  status?: string | number | null;
  sectionId?: number | null;
}

const INITIAL_FORM: MaterialFormState = {
  materialType: "DehydratedMedia",
  customType: "",
  materialName: "",
  manufacturerName: "",
  batchNumber: "",
  receivingDate: new Date().toISOString().slice(0, 10),
  expiryDate: "",
  code: "",
  location: "",
  storageCondition: "",
  quantityReceived: "",
  unit: "Gram",
  minimumStockLevel: "",
  purity: "",
  moisturePercent: "",
  atccNumber: "",
  organismId: null,
  referenceOrganismId: null,
  mediaProductId: null,
  materialMasterEntryId: null
};

// The stored location for a material kept in a piece of equipment, e.g.
// "Refrigerator, Thermo (EQ-12)". Records saved before October 2026 used an
// em dash between type and manufacturer; those still match their equipment,
// and are only rewritten if someone changes the location.
function equipmentLocationTag(eq: StorageEquipmentOption): string {
  return `${eq.instrumentType}, ${eq.manufacturerName} (${eq.code})`;
}

function isEquipmentLocation(value: string | undefined, eq: StorageEquipmentOption): boolean {
  return value === equipmentLocationTag(eq) || value === `${eq.instrumentType} \u2014 ${eq.manufacturerName} (${eq.code})`;
}

export function AddMaterialDialog({ open, onClose, onSuccess, editingItem }: AddMaterialDialogProps) {
  const theme = useTheme();
  const [form, setForm] = useState<MaterialFormState>(INITIAL_FORM);
  const [masterEntries, setMasterEntries] = useState<MaterialMasterEntry[]>([]);
  const [masterEntriesLoading, setMasterEntriesLoading] = useState(false);
  const [typeOptionsData, setTypeOptionsData] = useState<{
    builtIn: MaterialType[];
    custom: string[];
  } | null>(null);
  const [mySections, setMySections] = useState<LaboratorySection[]>([]);
  // Lyophilized lots are received against a reference strain.
  const [strains, setStrains] = useState<ReferenceOrganism[]>([]);
  // Other materials: the category chosen at receipt and the categories offered to the lot's lab.
  const [categories, setCategories] = useState<MaterialCategory[]>([]);
  const [categoriesLoading, setCategoriesLoading] = useState(false);
  const [categoryId, setCategoryId] = useState<number | "">("");
  // Without this, a failed storage-equipment load showed "No in-service
  // refrigerator configured" - a statement about the lab, not the network.
  const { failed: failedLists, fail, reset: resetFailures } = useLoadFailures();
  const [selectedSectionId, setSelectedSectionId] = useState<number | "">("");
  const [equipmentList, setEquipmentList] = useState<StorageEquipmentOption[]>([]);
  const [equipmentLoading, setEquipmentLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [hasSetInitialType, setHasSetInitialType] = useState(false);

  useEffect(() => {
    if (editingItem) {
      setForm({
        materialType: editingItem.materialType,
        customType: editingItem.customType ?? "",
        materialName: editingItem.materialName,
        manufacturerName: editingItem.manufacturerName ?? "",
        batchNumber: editingItem.batchNumber,
        receivingDate: editingItem.receivingDate?.slice(0, 10) ?? "",
        expiryDate: editingItem.expiryDate?.slice(0, 10) ?? "",
        code: editingItem.code ?? "",
        location: editingItem.location,
        storageCondition: editingItem.storageCondition ?? "",
        quantityReceived: editingItem.quantityReceived,
        unit: editingItem.unit,
        minimumStockLevel: editingItem.minimumStockLevel ?? "",
        purity: editingItem.purity != null ? editingItem.purity : "",
        moisturePercent: editingItem.moisturePercent != null ? editingItem.moisturePercent : "",
        atccNumber: editingItem.atccNumber ?? "",
        organismId: editingItem.organismId ?? null,
        referenceOrganismId: editingItem.referenceOrganismId ?? null,
        mediaProductId: editingItem.mediaProductId ?? null,
        materialMasterEntryId: editingItem.materialMasterEntryId ?? null
      });
      setSelectedSectionId("");
    } else {
      setForm({
        ...INITIAL_FORM,
        receivingDate: new Date().toISOString().slice(0, 10)
      });
      setSelectedSectionId("");
    }
    setCategoryId("");
    setError(null);
    setHasSetInitialType(false);

    if (open) {
      resetFailures();
      getMySections()
        .then((secs) => {
          setMySections(secs);
          if (!editingItem) {
            if (secs.length === 1) {
              setSelectedSectionId(secs[0].sectionId);
            } else {
              setSelectedSectionId("");
            }
          }
        })
        .catch(fail("your laboratory sections", () => setMySections([])));

      setEquipmentLoading(true);
      EquipmentInventoryService.getAll()
        .then((data: StorageEquipmentOption[]) => setEquipmentList(data || []))
        .catch(fail("storage equipment", () => setEquipmentList([])))
        .finally(() => setEquipmentLoading(false));
    }
  }, [editingItem, open, fail, resetFailures]);

  const activeSectionId = editingItem
    ? editingItem.sectionId
    : selectedSectionId !== ""
    ? Number(selectedSectionId)
    : undefined;

  const activeSection = mySections.find((s) => s.sectionId === activeSectionId);

  const roomStorageOption = activeSection
    ? activeSection.sectionCode === "MICRO"
      ? "Microbiology Lab"
      : activeSection.sectionCode === "FP"
      ? "Physicochemical Lab"
      : activeSection.sectionName
    : null;

  // Eligible storage equipment: Status = InService (filtered by active lab when known)
  const inServiceEquipment = equipmentList.filter(
    (e) =>
      (e.status === "InService" || e.status === 0 || e.status === "0") &&
      (activeSectionId != null ? e.sectionId === activeSectionId || e.sectionId == null : true)
  );

  const refrigerators = inServiceEquipment.filter((e) =>
    (e.instrumentType || "").toLowerCase().includes("refrigerator")
  );

  const deepFreezers = inServiceEquipment.filter((e) =>
    (e.instrumentType || "").toLowerCase().includes("deep freezer")
  );

  const freezers = inServiceEquipment.filter(
    (e) =>
      (e.instrumentType || "").toLowerCase().includes("freezer") &&
      !(e.instrumentType || "").toLowerCase().includes("deep freezer")
  );

  const selectedEquipment = inServiceEquipment.find((eq) => {
    return (
      isEquipmentLocation(form.location, eq) ||
      form.location === eq.code ||
      (form.location && form.location.includes(eq.code))
    );
  });

  const isFallbackLocation = Boolean(
    form.location &&
      form.location !== roomStorageOption &&
      !inServiceEquipment.some(
        (eq) => isEquipmentLocation(form.location, eq)
      )
  );
  const hasOtherOptions = Boolean(roomStorageOption || isFallbackLocation);

  useEffect(() => {
    // An Other lot already received shows its category and entry read-only: nothing to pick.
    if (!open || !isMasterLinkedType(form.materialType) || (form.materialType === "Other" && editingItem)) {
      setMasterEntries([]);
      return;
    }

    const category = getMasterCategoryForType(form.materialType);
    if (!category) return;

    let canceled = false;
    setMasterEntriesLoading(true);

    const activeOnly = !(editingItem && editingItem.materialMasterEntryId);

    MaterialMasterService.getAll(category, activeOnly)
      .then((data) => {
        if (canceled) return;
        setMasterEntries(Array.isArray(data) ? data : []);
      })
      .catch((err) => {
        console.error("Failed to load material master entries:", err);
        if (!canceled) setMasterEntries([]);
      })
      .finally(() => {
        if (!canceled) setMasterEntriesLoading(false);
      });

    return () => {
      canceled = true;
    };
  }, [open, form.materialType, editingItem]);

  // Master entries are app-wide: any lab can receive stock against any active entry.
  // An Other lot picks its category first, so only that category's entries are offered.
  const availableMasterEntries =
    form.materialType === "Other" ? masterEntries.filter((e) => e.materialCategoryId === categoryId) : masterEntries;

  // Categories offered to the lot's lab (the server filters by lab and active state).
  useEffect(() => {
    if (!open || editingItem || form.materialType !== "Other" || activeSectionId == null) {
      setCategories([]);
      return;
    }
    let canceled = false;
    setCategoriesLoading(true);
    MaterialCategoryService.getAll(true, activeSectionId)
      .then((data) => {
        if (!canceled) setCategories(data);
      })
      .catch(() => {
        if (!canceled) setCategories([]);
      })
      .finally(() => {
        if (!canceled) setCategoriesLoading(false);
      });
    return () => {
      canceled = true;
    };
  }, [open, editingItem, form.materialType, activeSectionId]);

  const selectedCategory = categories.find((c) => c.id === categoryId) ?? null;

  // The category's rules prefill the receipt (editable); the server enforces them either way.
  const handleCategoryChange = (id: number | "") => {
    setCategoryId(id);
    const picked = categories.find((c) => c.id === id);
    setForm((f) => ({
      ...f,
      materialMasterEntryId: null,
      materialName: "",
      code: "",
      unit: picked?.defaultUnit ?? f.unit,
      storageCondition: picked?.defaultStorageCondition ?? ""
    }));
  };

  const selectedMasterEntry = useMemo(() => {
    if (!form.materialMasterEntryId) return null;
    const found = masterEntries.find((e) => e.id === form.materialMasterEntryId);
    if (found) return found;
    if (editingItem && editingItem.materialMasterEntryId === form.materialMasterEntryId) {
      return {
        id: editingItem.materialMasterEntryId,
        code: editingItem.materialMasterEntryCode || form.code || `#${editingItem.materialMasterEntryId}`,
        name: form.materialName,
        category: getMasterCategoryForType(form.materialType) ?? "Reagent",
        baseUnit: form.unit,
        isActive: true,
        sectionId: editingItem.sectionId
      } as MaterialMasterEntry;
    }
    return null;
  }, [masterEntries, form.materialMasterEntryId, form.code, form.materialName, form.materialType, form.unit, editingItem]);

  const handleMasterEntryChange = (entry: MaterialMasterEntry | null) => {
    if (!entry) {
      if (editingItem && editingItem.materialMasterEntryId === null) {
        setForm((f) => ({
          ...f,
          materialMasterEntryId: null,
          materialName: editingItem.materialName,
          code: editingItem.code ?? ""
        }));
      } else {
        setForm((f) => ({
          ...f,
          materialMasterEntryId: null,
          materialName: "",
          code: ""
        }));
      }
      return;
    }

    setForm((f) => ({
      ...f,
      materialMasterEntryId: entry.id,
      materialName: entry.name,
      code: entry.code,
      // An Other material takes its unit from the category chosen at receipt.
      unit: entry.category === "Other" ? f.unit : entry.baseUnit || f.unit
    }));
  };

  const onMaterialTypeChange = useCallback(
    async (type: MaterialType) => {
      const nextCategory = getMasterCategoryForType(type);

      let defaultUnit: MaterialUnit | null = null;
      try {
        defaultUnit = (await MaterialService.getDefaultUnit(type)) as MaterialUnit;
      } catch {
        // Keep the unit already chosen.
      }

      setForm((f) => {
        const prevCategory = getMasterCategoryForType(f.materialType);
        const keepMaster = Boolean(prevCategory && nextCategory && prevCategory === nextCategory);
        return {
          ...f,
          materialType: type,
          customType: "",
          ...(defaultUnit ? { unit: defaultUnit } : {}),
          storageCondition: "",
          mediaProductId: type === "DehydratedMedia" ? f.mediaProductId : null,
          referenceOrganismId: type === "LyophilizedMicroorganism" ? f.referenceOrganismId : null,
          purity: (type === "ReferenceStandard" || type === "PrimaryStandard") ? f.purity : "",
          moisturePercent: type === "ReferenceStandard" ? f.moisturePercent : "",
          materialMasterEntryId: keepMaster ? f.materialMasterEntryId : null,
          ...(keepMaster
            ? {}
            : isMasterLinkedType(type) && !editingItem
            ? { materialName: "", code: "" }
            : {})
        };
      });
      setCategoryId("");
    },
    [editingItem]
  );

  useEffect(() => {
    if (!open) {
      setTypeOptionsData(null);
      return;
    }

    if (activeSectionId != null) {
      let canceled = false;
      MaterialService.getTypeOptions(activeSectionId)
        .then((data) => {
          if (canceled) return;
          setTypeOptionsData(data);
        })
        .catch(() => {
          if (!canceled) {
            setTypeOptionsData(null);
          }
        });

      return () => {
        canceled = true;
      };
    } else {
      setTypeOptionsData(null);
    }
  }, [open, activeSectionId, editingItem]);

  // Types offered for a new lot: what the server lists for the lab, plus the Other material path.
  const newLotTypes: MaterialType[] = useMemo(() => {
    if (!typeOptionsData) return NEW_LOT_TYPES;
    const listed = typeOptionsData.builtIn.filter((t) => !LEGACY_MATERIAL_TYPES.includes(t) && t !== "WorkingStandard");
    return listed.includes("Other") ? listed : [...listed, "Other"];
  }, [typeOptionsData]);

  // When creating, the initial type is the first type of the lab,
  // not "DehydratedMedia".
  useEffect(() => {
    if (editingItem || !typeOptionsData || newLotTypes.length === 0) return;
    if (!hasSetInitialType) {
      setHasSetInitialType(true);
      void onMaterialTypeChange(newLotTypes[0]);
    } else if (!newLotTypes.includes(form.materialType)) {
      void onMaterialTypeChange(newLotTypes[0]);
    }
  }, [typeOptionsData, newLotTypes, editingItem, hasSetInitialType, form.materialType, onMaterialTypeChange]);

  // Lots of an Other material (and legacy named types) keep their stored type: the category and
  // master entry carry the rules, so the type is shown read-only. An old lot received before
  // categories, with no master entry, keeps its stored type and free-text name as they are.
  const editingOtherMaterial = Boolean(
    editingItem && (editingItem.materialType === "Other" || LEGACY_MATERIAL_TYPES.includes(editingItem.materialType))
  );
  const editingLegacyLot = Boolean(
    editingItem && (LEGACY_MATERIAL_TYPES.includes(editingItem.materialType) || (editingItem.materialType === "Other" && !editingItem.materialMasterEntryId))
  );
  const storedTypeLabel = editingItem
    ? editingItem.categoryName?.trim() || editingItem.customType?.trim() || typeOptionLabel(editingItem.materialType)
    : "";

  // A new Other lot has no free-text name or code: both come from the master entry.
  const nameFromEntryOnly = form.materialType === "Other" && !editingLegacyLot;

  // A lyophilized lot's name and code come from its reference strain; only an old lot with no strain keeps them editable.
  const isLyophilized = form.materialType === "LyophilizedMicroorganism";
  const nameFromStrain = isLyophilized && (!editingItem || Boolean(form.referenceOrganismId));
  const strainOptions = strains.filter((s) => s.isActive || s.id === form.referenceOrganismId);

  useEffect(() => {
    if (!open || !isLyophilized) return;
    ReferenceOrganismService.getAll()
      .then(setStrains)
      .catch(fail("reference organisms", () => setStrains([])));
  }, [open, isLyophilized, fail]);

  const handleStrainChange = (strain: ReferenceOrganism | null) => {
    if (!strain) {
      setForm((f) => ({
        ...f,
        referenceOrganismId: null,
        materialName: editingItem ? editingItem.materialName : "",
        code: editingItem ? editingItem.code ?? "" : ""
      }));
      return;
    }
    setForm((f) => ({ ...f, referenceOrganismId: strain.id, materialName: strain.name, code: strain.code }));
  };

  // Types for the select: an existing lot whose type is not in its lab's list still shows its current type.
  const typeSelectOptions: MaterialType[] =
    editingItem && !newLotTypes.includes(editingItem.materialType) ? [...newLotTypes, editingItem.materialType] : newLotTypes;

  const handleMediaProductChange = (id: number | null, product: MediaProductOption | null) => {
    if (!product || id === null) {
      if (editingItem && editingItem.mediaProductId === null) {
        setForm((f) => ({
          ...f,
          mediaProductId: null,
          materialName: editingItem.materialName,
          code: editingItem.code ?? ""
        }));
      } else {
        setForm((f) => ({
          ...f,
          mediaProductId: null,
          materialName: "",
          code: ""
        }));
      }
      return;
    }

    if (editingItem && editingItem.mediaProductId !== null && id === editingItem.mediaProductId) {
      setForm((f) => ({
        ...f,
        mediaProductId: id,
        materialName: editingItem.materialName,
        code: editingItem.code ?? ""
      }));
    } else {
      setForm((f) => ({
        ...f,
        mediaProductId: id,
        materialName: product.name,
        code: product.code
      }));
    }
  };

  const handleSave = async () => {
    setError(null);
    if (form.materialType === "DehydratedMedia" && !form.mediaProductId) {
      setError("Choose the configured media product for this dehydrated media.");
      return;
    }

    if (!editingItem && isLyophilized && !form.referenceOrganismId) {
      setError("Choose the reference organism for this lyophilized microorganism.");
      return;
    }

    if (!editingItem && form.materialType === "Other" && !categoryId) {
      setError("Choose the material category for this lot.");
      return;
    }

    if (!editingItem && isMasterLinkedType(form.materialType) && !form.materialMasterEntryId) {
      setError("Choose the material master entry for this lot.");
      return;
    }

    // Mirrors the category rule for the user's benefit; the server enforces it.
    if (!editingItem && selectedCategory?.expiryRequired && !form.expiryDate) {
      setError(`${selectedCategory.name} lots need an expiry date.`);
      return;
    }

    if (form.materialType === "ReferenceStandard" || form.materialType === "PrimaryStandard") {
      if (form.purity === "" || form.purity == null) {
        setError(
          form.materialType === "PrimaryStandard"
            ? "Purity percentage is required for primary standards."
            : "Purity percentage is required for reference standards."
        );
        return;
      }
      const p = Number(form.purity);
      if (isNaN(p) || p <= 0 || p > 100) {
        setError("Purity must be greater than 0 and less than or equal to 100.");
        return;
      }
      if (form.materialType === "ReferenceStandard" && form.moisturePercent !== "" && form.moisturePercent != null) {
        const m = Number(form.moisturePercent);
        if (isNaN(m) || m < 0 || m >= 100) {
          setError("Moisture content must be at least 0 and below 100.");
          return;
        }
      }
    }

    if (
      !form.materialName.trim() ||
      !form.batchNumber.trim() ||
      !form.receivingDate ||
      !form.location.trim() ||
      form.quantityReceived === "" ||
      form.quantityReceived == null
    ) {
      setError("Material name, batch/lot number, receiving date, storage location, and quantity received are required.");
      return;
    }

    if (Number(form.quantityReceived) <= 0) {
      setError("Quantity received must be greater than zero.");
      return;
    }

    if (!editingItem && mySections.length > 1 && !selectedSectionId) {
      setError("Laboratory section is required.");
      return;
    }

    const payload: SaveMaterialPayload = {
      materialType: form.materialType,
      // Only the stored custom type of an old lot is ever sent back; new lots take no free-text type.
      customType: editingItem && form.materialType === "Other" && form.customType.trim() ? form.customType.trim() : null,
      materialName: form.materialName.trim(),
      manufacturerName: form.manufacturerName.trim(),
      batchNumber: form.batchNumber.trim(),
      receivingDate: form.receivingDate,
      expiryDate: form.expiryDate || null,
      code: form.code.trim() || null,
      location: form.location.trim(),
      storageCondition: form.storageCondition.trim() || null,
      quantityReceived: Number(form.quantityReceived),
      unit: form.unit,
      minimumStockLevel: form.minimumStockLevel === "" ? null : Number(form.minimumStockLevel),
      // With a strain the server derives the organism and ATCC number; only an old lot without one sends its stored values back.
      atccNumber: isLyophilized && !form.referenceOrganismId ? form.atccNumber.trim() || null : null,
      organismId: isLyophilized && !form.referenceOrganismId ? form.organismId || null : null,
      referenceOrganismId: isLyophilized ? form.referenceOrganismId : null,
      mediaProductId: form.materialType === "DehydratedMedia" ? form.mediaProductId : null,
      purity:
        (form.materialType === "ReferenceStandard" || form.materialType === "PrimaryStandard") && form.purity !== ""
          ? Number(form.purity)
          : null,
      moisturePercent:
        form.materialType === "ReferenceStandard" && form.moisturePercent !== "" && form.moisturePercent != null
          ? Number(form.moisturePercent)
          : null,
      materialMasterEntryId:
        isMasterLinkedType(form.materialType) || LEGACY_MATERIAL_TYPES.includes(form.materialType) ? form.materialMasterEntryId : null,
      ...(!editingItem && mySections.length > 1 && selectedSectionId !== "" ? { sectionId: Number(selectedSectionId) } : {})
    };

    setSaving(true);
    try {
      if (editingItem) {
        await MaterialService.update(editingItem.id, payload, editingItem.version);
        onSuccess("Material stock updated successfully.");
      } else {
        await MaterialService.create(payload);
        onSuccess("Material added to stock successfully.");
      }
      onClose();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      setError(errorObj.response?.data?.message ?? errorObj.message ?? "Could not save material stock.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      maxWidth="md"
      titleSx={{ pb: 1.5 }}
      title={
        <Typography variant="h6" sx={{ fontWeight: 700, color: theme.palette.primary.main }}>
          {editingItem ? "Edit Material" : "Add Material to Stock"}
        </Typography>
      }
      actions={
        <Box sx={{ display: "flex", justifyContent: "space-between", width: "100%" }}>
          <Button onClick={onClose} disabled={saving} color="inherit">
            Cancel
          </Button>
          <Button
            variant="contained"
            onClick={handleSave}
            disabled={saving}
            sx={{
              bgcolor: brandColors.sectionTitle,
              px: 3,
              "&:hover": { bgcolor: brandColors.pageTitle }
            }}
          >
            {saving ? "Saving..." : editingItem ? "Save Changes" : "Add to Stock"}
          </Button>
        </Box>
      }
    >
      <LoadFailuresAlert failed={failedLists} sx={{ mb: 2.5 }} />
      {error && (
        <Alert severity="error" sx={{ mb: 2.5 }}>
          {error}
        </Alert>
      )}

      {/* SECTION 1 — Material Information */}
      <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 1.5 }}>
        1. Material Information
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2, mb: 3 }}>
        {editingOtherMaterial ? (
          <TextField
            size="small"
            label="Material Type"
            value={storedTypeLabel}
            disabled
            helperText={editingLegacyLot ? "Received before material categories; the stored type is kept." : "A received lot keeps its category."}
          />
        ) : (
          <FormControl size="small" fullWidth required sx={{ gridColumn: { xs: "1", sm: form.materialType === "DehydratedMedia" ? "span 2" : "1" } }}>
            <InputLabel id="material-type-label">Material Type</InputLabel>
            <Select<MaterialType>
              labelId="material-type-label"
              label="Material Type"
              value={typeSelectOptions.includes(form.materialType) ? form.materialType : ""}
              onChange={(e) => void onMaterialTypeChange(e.target.value as MaterialType)}
            >
              {typeSelectOptions.map((t) => (
                <MenuItem key={t} value={t}>
                  {typeOptionLabel(t)}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}

        {form.materialType === "DehydratedMedia" && (
          <Box sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
            <MediaProductPicker
              value={form.mediaProductId}
              onChange={handleMediaProductChange}
              required
              helperText="Media not listed? A Section Head adds it in Laboratory Configuration > Media Configuration."
            />
          </Box>
        )}

        {form.materialType === "Other" && editingItem && editingItem.materialMasterEntryId && (
          <>
            <TextField size="small" label="Material category" value={editingItem.categoryName ?? "—"} disabled />
            <TextField
              size="small"
              label="Master entry"
              value={`${editingItem.materialMasterEntryCode ?? editingItem.code ?? ""}: ${editingItem.materialName}`}
              disabled
            />
          </>
        )}

        {form.materialType === "Other" && !editingItem && (
          <FormControl size="small" fullWidth required sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
            <InputLabel id="material-category-label">Material category</InputLabel>
            <Select<number | "">
              labelId="material-category-label"
              label="Material category"
              value={selectedCategory ? selectedCategory.id : ""}
              onChange={(e) => handleCategoryChange(e.target.value === "" ? "" : Number(e.target.value))}
              disabled={activeSectionId == null || categoriesLoading}
            >
              {categories.map((c) => (
                <MenuItem key={c.id} value={c.id}>
                  {c.name}
                </MenuItem>
              ))}
            </Select>
            <FormHelperText>
              {activeSectionId == null
                ? "Choose the laboratory section first."
                : categories.length === 0 && !categoriesLoading
                ? "No category is available to this lab; a Section Head adds one under Reagents & Standards > Material categories."
                : "Sets the unit, storage and expiry rules for this lot."}
            </FormHelperText>
          </FormControl>
        )}

        {isMasterLinkedType(form.materialType) && !(form.materialType === "Other" && editingItem) && (
          <Box sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
            <Autocomplete<MaterialMasterEntry>
              size="small"
              options={availableMasterEntries}
              disabled={form.materialType === "Other" && !selectedCategory}
              getOptionLabel={(option) => `${option.code}: ${option.name}${option.grade ? ` (${option.grade})` : ""}`}
              isOptionEqualToValue={(option, val) => option.id === val.id}
              value={selectedMasterEntry}
              onChange={(_event, newValue) => handleMasterEntryChange(newValue)}
              loading={masterEntriesLoading}
              renderInput={(params) => (
                <TextField
                  {...params}
                  size="small"
                  required={!editingItem}
                  label={`${
                    form.materialType === "Chemical"
                      ? "Reagent"
                      : form.materialType === "Indicator"
                      ? "Indicator"
                      : form.materialType === "PrimaryStandard"
                      ? "Primary Standard"
                      : form.materialType === "Other"
                      ? "Material"
                      : "Reference Standard"
                  } Master Entry`}
                  placeholder="Select master entry..."
                  helperText={
                    form.materialMasterEntryId
                      ? `Selected: ${selectedMasterEntry?.code ?? form.code} (Base Unit: ${selectedMasterEntry?.baseUnit ?? form.unit})`
                      : !editingItem
                      ? form.materialType === "Other"
                        ? selectedCategory
                          ? "Required. The master entry sets the name and code; a missing material is added by its owner lab."
                          : "Choose the material category first."
                        : "Required. Master entry sets the name, code, and base unit."
                      : "Optional for legacy unlinked lots. Select an entry to link this lot."
                  }
                />
              )}
            />
          </Box>
        )}

        {!editingItem && mySections.length > 1 && (
          <FormControl size="small" fullWidth required sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
            <InputLabel id="dialog-section-label">Laboratory Section</InputLabel>
            <Select<number | "">
              labelId="dialog-section-label"
              label="Laboratory Section"
              value={selectedSectionId}
              onChange={(e) => {
                const newSecId = e.target.value === "" ? "" : Number(e.target.value);
                setSelectedSectionId(newSecId);
                // Categories are offered per lab.
                if (form.materialType === "Other") handleCategoryChange("");
              }}
              inputProps={{ "aria-label": "Laboratory Section" }}
            >
              <MenuItem value="">
                <em>Select Laboratory Section...</em>
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
          size="small"
          required
          label="Material Name"
          placeholder="e.g. Tryptic Soy Agar Powder"
          value={form.materialName}
          onChange={(e) => setForm({ ...form, materialName: e.target.value })}
          disabled={form.materialType === "DehydratedMedia" || Boolean(form.materialMasterEntryId) || nameFromEntryOnly || nameFromStrain}
          helperText={form.materialMasterEntryId || nameFromEntryOnly ? "Populated from master entry" : nameFromStrain ? "Populated from reference organism" : undefined}
        />

        <TextField
          size="small"
          label="Manufacturer"
          placeholder="e.g. Oxoid, Merck, Difco"
          value={form.manufacturerName}
          onChange={(e) => setForm({ ...form, manufacturerName: e.target.value })}
        />

        <TextField
          size="small"
          label={form.materialType === "DehydratedMedia" ? "Media code" : "Code / Catalog No."}
          placeholder={form.materialType === "DehydratedMedia" ? "e.g. TSA" : "e.g. CM0131B"}
          value={form.code}
          onChange={(e) => setForm({ ...form, code: e.target.value })}
          disabled={form.materialType === "DehydratedMedia" || Boolean(form.materialMasterEntryId) || nameFromEntryOnly || nameFromStrain}
          helperText={form.materialMasterEntryId || nameFromEntryOnly ? "Populated from master entry" : nameFromStrain ? "Populated from reference organism" : undefined}
        />

        {form.materialType === "LyophilizedMicroorganism" && (
          <>
            <Box sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
              <Autocomplete
                size="small"
                options={strainOptions}
                value={strains.find((s) => s.id === form.referenceOrganismId) ?? null}
                getOptionLabel={(s) => `${s.code} · ${s.name}`}
                isOptionEqualToValue={(o, v) => o.id === v.id}
                onChange={(_e, v) => handleStrainChange(v)}
                renderInput={(params) => (
                  <TextField
                    {...params}
                    label="Reference organism"
                    required={!editingItem}
                    placeholder="Select strain"
                    helperText={
                      editingItem && !form.referenceOrganismId
                        ? "Received before reference organisms; pick a strain to link this lot (optional)."
                        : "Strains are managed under Organisms > Reference organisms."
                    }
                  />
                )}
              />
            </Box>
            {editingItem && !form.referenceOrganismId && (
              <>
                <TextField size="small" label="Organism (stored)" value={editingItem.organism?.scientificName ?? "—"} disabled />
                <TextField size="small" label="ATCC No. (stored)" value={form.atccNumber || "—"} disabled />
              </>
            )}
          </>
        )}

        {(form.materialType === "ReferenceStandard" || form.materialType === "PrimaryStandard") && (
          <TextField
            size="small"
            required
            label="Purity (%)"
            placeholder="e.g. 99.8"
            type="number"
            value={form.purity}
            onChange={(e) => setForm({ ...form, purity: e.target.value })}
            slotProps={{
              htmlInput: { step: "0.001", min: "0.001", max: "100" }
            }}
            helperText="Purity percentage (0 < p ≤ 100)"
          />
        )}
        {form.materialType === "ReferenceStandard" && (
          <TextField
            size="small"
            label="Moisture Content (%)"
            placeholder="e.g. 0.5"
            type="number"
            value={form.moisturePercent}
            onChange={(e) => setForm({ ...form, moisturePercent: e.target.value })}
            slotProps={{
              htmlInput: { step: "0.001", min: "0", max: "99.999" }
            }}
            helperText="Moisture percentage (0 ≤ m < 100, optional)"
          />
        )}
      </Box>

      <Divider sx={{ my: 2.5 }} />

      {/* SECTION 2 — Batch & Quantity */}
      <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 1.5 }}>
        2. Batch &amp; Quantity
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" }, gap: 2, mb: 1.5 }}>
        <TextField
          size="small"
          required
          label="Batch / Lot No."
          placeholder="e.g. 3458921"
          value={form.batchNumber}
          onChange={(e) => setForm({ ...form, batchNumber: e.target.value })}
        />

        <FormControl size="small" fullWidth required sx={{ gridColumn: { sm: "span 2", md: "span 2" } }}>
          <InputLabel id="storage-location-label">Storage Location</InputLabel>
          <Select
            labelId="storage-location-label"
            label="Storage Location"
            value={selectedEquipment && isEquipmentLocation(form.location, selectedEquipment) ? equipmentLocationTag(selectedEquipment) : form.location}
            onChange={(e) => setForm({ ...form, location: e.target.value })}
            disabled={equipmentLoading}
          >
            <MenuItem value="">
              <em>{equipmentLoading ? "Loading storage locations..." : "Select storage location..."}</em>
            </MenuItem>

            <ListSubheader sx={{ fontWeight: 700, fontSize: 12, color: "text.secondary", textTransform: "uppercase", lineHeight: "28px" }}>
              Equipment: Refrigerator
            </ListSubheader>
            {refrigerators.length > 0 ? (
              refrigerators.map((eq) => {
                const val = equipmentLocationTag(eq);
                return (
                  <MenuItem key={eq.id} value={val}>
                    {val}
                  </MenuItem>
                );
              })
            ) : (
              <MenuItem disabled value="no-ref" sx={{ fontSize: 12, fontStyle: "italic" }}>
                No in-service refrigerator configured
              </MenuItem>
            )}

            <ListSubheader sx={{ fontWeight: 700, fontSize: 12, color: "text.secondary", textTransform: "uppercase", lineHeight: "28px" }}>
              Equipment: Deep Freezer
            </ListSubheader>
            {deepFreezers.length > 0 ? (
              deepFreezers.map((eq) => {
                const val = equipmentLocationTag(eq);
                return (
                  <MenuItem key={eq.id} value={val}>
                    {val}
                  </MenuItem>
                );
              })
            ) : (
              <MenuItem disabled value="no-df" sx={{ fontSize: 12, fontStyle: "italic" }}>
                No in-service deep freezer configured
              </MenuItem>
            )}

            <ListSubheader sx={{ fontWeight: 700, fontSize: 12, color: "text.secondary", textTransform: "uppercase", lineHeight: "28px" }}>
              Equipment: Freezer
            </ListSubheader>
            {freezers.length > 0 ? (
              freezers.map((eq) => {
                const val = equipmentLocationTag(eq);
                return (
                  <MenuItem key={eq.id} value={val}>
                    {val}
                  </MenuItem>
                );
              })
            ) : (
              <MenuItem disabled value="no-fr" sx={{ fontSize: 12, fontStyle: "italic" }}>
                No in-service freezer configured
              </MenuItem>
            )}

            {hasOtherOptions && (
              <ListSubheader sx={{ fontWeight: 700, fontSize: 12, color: "text.secondary", textTransform: "uppercase", lineHeight: "28px" }}>
                Other
              </ListSubheader>
            )}
            {roomStorageOption && (
              <MenuItem value={roomStorageOption}>
                {roomStorageOption}
              </MenuItem>
            )}

            {/* Fallback for legacy material locations when editing */}
            {isFallbackLocation && (
              <MenuItem value={form.location}>
                {form.location} (Current Location)
              </MenuItem>
            )}
          </Select>
        </FormControl>

        <TextField
          size="small"
          required
          type="number"
          label="Quantity Received"
          value={form.quantityReceived}
          onChange={(e) => setForm({ ...form, quantityReceived: e.target.value })}
          slotProps={{
            htmlInput: { min: 0, step: "any" }
          }}
        />

        <FormControl size="small" fullWidth required>
          <InputLabel id="dialog-unit-label">Unit</InputLabel>
          <Select
            labelId="dialog-unit-label"
            label="Unit"
            value={form.unit}
            onChange={(e) => setForm({ ...form, unit: e.target.value as MaterialUnit })}
          >
            {MATERIAL_UNITS.map((u) => (
              <MenuItem key={u} value={u}>
                {u}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <TextField
          size="small"
          type="number"
          label="Min. Stock Level (optional)"
          placeholder="e.g. 500"
          value={form.minimumStockLevel}
          onChange={(e) => setForm({ ...form, minimumStockLevel: e.target.value })}
          slotProps={{
            htmlInput: { min: 0, step: "any" }
          }}
        />

        <TextField
          size="small"
          label="Storage Condition"
          placeholder="e.g. 2-8 °C, protect from light"
          value={form.storageCondition}
          onChange={(e) => setForm({ ...form, storageCondition: e.target.value })}
          helperText={selectedCategory?.defaultStorageCondition ? "Prefilled from the category; change it if this lot differs." : undefined}
          sx={{ gridColumn: { xs: "1", sm: "1 / -1" } }}
        />
      </Box>

      {/* Selected Equipment Read-Only Details Card */}
      {selectedEquipment && (
        <Paper
          variant="outlined"
          sx={{
            mb: 3,
            p: 1.5,
            bgcolor: "background.default",
            borderColor: "divider",
            borderRadius: 1
          }}
        >
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "1fr",
                sm: "repeat(2, 1fr)",
                md: "repeat(4, 1fr)"
              },
              gap: 1.5
            }}
          >
            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Selected Storage
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.sectionTitle }}>
                {selectedEquipment.instrumentType}, {selectedEquipment.manufacturerName || "Asset"}
              </Typography>
            </Box>
            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Code
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                {selectedEquipment.code}
              </Typography>
            </Box>
            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Location
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                {selectedEquipment.location || "—"}
              </Typography>
            </Box>
            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Status
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600, color: "success.main" }}>
                In Service
              </Typography>
            </Box>
          </Box>
        </Paper>
      )}

      {/* Room Storage Read-Only Details Card */}
      {roomStorageOption && form.location === roomStorageOption && (
        <Paper
          variant="outlined"
          sx={{
            mb: 3,
            p: 1.5,
            bgcolor: "background.default",
            borderColor: "divider",
            borderRadius: 1
          }}
        >
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "1fr",
                sm: "repeat(2, 1fr)",
                md: "repeat(3, 1fr)"
              },
              gap: 1.5
            }}
          >
            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Selected Storage
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.sectionTitle }}>
                {roomStorageOption}
              </Typography>
            </Box>
            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Storage Type
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                Ambient / Room Storage
              </Typography>
            </Box>
            <Box>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Location
              </Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                {activeSection?.sectionName ?? roomStorageOption}
              </Typography>
            </Box>
          </Box>
        </Paper>
      )}

      <Divider sx={{ my: 2.5 }} />

      {/* SECTION 3 — Dates */}
      <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 1.5 }}>
        3. Dates & Traceability
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
        <DateField
          required
          label="Receiving Date"
          value={form.receivingDate}
          onChange={(v) => setForm({ ...form, receivingDate: v })}
        />

        <DateField
          required={Boolean(selectedCategory?.expiryRequired)}
          label="Expiry Date"
          value={form.expiryDate}
          onChange={(v) => setForm({ ...form, expiryDate: v })}
        />
      </Box>

      {editingItem && (
        <Box sx={{ mt: 2.5, p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            <strong>Note:</strong> Changing Quantity Received adjusts Quantity Remaining by the difference (a receiving correction);
            it preserves consumption already recorded by Media Preparation or Cryovials.
          </Typography>
        </Box>
      )}
    </FloatingDialog>
  );
}
