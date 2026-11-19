import type { MediaProductOption } from "../../../../hooks/useMediaProducts";
import type { MaterialCategory } from "../../../laboratoryConfiguration/masterDataSimple/services/MaterialCategoryService";
import type { MaterialMasterEntry } from "../../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService";
import type { ReferenceOrganism } from "../../../laboratoryConfiguration/masterDataSimple/services/ReferenceOrganismService";
import { LEGACY_MATERIAL_TYPES, type MaterialItem } from "../types/materialTypes";
import {
  getMasterCategoryForType,
  typeOptionLabel,
  type MaterialFormModel,
  type MaterialModelContext
} from "./materialFormModel";

// What picking a category, master entry, strain or media product does to the form. Pure, so the
// rules can be read and tested without the screen.

// The category's rules prefill the receipt (editable); the server enforces them either way.
export function categoryPatch(
  model: MaterialFormModel,
  id: number | "",
  categories: MaterialCategory[]
): Partial<MaterialFormModel> {
  const picked = categories.find((c) => c.id === id);
  return {
    categoryId: id,
    materialMasterEntryId: null,
    materialName: "",
    code: "",
    unit: picked?.defaultUnit ?? model.unit,
    storageCondition: picked?.defaultStorageCondition ?? ""
  };
}

export function masterEntryPatch(
  model: MaterialFormModel,
  entry: MaterialMasterEntry | null,
  editing: MaterialItem | null
): Partial<MaterialFormModel> {
  if (!entry) {
    if (editing && editing.materialMasterEntryId === null) {
      return { materialMasterEntryId: null, materialName: editing.materialName, code: editing.code ?? "" };
    }
    return { materialMasterEntryId: null, materialName: "", code: "" };
  }
  return {
    materialMasterEntryId: entry.id,
    materialName: entry.name,
    code: entry.code,
    // An Other material takes its unit from the category chosen at receipt.
    unit: entry.category === "Other" ? model.unit : entry.baseUnit || model.unit
  };
}

export function strainPatch(strain: ReferenceOrganism | null, editing: MaterialItem | null): Partial<MaterialFormModel> {
  if (!strain) {
    return {
      referenceOrganismId: null,
      materialName: editing ? editing.materialName : "",
      code: editing ? editing.code ?? "" : ""
    };
  }
  return { referenceOrganismId: strain.id, materialName: strain.name, code: strain.code };
}

export function mediaProductPatch(
  id: number | null,
  product: MediaProductOption | null,
  editing: MaterialItem | null
): Partial<MaterialFormModel> {
  if (!product || id === null) {
    if (editing && editing.mediaProductId === null) {
      return { mediaProductId: null, materialName: editing.materialName, code: editing.code ?? "" };
    }
    return { mediaProductId: null, materialName: "", code: "" };
  }
  if (editing && editing.mediaProductId !== null && id === editing.mediaProductId) {
    return { mediaProductId: id, materialName: editing.materialName, code: editing.code ?? "" };
  }
  return { mediaProductId: id, materialName: product.name, code: product.code };
}

// Lists the dialog loads for the lot being received.
export interface MaterialPickLists {
  categories: MaterialCategory[];
  masterEntries: MaterialMasterEntry[];
  strains: ReferenceOrganism[];
}

// Read-only facts the screen derives from the form and the lot being edited.
export interface MaterialView {
  // Lots of an Other material (and legacy named types) keep their stored type, shown read-only.
  editingOtherMaterial: boolean;
  editingLegacyLot: boolean;
  storedTypeLabel: string;
  // A new Other lot has no free-text name or code: both come from the master entry.
  nameFromEntryOnly: boolean;
  isLyophilized: boolean;
  // A lyophilized lot's name and code come from its reference strain.
  nameFromStrain: boolean;
  strainOptions: ReferenceOrganism[];
  selectedCategory: MaterialCategory | null;
  availableMasterEntries: MaterialMasterEntry[];
  selectedMasterEntry: MaterialMasterEntry | null;
}

export function deriveMaterialView(
  model: MaterialFormModel,
  editing: MaterialItem | null,
  lists: MaterialPickLists
): MaterialView {
  const editingOtherMaterial = Boolean(
    editing && (editing.materialType === "Other" || LEGACY_MATERIAL_TYPES.includes(editing.materialType))
  );
  const editingLegacyLot = Boolean(
    editing &&
      (LEGACY_MATERIAL_TYPES.includes(editing.materialType) ||
        (editing.materialType === "Other" && !editing.materialMasterEntryId))
  );
  const isLyophilized = model.materialType === "LyophilizedMicroorganism";
  const selectedCategory = lists.categories.find((c) => c.id === model.categoryId) ?? null;

  let selectedMasterEntry: MaterialMasterEntry | null = null;
  if (model.materialMasterEntryId) {
    selectedMasterEntry = lists.masterEntries.find((e) => e.id === model.materialMasterEntryId) ?? null;
    if (!selectedMasterEntry && editing && editing.materialMasterEntryId === model.materialMasterEntryId) {
      selectedMasterEntry = {
        id: editing.materialMasterEntryId,
        code: editing.materialMasterEntryCode || model.code || `#${editing.materialMasterEntryId}`,
        name: model.materialName,
        category: getMasterCategoryForType(model.materialType) ?? "Reagent",
        baseUnit: model.unit,
        isActive: true,
        sectionId: editing.sectionId
      };
    }
  }

  return {
    editingOtherMaterial,
    editingLegacyLot,
    storedTypeLabel: editing
      ? editing.categoryName?.trim() || editing.customType?.trim() || typeOptionLabel(editing.materialType)
      : "",
    nameFromEntryOnly: model.materialType === "Other" && !editingLegacyLot,
    isLyophilized,
    nameFromStrain: isLyophilized && (!editing || Boolean(model.referenceOrganismId)),
    strainOptions: lists.strains.filter((s) => s.isActive || s.id === model.referenceOrganismId),
    selectedCategory,
    // Master entries are app-wide: any lab can receive stock against any active entry.
    // An Other lot picks its category first, so only that category's entries are offered.
    availableMasterEntries:
      model.materialType === "Other"
        ? lists.masterEntries.filter((e) => e.materialCategoryId === model.categoryId)
        : lists.masterEntries,
    selectedMasterEntry
  };
}

// The save rules' view of the screen: how many sections, whether editing, and the chosen category's rules.
export function buildMaterialContext(
  model: MaterialFormModel,
  editing: MaterialItem | null,
  sectionCount: number,
  categories: MaterialCategory[]
): MaterialModelContext {
  const category = categories.find((c) => c.id === model.categoryId) ?? null;
  return {
    isEditing: editing !== null,
    sectionCount,
    category: category ? { name: category.name, expiryRequired: category.expiryRequired } : null
  };
}
