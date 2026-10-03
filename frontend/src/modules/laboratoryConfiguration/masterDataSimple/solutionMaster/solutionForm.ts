import {
  SolutionMaster,
  SolutionType,
  ShelfLifeUnit,
  SolutionComponentUnit,
  TitrantStrengthUnit,
  StandardizationMode,
  SaveSolutionMasterRequest
} from "../services/SolutionMasterService";
import { MaterialMasterEntry } from "../services/MaterialMasterService";

export interface ComponentRowState {
  materialMasterEntryId: string | number;
  quantity: string | number;
  unit: SolutionComponentUnit;
}

export interface SolutionFormState {
  // General
  name: string;
  type: SolutionType;
  shelfLifeValue: string | number;
  shelfLifeUnit: ShelfLifeUnit;
  storageCondition: string;
  finalVolumeMl: string | number;
  instructions: string;
  sectionId: string | number;

  // pH
  phTarget: string | number;
  phTolerance: string | number;
  phAdjustingEntryId: string | number;

  // Components
  components: ComponentRowState[];

  // Titrant
  nominalStrength: string | number;
  strengthUnit: TitrantStrengthUnit;
  standardizationMode: StandardizationMode;
  standardEntryId: string | number;
  equivalenceMgPerMl: string | number;
  referenceSolutionId: string | number;
  blankRequired: boolean;
  replicateCount: string | number;
  factorMin: string | number;
  factorMax: string | number;
  maxRsdPercent: string | number;
  validityDays: string | number;
}

export const SOLUTION_TYPE_OPTIONS: Array<{ value: SolutionType; label: string }> = [
  { value: "MobilePhase", label: "Mobile Phase" },
  { value: "Diluent", label: "Diluent" },
  { value: "Titrant", label: "Titrant" }
];

export const SHELF_LIFE_UNIT_OPTIONS: Array<{ value: ShelfLifeUnit; label: string }> = [
  { value: "Hours", label: "Hours" },
  { value: "Days", label: "Days" }
];

export const COMPONENT_UNIT_OPTIONS: Array<{ value: SolutionComponentUnit; label: string }> = [
  { value: "Gram", label: "g (Gram)" },
  { value: "Milligram", label: "mg (Milligram)" },
  { value: "Milliliter", label: "mL (Milliliter)" },
  { value: "Liter", label: "L (Liter)" },
  { value: "PercentVolume", label: "% v/v (Percent Volume)" },
  { value: "Parts", label: "Parts" }
];

export const STRENGTH_UNIT_OPTIONS: Array<{ value: TitrantStrengthUnit; label: string }> = [
  { value: "Normal", label: "Normal (N)" },
  { value: "Molar", label: "Molar (M)" },
  { value: "MgWaterPerMl", label: "mg H2O/mL (Karl Fischer)" }
];

export function createInitialSolutionFormState(
  defaultSectionId: string | number = ""
): SolutionFormState {
  return {
    name: "",
    type: "MobilePhase",
    shelfLifeValue: "24",
    shelfLifeUnit: "Hours",
    storageCondition: "",
    finalVolumeMl: "1000",
    instructions: "",
    sectionId: defaultSectionId,
    phTarget: "",
    phTolerance: "",
    phAdjustingEntryId: "",
    components: [{ materialMasterEntryId: "", quantity: "", unit: "Milliliter" }],
    nominalStrength: "",
    strengthUnit: "Normal",
    standardizationMode: "PrimaryStandard",
    standardEntryId: "",
    equivalenceMgPerMl: "",
    referenceSolutionId: "",
    blankRequired: false,
    replicateCount: "3",
    factorMin: "0.97000",
    factorMax: "1.03000",
    maxRsdPercent: "0.20",
    validityDays: "30"
  };
}

