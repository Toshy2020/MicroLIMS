import type { TFunction } from "i18next";
import type { LabelEntityType } from "../../services/labelService";

export const ENTITY_TYPE_LABELS: Record<LabelEntityType, string> = {
  Sample: "Sample",
  MediaLot: "Media lot",
  SolutionPreparation: "Solution preparation",
  Cryovial: "Cryovial",
  MaterialStockLot: "Material stock lot",
  EquipmentAsset: "Equipment"
};

// Localised label for display inside a translated message. ENTITY_TYPE_LABELS
// stays English for the surfaces that are not translated yet.
export function entityTypeLabel(t: TFunction, type: LabelEntityType): string {
  return t(`components.entityTypes.${type}`, { defaultValue: ENTITY_TYPE_LABELS[type] ?? type });
}
