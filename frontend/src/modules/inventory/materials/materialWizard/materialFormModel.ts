import type { SaveMaterialPayload } from "../services/MaterialService";
import {
  LEGACY_MATERIAL_TYPES,
  MATERIAL_TYPE_LABELS,
  type MaterialFormState,
  type MaterialItem,
  type MaterialType
} from "../types/materialTypes";
import type { MaterialMasterCategory } from "../../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService";

export type MaterialWizardStep = "material" | "lot" | "dates";

export const MATERIAL_WIZARD_STEP_ORDER: readonly MaterialWizardStep[] = ["material", "lot", "dates"];

// Every field the add / edit material dialog holds. Number inputs are kept as typed;
// toMaterialPayload converts them on save.
export interface MaterialFormModel extends MaterialFormState {
  // Other materials: the category chosen at receipt (not sent; it drives the master entry list and the rules).
  categoryId: number | "";
  // Laboratory section picked when the user has several ("" = none picked).
  sectionId: number | "";
}

// Facts about the surrounding screen that the save rules depend on.
export interface MaterialModelContext {
  isEditing: boolean;
  // How many laboratory sections the signed-in user may receive stock into.
  sectionCount: number;
  // The chosen category's rules, when an Other lot is being received (null when none is chosen).
  category: { name: string; expiryRequired: boolean } | null;
}

// Types a new lot can still be received under. Supplements, discs, kits and the like are now
// categories of Other materials; WorkingStandard lots come from qualifications only.
export const NEW_LOT_TYPES = (Object.keys(MATERIAL_TYPE_LABELS) as MaterialType[]).filter(
  (t) => t !== "WorkingStandard" && !LEGACY_MATERIAL_TYPES.includes(t)
);

// Types offered for a new lot: what the server lists for the lab, plus the Other material path.
export function newLotTypesFor(options: { builtIn: MaterialType[] } | null): MaterialType[] {
  if (!options) return NEW_LOT_TYPES;
  const listed = options.builtIn.filter((t) => !LEGACY_MATERIAL_TYPES.includes(t) && t !== "WorkingStandard");
  return listed.includes("Other") ? listed : [...listed, "Other"];
}

// A calendar date ("YYYY-MM-DD") as the date fields show it. These are dates, not instants, so
// there is no time zone to convert or display.
export function formatCalendarDate(value: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : value;
}

export const typeOptionLabel = (type: MaterialType): string =>
  type === "Other" ? "Other material" : MATERIAL_TYPE_LABELS[type] ?? type;

export const isMasterLinkedType = (type: MaterialType): boolean =>
  type === "Chemical" ||
  type === "Indicator" ||
  type === "ReferenceStandard" ||
  type === "PrimaryStandard" ||
  type === "Other";