export function solutionEntityToFormState(entry: SolutionMaster): SolutionFormState {
  return {
    name: entry.name,
    type: entry.type,
    shelfLifeValue: entry.shelfLifeValue,
    shelfLifeUnit: entry.shelfLifeUnit,
    storageCondition: entry.storageCondition,
    finalVolumeMl: entry.finalVolumeMl,
    instructions: entry.instructions,
    sectionId: entry.sectionId,
    phTarget: entry.phTarget ?? "",
    phTolerance: entry.phTolerance ?? "",
    phAdjustingEntryId: entry.phAdjustingEntryId ?? "",
    components:
      entry.components && entry.components.length > 0
        ? entry.components.map((c) => ({
            materialMasterEntryId: c.materialMasterEntryId,
            quantity: c.quantity,
            unit: c.unit
          }))
        : [{ materialMasterEntryId: "", quantity: "", unit: "Milliliter" }],
    nominalStrength: entry.nominalStrength ?? "",
    strengthUnit: entry.strengthUnit ?? "Normal",
    standardizationMode: entry.standardizationMode ?? "PrimaryStandard",
    standardEntryId: entry.standardEntryId ?? "",
    equivalenceMgPerMl: entry.equivalenceMgPerMl ?? "",
    referenceSolutionId: entry.referenceSolutionId ?? "",
    blankRequired: Boolean(entry.blankRequired),
    replicateCount: entry.replicateCount ?? "3",
    factorMin: entry.factorMin ?? "0.97000",
    factorMax: entry.factorMax ?? "1.03000",
    maxRsdPercent: entry.maxRsdPercent ?? "0.20",
    validityDays: entry.validityDays ?? "30"
  };
}

export interface ValidateSolutionFormOptions {
  isEditing: boolean;
  hasMultipleSections: boolean;
  editingEntryId?: number;
}

// Fields that show their validation message inline. Errors on the other
// sections (components, titrant) carry field: null and go to the dialog alert.
export type SolutionFieldKey =
  | "name"
  | "section"
  | "shelfLife"
  | "finalVolume"
  | "storage"
  | "instructions"
  | "phTarget"
  | "phTolerance"
  | "phAdjuster";

export interface SolutionFormError {
  field: SolutionFieldKey | null;
  message: string;
}

