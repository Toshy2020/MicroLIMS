import type { TFunction } from "i18next";
import { i18n } from "../../../../i18n";
import type {
  CdsSoftware,
  ConfiguredEquipmentSummary,
  CreateEquipmentRequest,
  EquipmentType,
  UpdateEquipmentRequest
} from "../services/EquipmentConfigurationService";

const defaultT = (): TFunction => i18n.getFixedT("en", "masterData");

export type EquipmentWizardStep = "identity" | "typeSettings" | "documents";

export const EQUIPMENT_WIZARD_STEP_ORDER: readonly EquipmentWizardStep[] = [
  "identity",
  "typeSettings",
  "documents"
];

// Every field the equipment dialog holds. Text and number inputs are kept as
// strings exactly as typed; toEquipmentPayload converts them on save.
export interface EquipmentFormModel {
  name: string;
  code: string;
  type: string;
  location: string;
  vendor: string;
  cdsSoftware: string;
  connectionSettings: string;
  setPointTemperature: string;
  calibrationDueDate: string;
  equipmentInventoryId: number | null;
  // Laboratory section picked in the dialog ("" = none picked).
  sectionId: number | "";
}

// Facts about the surrounding screen that the save rules depend on.
export interface EquipmentModelContext {
  isEditing: boolean;
  // How many laboratory sections the signed-in user may add equipment to.
  sectionCount: number;
}

export function createEmptyEquipmentFormModel(overrides: Partial<EquipmentFormModel> = {}): EquipmentFormModel {
  return {
    name: "",
    code: "",
    type: "Incubator",
    location: "",
    vendor: "",
    cdsSoftware: "",
    connectionSettings: "",
    setPointTemperature: "",
    calibrationDueDate: "",
    equipmentInventoryId: null,
    sectionId: "",
    ...overrides
  };
}

// Numeric enum values from older responses are mapped to the type names.
const TYPE_BY_NUMBER: Record<string, string> = {
  "0": "Incubator",
  "1": "Autoclave",
  "2": "LafCabinet",
  "3": "BiologicalSafetyCabinet",
  "4": "WaterBath",
  "5": "Other",
  "6": "Hplc",
  "7": "PhMeter",
  "8": "Balance",
  "9": "IcpOes"
};

export const normalizeEquipmentTypeValue = (type: string | number): string =>
  TYPE_BY_NUMBER[String(type)] || String(type);

export function fromEquipment(eq: ConfiguredEquipmentSummary): EquipmentFormModel {
  return {
    name: eq.name,
    code: eq.code,
    type: normalizeEquipmentTypeValue(eq.type),
    location: eq.location || eq.inventoryLocation || "",
    vendor: eq.vendor || eq.manufacturerName || "",
    cdsSoftware: eq.cdsSoftware ? String(eq.cdsSoftware) : "",
    connectionSettings: eq.connectionSettings || "",
    setPointTemperature: eq.setPointTemperature != null ? String(eq.setPointTemperature) : "",
    calibrationDueDate: eq.calibrationDueDate ? eq.calibrationDueDate.slice(0, 10) : "",
    equipmentInventoryId: eq.equipmentInventoryId ?? null,
    sectionId: eq.sectionId ?? ""
  };
}

// Changing the type clears the CDS software unless the new type is HPLC, so a
// value chosen for HPLC is never carried to another type.
export function withEquipmentType(model: EquipmentFormModel, nextType: string): EquipmentFormModel {
  return {
    ...model,
    type: nextType,
    cdsSoftware: nextType === "Hplc" ? model.cdsSoftware : ""
  };
}

export const EQUIPMENT_REQUIRED_MESSAGE = "Name, Code, and Type are required.";
export const EQUIPMENT_CDS_MESSAGE = "CDS Software is required for HPLC equipment.";
export const EQUIPMENT_SECTION_MESSAGE = "Laboratory section is required.";

export function validateStep(
  step: EquipmentWizardStep,
  model: EquipmentFormModel,
  ctx: EquipmentModelContext,
  t: TFunction = defaultT()
): Record<string, string> {
  const errors: Record<string, string> = {};
  if (step === "identity") {
    if (!model.name.trim() || !model.code.trim() || !model.type) {
      errors.required = t("equipmentWizard.errors.required");
    }
    if (!ctx.isEditing && ctx.sectionCount > 1 && !model.sectionId) {
      errors.section = t("equipmentWizard.errors.sectionRequired");
    }
  } else if (step === "typeSettings") {
    if (model.type === "Hplc" && !model.cdsSoftware) {
      errors.cdsSoftware = t("equipmentWizard.errors.cdsRequired");
    }
  }
  return errors;
}

// The single message the save button shows, in the order the old dialog
// checked: required fields, then CDS software, then laboratory section.
export function firstEquipmentError(
  model: EquipmentFormModel,
  ctx: EquipmentModelContext,
  t: TFunction = defaultT()
): string | null {
  const identity = validateStep("identity", model, ctx, t);
  const settings = validateStep("typeSettings", model, ctx, t);
  return identity.required ?? settings.cdsSoftware ?? identity.section ?? null;
}

export function toEquipmentPayload(
  model: EquipmentFormModel,
  ctx: EquipmentModelContext
): CreateEquipmentRequest | UpdateEquipmentRequest {
  return {
    name: model.name.trim(),
    code: model.code.trim(),
    type: model.type as EquipmentType,
    location: model.location.trim() || null,
    vendor: model.vendor.trim() || null,
    cdsSoftware: model.type === "Hplc" && model.cdsSoftware ? (model.cdsSoftware as CdsSoftware) : null,
    connectionSettings: model.connectionSettings.trim() || null,
    setPointTemperature: model.setPointTemperature !== "" ? Number(model.setPointTemperature) : null,
    calibrationDueDate: model.calibrationDueDate || null,
    equipmentInventoryId: model.equipmentInventoryId ?? null,
    ...(!ctx.isEditing && ctx.sectionCount > 1 && model.sectionId !== ""
      ? { sectionId: Number(model.sectionId) }
      : {})
  };
}