export const getMasterCategoryForType = (type: MaterialType): MaterialMasterCategory | null => {
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

export function createEmptyMaterialFormModel(
  receivingDate: string,
  overrides: Partial<MaterialFormModel> = {}
): MaterialFormModel {
  return {
    materialType: "DehydratedMedia",
    customType: "",
    materialName: "",
    manufacturerName: "",
    batchNumber: "",
    receivingDate,
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
    materialMasterEntryId: null,
    categoryId: "",
    sectionId: "",
    ...overrides
  };
}

export function fromMaterial(item: MaterialItem): MaterialFormModel {
  return {
    materialType: item.materialType,
    customType: item.customType ?? "",
    materialName: item.materialName,
    manufacturerName: item.manufacturerName ?? "",
    batchNumber: item.batchNumber,
    receivingDate: item.receivingDate?.slice(0, 10) ?? "",
    expiryDate: item.expiryDate?.slice(0, 10) ?? "",
    code: item.code ?? "",
    location: item.location,
    storageCondition: item.storageCondition ?? "",
    quantityReceived: item.quantityReceived,
    unit: item.unit,
    minimumStockLevel: item.minimumStockLevel ?? "",
    purity: item.purity != null ? item.purity : "",
    moisturePercent: item.moisturePercent != null ? item.moisturePercent : "",
    atccNumber: item.atccNumber ?? "",
    organismId: item.organismId ?? null,
    referenceOrganismId: item.referenceOrganismId ?? null,
    mediaProductId: item.mediaProductId ?? null,
    materialMasterEntryId: item.materialMasterEntryId ?? null,
    categoryId: "",
    sectionId: ""
  };
}

// Changing the type clears what only the old type uses, so a hidden value is never saved.
// defaultUnit is the unit the server suggests for the new type (null keeps the unit already chosen).
export function withMaterialType(
  model: MaterialFormModel,
  type: MaterialType,
  defaultUnit: MaterialFormModel["unit"] | null,
  isEditing: boolean
): MaterialFormModel {
  const nextCategory = getMasterCategoryForType(type);
  const prevCategory = getMasterCategoryForType(model.materialType);
  const keepMaster = Boolean(prevCategory && nextCategory && prevCategory === nextCategory);
  return {
    ...model,
    materialType: type,
    customType: "",
    ...(defaultUnit ? { unit: defaultUnit } : {}),
    storageCondition: "",
    mediaProductId: type === "DehydratedMedia" ? model.mediaProductId : null,
    referenceOrganismId: type === "LyophilizedMicroorganism" ? model.referenceOrganismId : null,
    purity: type === "ReferenceStandard" || type === "PrimaryStandard" ? model.purity : "",
    moisturePercent: type === "ReferenceStandard" ? model.moisturePercent : "",
    materialMasterEntryId: keepMaster ? model.materialMasterEntryId : null,
    ...(keepMaster ? {} : isMasterLinkedType(type) && !isEditing ? { materialName: "", code: "" } : {}),
    categoryId: ""
  };
}

export const MATERIAL_MEDIA_MESSAGE = "Choose the configured media product for this dehydrated media.";
export const MATERIAL_STRAIN_MESSAGE = "Choose the reference organism for this lyophilized microorganism.";
export const MATERIAL_CATEGORY_MESSAGE = "Choose the material category for this lot.";
export const MATERIAL_MASTER_MESSAGE = "Choose the material master entry for this lot.";
export const MATERIAL_REQUIRED_MESSAGE =
  "Material name, batch/lot number, receiving date, storage location, and quantity received are required.";
export const MATERIAL_QUANTITY_MESSAGE = "Quantity received must be greater than zero.";
export const MATERIAL_SECTION_MESSAGE = "Laboratory section is required.";
export const MATERIAL_PURITY_RANGE_MESSAGE = "Purity must be greater than 0 and less than or equal to 100.";
export const MATERIAL_MOISTURE_MESSAGE = "Moisture content must be at least 0 and below 100.";

const isStandard = (m: MaterialFormModel) => m.materialType === "ReferenceStandard" || m.materialType === "PrimaryStandard";

interface Check {
  step: MaterialWizardStep;
  field: string;
  message: (model: MaterialFormModel, ctx: MaterialModelContext) => string | null;
}

const hasQuantity = (m: MaterialFormModel) => m.quantityReceived !== "" && m.quantityReceived != null;

// The checks in the order the old dialog ran them; the first failing message is the one it showed.
const CHECKS: readonly Check[] = [
  {
    step: "material",
    field: "mediaProductId",
    message: (m) => (m.materialType === "DehydratedMedia" && !m.mediaProductId ? MATERIAL_MEDIA_MESSAGE : null)
  },
  {
    step: "material",
    field: "referenceOrganismId",
    message: (m, c) =>
      !c.isEditing && m.materialType === "LyophilizedMicroorganism" && !m.referenceOrganismId
        ? MATERIAL_STRAIN_MESSAGE
        : null
  },
  {
    step: "material",
    field: "categoryId",
    message: (m, c) => (!c.isEditing && m.materialType === "Other" && !m.categoryId ? MATERIAL_CATEGORY_MESSAGE : null)
  },
  {
    step: "material",
    field: "materialMasterEntryId",
    message: (m, c) =>
      !c.isEditing && isMasterLinkedType(m.materialType) && !m.materialMasterEntryId ? MATERIAL_MASTER_MESSAGE : null
  },
  {
    // Mirrors the category rule for the user's benefit; the server enforces it.
    step: "dates",
    field: "expiryDate",
    message: (m, c) =>
      !c.isEditing && c.category?.expiryRequired && !m.expiryDate ? `${c.category.name} lots need an expiry date.` : null
  },
  {
    step: "material",
    field: "purity",
    message: (m) =>
      isStandard(m) && (m.purity === "" || m.purity == null)
        ? m.materialType === "PrimaryStandard"
          ? "Purity percentage is required for primary standards."
          : "Purity percentage is required for reference standards."
        : null
  },
  {
    step: "material",
    field: "purity",
    message: (m) => {
      if (!isStandard(m)) return null;
      const p = Number(m.purity);
      return isNaN(p) || p <= 0 || p > 100 ? MATERIAL_PURITY_RANGE_MESSAGE : null;
    }
  },
  {
    step: "material",
    field: "moisturePercent",
    message: (m) => {
      if (!isStandard(m) || m.materialType !== "ReferenceStandard") return null;
      if (m.moisturePercent === "" || m.moisturePercent == null) return null;
      const v = Number(m.moisturePercent);
      return isNaN(v) || v < 0 || v >= 100 ? MATERIAL_MOISTURE_MESSAGE : null;
    }
  },
  {
    step: "material",
    field: "materialName",
    message: (m) => (!m.materialName.trim() ? MATERIAL_REQUIRED_MESSAGE : null)
  },
  {
    step: "lot",
    field: "batchNumber",
    message: (m) => (!m.batchNumber.trim() ? MATERIAL_REQUIRED_MESSAGE : null)
  },
  {
    step: "dates",
    field: "receivingDate",
    message: (m) => (!m.receivingDate ? MATERIAL_REQUIRED_MESSAGE : null)
  },
  {
    step: "lot",
    field: "location",
    message: (m) => (!m.location.trim() ? MATERIAL_REQUIRED_MESSAGE : null)
  },
  {
    step: "lot",
    field: "quantityReceived",
    message: (m) => (!hasQuantity(m) ? MATERIAL_REQUIRED_MESSAGE : null)
  },
  {
    step: "lot",
    field: "quantityReceived",
    message: (m) => (hasQuantity(m) && Number(m.quantityReceived) <= 0 ? MATERIAL_QUANTITY_MESSAGE : null)
  },
  {
    step: "material",
    field: "section",
    message: (m, c) => (!c.isEditing && c.sectionCount > 1 && !m.sectionId ? MATERIAL_SECTION_MESSAGE : null)
  }
];

// Errors of one step, keyed by field name. The first message per field wins, in the old order.
export function validateStep(
  step: MaterialWizardStep,
  model: MaterialFormModel,
  ctx: MaterialModelContext
): Record<string, string> {
  const errors: Record<string, string> = {};
  for (const check of CHECKS) {
    if (check.step !== step || check.field in errors) continue;
    const message = check.message(model, ctx);
    if (message) errors[check.field] = message;
  }
  return errors;
}

// The single message the old dialog's save button showed: the first failing check in its order.
export function firstMaterialError(model: MaterialFormModel, ctx: MaterialModelContext): string | null {
  for (const check of CHECKS) {
    const message = check.message(model, ctx);
    if (message) return message;
  }
  return null;
}

export function toMaterialPayload(model: MaterialFormModel, ctx: MaterialModelContext): SaveMaterialPayload {
  const isLyophilized = model.materialType === "LyophilizedMicroorganism";
  return {
    materialType: model.materialType,
    // Only the stored custom type of an old lot is ever sent back; new lots take no free-text type.
    customType: ctx.isEditing && model.materialType === "Other" && model.customType.trim() ? model.customType.trim() : null,
    materialName: model.materialName.trim(),
    manufacturerName: model.manufacturerName.trim(),
    batchNumber: model.batchNumber.trim(),
    receivingDate: model.receivingDate,
    expiryDate: model.expiryDate || null,
    code: model.code.trim() || null,
    location: model.location.trim(),
    storageCondition: model.storageCondition.trim() || null,
    quantityReceived: Number(model.quantityReceived),
    unit: model.unit,
    minimumStockLevel: model.minimumStockLevel === "" ? null : Number(model.minimumStockLevel),
    // With a strain the server derives the organism and ATCC number; only an old lot without one sends its stored values back.
    atccNumber: isLyophilized && !model.referenceOrganismId ? model.atccNumber.trim() || null : null,
    organismId: isLyophilized && !model.referenceOrganismId ? model.organismId || null : null,
    referenceOrganismId: isLyophilized ? model.referenceOrganismId : null,
    mediaProductId: model.materialType === "DehydratedMedia" ? model.mediaProductId : null,
    purity: isStandard(model) && model.purity !== "" ? Number(model.purity) : null,
    moisturePercent:
      model.materialType === "ReferenceStandard" && model.moisturePercent !== "" && model.moisturePercent != null
        ? Number(model.moisturePercent)
        : null,
    materialMasterEntryId:
      isMasterLinkedType(model.materialType) || LEGACY_MATERIAL_TYPES.includes(model.materialType)
        ? model.materialMasterEntryId
        : null,
    ...(!ctx.isEditing && ctx.sectionCount > 1 && model.sectionId !== "" ? { sectionId: Number(model.sectionId) } : {})
  };
}