export function validateSolutionForm(
  form: SolutionFormState,
  options: ValidateSolutionFormOptions
): SolutionFormError | null {
  const fail = (field: SolutionFieldKey | null, message: string): SolutionFormError => ({ field, message });
  const trimmedName = form.name.trim();
  const trimmedStorage = form.storageCondition.trim();
  const trimmedInstructions = form.instructions.trim();

  if (!trimmedName) return fail("name", "Solution Name is required.");
  if (!options.isEditing && options.hasMultipleSections && !form.sectionId) {
    return fail("section", "Laboratory Section is required.");
  }

  const shelfLifeNum = Number(form.shelfLifeValue);
  if (!form.shelfLifeValue || isNaN(shelfLifeNum) || shelfLifeNum <= 0) {
    return fail("shelfLife", "Shelf Life Value must be greater than zero.");
  }

  const finalVolumeNum = Number(form.finalVolumeMl);
  if (!form.finalVolumeMl || isNaN(finalVolumeNum) || finalVolumeNum <= 0) {
    return fail("finalVolume", "Final Volume (mL) must be greater than zero.");
  }

  if (!trimmedStorage) return fail("storage", "Storage Condition is required.");
  if (!trimmedInstructions) return fail("instructions", "Preparation Instructions are required.");
  if (form.components.length === 0) return fail(null, "At least one recipe component is required.");

  const usedEntryIds = new Set<number>();
  for (let i = 0; i < form.components.length; i++) {
    const comp = form.components[i];
    const entryId = Number(comp.materialMasterEntryId);
    const qty = Number(comp.quantity);

    if (!comp.materialMasterEntryId || isNaN(entryId) || entryId <= 0) {
      return fail(null, `Component #${i + 1} must select a Material Master entry.`);
    }
    if (usedEntryIds.has(entryId)) {
      return fail(null, "Material entry is listed more than once in the components recipe.");
    }
    usedEntryIds.add(entryId);

    if (!comp.quantity || isNaN(qty) || qty <= 0) {
      return fail(null, `Component #${i + 1} quantity must be greater than zero.`);
    }
  }

  // pH validation
  const hasPhTarget = form.phTarget !== "";
  const phTargetNum = Number(form.phTarget);
  if (hasPhTarget && (isNaN(phTargetNum) || phTargetNum < 0 || phTargetNum > 14)) {
    return fail("phTarget", "pH Target must be between 0.00 and 14.00.");
  }

  if (form.phTolerance !== "") {
    if (!hasPhTarget) return fail("phTarget", "pH Target is required when pH Tolerance is specified.");
    const tolNum = Number(form.phTolerance);
    if (isNaN(tolNum) || tolNum <= 0) return fail("phTolerance", "pH Tolerance must be greater than zero.");
  }

  if (form.phAdjustingEntryId && !hasPhTarget) {
    return fail("phAdjuster", "pH Target is required when a pH adjusting reagent is selected.");
  }

  // Titrant validation (only when type is Titrant)
  if (form.type === "Titrant") {
    const strengthNum = Number(form.nominalStrength);
    if (!form.nominalStrength || isNaN(strengthNum) || strengthNum <= 0) {
      return fail(null, "Nominal Strength must be greater than zero for titrants.");
    }

    if (form.standardizationMode === "PrimaryStandard") {
      if (!form.standardEntryId) return fail(null, "Primary Standard entry is required for PrimaryStandard mode.");
      const equivNum = Number(form.equivalenceMgPerMl);
      if (!form.equivalenceMgPerMl || isNaN(equivNum) || equivNum <= 0) {
        return fail(null, "Equivalence (mg/mL) must be greater than zero.");
      }
    } else if (form.standardizationMode === "AgainstVolumetricSolution") {
      if (!form.referenceSolutionId) {
        return fail(null, "Reference Titrant is required for AgainstVolumetricSolution mode.");
      }
      if (options.editingEntryId && Number(form.referenceSolutionId) === options.editingEntryId) {
        return fail(null, "A titrant cannot standardize against itself.");
      }
    }

    const repNum = Number(form.replicateCount);
    if (!form.replicateCount || isNaN(repNum) || repNum < 1 || !Number.isInteger(repNum)) {
      return fail(null, "Replicate Count must be an integer of at least 1.");
    }

    const minNum = Number(form.factorMin);
    const maxNum = Number(form.factorMax);
    if (!form.factorMin || isNaN(minNum) || minNum <= 0) return fail(null, "Factor Min must be greater than zero.");
    if (!form.factorMax || isNaN(maxNum) || maxNum <= 0) return fail(null, "Factor Max must be greater than zero.");
    if (minNum > maxNum) return fail(null, "Factor Min cannot be greater than Factor Max.");

    const rsdNum = Number(form.maxRsdPercent);
    if (!form.maxRsdPercent || isNaN(rsdNum) || rsdNum <= 0) return fail(null, "Max RSD % must be greater than zero.");

    const valDaysNum = Number(form.validityDays);
    if (form.validityDays === "" || isNaN(valDaysNum) || valDaysNum < 0 || !Number.isInteger(valDaysNum)) {
      return fail(null, "Standardization Validity Days must be 0 or greater.");
    }
  }

  return null;
}

