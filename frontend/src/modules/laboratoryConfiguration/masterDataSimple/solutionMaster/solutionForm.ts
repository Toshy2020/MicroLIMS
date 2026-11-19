import type { TFunction } from "i18next";
import { i18n } from "../../../../i18n";
import {
  SolutionMaster,
  SolutionType,
  ShelfLifeUnit,
  SolutionComponentUnit,
  SolutionComponentKind,
  SolutionComponentInput,
  isGeneralSolutionType,
  TitrantStrengthUnit,
  StandardizationMode,
  SaveSolutionMasterRequest
} from "../services/SolutionMasterService";
import { MaterialMasterEntry } from "../services/MaterialMasterService";

export interface ComponentRowState {
  kind: SolutionComponentKind;
  materialMasterEntryId: string | number;
  sourceSolutionMasterId: string | number;
  text: string;
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
  // Diluent / Reagent / Neutralizer only
  sterilizationRequired: boolean;
  releaseRequired: boolean;

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
  { value: "Reagent", label: "Reagent" },
  { value: "Neutralizer", label: "Neutralizer" },
  { value: "Titrant", label: "Titrant" }
];

export const COMPONENT_KIND_OPTIONS: Array<{ value: SolutionComponentKind; label: string }> = [
  { value: "Material", label: "Material" },
  { value: "PreparedSolution", label: "Prepared solution" },
  { value: "Text", label: "Text line" }
];

export const MAX_TEXT_LINE_LENGTH = 200;

export const solutionTypeLabel = (type: SolutionType): string =>
  SOLUTION_TYPE_OPTIONS.find((o) => o.value === type)?.label ?? type;

export function createEmptyComponentRow(kind: SolutionComponentKind = "Material"): ComponentRowState {
  return {
    kind,
    materialMasterEntryId: "",
    sourceSolutionMasterId: "",
    text: "",
    quantity: "",
    unit: "Milliliter"
  };
}

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
    sterilizationRequired: false,
    // Release is on by default for the Microbiology types (plan 3.1).
    releaseRequired: true,
    phTarget: "",
    phTolerance: "",
    phAdjustingEntryId: "",
    components: [createEmptyComponentRow()],
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
    sterilizationRequired: Boolean(entry.sterilizationRequired),
    releaseRequired: Boolean(entry.releaseRequired),
    phTarget: entry.phTarget ?? "",
    phTolerance: entry.phTolerance ?? "",
    phAdjustingEntryId: entry.phAdjustingEntryId ?? "",
    components:
      entry.components && entry.components.length > 0
        ? entry.components.map((c) => ({
            kind: c.kind ?? "Material",
            materialMasterEntryId: c.materialMasterEntryId ?? "",
            sourceSolutionMasterId: c.sourceSolutionMasterId ?? "",
            text: c.text ?? "",
            quantity: c.quantity ?? "",
            unit: c.unit ?? "Milliliter"
          }))
        : [createEmptyComponentRow()],
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

const defaultT = (): TFunction => i18n.getFixedT("en", "masterData");

