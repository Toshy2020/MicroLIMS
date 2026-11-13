import type { LabelEntityType } from "../../services/labelService";

export const ENTITY_TYPE_LABELS: Record<LabelEntityType, string> = {
  Sample: "Sample",
  MediaLot: "Media lot",
  SolutionPreparation: "Solution preparation",
  Cryovial: "Cryovial",
  MaterialStockLot: "Material stock lot",
  EquipmentAsset: "Equipment"
};