export function formToSaveRequest(
  form: SolutionFormState,
  resolvedSectionId: number | null
): SaveSolutionMasterRequest {
  const hasPhTarget = form.phTarget !== "";
  const isTitrant = form.type === "Titrant";

  return {
    name: form.name.trim(),
    type: form.type,
    shelfLifeValue: Number(form.shelfLifeValue),
    shelfLifeUnit: form.shelfLifeUnit,
    storageCondition: form.storageCondition.trim(),
    finalVolumeMl: Number(form.finalVolumeMl),
    instructions: form.instructions.trim(),
    sectionId: resolvedSectionId,
    components: form.components.map((c) => ({
      materialMasterEntryId: Number(c.materialMasterEntryId),
      quantity: Number(c.quantity),
      unit: c.unit
    })),
    phTarget: hasPhTarget ? Number(form.phTarget) : null,
    phTolerance: form.phTolerance !== "" ? Number(form.phTolerance) : null,
    phAdjustingEntryId: form.phAdjustingEntryId ? Number(form.phAdjustingEntryId) : null,
    nominalStrength: isTitrant && form.nominalStrength !== "" ? Number(form.nominalStrength) : null,
    strengthUnit: isTitrant ? form.strengthUnit : null,
    standardizationMode: isTitrant ? form.standardizationMode : null,
    standardEntryId:
      isTitrant && form.standardizationMode === "PrimaryStandard" && form.standardEntryId
        ? Number(form.standardEntryId)
        : null,
    equivalenceMgPerMl:
      isTitrant && form.standardizationMode === "PrimaryStandard" && form.equivalenceMgPerMl !== ""
        ? Number(form.equivalenceMgPerMl)
        : null,
    referenceSolutionId:
      isTitrant &&
      form.standardizationMode === "AgainstVolumetricSolution" &&
      form.referenceSolutionId
        ? Number(form.referenceSolutionId)
        : null,
    blankRequired: isTitrant ? Boolean(form.blankRequired) : false,
    replicateCount: isTitrant && form.replicateCount !== "" ? Number(form.replicateCount) : null,
    factorMin: isTitrant && form.factorMin !== "" ? Number(form.factorMin) : null,
    factorMax: isTitrant && form.factorMax !== "" ? Number(form.factorMax) : null,
    maxRsdPercent: isTitrant && form.maxRsdPercent !== "" ? Number(form.maxRsdPercent) : null,
    validityDays: isTitrant && form.validityDays !== "" ? Number(form.validityDays) : null
  };
}

export function getAvailableMaterialEntries(
  currentSectionId: number | null,
  materialMasters: MaterialMasterEntry[],
  editingEntry: SolutionMaster | null
): MaterialMasterEntry[] {
  if (!currentSectionId) return [];
  const list: MaterialMasterEntry[] = materialMasters.filter(
    (m) => m.sectionId === currentSectionId
  );

  if (editingEntry) {
    for (const comp of editingEntry.components) {
      if (!list.some((m) => m.id === comp.materialMasterEntryId)) {
        list.push({
          id: comp.materialMasterEntryId,
          code: comp.entryCode,
          name: comp.entryName,
          category: "Reagent",
          baseUnit: "Gram",
          isActive: false,
          sectionId: editingEntry.sectionId
        });
      }
    }
    if (
      editingEntry.phAdjustingEntryId &&
      !list.some((m) => m.id === editingEntry.phAdjustingEntryId)
    ) {
      list.push({
        id: editingEntry.phAdjustingEntryId,
        code: editingEntry.phAdjustingEntryCode ?? `Entry #${editingEntry.phAdjustingEntryId}`,
        name: editingEntry.phAdjustingEntryCode ?? `Entry #${editingEntry.phAdjustingEntryId}`,
        category: "Reagent",
        baseUnit: "Gram",
        isActive: false,
        sectionId: editingEntry.sectionId
      });
    }
    if (
      editingEntry.standardEntryId &&
      !list.some((m) => m.id === editingEntry.standardEntryId)
    ) {
      list.push({
        id: editingEntry.standardEntryId,
        code: editingEntry.standardEntryCode ?? `Standard #${editingEntry.standardEntryId}`,
        name: editingEntry.standardEntryCode ?? `Standard #${editingEntry.standardEntryId}`,
        category: "ReferenceStandard",
        baseUnit: "Gram",
        isActive: false,
        sectionId: editingEntry.sectionId
      });
    }
  }

  return list;
}

export function getAvailableReferenceTitrants(
  currentSectionId: number | null,
  solutions: SolutionMaster[],
  editingEntry: SolutionMaster | null
): SolutionMaster[] {
  if (!currentSectionId) return [];
  return solutions.filter(
    (s) =>
      s.type === "Titrant" &&
      s.isActive &&
      s.sectionId === currentSectionId &&
      (!editingEntry || s.id !== editingEntry.id)
  );
}
