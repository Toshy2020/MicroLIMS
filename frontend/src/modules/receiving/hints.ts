import type { Hint } from "../../components/FieldHint";

// Field hints for the sample details correction dialog. Wording is taken from
// guide chapters 11 and 12. Hints describe meaning and effect only.
const RECEIVING = {
  chapterId: "12-sample-receiving",
  heading: "12.1 Receiving Product, Raw Material, and Packaging Material Samples"
};
const CORRECTION = { chapterId: "11-sample-management", heading: "11.3 Correcting Sample Details" };

export const hints = {
  batchNumber: {
    text: "Required. The batch number of the sample; a save is refused without it.",
    guide: RECEIVING
  },
  controlNumber: {
    text: "Required. The control number of the sample; a save is refused without it.",
    guide: RECEIVING
  },
  storageTime: {
    text: "Asked for only when the storage condition is Refrigerator. Enter the time in hours.",
    guide: CORRECTION
  },
  reasonForCorrection: {
    text: "Required. The reason is recorded with your electronic signature and in the sample's audit trail.",
    guide: CORRECTION
  }
} satisfies Record<string, Hint>;
