import type { TFunction } from "i18next";
import type { Hint } from "../../../components/FieldHint";
import { i18n } from "../../../i18n";

// Field hints for the equipment configuration (EquipmentPage). Wording is
// taken from guide chapter 30. Hints describe meaning and effect only.
const EQUIPMENT_CONFIG = {
  chapterId: "30-equipment",
  heading: "30.3 Laboratory Configuration: Equipment, Set Points, and Autoclave Programs"
};

export function getEquipmentHints(t: TFunction): Record<"equipmentCode" | "laboratorySection" | "reasonForChange", Hint> {
  return {
    equipmentCode: {
      text: t("equipmentHints.equipmentCode"),
      guide: EQUIPMENT_CONFIG
    },
    laboratorySection: {
      text: t("equipmentHints.laboratorySection"),
      guide: EQUIPMENT_CONFIG
    },
    reasonForChange: {
      text: t("equipmentHints.reasonForChange"),
      guide: EQUIPMENT_CONFIG
    }
  };
}

export const hints = getEquipmentHints(i18n.getFixedT("en", "masterData"));
