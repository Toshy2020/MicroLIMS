import type { LaboratorySection } from "../../../../services/laboratorySectionService";
import type { EquipmentItem } from "../../../inventory/equipment/types/equipmentTypes";

// Reference data the wizard steps read: the sections the user may add equipment
// to and the asset register entries that can be linked.
export interface EquipmentWizardLookup {
  mySections: LaboratorySection[];
  inventory: EquipmentItem[];
}

export const assetLabel = (item: EquipmentItem): string => `${item.code} — ${item.instrumentType}`;
