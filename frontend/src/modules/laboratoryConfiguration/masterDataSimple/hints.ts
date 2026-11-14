import type { Hint } from "../../../components/FieldHint";

// Field hints for the equipment configuration (EquipmentPage). Wording is
// taken from guide chapter 30. Hints describe meaning and effect only.
const EQUIPMENT_CONFIG = {
  chapterId: "30-equipment",
  heading: "30.3 Laboratory Configuration: Equipment, Set Points, and Autoclave Programs"
};

export const hints = {
  equipmentCode: {
    text: "The equipment code. It must be unique; the name and code are trimmed when saved.",
    guide: EQUIPMENT_CONFIG
  },
  laboratorySection: {
    text: "Asked for only when your account belongs to more than one laboratory section. With one section it is used automatically.",
    guide: EQUIPMENT_CONFIG
  },
  reasonForChange: {
    text: "Required with every set point change. The change is added to the set point history with the previous value, the new value, the reason, your user and the time.",
    guide: EQUIPMENT_CONFIG
  }
} satisfies Record<string, Hint>;