export function validateSolutionForm(
  form: SolutionFormState,
  options: ValidateSolutionFormOptions,
  t: TFunction = defaultT()
): SolutionFormError | null {
  const fail = (field: SolutionFieldKey | null, message: string): SolutionFormError => ({ field, message });
  const trimmedName = form.name.trim();
  const trimmedStorage = form.storageCondition.trim();
  const trimmedInstructions = form.instructions.trim();

  if (!trimmedName) return fail("name", t("solutionMaster.errors.nameRequired"));
  if (!options.isEditing && options.hasMultipleSections && !form.sectionId) {
    return fail("section", t("solutionMaster.errors.sectionRequired"));
  }

  const shelfLifeNum = Number(form.shelfLifeValue);
  if (!form.shelfLifeValue || isNaN(shelfLifeNum) || shelfLifeNum <= 0) {
    return fail("shelfLife", t("solutionMaster.errors.shelfLifePositive"));
  }

  const finalVolumeNum = Number(form.finalVolumeMl);
  if (!form.finalVolumeMl || isNaN(finalVolumeNum) || finalVolumeNum <= 0) {
    return fail("finalVolume", t("solutionMaster.errors.finalVolumePositive"));
  }

  if (!trimmedStorage) return fail("storage", t("solutionMaster.errors.storageRequired"));
  if (!trimmedInstructions) return fail("instructions", t("solutionMaster.errors.instructionsRequired"));
  if (form.components.length === 0) return fail(null, t("solutionMaster.errors.componentsRequired"));

  const usedEntryIds = new Set<number>();
  const usedSourceIds = new Set<number>();
  for (let i = 0; i < form.components.length; i++) {
    const comp = form.components[i];
    const index = i + 1;

    if (comp.kind === "Text") {
      const text = comp.text.trim();
      if (!text) return fail(null, t("solutionMaster.errors.componentTextRequired", { index }));
      if (text.length > MAX_TEXT_LINE_LENGTH) {
        return fail(null, t("solutionMaster.errors.componentTextMax", { index, max: MAX_TEXT_LINE_LENGTH }));
      }
      continue;
    }

    const qty = Number(comp.quantity);
    if (comp.kind === "PreparedSolution") {
      const sourceId = Number(comp.sourceSolutionMasterId);
      if (!comp.sourceSolutionMasterId || isNaN(sourceId) || sourceId <= 0) {
        return fail(null, t("solutionMaster.errors.componentSourceRequired", { index }));
      }
      if (usedSourceIds.has(sourceId)) {
        return fail(null, t("solutionMaster.errors.duplicateSource"));
      }
      usedSourceIds.add(sourceId);
    } else {
      const entryId = Number(comp.materialMasterEntryId);
      if (!comp.materialMasterEntryId || isNaN(entryId) || entryId <= 0) {
        return fail(null, t("solutionMaster.errors.componentMaterialRequired", { index }));
      }
      if (usedEntryIds.has(entryId)) {
        return fail(null, t("solutionMaster.errors.duplicateMaterial"));
      }
      usedEntryIds.add(entryId);
    }

    if (!comp.quantity || isNaN(qty) || qty <= 0) {
      return fail(null, t("solutionMaster.errors.componentQuantityPositive", { index }));
    }
  }

  // pH validation
  const hasPhTarget = form.phTarget !== "";
  const phTargetNum = Number(form.phTarget);
  if (hasPhTarget && (isNaN(phTargetNum) || phTargetNum < 0 || phTargetNum > 14)) {
    return fail("phTarget", t("solutionMaster.errors.phTargetRange"));
  }

  if (form.phTolerance !== "") {
    if (!hasPhTarget) return fail("phTarget", t("solutionMaster.errors.phTargetForTolerance"));
    const tolNum = Number(form.phTolerance);
    if (isNaN(tolNum) || tolNum <= 0) return fail("phTolerance", t("solutionMaster.errors.phTolerancePositive"));
  }

  if (form.phAdjustingEntryId && !hasPhTarget) {
    return fail("phAdjuster", t("solutionMaster.errors.phTargetForAdjuster"));
  }

  // Titrant validation (only when type is Titrant)
  if (form.type === "Titrant") {
    const strengthNum = Number(form.nominalStrength);
    if (!form.nominalStrength || isNaN(strengthNum) || strengthNum <= 0) {
      return fail(null, t("solutionMaster.errors.nominalStrengthPositive"));
    }

    if (form.standardizationMode === "PrimaryStandard") {
      if (!form.standardEntryId) return fail(null, t("solutionMaster.errors.primaryStandardRequired"));
      const equivNum = Number(form.equivalenceMgPerMl);
      if (!form.equivalenceMgPerMl || isNaN(equivNum) || equivNum <= 0) {
        return fail(null, t("solutionMaster.errors.equivalencePositive"));
      }
    } else if (form.standardizationMode === "AgainstVolumetricSolution") {
      if (!form.referenceSolutionId) {
        return fail(null, t("solutionMaster.errors.referenceTitrantRequired"));
      }
      if (options.editingEntryId && Number(form.referenceSolutionId) === options.editingEntryId) {
        return fail(null, t("solutionMaster.errors.selfStandardization"));
      }
    }

    const repNum = Number(form.replicateCount);
    if (!form.replicateCount || isNaN(repNum) || repNum < 1 || !Number.isInteger(repNum)) {
      return fail(null, t("solutionMaster.errors.replicateInteger"));
    }

    const minNum = Number(form.factorMin);
    const maxNum = Number(form.factorMax);
    if (!form.factorMin || isNaN(minNum) || minNum <= 0) return fail(null, t("solutionMaster.errors.factorMinPositive"));
    if (!form.factorMax || isNaN(maxNum) || maxNum <= 0) return fail(null, t("solutionMaster.errors.factorMaxPositive"));
    if (minNum > maxNum) return fail(null, t("solutionMaster.errors.factorMinMaxOrder"));

    const rsdNum = Number(form.maxRsdPercent);
    if (!form.maxRsdPercent || isNaN(rsdNum) || rsdNum <= 0) return fail(null, t("solutionMaster.errors.maxRsdPositive"));

    const valDaysNum = Number(form.validityDays);
    if (form.validityDays === "" || isNaN(valDaysNum) || valDaysNum < 0 || !Number.isInteger(valDaysNum)) {
      return fail(null, t("solutionMaster.errors.validityDaysNonNegative"));
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
  const isGeneral = isGeneralSolutionType(form.type);

  return {
    name: form.name.trim(),
    type: form.type,
    shelfLifeValue: Number(form.shelfLifeValue),
    shelfLifeUnit: form.shelfLifeUnit,
    storageCondition: form.storageCondition.trim(),
    finalVolumeMl: Number(form.finalVolumeMl),
    instructions: form.instructions.trim(),
    sectionId: resolvedSectionId,
    sterilizationRequired: isGeneral ? form.sterilizationRequired : false,
    releaseRequired: isGeneral ? form.releaseRequired : false,
    components: form.components.map((c): SolutionComponentInput => {
      if (c.kind === "Text") {
        return { kind: "Text", text: c.text.trim() };
      }
      if (c.kind === "PreparedSolution") {
        return {
          kind: "PreparedSolution",
          sourceSolutionMasterId: Number(c.sourceSolutionMasterId),
          quantity: Number(c.quantity),
          unit: c.unit
        };
      }
      return {
        kind: "Material",
        materialMasterEntryId: Number(c.materialMasterEntryId),
        quantity: Number(c.quantity),
        unit: c.unit
      };
    }),
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
  // Master entries are app-wide: every lab can use every entry.
  const list: MaterialMasterEntry[] = [...materialMasters];

  if (editingEntry) {
    for (const comp of editingEntry.components) {
      if (comp.materialMasterEntryId && !list.some((m) => m.id === comp.materialMasterEntryId)) {
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

// Active recipes of the same laboratory that can be used as a "prepared solution"
// component (general types only), excluding the recipe being edited. A source already
// kept on the edited recipe stays listed even if it has since been deactivated.
export function getAvailableSourceSolutions(
  currentSectionId: number | null,
  solutions: SolutionMaster[],
  editingEntry: SolutionMaster | null
): SolutionMaster[] {
  if (!currentSectionId) return [];
  const keptIds = new Set(
    (editingEntry?.components ?? [])
      .map((c) => c.sourceSolutionMasterId)
      .filter((id): id is number => typeof id === "number")
  );
  return solutions.filter(
    (s) =>
      s.sectionId === currentSectionId &&
      isGeneralSolutionType(s.type) &&
      (s.isActive || keptIds.has(s.id)) &&
      (!editingEntry || s.id !== editingEntry.id)
  );
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
